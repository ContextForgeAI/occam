using OccamMcp.Core.Knowledge.Canonical;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Outcome of resolving a claim's provenance chain from a <see cref="MaterializedKnowledgeView"/>
/// (migration PR-D). Structural resolution is independent of Merkle verification.
/// </summary>
public enum ProvenanceTraceStatus
{
    /// <summary>Claim → Evidence → Source resolved; leaf bridge present when requested verify is skipped.</summary>
    Resolved = 0,

    ClaimNotFound = 1,
    EvidenceMissing = 2,
    SourceMissing = 3,

    /// <summary>Chain resolved but no block-leaf bridge exists to verify against a receipt.</summary>
    NoLeafBridge = 4,

    /// <summary>Leaf hash is not among the supplied receipt <c>blockLeaves</c>.</summary>
    LeafNotInReceipt = 5,

    /// <summary>Supplied leaves do not reconstruct the signed <c>blockMerkleRoot</c>.</summary>
    RootMismatch = 6,
}

/// <summary>
/// Read-only provenance answer for one claim. <see cref="MembershipVerified"/> proves only that a
/// cited leaf was in a signed extract — never that the claim is true (ADR-0003 / attest honesty:
/// retrieval ≠ support; Merkle inclusion ≠ semantic correctness).
/// </summary>
public sealed record ProvenanceTrace(
    ProvenanceTraceStatus Status,
    ClaimCandidate? Claim = null,
    Evidence? Evidence = null,
    Source? Source = null,
    KnowledgeProvenance? Provenance = null,
    string? BlockLeafHash = null,
    string? ReceiptContentHash = null,
    int? LeafIndex = null,
    IReadOnlyList<MerkleProofStep>? MembershipProof = null,
    bool? MembershipVerified = null)
{
    /// <summary>True when Claim, Evidence and Source were all located in the view.</summary>
    public bool ChainResolved => Claim is not null && Evidence is not null && Source is not null;
}
