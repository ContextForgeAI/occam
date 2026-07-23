using OccamMcp.Core.Compile;

namespace OccamMcp.Rc2Regression;

/// <summary>Frozen RC.1 raw-inventory behavior; characterization only, never production.</summary>
internal static class LegacyBudgetOwnershipCharacterization
{
    public static BudgetOwnership.Prepared PrepareSurfaceBudget(
        int? userMaxTokens,
        ResponseBudgetSidecars rawSidecars,
        ResponseBudgetMode mode = ResponseBudgetMode.Full)
    {
        if (userMaxTokens is not int maxTokens)
        {
            return new BudgetOwnership.Prepared(null, null, null);
        }

        var caps = ResponseBudgetPlanner.AllocateMarkdownCap(maxTokens, rawSidecars, mode);
        return new BudgetOwnership.Prepared(maxTokens, caps.MarkdownCap, caps);
    }
}
