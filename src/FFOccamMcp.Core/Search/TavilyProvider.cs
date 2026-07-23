using System.Net.Http.Json;
using System.Text.Json;

namespace OccamMcp.Core.Search;

/// <summary>
/// Tavily — AI-agent search API. POST /search with Bearer key and { query, max_results }.
/// Requires <c>OCCAM_SEARCH_API_KEY</c>. Default base: https://api.tavily.com.
/// </summary>
public sealed class TavilyProvider : ISearchProvider
{
    public string Name => "tavily";
    public bool RequiresApiKey => true;
    public bool RequiresBaseUrl => false;

    public async Task<SearchOutcome> SearchAsync(HttpClient client, string query, int maxResults, string? baseUrl, string? apiKey, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.tavily.com" : baseUrl.TrimEnd('/');
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{root}/search")
            {
                Content = JsonContent.Create(
                    new TavilyRequest { Query = query, MaxResults = maxResults },
                    SearchJsonContext.Default.TavilyRequest),
            };
            request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");

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
                SearchJsonContext.Default.TavilyResponse,
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
