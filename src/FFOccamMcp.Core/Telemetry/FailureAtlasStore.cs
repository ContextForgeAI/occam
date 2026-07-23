using System.Globalization;

namespace OccamMcp.Core.Telemetry;

/// <summary>
/// SI-10: an in-process aggregation of transcode outcomes per host — the "failure atlas". Over a
/// running host's lifetime it accumulates, per host, how many extractions succeeded vs failed and the
/// breakdown of failure codes, so an operator/agent can see a **closure map**: which hosts are provably
/// walled (captcha / login / 4xx) and would keep failing, vs transient blips worth a retry. In-memory
/// and bounded (no persistence — the map is meaningful only for the current run); reads come through the
/// opt-in <c>occam_failure_atlas</c> tool. Thread-safe.
/// </summary>
public sealed class FailureAtlasStore
{
    private const int MaxHosts = 500;
    private readonly object _lock = new();
    private readonly Dictionary<string, HostEntry> _hosts = new(StringComparer.OrdinalIgnoreCase);

    private sealed class HostEntry
    {
        public int Successes;
        public readonly Dictionary<string, int> FailureCounts = new(StringComparer.Ordinal);
        public string? LastFailureAt;
    }

    public void RecordSuccess(string url)
    {
        if (!TryHost(url, out var host))
        {
            return;
        }

        lock (_lock)
        {
            if (TryGetOrAdd(host, out var entry))
            {
                entry.Successes++;
            }
        }
    }

    public void RecordFailure(string url, string? failureCode)
    {
        if (!TryHost(url, out var host))
        {
            return;
        }

        var code = string.IsNullOrWhiteSpace(failureCode) ? "unknown" : failureCode;
        lock (_lock)
        {
            if (TryGetOrAdd(host, out var entry))
            {
                entry.FailureCounts[code] = entry.FailureCounts.GetValueOrDefault(code) + 1;
                entry.LastFailureAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            }
        }
    }

    /// <summary>Per-host closure summaries, worst (highest closure rate, then most failures) first.</summary>
    public IReadOnlyList<FailureAtlasHostSummary> Snapshot()
    {
        lock (_lock)
        {
            return _hosts
                .Select(kv => FailureAtlasClassifier.Summarize(
                    kv.Key,
                    kv.Value.Successes,
                    kv.Value.FailureCounts.Select(f => new FailureCodeCount(f.Key, f.Value)).ToArray(),
                    kv.Value.LastFailureAt))
                .OrderByDescending(s => s.ClosureRate)
                .ThenByDescending(s => s.Failures)
                .ToArray();
        }
    }

    private bool TryGetOrAdd(string host, out HostEntry entry)
    {
        if (_hosts.TryGetValue(host, out var existing))
        {
            entry = existing;
            return true;
        }

        if (_hosts.Count >= MaxHosts)
        {
            entry = null!;
            return false; // bounded — stop tracking new hosts once full
        }

        entry = new HostEntry();
        _hosts[host] = entry;
        return true;
    }

    private static bool TryHost(string url, out string host)
    {
        host = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || string.IsNullOrEmpty(uri.Host))
        {
            return false;
        }

        var lower = uri.Host.ToLowerInvariant();
        host = lower.StartsWith("www.", StringComparison.Ordinal) ? lower[4..] : lower;
        return true;
    }
}

/// <summary>One failure code and how many times it occurred for a host.</summary>
public sealed record FailureCodeCount(string Code, int Count);

/// <summary>
/// A host's closure profile. <see cref="Walled"/> is the headline: no success ever AND the dominant
/// failure is an honest closure (a provable wall — captcha/login/4xx), so further attempts are wasted.
/// <see cref="ClosureRate"/> is the share of attempts that hit an honest closure.
/// </summary>
public sealed record FailureAtlasHostSummary(
    string Host,
    int Attempts,
    int Successes,
    int Failures,
    double ClosureRate,
    bool Walled,
    string? DominantFailure,
    FailureCodeCount[] ByCode,
    string? LastFailureAt);

/// <summary>
/// Pure closure classification — the gate-testable core. An "honest closure" is a provable wall where
/// retrying won't help (anti-bot challenge, auth wall, or a definitive 4xx); transient codes
/// (timeout/network/dns/429/5xx) are excluded because they are worth a retry.
/// </summary>
public static class FailureAtlasClassifier
{
    private static readonly HashSet<string> ClosureCodes = new(StringComparer.Ordinal)
    {
        "captcha_or_challenge",
        "requires_login",
        "http_401",
        "http_403",
        "http_404",
        "http_410",
    };

    public static bool IsClosure(string code) => ClosureCodes.Contains(code);

    public static FailureAtlasHostSummary Summarize(
        string host, int successes, FailureCodeCount[] byCode, string? lastFailureAt)
    {
        var failures = byCode.Sum(c => c.Count);
        var attempts = successes + failures;
        var closures = byCode.Where(c => IsClosure(c.Code)).Sum(c => c.Count);
        var closureRate = attempts == 0 ? 0.0 : Math.Round((double)closures / attempts, 4);

        var dominant = byCode
            .OrderByDescending(c => c.Count)
            .ThenBy(c => c.Code, StringComparer.Ordinal)
            .Select(c => c.Code)
            .FirstOrDefault();

        var walled = successes == 0 && failures > 0 && dominant is not null && IsClosure(dominant);

        return new FailureAtlasHostSummary(
            host, attempts, successes, failures, closureRate, walled, dominant,
            [.. byCode.OrderByDescending(c => c.Count).ThenBy(c => c.Code, StringComparer.Ordinal)],
            lastFailureAt);
    }
}
