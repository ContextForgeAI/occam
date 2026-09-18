using System.Text.Json;
using System.Text.Json.Nodes;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Exam;
using OccamMcp.Core.Telemetry;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Transport;

public static class OccamMcpServerRegistration
{
    /// <summary>Full core-tool catalog (profile <c>full</c>). Runtime exposure may be narrower via <c>OCCAM_PROFILE</c>.</summary>
    public static readonly string[] OccamToolNames =
    [
        "occam_client_capabilities",
        "occam",
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
        "occam_canary_issue",
        "occam_canary_verify",
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

        var examMcp = ExamMcpRuntime.IsEnabled;
        var pinned = OccamToolProfile.IsPinned();
        // Exam path: start at reader unless the operator pinned OCCAM_PROFILE. Never start at
        // ExamResult.Default→basic (that would silently demote a working host).
        var profile = examMcp && !pinned ? OccamToolProfile.Reader : OccamToolProfile.Resolve();
        var instructions = OccamServerInstructions.TextFor(profile);

        if (examMcp)
        {
            services.AddSingleton(_ => new ExamResultCache());
            services.AddSingleton(_ => new SessionToolSurface(profile, pinned));
            services.AddSingleton<ExamMcpRuntime>();
            Console.Error.WriteLine(
                pinned
                    ? $"[occam.exam] OCCAM_EXAM_MCP=1; OCCAM_PROFILE pinned to '{profile}' (exam will not change surface)."
                    : $"[occam.exam] OCCAM_EXAM_MCP=1; session surface starts at '{profile}' (submit may change it).");
        }

        var builder = services
            // Surface the capability + decision guide to the consuming model on initialize, so the
            // off-by-default power features are discoverable instead of invisible behind 20 params.
            .AddMcpServer(options =>
            {
                options.ServerInstructions = instructions;
                options.ServerInfo = new Implementation
                {
                    Name = "ff-occam",
                    Version = OccamHostVersion.Current,
                };
                // Preferred advertise shape (SDK may overwrite listChanged when ToolCollection
                // is present — see WithMessageFilters honesty rewrite below).
                options.Capabilities = new ServerCapabilities
                {
                    Tools = new ToolsCapability { ListChanged = examMcp },
                };
            })
            .WithMessageFilters(messageFilters =>
            {
                if (examMcp)
                {
                    messageFilters.AddIncomingFilter(next => async (context, cancellationToken) =>
                    {
                        TryBootstrapExamCache(context);
                        await next(context, cancellationToken).ConfigureAwait(false);
                    });
                }

                messageFilters.AddOutgoingFilter(next => async (context, cancellationToken) =>
                {
                    OccamCapabilityHonesty.RewriteOutgoingMessage(context.JsonRpcMessage);
                    await next(context, cancellationToken).ConfigureAwait(false);
                });
            })
            // Map MEAI required-parameter / declared-type binding failures to typed invalid_arguments
            // before McpServerImpl.ToolCallError logs them as unhandled exceptions (EventId 1433779783).
            .WithRequestFilters(filters =>
            {
                filters.AddCallToolFilter(next => async (request, cancellationToken) =>
                {
                    if (examMcp
                        && request.Params?.Name is { } toolName
                        && request.Services?.GetService<SessionToolSurface>() is { } surface
                        && !surface.IsExposed(toolName))
                    {
                        return OccamMcpToolWireEnricher.EnrichCallToolResult(
                            ToolNotExposedResult(toolName, surface.ProfileId));
                    }

                    try
                    {
                        var result = await next(request, cancellationToken).ConfigureAwait(false);
                        return OccamMcpToolWireEnricher.EnrichCallToolResult(result);
                    }
                    catch (Exception ex) when (McpArgumentBindingGuard.IsClientInputBindingFailure(ex))
                    {
                        var name = request.Params?.Name;
                        McpArgumentBindingGuard.LogBindingRejection(name, ex);
                        return OccamMcpToolWireEnricher.EnrichCallToolResult(
                            McpArgumentBindingGuard.ToTypedInvalidArgumentsResult(ex, name));
                    }
                });
                filters.AddListToolsFilter(next => async (request, cancellationToken) =>
                {
                    var result = await next(request, cancellationToken).ConfigureAwait(false);
                    if (examMcp
                        && request.Services?.GetService<SessionToolSurface>() is { } surface
                        && result.Tools is { Count: > 0 })
                    {
                        result.Tools = result.Tools.Where(t => surface.IsExposed(t.Name)).ToList();
                    }

                    return OccamMcpToolWireEnricher.EnrichListToolsResult(result);
                });
            });

        // When exam MCP is on, register the full core catalog and filter at list/call time.
        // When off, keep the historical profile-gated DI registration (zero behavior change).
        var registrationProfile = examMcp ? OccamToolProfile.Full : profile;
        builder = RegisterCoreTools(builder, services, registrationProfile);

        if (examMcp)
        {
            builder = builder.WithTools<OccamExamSubmitTool>();
        }

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

        // Opt-in browser actions (click/type/scroll then materialize). Off by default.
        // Enable with OCCAM_BROWSER_ACTIONS_MCP=1. Subresource SSRF must stay enabled.
        if (OccamMcp.Core.Configuration.OccamEnvironment.GetFlag("OCCAM_BROWSER_ACTIONS_MCP", defaultValue: false))
        {
            builder.WithTools<OccamBrowserInteractTool>();
        }

        return builder;
    }

