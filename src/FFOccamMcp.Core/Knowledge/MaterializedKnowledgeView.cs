using OccamMcp.Core.Knowledge.Canonical;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// The explicit planner↔codec boundary object (ADR-0002): a task-shaped selection ready to serialize
/// but NOT yet bound to any codec syntax. Produced by <see cref="MaterializationPlanner"/>; consumed
/// by codecs unchanged. Distinct from Canonical Knowledge records (<c>Knowledge.Canonical</c>) and from
/// the document IR (<see cref="KnowledgeDocument"/>).
///
/// <para>The opaque <see cref="Surface"/> carries the compatibility projection (today: Markdown bytes).
/// Canonical models never reference Markdown. The <see cref="Markdown"/> accessor is a convenience for
/// Markdown-family codecs and the public MCP DTO bridge — not a Canonical field.</para>
///
/// <para>Future-proofing (no store in 1.1): keep this type serializable and identity/provenance-bearing
/// so a later Knowledge Store can reuse views as durable artifacts without a redesign. Do not assume
/// the view is valid only inside a single in-memory pipeline call.</para>
/// </summary>
public sealed record MaterializedKnowledgeView(
    SourceSurface Surface,
    KnowledgeDocument? Knowledge = null,
    IReadOnlyList<Source>? SourceRefs = null,
    IReadOnlyList<Evidence>? EvidenceRefs = null,
    IReadOnlyList<ClaimCandidate>? Claims = null,
    IReadOnlyList<KnowledgeProvenance>? Provenance = null)
{
    /// <summary>
    /// Compatibility factory used by tests and migration call sites that still speak Markdown.
    /// </summary>
    public static MaterializedKnowledgeView FromMarkdown(
        string markdown,
        KnowledgeDocument? knowledge = null,
        IReadOnlyList<Source>? sourceRefs = null,
        IReadOnlyList<Evidence>? evidenceRefs = null,
        IReadOnlyList<ClaimCandidate>? claims = null,
        IReadOnlyList<KnowledgeProvenance>? provenance = null) =>
        new(SourceSurface.Markdown(markdown), knowledge, sourceRefs, evidenceRefs, claims, provenance);

    /// <summary>Markdown projection of <see cref="Surface"/> for Markdown-family codecs / MCP bridge.</summary>
    public string Markdown => Surface.Text;

    /// <summary>True when any canonical knowledge slot is non-empty.</summary>
    public bool HasCanonicalKnowledge =>
        (SourceRefs is { Count: > 0 })
        || (EvidenceRefs is { Count: > 0 })
        || (Claims is { Count: > 0 })
        || (Provenance is { Count: > 0 });
}
