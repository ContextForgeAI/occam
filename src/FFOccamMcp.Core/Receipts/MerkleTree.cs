using System.Security.Cryptography;
using System.Text;

namespace OccamMcp.Core.Receipts;

/// <summary>
/// Ordered SHA-256 Merkle tree over extraction blocks. The leaf preimage binds a block's text AND
/// its CSS location (D2): <c>text + '\0' + source_selector</c>, so a later citation (SI-02) can
/// prove "this quote was at this selector" without shipping the page. Odd levels duplicate the last
/// node (Bitcoin-style). Returns a lowercase-hex root prefixed with the hash alg, matching the
/// content-hash codec used elsewhere (<c>sha256:…</c>).
/// </summary>
public static class MerkleTree
{
    public const string HashPrefix = "sha256:";

    /// <summary>Leaf hash for one block. Public so the live-verify path (SI-06) can recompute membership.</summary>
    public static byte[] LeafHash(string text, string? sourceSelector)
    {
        var preimage = Encoding.UTF8.GetBytes($"{text}\0{sourceSelector ?? string.Empty}");
        return SHA256.HashData(preimage);
    }

    /// <summary>
    /// Ordered hex leaf hashes for the blocks (SI-02). Carried in the receipt so a consumer can do a
    /// granular live check — "N of M blocks still present" — by re-hashing the re-fetched blocks and
    /// counting membership. The leaves reconstruct the signed <see cref="Root"/>, so they are
    /// authentic without being individually signed.
    /// </summary>
    public static string[] LeafHashesHex(IReadOnlyList<(string Text, string? SourceSelector)> blocks)
    {
        var leaves = new string[blocks.Count];
        for (var i = 0; i < blocks.Count; i++)
        {
            leaves[i] = Convert.ToHexString(LeafHash(blocks[i].Text, blocks[i].SourceSelector)).ToLowerInvariant();
        }

        return leaves;
    }

    /// <summary>Root over pre-computed hex leaves — lets a verifier confirm supplied leaves match the signed root.</summary>
    public static string? RootFromLeafHashes(IReadOnlyList<string> leafHashesHex)
    {
        if (leafHashesHex.Count == 0)
        {
            return null;
        }

        var level = new List<byte[]>(leafHashesHex.Count);
        foreach (var hex in leafHashesHex)
        {
            level.Add(Convert.FromHexString(hex));
        }

        while (level.Count > 1)
        {
            var next = new List<byte[]>((level.Count + 1) / 2);
            for (var i = 0; i < level.Count; i += 2)
            {
                var right = i + 1 < level.Count ? level[i + 1] : level[i];
                next.Add(HashPair(level[i], right));
            }

            level = next;
        }

        return HashPrefix + Convert.ToHexString(level[0]).ToLowerInvariant();
    }

    /// <summary>
    /// Root over an ordered list of blocks. Empty list → null (a page with no json_blocks has no
    /// block root; the content hash still covers the markdown). Single leaf → that leaf is the root.
    /// </summary>
    public static string? Root(IReadOnlyList<(string Text, string? SourceSelector)> blocks)
    {
        if (blocks.Count == 0)
        {
            return null;
        }

        var level = new List<byte[]>(blocks.Count);
        foreach (var (text, selector) in blocks)
        {
            level.Add(LeafHash(text, selector));
        }

        while (level.Count > 1)
        {
            var next = new List<byte[]>((level.Count + 1) / 2);
            for (var i = 0; i < level.Count; i += 2)
            {
                var left = level[i];
                var right = i + 1 < level.Count ? level[i + 1] : level[i]; // duplicate last when odd
                next.Add(HashPair(left, right));
            }

            level = next;
        }

        return HashPrefix + Convert.ToHexString(level[0]).ToLowerInvariant();
    }

    /// <summary>
    /// SI-02b: a compact membership proof for the leaf at <paramref name="index"/> — the sibling
    /// hashes on the path to the root (≈log₂N of them). With it a third party can prove "this block
    /// was in the signed root" WITHOUT the page or the other leaves. Empty when the tree is a single
    /// leaf (the leaf is the root).
    /// </summary>
    public static IReadOnlyList<MerkleProofStep> Proof(IReadOnlyList<string> leafHashesHex, int index)
    {
        var proof = new List<MerkleProofStep>();
        if (index < 0 || index >= leafHashesHex.Count)
        {
            return proof;
        }

        var level = new List<byte[]>(leafHashesHex.Count);
        foreach (var hex in leafHashesHex)
        {
            level.Add(Convert.FromHexString(hex));
        }

        var idx = index;
        while (level.Count > 1)
        {
            var currentIsRight = idx % 2 == 1;
            var siblingIdx = currentIsRight ? idx - 1 : idx + 1;
            if (siblingIdx >= level.Count)
            {
                siblingIdx = idx; // odd tail duplicates itself
            }

            // sibling is on the opposite side of the current node
            proof.Add(new MerkleProofStep(Convert.ToHexString(level[siblingIdx]).ToLowerInvariant(), !currentIsRight));

            var next = new List<byte[]>((level.Count + 1) / 2);
            for (var i = 0; i < level.Count; i += 2)
            {
                var right = i + 1 < level.Count ? level[i + 1] : level[i];
                next.Add(HashPair(level[i], right));
            }

            level = next;
            idx /= 2;
        }

        return proof;
    }

    /// <summary>Recompute the root from a leaf + its proof and compare to <paramref name="root"/>.</summary>
    public static bool VerifyProof(string leafHashHex, IReadOnlyList<MerkleProofStep> proof, string root)
    {
        byte[] cur;
        try
        {
            cur = Convert.FromHexString(leafHashHex);
            foreach (var step in proof)
            {
                var sibling = Convert.FromHexString(step.Hash);
                cur = step.SiblingIsRight ? HashPair(cur, sibling) : HashPair(sibling, cur);
            }
        }
        catch (FormatException)
        {
            return false;
        }

        return string.Equals(HashPrefix + Convert.ToHexString(cur).ToLowerInvariant(), root, StringComparison.Ordinal);
    }

    private static byte[] HashPair(byte[] left, byte[] right)
    {
        var buffer = new byte[left.Length + right.Length];
        Buffer.BlockCopy(left, 0, buffer, 0, left.Length);
        Buffer.BlockCopy(right, 0, buffer, left.Length, right.Length);
        return SHA256.HashData(buffer);
    }
}

/// <summary>One step of a Merkle membership proof: a sibling hash and which side it sits on.</summary>
public sealed record MerkleProofStep(string Hash, bool SiblingIsRight);
