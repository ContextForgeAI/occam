using System.Diagnostics;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Cascade;

/// <summary>Production adapter: playbook resolver + <see cref="TranscodePipeline"/> + content hash.</summary>
public sealed class LiveCascadeExtractBackend(
    PlaybookSeedResolver playbookSeedResolver,
    TranscodePipeline pipeline,
    WorkerPaths workerPaths) : ICascadeExtractBackend
{
    public bool ReceiptsEnabled => ReceiptsPolicy.Enabled();

    public PlaybookSeedResolveResult ResolvePlaybook(string url) =>
        playbookSeedResolver.ResolveExtended(new PlaybookResolveOptions(url));

    public async ValueTask<CascadeExtractAttempt> ExtractAsync(
        string url,
        OccamBackendPolicy policy,
        OccamTranscodeOptions options,
        CancellationToken cancellationToken)
    {
        if (!workerPaths.IsConfigured)
        {
            return new CascadeExtractAttempt(
                false,
                null,
                null,
                "workers_unavailable",
                "OCCAM_HOME workers not configured. Run occam doctor.");
        }

        var outcome = await pipeline.TranscodeAsync(url, policy, options, cancellationToken).ConfigureAwait(false);
        if (!outcome.Ok)
        {
            return new CascadeExtractAttempt(
                false,
                null,
                outcome.Backend,
                outcome.FailureCode,
                outcome.Message,
                outcome.PlaybookId);
        }

        var markdown = outcome.Markdown ?? string.Empty;
        return new CascadeExtractAttempt(
            true,
            markdown,
            outcome.Backend,
            null,
            null,
            outcome.PlaybookId,
            ContentHashToken.BareHex(markdown));
    }

    public string? ComputeContentHash(string markdown) =>
        string.IsNullOrEmpty(markdown) ? null : ContentHashToken.BareHex(markdown);
}

/// <summary>
/// Orchestrates playbook → HTTP → browser → focus/budget → receipt with per-step timeouts and
/// graceful degradation. Partial success is honest: <c>ok:true</c> with <c>partial:true</c> when
/// later stages were omitted but a usable body remains.
/// </summary>
public sealed class CascadeService(ICascadeExtractBackend backend)
{
    private const int MinBudgetTokens = 128;

    public async Task<CascadeResult> RunAsync(CascadeRequest request, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var mode = NormalizeMode(request.Mode);
        var timeouts = request.Timeouts ?? CascadeTimeouts.Default;
        var steps = new List<CascadeStep>(5);
        var omitted = new List<string>();
        var url = (request.Url ?? string.Empty).Trim();

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return Fail(
                url.Length == 0 ? "(empty)" : url,
                "invalid_arguments",
                "url must be an absolute http(s) URL.",
                mode,
                steps,
                omitted);
        }

        using var totalCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (timeouts.TotalMs > 0)
        {
            totalCts.CancelAfter(timeouts.TotalMs);
        }

        var totalToken = totalCts.Token;
        string? playbookId = null;
        var playbookPolicy = PlaybookPolicy.Auto;

