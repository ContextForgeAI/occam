namespace OccamMcp.Core.Batch;

internal interface IBatchJobStore
{
    void Initialize();

    bool IsHealthy();

    string? FindJobByIdempotencyKey(string idempotencyKey, DateTimeOffset since);

    void InsertJob(BatchJobRecord job, IReadOnlyList<string> urls);

    BatchJobRecord? GetJob(string jobId);

    IReadOnlyList<BatchJobItemRecord> GetItems(string jobId, int cursor, int limit);

    int CountItems(string jobId);

    PendingBatchItem? ClaimNextPendingItem();

    void MarkItemRunning(string jobId, int seq);

    void MarkItemComplete(
        string jobId,
        int seq,
        bool ok,
        string? resultJson,
        string? failureCode,
        string? failureMessage);

    void RefreshJobProgress(string jobId);

    void MarkJobFailed(string jobId);
}
