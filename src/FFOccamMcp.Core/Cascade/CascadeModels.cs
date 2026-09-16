namespace OccamMcp.Core.Cascade;

/// <summary>Ordered stages of the cascade facade. Each may succeed, fail, be skipped, or be omitted under budget.</summary>
public enum CascadeStepKind
{
    PlaybookResolve = 0,
    HttpExtract = 1,
    BrowserFallback = 2,
    FocusBudget = 3,
    Receipt = 4,
}

/// <summary>Outcome of one cascade stage.</summary>
public enum CascadeStepStatus
{
    /// <summary>Stage completed and contributed to the result.</summary>
    Ok = 0,

    /// <summary>Stage was not applicable (e.g. receipts off, no task/budget).</summary>
    Skipped = 1,

    /// <summary>Stage was cut by a per-step or total timeout; later stages may still run.</summary>
    Omitted = 2,

    /// <summary>Stage ran and failed; cascade may degrade to a later stage.</summary>
    Failed = 3,

    /// <summary>Stage succeeded only after falling back (recorded on the fallback stage).</summary>
    Degraded = 4,
}

/// <summary>Wire spellings for <see cref="CascadeStepKind"/>.</summary>
public static class CascadeStepKindStrings
{
    public const string PlaybookResolve = "playbook_resolve";
    public const string HttpExtract = "http_extract";
    public const string BrowserFallback = "browser_fallback";
    public const string FocusBudget = "focus_budget";
    public const string Receipt = "receipt";

    public static string Format(CascadeStepKind kind) => kind switch
    {
        CascadeStepKind.PlaybookResolve => PlaybookResolve,
        CascadeStepKind.HttpExtract => HttpExtract,
        CascadeStepKind.BrowserFallback => BrowserFallback,
        CascadeStepKind.FocusBudget => FocusBudget,
        CascadeStepKind.Receipt => Receipt,
        _ => kind.ToString().ToLowerInvariant(),
    };
}

/// <summary>Wire spellings for <see cref="CascadeStepStatus"/>.</summary>
public static class CascadeStepStatusStrings
{
    public const string Ok = "ok";
    public const string Skipped = "skipped";
    public const string Omitted = "omitted";
    public const string Failed = "failed";
    public const string Degraded = "degraded";

    public static string Format(CascadeStepStatus status) => status switch
    {
        CascadeStepStatus.Ok => Ok,
        CascadeStepStatus.Skipped => Skipped,
        CascadeStepStatus.Omitted => Omitted,
        CascadeStepStatus.Failed => Failed,
        CascadeStepStatus.Degraded => Degraded,
        _ => status.ToString().ToLowerInvariant(),
    };
}

/// <summary>One recorded stage of a cascade run.</summary>
public sealed record CascadeStep(
    CascadeStepKind Kind,
    CascadeStepStatus Status,
    int DurationMs,
    string? Detail = null);

/// <summary>Per-stage and total wall-clock ceilings (milliseconds).</summary>
public sealed record CascadeTimeouts(
    int PlaybookMs = 2_000,
    int HttpMs = 35_000,
    int BrowserMs = 120_000,
    int FocusMs = 5_000,
    int ReceiptMs = 2_000,
    int TotalMs = 150_000)
{
    public static CascadeTimeouts Default { get; } = new();

    /// <summary>Tight ceilings for selftests and demos that must not wait on the network.</summary>
    public static CascadeTimeouts Fast { get; } = new(
        PlaybookMs: 50,
        HttpMs: 80,
        BrowserMs: 80,
        FocusMs: 50,
        ReceiptMs: 50,
        TotalMs: 250);
}

/// <summary>
/// Public cascade request. <c>mode=auto</c> runs the progressive ladder; <c>mode=advanced</c>
/// still extracts via the cascade but records that specialised tools remain the power surface.
/// </summary>
public sealed record CascadeRequest(
    string Url,
    string? Task = null,
    int? Budget = null,
    string Mode = "auto",
    CascadeTimeouts? Timeouts = null);

/// <summary>Structured cascade response — success, partial, or typed failure, always with a step log.</summary>
public sealed record CascadeResult(
    bool Ok,
    bool Partial,
    string Url,
    string? Markdown,
    string? FailureCode,
    string? FailureMessage,
    string? PlaybookId,
    string? BackendUsed,
    int? MaxTokensApplied,
    string? FocusQueryApplied,
    string? ContentHash,
    string Mode,
    IReadOnlyList<CascadeStep> Steps,
    IReadOnlyList<string> Omitted);

/// <summary>One extract attempt returned by <see cref="ICascadeExtractBackend"/>.</summary>
public sealed record CascadeExtractAttempt(
    bool Ok,
    string? Markdown,
    string? Backend,
    string? FailureCode,
    string? FailureMessage,
    string? PlaybookId = null,
    string? ContentHash = null);
