using System.Text.RegularExpressions;
using OccamMcp.Core.Text;

namespace OccamMcp.Core.Probe;

public static partial class HtmlLinkExtractor
{
    public static IReadOnlyList<MappedLink> Extract(
        string html,
        string baseUrl,
        int maxLinks = 50,
        bool sameDomainOnly = true)
    {
        if (string.IsNullOrWhiteSpace(html) || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = new List<MappedLink>();
        var enumerator = HtmlStreamScanner.EnumerateAnchors(html.AsSpan());
        var searchFrom = 0;

        while (enumerator.MoveNext())
        {
            if (links.Count >= maxLinks)
            {
                break;
            }

            var href = enumerator.Href.Trim();
            if (!TryNormalizeLink(href, baseUri, out var absolute, out var path))
            {
                continue;
            }

            if (sameDomainOnly && !IsSameHost(baseUri, absolute))
            {
                continue;
            }

            if (!seen.Add(absolute))
            {
                continue;
            }

            var title = MapLinkTitleSanitizer.Sanitize(StripTags(enumerator.InnerText).Trim());
            var context = ExtractNeighborContext(html, href.ToString(), ref searchFrom);
            links.Add(new MappedLink(absolute, title, path, Description: null, Context: context));
        }

        return links;
    }

    /// <summary>
    /// Scan the full HTML for anchors whose path or title hits a primary focus anchor.
    /// Unbounded by DOM order (unlike <see cref="Extract"/>) so rare entity pages are not
    /// dropped when they appear after the sequential extract cap.
    /// </summary>
    public static IReadOnlyList<MappedLink> ExtractPrimaryMatches(
        string html,
        string baseUrl,
        IReadOnlyList<string> primaryAnchors,
        bool sameDomainOnly = true,
        int maxMatches = 48)
    {
        if (string.IsNullOrWhiteSpace(html)
            || primaryAnchors.Count == 0
            || !Uri.TryCreate(baseUrl, UriKind.Absolute, out var baseUri))
        {
            return [];
        }

        var anchors = primaryAnchors
            .Where(a => a.Length >= 3)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
        if (anchors.Count == 0)
        {
            return [];
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var links = new List<MappedLink>();
        var enumerator = HtmlStreamScanner.EnumerateAnchors(html.AsSpan());
        var searchFrom = 0;

        while (enumerator.MoveNext())
        {
            if (links.Count >= maxMatches)
            {
                break;
            }

            var href = enumerator.Href.Trim();
            if (!TryNormalizeLink(href, baseUri, out var absolute, out var path))
            {
                continue;
            }

            if (sameDomainOnly && !IsSameHost(baseUri, absolute))
            {
                continue;
            }

            var title = MapLinkTitleSanitizer.Sanitize(StripTags(enumerator.InnerText).Trim());
            var pathLower = path.ToLowerInvariant();
            var titleLower = (title ?? string.Empty).ToLowerInvariant();
            if (!anchors.Any(a =>
                    PathHitsPrimary(pathLower, a) || titleLower.Contains(a, StringComparison.Ordinal)))
            {
                continue;
            }

            if (!seen.Add(absolute))
            {
                continue;
            }

            var context = ExtractNeighborContext(html, href.ToString(), ref searchFrom);
            links.Add(new MappedLink(absolute, title, path, Description: null, Context: context));
        }

        return links;
    }

    private static bool PathHitsPrimary(string pathLower, string anchor)
    {
        if (pathLower.Contains(anchor, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var segment in pathLower.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment.Equals(anchor, StringComparison.Ordinal)
                || segment.Equals(anchor + ".html", StringComparison.Ordinal)
                || segment.Equals(anchor + ".htm", StringComparison.Ordinal)
                || segment.StartsWith(anchor + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Plain-text window around the next occurrence of <paramref name="hrefRaw"/> in the HTML
    /// (anchor neighbors: preceding/following words the ranker can use as soft evidence).
    /// </summary>
    private static string? ExtractNeighborContext(string html, string hrefRaw, ref int searchFrom)
    {
        if (string.IsNullOrEmpty(hrefRaw) || searchFrom >= html.Length)
        {
            return null;
        }

        var idx = html.IndexOf(hrefRaw, searchFrom, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            // Absolute/normalized href may differ from raw attribute — best-effort skip.
            return null;
        }

        searchFrom = idx + hrefRaw.Length;
        const int radius = 100;
        var start = Math.Max(0, idx - radius);
        var end = Math.Min(html.Length, idx + hrefRaw.Length + radius);
        var window = StripTags(html.AsSpan(start, end - start)).Trim();
        if (window.Length < 8)
        {
            return null;
        }

        if (window.Length > 240)
        {
            window = window[..240].TrimEnd();
        }

        return MapLinkTitleSanitizer.Sanitize(window);
    }

    private static bool TryNormalizeLink(ReadOnlySpan<char> href, Uri baseUri, out string absolute, out string path)
    {
        absolute = string.Empty;
        path = string.Empty;
        if (href.IsEmpty || href.Trim().IsEmpty)
        {
            return false;
        }

        if (href[0] == '#'
            || href.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("mailto:", StringComparison.OrdinalIgnoreCase)
            || href.StartsWith("tel:", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (!Uri.TryCreate(baseUri, href.ToString(), out var uri) || uri.Scheme is not "http" and not "https")
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

    private static bool IsSameHost(Uri baseUri, string absoluteUrl) =>
        Uri.TryCreate(absoluteUrl, UriKind.Absolute, out var uri)
        && uri.Host.Equals(baseUri.Host, StringComparison.OrdinalIgnoreCase);

    private static string StripTags(ReadOnlySpan<char> value) =>
        TagRegex().Replace(value.ToString(), " ").Replace('\n', ' ').Replace('\r', ' ');

    [GeneratedRegex("<[^>]+>")]
    private static partial Regex TagRegex();
}

internal static class MapLinkTitleSanitizer
{
    public static string? Sanitize(string? title)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            return null;
        }

        var t = title.Trim();
        if (t.Length < 2 || LooksLikeSvgCssGarbage(t))
        {
            return null;
        }

        if (t.Length > 120)
        {
            t = t[..120].TrimEnd();
        }

        return string.IsNullOrWhiteSpace(t) ? null : t;
    }

    private static bool LooksLikeSvgCssGarbage(string title) =>
        title.Contains("xmlns", StringComparison.OrdinalIgnoreCase)
        || title.Contains("viewBox", StringComparison.OrdinalIgnoreCase)
        || title.Contains("{", StringComparison.Ordinal)
        || title.Contains("display:", StringComparison.OrdinalIgnoreCase);
}
