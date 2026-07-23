using OccamMcp.Core.Attest;
using OccamMcp.Core.Receipts;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_ATTEST — SI-11 three-layer attest core. Retrieval (BM25) is not tested here as support;
/// semantic classification + grounded alias + Merkle existence proof are gate-testable offline.
/// </summary>
public static class AttestUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        // --- Required asyncio regressions (synthetic blocks; no network) ---
        assert(
            "asyncio is library -> supported",
            ClaimSemanticClassifier.Classify(
                "asyncio is library",
                ["asyncio is a library to write concurrent code using the async/await syntax."])
            == AttestStatus.Supported);

        assert(
            "asyncio is database engine -> unsupported (lexical co-occur is not support)",
            ClaimSemanticClassifier.Classify(
                "asyncio is database engine",
                ["Networking and inter-process communication, including database connection libraries."])
            == AttestStatus.Unsupported);

        assert(
            "asyncio uses async/await -> supported",
            ClaimSemanticClassifier.Classify(
                "asyncio uses async/await",
                ["asyncio uses async/await syntax to write concurrent code."])
            == AttestStatus.Supported);

        assert(
            "asyncio is SQL database -> unsupported",
            ClaimSemanticClassifier.Classify(
                "asyncio is SQL database",
                ["Networking and inter-process communication, including database connection libraries."])
            == AttestStatus.Unsupported);

        // Same false claim even when subject co-occurs with database terms (still not is-a).
        assert(
            "asyncio + database collocates without is-a -> unsupported",
            ClaimSemanticClassifier.Classify(
                "asyncio is a database engine",
                ["asyncio includes helpers for database connection libraries and sockets."])
            == AttestStatus.Unsupported);

        // Fail-closed: unparsed claim shape → unknown (not supported).
        assert(
            "unparsed claim -> unknown",
            ClaimSemanticClassifier.Classify("perhaps concurrency somehow", ["asyncio is a library."])
            == AttestStatus.Unknown);

        // Empty retrieval: complete → unsupported; incomplete → unknown.
        assert(
            "empty complete retrieval -> unsupported",
            ClaimSemanticClassifier.Classify("asyncio is a library", [], retrievalComplete: true)
            == AttestStatus.Unsupported);
        assert(
            "empty incomplete retrieval -> unknown",
            ClaimSemanticClassifier.Classify("asyncio is a library", [], retrievalComplete: false)
            == AttestStatus.Unknown);

        // Contradiction.
        assert(
            "explicit is-not -> contradicted",
            ClaimSemanticClassifier.Classify(
                "asyncio is a database",
                ["asyncio is not a database; it is a library for concurrent code."])
            == AttestStatus.Contradicted);

        // grounded alias: only supported.
        assert("grounded alias for supported", AttestClassifier.IsGrounded(AttestStatus.Supported));
        assert("not grounded for related", !AttestClassifier.IsGrounded(AttestStatus.Related));
        assert("not grounded for unsupported", !AttestClassifier.IsGrounded(AttestStatus.Unsupported));
        assert("not grounded for unknown", !AttestClassifier.IsGrounded(AttestStatus.Unknown));
        assert("not grounded for contradicted", !AttestClassifier.IsGrounded(AttestStatus.Contradicted));

        // Summarize named counts + unsupportedTotal partition.
        static OccamAttestClaimResult R(string status) =>
            new("c", "https://ex", status, AttestStatus.IsGroundedAlias(status),
                null, null, null, null, null, null, null, status == AttestStatus.Supported ? null : "x");

        var perClaim = new[]
        {
            R(AttestStatus.Supported),
            R(AttestStatus.Unsupported),
            R(AttestStatus.Related),
            R(AttestStatus.Contradicted),
            R(AttestStatus.Unknown),
            R(AttestStatus.Supported),
        };
        var counts = AttestClassifier.Summarize(perClaim);
        assert("summarize supported", counts.Supported == 2);
        assert("summarize contradicted", counts.Contradicted == 1);
        assert("summarize related", counts.Related == 1);
        assert("summarize unsupported", counts.Unsupported == 1);
        assert("summarize unknown", counts.Unknown == 1);
        assert("summarize unsupportedTotal = non-supported",
            counts.UnsupportedTotal == 4 && counts.Supported + counts.UnsupportedTotal == perClaim.Length);
        assert("summarize empty -> zero",
            AttestClassifier.Summarize([]) is { Supported: 0, UnsupportedTotal: 0 });

        // Layer 3: Merkle proof still proves existence only (independent of status).
        (string Text, string? SourceSelector)[] blocks =
            [("first block", "#a"), ("asyncio is a library to write concurrent code", "#b"), ("third", "#c")];
        var leaves = MerkleTree.LeafHashesHex(blocks);
        var root = MerkleTree.RootFromLeafHashes(leaves);
        var proof = MerkleTree.Proof(leaves, 1);
        assert("attest existence proof reconstructs the signed root",
            root is not null && MerkleTree.VerifyProof(leaves[1], proof, root));

        Console.WriteLine("L_ATTEST_OK");
    }
}
