using OccamMcp.Core.Session;

namespace OccamMcp.Core.Search;

/// <summary>
/// Keyless open-web discovery via DuckDuckGo's HTML SERP
/// (<c>html.duckduckgo.com</c>, lite fallback). Not an Occam index —
/// <c>provider</c> in the outcome is always <c>duckduckgo</c>.
/// Prefer GET: POST often receives a soft-block 202 anomaly page from
/// datacenter/HttpClient egress, while GET returns parseable results.
/// </summary>
public sealed class DuckDuckGoSearchProvider : ISearchProvider
{
    public const string HtmlEndpoint = "https://html.duckduckgo.com/html/";
    public const string LiteEndpoint = "https://lite.duckduckgo.com/lite/";

    public string Name => "duckduckgo";
    public bool RequiresApiKey => false;
    public bool RequiresBaseUrl => false;

    public async Task<SearchOutcome> SearchAsync(
        HttpClient client,
        string query,
        int maxResults,
        string? baseUrl,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var first = await FetchParseAsync(client, HtmlEndpoint, query, maxResults, cancellationToken, started)
            .ConfigureAwait(false);
        if (first.Ok && first.Results.Count > 0)
        {
            return first;
        }

        if (first.FailureCode is { } code
            && code.StartsWith("search_http_", StringComparison.Ordinal)
            && code is not ("search_http_403" or "search_http_429" or "search_http_202"))
        {
            return first;
        }

        var lite = await FetchParseAsync(client, LiteEndpoint, query, maxResults, cancellationToken, started)
            .ConfigureAwait(false);
        if (lite.Ok && lite.Results.Count > 0)
        {
            return lite;
        }

        if (!first.Ok)
        {
            return first;
        }

        if (!lite.Ok)
        {
            return lite;
        }

        return SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
    }

    private async Task<SearchOutcome> FetchParseAsync(
        HttpClient client,
        string endpoint,
        string query,
        int maxResults,
        CancellationToken cancellationToken,
        long started)
    {
        try
        {
            var url = $"{endpoint}?q={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Version = System.Net.HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            request.Headers.TryAddWithoutValidation("User-Agent", OccamFetchDefaults.UserAgent);
            request.Headers.TryAddWithoutValidation("Referer", endpoint);

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return SearchOutcome.Failure(Name, $"search_http_{(int)response.StatusCode}", SearchElapsed.Ms(started));
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (DuckDuckGoHtmlParser.LooksLikeAnomalyChallenge(html))
            {
                // Soft-block CAPTCHA interstitial — never solve; surface as HTTP 202-class block.
                return SearchOutcome.Failure(Name, "search_http_202", SearchElapsed.Ms(started));
            }

            var items = DuckDuckGoHtmlParser.Parse(html, maxResults);
            if (items.Count > 0)
            {
                return SearchOutcome.Success(Name, items, SearchElapsed.Ms(started));
            }

            if (!DuckDuckGoHtmlParser.LooksLikeResultsPage(html))
            {
                return SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
            }

            return SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return SearchOutcome.Failure(Name, SearchElapsed.FailureFor(ex), SearchElapsed.Ms(started));
        }
    }
}
