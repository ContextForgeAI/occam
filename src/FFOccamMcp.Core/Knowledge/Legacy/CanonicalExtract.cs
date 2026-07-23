using OccamMcp.Core.Knowledge.Canonical;

namespace OccamMcp.Core.Knowledge.Legacy;

/// <summary>
/// Result of the 0.9 → canonical Legacy Adapter (migration PR-B). Pure in-memory snapshot —
/// not a store. Blocks/tables become <see cref="Evidence"/>; block text becomes
/// <see cref="ClaimCandidate"/> only (never <see cref="Fact"/>).
/// </summary>
public sealed record CanonicalExtract(
    Source Source,
    IReadOnlyList<Evidence> Evidence,
    IReadOnlyList<ClaimCandidate> Claims,
    IReadOnlyList<KnowledgeProvenance> Provenance);
