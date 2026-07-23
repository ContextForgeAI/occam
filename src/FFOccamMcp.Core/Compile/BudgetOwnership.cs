namespace OccamMcp.Core.Compile;

/// <summary>
/// Single ownership story for <c>max_tokens</c> on the live transcode path (Occam 1.1 R6).
/// <para>
/// Two layers, one user knob:
/// </para>
/// <list type="number">
/// <item>
/// <b>Whole-response</b> (<see cref="ResponseBudgetPlanner"/>) — the public <c>max_tokens</c> is shared
/// across markdown + structured sidecars + receipt. Allocates a markdown/surface cap first, then
/// trims sidecars after materialization.
/// </item>
/// <item>
/// <b>Surface / semantic</b> (<c>MaterializationPlanner</c>) — receives only the surface token budget
/// (markdown floor/cap + document-IR + Canonical claim share). Never sees the whole-response pool;
/// never trims MCP sidecars.
/// </item>
/// </list>
/// Codecs do not own budget. Collapsing both layers into the materialization planner would couple
/// semantic retention to post-reconcile sidecar trim and break byte/hash parity — do not merge them.
/// </summary>
public static class BudgetOwnership
{
    /// <summary>
    /// Result of mapping the caller's whole-response <c>max_tokens</c> into the surface budget the
    /// MaterializationPlanner may spend.
    /// </summary>
    public sealed record Prepared(
        int? WholeResponseMaxTokens,
        int? SurfaceMaxTokens,
        ResponseBudgetMarkdownCap? Caps)
    {
        public bool IsCapped => Caps is not null && WholeResponseMaxTokens is not null;
    }

    /// <summary>
    /// Phase 1 — before <c>MaterializationPlanner.Plan</c>: derive the surface MaxTokens from the
    /// caller's whole-response budget and raw sidecar inventory.
    /// </summary>
    public static Prepared PrepareSurfaceBudget(
        int? userMaxTokens,
        ResponseBudgetSidecars sidecars,
        ResponseBudgetMode mode = ResponseBudgetMode.Full)
    {
        if (userMaxTokens is not int maxTokens)
        {
            return new Prepared(null, null, null);
        }

        // Raw extraction inventory is not a public charge. Callers must explicitly project the
        // fields they will serialize; an unmarked inventory therefore contributes only receipt cost.
        if (!sidecars.IsProjected)
        {
            sidecars = ResponseProjection.Empty(sidecars.ExpectReceipt);
        }

        var caps = ResponseBudgetPlanner.AllocateMarkdownCap(maxTokens, sidecars, mode);
        return new Prepared(maxTokens, caps.MarkdownCap, caps);
    }

    /// <summary>
    /// Apply the surface budget onto a materialization request. Leaves FocusQuery / selectors /
    /// ProvenancePolicy untouched. When uncapped, returns <paramref name="request"/> unchanged.
    /// </summary>
    public static Knowledge.MaterializationRequest ApplyToRequest(
        Knowledge.MaterializationRequest request,
        Prepared prepared)
    {
        ArgumentNullException.ThrowIfNull(request);
        ArgumentNullException.ThrowIfNull(prepared);

        return prepared.SurfaceMaxTokens is int surface
            ? request with { MaxTokens = surface }
            : request;
    }
}
