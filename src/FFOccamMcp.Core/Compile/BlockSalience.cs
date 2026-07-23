using OccamMcp.Core.Claims;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Compile;

/// <summary>
/// #3 span-substrate: annotate extraction blocks with a per-span <c>salience</c> (0–1) — the block's
/// BM25 relevance to the focus query, normalized to the top-scoring block. An explicit machine-native
/// attention signal: the consuming LLM sees which spans to weight/cite, rather than re-deriving
/// importance from the full text. Reuses the deterministic <see cref="ClaimBlockRanker"/> (same BM25).
/// In-place; a page with no query or no matches leaves salience null (omitted).
/// </summary>
public static class BlockSalience
{
    public static void Annotate(IReadOnlyList<WorkerExtractBlockInfo> blocks, string? focusQuery)
    {
        if (blocks.Count == 0 || string.IsNullOrWhiteSpace(focusQuery))
        {
            return;
        }

        var ranks = ClaimBlockRanker.Rank(blocks, focusQuery);
        if (ranks.Count == 0)
        {
            return;
        }

        var max = ranks.Max(r => r.Score);
        if (max <= 0)
        {
            // Query matched nothing — every block is equally (un)related; a flat 0 is honest and lets
            // the consumer distinguish "ranked, no signal" from "not ranked" (null).
            foreach (var block in blocks)
            {
                block.Salience = 0.0;
            }

            return;
        }

        foreach (var rank in ranks)
        {
            blocks[rank.Index].Salience = Math.Round(rank.Score / max, 4);
        }
    }
}
