using OccamMcp.Core.Compile;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;

namespace OccamMcp.Core.Services;

public sealed class MapService(HttpProbeFetcher fetcher)
{
    public const int MaxLinksCap = 64;
    public const int DefaultMaxLinks = 32;
    public const int DefaultTimeoutMs = 15_000;
    public const int MinTimeoutMs = 3_000;
    public const int MaxTimeoutMs = 30_000;
    public const int MaxSecondLevelHubs = 3;

    public async Task<MapAnalysis> MapAsync(
        string url,
        int maxLinks = DefaultMaxLinks,
        bool sameDomainOnly = true,
        int timeoutMs = DefaultTimeoutMs,
        string source = "homepage",
        bool filterNonsense = true,
        string? focusQuery = null,
        string? sessionProfile = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            return MapAnalysis.Failed(url, null, "invalid_url", 0);
        }

        var preflight = FetchPreflight.Prepare(url, sessionProfile);
        if (!preflight.Ok)
        {
            return MapAnalysis.Failed(url, null, preflight.FailureCode!, 0);
        }

        using (preflight.HeadersScope)
        {
            return await MapCoreAsync(
                url,
                maxLinks,
                sameDomainOnly,
                timeoutMs,
                source,
                filterNonsense,
                focusQuery,
                preflight.MergedHeaders,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private async Task<MapAnalysis> MapCoreAsync(
        string url,
        int maxLinks,
        bool sameDomainOnly,
        int timeoutMs,
        string source,
        bool filterNonsense,
        string? focusQuery,
        IReadOnlyDictionary<string, string>? requestHeaders,
        CancellationToken cancellationToken)
    {
        if (ContentFormatDetector.IsPdfUrl(url))
        {
            return MapAnalysis.Failed(url, null, "unsupported_content_type", 0);
        }

        var normalizedSource = NormalizeSource(source);
        if (normalizedSource == "invalid_source")
        {
            return MapAnalysis.Failed(url, null, "invalid_arguments", 0);
        }

        maxLinks = Math.Clamp(maxLinks, 1, MaxLinksCap);
        timeoutMs = Math.Clamp(timeoutMs, MinTimeoutMs, MaxTimeoutMs);

        var latencyMs = 0;
        var statusCode = 0;
        string? contentType = null;
        string? finalUrl = null;
        var links = new List<MappedLink>();
        var filtered = 0;
        var partial = false;
        var expanded = false;

        // When focusing, over-fetch candidates so ranking has a real pool (not just first N in DOM order).
        var extractCap = string.IsNullOrWhiteSpace(focusQuery)
            ? Math.Max(maxLinks * 2, maxLinks)
            : Math.Min(200, Math.Max(maxLinks * 8, 64));

        if (normalizedSource == "homepage")
        {
            var fetch = await fetcher.FetchAsync(
                url,
                timeoutMs,
                maxBytes: 512 * 1024,
                requestHeaders: requestHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            latencyMs += fetch.LatencyMs;
            statusCode = fetch.StatusCode;
            contentType = fetch.ContentType;
            finalUrl = fetch.FinalUrl ?? url;

            if (fetch.FailureCode is not null && fetch.HtmlSample is null)
            {
                var code = fetch.FailureCode switch
                {
                    "timeout" => "timeout",
                    "unsupported_content_type" => "unsupported_content_type",
                    _ when fetch.FailureCode.StartsWith("http_", StringComparison.Ordinal) => fetch.FailureCode,
                    _ => "extraction_failed",
                };
                return MapAnalysis.Failed(url, finalUrl, code, latencyMs, statusCode);
            }

            links.AddRange(HtmlLinkExtractor.Extract(
                fetch.HtmlSample ?? string.Empty,
                finalUrl,
                extractCap,
                sameDomainOnly));
            links.AddRange(ExtractPrimaryEnrichment(
                fetch.HtmlSample ?? string.Empty,
                finalUrl,
                focusQuery,
                sameDomainOnly));
        }

        if (normalizedSource is "sitemap" or "robots")
        {
            var discoverCap = extractCap;
            if (filterNonsense)
            {
                discoverCap = Math.Max(discoverCap, 48);
            }

            var discovery = await SitemapDiscovery.DiscoverAsync(
                fetcher,
                finalUrl ?? url,
                discoverCap,
                sameDomainOnly,
                timeoutMs,
                robotsOnly: normalizedSource == "robots",
                requestHeaders: requestHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            latencyMs += discovery.LatencyMs;
            links.AddRange(discovery.Links);
            if (links.Count == 0)
            {
                var code = discovery.TimedOut ? "timeout" : "sitemap_not_found";
                return MapAnalysis.Failed(url, finalUrl, code, latencyMs, statusCode);
            }

            partial = discovery.TimedOut;
        }

        var deduped = DeduplicateLinks(links);
        if (filterNonsense)
        {
            var kept = new List<MappedLink>();
            foreach (var link in deduped)
            {
                if (MapLinkFilter.IsNonsense(link.Url, link.Path, link.Title))
                {
                    filtered++;
                    continue;
                }

                kept.Add(link);
            }

            deduped = kept;
        }

        if (!string.IsNullOrWhiteSpace(focusQuery))
        {
            var scored = MapLinkRanker.RankScored(deduped, focusQuery);

            // Second-level crawl: homepage/sitemap had no strong focus hits — expand hub pages.
            if (normalizedSource == "homepage"
                && !MapLinkRanker.HasStrongHit(scored)
                && latencyMs < timeoutMs)
            {
                var remainingMs = Math.Max(1_500, timeoutMs - latencyMs);
                var (extra, expandLatency) = await ExpandSecondLevelAsync(
                    scored,
                    focusQuery,
                    sameDomainOnly,
                    filterNonsense,
                    remainingMs,
                    requestHeaders,
                    cancellationToken).ConfigureAwait(false);
                latencyMs += expandLatency;
                if (extra.Count > 0)
                {
                    expanded = true;
                    deduped = DeduplicateLinks([.. deduped, .. extra]);
                    if (filterNonsense)
                    {
                        deduped = deduped
                            .Where(l => !MapLinkFilter.IsNonsense(l.Url, l.Path, l.Title))
                            .ToList();
                    }

                    scored = MapLinkRanker.RankScored(deduped, focusQuery);
                }
            }

            deduped = scored.Take(maxLinks).Select(s => s.Link).ToList();
        }
        else if (deduped.Count > maxLinks)
        {
            deduped = deduped.Take(maxLinks).ToList();
        }

        if (deduped.Count == 0)
        {
            var emptyCode = normalizedSource == "homepage" ? "thin_extract" : "sitemap_not_found";
            return MapAnalysis.Failed(url, finalUrl ?? url, emptyCode, latencyMs, statusCode);
        }

        return new MapAnalysis
        {
            Ok = true,
            Url = url,
            FinalUrl = finalUrl ?? url,
            Links = deduped,
            LinkCount = deduped.Count,
            LatencyMs = latencyMs,
            StatusCode = statusCode,
            ContentType = contentType,
            Source = normalizedSource,
            FilteredCount = filtered,
            FocusQuery = string.IsNullOrWhiteSpace(focusQuery) ? null : focusQuery.Trim(),
            Partial = partial,
            Expanded = expanded,
        };
    }

    /// <summary>
    /// Fetch a few hub pages (library/docs/index) when the seed page lacks strong focus matches,
    /// then merge their links into the candidate pool.
    /// </summary>
    private async Task<(List<MappedLink> Links, int LatencyMs)> ExpandSecondLevelAsync(
        IReadOnlyList<(MappedLink Link, double Score)> scored,
        string focusQuery,
        bool sameDomainOnly,
        bool filterNonsense,
        int budgetMs,
        IReadOnlyDictionary<string, string>? requestHeaders,
        CancellationToken cancellationToken)
    {
        var hubs = SelectExpansionHubs(scored.Select(s => s.Link).ToList(), focusQuery, MaxSecondLevelHubs);
        if (hubs.Count == 0)
        {
            return ([], 0);
        }

        var perHubTimeout = Math.Clamp(budgetMs / hubs.Count, 1_500, 8_000);
        var extra = new List<MappedLink>();
        var latency = 0;

        foreach (var hub in hubs)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fetch = await fetcher.FetchAsync(
                hub.Url,
                perHubTimeout,
                maxBytes: 512 * 1024,
                requestHeaders: requestHeaders,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            latency += fetch.LatencyMs;
            if (string.IsNullOrWhiteSpace(fetch.HtmlSample))
            {
                continue;
            }

            var extracted = HtmlLinkExtractor.Extract(
                fetch.HtmlSample,
                fetch.FinalUrl ?? hub.Url,
                maxLinks: 500,
                sameDomainOnly: sameDomainOnly);
            foreach (var link in extracted)
            {
                if (filterNonsense && MapLinkFilter.IsNonsense(link.Url, link.Path, link.Title))
                {
                    continue;
                }

                extra.Add(link);
            }

            foreach (var link in ExtractPrimaryEnrichment(
                         fetch.HtmlSample,
                         fetch.FinalUrl ?? hub.Url,
                         focusQuery,
                         sameDomainOnly))
            {
                if (filterNonsense && MapLinkFilter.IsNonsense(link.Url, link.Path, link.Title))
                {
                    continue;
                }

                extra.Add(link);
            }
        }

        return (extra, latency);
    }

    private static IReadOnlyList<MappedLink> ExtractPrimaryEnrichment(
        string html,
        string baseUrl,
        string? focusQuery,
        bool sameDomainOnly)
    {
        if (string.IsNullOrWhiteSpace(focusQuery) || string.IsNullOrWhiteSpace(html))
        {
            return [];
        }

        var decomp = FocusQueryDecomposition.Decompose(focusQuery);
        if (!decomp.HasPrimaryAnchors)
        {
            return [];
        }

        return HtmlLinkExtractor.ExtractPrimaryMatches(
            html,
            baseUrl,
            decomp.PrimaryAnchors,
            sameDomainOnly);
    }

    internal static IReadOnlyList<MappedLink> SelectExpansionHubs(
        IReadOnlyList<MappedLink> links,
        string focusQuery,
        int maxHubs)
    {
        var queryLower = focusQuery.Trim().ToLowerInvariant();
        return links
            .Where(l => LooksLikeHub(l) && !PathContainsQuery(l.Path, queryLower))
            .OrderByDescending(HubPriority)
            .ThenBy(l => l.Path, StringComparer.Ordinal)
            .Take(Math.Max(1, maxHubs))
            .ToList();
    }

    private static bool PathContainsQuery(string path, string queryLower) =>
        queryLower.Length >= 3
        && path.Contains(queryLower, StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeHub(MappedLink link)
    {
        var path = link.Path.ToLowerInvariant();
        if (path.EndsWith('/') || path.EndsWith("/index.html", StringComparison.Ordinal)
            || path.EndsWith("/index.htm", StringComparison.Ordinal))
        {
            return true;
        }

        // Reference roots without a leaf document (e.g. /3/library, /docs/api) — not /library/foo.html.
        var lastSegment = path.Split('/', StringSplitOptions.RemoveEmptyEntries).LastOrDefault() ?? string.Empty;
        var isLeafDoc = lastSegment.Contains('.', StringComparison.Ordinal)
                        && !lastSegment.StartsWith("index.", StringComparison.Ordinal);
        if (!isLeafDoc
            && (path.Contains("/library", StringComparison.Ordinal)
                || path.Contains("/reference", StringComparison.Ordinal)
                || path.Contains("/docs", StringComparison.Ordinal)
                || path.Contains("/api", StringComparison.Ordinal)
                || path.Contains("/guide", StringComparison.Ordinal)))
        {
            return true;
        }

        return link.Title is not null && (
            link.Title.Contains("library", StringComparison.OrdinalIgnoreCase)
            || link.Title.Contains("reference", StringComparison.OrdinalIgnoreCase)
            || link.Title.Contains("documentation", StringComparison.OrdinalIgnoreCase)
            || link.Title.Contains("standard library", StringComparison.OrdinalIgnoreCase)
            || link.Title.Contains("index", StringComparison.OrdinalIgnoreCase));
    }

    private static int HubPriority(MappedLink link)
    {
        var path = link.Path.ToLowerInvariant();
        var score = 0;
        if (path.Contains("/library", StringComparison.Ordinal))
        {
            score += 5;
        }

        if (path.Contains("/reference", StringComparison.Ordinal))
        {
            score += 4;
        }

        if (path.Contains("/docs", StringComparison.Ordinal))
        {
            score += 3;
        }

        if (path.EndsWith('/') || path.Contains("index", StringComparison.Ordinal))
        {
            score += 2;
        }

        return score;
    }

    internal static string NormalizeSource(string source) => source.Trim().ToLowerInvariant() switch
    {
        "sitemap" => "sitemap",
        "robots" => "robots",
        "homepage" or "" => "homepage",
        _ => "invalid_source",
    };

    private static List<MappedLink> DeduplicateLinks(IReadOnlyList<MappedLink> links)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<MappedLink>();
        foreach (var link in links)
        {
            if (!seen.Add(link.Url))
            {
                continue;
            }

            unique.Add(link);
        }

        return unique;
    }
}

public sealed class MapAnalysis
{
    public required bool Ok { get; init; }
    public required string Url { get; init; }
    public string? FinalUrl { get; init; }
    public IReadOnlyList<MappedLink> Links { get; init; } = [];
    public int LinkCount { get; init; }
    public int LatencyMs { get; init; }
    public int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public string Source { get; init; } = "homepage";
    public int FilteredCount { get; init; }
    public string? FocusQuery { get; init; }
    public string? FailureCode { get; init; }
    public int? FailureStatusCode { get; init; }
    public bool Partial { get; init; }

    /// <summary>True when a second-level hub crawl ran because the seed page lacked strong focus hits.</summary>
    public bool Expanded { get; init; }

    public static MapAnalysis Failed(
        string url,
        string? finalUrl,
        string failureCode,
        int latencyMs,
        int statusCode = 0) =>
        new()
        {
            Ok = false,
            Url = url,
            FinalUrl = finalUrl,
            FailureCode = failureCode,
            LatencyMs = latencyMs,
            FailureStatusCode = statusCode > 0 ? statusCode : null,
        };
}
