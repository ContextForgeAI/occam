namespace OccamMcp.Core.Receipts;

/// <summary>Per-chunk staleness verdict: which specific chunk hashes are gone from the live page.</summary>
public sealed record ChunkStalenessResult(int Total, int Present, int Stale, string[] StaleChunks);

/// <summary>
/// SI-12: chunk-level RAG expiry. A RAG store holds a set of chunk leaf-hashes for a URL (from index
/// time). Against the live page's current leaf set, this reports WHICH specific chunks are stale — so a
/// consumer invalidates individual fragments, not whole documents ("this fragment's source changed").
/// Pure and deterministic; the live leaf set is computed by the re-fetch in <c>occam_verify</c>.
/// </summary>
public static class ChunkStalenessEvaluator
{
    public static ChunkStalenessResult Compute(IReadOnlyList<string> sourceChunkHashes, ISet<string> currentLeaves)
    {
        if (sourceChunkHashes.Count == 0)
        {
            return new ChunkStalenessResult(0, 0, 0, []);
        }

        var stale = sourceChunkHashes.Where(h => !currentLeaves.Contains(h)).ToArray();
        return new ChunkStalenessResult(sourceChunkHashes.Count, sourceChunkHashes.Count - stale.Length, stale.Length, stale);
    }
}
