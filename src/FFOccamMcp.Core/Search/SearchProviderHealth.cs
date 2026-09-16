using System.Collections.Concurrent;

namespace OccamMcp.Core.Search;

/// <summary>
/// Process-local per-provider health: fixed-window rate limit + temporary degrade after
/// 429 / CAPTCHA (202) / timeout. Fan-out skips unhealthy providers instead of waiting.
/// </summary>
public sealed class SearchProviderHealth
{
    private readonly TimeProvider _time;
    private readonly ConcurrentDictionary<string, DateTimeOffset> _degradedUntil =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, RateWindow> _windows =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _rateGate = new();

    private readonly int _rateMax;
    private readonly TimeSpan _rateWindow;
    private readonly TimeSpan _degradeFor;

    public SearchProviderHealth(TimeProvider? timeProvider = null)
    {
        _time = timeProvider ?? TimeProvider.System;
        _rateMax = Configuration.OccamEnvironment.GetInt(
            "OCCAM_SEARCH_RATE_MAX", defaultValue: 30, min: 1, max: 10_000);
        var windowS = Configuration.OccamEnvironment.GetInt(
            "OCCAM_SEARCH_RATE_WINDOW_S", defaultValue: 60, min: 1, max: 3600);
        _rateWindow = TimeSpan.FromSeconds(windowS);
        var degradeMin = Configuration.OccamEnvironment.GetInt(
            "OCCAM_SEARCH_DEGRADE_MINUTES", defaultValue: 5, min: 1, max: 120);
        _degradeFor = TimeSpan.FromMinutes(degradeMin);
    }

    /// <summary>True when the provider may be called now (not degraded, rate permit available).</summary>
    public bool TryBeginCall(string provider)
    {
        ArgumentException.ThrowIfNullOrEmpty(provider);
        var now = _time.GetUtcNow();

        if (_degradedUntil.TryGetValue(provider, out var until))
        {
            if (until > now)
            {
                Log("skipped_degraded", provider, "degraded", until);
                return false;
            }

            if (_degradedUntil.TryRemove(provider, out _))
            {
                Log("recovered", provider, "cooldown_elapsed", null);
            }
        }

        if (!TryAcquireRate(provider, now, out var retryAfter))
        {
            Log("rate_limited", provider, "rate_window", now + retryAfter);
            return false;
        }

        return true;
    }

    /// <summary>Records success (clears degrade) or a degrade-worthy failure.</summary>
    public void Observe(SearchOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        if (string.IsNullOrEmpty(outcome.Provider) || outcome.Provider is "none" or "fanout")
        {
            return;
        }

        if (outcome.Ok)
        {
            if (_degradedUntil.TryRemove(outcome.Provider, out _))
            {
                Log("recovered", outcome.Provider, "success", null);
            }

            return;
        }

        if (!IsDegradeWorthy(outcome.FailureCode))
        {
            return;
        }

        var until = _time.GetUtcNow() + _degradeFor;
        _degradedUntil[outcome.Provider] = until;
        Log("degraded", outcome.Provider, outcome.FailureCode ?? "search_error", until);
    }

    internal bool IsDegraded(string provider)
    {
        if (!_degradedUntil.TryGetValue(provider, out var until))
        {
            return false;
        }

        return until > _time.GetUtcNow();
    }

    internal static bool IsDegradeWorthy(string? failureCode) =>
        failureCode is "search_http_429" or "search_http_202" or "search_timeout";

    private bool TryAcquireRate(string provider, DateTimeOffset now, out TimeSpan retryAfter)
    {
        lock (_rateGate)
        {
            if (_windows.TryGetValue(provider, out var window) && window.StartedAt + _rateWindow > now)
            {
                if (window.Used >= _rateMax)
                {
                    retryAfter = window.StartedAt + _rateWindow - now;
                    return false;
                }

                _windows[provider] = window with { Used = window.Used + 1 };
                retryAfter = TimeSpan.Zero;
                return true;
            }

            _windows[provider] = new RateWindow(now, Used: 1);
            retryAfter = TimeSpan.Zero;
            return true;
        }
    }

    private static void Log(string eventName, string provider, string reason, DateTimeOffset? untilUtc)
    {
        var until = untilUtc is { } u ? u.UtcDateTime.ToString("O") : "";
        Console.Error.WriteLine(
            $"[occam.search] event={eventName} provider={provider} reason={reason} untilUtc={until}");
    }

    private readonly record struct RateWindow(DateTimeOffset StartedAt, int Used);
}
