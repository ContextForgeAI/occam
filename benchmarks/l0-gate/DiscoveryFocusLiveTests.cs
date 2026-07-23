using OccamMcp.Core.Agent;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;

namespace OccamMcp.L0Gate;

/// <summary>
/// Live discovery focus diagnostics (full L2 path, not --fast).
/// Prints candidate/score breakdown to stderr; asserts entity pages win.
/// </summary>
internal static class DiscoveryFocusLiveTests
{
    private const string PythonSeed = "https://docs.python.org/3/";
    private const string FocusQuery = "asyncio event loop tasks synchronization";

    public static void Run(MapService map, DigestService digest, Action<string, bool> assert)
    {
        Console.Error.WriteLine("=== DiscoveryFocusLive: map ===");
        RunMapLive(map, assert);

        Console.Error.WriteLine("=== DiscoveryFocusLive: digest source_url ===");
        RunDigestLive(digest, assert);

        Console.WriteLine("L_DISCOVERY_FOCUS_LIVE_OK");
    }

    private static void RunMapLive(MapService map, Action<string, bool> assert)
    {
        var decomp = FocusQueryDecomposition.Decompose(FocusQuery);
        Console.Error.WriteLine(
            $"primary_anchors=[{string.Join(", ", decomp.PrimaryAnchors)}] supporting=[{string.Join(", ", decomp.SupportingTerms)}]");

        var analysis = map.MapAsync(
            PythonSeed,
            maxLinks: 8,
            sameDomainOnly: true,
            timeoutMs: 20_000,
            source: "homepage",
            filterNonsense: true,
            focusQuery: FocusQuery).GetAwaiter().GetResult();

        assert("live map: ok", analysis.Ok);
        if (!analysis.Ok)
        {
            Console.Error.WriteLine($"live map failed: {analysis.FailureCode}");
            return;
        }

        Console.Error.WriteLine(
            $"hub_expansion_ran={analysis.Expanded} link_count={analysis.LinkCount} filtered={analysis.FilteredCount}");

        // Re-score the returned set for diagnostics (pool after cap).
        var detailed = MapLinkRanker.RankDetailed(analysis.Links.ToList(), FocusQuery);
        PrintTopBreakdown(detailed);

        var top5HasAsyncio = analysis.Links.Take(5).Any(l =>
            l.Path.Contains("/library/asyncio", StringComparison.OrdinalIgnoreCase));
        assert("live map: top5 contains /library/asyncio", top5HasAsyncio);

        if (detailed.Count >= 2)
        {
            Console.Error.WriteLine(
                $"why_winner: path={detailed[0].Link.Path} total={detailed[0].Total:F2} " +
                $"(pathSeg={detailed[0].PathSegment:F1} title={detailed[0].TitleToken:F1} " +
                $"missPrimary={detailed[0].MissingPrimaryPenaltyApplied:F1} ver={detailed[0].VersionPenaltyApplied:F1}) " +
                $"vs runner_up={detailed[1].Link.Path} total={detailed[1].Total:F2}");
        }
    }

    private static void RunDigestLive(DigestService digest, Action<string, bool> assert)
    {
        var analysis = digest.DigestAsync(
            entries: null,
            maxUrls: 8,
            perUrlMaxTokens: 256,
            backendPolicy: OccamBackendPolicy.Http,
            focusQuery: FocusQuery,
            fitMarkdown: true,
            includeCombined: true,
            sessionProfile: null,
            sourceUrl: PythonSeed,
            maxLinks: 8).GetAwaiter().GetResult();

        assert("live digest: ok or focus honesty", analysis.Ok || analysis.FocusNotFound);
        if (!analysis.Ok)
        {
            Console.Error.WriteLine($"live digest failed: {analysis.FailureCode} {analysis.FailureMessage}");
            return;
        }

        var discovered = analysis.DiscoveredLinks ?? [];
        Console.Error.WriteLine($"discovered_count={discovered.Count} focus_not_found={analysis.FocusNotFound}");
        foreach (var url in discovered.Take(8))
        {
            Console.Error.WriteLine($"  discovered: {url}");
        }

        var versionRootish = discovered.Count(u =>
            Uri.TryCreate(u, UriKind.Absolute, out var uri)
            && MapLinkRanker.LooksLikeVersionRoot(uri.AbsolutePath));
        assert(
            "live digest: not mostly version roots",
            discovered.Count == 0 || versionRootish < discovered.Count / 2 + 1);

        var hasAsyncio = discovered.Any(u =>
            u.Contains("/library/asyncio", StringComparison.OrdinalIgnoreCase));
        assert("live digest: discovered contains /library/asyncio", hasAsyncio);

        var hints = DigestAgentHints.ForDigest(analysis);
        if (analysis.FocusNotFound)
        {
            assert(
                "live digest: focus_not_found warning when unfocused",
                hints.Warnings.Any(w => w.StartsWith("focus_not_found:", StringComparison.Ordinal)));
        }

        var matched = analysis.Items.Count(i => i.FocusMatched == true);
        Console.Error.WriteLine($"focusMatched_true={matched}/{analysis.Items.Count(i => i.Ok)}");
    }

    private static void PrintTopBreakdown(IReadOnlyList<MapLinkRanker.ScoreBreakdown> detailed)
    {
        Console.Error.WriteLine($"candidates_in_result_set={detailed.Count} (post-cap; pre-cap ≥ this when expanded)");
        foreach (var row in detailed.Take(5))
        {
            Console.Error.WriteLine(
                $"  score={row.Total:F2} path={row.Link.Path} " +
                $"prim=[{string.Join(',', row.PrimaryHits)}] " +
                $"pathSeg={row.PathSegment:F1} title={row.TitleToken:F1} phrase={row.AnchorPhrase:F1} " +
                $"supp={row.Supporting:F1} bm25={row.Bm25:F2} " +
                $"penPrimary={row.MissingPrimaryPenaltyApplied:F1} penVer={row.VersionPenaltyApplied:F1}");
        }
    }
}
