using OccamMcp.Core.Compile;

namespace OccamMcp.Core.Knowledge;

public enum MaterializationCompleteness
{
    Complete,
    Partial,
    Incomplete,
}

/// <summary>Internal truth state retained for the additive PR-F response mapping.</summary>
public sealed record MaterializationAssessment(
    FocusMatchStatus Focus,
    MaterializationCompleteness Completeness,
    string? IncompleteReason = null,
    int? SuggestedMinTokens = null,
    int? SelectedAnswerUnitTokens = null,
    int PlannerRetries = 0);

public static class MaterializationAssessmentEvaluator
{
    public static MaterializationAssessment Evaluate(
        string sourceMarkdown,
        string compiledMarkdown,
        string? focusQuery,
        string? focusFragment,
        bool truncated)
    {
        if (string.IsNullOrWhiteSpace(focusQuery) && string.IsNullOrWhiteSpace(focusFragment))
        {
            return new MaterializationAssessment(
                FocusMatchStatus.Miss,
                truncated ? MaterializationCompleteness.Partial : MaterializationCompleteness.Complete);
        }

        var selection = SectionRanker.Select(SectionIndex.Build(sourceMarkdown), focusQuery, focusFragment);
        if (selection.Section is null)
        {
            return new MaterializationAssessment(
                FocusMatchStatus.Miss,
                MaterializationCompleteness.Incomplete,
                "source_missing");
        }

        var unit = AnswerUnitSelector.Select(selection.Section, focusQuery ?? focusFragment);
        var retained = unit is not null && ContainsUnit(compiledMarkdown, unit.Text);
        if (!retained)
        {
            return new MaterializationAssessment(
                selection.Status,
                MaterializationCompleteness.Incomplete,
                "focus_body_truncated",
                unit is null ? null : Math.Max(ResponseBudgetPlanner.MinMarkdownTokens, unit.Tokens + 32),
                unit?.Tokens);
        }

        return new MaterializationAssessment(
            selection.Status,
            truncated ? MaterializationCompleteness.Partial : MaterializationCompleteness.Complete,
            truncated ? "context_truncated" : null,
            SelectedAnswerUnitTokens: unit?.Tokens);
    }

    private static bool ContainsUnit(string compiled, string unit) =>
        AnswerUnitSelector.SplitBlocks(unit)
            .Where(block => !block.StartsWith('#'))
            .All(block => compiled.Contains(block, StringComparison.Ordinal));
}
