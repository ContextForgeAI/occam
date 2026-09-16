namespace OccamMcp.Core.Exam;

/// <summary>
/// Remembers a graded exam so the same client is not re-examined on every connect.
/// </summary>
/// <remarks>
/// <para>
/// Bounded in both time and size. The time bound exists because a tier is a claim about a model,
/// and models change under a stable client name — a 24-hour entry is a compromise between
/// re-examining constantly and authorising a surface on stale evidence. The size bound exists
/// because the key includes a session id, which is caller-influenced.
/// </para>
/// <para>
/// Eviction is oldest-first rather than least-recently-used. LRU would keep a long-lived session
/// pinned indefinitely, which is the opposite of what the TTL is for.
/// </para>
/// </remarks>
public sealed class ExamResultCache
{
    /// <summary>Default entry lifetime.</summary>
    public const int DefaultTtlHours = 24;

    /// <summary>Default maximum retained entries.</summary>
    public const int DefaultCapacity = 1_024;

    private readonly object _gate = new();
    private readonly Dictionary<ExamSubject, Entry> _entries = [];
    private readonly Queue<ExamSubject> _insertionOrder = new();
    private readonly TimeProvider _timeProvider;
    private readonly TimeSpan _ttl;
    private readonly int _capacity;

    /// <summary>Creates a cache.</summary>
    /// <param name="ttlHours">Entry lifetime in hours; must be positive.</param>
    /// <param name="capacity">Maximum retained entries; must be positive.</param>
    /// <param name="timeProvider">Clock source; injectable so expiry is testable without sleeping.</param>
    public ExamResultCache(
        int ttlHours = DefaultTtlHours,
        int capacity = DefaultCapacity,
        TimeProvider? timeProvider = null)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(ttlHours);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(capacity);
        _ttl = TimeSpan.FromHours(ttlHours);
        _capacity = capacity;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>Number of unexpired entries currently retained.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                PruneExpiredLocked(_timeProvider.GetUtcNow());
                return _entries.Count;
            }
        }
    }

    /// <summary>Stores a result, replacing any existing entry for the same subject.</summary>
    /// <param name="subject">Who the result belongs to.</param>
    /// <param name="result">The graded result.</param>
    public void Store(ExamSubject subject, ExamResult result)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            PruneExpiredLocked(now);

            if (!_entries.ContainsKey(subject))
            {
                _insertionOrder.Enqueue(subject);
            }

            _entries[subject] = new Entry(result, now + _ttl);

            while (_entries.Count > _capacity && _insertionOrder.Count > 0)
            {
                var oldest = _insertionOrder.Dequeue();
                _entries.Remove(oldest);
            }
        }
    }

    /// <summary>Retrieves an unexpired result.</summary>
    /// <param name="subject">Who to look up.</param>
    /// <param name="result">The cached result when found.</param>
    /// <returns><c>true</c> when a live entry exists.</returns>
    public bool TryGet(ExamSubject subject, out ExamResult result)
    {
        lock (_gate)
        {
            var now = _timeProvider.GetUtcNow();
            if (_entries.TryGetValue(subject, out var entry))
            {
                // Checked per lookup, not just during a sweep: a re-stored entry keeps its original
                // queue position, so sweep order alone cannot guarantee an expired entry is gone.
                if (entry.ExpiresAt > now)
                {
                    result = entry.Result;
                    return true;
                }

                _entries.Remove(subject);
            }

            result = default;
            return false;
        }
    }

    /// <summary>
    /// Returns the cached result, or the default-tier result for a client that never sat the exam.
    /// </summary>
    /// <param name="subject">Who to look up.</param>
    /// <returns>A result that is always safe to act on.</returns>
    public ExamResult GetOrDefault(ExamSubject subject) =>
        TryGet(subject, out var result) ? result : ExamResult.Default(_timeProvider.GetUtcNow());

    /// <summary>Drops every entry. Used when an operator changes the surface policy at runtime.</summary>
    public void Clear()
    {
        lock (_gate)
        {
            _entries.Clear();
            _insertionOrder.Clear();
        }
    }

    /// <summary>
    /// Drops every expired entry. A full scan rather than a queue drain: re-storing a subject
    /// extends its expiry without moving it in the queue, so queue order is not expiry order and a
    /// front-to-back drain would stop early and leave expired entries reachable.
    /// </summary>
    private void PruneExpiredLocked(DateTimeOffset now)
    {
        if (_entries.Count == 0)
        {
            return;
        }

        List<ExamSubject>? expired = null;
        foreach (var (subject, entry) in _entries)
        {
            if (entry.ExpiresAt <= now)
            {
                (expired ??= []).Add(subject);
            }
        }

        if (expired is null)
        {
            return;
        }

        foreach (var subject in expired)
        {
            _entries.Remove(subject);
        }
    }

    private readonly record struct Entry(ExamResult Result, DateTimeOffset ExpiresAt);
}
