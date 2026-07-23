using System.Text.Json;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Telemetry;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Transport;

public static class OccamMcpServerRegistration
{
    /// <summary>Full core-tool catalog (profile <c>full</c>). Runtime exposure may be narrower via <c>OCCAM_PROFILE</c>.</summary>
    public static readonly string[] OccamToolNames =
    [
        "occam_client_capabilities",
        "occam_transcode",
        "occam_probe",
        "occam_digest",
        "occam_playbook_resolve",
        "occam_map",
        "occam_playbook_heal",
        "occam_playbook_save",
        "occam_extract_knowledge",
        "occam_search",
        "occam_verify",
        "occam_claim_check",
        "occam_attest",
        "occam_playbook_lint",
        "occam_dataset_export",
    ];

    public static IMcpServerBuilder AddOccamMcpServer(this IServiceCollection services)
    {
        services.AddOccamCore();
        var workerPaths = WorkerPaths.Resolve();
        OccamLogger.TryWriteStartupBanner(workerPaths);

        // Pre-warm the HTTP extract daemon in the background so the FIRST transcode is already warm.
        // The daemon (HttpDaemonHost) amortizes Node startup + module load across requests, but it
        // otherwise spawns lazily on the first request — making only that call pay the cold start.
        // Best-effort and non-blocking; skipped when the daemon is off (OCCAM_HTTP_DAEMON=0) or when
        // OCCAM_HTTP_DAEMON_PREWARM=0.
        if (HttpDaemonHost.IsEnabled
            && OccamMcp.Core.Configuration.OccamEnvironment.GetFlag("OCCAM_HTTP_DAEMON_PREWARM", defaultValue: true))
        {
            System.Threading.Tasks.Task.Run(() =>
            {
                try { HttpDaemonHost.TryEnsureRunning(workerPaths); }
                catch { /* pre-warm is best-effort; the first real call spawns it if this failed */ }
            });
        }

        var profile = OccamToolProfile.Resolve();
        var instructions = OccamServerInstructions.TextFor(profile);

        var builder = services
            // Surface the capability + decision guide to the consuming model on initialize, so the
            // off-by-default power features are discoverable instead of invisible behind 20 params.
            .AddMcpServer(options => options.ServerInstructions = instructions)
            // Map MEAI required-parameter / declared-type binding failures to typed invalid_arguments
            // before McpServerImpl.ToolCallError logs them as unhandled exceptions (EventId 1433779783).
            .WithRequestFilters(filters => filters.AddCallToolFilter(next => async (request, cancellationToken) =>
            {
                try
                {
                    return await next(request, cancellationToken).ConfigureAwait(false);
                }
                catch (Exception ex) when (McpArgumentBindingGuard.IsClientInputBindingFailure(ex))
                {
                    var toolName = request.Params?.Name;
                    McpArgumentBindingGuard.LogBindingRejection(toolName, ex);
                    return McpArgumentBindingGuard.ToTypedInvalidArgumentsResult(ex, toolName);
                }
            }));

        // Role-scoped core tools (OCCAM_PROFILE). Default full = entire OccamToolNames catalog.
        if (OccamToolProfile.IsExposed("occam_client_capabilities", profile))
            builder = builder.WithTools<OccamClientCapabilitiesTool>();
        if (OccamToolProfile.IsExposed("occam_transcode", profile))
            builder = builder.WithTools<OccamTranscodeTool>();
        if (OccamToolProfile.IsExposed("occam_probe", profile))
            builder = builder.WithTools<OccamProbeTool>();
        if (OccamToolProfile.IsExposed("occam_digest", profile))
        {
            services.AddTransient<OccamDigestTool>();
            var method = typeof(OccamDigestTool).GetMethod(nameof(OccamDigestTool.Digest))
                ?? throw new InvalidOperationException("Could not resolve occam_digest handler.");
            var digestTool = McpServerTool.Create(
                method,
                context => context.Services!.GetRequiredService<OccamDigestTool>());
            digestTool.ProtocolTool.InputSchema = WithDigestUrlsUnion(
                digestTool.ProtocolTool.InputSchema);
            builder = builder.WithTools([digestTool]);
        }
        if (OccamToolProfile.IsExposed("occam_playbook_resolve", profile))
            builder = builder.WithTools<OccamPlaybookResolveTool>();
        if (OccamToolProfile.IsExposed("occam_map", profile))
            builder = builder.WithTools<OccamMapTool>();
        if (OccamToolProfile.IsExposed("occam_playbook_heal", profile))
            builder = builder.WithTools<OccamPlaybookHealTool>();
        if (OccamToolProfile.IsExposed("occam_playbook_save", profile))
            builder = builder.WithTools<OccamPlaybookSaveTool>();
        if (OccamToolProfile.IsExposed("occam_extract_knowledge", profile))
            builder = builder.WithTools<OccamExtractKnowledgeTool>();
        if (OccamToolProfile.IsExposed("occam_search", profile))
            builder = builder.WithTools<OccamSearchTool>();
        if (OccamToolProfile.IsExposed("occam_verify", profile))
            builder = builder.WithTools<OccamVerifyTool>();
        if (OccamToolProfile.IsExposed("occam_claim_check", profile))
            builder = builder.WithTools<OccamClaimCheckTool>();
        if (OccamToolProfile.IsExposed("occam_attest", profile))
            builder = builder.WithTools<OccamAttestTool>();
        if (OccamToolProfile.IsExposed("occam_playbook_lint", profile))
            builder = builder.WithTools<OccamPlaybookLintTool>();
        if (OccamToolProfile.IsExposed("occam_dataset_export", profile))
            builder = builder.WithTools<OccamDatasetExportTool>();

        // Opt-in async batch (fire-and-forget). Off by default: no background processor, no extra
        // tools, tool count stays at the profile surface. Enable with OCCAM_BATCH_MCP=1.
        if (OccamMcp.Core.Configuration.OccamEnvironment.GetFlag("OCCAM_BATCH_MCP", defaultValue: false))
        {
            services.AddSingleton<Batch.IBatchJobStore, Batch.JsonFileBatchJobStore>();
            services.AddSingleton<Batch.IBatchJobService, Batch.BatchJobService>();
            services.AddHostedService<Batch.BatchJobProcessor>();
            builder
                .WithTools<OccamBatchSubmitTool>()
                .WithTools<OccamBatchStatusTool>()
                .WithTools<OccamBatchResultsTool>();
        }

        // Opt-in stateful page-change watch. Off by default. Enable with OCCAM_WATCH_MCP=1.
        if (OccamMcp.Core.Configuration.OccamEnvironment.GetFlag("OCCAM_WATCH_MCP", defaultValue: false))
        {
            services.AddSingleton<Watch.IWatchStore, Watch.WatchStore>();
            services.AddSingleton<Watch.IWatchService, Watch.WatchService>();
            builder.WithTools<OccamWatchTool>();
        }

        // Opt-in consensus / cloaking cross-check (SI-14). Enable with OCCAM_CONSENSUS_MCP=1.
        if (OccamMcp.Core.Configuration.OccamEnvironment.GetFlag("OCCAM_CONSENSUS_MCP", defaultValue: false))
        {
            services.AddSingleton<Consensus.IConsensusService, Consensus.ConsensusService>();
            builder.WithTools<OccamCrosscheckTool>();
        }

        // Opt-in failure atlas (SI-10). Enable with OCCAM_ATLAS_MCP=1.
        if (OccamMcp.Core.Configuration.OccamEnvironment.GetFlag("OCCAM_ATLAS_MCP", defaultValue: false))
        {
            services.AddSingleton<Telemetry.FailureAtlasStore>();
            services.AddSingleton<Abstractions.IOccamTelemetrySink>(sp =>
                new Telemetry.FailureAtlasSink(
                    new Telemetry.OccamLoggerTelemetrySink(),
                    sp.GetRequiredService<Telemetry.FailureAtlasStore>()));
            builder.WithTools<OccamFailureAtlasTool>();
        }

        return builder;
    }

