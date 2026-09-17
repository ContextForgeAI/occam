using OccamMcp.Core.Configuration;
using OccamMcp.Core.Search;

namespace OccamMcp.Core.Services;

/// <summary>
/// Open-web search adapter (the agent's discovery step: query → result URLs).
/// Default when <c>OCCAM_SEARCH_PROVIDER</c> is unset: keyless DuckDuckGo HTML
/// (<c>provider=duckduckgo</c> disclosed). Set <c>off</c>/<c>none</c> for the old
/// air-gap <c>search_unconfigured</c> contract. Explicit <c>searxng</c>/<c>brave</c>/
/// <c>tavily</c>/<c>external_cli</c> still require their URL/key/binary.
/// When <c>OCCAM_SEARCH_PROVIDERS</c> is set (CSV), fan-out polls all configured
/// healthy providers in parallel, merges by URL consensus, and skips degraded ones.
/// Core never crawls or indexes — it delegates and normalizes results.
/// </summary>
public interface ISearchService
{
    bool IsConfigured { get; }

    /// <summary>Active provider name (<c>fanout</c> in multi mode), or null when unconfigured.</summary>
    string? ProviderName { get; }

    /// <summary>Runs a search. Returns a typed failure (`search_unconfigured`, …) when not usable.</summary>
    Task<SearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken);
}

