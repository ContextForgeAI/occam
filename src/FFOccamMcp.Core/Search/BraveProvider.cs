using System.Text.Json;

namespace OccamMcp.Core.Search;

/// <summary>
/// Brave Search API. GET /res/v1/web/search?q=… with header X-Subscription-Token.
/// Requires <c>OCCAM_SEARCH_API_KEY</c>. Default base: https://api.search.brave.com.
/// </summary>
public sealed class BraveProvider : ISearchProvider
{
    public string Name => "brave";
    public bool RequiresApiKey => true;
    public bool RequiresBaseUrl => false;

    public async Task<SearchOutcome> SearchAsync(HttpClient client, string query, int maxResults, string? baseUrl, string? apiKey, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.search.brave.com" : baseUrl.TrimEnd('/');
        try
        {
            var url = $"{root}/res/v1/web/search?q={Uri.EscapeDataString(query)}&count={maxResults}";
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("Accept", "application/json");
            request.Headers.TryAddWithoutValidation("X-Subscription-Token", apiKey);

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
                SearchJsonContext.Default.BraveResponse,
                cancellationToken).ConfigureAwait(false);
            var items = (payload?.Web?.Results ?? [])
                .Where(r => !string.IsNullOrWhiteSpace(r.Url))
                .Take(maxResults)
                .Select(r => new SearchResultItem(SearchElapsed.Trim(r.Title) ?? "", r.Url!, SearchElapsed.Trim(r.Description)))
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
