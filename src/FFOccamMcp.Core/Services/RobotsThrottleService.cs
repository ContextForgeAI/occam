using System.Collections.Concurrent;
using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Services;

/// <summary>
/// Opt-in polite-fetch policy: respects robots.txt <c>Disallow</c>/<c>Crawl-delay</c> and applies a
/// per-host minimum request interval. Entirely env-gated — when both <c>OCCAM_RESPECT_ROBOTS</c> and
/// <c>OCCAM_HOST_THROTTLE_MS</c> are unset the service is a no-op and never fetches robots.txt, so
/// default behavior is unchanged. Occam is user-directed (not a crawler), so this is off by default.
/// </summary>
public interface IRobotsThrottleService
{
    /// <summary>
    /// Returns a failure code (currently <c>robots_disallowed</c>) when the fetch must not proceed,
    /// otherwise <c>null</c> after applying any per-host throttle delay.
    /// </summary>
    string? CheckAndThrottle(string url, CancellationToken cancellationToken);
}

public sealed class RobotsThrottleService(IHttpClientFactory httpClientFactory) : IRobotsThrottleService
{
    public const string HttpClientName = "occam.robots";

    private readonly ConcurrentDictionary<string, RobotsRules> _rulesCache = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, long> _nextAllowedTicks = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _throttleLock = new();

    public string? CheckAndThrottle(string url, CancellationToken cancellationToken)
    {
        var respectRobots = OccamEnvironment.GetFlag("OCCAM_RESPECT_ROBOTS", defaultValue: false);
        var throttleMs = OccamEnvironment.GetInt("OCCAM_HOST_THROTTLE_MS", defaultValue: 0, min: 0, max: 600_000);
        if (!respectRobots && throttleMs <= 0)
        {
            return null; // disabled — zero behavior change, no robots.txt fetch
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return null;
        }

        var host = uri.Authority;
        RobotsRules? rules = null;
        if (respectRobots)
        {
            rules = _rulesCache.GetOrAdd(host, _ => FetchRules(uri, cancellationToken));
            if (rules.IsDisallowed(uri.AbsolutePath))
            {
                return "robots_disallowed";
            }
        }

        var minIntervalMs = throttleMs;
        if (rules?.CrawlDelaySeconds is int cd && cd > 0)
        {
            minIntervalMs = Math.Max(minIntervalMs, cd * 1000);
        }
        if (minIntervalMs > 0)
        {
            ApplyThrottle(host, minIntervalMs, cancellationToken);
        }
        return null;
    }

    private void ApplyThrottle(string host, int minIntervalMs, CancellationToken cancellationToken)
    {
        long waitMs;
        lock (_throttleLock)
        {
            var now = Environment.TickCount64;
            var nextAllowed = _nextAllowedTicks.TryGetValue(host, out var t) ? t : now;
            var start = Math.Max(now, nextAllowed);
            waitMs = start - now;
            // Reserve the following slot so concurrent callers to the same host queue politely.
            _nextAllowedTicks[host] = start + minIntervalMs;
        }
        if (waitMs > 0)
        {
            try
            {
                Task.Delay(TimeSpan.FromMilliseconds(waitMs), cancellationToken).GetAwaiter().GetResult();
            }
            catch (OperationCanceledException)
            {
                // Cancellation falls through to the caller's own cancellation handling.
            }
        }
    }

    private RobotsRules FetchRules(Uri uri, CancellationToken cancellationToken)
    {
        try
        {
            var robotsUrl = $"{uri.Scheme}://{uri.Authority}/robots.txt";
            var client = httpClientFactory.CreateClient(HttpClientName);
            using var request = new HttpRequestMessage(HttpMethod.Get, robotsUrl);
            using var response = client.Send(request, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return RobotsRules.AllowAll;
            }

            var body = response.Content.ReadAsStringAsync(cancellationToken).GetAwaiter().GetResult();
            return RobotsRules.Parse(body);
        }
        catch
        {
            // A robots.txt fetch failure must never block the actual request — fail open.
            return RobotsRules.AllowAll;
        }
    }
}

/// <summary>Parsed robots.txt rules for the <c>*</c> user-agent group (the conservative subset).</summary>
public sealed class RobotsRules
{
    public static readonly RobotsRules AllowAll = new([], null);

    private readonly string[] _disallow;

    public int? CrawlDelaySeconds { get; }

    private RobotsRules(string[] disallow, int? crawlDelaySeconds)
    {
        _disallow = disallow;
        CrawlDelaySeconds = crawlDelaySeconds;
    }

    public bool IsDisallowed(string path)
    {
        foreach (var prefix in _disallow)
        {
            if (prefix.Length > 0 && path.StartsWith(prefix, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    /// <summary>
    /// Parses the <c>User-agent: *</c> group. Honors grouped user-agent lines, ignores comments,
    /// collects non-empty <c>Disallow</c> prefixes and the largest <c>Crawl-delay</c>. <c>Allow</c>
    /// is intentionally not modeled (conservative: any matching Disallow prefix blocks).
    /// </summary>
    public static RobotsRules Parse(string body)
    {
        var disallow = new List<string>();
        int? crawlDelay = null;
        var starActive = false;
        var prevWasAgent = false;

        foreach (var rawLine in body.Split('\n'))
        {
            var line = rawLine;
            var hash = line.IndexOf('#');
            if (hash >= 0)
            {
                line = line[..hash];
            }
            line = line.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            var colon = line.IndexOf(':');
            if (colon <= 0)
            {
                continue;
            }

            var field = line[..colon].Trim().ToLowerInvariant();
            var value = line[(colon + 1)..].Trim();

            if (field == "user-agent")
            {
                if (!prevWasAgent)
                {
                    starActive = false; // a non-agent line ended the previous group
                }
                if (value == "*")
                {
                    starActive = true;
                }
                prevWasAgent = true;
                continue;
            }

            prevWasAgent = false;
            if (!starActive)
            {
                continue;
            }

            if (field == "disallow" && value.Length > 0)
            {
                disallow.Add(value);
            }
            else if (field == "crawl-delay"
                && int.TryParse(value, out var seconds)
                && seconds > 0)
            {
                crawlDelay = crawlDelay is int existing ? Math.Max(existing, seconds) : seconds;
            }
        }

        return disallow.Count == 0 && crawlDelay is null
            ? AllowAll
            : new RobotsRules([.. disallow], crawlDelay);
    }
}
