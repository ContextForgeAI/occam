using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends.Managed;

/// <summary>
/// A managed extraction provider (Firecrawl, Jina, …). Package 3: opt-in escalation to a
/// third-party scraping API for anti-bot / JS-heavy pages the local http/browser backends cannot
/// crack. Providers normalize their wire shape to <see cref="ExtractRunResult"/>.
/// </summary>
public interface IManagedProvider
{
    /// <summary>Stable id matched against <c>OCCAM_MANAGED_PROVIDER</c> (e.g. "firecrawl", "jina").</summary>
    string Name { get; }

    /// <summary>True when this provider cannot run without <c>OCCAM_MANAGED_API_KEY</c>.</summary>
    bool RequiresApiKey { get; }

    /// <summary>
    /// Fetches <paramref name="url"/> through the managed service and returns a normalized result.
    /// Implementations must not throw: map failures to an <see cref="ExtractRunResult"/> with
    /// <c>Ok=false</c> and a typed failure code.
    /// </summary>
    ExtractRunResult Fetch(HttpClient client, string url, string? apiKey, string? baseUrl, CancellationToken cancellationToken);
}
