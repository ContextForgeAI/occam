namespace OccamMcp.Core.Workers;

public sealed record ExtractRunResult(
    bool Ok,
    string? Markdown,
    string? Backend,
    string? Failure,
    int LatencyMs,
    string? FinalUrl,
    bool TimedOut,
    int StatusCode = 0,
    IReadOnlyList<MediaRefInfo>? MediaRefs = null,
    IReadOnlyList<WorkerExtractChunkInfo>? Chunks = null,
    IReadOnlyList<WorkerExtractBlockInfo>? Blocks = null,
    IReadOnlyList<WorkerExtractTableInfo>? Tables = null,
    WorkerExtractFeedInfo? Feed = null,
    WorkerExtractMetaInfo? Meta = null,
    string? Screenshot = null,
    int NetworkMs = 0,
    int ParseMs = 0,
    // Browser-availability remedy (playwright_missing): why + how to fix. Null for other failures.
    string? Reason = null,
    WorkerExtractFixInfo? Fix = null,
    // Branch-2: set on success when this call auto-provisioned the browser binary.
    WorkerBrowserProvisionedInfo? BrowserProvisioned = null,
    // A3: true when a playbook overlay actually matched this host and shaped the extract (honest
    // provenance — the receipt stamps PlaybookId only when this is true, not merely on overlay push).
    bool OverlayApplied = false,
    WorkerAccessEvidenceInfo? Access = null);
