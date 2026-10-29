using System.Net;
using System.Text.RegularExpressions;

namespace OccamMcp.Core.Search;

/// <summary>
/// Pure HTML → hits parser for Bing keyless SERP. No network.
/// Matches classic <c>li.b_algo h2 a</c> title anchors.
/// </summary>
public static partial class BingHtmlParser
{
    [GeneratedRegex(
        """<li\b[^>]*\bclass\s*=\s*["'][^"']*\bb_algo\b[^"']*["'][^>]*>.*?<h2[^>]*>\s*<a\b[^>]*\bhref\s*=\s*["']([^"']+)["'][^>]*>(.*?)</a>""",
        RegexOptions.IgnoreCase | RegexOptions.Singleline | RegexOptions.CultureInvariant)]
    private static partial Regex AlgoTitleRegex();

    [GeneratedRegex(
        """<[^>]*\bclass\s*=\s*["'][^"']*\bb_caption\b[^"']*["'][^>]*>.*?<p[^>]*>(.*?)</p>""",
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
        foreach (Match match in AlgoTitleRegex().Matches(html))
        {
            var url = NormalizeUrl(match.Groups[1].Value);
            if (url is null || !seen.Add(url))
            {
                continue;
            }

            var title = SearchElapsed.Trim(StripTags(WebUtility.HtmlDecode(match.Groups[2].Value))) ?? url;
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

        if (uri.Host.Contains("bing.com", StringComparison.OrdinalIgnoreCase)
            || uri.Host.Contains("microsoft.com", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return uri.GetLeftPart(UriPartial.Query);
    }

    private static string StripTags(string html) => TagRegex().Replace(html, string.Empty);

    [GeneratedRegex("<[^>]+>", RegexOptions.CultureInvariant)]
    private static partial Regex TagRegex();
}