    private static IMcpServerBuilder RegisterCoreTools(
        IMcpServerBuilder builder,
        IServiceCollection services,
        string profile)
    {
        if (OccamToolProfile.IsExposed("occam_client_capabilities", profile))
            builder = builder.WithTools<OccamClientCapabilitiesTool>();
        if (OccamToolProfile.IsExposed("occam", profile))
            builder = builder.WithTools<OccamCascadeTool>();
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
        if (OccamToolProfile.IsExposed("occam_canary_issue", profile))
            builder = builder.WithTools<OccamCanaryIssueTool>();
        if (OccamToolProfile.IsExposed("occam_canary_verify", profile))
            builder = builder.WithTools<OccamCanaryVerifyTool>();
        return builder;
    }

    /// <summary>
    /// Non-blocking: on initialize, try a cache hit for this client. Never applies Default→basic.
    /// </summary>
    private static void TryBootstrapExamCache(MessageContext context)
    {
        if (context.JsonRpcMessage is not JsonRpcRequest { Method: "initialize" } request)
        {
            return;
        }

        var exam = context.Services?.GetService<ExamMcpRuntime>();
        if (exam is null || exam.Surface.IsPinned)
        {
            return;
        }

        var clientInfo = TryReadClientInfo(request.Params);
        if (clientInfo is null)
        {
            return;
        }

        // modelHint/sessionId are not on initialize; only a prior submit that used blanks/"unknown"
        // for those fields can hit. Still non-blocking and never demotes on miss.
        var subject = ExamSubject.Create(clientInfo, modelHint: null, sessionId: null);
        _ = Task.Run(() =>
        {
            try
            {
                if (exam.TryApplyCached(subject, out var result))
                {
                    Console.Error.WriteLine(
                        $"[occam.exam] cache hit on initialize → profile={exam.Surface.ProfileId} tier={result.TierWire}");
                }
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[occam.exam] initialize cache apply failed: {ex.Message}");
            }
        });
    }

    private static string? TryReadClientInfo(JsonNode? parameters)
    {
        if (parameters is not JsonObject root)
        {
            return null;
        }

        if (root["clientInfo"] is not JsonObject info)
        {
            return null;
        }

        var name = info["name"]?.GetValue<string>();
        var version = info["version"]?.GetValue<string>();
        if (string.IsNullOrWhiteSpace(name))
        {
            return null;
        }

        return string.IsNullOrWhiteSpace(version) ? name.Trim() : $"{name.Trim()}/{version.Trim()}";
    }

    private static CallToolResult ToolNotExposedResult(string toolName, string profile)
    {
        var message =
            $"Tool '{toolName}' is not on the current exam surface (profile={profile}). " +
            "Re-list tools after list_changed, or submit a new exam / pin OCCAM_PROFILE.";
        var payload =
            "{\"ok\":false,\"failureCode\":\"tool_not_on_surface\",\"message\":"
            + McpArgumentBindingGuard.JsonString(message)
            + ",\"timestamp\":"
            + McpArgumentBindingGuard.JsonString(DateTimeOffset.UtcNow.ToString("O"))
            + "}";

        return new CallToolResult
        {
            IsError = true,
            Content = [new TextContentBlock { Text = payload }],
        };
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
