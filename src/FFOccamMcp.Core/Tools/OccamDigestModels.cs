using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Tools;

public sealed record OccamDigestItemInfo(
    string Url,
    bool Ok,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Excerpt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Backend,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int TokensEstimated,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDigestItemFailureInfo? Failure,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FocusQuery,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? FocusMatched,
    OccamTranscodeMediaRefInfo[] MediaRefs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    double Confidence = 0.0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeReceiptInfo? Receipt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticAccessInfo? Access = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticFocusInfo? Focus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticCompletenessInfo? Completeness = null);

public sealed record OccamDigestItemFailureInfo(string Code, string Message);

public sealed record OccamDigestStatsInfo(
    int Requested,
    int Succeeded,
    int Failed,
    int TotalTokensEstimated);

public sealed record OccamDigestAgentHintsInfo(
    string SuggestedReadOrder,
    string[] Warnings,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DigestDecision[]? Decisions);

/// <summary>AF-5: discovered link from source_url.</summary>
public sealed record OccamDigestDiscoveredLinkInfo(string Url);

public sealed record OccamDigestSuccessResponse(
    bool Ok,
    string DigestId,
    OccamDigestItemInfo[] Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Combined,
    OccamDigestStatsInfo Stats,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDigestAgentHintsInfo? AgentHints,
    string Timestamp,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SourceUrl = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDigestDiscoveredLinkInfo[]? DiscoveredLinks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Unchanged = null);

public sealed record OccamDigestFailureResponse(
    bool Ok,
    string FailureCode,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DigestId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDigestItemInfo[]? Items,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDigestStatsInfo? Stats,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDigestFailureAgentHintsInfo? AgentHints,
    string Timestamp);

public sealed record OccamDigestFailureAgentHintsInfo(ProbeDecision[] Decisions);

internal static class OccamDigestResponseMapper
{
    public static OccamDigestSuccessResponse MapSuccess(Services.DigestAnalysis analysis)
    {
        var hints = DigestAgentHints.ForDigest(analysis);
        return new(
            Ok: true,
            DigestId: analysis.DigestId!,
            Items: MapItems(analysis.Items),
            Combined: analysis.Combined,
            Stats: new OccamDigestStatsInfo(
                analysis.Requested,
                analysis.Succeeded,
                analysis.Failed,
                analysis.TotalTokensEstimated),
            AgentHints: new OccamDigestAgentHintsInfo(
                hints.SuggestedReadOrder,
                hints.Warnings,
                hints.Decisions.Length > 0 ? hints.Decisions : null),
            Timestamp: DateTimeOffset.UtcNow.ToString("O"),
            SourceUrl: analysis.SourceUrl,
            DiscoveredLinks: analysis.DiscoveredLinks is { Count: > 0 }
                ? analysis.DiscoveredLinks.Select(l => new OccamDigestDiscoveredLinkInfo(l)).ToArray()
                : null,
            Unchanged: analysis.Unchanged);
    }

    public static OccamDigestFailureResponse MapFailure(Services.DigestAnalysis analysis)
    {
        var code = FailureCodeStrings.Normalize(analysis.FailureCode ?? "digest_failed");
        var hints = FailureAgentHints.ForCode(code);
        return new(
            Ok: false,
            FailureCode: code,
            Message: analysis.FailureMessage ?? "Digest failed.",
            DigestId: analysis.DigestId,
            Items: analysis.Items.Count > 0 ? MapItems(analysis.Items) : null,
            Stats: analysis.Requested > 0
                ? new OccamDigestStatsInfo(
                    analysis.Requested,
                    analysis.Succeeded,
                    analysis.Failed,
                    analysis.TotalTokensEstimated)
                : null,
            AgentHints: hints is null ? null : new OccamDigestFailureAgentHintsInfo(hints.Decisions),
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));
    }

    private static OccamDigestItemInfo[] MapItems(IReadOnlyList<Services.DigestItemResult> items) =>
        items.Select(i => new OccamDigestItemInfo(
            i.Url,
            i.Ok,
            i.Title,
            i.Excerpt,
            i.Backend,
            i.TokensEstimated,
            i.Ok || i.FailureCode is null
                ? null
                : new OccamDigestItemFailureInfo(
                    FailureCodeStrings.Normalize(i.FailureCode),
                    i.FailureMessage ?? FailureCodeStrings.FormatTranscodeMessage(
                        i.FailureCode,
                        FailureCodeStrings.TryParseHttpStatusCode(i.FailureCode))),
            i.FocusQuery,
            i.FocusMatched,
            i.Ok
                ? (i.MediaRefs is null || i.MediaRefs.Count == 0
                    ? []
                    : i.MediaRefs
                        .Select(m => new OccamTranscodeMediaRefInfo(
                            m.Url,
                            m.Kind,
                            m.Alt,
                            m.ContextHeading,
                            m.SelectorHint))
                        .ToArray())
                : [],
            i.Confidence,
            i.Ok
                ? new OccamTranscodeReceiptInfo(
                    i.TokensEstimated,
                    i.TruncationStrategy,
                    i.Confidence,
                    i.LatencyMs,
                    Signed: i.Receipt,
                    TokenEstimator: OccamMcp.Core.Compile.TokenEstimator.EstimatorId)
                : null,
            Access: Semantics.SemanticOutcomeMapper.MapAccess(i.AccessAssessment),
            Focus: i.MaterializationAssessment is not null
                ? Semantics.SemanticOutcomeMapper.MapFocus(
                    i.MaterializationAssessment, i.FocusQuery, focusFragment: null)
                : Semantics.SemanticOutcomeMapper.MapDigestFocus(i.FocusMatched, i.FocusQuery),
            Completeness: Semantics.SemanticOutcomeMapper.MapCompleteness(i.MaterializationAssessment))).ToArray();
}

[JsonSerializable(typeof(OccamTranscodeMediaRefInfo))]
[JsonSerializable(typeof(OccamDigestSuccessResponse))]
[JsonSerializable(typeof(OccamDigestFailureResponse))]
[JsonSerializable(typeof(OccamDigestItemInfo))]
[JsonSerializable(typeof(OccamDigestItemFailureInfo))]
[JsonSerializable(typeof(OccamDigestStatsInfo))]
[JsonSerializable(typeof(OccamDigestAgentHintsInfo))]
[JsonSerializable(typeof(OccamDigestFailureAgentHintsInfo))]
[JsonSerializable(typeof(DigestDecision))]
[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(ProbeDecision[]))]
[JsonSerializable(typeof(OccamTranscodeReceiptInfo))]
[JsonSerializable(typeof(OccamMcp.Core.Receipts.ReceiptEnvelope))]
[JsonSerializable(typeof(OccamMcp.Core.Receipts.ReceiptPlaybook))]
[JsonSerializable(typeof(OccamMcp.Core.Receipts.ReceiptTimeAnchor))]
[JsonSerializable(typeof(OccamDigestDiscoveredLinkInfo))]
[JsonSerializable(typeof(OccamDigestDiscoveredLinkInfo[]))]
[JsonSerializable(typeof(Semantics.SemanticAccessInfo))]
[JsonSerializable(typeof(Semantics.SemanticFocusInfo))]
[JsonSerializable(typeof(Semantics.SemanticCompletenessInfo))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(IReadOnlyList<string>))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamDigestJsonContext : JsonSerializerContext;
