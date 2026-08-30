using System.Net;
using System.Text.RegularExpressions;

namespace OccamMcp.Core.Search;

/// <summary>
/// Pure HTML → hits parser for DuckDuckGo's keyless HTML/lite SERP pages.
/// No network. Unit-tested on fixtures so CI stays deterministic.
/// </summary>
public static partial class DuckDuckGoHtmlParser
{
    // result__a (html.duckduckgo.com) and result-link (lite.duckduckgo.com)
    [GeneratedRegex(
        """<a\b[^>]*\bclass\s*=\s*["'][^"']*\b(?:result__a|result-link)\b[^"']*["'][^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*>(.*?)</a>|<a\b[^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*\bclass\s*=\s*["'][^"']*\b(?:result__a|result-link)\b[^"']*["'][^>]*>(.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex ResultAnchorRegex();

    [GeneratedRegex(
        """<[^>]*\bclass\s*=\s*["'][^"']*\b(?:result__snippet|result-snippet)\b[^"']*["'][^>]*>(.*?)</(?:a|td|div|span)>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex SnippetRegex();

    /// <summary>
    /// Extract up to <paramref name="maxResults"/> hits. Returns empty when the markup has no result anchors.
    /// </summary>
    public static IReadOnlyList<SearchResultItem> Parse(string html, int maxResults)
    {
        if (string.IsNullOrEmpty(html) || maxResults < 1)
        {
            return [];
        }

        var snippets = new Queue<string?>();
        foreach (Match snip in SnippetRegex().Matches(html))
        {
            snippets.Enqueue(SearchElapsed.Trim(StripTags(WebUtility.HtmlDecode(snip.Groups[1].Value))));
        }

        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var items = new List<SearchResultItem>(Math.Min(maxResults, 16));
        foreach (Match match in ResultAnchorRegex().Matches(html))
        {
            var href = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
            var titleHtml = match.Groups[1].Success ? match.Groups[2].Value : match.Groups[4].Value;
            var url = ResolveResultUrl(href);
            if (url is null || !seen.Add(url))
            {
                continue;
            }

            var title = SearchElapsed.Trim(StripTags(WebUtility.HtmlDecode(titleHtml))) ?? "";
            string? snippet = null;
            if (snippets.Count > 0)
            {
                snippet = snippets.Dequeue();
            }

            items.Add(new SearchResultItem(title, url, snippet));
            if (items.Count >= maxResults)
            {
                break;
            }
        }

        return items;
    }

    /// <summary>True when the body looks like a DDG results document (vs captcha/block shell).</summary>
    public static bool LooksLikeResultsPage(string html) =>
        !string.IsNullOrEmpty(html)
        && (html.Contains("result__a", StringComparison.OrdinalIgnoreCase)
            || html.Contains("result-link", StringComparison.OrdinalIgnoreCase)
            || html.Contains("no-results", StringComparison.OrdinalIgnoreCase)
            || html.Contains("result__body", StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// DuckDuckGo soft-block / anomaly CAPTCHA interstitial (do not solve — typed failure only).
    /// </summary>
    public static bool LooksLikeAnomalyChallenge(string html) =>
        !string.IsNullOrEmpty(html)
        && (html.Contains("anomaly-modal", StringComparison.OrdinalIgnoreCase)
            || html.Contains("anomaly.js", StringComparison.OrdinalIgnoreCase)
            || html.Contains("Unfortunately, bots use DuckDuckGo too", StringComparison.OrdinalIgnoreCase));

    internal static string? ResolveResultUrl(string? href)
    {
        if (string.IsNullOrWhiteSpace(href))
        {
            return null;
        }

        var raw = WebUtility.HtmlDecode(href.Trim());
        if (raw.StartsWith("//", StringComparison.Ordinal))
        {
            raw = "https:" + raw;
        }
        else if (raw.StartsWith("/l/?", StringComparison.OrdinalIgnoreCase))
        {
            raw = "https://duckduckgo.com" + raw;
        }

        if (TryExtractUddg(raw, out var uddg))
        {
            raw = uddg!;
        }

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        // Drop DDG chrome / ad redirects that never carry uddg.
        if (uri.Host.Contains("duckduckgo.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (uri.AbsolutePath.Contains("y.js", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.AbsoluteUri;
    }

    private static bool TryExtractUddg(string url, out string? decoded)
    {
        decoded = null;
        const string marker = "uddg=";
        var idx = url.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
        if (idx < 0)
        {
            return false;
        }

        var start = idx + marker.Length;
        var end = url.IndexOf('&', start);
        var encoded = end < 0 ? url[start..] : url[start..end];
        try
        {
            decoded = Uri.UnescapeDataString(encoded.Replace("+", "%20", StringComparison.Ordinal));
            return !string.IsNullOrWhiteSpace(decoded);
        }
        catch (UriFormatException)
        {
            return false;
        }
    }

    private static string StripTags(string value) =>
        TagStripRegex().Replace(value, " ").Replace("&nbsp;", " ", StringComparison.OrdinalIgnoreCase);

    [GeneratedRegex(@"<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex TagStripRegex();
}
