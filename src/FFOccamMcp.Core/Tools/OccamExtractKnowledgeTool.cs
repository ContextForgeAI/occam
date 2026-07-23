using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamExtractKnowledgeTool(WorkerPaths workerPaths, KnowledgeExtractService extractService)
{
    [McpServerTool(Name = "occam_extract_knowledge"), Description("Extract typed structured fields from a page (e.g. title, price, author) as facts[], driven by the site's playbook knowledge_schema. Use when you need specific data points, not prose; requires a resolvable schema for the host (check with occam_playbook_resolve).")]
    public string ExtractKnowledge(
        [Description("HTTP or HTTPS URL (same URL used with occam_playbook_resolve).")] string url,
        [Description("Backend policy: http, browser, or http_then_browser. Default from playbook routing or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Optional session profile id — loads headers from OCCAM_SESSIONS_ROOT/<id>.json.")] string? session_profile = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url))
        {
            return SerializeFailure(string.Empty, "invalid_arguments", "url is required.");
        }

        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return SerializeFailure(url.Trim(), "invalid_arguments", "backend_policy must be http, browser, or http_then_browser.");
        }

        if (!workerPaths.IsConfigured)
        {
            var home = Environment.GetEnvironmentVariable("OCCAM_HOME");
            var diag = string.IsNullOrWhiteSpace(home)
                ? "OCCAM_HOME is not set. Set it to the Occam install root, then run occam doctor."
                : $"Workers not found at OCCAM_HOME={home}. Run occam doctor to install.";
            return SerializeFailure(url.Trim(), "workers_unavailable", diag);
        }

        var result = extractService.Extract(url.Trim(), policy, session_profile, cancellationToken);
        if (result.Ok)
        {
            return JsonSerializer.Serialize(
                OccamExtractKnowledgeResponseMapper.MapSuccess(result),
                OccamExtractKnowledgeJsonContext.Default.OccamExtractKnowledgeSuccessResponse);
        }

        return JsonSerializer.Serialize(
            OccamExtractKnowledgeResponseMapper.MapFailure(result),
            OccamExtractKnowledgeJsonContext.Default.OccamExtractKnowledgeFailureResponse);
    }

    private static string SerializeFailure(string url, string code, string message)
    {
        var hints = FailureAgentHints.ForCode(code);
        return JsonSerializer.Serialize(
            new OccamExtractKnowledgeFailureResponse(
                false,
                url,
                code,
                message,
                null,
                null,
                null,
                hints is null ? null : new OccamExtractFailureAgentHintsInfo(hints.Decisions),
                0),
            OccamExtractKnowledgeJsonContext.Default.OccamExtractKnowledgeFailureResponse);
    }
}

internal static class OccamExtractKnowledgeResponseMapper
{
    public static OccamExtractKnowledgeSuccessResponse MapSuccess(KnowledgeExtractResult result) =>
        new(
            true,
            result.Url,
            result.PlaybookId ?? string.Empty,
            result.PageClass ?? string.Empty,
            result.Facts?.Select(f => new OccamKnowledgeFactInfo(f.Name, f.Value, f.Selector)).ToArray()
                ?? Array.Empty<OccamKnowledgeFactInfo>(),
            new OccamExtractKnowledgeMetaInfo(result.Meta!.KoId),
            result.LatencyMs,
            result.Backend,
            result.Confidence,
            new OccamExtractKnowledgeReceiptInfo(result.Confidence, result.LatencyMs));

    public static OccamExtractKnowledgeFailureResponse MapFailure(KnowledgeExtractResult result)
    {
        var code = FailureCodeStrings.Normalize(result.FailureCode ?? "extraction_failed");
        var hints = FailureAgentHints.ForCode(code);
        return new(
            false,
            result.Url,
            code,
            result.FailureMessage ?? "Knowledge extract failed.",
            result.PlaybookId,
            result.PageClass,
            result.PartialFacts?.Select(f => new OccamKnowledgeFactInfo(f.Name, f.Value, f.Selector)).ToArray(),
            hints is null ? null : new OccamExtractFailureAgentHintsInfo(hints.Decisions),
            result.LatencyMs);
    }
}

public sealed record OccamKnowledgeFactInfo(string Name, string Value, string Selector);

public sealed record OccamExtractKnowledgeMetaInfo(string KoId);

/// <summary>AF-3: receipt for knowledge extract.</summary>
public sealed record OccamExtractKnowledgeReceiptInfo(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    double Confidence = 0.0,
    int ElapsedMs = 0);

public sealed record OccamExtractKnowledgeSuccessResponse(
    bool Ok,
    string Url,
    string PlaybookId,
    string PageClass,
    OccamKnowledgeFactInfo[] Facts,
    OccamExtractKnowledgeMetaInfo Meta,
    int LatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Backend,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    double Confidence = 0.0,
    OccamExtractKnowledgeReceiptInfo? Receipt = null);

public sealed record OccamExtractKnowledgeFailureResponse(
    bool Ok,
    string Url,
    string FailureCode,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PlaybookId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PageClass,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamKnowledgeFactInfo[]? PartialFacts,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamExtractFailureAgentHintsInfo? AgentHints,
    int LatencyMs);

public sealed record OccamExtractFailureAgentHintsInfo(ProbeDecision[] Decisions);

[JsonSerializable(typeof(OccamExtractKnowledgeSuccessResponse))]
[JsonSerializable(typeof(OccamExtractKnowledgeFailureResponse))]
[JsonSerializable(typeof(OccamExtractFailureAgentHintsInfo))]
[JsonSerializable(typeof(OccamExtractKnowledgeReceiptInfo))]
[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(ProbeDecision[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamExtractKnowledgeJsonContext : JsonSerializerContext;
