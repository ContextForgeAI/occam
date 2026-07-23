namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Fixed deterministic planner-policy profiles for <see cref="PlannerBench"/>.
/// Benchmark fixtures only — not public MCP product profiles.
/// </summary>
public static class PlannerBenchProfiles
{
    /// <summary>Compatibility-like path: generous budget, no focus, default Canonical retention.</summary>
    public static MaterializationRequest Compat { get; } = new(
        MaxTokens: 4096,
        FocusQuery: null,
        FitMarkdown: false,
        ContentSelectors: null,
        CapabilityProfile: "default",
        ProvenancePolicy: CanonicalRetention.PolicyDefault,
        DisclosurePolicy: "default");

    /// <summary>
    /// Smaller materialized view via a lower token budget. Default ProvenancePolicy may prune
    /// Canonical claims when the claim budget (MaxTokens/4) binds.
    /// </summary>
    public static MaterializationRequest Compact { get; } = new(
        MaxTokens: 128,
        FocusQuery: null,
        FitMarkdown: false,
        ContentSelectors: null,
        CapabilityProfile: "default",
        ProvenancePolicy: CanonicalRetention.PolicyDefault,
        DisclosurePolicy: "default");

    /// <summary>
    /// Query-conditioned materialization. Budget aligned with <see cref="Compact"/> so differences
    /// are attributable to focus/fit (and focus-ranked Canonical under default policy).
    /// </summary>
    public static MaterializationRequest Focus(string focusQuery) => new(
        MaxTokens: 128,
        FocusQuery: focusQuery,
        FitMarkdown: true,
        ContentSelectors: null,
        CapabilityProfile: "default",
        ProvenancePolicy: CanonicalRetention.PolicyDefault,
        DisclosurePolicy: "default");

    /// <summary>
    /// Surface budget aligned with <see cref="Compact"/>, but Canonical claim→evidence→source→
    /// provenance closure is fully retained (<c>evidence-preserving</c>). Differs from compact on
    /// Canonical metrics when the claim budget would otherwise bind.
    /// </summary>
    public static MaterializationRequest EvidencePreserving { get; } = new(
        MaxTokens: 128,
        FocusQuery: null,
        FitMarkdown: false,
        ContentSelectors: null,
        CapabilityProfile: "default",
        ProvenancePolicy: CanonicalRetention.PolicyEvidencePreserving,
        DisclosurePolicy: "default");

    /// <summary>Standard case set for a fixture. <paramref name="focusQuery"/> required for the focus case.</summary>
    public static IReadOnlyList<PlannerBenchCase> StandardCases(string focusQuery) =>
    [
        new("compat", Compat),
        new("compact", Compact),
        new("focus", Focus(focusQuery)),
        new("evidence-preserving", EvidencePreserving),
    ];
}
