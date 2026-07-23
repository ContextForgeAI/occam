using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;

namespace OccamMcp.Core.Tools;

public sealed record OccamMapLinkInfo(
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Title,
    string Path);

public sealed record OccamMapAgentHintsInfo(
    string SuggestedNext,
    int MaxDigestUrls,
    string[] Warnings);

public sealed record OccamMapSuccessResponse(
    bool Ok,
    string Url,
    string FinalUrl,
    string Source,
    OccamMapLinkInfo[] Links,
    int LinkCount,
    int Filtered,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Partial,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FocusQuery,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Expanded,
    OccamMapAgentHintsInfo AgentHints,
    string Timestamp);

public sealed record OccamMapFailureResponse(
    bool Ok,
    string FailureCode,
    string Message,
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FinalUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? StatusCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamMapFailureAgentHintsInfo? AgentHints,
    string Timestamp);

public sealed record OccamMapFailureAgentHintsInfo(ProbeDecision[] Decisions);

internal static class OccamMapResponseMapper
{
    public static OccamMapSuccessResponse MapSuccess(MapAnalysis analysis) =>
        new(
            Ok: true,
            Url: analysis.Url,
            FinalUrl: analysis.FinalUrl ?? analysis.Url,
            Source: analysis.Source,
            Links: MapLinks(analysis.Links),
            LinkCount: analysis.LinkCount,
            Filtered: analysis.FilteredCount,
            Partial: analysis.Partial,
            FocusQuery: analysis.FocusQuery,
            Expanded: analysis.Expanded,
            AgentHints: new OccamMapAgentHintsInfo(
                "occam_digest",
                DigestService.MaxUrlsCap,
                BuildMapWarnings(analysis)),
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));

    private static string[] BuildMapWarnings(MapAnalysis analysis)
    {
        var warnings = new List<string>();
        if (analysis.Partial)
        {
            warnings.Add("Sitemap discovery reached timeout_ms after finding some links; links[] is partial.");
        }

        if (analysis.Expanded)
        {
            warnings.Add(
                "focus_expand: seed page lacked a strong focus hit — expanded hub pages (library/docs/index) and re-ranked.");
        }

        return [.. warnings];
    }

    public static OccamMapFailureResponse MapFailure(MapAnalysis analysis)
    {
        var code = FailureCodeStrings.Normalize(analysis.FailureCode ?? "extraction_failed");
        var hints = FailureAgentHints.ForCode(code);
        return new(
            Ok: false,
            FailureCode: code,
            Message: FormatMapMessage(analysis.FailureCode ?? "extraction_failed", analysis.FailureStatusCode),
            Url: analysis.Url,
            FinalUrl: analysis.FinalUrl,
            StatusCode: analysis.FailureStatusCode,
            AgentHints: hints is null ? null : new OccamMapFailureAgentHintsInfo(hints.Decisions),
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));
    }

    private static OccamMapLinkInfo[] MapLinks(IReadOnlyList<MappedLink> links) =>
        links.Select(link => new OccamMapLinkInfo(link.Url, link.Title, link.Path)).ToArray();

    private static string FormatMapMessage(string failureCode, int? statusCode) =>
        failureCode switch
        {
            "sitemap_not_found" => "Sitemap/robots discovery found no links.",
            "thin_extract" => "Homepage HTML had no extractable same-domain links after filtering.",
            "invalid_url" => "URL is not a valid absolute HTTP or HTTPS URL.",
            "invalid_arguments" => "Invalid map arguments (source, max_links, or timeout_ms).",
            "private_url_blocked" => "Private or local URLs are blocked in map v1.",
            "timeout" => "Map HTTP fetch timed out.",
            "unsupported_content_type" => "URL is not an HTML page suitable for link discovery.",
            _ when failureCode.StartsWith("http_", StringComparison.Ordinal) =>
                statusCode is > 0
                    ? $"HTTP {statusCode} ({failureCode})."
                    : $"HTTP error ({failureCode}).",
            "extraction_failed" => "Homepage fetch failed or returned no usable HTML.",
            _ => "Map discovery failed.",
        };
}

[JsonSerializable(typeof(OccamMapSuccessResponse))]
[JsonSerializable(typeof(OccamMapFailureResponse))]
[JsonSerializable(typeof(OccamMapLinkInfo))]
[JsonSerializable(typeof(OccamMapAgentHintsInfo))]
[JsonSerializable(typeof(OccamMapFailureAgentHintsInfo))]
[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(ProbeDecision[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamMapJsonContext : JsonSerializerContext;
