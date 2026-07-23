using System.Text.Json.Serialization;

namespace OccamMcp.Core.Batch;

public static class BatchJobStates
{
    public const string Queued = "queued";
    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";
}

public static class BatchItemStates
{
    public const string Pending = "pending";
    public const string Running = "running";
    public const string Done = "done";
    public const string Failed = "failed";
}

public sealed record BatchSubmitRequest
{
    [JsonPropertyName("urls")]
    public string[]? Urls { get; init; }

    [JsonPropertyName("backend_policy")]
    public string? BackendPolicy { get; init; }

    [JsonPropertyName("focus_query")]
    public string? FocusQuery { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("fit_markdown")]
    public bool? FitMarkdown { get; init; }

    [JsonPropertyName("session_profile")]
    public string? SessionProfile { get; init; }

    [JsonPropertyName("playbook_policy")]
    public string? PlaybookPolicy { get; init; }

    [JsonPropertyName("idempotency_key")]
    public string? IdempotencyKey { get; init; }

    /// <summary>When HTTP body exceeds cap: <c>fail</c> (default) or <c>partial</c> (honest <c>response_truncated</c> + partial markdown).</summary>
    [JsonPropertyName("on_oversize")]
    public string? OnOversize { get; init; }
}

public sealed record BatchSubmitResponse(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("accepted_count")] int AcceptedCount,
    [property: JsonPropertyName("state")] string State);

public sealed record BatchProgress(
    [property: JsonPropertyName("total")] int Total,
    [property: JsonPropertyName("done")] int Done,
    [property: JsonPropertyName("failed")] int Failed,
    [property: JsonPropertyName("running")] int Running);

public sealed record BatchStatusResponse(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("state")] string State,
    [property: JsonPropertyName("progress")] BatchProgress Progress);

public sealed record BatchFailureInfo(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

public sealed record BatchItemResult(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("markdown")] string? Markdown,
    [property: JsonPropertyName("backend")] string? Backend,
    [property: JsonPropertyName("tokens_estimated")] int TokensEstimated,
    [property: JsonPropertyName("failure")] BatchFailureInfo? Failure);

public sealed record BatchResultsResponse(
    [property: JsonPropertyName("job_id")] string JobId,
    [property: JsonPropertyName("items")] BatchItemResult[] Items,
    [property: JsonPropertyName("next_cursor")] int? NextCursor);

public sealed record BatchErrorResponse(
    [property: JsonPropertyName("error")] BatchFailureInfo Error);

internal sealed record BatchJobParams(
    string BackendPolicy,
    string? FocusQuery,
    int? MaxTokens,
    bool FitMarkdown,
    string? SessionProfile,
    string PlaybookPolicy,
    string OnOversize = "fail");

internal sealed record BatchJobRecord(
    string Id,
    string State,
    DateTimeOffset SubmittedAt,
    BatchJobParams Params,
    string? IdempotencyKey,
    BatchProgress Progress);

internal sealed record BatchJobItemRecord(
    string JobId,
    int Seq,
    string Url,
    string State,
    string? ResultJson,
    string? FailureCode,
    string? FailureMessage);

internal sealed record PendingBatchItem(
    string JobId,
    int Seq,
    string Url,
    BatchJobParams Params);

public sealed record BatchHealthResponse(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("workers")] string Workers);

