using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;

namespace OccamMcp.Core.Tools;

public sealed record OccamProbeUrlInfo(string Requested, string? Final);

public sealed record OccamProbeChallengeInfo(
    string Kind,
    bool HealEligible,
    string RecommendedAction);

public sealed record OccamProbeClassificationInfo(
    string PageClass,
    bool RequiresJavascript,
    bool LikelyCookieConsent,
    bool LikelyChallenge,
    bool LikelyLoginRequired,
    bool LikelyPaywall,
    string[] RiskFlags,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? DomainTier,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool HttpOnlyRoute,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamProbeChallengeInfo? Challenge);

public sealed record OccamProbeRecommendationInfo(
    string Backend,
    int EstimatedLatencyMs,
    // Cheap [0,1] extractability estimate (same scorer occam_search uses for rerank): how readable
    // this page is likely to be (dead/blocked/paywall/anti-bot/JS-stub score low; clean docs/articles
    // high). Lets an agent decide whether a transcode is worth it before paying for one.
    double Extractability);

public sealed record OccamProbePolicyInfo(string PrivacyMode);

public sealed record OccamProbeSocialMetaInfo(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Title,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Description,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Image,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TwitterCard,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SiteName,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Lang);

public sealed record OccamProbeAgentHintsInfo(
    string SuggestedNextTool,
    string[] Warnings,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProbeDecision[]? Decisions);

public sealed record OccamProbeSuccessResponse(
    bool Ok,
    OccamProbeUrlInfo Url,
    OccamProbeClassificationInfo Classification,
    OccamProbeRecommendationInfo Recommendation,
    OccamProbePolicyInfo Policy,
    int StatusCode,
    string? ContentType,
    int ProbeLatencyMs,
    OccamProbeAgentHintsInfo AgentHints,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamProbeSocialMetaInfo? SocialMeta,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? RedirectChain,
    string Timestamp,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticAccessInfo? Access = null);

public sealed record OccamProbeFailureResponse(
    bool Ok,
    OccamProbeUrlInfo Url,
    string FailureCode,
    string Message,
    OccamProbePolicyInfo Policy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int StatusCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? RedirectChain,
    int ProbeLatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamProbeAgentHintsInfo? AgentHints,
    string Timestamp);

internal static class OccamProbeResponseMapper
{
    public static OccamProbeSuccessResponse MapSuccess(ProbeAnalysis analysis)
    {
        var c = analysis.Classification!;
        var signals = c.Signals;
        var hints = ProbeAgentHints.ForProbe(analysis);
        return new OccamProbeSuccessResponse(
            Ok: true,
            Url: new OccamProbeUrlInfo(analysis.Url, analysis.FinalUrl),
            Classification: new OccamProbeClassificationInfo(
                signals.PageClass,
                signals.RequiresJavascript,
                signals.LikelyCookieConsent,
                signals.LikelyChallenge,
                signals.LikelyLoginRequired,
                signals.LikelyPaywall,
                c.RiskFlags,
                analysis.DomainTier,
                DomainTierRegistry.PreferHttpOnlyRoute(analysis.Url, signals),
                MapChallenge(analysis.Challenge ?? c.Challenge)),
            Recommendation: new OccamProbeRecommendationInfo(
                analysis.RecommendedBackend ?? "http",
                analysis.EstimatedLatencyMs,
                Math.Round(SearchExtractabilityScorer.Score(analysis), 2)),
            Policy: new OccamProbePolicyInfo(MapPrivacy(analysis.Privacy.Mode)),
            StatusCode: analysis.StatusCode,
            ContentType: analysis.ContentType,
            ProbeLatencyMs: analysis.LatencyMs,
            AgentHints: new OccamProbeAgentHintsInfo(
                hints.SuggestedNextTool,
                hints.Warnings,
                hints.Decisions.Length > 0 ? hints.Decisions : null),
            SocialMeta: MapSocial(analysis.SocialMeta),
            RedirectChain: analysis.RedirectChain,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"),
            Access: Semantics.SemanticOutcomeMapper.MapAccess(c.Access));
    }

    private static OccamProbeSocialMetaInfo? MapSocial(SocialMeta? meta) =>
        meta is null
            ? null
            : new OccamProbeSocialMetaInfo(
                meta.Title,
                meta.Description,
                meta.Image,
                meta.TwitterCard,
                meta.SiteName,
                meta.Lang);

    private static OccamProbeChallengeInfo? MapChallenge(ChallengeHint? challenge) =>
        challenge is null
            ? null
            : new OccamProbeChallengeInfo(challenge.Kind, challenge.HealEligible, challenge.RecommendedAction);

    public static OccamProbeFailureResponse MapFailure(ProbeAnalysis analysis)
    {
        var code = FailureCodeStrings.Normalize(analysis.FailureCode);
        var hints = ProbeAgentHints.ForFailure(code);
        return new(
            Ok: false,
            Url: new OccamProbeUrlInfo(analysis.Url, analysis.FinalUrl),
            FailureCode: code,
            Message: FailureCodeStrings.FormatProbeMessage(analysis.FailureCode, analysis.StatusCode),
            Policy: new OccamProbePolicyInfo(MapPrivacy(analysis.Privacy.Mode)),
            StatusCode: analysis.StatusCode,
            RedirectChain: analysis.RedirectChain,
            ProbeLatencyMs: analysis.LatencyMs,
            AgentHints: hints.Decisions.Length > 0
                ? new OccamProbeAgentHintsInfo(hints.SuggestedNextTool, hints.Warnings, hints.Decisions)
                : null,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));
    }

    private static string MapPrivacy(PrivacyMode mode) => mode switch
    {
        PrivacyMode.LocalPrivate => "local_private",
        PrivacyMode.BlockedByPolicy => "blocked_by_policy",
        _ => "local_public",
    };
}

[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(ProbeDecision[]))]
[JsonSerializable(typeof(OccamProbeSuccessResponse))]
[JsonSerializable(typeof(OccamProbeFailureResponse))]
[JsonSerializable(typeof(Semantics.SemanticAccessInfo))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamProbeJsonContext : JsonSerializerContext;
