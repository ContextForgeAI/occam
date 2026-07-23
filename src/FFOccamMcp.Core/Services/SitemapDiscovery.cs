using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Xml;
using OccamMcp.Core.Probe;

namespace OccamMcp.Core.Services;

internal sealed record SitemapDiscoveryResult(
    IReadOnlyList<MappedLink> Links,
    bool TimedOut,
    int LatencyMs);

/// <summary>robots.txt + sitemap.xml discovery (cherry-pick from FFWebMCP P10-B1).</summary>
public static partial class SitemapDiscovery
{
    private const int MaxSitemapFetches = 4;
    private const int MaxSitemapBytes = 2 * 1024 * 1024;

    public static IReadOnlyList<string> ParseRobotsSitemapUrls(string robotsBody, Uri baseUri)
    {
        if (string.IsNullOrWhiteSpace(robotsBody))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var urls = new List<string>();
        foreach (var line in robotsBody.Split('\n'))
        {
            var trimmed = line.Trim();
            if (!trimmed.StartsWith("Sitemap:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var value = trimmed["Sitemap:".Length..].Trim();
            if (TryNormalizeSitemapUrl(value, baseUri, out var absolute) && seen.Add(absolute))
            {
                urls.Add(absolute);
            }
        }

        return urls;
    }

    public static IReadOnlyList<MappedLink> ParseSitemapXml(
        string xml,
        Uri baseUri,
        int maxLinks,
        bool sameDomainOnly)
    {
        if (string.IsNullOrWhiteSpace(xml) || maxLinks <= 0)
        {
            return [];
        }

        var links = new List<MappedLink>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings
            {
                DtdProcessing = DtdProcessing.Ignore,
                XmlResolver = null,
            });

            while (reader.Read())
            {
                if (reader.NodeType != XmlNodeType.Element
                    || !string.Equals(reader.LocalName, "loc", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var loc = reader.ReadElementContentAsString().Trim();
                if (!TryNormalizePageUrl(loc, baseUri, sameDomainOnly, out var absolute, out var path))
                {
                    continue;
                }

                if (!seen.Add(absolute))
                {
                    continue;
                }

                links.Add(new MappedLink(absolute, null, path));
                if (links.Count >= maxLinks)
                {
                    break;
                }
            }
        }
        catch
        {
            foreach (Match match in SitemapLocRegex().Matches(xml))
            {
                var loc = match.Groups["url"].Value.Trim();
                if (!TryNormalizePageUrl(loc, baseUri, sameDomainOnly, out var absolute, out var path))
                {
                    continue;
                }

                if (!seen.Add(absolute))
                {
                    continue;
                }

                links.Add(new MappedLink(absolute, null, path));
                if (links.Count >= maxLinks)
                {
                    break;
                }
            }
        }

        return links;
    }

    public static bool IsSitemapIndex(string xml) =>
        !string.IsNullOrWhiteSpace(xml)
        && xml.Contains("<sitemapindex", StringComparison.OrdinalIgnoreCase);

    internal static async Task<SitemapDiscoveryResult> DiscoverAsync(
        HttpProbeFetcher fetcher,
        string url,
        int maxLinks,
        bool sameDomainOnly,
        int timeoutMs,
        bool robotsOnly,
        IReadOnlyDictionary<string, string>? requestHeaders = null,
        CancellationToken cancellationToken = default)
    {
        var started = Stopwatch.GetTimestamp();
        if (!Uri.TryCreate(url, UriKind.Absolute, out var seedUri))
        {
            return new SitemapDiscoveryResult([], false, ElapsedMs(started));
        }

        // Treat timeoutMs as a TOTAL discovery budget, not a per-fetch one. robots + up to
        // MaxSitemapFetches sitemaps ran sequentially at the full timeout each, so a slow host could
        // stall map for ~5x the caller's timeout. A shared deadline bounds the whole walk to ~timeoutMs
        // while still letting early sitemaps use what budget remains.
        using var deadlineCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        deadlineCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Max(1, timeoutMs)));
        int RemainingMs() => Math.Max(0, timeoutMs - ElapsedMs(started));

        var links = new List<MappedLink>();
        try
        {
            var remaining = RemainingMs();
            if (remaining == 0)
            {
                return new SitemapDiscoveryResult(links, true, ElapsedMs(started));
            }

            var robotsUrl = new Uri(seedUri, "/robots.txt").ToString();
            var robotsFetch = await fetcher.FetchAsync(
                robotsUrl,
                remaining,
                maxBytes: 128 * 1024,
                requestHeaders: requestHeaders,
                cancellationToken: deadlineCts.Token).ConfigureAwait(false);
            if (robotsFetch.FailureCode == "timeout")
            {
                return new SitemapDiscoveryResult(links, true, ElapsedMs(started));
            }

            var sitemapUrls = ParseRobotsSitemapUrls(robotsFetch.HtmlSample ?? string.Empty, seedUri).ToList();
            if (!robotsOnly && sitemapUrls.Count == 0)
            {
                sitemapUrls.Add(new Uri(seedUri, "/sitemap.xml").ToString());
            }

            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var fetched = 0;
            var queue = new Queue<string>(sitemapUrls);

            while (queue.Count > 0 && fetched < MaxSitemapFetches && links.Count < maxLinks)
            {
                remaining = RemainingMs();
                if (remaining == 0)
                {
                    return new SitemapDiscoveryResult(links, true, ElapsedMs(started));
                }

                var sitemapUrl = queue.Dequeue();
                fetched++;
                var fetch = await fetcher.FetchAsync(
                    sitemapUrl,
                    remaining,
                    maxBytes: MaxSitemapBytes,
                    requestHeaders: requestHeaders,
                    cancellationToken: deadlineCts.Token).ConfigureAwait(false);
                if (fetch.FailureCode == "timeout")
                {
                    return new SitemapDiscoveryResult(links, true, ElapsedMs(started));
                }

                var body = fetch.HtmlSample ?? string.Empty;
                if (string.IsNullOrWhiteSpace(body))
                {
                    continue;
                }

                if (IsSitemapIndex(body))
                {
                    foreach (Match match in SitemapLocRegex().Matches(body))
                    {
                        var nested = match.Groups["url"].Value.Trim();
                        if (TryNormalizeSitemapUrl(nested, seedUri, out var absolute))
                        {
                            queue.Enqueue(absolute);
                        }
                    }

                    continue;
                }

                foreach (var link in ParseSitemapXml(body, seedUri, maxLinks - links.Count, sameDomainOnly))
                {
                    if (seen.Add(link.Url))
                    {
                        links.Add(link);
                    }

                    if (links.Count >= maxLinks)
                    {
                        break;
                    }
                }
            }

            return new SitemapDiscoveryResult(links, false, ElapsedMs(started));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new SitemapDiscoveryResult(links, true, ElapsedMs(started));
        }
    }

    private static int ElapsedMs(long started) =>
        (int)Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    private static bool TryNormalizeSitemapUrl(string raw, Uri baseUri, out string absolute)
    {
        absolute = string.Empty;
        if (!Uri.TryCreate(baseUri, raw.Trim(), out var uri) || uri.Scheme is not "http" and not "https")
        {
            return false;
        }

        absolute = uri.ToString();
        return true;
    }

    private static bool TryNormalizePageUrl(
        string raw,
        Uri baseUri,
        bool sameDomainOnly,
        out string absolute,
        out string path)
    {
        absolute = string.Empty;
        path = string.Empty;
        if (!Uri.TryCreate(baseUri, raw.Trim(), out var uri) || uri.Scheme is not "http" and not "https")
        {
            return false;
        }

        if (sameDomainOnly && !uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        absolute = uri.GetLeftPart(UriPartial.Path);
        if (!string.IsNullOrEmpty(uri.Query))
        {
            absolute += uri.Query;
        }

        path = uri.AbsolutePath;
        return true;
    }

    [GeneratedRegex(@"<loc>\s*(?<url>[^<\s]+)\s*</loc>", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SitemapLocRegex();
}
