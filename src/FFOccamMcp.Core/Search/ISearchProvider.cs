namespace OccamMcp.Core.Search;

/// <summary>
/// A web-search provider (SearXNG, Brave, Tavily). The search adapter delegates the open-web
/// discovery step (query → result URLs) to a configured backend; Core never crawls or indexes.
/// Providers normalize their wire shape to <see cref="SearchOutcome"/> and must not throw.
/// </summary>
public interface ISearchProvider
{
    /// <summary>Stable id matched against <c>OCCAM_SEARCH_PROVIDER</c> (e.g. "searxng", "brave", "tavily").</summary>
    string Name { get; }

    /// <summary>True when this provider cannot run without <c>OCCAM_SEARCH_API_KEY</c>.</summary>
    bool RequiresApiKey { get; }

    /// <summary>True when this provider needs an explicit <c>OCCAM_SEARCH_URL</c> (no public default).</summary>
    bool RequiresBaseUrl { get; }

    Task<SearchOutcome> SearchAsync(
        HttpClient client,
        string query,
        int maxResults,
        string? baseUrl,
        string? apiKey,
        CancellationToken cancellationToken);
}
