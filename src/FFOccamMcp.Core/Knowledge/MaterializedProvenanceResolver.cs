using OccamMcp.Core.Knowledge.Canonical;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Migration PR-D: read-only helper that answers "where did this claim come from?" against a
/// <see cref="MaterializedKnowledgeView"/>. Resolves Claim → Evidence → Source and optionally verifies
/// the leaf against receipt <c>blockLeaves</c> / <c>blockMerkleRoot</c> using the same
/// <see cref="MerkleTree"/> primitives as <c>occam_verify</c> prove/citation modes.
///
/// <para>Pure / fail-closed / no network / no MCP / does not invent missing links / does not promote
/// claims to Facts / does not treat membership as truth.</para>
/// </summary>
public static class MaterializedProvenanceResolver
{
    /// <summary>
    /// Resolve the structural provenance chain for <paramref name="claimId"/> from the view.
    /// Does not verify Merkle membership.
    /// </summary>
    public static ProvenanceTrace Resolve(MaterializedKnowledgeView view, ClaimCandidateId claimId)
    {
        ArgumentNullException.ThrowIfNull(view);

        if (!TryLocateChain(view, claimId, out var claim, out var evidence, out var source, out var provenance, out var status))
        {
            return new ProvenanceTrace(status, Claim: claim, Evidence: evidence, Source: source, Provenance: provenance);
        }

        var leaf = ResolveLeafHash(evidence, provenance);
        var contentHash = provenance?.ReceiptContentHash ?? source.ContentHash;
        return new ProvenanceTrace(
            ProvenanceTraceStatus.Resolved,
            Claim: claim,
            Evidence: evidence,
            Source: source,
            Provenance: provenance,
            BlockLeafHash: leaf,
            ReceiptContentHash: contentHash);
    }

    /// <summary>
    /// Resolve the chain and verify the leaf against receipt leaves (and optional signed root).
    /// Membership success means only "this leaf was in the signed extract", never claim truth.
    /// </summary>
    public static ProvenanceTrace ResolveAndVerify(
        MaterializedKnowledgeView view,
        ClaimCandidateId claimId,
        IReadOnlyList<string> blockLeaves,
        string? blockMerkleRoot = null)
    {
        ArgumentNullException.ThrowIfNull(view);
        ArgumentNullException.ThrowIfNull(blockLeaves);

        if (!TryLocateChain(view, claimId, out var claim, out var evidence, out var source, out var provenance, out var status))
        {
            return new ProvenanceTrace(status, Claim: claim, Evidence: evidence, Source: source, Provenance: provenance);
        }

        var leaf = ResolveLeafHash(evidence, provenance);
        var contentHash = provenance?.ReceiptContentHash ?? source.ContentHash;
        if (string.IsNullOrWhiteSpace(leaf))
        {
            return new ProvenanceTrace(
                ProvenanceTraceStatus.NoLeafBridge,
                Claim: claim,
                Evidence: evidence,
                Source: source,
                Provenance: provenance,
                BlockLeafHash: null,
                ReceiptContentHash: contentHash,
                MembershipVerified: false);
        }

        if (!string.IsNullOrWhiteSpace(blockMerkleRoot))
        {
            if (blockLeaves.Count == 0
                || !string.Equals(MerkleTree.RootFromLeafHashes(blockLeaves), blockMerkleRoot, StringComparison.Ordinal))
            {
                return new ProvenanceTrace(
                    ProvenanceTraceStatus.RootMismatch,
                    Claim: claim,
                    Evidence: evidence,
                    Source: source,
                    Provenance: provenance,
                    BlockLeafHash: leaf,
                    ReceiptContentHash: contentHash,
                    MembershipVerified: false);
            }
        }

        var index = IndexOfLeaf(blockLeaves, leaf);
        if (index < 0)
        {
            return new ProvenanceTrace(
                ProvenanceTraceStatus.LeafNotInReceipt,
                Claim: claim,
                Evidence: evidence,
                Source: source,
                Provenance: provenance,
                BlockLeafHash: leaf,
                ReceiptContentHash: contentHash,
                MembershipVerified: false);
        }

        var proof = MerkleTree.Proof(blockLeaves, index);
        var root = blockMerkleRoot ?? MerkleTree.RootFromLeafHashes(blockLeaves);
        var membershipOk = root is not null && MerkleTree.VerifyProof(leaf, proof, root);

        return new ProvenanceTrace(
            ProvenanceTraceStatus.Resolved,
            Claim: claim,
            Evidence: evidence,
            Source: source,
            Provenance: provenance,
            BlockLeafHash: leaf,
            ReceiptContentHash: contentHash,
            LeafIndex: index,
            MembershipProof: proof,
            MembershipVerified: membershipOk);
    }

