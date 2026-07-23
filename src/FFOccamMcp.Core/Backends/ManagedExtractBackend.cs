using OccamMcp.Core.Backends.Managed;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends;

/// <summary>
/// Package 3 managed escalation backend. Disabled unless <c>OCCAM_MANAGED_PROVIDER</c> names a
/// registered provider (and its API key is present when required). Per-domain opt-in via
/// <c>OCCAM_MANAGED_DOMAINS</c>. Credentials live only in the environment, never in the repo.
/// </summary>
public sealed class ManagedExtractBackend : IManagedExtractBackend
{
    public const string HttpClientName = "occam.managed";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IReadOnlyDictionary<string, IManagedProvider> _providers;

    public ManagedExtractBackend(IHttpClientFactory httpClientFactory, IEnumerable<IManagedProvider> providers)
    {
        _httpClientFactory = httpClientFactory;
        _providers = providers.ToDictionary(p => p.Name, StringComparer.OrdinalIgnoreCase);
    }

    public string Name => "managed";

    public bool IsReady => ResolveProvider() is not null;

    public bool ShouldAttempt(string url) => ResolveProvider() is not null && IsHostOptedIn(url);

    // C2: the interface is async, but IManagedProvider.Fetch is still a synchronous call. Managed providers
    // are off by default and only reachable as the last cascade step on an opted-in host, so their internals
    // are left alone here — wrapping the sync result is honest about that rather than faking an await.
    public ValueTask<ExtractRunResult> ExtractAsync(string url, CancellationToken cancellationToken)
    {
        var provider = ResolveProvider();
        if (provider is null)
        {
            return ValueTask.FromResult(new ExtractRunResult(false, null, "managed", "managed_disabled", 0, url, false));
        }

        var apiKey = Environment.GetEnvironmentVariable("OCCAM_MANAGED_API_KEY");
        var baseUrl = Environment.GetEnvironmentVariable("OCCAM_MANAGED_BASE_URL");
        var client = _httpClientFactory.CreateClient(HttpClientName);
        return ValueTask.FromResult(provider.Fetch(client, url, apiKey, baseUrl, cancellationToken));
    }

    /// <summary>Returns the configured provider, or null when disabled / unresolvable / missing key.</summary>
    private IManagedProvider? ResolveProvider()
    {
        var name = Environment.GetEnvironmentVariable("OCCAM_MANAGED_PROVIDER")?.Trim();
        if (string.IsNullOrEmpty(name) || !_providers.TryGetValue(name, out var provider))
        {
            return null;
        }

        if (provider.RequiresApiKey
            && string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("OCCAM_MANAGED_API_KEY")))
        {
            return null;
        }

        return provider;
    }

    /// <summary>
    /// Per-domain opt-in. When <c>OCCAM_MANAGED_DOMAINS</c> is unset, any host is eligible (the
    /// provider env var is itself the opt-in). When set, the host must equal or be a subdomain of a
    /// listed domain.
    /// </summary>
    private static bool IsHostOptedIn(string url)
    {
        var raw = Environment.GetEnvironmentVariable("OCCAM_MANAGED_DOMAINS");
        if (string.IsNullOrWhiteSpace(raw))
        {
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        var host = uri.Host;
        foreach (var entry in raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var domain = entry.TrimStart('.').ToLowerInvariant();
            if (host.Equals(domain, StringComparison.OrdinalIgnoreCase)
                || host.EndsWith("." + domain, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
