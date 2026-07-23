using OccamMcp.Core.Claims;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_CLAIM — SI-16 claim-check core. Pure, deterministic BM25 block ranking + the relevance floor +
/// citation-proof round-trip (no network). Stance is never inferred; this only proves which source
/// block is relevant. Live claim-check stays a smoke concern.
/// </summary>
public static class ClaimCheckUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        static WorkerExtractBlockInfo B(string text, string selector) =>
            new() { Type = "paragraph", Text = text, SourceSelector = selector };

        var blocks = new[]
        {
            B("The sky appears blue on a clear day due to scattering.", "#a"),
            B("Photosynthesis converts sunlight into chemical energy inside green plants.", "#b"),
            B("Contact us at the office for more information.", "#c"),
        };

        var ranked = ClaimBlockRanker.Rank(blocks, "plants use photosynthesis to convert sunlight into energy");
        assert("claim ranks the relevant block first",
            ranked.Count == 3 && ranked[0].Index == 1 && ranked[0].ClearsFloor);
        assert("claim floor uses ~40% term coverage",
            ranked[0].MatchedTerms >= 3 && ranked[0].ClaimTerms >= 5);

        // No blocks / empty claim → no ranking.
        assert("no blocks -> empty ranking", ClaimBlockRanker.Rank([], "anything relevant").Count == 0);
        assert("empty claim -> empty ranking", ClaimBlockRanker.Rank(blocks, "   ").Count == 0);

        // A claim sharing only one common word with any block clears the floor for none of them.
        var weak = ClaimBlockRanker.Rank(blocks, "office building downtown location parking garage");
        assert("single-term overlap does not clear the floor", !weak.Any(r => r.ClearsFloor));

        // Citation-proof round-trip: the matched block's leaf + proof reconstruct the Merkle root.
        (string Text, string? SourceSelector)[] merkleBlocks =
            [.. blocks.Select(b => (b.Text, (string?)b.SourceSelector))];
        var leaves = MerkleTree.LeafHashesHex(merkleBlocks);
        var root = MerkleTree.RootFromLeafHashes(leaves);
        var top = ranked[0].Index;
        var proof = MerkleTree.Proof(leaves, top);
        assert("claim match proof reconstructs the signed root",
            root is not null && MerkleTree.VerifyProof(leaves[top], proof, root));
        assert("claim match leaf matches recomputed block leaf",
            leaves[top] == Convert.ToHexString(MerkleTree.LeafHash(blocks[top].Text, blocks[top].SourceSelector)).ToLowerInvariant());

        Console.WriteLine("L_CLAIM_OK");
    }
}
