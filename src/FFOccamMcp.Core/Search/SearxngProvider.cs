using System.Text.Json;

namespace OccamMcp.Core.Search;

/// <summary>
/// SearXNG — self-hosted meta-search. Keyless; requires <c>OCCAM_SEARCH_URL</c> (your instance).
/// GET {base}/search?q=&lt;query&gt;&amp;format=json. The instance must allow the JSON format.
/// </summary>
public sealed class SearxngProvider : ISearchProvider
{
    public string Name => "searxng";
    public bool RequiresApiKey => false;
    public bool RequiresBaseUrl => true;

    public async Task<SearchOutcome> SearchAsync(HttpClient client, string query, int maxResults, string? baseUrl, string? apiKey, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var root = (baseUrl ?? "").TrimEnd('/');
        try
        {
            var url = $"{root}/search?q={Uri.EscapeDataString(query)}&format=json";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            }

            using var response = await client.SendAsync(
                request,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return SearchOutcome.Failure(Name, $"search_http_{(int)response.StatusCode}", SearchElapsed.Ms(started));
            }

            await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            var payload = await JsonSerializer.DeserializeAsync(
                stream,
                SearchJsonContext.Default.SearxngResponse,
                cancellationToken).ConfigureAwait(false);
            var items = (payload?.Results ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r.Url))
                .Take(maxResults)
                .Select(r => new SearchResultItem(SearchElapsed.Trim(r.Title) ?? "", r.Url!, SearchElapsed.Trim(r.Content)))
                .ToArray();
            return SearchOutcome.Success(Name, items, SearchElapsed.Ms(started));
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
