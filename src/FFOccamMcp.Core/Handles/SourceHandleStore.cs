using System.Text.RegularExpressions;

namespace OccamMcp.Core.Handles;

/// <summary>
/// Process-local search handles. <c>S1</c> is latest-search shorthand;
/// <c>Hxxxxxxxx</c> survives later searches until TTL or LRU eviction.
/// Raw URL is always stored; callers must run SSRF/preflight after resolve.
/// </summary>
public sealed record SourceHandleRecord(
    string Handle,
    string? Alias,
    string Url,
    string Title,
    string Query,
    int Generation,
    DateTimeOffset CreatedAt);

public static class SourceHandleSyntax
{
    private static readonly Regex AliasPattern = new(
        @"^S([1-9]|1[0-9]|20)$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static readonly Regex DurablePattern = new(
        @"^H[0-9a-f]{8}$",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static bool IsHandle(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var token = raw.Trim();
        return AliasPattern.IsMatch(token) || DurablePattern.IsMatch(token);
    }

    public static string Normalize(string raw) => raw.Trim();
}

public sealed class SourceHandleStore
{
    public const int MaxEntries = 64;
    public static readonly TimeSpan DefaultTtl = TimeSpan.FromHours(1);

    private readonly Func<DateTimeOffset> _clock;
    private readonly TimeSpan _ttl;
    private readonly object _gate = new();
    private readonly Dictionary<string, SourceHandleRecord> _byHandle = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _aliasToHandle = new(StringComparer.OrdinalIgnoreCase);
    private readonly LinkedList<string> _lru = new();
    private int _generation;
    private long _seq;

    public SourceHandleStore()
        : this(() => DateTimeOffset.UtcNow, DefaultTtl)
    {
    }

    public SourceHandleStore(Func<DateTimeOffset> clock, TimeSpan? ttl = null)
    {
        _clock = clock;
        _ttl = ttl ?? DefaultTtl;
    }

    public int Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    public int Count
    {
        get
        {
            lock (_gate)
            {
                return _byHandle.Count;
            }
        }
    }

    /// <summary>
    /// Pass-through for normal URLs. Resolves <c>S1</c>/<c>H…</c> against this process store.
    /// </summary>
    public bool TryBind(string? raw, out string url, out string? failureCode, out string? failureMessage)
    {
        url = raw?.Trim() ?? "";
        failureCode = null;
        failureMessage = null;
        if (!SourceHandleSyntax.IsHandle(url))
        {
            return true;
        }

        if (!TryResolve(url, out var record, out failureCode, out failureMessage))
        {
            return false;
        }

        url = record.Url;
        return true;
    }

    public IReadOnlyList<SourceHandleRecord> RememberSearch(
        string query,
        IReadOnlyList<(string Title, string Url)> hits)
    {
        lock (_gate)
        {
            var now = _clock();
            EvictExpired(now);
            _generation++;
            _aliasToHandle.Clear();

            var remembered = new List<SourceHandleRecord>(hits.Count);
            for (var i = 0; i < hits.Count; i++)
            {
                var hit = hits[i];
                _seq++;
                var handle = $"H{_seq:x8}";
                var alias = $"S{i + 1}";
                var record = new SourceHandleRecord(
                    handle,
                    alias,
                    hit.Url,
                    hit.Title,
                    query,
                    _generation,
                    now);
                _byHandle[handle] = record;
                _aliasToHandle[alias] = handle;
                TouchLru(handle);
                remembered.Add(record);
            }

            EvictOverflow();
            return remembered;
        }
    }

    public bool TryResolve(
        string token,
        out SourceHandleRecord record,
        out string? failureCode,
        out string? failureMessage)
    {
        record = null!;
        failureCode = null;
        failureMessage = null;
        var key = SourceHandleSyntax.Normalize(token);
        lock (_gate)
        {
            var now = _clock();
            EvictExpired(now);

            SourceHandleRecord? found = null;
            if (_byHandle.TryGetValue(key, out var byDurable))
            {
                found = byDurable;
            }
            else if (_aliasToHandle.TryGetValue(key, out var mapped)
                     && _byHandle.TryGetValue(mapped, out var byAlias))
            {
                found = byAlias;
            }

            if (found is null)
            {
                failureCode = key.StartsWith("H", StringComparison.OrdinalIgnoreCase)
                    ? "stale_handle"
                    : "unknown_handle";
                failureMessage = DescribeMiss(key);
                return false;
            }

            TouchLru(found.Handle);
            record = found;
            return true;
        }
    }

    public string[] ListLiveAliases()
    {
        lock (_gate)
        {
            return _aliasToHandle.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }

    private void TouchLru(string handle)
    {
        var node = _lru.Find(handle);
        if (node is not null)
        {
            _lru.Remove(node);
        }

        _lru.AddLast(handle);
    }

    private void EvictExpired(DateTimeOffset now)
    {
        if (_byHandle.Count == 0)
        {
            return;
        }

        List<string>? dead = null;
        foreach (var pair in _byHandle)
        {
            if (now - pair.Value.CreatedAt > _ttl)
            {
                dead ??= [];
                dead.Add(pair.Key);
            }
        }

        if (dead is null)
        {
            return;
        }

        foreach (var handle in dead)
        {
            Remove(handle);
        }
    }

    private void EvictOverflow()
    {
        while (_byHandle.Count > MaxEntries && _lru.First is not null)
        {
            Remove(_lru.First.Value);
        }
    }

    private void Remove(string handle)
    {
        if (_byHandle.TryGetValue(handle, out var record) && record.Alias is not null)
        {
            if (_aliasToHandle.TryGetValue(record.Alias, out var mapped)
                && string.Equals(mapped, handle, StringComparison.OrdinalIgnoreCase))
            {
                _aliasToHandle.Remove(record.Alias);
            }
        }

        _byHandle.Remove(handle);
        var node = _lru.Find(handle);
        if (node is not null)
        {
            _lru.Remove(node);
        }
    }

    private string DescribeMiss(string token)
    {
        var aliases = _aliasToHandle.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase).ToArray();
        var live = aliases.Length == 0
            ? "no live search aliases"
            : "live aliases: " + string.Join(", ", aliases);
        if (token.StartsWith("H", StringComparison.OrdinalIgnoreCase))
        {
            return $"Handle {token} is unknown or expired ({_ttl.TotalMinutes:0} min / {MaxEntries} cap). {live}. Pass the raw url instead.";
        }

        return $"Handle {token} is not in the current search. {live}. Pass result.handle or the raw url to keep a source across searches.";
    }
}
