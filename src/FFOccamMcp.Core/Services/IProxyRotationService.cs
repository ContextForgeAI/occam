namespace OccamMcp.Core.Services;

/// <summary>
/// Rotating egress proxy pool for mass-scrape / batch workloads (Tier-E).
/// When not configured, callers fall back to <see cref="Workers.EgressProxyConfig"/>.
/// </summary>
public interface IProxyRotationService
{
    /// <summary>True when at least one proxy URL loaded from file or list env.</summary>
    bool IsConfigured { get; }

    /// <summary>Number of proxies in the pool (0 when not configured).</summary>
    int Count { get; }

    /// <summary>
    /// Returns the next proxy for one worker spawn (round-robin).
    /// Null when the pool is empty — use static <c>OCCAM_HTTP_PROXY</c> instead.
    /// </summary>
    RotatedProxy? AcquireNext();
}

/// <summary>One proxy endpoint for a single extract spawn.</summary>
/// <param name="ProxyUrl">Absolute http/https/socks5 URL passed to worker env.</param>
public readonly record struct RotatedProxy(string ProxyUrl);