        // --- 1. Playbook resolve (soft-fail) ---
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(totalToken);
                stepCts.CancelAfter(Math.Max(1, timeouts.PlaybookMs));
                // Resolve is CPU/disk; run on thread pool so CancelAfter can interrupt waits.
                var resolved = await Task.Run(() => backend.ResolvePlaybook(url), stepCts.Token).ConfigureAwait(false);
                sw.Stop();
                if (resolved.Ok && !string.IsNullOrWhiteSpace(resolved.PlaybookId))
                {
                    playbookId = resolved.PlaybookId;
                    steps.Add(new CascadeStep(
                        CascadeStepKind.PlaybookResolve,
                        CascadeStepStatus.Ok,
                        (int)sw.ElapsedMilliseconds,
                        playbookId));
                }
                else
                {
                    playbookPolicy = PlaybookPolicy.Off;
                    steps.Add(new CascadeStep(
                        CascadeStepKind.PlaybookResolve,
                        CascadeStepStatus.Skipped,
                        (int)sw.ElapsedMilliseconds,
                        resolved.Ok ? "no_matching_playbook" : (resolved.Message ?? "resolve_miss")));
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                playbookPolicy = PlaybookPolicy.Off;
                Omit(steps, omitted, CascadeStepKind.PlaybookResolve, (int)sw.ElapsedMilliseconds, "timeout");
            }
        }

        if (totalToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
        {
            return Fail(url, "timeout", "cascade total budget exhausted before extract.", mode, steps, omitted);
        }

        // --- Focus / budget mapping (applied to extract options; recorded as its own step) ---
        var focusQuery = string.IsNullOrWhiteSpace(request.Task) ? null : request.Task.Trim();
        int? maxTokens = null;
        if (request.Budget is int budget)
        {
            if (budget < MinBudgetTokens)
            {
                return Fail(
                    url,
                    "invalid_arguments",
                    $"budget must be at least {MinBudgetTokens} tokens.",
                    mode,
                    steps,
                    omitted);
            }

            maxTokens = budget;
        }

        var fit = focusQuery is not null;
        {
            var sw = Stopwatch.StartNew();
            if (focusQuery is null && maxTokens is null)
            {
                sw.Stop();
                steps.Add(new CascadeStep(
                    CascadeStepKind.FocusBudget,
                    CascadeStepStatus.Skipped,
                    (int)sw.ElapsedMilliseconds,
                    "no_task_or_budget"));
            }
            else if (timeouts.FocusMs <= 0 || totalToken.IsCancellationRequested)
            {
                sw.Stop();
                Omit(steps, omitted, CascadeStepKind.FocusBudget, (int)sw.ElapsedMilliseconds, "timeout");
                focusQuery = null;
                maxTokens = null;
                fit = false;
            }
            else
            {
                sw.Stop();
                var detail = focusQuery is null
                    ? $"budget={maxTokens}"
                    : maxTokens is null
                        ? $"task={focusQuery}"
                        : $"task={focusQuery};budget={maxTokens}";
                steps.Add(new CascadeStep(
                    CascadeStepKind.FocusBudget,
                    CascadeStepStatus.Ok,
                    (int)sw.ElapsedMilliseconds,
                    detail));
            }
        }

        var options = new OccamTranscodeOptions
        {
            MaxTokens = maxTokens,
            FitMarkdown = fit,
            FocusQuery = focusQuery,
            PlaybookPolicy = playbookPolicy,
        };

        CascadeExtractAttempt? body = null;

        // --- 2. HTTP extract ---
        {
            var sw = Stopwatch.StartNew();
            try
            {
                using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(totalToken);
                stepCts.CancelAfter(Math.Max(1, timeouts.HttpMs));
                var attempt = await backend.ExtractAsync(url, OccamBackendPolicy.Http, options, stepCts.Token)
                    .ConfigureAwait(false);
                sw.Stop();
                if (attempt.Ok)
                {
                    body = attempt;
                    steps.Add(new CascadeStep(
                        CascadeStepKind.HttpExtract,
                        CascadeStepStatus.Ok,
                        (int)sw.ElapsedMilliseconds,
                        attempt.Backend ?? "http"));
                }
                else
                {
                    steps.Add(new CascadeStep(
                        CascadeStepKind.HttpExtract,
                        CascadeStepStatus.Failed,
                        (int)sw.ElapsedMilliseconds,
                        attempt.FailureCode ?? "extraction_failed"));
                }
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                sw.Stop();
                Omit(steps, omitted, CascadeStepKind.HttpExtract, (int)sw.ElapsedMilliseconds, "timeout");
            }
        }

        // --- 3. Browser fallback when HTTP did not produce a body ---
        if (body is null)
        {
            if (totalToken.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                Omit(steps, omitted, CascadeStepKind.BrowserFallback, 0, "total_budget");
            }
            else
            {
                var sw = Stopwatch.StartNew();
                try
                {
                    using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(totalToken);
                    stepCts.CancelAfter(Math.Max(1, timeouts.BrowserMs));
                    var attempt = await backend.ExtractAsync(url, OccamBackendPolicy.Browser, options, stepCts.Token)
                        .ConfigureAwait(false);
                    sw.Stop();
                    if (attempt.Ok)
                    {
                        body = attempt;
                        steps.Add(new CascadeStep(
                            CascadeStepKind.BrowserFallback,
                            CascadeStepStatus.Degraded,
                            (int)sw.ElapsedMilliseconds,
                            attempt.Backend ?? "browser"));
                    }
                    else
                    {
                        steps.Add(new CascadeStep(
                            CascadeStepKind.BrowserFallback,
                            CascadeStepStatus.Failed,
                            (int)sw.ElapsedMilliseconds,
                            attempt.FailureCode ?? "extraction_failed"));
                    }
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    Omit(steps, omitted, CascadeStepKind.BrowserFallback, (int)sw.ElapsedMilliseconds, "timeout");
                }
            }
        }
        else
        {
            steps.Add(new CascadeStep(
                CascadeStepKind.BrowserFallback,
                CascadeStepStatus.Skipped,
                0,
                "http_succeeded"));
        }

        // --- 4. Receipt / content hash ---
        string? contentHash = null;
        if (body is { Ok: true, Markdown: not null })
        {
            var sw = Stopwatch.StartNew();
            if (!backend.ReceiptsEnabled)
            {
                sw.Stop();
                contentHash = backend.ComputeContentHash(body.Markdown);
                steps.Add(new CascadeStep(
                    CascadeStepKind.Receipt,
                    CascadeStepStatus.Skipped,
                    (int)sw.ElapsedMilliseconds,
                    "receipts_disabled"));
            }
            else
            {
                try
                {
                    using var stepCts = CancellationTokenSource.CreateLinkedTokenSource(totalToken);
                    stepCts.CancelAfter(Math.Max(1, timeouts.ReceiptMs));
                    contentHash = await Task.Run(
                        () => body.ContentHash ?? backend.ComputeContentHash(body.Markdown),
                        stepCts.Token).ConfigureAwait(false);
                    sw.Stop();
                    steps.Add(new CascadeStep(
                        CascadeStepKind.Receipt,
                        CascadeStepStatus.Ok,
                        (int)sw.ElapsedMilliseconds,
                        contentHash is null ? "hash_unavailable" : "content_hash"));
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    sw.Stop();
                    Omit(steps, omitted, CascadeStepKind.Receipt, (int)sw.ElapsedMilliseconds, "timeout");
                }
            }
        }
        else
        {
            steps.Add(new CascadeStep(
                CascadeStepKind.Receipt,
                CascadeStepStatus.Skipped,
                0,
                "no_body"));
        }

        if (body is { Ok: true })
        {
            var partial = omitted.Count > 0;
            return new CascadeResult(
                Ok: true,
                Partial: partial,
                Url: url,
                Markdown: body.Markdown,
                FailureCode: null,
                FailureMessage: null,
                PlaybookId: body.PlaybookId ?? playbookId,
                BackendUsed: body.Backend,
                MaxTokensApplied: maxTokens,
                FocusQueryApplied: focusQuery,
                ContentHash: contentHash ?? body.ContentHash,
                Mode: mode,
                Steps: steps,
                Omitted: omitted);
        }

        var lastFail = steps.LastOrDefault(s =>
            s.Kind is CascadeStepKind.HttpExtract or CascadeStepKind.BrowserFallback
            && s.Status is CascadeStepStatus.Failed or CascadeStepStatus.Omitted);

        return Fail(
            url,
            lastFail?.Detail ?? "extraction_failed",
            "cascade produced no usable extract.",
            mode,
            steps,
            omitted);
    }

    private static string NormalizeMode(string? mode)
    {
        if (string.IsNullOrWhiteSpace(mode))
        {
            return "auto";
        }

        var m = mode.Trim().ToLowerInvariant();
        return m is "auto" or "advanced" ? m : "auto";
    }

    private static void Omit(
        List<CascadeStep> steps,
        List<string> omitted,
        CascadeStepKind kind,
        int durationMs,
        string detail)
    {
        var name = CascadeStepKindStrings.Format(kind);
        omitted.Add(name);
        steps.Add(new CascadeStep(kind, CascadeStepStatus.Omitted, durationMs, detail));
    }

    private static CascadeResult Fail(
        string url,
        string code,
        string message,
        string mode,
        List<CascadeStep> steps,
        List<string> omitted) =>
        new(
            Ok: false,
            Partial: false,
            Url: url,
            Markdown: null,
            FailureCode: code,
            FailureMessage: message,
            PlaybookId: null,
            BackendUsed: null,
            MaxTokensApplied: null,
            FocusQueryApplied: null,
            ContentHash: null,
            Mode: mode,
            Steps: steps,
            Omitted: omitted);
}
