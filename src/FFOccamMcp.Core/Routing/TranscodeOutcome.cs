using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Routing;

public sealed record TranscodeOutcome(
    bool Ok,
    string? Markdown,
    string? FinalUrl,
    string? Backend,
    string? FailureCode,
    string? Message,
    int LatencyMs = 0,
    int? TokensEstimated = null,
    bool Truncated = false,
    string? TruncationStrategy = null,
    // #7 omitted-manifest: what max_tokens budgeting dropped from the returned markdown. Null unless
    // truncation occurred (or structured sidecars were trimmed under the shared budget).
    Compile.OmittedManifest? Omitted = null,
    // Unified response budget: factual per-bucket spend when max_tokens was set. Null when uncapped.
    Compile.ResponseBudgetAllocation? Budget = null,
    int StatusCode = 0,
    Session.SessionProfileInfo? Session = null,
    IReadOnlyList<Workers.MediaRefInfo>? MediaRefs = null,
    double Confidence = 0.0,
    // ADR-0004 EQM breakdown (success path). Null on failures / when not evaluated.
    PostProcessors.ExtractQualityEvaluator.ExtractQualityReport? Quality = null,
    IReadOnlyList<Workers.WorkerExtractChunkInfo>? Chunks = null,
    IReadOnlyList<Workers.WorkerExtractBlockInfo>? Blocks = null,
    IReadOnlyList<Workers.WorkerExtractTableInfo>? Tables = null,
    Workers.WorkerExtractFeedInfo? Feed = null,
    Workers.WorkerExtractMetaInfo? Meta = null,
    string? Screenshot = null,
    int WorkerNetworkMs = 0,
    int WorkerParseMs = 0,
    TranscodeTimings? Timings = null,
    // Receipt v1 provenance: the winning playbook tier applied (null when none resolved).
    string? PlaybookId = null,
    string? PlaybookVersion = null,
    // Browser-availability remedy (playwright_missing): why + how to fix. Null for other outcomes.
    string? Reason = null,
    Workers.WorkerExtractFixInfo? Fix = null,
    // Branch-2: set on success when this call auto-provisioned the browser binary.
    Workers.WorkerBrowserProvisionedInfo? BrowserProvisioned = null,
    // Per-attempt log of the http→browser cascade (null for single-backend policies). The router
    // populates this; OccamTranscodeTool maps it to the response's recovery[] field.
    IReadOnlyList<TranscodeAttempt>? Recovery = null,
    // A3: true when the worker confirmed a playbook overlay actually matched the host and shaped the
    // extract. TranscodePipeline stamps PlaybookId/PlaybookVersion only when this is true.
    bool OverlayApplied = false,
    // PR-C internal access evidence/decision. Public dimensioned fields are deferred to PR-F.
    Workers.WorkerAccessEvidenceInfo? Access = null,
    OccamMcp.Core.Access.AccessAssessment? AccessAssessment = null,
    // PR-E internal planning truth; public additive mapping is owned by PR-F.
    Knowledge.MaterializationAssessment? MaterializationAssessment = null,
    Compile.ResponseBudgetDiagnostics? BudgetDiagnostics = null);

/// <summary>
/// One backend attempt inside the router's http→browser cascade (recovery log entry).
/// <see cref="Ok"/> remains the legacy transport/extract-completion alias for <see cref="TransportOk"/>;
/// usability, failure, and escalation are independent dimensions (INV-9 / PR-F).
/// </summary>
public sealed record TranscodeAttempt(
    string Backend,
    bool Ok,
    int LatencyMs,
    bool TransportOk,
    bool Usable,
    string? FailureCode = null,
    string? EscalationReason = null)
{
    /// <summary>RC.1-compatible constructor: <paramref name="ok"/> means transport completion only.</summary>
    public TranscodeAttempt(string backend, bool ok, int latencyMs)
        : this(backend, ok, latencyMs, TransportOk: ok, Usable: ok)
    {
    }

    public static TranscodeAttempt Create(
        string backend,
        bool transportOk,
        int latencyMs,
        bool usable,
        string? failureCode = null,
        string? escalationReason = null) =>
        new(backend, transportOk, latencyMs, transportOk, usable, failureCode, escalationReason);
}

/// <summary>
/// Per-stage wall-clock breakdown of a transcode, surfaced so an agent can see where time goes:
/// the network leg (with-internet) vs the CPU leg (without-internet) inside the worker, plus the
/// host-side pipeline stages. All values are milliseconds.
/// </summary>
public sealed record TranscodeTimings(
    int TotalMs,
    int PreflightMs,
    int RouteMs,
    int NetworkMs,
    int ParseMs,
    int PostProcessMs,
    int CompileMs);

public sealed record TranscodeContext(
    string Url,
    OccamBackendPolicy Policy,
    OccamTranscodeOptions Options,
    Session.SessionProfileInfo? Session = null);

public static class TranscodeOutcomeMapper
{
    public static TranscodeOutcome FromExtractRun(ExtractRunResult result)
    {
        if (result.TimedOut)
        {
            return new TranscodeOutcome(
                false,
                null,
                result.FinalUrl,
                result.Backend,
                "timeout",
                FailureCodeStrings.FormatTranscodeMessage("timeout", 0),
                result.LatencyMs,
                StatusCode: result.StatusCode,
                Chunks: result.Chunks,
                Blocks: result.Blocks,
                Tables: result.Tables,
                Feed: result.Feed,
                Meta: result.Meta,
                Screenshot: result.Screenshot,
                WorkerNetworkMs: result.NetworkMs,
                WorkerParseMs: result.ParseMs,
                BrowserProvisioned: result.BrowserProvisioned);
        }

        if (!result.Ok || string.IsNullOrWhiteSpace(result.Markdown))
        {
            var statusCode = ResolveStatusCode(result);
            var failure = FailureCodeStrings.ResolveTranscodeFailure(result.Failure, statusCode);
            return new TranscodeOutcome(
                false,
                result.Markdown,
                result.FinalUrl,
                result.Backend,
                failure,
                FailureCodeStrings.FormatTranscodeMessage(failure, statusCode, result.Failure),
                result.LatencyMs,
                StatusCode: statusCode,
                Chunks: result.Chunks,
                Blocks: result.Blocks,
                Tables: result.Tables,
                Feed: result.Feed,
                Meta: result.Meta,
                Screenshot: result.Screenshot,
                WorkerNetworkMs: result.NetworkMs,
                WorkerParseMs: result.ParseMs,
                Reason: result.Reason,
                Fix: result.Fix,
                BrowserProvisioned: result.BrowserProvisioned,
                Access: result.Access);
        }

        return new TranscodeOutcome(
            true,
            result.Markdown,
            result.FinalUrl,
            result.Backend,
            null,
            null,
            result.LatencyMs,
            StatusCode: result.StatusCode,
            MediaRefs: result.MediaRefs,
            Chunks: result.Chunks,
            Blocks: result.Blocks,
            Tables: result.Tables,
            Feed: result.Feed,
            Meta: result.Meta,
            Screenshot: result.Screenshot,
            WorkerNetworkMs: result.NetworkMs,
            WorkerParseMs: result.ParseMs,
            BrowserProvisioned: result.BrowserProvisioned,
            OverlayApplied: result.OverlayApplied,
            Access: result.Access);
    }

    private static int ResolveStatusCode(ExtractRunResult result)
    {
        if (result.StatusCode > 0)
        {
            return result.StatusCode;
        }

        return FailureCodeStrings.TryParseHttpStatusCode(result.Failure);
    }
}