    private static bool TryLocateChain(
        MaterializedKnowledgeView view,
        ClaimCandidateId claimId,
        out ClaimCandidate? claim,
        out Evidence? evidence,
        out Source? source,
        out KnowledgeProvenance? provenance,
        out ProvenanceTraceStatus status)
    {
        claim = null;
        evidence = null;
        source = null;
        provenance = null;

        if (view.Claims is null || view.Claims.Count == 0)
        {
            status = ProvenanceTraceStatus.ClaimNotFound;
            return false;
        }

        claim = view.Claims.FirstOrDefault(c => c.Id.Equals(claimId));
        if (claim is null)
        {
            status = ProvenanceTraceStatus.ClaimNotFound;
            return false;
        }

        if (claim.EvidenceRefs.Count == 0 || view.EvidenceRefs is null)
        {
            status = ProvenanceTraceStatus.EvidenceMissing;
            return false;
        }

        // First evidence ref that is present in the view (multi-evidence selection deferred).
        Evidence? foundEvidence = null;
        foreach (var wantedEvidenceId in claim.EvidenceRefs)
        {
            foundEvidence = view.EvidenceRefs.FirstOrDefault(e => e.Id.Equals(wantedEvidenceId));
            if (foundEvidence is not null)
            {
                break;
            }
        }

        if (foundEvidence is null)
        {
            status = ProvenanceTraceStatus.EvidenceMissing;
            return false;
        }

        evidence = foundEvidence;

        if (view.SourceRefs is null)
        {
            status = ProvenanceTraceStatus.SourceMissing;
            return false;
        }

        var evidenceSourceId = evidence.SourceId;
        source = view.SourceRefs.FirstOrDefault(s => s.Id.Equals(evidenceSourceId));
        if (source is null)
        {
            status = ProvenanceTraceStatus.SourceMissing;
            return false;
        }

        var resolvedSourceId = source.Id;
        var resolvedEvidenceId = evidence.Id;
        provenance = view.Provenance?.FirstOrDefault(p =>
            p.SourceId.Equals(resolvedSourceId)
            && p.EvidenceIds.Any(id => id.Equals(resolvedEvidenceId)));

        status = ProvenanceTraceStatus.Resolved;
        return true;
    }

    private static string? ResolveLeafHash(Evidence evidence, KnowledgeProvenance? provenance)
    {
        if (!string.IsNullOrWhiteSpace(provenance?.BlockLeafHash))
        {
            return provenance.BlockLeafHash.Trim().ToLowerInvariant();
        }

        // Legacy Adapter stores the Merkle leaf on Evidence.ContentHash for content blocks.
        if (evidence.Kind == EvidenceKind.ContentBlock && !string.IsNullOrWhiteSpace(evidence.ContentHash))
        {
            return evidence.ContentHash.Trim().ToLowerInvariant();
        }

        return null;
    }

    private static int IndexOfLeaf(IReadOnlyList<string> leaves, string leafHex)
    {
        for (var i = 0; i < leaves.Count; i++)
        {
            if (string.Equals(leaves[i], leafHex, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }
}
