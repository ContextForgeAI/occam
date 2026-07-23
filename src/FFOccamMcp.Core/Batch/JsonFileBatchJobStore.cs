using System.Text.Json;

namespace OccamMcp.Core.Batch;

/// <summary>
/// Pure-managed batch job store: in-memory model + atomic JSON snapshot persistence.
/// Replaces the SQLite store to drop the native SQLite dependency and its unpatched
/// high-severity advisory (CVE-2025-6965 / GHSA-2m69-gcr7-jv3q). All access is serialized
/// through a single lock — identical concurrency to the original store — and the batch
/// workload (job metadata for network-bound extracts) is small enough that in-memory
/// operations outperform SQLite here, with zero native interop and no SQL parsing.
/// </summary>
internal sealed class JsonFileBatchJobStore : IBatchJobStore, IDisposable
{
    private readonly string _path;
    private readonly object _sync = new();
    private readonly Dictionary<string, BatchJobSnapshot> _jobs = new(StringComparer.Ordinal);
    private BatchStoreSnapshot _snapshot = new();
    private bool _initialized;

    public JsonFileBatchJobStore()
        : this(DefaultPath())
    {
    }

    internal JsonFileBatchJobStore(string path) => _path = path;

    // SQLite store used a .db file; use a sibling .json so a stale .db is never misread.
    private static string DefaultPath() => Path.ChangeExtension(BatchSettings.DbPath, ".json");

    public bool IsHealthy()
    {
        lock (_sync)
        {
            return _initialized;
        }
    }

    public void Initialize()
    {
        lock (_sync)
        {
            var dir = Path.GetDirectoryName(_path);
            if (!string.IsNullOrWhiteSpace(dir))
            {
                Directory.CreateDirectory(dir);
            }

            _snapshot = LoadSnapshot(_path);
            _jobs.Clear();
            foreach (var job in _snapshot.Jobs)
            {
                _jobs[job.Id] = job;
            }

            _initialized = true;
        }
    }

    public string? FindJobByIdempotencyKey(string idempotencyKey, DateTimeOffset since)
    {
        lock (_sync)
        {
            EnsureInit();
            BatchJobSnapshot? best = null;
            foreach (var job in _snapshot.Jobs)
            {
                if (job.IdempotencyKey == idempotencyKey
                    && job.SubmittedAt >= since
                    && (best is null || job.SubmittedAt > best.SubmittedAt))
                {
                    best = job;
                }
            }

            return best?.Id;
        }
    }

    public void InsertJob(BatchJobRecord job, IReadOnlyList<string> urls)
    {
        lock (_sync)
        {
            EnsureInit();
            var snapshot = new BatchJobSnapshot
            {
                Id = job.Id,
                State = job.State,
                SubmittedAt = job.SubmittedAt,
                Params = job.Params,
                IdempotencyKey = job.IdempotencyKey,
                Total = urls.Count,
                Items = new List<BatchItemSnapshot>(urls.Count),
            };

            for (var i = 0; i < urls.Count; i++)
            {
                snapshot.Items.Add(new BatchItemSnapshot
                {
                    Seq = i,
                    Url = urls[i],
                    State = BatchItemStates.Pending,
                });
            }

            _snapshot.Jobs.Add(snapshot);
            _jobs[job.Id] = snapshot;
            Persist();
        }
    }

    public BatchJobRecord? GetJob(string jobId)
    {
        lock (_sync)
        {
            EnsureInit();
            return _jobs.TryGetValue(jobId, out var job) ? ToRecord(job) : null;
        }
    }

