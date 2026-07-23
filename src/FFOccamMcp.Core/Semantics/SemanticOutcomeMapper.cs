using OccamMcp.Core.Access;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Semantics;

/// <summary>Public access dimension (INV-9). Scoped confidence — never a generic success score.</summary>
public sealed record SemanticAccessInfo(
    string Disposition,
    double Confidence,
    string[] EvidenceCodes,
    string RecommendedAction);

/// <summary>Public focus dimension: hit | weak | miss | not_requested.</summary>
public sealed record SemanticFocusInfo(
    string Status,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    double? Confidence = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? MatchedAnchor = null);

/// <summary>Public completeness dimension: complete | partial | incomplete.</summary>
public sealed record SemanticCompletenessInfo(
    string Status,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    string? IncompleteReason = null,
    [property: System.Text.Json.Serialization.JsonIgnore(
        Condition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull)]
    int? SuggestedMinTokens = null);

/// <summary>Claim / attestation semantic judgment. Retrieval tools emit <c>not_evaluated</c>.</summary>
public static class SemanticVerdict
{
    public const string NotEvaluated = "not_evaluated";
    public const string Supported = "supported";
    public const string Refuted = "refuted";
    public const string Contradicted = "contradicted";
}

/// <summary>Maps PR-C/PR-E internal truth onto additive public semantic fields (PR-F).</summary>
public static class SemanticOutcomeMapper
{
    public static SemanticAccessInfo? MapAccess(AccessAssessment? assessment)
    {
        if (assessment is null)
        {
            return null;
        }

        return new SemanticAccessInfo(
            Disposition: ToSnake(assessment.Disposition.ToString()),
            Confidence: Math.Round(assessment.Confidence, 2),
            EvidenceCodes: assessment.EvidenceCodes is { Count: > 0 }
                ? assessment.EvidenceCodes.ToArray()
                : [],
            RecommendedAction: assessment.RecommendedAction);
    }

    public static SemanticFocusInfo MapFocus(
        MaterializationAssessment? assessment,
        string? focusQuery,
        string? focusFragment,
        string? matchedAnchor = null)
    {
        if (string.IsNullOrWhiteSpace(focusQuery) && string.IsNullOrWhiteSpace(focusFragment))
        {
            return new SemanticFocusInfo("not_requested");
        }

        if (assessment is null)
        {
            return new SemanticFocusInfo("miss");
        }

        var status = assessment.Focus switch
        {
            FocusMatchStatus.Hit => "hit",
            FocusMatchStatus.Weak => "weak",
            _ => "miss",
        };
        return new SemanticFocusInfo(status, Confidence: null, MatchedAnchor: matchedAnchor);
    }

    public static SemanticCompletenessInfo? MapCompleteness(MaterializationAssessment? assessment)
    {
        if (assessment is null)
        {
            return null;
        }

        var status = assessment.Completeness switch
        {
            MaterializationCompleteness.Complete => "complete",
            MaterializationCompleteness.Partial => "partial",
            _ => "incomplete",
        };
        return new SemanticCompletenessInfo(
            status,
            assessment.IncompleteReason,
            assessment.SuggestedMinTokens);
    }

    /// <summary>
    /// Digest lexical <c>focusMatched</c> stays an alias for relevance-ish match evidence.
    /// Structural focus status is independent and may disagree.
    /// </summary>
    public static SemanticFocusInfo MapDigestFocus(bool? focusMatched, string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return new SemanticFocusInfo("not_requested");
        }

        return focusMatched switch
        {
            true => new SemanticFocusInfo("hit"),
            false => new SemanticFocusInfo("miss"),
            _ => new SemanticFocusInfo("miss"),
        };
    }

    private static string ToSnake(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        return string.Create(value.Length, value, static (span, src) =>
        {
            span[0] = char.ToLowerInvariant(src[0]);
            src.AsSpan(1).CopyTo(span[1..]);
        });
    }
}
