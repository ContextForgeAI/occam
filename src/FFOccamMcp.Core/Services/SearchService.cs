using OccamMcp.Core.Search;

namespace OccamMcp.Core.Services;

/// <summary>
/// Open-web search adapter (the agent's discovery step: query → result URLs). Off unless
/// <c>OCCAM_SEARCH_PROVIDER</c> names a registered provider with its required config
/// (<c>OCCAM_SEARCH_URL</c> for SearXNG, <c>OCCAM_SEARCH_API_KEY</c> for Brave/Tavily). Core never
/// crawls or indexes — it delegates to the configured backend and normalizes results.
/// </summary>
public interface ISearchService
{
    bool IsConfigured { get; }

    /// <summary>Active provider name, or null when unconfigured.</summary>
    string? ProviderName { get; }

    /// <summary>Runs a search. Returns a typed failure (`search_unconfigured`, …) when not usable.</summary>
    Task<SearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken);
}

public sealed class SearchService(IHttpClientFactory httpClientFactory, IEnumerable<ISearchProvider> providers) : ISearchService
{
    public const string HttpClientName = "occam.search";

    private readonly IReadOnlyDictionary<string, ISearchProvider> _providers =
        providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);

    public bool IsConfigured => ResolveProvider() is not null;

    public string? ProviderName => ResolveProvider()?.Name;

    public Task<SearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        var provider = ResolveProvider();
        if (provider is null)
        {
            return Task.FromResult(SearchOutcome.Failure("none", "search_unconfigured", 0));
        }

        var baseUrl = Environment.GetEnvironmentVariable("OCCAM_SEARCH_URL");
        var apiKey = Environment.GetEnvironmentVariable("OCCAM_SEARCH_API_KEY");
        var client = httpClientFactory.CreateClient(HttpClientName);
        return provider.SearchAsync(client, query, maxResults, baseUrl, apiKey, cancellationToken);
    }

    /// <summary>Provider named by env, only if its required config is present; else null.</summary>
    private ISearchProvider? ResolveProvider()
    {
        var name = Environment.GetEnvironmentVariable("OCCAM_SEARCH_PROVIDER")?.Trim();
        if (string.IsNullOrEmpty(name) || !_providers.TryGetValue(name, out var provider))
        {
            return null;
        }

        if (provider.RequiresApiKey
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCCAM_SEARCH_API_KEY")))
        {
            return null;
        }

        if (provider.RequiresBaseUrl
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCCAM_SEARCH_URL")))
        {
            return null;
        }

        return provider;
    }
}