    public IReadOnlyList<BatchJobItemRecord> GetItems(string jobId, int cursor, int limit)
    {
        lock (_sync)
        {
            EnsureInit();
            var items = new List<BatchJobItemRecord>();
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                return items;
            }

            // Items are stored in seq order, matching the SQLite ORDER BY seq.
            foreach (var item in job.Items)
            {
                if (item.Seq < cursor)
                {
                    continue;
                }

                items.Add(new BatchJobItemRecord(
                    jobId,
                    item.Seq,
                    item.Url,
                    item.State,
                    item.ResultJson,
                    item.FailureCode,
                    item.FailureMessage));

                if (items.Count >= limit)
                {
                    break;
                }
            }

            return items;
        }
    }

    public int CountItems(string jobId)
    {
        lock (_sync)
        {
            EnsureInit();
            return _jobs.TryGetValue(jobId, out var job) ? job.Items.Count : 0;
        }
    }

    public PendingBatchItem? ClaimNextPendingItem()
    {
        lock (_sync)
        {
            EnsureInit();

            // Global order: earliest submitted job (in queued/running) then earliest pending seq.
            BatchJobSnapshot? bestJob = null;
            BatchItemSnapshot? bestItem = null;
            foreach (var job in _snapshot.Jobs)
            {
                if (job.State != BatchJobStates.Queued && job.State != BatchJobStates.Running)
                {
                    continue;
                }

                var firstPending = FirstPending(job);
                if (firstPending is null)
                {
                    continue;
                }

                if (bestJob is null
                    || job.SubmittedAt < bestJob.SubmittedAt
                    || (job.SubmittedAt == bestJob.SubmittedAt && firstPending.Seq < bestItem!.Seq))
                {
                    bestJob = job;
                    bestItem = firstPending;
                }
            }

            if (bestJob is null || bestItem is null)
            {
                return null;
            }

            if (bestJob.State == BatchJobStates.Queued)
            {
                bestJob.State = BatchJobStates.Running;
            }

            bestItem.State = BatchItemStates.Running;
            bestJob.Running += 1;
            Persist();

            return new PendingBatchItem(bestJob.Id, bestItem.Seq, bestItem.Url, bestJob.Params);
        }
    }

    public void MarkItemRunning(string jobId, int seq)
    {
        lock (_sync)
        {
            EnsureInit();
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                return;
            }

            if (job.State == BatchJobStates.Queued)
            {
                job.State = BatchJobStates.Running;
            }

            job.Running += 1;

            var item = FindItem(job, seq);
            if (item is not null && item.State == BatchItemStates.Pending)
            {
                item.State = BatchItemStates.Running;
            }

            Persist();
        }
    }

    public void MarkItemComplete(
        string jobId,
        int seq,
        bool ok,
        string? resultJson,
        string? failureCode,
        string? failureMessage)
    {
        lock (_sync)
        {
            EnsureInit();
            if (!_jobs.TryGetValue(jobId, out var job))
            {
                return;
            }

            var item = FindItem(job, seq);
            if (item is not null)
            {
                item.State = ok ? BatchItemStates.Done : BatchItemStates.Failed;
                item.ResultJson = resultJson;
                item.FailureCode = failureCode;
                item.FailureMessage = failureMessage;
            }

            job.Running = job.Running > 0 ? job.Running - 1 : 0;
            if (ok)
            {
                job.Done += 1;
            }
            else
            {
                job.Failed += 1;
            }

            RefreshLocked(job);
            Persist();
        }
    }

    public void RefreshJobProgress(string jobId)
    {
        lock (_sync)
        {
            EnsureInit();
            if (_jobs.TryGetValue(jobId, out var job))
            {
                RefreshLocked(job);
                Persist();
            }
        }
    }

    public void MarkJobFailed(string jobId)
    {
        lock (_sync)
        {
            EnsureInit();
            if (_jobs.TryGetValue(jobId, out var job))
            {
                job.State = BatchJobStates.Failed;
                Persist();
            }
        }
    }

    public void Dispose()
    {
    }

    private static void RefreshLocked(BatchJobSnapshot job)
    {
        if (job.State == BatchJobStates.Running
            && job.Done + job.Failed >= job.Total
            && job.Running == 0)
        {
            job.State = BatchJobStates.Done;
        }
    }

    private static BatchItemSnapshot? FirstPending(BatchJobSnapshot job)
    {
        foreach (var item in job.Items)
        {
            if (item.State == BatchItemStates.Pending)
            {
                return item;
            }
        }

        return null;
    }

    private static BatchItemSnapshot? FindItem(BatchJobSnapshot job, int seq)
    {
        foreach (var item in job.Items)
        {
            if (item.Seq == seq)
            {
                return item;
            }
        }

        return null;
    }

    private static BatchJobRecord ToRecord(BatchJobSnapshot job) =>
        new(
            job.Id,
            job.State,
            job.SubmittedAt,
            job.Params,
            job.IdempotencyKey,
            new BatchProgress(job.Total, job.Done, job.Failed, job.Running));

    private void EnsureInit()
    {
        if (!_initialized)
        {
            Initialize();
        }
    }

    private void Persist()
    {
        try
        {
            var json = JsonSerializer.Serialize(_snapshot, BatchJsonContext.Default.BatchStoreSnapshot);
            var tmp = _path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, _path, overwrite: true);
        }
        catch
        {
            // Best-effort durability; the in-memory state stays authoritative for this run.
        }
    }

    private static BatchStoreSnapshot LoadSnapshot(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                var json = File.ReadAllText(path);
                var snap = JsonSerializer.Deserialize(json, BatchJsonContext.Default.BatchStoreSnapshot);
                if (snap is not null)
                {
                    snap.Jobs ??= new List<BatchJobSnapshot>();
                    return snap;
                }
            }
        }
        catch
        {
            // Corrupt snapshot -> start from empty rather than crashing the server.
        }

        return new BatchStoreSnapshot();
    }
}

internal sealed class BatchStoreSnapshot
{
    public List<BatchJobSnapshot> Jobs { get; set; } = new();
}

internal sealed class BatchJobSnapshot
{
    public string Id { get; set; } = "";
    public string State { get; set; } = BatchJobStates.Queued;
    public DateTimeOffset SubmittedAt { get; set; }
    public BatchJobParams Params { get; set; } = null!;
    public string? IdempotencyKey { get; set; }
    public int Total { get; set; }
    public int Done { get; set; }
    public int Failed { get; set; }
    public int Running { get; set; }
    public List<BatchItemSnapshot> Items { get; set; } = new();
}

internal sealed class BatchItemSnapshot
{
    public int Seq { get; set; }
    public string Url { get; set; } = "";
    public string State { get; set; } = BatchItemStates.Pending;
    public string? ResultJson { get; set; }
    public string? FailureCode { get; set; }
    public string? FailureMessage { get; set; }
}
