namespace OccamMcp.Core.Canary;

/// <summary>
/// Bounded, in-memory audit trail of issued sentinels. Answers exactly one question for the
/// verifier — "did this host actually hand out a sentinel for this (session, bucket)?" — which is
/// what separates <see cref="CanaryVerdict.ReadVerified"/> from
/// <see cref="CanaryVerdict.ReplaySuspect"/>.
/// </summary>
/// <remarks>
/// Eviction is bounded twice over: by <see cref="CanaryOptions.IssueLogRetentionHours"/> and by
/// <see cref="CanaryOptions.IssueLogCapacity"/>, so a spray of requests cannot grow the process
/// heap without limit. Retention must cover the stale horizon, otherwise genuine older reads start
/// reporting as replay suspects — <see cref="CanaryOptionsValidator"/> does not enforce that
/// relationship, so operators overriding both values should read PROBE_PROTOCOL.md §6.2.
/// </remarks>
public sealed class CanaryIssueLog
{
    private readonly object _gate = new();
    private readonly Queue<CanaryIssueRecord> _records = new();
    private readonly Dictionary<IssueKey, int> _index = [];
    private readonly TimeProvider _timeProvider;
    private readonly int _capacity;
    private readonly TimeSpan _retention;

    /// <summary>Creates a log sized and time-bounded by <paramref name="options"/>.</summary>
    /// <param name="options">Active canary options.</param>
    /// <param name="timeProvider">Clock source; injectable so retention is testable without sleeping.</param>
    public CanaryIssueLog(CanaryOptions options, TimeProvider? timeProvider = null)
    {
        ArgumentNullException.ThrowIfNull(options);
        _timeProvider = timeProvider ?? TimeProvider.System;
        _capacity = options.IssueLogCapacity;
        _retention = TimeSpan.FromHours(options.IssueLogRetentionHours);
    }

    /// <summary>Number of records currently retained.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                PruneExpiredLocked();
                return _records.Count;
            }
        }
    }

    /// <summary>Appends an issuance record, evicting expired and surplus entries first.</summary>
    /// <param name="record">Record to retain.</param>
    public void Record(CanaryIssueRecord record)
    {
        lock (_gate)
        {
            PruneExpiredLocked();
            _records.Enqueue(record);
            var key = new IssueKey(record.SessionId, record.Bucket);
            _index[key] = _index.TryGetValue(key, out var existing) ? existing + 1 : 1;

            while (_records.Count > _capacity)
            {
                DropOldestLocked();
            }
        }
    }

    /// <summary>
    /// Whether a sentinel for this (session, bucket) pair was issued and is still retained.
    /// </summary>
    /// <param name="sessionId">Session to look up.</param>
    /// <param name="bucket">Bucket to look up.</param>
    /// <returns><c>true</c> when a matching, unexpired issuance record exists.</returns>
    public bool WasIssued(string sessionId, long bucket)
    {
        if (string.IsNullOrEmpty(sessionId))
        {
            return false;
        }

        lock (_gate)
        {
            PruneExpiredLocked();
            return _index.ContainsKey(new IssueKey(sessionId, bucket));
        }
    }

    /// <summary>Point-in-time copy of retained records, oldest first, for diagnostics endpoints.</summary>
    /// <returns>An independent snapshot; mutating the log afterwards does not affect it.</returns>
    public IReadOnlyList<CanaryIssueRecord> Snapshot()
    {
        lock (_gate)
        {
            PruneExpiredLocked();
            return [.. _records];
        }
    }

    private void PruneExpiredLocked()
    {
        var cutoff = _timeProvider.GetUtcNow() - _retention;
        while (_records.Count > 0 && _records.Peek().IssuedAt < cutoff)
        {
            DropOldestLocked();
        }
    }

    private void DropOldestLocked()
    {
        var evicted = _records.Dequeue();
        var key = new IssueKey(evicted.SessionId, evicted.Bucket);
        if (!_index.TryGetValue(key, out var count))
        {
            return;
        }

        if (count <= 1)
        {
            _index.Remove(key);
        }
        else
        {
            _index[key] = count - 1;
        }
    }

    private readonly record struct IssueKey(string SessionId, long Bucket);
}