    private const string DigestUrlsUnionSchema = """
        {
          "description": "Preferred: array of URL strings. Deprecated compatibility: a JSON-array string or newline/comma-separated URL string. Optional when source_url is set (ignored in that case).",
          "oneOf": [
            {
              "type": "array",
              "items": { "type": "string", "format": "uri" },
              "minItems": 1,
              "maxItems": 256
            },
            { "type": "string", "minLength": 1 }
          ]
        }
        """;

    private static JsonElement WithDigestUrlsUnion(JsonElement inputSchema)
    {
        using var unionDocument = JsonDocument.Parse(DigestUrlsUnionSchema);
        using var stream = new MemoryStream();
        using (var writer = new Utf8JsonWriter(stream))
        {
            writer.WriteStartObject();
            foreach (var property in inputSchema.EnumerateObject())
            {
                writer.WritePropertyName(property.Name);
                if (property.NameEquals("properties"))
                {
                    writer.WriteStartObject();
                    foreach (var parameter in property.Value.EnumerateObject())
                    {
                        writer.WritePropertyName(parameter.Name);
                        if (parameter.NameEquals("urls"))
                        {
                            unionDocument.RootElement.WriteTo(writer);
                        }
                        else
                        {
                            parameter.Value.WriteTo(writer);
                        }
                    }
                    writer.WriteEndObject();
                }
                else
                {
                    property.Value.WriteTo(writer);
                }
            }
            writer.WriteEndObject();
        }

        using var document = JsonDocument.Parse(stream.ToArray());
        return document.RootElement.Clone();
    }
}
