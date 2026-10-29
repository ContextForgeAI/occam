using OccamMcp.Core.Session;

namespace OccamMcp.Core.Search;

/// <summary>
/// Keyless open-web discovery via Bing HTML SERP. Arm for
/// <c>OCCAM_SEARCH_PROVIDERS=keyless</c> fan-out. Individual arms may soft-fail;
/// fan-out merges survivors.
/// </summary>
public sealed class BingSearchProvider : ISearchProvider
{
    public const string Endpoint = "https://www.bing.com/search";

    public string Name => "bing";
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
        _ = baseUrl;
        _ = apiKey;
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        try
        {
            var url = $"{Endpoint}?q={Uri.EscapeDataString(query)}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Version = System.Net.HttpVersion.Version11;
            request.VersionPolicy = HttpVersionPolicy.RequestVersionOrLower;
            request.Headers.TryAddWithoutValidation("Accept", "text/html,application/xhtml+xml;q=0.9,*/*;q=0.8");
            request.Headers.TryAddWithoutValidation("Accept-Language", "en-US,en;q=0.9");
            request.Headers.TryAddWithoutValidation("User-Agent", OccamFetchDefaults.UserAgent);

            using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
            var status = (int)response.StatusCode;
            if (status is 429 or 202 or 403)
            {
                return SearchOutcome.Failure(
                    Name,
                    status == 429 ? "search_rate_limited" : $"search_http_{status}",
                    SearchElapsed.Ms(started));
            }

            if (!response.IsSuccessStatusCode)
            {
                return SearchOutcome.Failure(Name, $"search_http_{status}", SearchElapsed.Ms(started));
            }

            var html = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var hits = BingHtmlParser.Parse(html, maxResults);
            if (hits.Count == 0)
            {
                return SearchOutcome.Failure(Name, "search_error", SearchElapsed.Ms(started));
            }

            return SearchOutcome.Success(Name, hits, SearchElapsed.Ms(started));
        }
        catch (Exception ex)
        {
            return SearchOutcome.Failure(Name, SearchElapsed.FailureFor(ex), SearchElapsed.Ms(started));
        }
    }
}
