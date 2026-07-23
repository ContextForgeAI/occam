using System.Text.RegularExpressions;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Claims;

/// <summary>One block's relevance to the claim: BM25 score + how many claim terms it covers.</summary>
public sealed record ClaimBlockRank(int Index, double Score, int MatchedTerms, int ClaimTerms)
{
    /// <summary>Relevance floor: cover at least ~40% of the claim's content terms (≥1), so a single
    /// common word is not a "match" (guards the honesty of <c>found</c>).</summary>
    public bool ClearsFloor => ClaimTerms > 0 && MatchedTerms >= Math.Max(1, (int)Math.Ceiling(0.4 * ClaimTerms));
}

/// <summary>
/// SI-16: rank extraction blocks by BM25 relevance to a claim (the same k1/b formula as
/// <see cref="Services.MapLinkRanker"/>, generalised over block text). Pure and deterministic — the
/// stance (support vs refute) is NOT decided here; this only surfaces which source text is relevant so
/// the caller can ground the claim in a provable block.
/// </summary>
public static partial class ClaimBlockRanker
{
    private const double K1 = 1.2;
    private const double B = 0.75;

    public static IReadOnlyList<ClaimBlockRank> Rank(IReadOnlyList<WorkerExtractBlockInfo> blocks, string claim)
    {
        var claimTerms = Tokenize(claim).Distinct(StringComparer.Ordinal).ToList();
        if (blocks.Count == 0 || claimTerms.Count == 0)
        {
            return [];
        }

        var docs = new List<List<string>>(blocks.Count);
        var docFreq = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var block in blocks)
        {
            var tokens = Tokenize(block.Text ?? string.Empty);
            docs.Add(tokens);
            foreach (var term in tokens.Distinct(StringComparer.Ordinal))
            {
                docFreq.TryGetValue(term, out var count);
                docFreq[term] = count + 1;
            }
        }

        var avgDl = docs.Count == 0 ? 1.0 : docs.Average(d => Math.Max(1, d.Count));
        var ranks = new List<ClaimBlockRank>(blocks.Count);
        for (var i = 0; i < blocks.Count; i++)
        {
            var (score, matched) = Score(docs[i], blocks[i].Text ?? string.Empty, claimTerms, docFreq, blocks.Count, avgDl);
            ranks.Add(new ClaimBlockRank(i, score, matched, claimTerms.Count));
        }

        // Highest score first; ties broken by block order for determinism.
        return ranks
            .OrderByDescending(r => r.Score)
            .ThenBy(r => r.Index)
            .ToList();
    }

    private static (double Score, int Matched) Score(
        List<string> tokens,
        string docText,
        List<string> claimTerms,
        Dictionary<string, int> docFreq,
        int docCount,
        double avgDl)
    {
        var dl = Math.Max(1, tokens.Count);
        var score = 0.0;
        var matched = 0;

        foreach (var term in claimTerms)
        {
            docFreq.TryGetValue(term, out var df);
            var tf = 0;
            foreach (var token in tokens)
            {
                if (term == token
                    || (token.Length >= 4 && term.StartsWith(token, StringComparison.Ordinal))
                    || (term.Length >= 4 && token.StartsWith(term, StringComparison.Ordinal)))
                {
                    tf++;
                }
            }

            if (tf == 0 && docText.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                tf = 1;
            }

            if (tf == 0)
            {
                continue;
            }

            matched++;
            var idf = Math.Log(1 + (docCount - df + 0.5) / (df + 0.5));
            var numerator = tf * (K1 + 1);
            var denominator = tf + K1 * (1 - B + B * dl / avgDl);
            score += idf * numerator / denominator;
        }

        return (score, matched);
    }

    private static List<string> Tokenize(string text) =>
        WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => w.Length >= 3)
            .ToList();

    [GeneratedRegex(@"[a-z0-9]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}
