using OccamMcp.Core.Agent;
using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Semantics;

/// <summary>
/// Actionable next steps when focus prune or answer-unit completeness is weak —
/// focus is a budget prune, not full topic coverage or LLM summarization.
/// </summary>
public static class FocusBudgetHints
{
    public const string RaiseMaxTokens = "raise_max_tokens";
    public const string RefineFocus = "refine_focus";
    public const string DisableFit = "disable_fit_markdown";

    public static string? NextStepForFocus(string status) =>
        status switch
        {
            "weak" or "miss" =>
                "Focus prune for budget, not full topic coverage — raise max_tokens, use section/toc/must_contain, or fit_markdown=false.",
            _ => null,
        };

    public static string? NextStepForCompleteness(
        string status,
        string? incompleteReason,
        int? suggestedMinTokens)
    {
        if (status == "incomplete")
        {
            var reason = string.IsNullOrWhiteSpace(incompleteReason)
                ? "answer unit did not fit"
                : incompleteReason;
            var budget = suggestedMinTokens is > 0
                ? $" retry with max_tokens>={suggestedMinTokens}"
                : " raise max_tokens, use section/toc/must_contain, or fit_markdown=false";
            return $"Answer unit incomplete ({reason});{budget}.";
        }

        if (status == "partial")
        {
            return "Focused answer retained but context truncated — raise max_tokens or narrow focus_query.";
        }

        return null;
    }

    public static string SuggestedNext(
        SemanticFocusInfo? focus,
        SemanticCompletenessInfo? completeness)
    {
        if (completeness?.Status == "incomplete" && completeness.SuggestedMinTokens is > 0)
        {
            return RaiseMaxTokens;
        }

        if (completeness?.Status is "incomplete" or "partial")
        {
            return RaiseMaxTokens;
        }

        if (focus?.Status is "miss" or "weak")
        {
            return RefineFocus;
        }

        return "none";
    }

    public static IEnumerable<ProbeDecision> Decisions(
        SemanticFocusInfo? focus,
        SemanticCompletenessInfo? completeness)
    {
        if (completeness?.Status == "incomplete")
        {
            var reason = completeness.SuggestedMinTokens is > 0
                ? $"Answer unit missing; retry max_tokens>={completeness.SuggestedMinTokens}."
                : "Answer unit missing; raise max_tokens or disable fit_markdown.";
            yield return new ProbeDecision(
                RaiseMaxTokens,
                reason,
                Parameter: completeness.SuggestedMinTokens is > 0
                    ? $"max_tokens>={completeness.SuggestedMinTokens}"
                    : "max_tokens");
            if (string.Equals(completeness.IncompleteReason, "focus_body_filtered", StringComparison.Ordinal))
            {
                yield return new ProbeDecision(
                    DisableFit,
                    "Focus body filtered without budget truncation — try fit_markdown=false or section/toc.",
                    Parameter: "fit_markdown");
            }
        }
        else if (completeness?.Status == "partial")
        {
            yield return new ProbeDecision(
                RaiseMaxTokens,
                "Context truncated around a retained answer unit — raise max_tokens or narrow focus.",
                Parameter: "max_tokens");
        }

        if (focus?.Status is "miss" or "weak")
        {
            yield return new ProbeDecision(
                RefineFocus,
                "Focus prune missed or weakly matched the query — refine focus_query, use section/toc/must_contain, or fit_markdown=false.",
                Parameter: "focus_query");
        }
    }

    public static string? NextStepFromAssessment(MaterializationAssessment? assessment)
    {
        if (assessment is null)
        {
            return null;
        }

        var completeness = assessment.Completeness switch
        {
            MaterializationCompleteness.Complete => "complete",
            MaterializationCompleteness.Partial => "partial",
            _ => "incomplete",
        };
        return NextStepForCompleteness(
            completeness,
            assessment.IncompleteReason,
            assessment.SuggestedMinTokens);
    }
}
