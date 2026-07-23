namespace OccamMcp.Core.Consensus;

/// <summary>
/// SI-14 consensus core: compare a set of witness observations of one URL and classify their
/// agreement. Pure and deterministic (order-independent). A single node with multiple vantages
/// (http vs browser, anon vs session) is a local jury — divergence proves cloaking / personalization
/// / geo-variance / access-walling. The distributed jury (SI-14 moonshot) reuses this same comparison
/// over remote-signed receipts.
/// </summary>
public static class ConsensusEvaluator
{
    public const string Consensus = "consensus";
    public const string Divergent = "divergent";
    public const string AccessDivergent = "access_divergent";
    public const string Inconclusive = "inconclusive";

    public static ConsensusVerdict Evaluate(IReadOnlyList<VantageObservation> vantages)
    {
        var usable = vantages.Where(v => v.Ok && Fingerprint(v) is not null).ToList();
        var walls = vantages.Where(v => !v.Ok && v.IsAccessWall).ToList();

        var pairs = new List<DivergencePair>();
        for (var i = 0; i < usable.Count; i++)
        {
            for (var j = i + 1; j < usable.Count; j++)
            {
                pairs.Add(BuildPair(usable[i], usable[j]));
            }
        }

        // One witness saw real content while another hit a provable wall — the strongest cloaking
        // signal (the classic "serve the bot a clean page, serve the user a wall", or the reverse).
        string verdict;
        if (usable.Count >= 1 && walls.Count >= 1)
        {
            verdict = AccessDivergent;
        }
        else if (usable.Count >= 2)
        {
            var distinct = usable.Select(Fingerprint).Distinct(StringComparer.Ordinal).Count();
            verdict = distinct == 1 ? Consensus : Divergent;
        }
        else
        {
            verdict = Inconclusive; // fewer than two usable witnesses (transient failures / single vantage)
        }

        return new ConsensusVerdict(verdict, pairs);
    }

    private static string? Fingerprint(VantageObservation v) => v.BlockMerkleRoot ?? v.ContentHash;

    private static DivergencePair BuildPair(VantageObservation a, VantageObservation b)
    {
        var rootsMatch = string.Equals(Fingerprint(a), Fingerprint(b), StringComparison.Ordinal);
        int? common = null;
        int? total = null;
        if (a.LeafHashes is { Length: > 0 } la && b.LeafHashes is { Length: > 0 } lb)
        {
            var sa = new HashSet<string>(la, StringComparer.Ordinal);
            var sb = new HashSet<string>(lb, StringComparer.Ordinal);
            common = sa.Count(sb.Contains);
            var union = new HashSet<string>(sa, StringComparer.Ordinal);
            union.UnionWith(sb);
            total = union.Count;
        }

        return new DivergencePair(a.Label, b.Label, rootsMatch, common, total);
    }
}
