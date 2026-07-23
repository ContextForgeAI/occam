using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;

namespace OccamMcp.Core.Batch;

public interface IBatchJobService
{
    (BatchSubmitResponse? Response, BatchFailureInfo? Error) Submit(BatchSubmitRequest request);

    (BatchStatusResponse? Response, BatchFailureInfo? Error) GetStatus(string jobId);

    (BatchResultsResponse? Response, BatchFailureInfo? Error) GetResults(string jobId, int cursor, int limit);
}

internal sealed class BatchJobService(IBatchJobStore store) : IBatchJobService
{
    private static readonly TimeSpan IdempotencyWindow = TimeSpan.FromHours(24);

    public (BatchSubmitResponse? Response, BatchFailureInfo? Error) Submit(BatchSubmitRequest request)
    {
        if (!TryValidateSubmit(request, out var urls, out var jobParams, out var error))
        {
            return (null, error);
        }

        if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
        {
            var since = DateTimeOffset.UtcNow - IdempotencyWindow;
            var existing = store.FindJobByIdempotencyKey(request.IdempotencyKey.Trim(), since);
            if (existing is not null)
            {
                var existingJob = store.GetJob(existing);
                if (existingJob is not null)
                {
                    return (new BatchSubmitResponse(existing, existingJob.Progress.Total, existingJob.State), null);
                }
            }
        }

        var jobId = Guid.NewGuid().ToString("N");
        var job = new BatchJobRecord(
            jobId,
            BatchJobStates.Queued,
            DateTimeOffset.UtcNow,
            jobParams,
            string.IsNullOrWhiteSpace(request.IdempotencyKey) ? null : request.IdempotencyKey.Trim(),
            new BatchProgress(urls.Count, 0, 0, 0));

        store.InsertJob(job, urls);
        return (new BatchSubmitResponse(jobId, urls.Count, BatchJobStates.Queued), null);
    }

    public (BatchStatusResponse? Response, BatchFailureInfo? Error) GetStatus(string jobId)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return (null, new BatchFailureInfo("invalid_request", "job_id is required."));
        }

        var job = store.GetJob(jobId.Trim());
        if (job is null)
        {
            return (null, new BatchFailureInfo("job_not_found", "Job was not found."));
        }

        return (new BatchStatusResponse(job.Id, job.State, job.Progress), null);
    }

    public (BatchResultsResponse? Response, BatchFailureInfo? Error) GetResults(string jobId, int cursor, int limit)
    {
        if (string.IsNullOrWhiteSpace(jobId))
        {
            return (null, new BatchFailureInfo("invalid_request", "job_id is required."));
        }

        var job = store.GetJob(jobId.Trim());
        if (job is null)
        {
            return (null, new BatchFailureInfo("job_not_found", "Job was not found."));
        }

        limit = Math.Clamp(limit, 1, 200);
        cursor = Math.Max(0, cursor);
        var rows = store.GetItems(job.Id, cursor, limit + 1);
        int? nextCursor = null;
        if (rows.Count > limit)
        {
            nextCursor = cursor + limit;
            rows = rows.Take(limit).ToList();
        }

        var items = rows.Select(MapItemResult).ToArray();
        return (new BatchResultsResponse(job.Id, items, nextCursor), null);
    }

    internal static bool TryValidateSubmit(
        BatchSubmitRequest request,
        out IReadOnlyList<string> urls,
        out BatchJobParams jobParams,
        out BatchFailureInfo? error)
    {
        urls = [];
        jobParams = new BatchJobParams(
            "http_then_browser",
            null,
            null,
            true,
            null,
            Playbooks.PlaybookPolicy.Auto,
            "fail");
        error = null;

        if (request.Urls is null || request.Urls.Length == 0)
        {
            error = new BatchFailureInfo("invalid_request", "urls must contain at least one URL.");
            return false;
        }

        if (request.Urls.Length > BatchSettings.MaxUrls)
        {
            error = new BatchFailureInfo(
                "invalid_request",
                $"urls exceeds OCCAM_BATCH_MAX_URLS ({BatchSettings.MaxUrls}).");
            return false;
        }

        var normalized = new List<string>(request.Urls.Length);
        foreach (var raw in request.Urls)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                error = new BatchFailureInfo("invalid_request", "urls contains an empty entry.");
                return false;
            }

            var url = raw.Trim();
            var preflight = FetchPreflight.Prepare(url, request.SessionProfile);
            if (!preflight.Ok)
            {
                error = new BatchFailureInfo(
                    "invalid_request",
                    preflight.FailureMessage ?? $"Invalid URL: {url}");
                return false;
            }

            normalized.Add(url);
        }

        var backendPolicy = string.IsNullOrWhiteSpace(request.BackendPolicy)
            ? "http_then_browser"
            : request.BackendPolicy.Trim();
        if (!OccamBackendPolicyParser.TryParse(backendPolicy, out _))
        {
            error = new BatchFailureInfo(
                "invalid_request",
                "backend_policy must be http, browser, or http_then_browser.");
            return false;
        }

        if (!OccamTranscodeOptionsParser.TryBuild(
                request.MaxTokens,
                request.FitMarkdown ?? true,
                request.FocusQuery,
                content_selectors: null,
                request.SessionProfile,
                request.PlaybookPolicy ?? Playbooks.PlaybookPolicy.Auto,
                if_none_match: null,
                out var options,
                out var optionsError))
        {
            error = new BatchFailureInfo("invalid_request", optionsError ?? "Invalid transcode options.");
            return false;
        }

        urls = normalized;
        var onOversize = string.IsNullOrWhiteSpace(request.OnOversize)
            ? "fail"
            : request.OnOversize.Trim().ToLowerInvariant();
        if (onOversize is not "fail" and not "partial")
        {
            error = new BatchFailureInfo("invalid_request", "on_oversize must be fail or partial.");
            return false;
        }

        jobParams = new BatchJobParams(
            backendPolicy,
            options.FocusQuery,
            options.MaxTokens,
            options.FitMarkdown,
            options.SessionProfile,
            options.PlaybookPolicy,
            onOversize);
        return true;
    }

    private static BatchItemResult MapItemResult(BatchJobItemRecord row)
    {
        if (row.State == BatchItemStates.Done && row.ResultJson is not null)
        {
            var parsed = System.Text.Json.JsonSerializer.Deserialize(row.ResultJson, BatchJsonContext.Default.BatchItemResult);
            if (parsed is not null)
            {
                return parsed;
            }
        }

        var ok = row.State == BatchItemStates.Done;
        BatchFailureInfo? failure = null;
        if (!ok)
        {
            failure = new BatchFailureInfo(
                row.FailureCode ?? "extraction_failed",
                row.FailureMessage ?? "Extract failed.");
        }

        return new BatchItemResult(row.Url, ok, null, null, 0, failure);
    }
}
