namespace OccamMcp.Core.Canary;

/// <summary>
/// Outcome of a rate-limit check.
/// </summary>
/// <param name="Allowed">Whether the caller may proceed.</param>
/// <param name="Remaining">Requests still available in the current window.</param>
/// <param name="RetryAfter">How long until the window resets; <see cref="TimeSpan.Zero"/> when allowed.</param>
public readonly record struct CanaryRateDecision(bool Allowed, int Remaining, TimeSpan RetryAfter);

/// <summary>
/// Fixed-window rate limiter guarding the canary endpoints.
/// </summary>
/// <remarks>
/// The canary endpoint is an oracle: it mints fresh proof material on demand, and its verify
/// counterpart reveals whether a given value is authentic. Both need a ceiling so a caller cannot
/// farm sentinels or probe the verifier at will. The key space is capped as well, because the key
/// derives from caller-controlled input and an unbounded map would itself be the attack.
/// </remarks>
public sealed class CanaryRateLimiter
{
    private readonly object _gate = new();
    private readonly Dictionary<string, Window> _windows = [];
    private readonly TimeProvider _timeProvider;
    private readonly int _permitsPerWindow;
    private readonly TimeSpan _windowLength;
    private readonly int _maxTrackedKeys;

    /// <summary>Creates a limiter configured from <paramref name="options"/>.</summary>
    /// <param name="options">Active canary options.</param>
    /// <param name="timeProvider">Clock source; injectable so window rollover is testable.</param>
    public CanaryRateLimiter(CanaryOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _permitsPerWindow = options.RateLimitRequestsPerWindow;
        _windowLength = TimeSpan.FromSeconds(options.RateLimitWindowSeconds);
        _maxTrackedKeys = options.RateLimitMaxTrackedKeys;
    }

    /// <summary>Number of keys currently tracked.</summary>
    public int TrackedKeys
    {
        get
        {
            lock (_gate)
            {
                return _windows.Count;
            }
        }
    }

    /// <summary>
    /// Consumes one permit for <paramref name="key"/> if the window allows it.
    /// </summary>
    /// <param name="key">Rate-limit key; callers should combine session and hashed client.</param>
    /// <returns>The decision, including remaining permits and reset delay.</returns>
    public CanaryRateDecision TryAcquire(string key)
    {
        ArgumentException.ThrowIfNullOrEmpty(key);
        var now = _timeProvider.GetUtcNow();

        lock (_gate)
        {
            if (_windows.TryGetValue(key, out var window) && window.StartedAt + _windowLength > now)
            {
                if (window.Used >= _permitsPerWindow)
                {
                    return new CanaryRateDecision(
                        Allowed: false,
                        Remaining: 0,
                        RetryAfter: window.StartedAt + _windowLength - now);
                }

                _windows[key] = window with { Used = window.Used + 1 };
                return new CanaryRateDecision(true, _permitsPerWindow - window.Used - 1, TimeSpan.Zero);
            }

            // New or expired window. Reclaim space only when the map is actually under pressure, so
            // the common path stays a single dictionary probe.
            if (_windows.Count >= _maxTrackedKeys)
            {
                EvictLocked(now);
            }

            _windows[key] = new Window(now, 1);
            return new CanaryRateDecision(true, _permitsPerWindow - 1, TimeSpan.Zero);
        }
    }

    private void EvictLocked(DateTimeOffset now)
    {
        var expired = new List<string>();
        foreach (var (key, window) in _windows)
        {
            if (window.StartedAt + _windowLength <= now)
            {
                expired.Add(key);
            }
        }

        foreach (var key in expired)
        {
            _windows.Remove(key);
        }

        if (_windows.Count < _maxTrackedKeys)
        {
            return;
        }

        // Still full: every window is live, which means a genuine spray. Drop the oldest half so the
        // limiter degrades predictably instead of rejecting all new keys outright.
        var ordered = new List<KeyValuePair<string, Window>>(_windows);
        ordered.Sort(static (a, b) => a.Value.StartedAt.CompareTo(b.Value.StartedAt));
        var dropCount = Math.Max(1, ordered.Count / 2);
        for (var i = 0; i < dropCount; i++)
        {
            _windows.Remove(ordered[i].Key);
        }
    }

    private readonly record struct Window(DateTimeOffset StartedAt, int Used);
}
