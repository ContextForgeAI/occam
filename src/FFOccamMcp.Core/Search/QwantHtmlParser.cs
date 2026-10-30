using System.Net;
using System.Text.RegularExpressions;

namespace OccamMcp.Core.Search;

/// <summary>
/// Pure HTML → hits parser for Qwant keyless SERP. No network.
/// Matches <c>result-title</c> / <c>web-result</c> title anchors used in HTML results.
/// </summary>
public static partial class QwantHtmlParser
{
    [GeneratedRegex(
        """<a\b[^>]*\bclass\s*=\s*["'][^"']*\b(?:result-title|web-result__title)\b[^"']*["'][^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*>(.*?)</a>|<a\b[^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*\bclass\s*=\s*["'][^"']*\b(?:result-title|web-result__title)\b[^"']*["'][^>]*>(.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex TitleAnchorRegex();

    [GeneratedRegex(
        """<[^>]*\bclass\s*=\s*["'][^"']*\b(?:result-desc|web-result__desc)\b[^"']*["'][^>]*>(.*?)</(?:p|div|span)>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex SnippetRegex();

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
        foreach (Match match in TitleAnchorRegex().Matches(html))
        {
            var href = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[3].Value;
            var titleHtml = match.Groups[1].Success ? match.Groups[2].Value : match.Groups[4].Value;
            var url = NormalizeUrl(href);
            if (url is null || !seen.Add(url))
            {
                continue;
            }

            var title = SearchElapsed.Trim(StripTags(WebUtility.HtmlDecode(titleHtml))) ?? url;
            string? snippet = snippets.Count > 0 ? snippets.Dequeue() : null;
            items.Add(new SearchResultItem(title, url, snippet));
            if (items.Count >= maxResults)
            {
                break;
            }
        }

        return items;
    }

    private static string? NormalizeUrl(string href)
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

        if (!Uri.TryCreate(raw, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        if (uri.Host.Contains("qwant.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Query);
    }

    private static string StripTags(string html) => TagRegex().Replace(html, string.Empty);

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();
}