public sealed class SearchService(
    IHttpClientFactory httpClientFactory,
    IEnumerable<ISearchProvider> providers,
    SearchProviderHealth health) : ISearchService
{
    public const string HttpClientName = "occam.search";
    public const string DefaultProviderName = "duckduckgo";
    public const string FanoutProviderName = "fanout";

    private readonly IReadOnlyDictionary<string, ISearchProvider> _providers =
        providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    private readonly SearchProviderHealth _health = health;

    public bool IsConfigured => ResolveProviders() is { Count: > 0 };

    public string? ProviderName
    {
        get
        {
            var list = ResolveProviders();
            if (list.Count == 0)
            {
                return null;
            }

            return IsFanoutMode() ? FanoutProviderName : list[0].Name;
        }
    }

    public async Task<SearchOutcome> SearchAsync(string query, int maxResults, CancellationToken cancellationToken)
    {
        var selected = ResolveProviders();
        if (selected.Count == 0)
        {
            return SearchOutcome.Failure("none", "search_unconfigured", 0);
        }

        var baseUrl = Environment.GetEnvironmentVariable("OCCAM_SEARCH_URL");
        var apiKey = Environment.GetEnvironmentVariable("OCCAM_SEARCH_API_KEY");
        var client = httpClientFactory.CreateClient(HttpClientName);

        if (!IsFanoutMode())
        {
            return await CallOneAsync(
                selected[0], client, query, maxResults, baseUrl, apiKey, cancellationToken)
                .ConfigureAwait(false);
        }

        return await FanoutAsync(selected, client, query, maxResults, baseUrl, apiKey, cancellationToken)
            .ConfigureAwait(false);
    }

    private async Task<SearchOutcome> CallOneAsync(
        ISearchProvider provider,
        HttpClient client,
        string query,
        int maxResults,
        string? baseUrl,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        if (!_health.TryBeginCall(provider.Name))
        {
            return SearchOutcome.Failure(provider.Name, "search_rate_limited", 0);
        }

        var outcome = await provider.SearchAsync(
            client, query, maxResults, baseUrl, apiKey, cancellationToken).ConfigureAwait(false);
        _health.Observe(outcome);
        return outcome;
    }

    private async Task<SearchOutcome> FanoutAsync(
        IReadOnlyList<ISearchProvider> selected,
        HttpClient client,
        string query,
        int maxResults,
        string? baseUrl,
        string? apiKey,
        CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var perMs = OccamEnvironment.GetInt(
            "OCCAM_SEARCH_PROVIDER_TIMEOUT_MS", defaultValue: 3_000, min: 1_000, max: 30_000);
        var fanMs = OccamEnvironment.GetInt(
            "OCCAM_SEARCH_FANOUT_TIMEOUT_MS",
            defaultValue: Math.Clamp(perMs + 500, 1_000, 35_000),
            min: 1_000,
            max: 35_000);

        var runnable = new List<ISearchProvider>(selected.Count);
        foreach (var provider in selected)
        {
            if (_health.TryBeginCall(provider.Name))
            {
                runnable.Add(provider);
            }
        }

        if (runnable.Count == 0)
        {
            return SearchOutcome.Failure(FanoutProviderName, "search_rate_limited", SearchElapsed.Ms(started));
        }

        using var fanCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        fanCts.CancelAfter(fanMs);

        var tasks = new Task<SearchOutcome>[runnable.Count];
        for (var i = 0; i < runnable.Count; i++)
        {
            var provider = runnable[i];
            tasks[i] = InvokeWithTimeoutAsync(
                provider, client, query, maxResults, baseUrl, apiKey, perMs, fanCts.Token, cancellationToken);
        }

        var outcomes = await Task.WhenAll(tasks).ConfigureAwait(false);
        foreach (var outcome in outcomes)
        {
            _health.Observe(outcome);
        }

        var successes = outcomes.Where(o => o.Ok && o.Results.Count > 0).ToArray();
        if (successes.Length > 0)
        {
            // Merge in CSV priority order (runnable / selected order preserved in successes via outcomes order).
            var orderedSuccesses = runnable
                .Select(p => outcomes.First(o => string.Equals(o.Provider, p.Name, StringComparison.OrdinalIgnoreCase)))
                .Where(o => o.Ok && o.Results.Count > 0)
                .ToArray();
            var merged = SearchResultMerger.Merge(orderedSuccesses, maxResults);
            var used = orderedSuccesses
                .Select(o => o.Provider)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
            return SearchOutcome.Success(
                FanoutProviderName, merged, SearchElapsed.Ms(started), providersUsed: used);
        }

        return PickWorstFailure(outcomes, SearchElapsed.Ms(started));
    }

    private static async Task<SearchOutcome> InvokeWithTimeoutAsync(
        ISearchProvider provider,
        HttpClient client,
        string query,
        int maxResults,
        string? baseUrl,
        string? apiKey,
        int perProviderTimeoutMs,
        CancellationToken fanToken,
        CancellationToken callerToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        using var perCts = CancellationTokenSource.CreateLinkedTokenSource(fanToken);
        perCts.CancelAfter(perProviderTimeoutMs);
        try
        {
            return await provider.SearchAsync(
                client, query, maxResults, baseUrl, apiKey, perCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (callerToken.IsCancellationRequested)
        {
            throw;
        }
        catch (OperationCanceledException)
        {
            return SearchOutcome.Failure(provider.Name, "search_timeout", SearchElapsed.Ms(started));
        }
        catch (Exception ex)
        {
            return SearchOutcome.Failure(provider.Name, SearchElapsed.FailureFor(ex), SearchElapsed.Ms(started));
        }
    }

    private static SearchOutcome PickWorstFailure(IReadOnlyList<SearchOutcome> outcomes, int latencyMs)
    {
        // Prefer actionable codes when every arm failed.
        static int Rank(string? code) => code switch
        {
            "search_http_429" => 0,
            "search_http_202" => 1,
            "search_timeout" => 2,
            "search_rate_limited" => 3,
            _ when code is not null && code.StartsWith("search_http_", StringComparison.Ordinal) => 4,
            "search_error" => 5,
            _ => 6,
        };

        var best = outcomes
            .Where(o => !o.Ok)
            .OrderBy(o => Rank(o.FailureCode))
            .FirstOrDefault();

        return SearchOutcome.Failure(
            FanoutProviderName,
            best?.FailureCode ?? "search_error",
            latencyMs);
    }

    private bool IsFanoutMode()
    {
        var raw = Environment.GetEnvironmentVariable("OCCAM_SEARCH_PROVIDERS");
        return !string.IsNullOrWhiteSpace(raw);
    }

    /// <summary>
    /// Resolves the active provider list. <c>OCCAM_SEARCH_PROVIDERS</c> wins over singular
    /// <c>OCCAM_SEARCH_PROVIDER</c>. Entries missing required config are skipped.
    /// </summary>
    private IReadOnlyList<ISearchProvider> ResolveProviders()
    {
        var multi = Environment.GetEnvironmentVariable("OCCAM_SEARCH_PROVIDERS")?.Trim();
        if (!string.IsNullOrEmpty(multi))
        {
            var names = ParseCsv(multi);
            var list = new List<ISearchProvider>(names.Count);
            foreach (var name in names)
            {
                if (!_providers.TryGetValue(name, out var provider))
                {
                    Console.Error.WriteLine(
                        $"[occam.search] event=unknown_provider provider={name} reason=not_registered untilUtc=");
                    continue;
                }

                if (!HasRequiredConfig(provider))
                {
                    continue;
                }

                list.Add(provider);
            }

            return list;
        }

        var raw = Environment.GetEnvironmentVariable("OCCAM_SEARCH_PROVIDER")?.Trim();
        if (string.Equals(raw, "off", StringComparison.OrdinalIgnoreCase)
            || string.Equals(raw, "none", StringComparison.OrdinalIgnoreCase))
        {
            return [];
        }

        var singleName = string.IsNullOrEmpty(raw) ? DefaultProviderName : raw;
        if (!_providers.TryGetValue(singleName, out var single))
        {
            return [];
        }

        return HasRequiredConfig(single) ? [single] : [];
    }

    private static bool HasRequiredConfig(ISearchProvider provider)
    {
        if (provider.RequiresApiKey
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCCAM_SEARCH_API_KEY")))
        {
            return false;
        }

        if (provider.RequiresBaseUrl
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCCAM_SEARCH_URL")))
        {
            return false;
        }

        return true;
    }

    private static List<string> ParseCsv(string raw)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var list = new List<string>();
        foreach (var part in raw.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            if (seen.Add(part))
            {
                list.Add(part);
            }
        }

        return list;
    }
}
