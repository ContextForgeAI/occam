using System.Diagnostics;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Codecs;
using OccamMcp.Core.Knowledge;
using OccamMcp.Core.Knowledge.Extraction;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Session;
using OccamMcp.Core.Telemetry;

namespace OccamMcp.Core.Routing;

public sealed class TranscodePipeline(
    OccamRouter router,
    IEnumerable<ITranscodePostProcessor> postProcessors,
    IOccamTelemetrySink telemetry,
    PlaybookSeedResolver playbookSeedResolver,
    Services.IRobotsThrottleService robotsThrottle,
    KnowledgeCodecRegistry codecRegistry,
    MaterializationPlanner materializationPlanner)
{
    private readonly ITranscodePostProcessor[] _postProcessors =
        postProcessors.OrderBy(p => p.Order).ToArray();

    public ValueTask<TranscodeOutcome> TranscodeAsync(string url, OccamBackendPolicy policy, CancellationToken cancellationToken) =>
        TranscodeAsync(url, policy, OccamTranscodeOptions.Default, cancellationToken);

    public async ValueTask<TranscodeOutcome> TranscodeAsync(
        string url,
        OccamBackendPolicy policy,
        OccamTranscodeOptions options,
        CancellationToken cancellationToken)
    {
        var featuresList = new List<string>();
        if (options.SemanticChunking)
        {
            featuresList.Add("semantic_chunking");
        }
        if (options.CaptureScreenshot)
        {
            featuresList.Add("screenshot");
        }

        // Always request internal structural IR for Canonical/Planner. Public response still omits
        // blocks/tables unless the caller opted in (json_blocks / json_tables / diff_against).
        featuresList.Add("json_blocks");
        featuresList.Add("json_tables");

        if (options.JsonFeed)
        {
            featuresList.Add("json_feed");
        }
        string? features = featuresList.Count > 0 ? string.Join(",", featuresList) : null;

        using (OccamFeaturesScope.Push(features))
        {
            if (PlaybookPolicy.ShouldApply(options.PlaybookPolicy))
            {
                var resolved = playbookSeedResolver.ResolveExtended(new PlaybookResolveOptions(url));
                var effectivePolicy = ResolveEffectiveBackendPolicy(policy, resolved.PreferredBackend);
                if (resolved.Ok && !string.IsNullOrWhiteSpace(resolved.RawWinningPlaybookJson))
                {
                    // Soft overlay: ship the auto-resolved genome to the worker (selectors +
                    // postMarkdown) but keep the Readability fallback — don't force selector-only.
                    using (PlaybookVerifyScope.Push(resolved.RawWinningPlaybookJson, strict: false))
                    {
                        var outcome = await TranscodeCoreAsync(url, effectivePolicy, options, cancellationToken).ConfigureAwait(false);
                        // Receipt v1 provenance: stamp the winning tier ONLY when the worker confirmed the
                        // overlay actually matched the host and shaped the extract (A3). Stamping merely
                        // because the overlay was pushed produced false provenance whenever it silently
                        // didn't reach/match the worker (e.g. the old browser-daemon drop).
                        return outcome.OverlayApplied
                            ? outcome with
                            {
                                PlaybookId = resolved.PlaybookId,
                                PlaybookVersion = resolved.SchemaVersion,
                            }
                            : outcome;
                    }
                }
            }

            return await TranscodeCoreAsync(url, policy, options, cancellationToken).ConfigureAwait(false);
        }
    }

    private static OccamBackendPolicy ResolveEffectiveBackendPolicy(
        OccamBackendPolicy requested,
        string? playbookPreferredBackend)
    {
        if (string.IsNullOrWhiteSpace(playbookPreferredBackend))
        {
            return requested;
        }

        if (requested != OccamBackendPolicy.HttpThenBrowser)
        {
            return requested;
        }

        return OccamBackendPolicyParser.TryParse(playbookPreferredBackend, out var parsed)
            ? parsed
            : requested;
    }

    private async ValueTask<TranscodeOutcome> TranscodeCoreAsync(
        string url,
        OccamBackendPolicy policy,
        OccamTranscodeOptions options,
        CancellationToken cancellationToken)
    {
        var focusIntent = Compile.FocusIntent.FromUrl(url);
        var fetchUrl = focusIntent.FetchUrl;
        options = options with { FocusFragment = focusIntent.Fragment };
        var started = Stopwatch.GetTimestamp();
        var preflight = FetchPreflight.Prepare(fetchUrl, options.SessionProfile);
        if (!preflight.Ok)
        {
            return new TranscodeOutcome(
                false,
                null,
                null,
                null,
                preflight.FailureCode,
                preflight.FailureMessage,
                0);
        }
        var preflightMs = ElapsedMs(started);

        // Opt-in polite fetch (env-gated; no-op by default): honor robots.txt Disallow and apply a
        // per-host throttle/crawl-delay before dispatching to a backend.
        var robotsFailure = robotsThrottle.CheckAndThrottle(fetchUrl, cancellationToken);
        if (robotsFailure is not null)
        {
            return new TranscodeOutcome(
                false,
                null,
                null,
                null,
                robotsFailure,
                "Fetch disallowed by the site's robots.txt (OCCAM_RESPECT_ROBOTS=1).",
                ElapsedMs(started));
        }

        using (preflight.HeadersScope)
        {
            var ctx = new TranscodeContext(fetchUrl, policy, options, preflight.Session);
            var routeStarted = Stopwatch.GetTimestamp();
            var outcome = await router.TranscodeAsync(fetchUrl, policy, cancellationToken).ConfigureAwait(false);
            var routeMs = ElapsedMs(routeStarted);

            var postStarted = Stopwatch.GetTimestamp();
            foreach (var processor in _postProcessors)
            {
                outcome = processor.Process(outcome, ctx);
            }
            var postProcessMs = ElapsedMs(postStarted);

            if (!outcome.Ok)
            {
                OccamLogger.TryWriteStageBreakdown(
                    outcome.Backend ?? "http",
                    preflightMs,
                    routeMs,
                    postProcessMs,
                    compileMs: 0);
                telemetry.OnTranscodeFailed(ctx, outcome);
                return outcome with
                {
                    Timings = new TranscodeTimings(
                        ElapsedMs(started), preflightMs, routeMs,
                        outcome.WorkerNetworkMs, outcome.WorkerParseMs, postProcessMs, 0),
                };
            }

            var compileStarted = Stopwatch.GetTimestamp();
            var compiled = FinishMaterialize(
                outcome,
                ctx,
                fetchUrl);
            var compileMs = ElapsedMs(compileStarted);

            OccamLogger.TryWriteStageBreakdown(
                compiled.Backend ?? "http",
                preflightMs,
                routeMs,
                postProcessMs,
                compileMs);

            var timings = new TranscodeTimings(
                ElapsedMs(started), preflightMs, routeMs,
                outcome.WorkerNetworkMs, outcome.WorkerParseMs, postProcessMs, compileMs);

            if (compiled.Ok)
            {
                telemetry.OnTranscodeCompleted(ctx, compiled);
            }
            else
            {
                telemetry.OnTranscodeFailed(ctx, compiled);
            }

            // FinishMaterialize builds a fresh outcome, so re-attach fields dropped by that rebuild: the
            // router's cascade recovery log (flagship recovery[]), the worker's overlay_applied signal
            // (A3 — gates the honest playbook provenance stamp in Transcode), and PR-C/PR-F access truth.
            return compiled with
            {
                Session = preflight.Session,
                Timings = timings,
                Recovery = outcome.Recovery,
                OverlayApplied = outcome.OverlayApplied,
                Access = outcome.Access ?? compiled.Access,
                AccessAssessment = outcome.AccessAssessment ?? compiled.AccessAssessment,
            };
        }
    }

    private static int ElapsedMs(long started) =>
        (int)Math.Round(Stopwatch.GetElapsedTime(started).TotalMilliseconds);

    /// <summary>
    /// Live path: Extraction → Canonical bundle → MaterializationPlanner → View → Codec → Output.
    /// Semantic selection lives in the planner; the codec only serializes the completed view.
    /// </summary>
    private TranscodeOutcome FinishMaterialize(
        TranscodeOutcome raw,
        TranscodeContext ctx,
        string requestUrl)
    {
        var extractBackend = raw.Backend ?? "http";
        var latencyMs = raw.LatencyMs;
        var finalUrl = raw.FinalUrl ?? requestUrl;
        var blocks = raw.Blocks;
        var tables = raw.Tables;
        var chunks = raw.Chunks;
        var mediaRefs = raw.MediaRefs;
        var feed = raw.Feed;
        var meta = raw.Meta;
        var screenshot = raw.Screenshot;
        var browserProvisioned = raw.BrowserProvisioned;

        // R6 BudgetOwnership: public max_tokens = whole-response; planner gets surface cap only.
        // See Compile.BudgetOwnership / docs-internal/BUDGET-OWNERSHIP.md.
        var request = MaterializationRequest.FromTranscodeOptions(ctx.Options);
        var rawSidecars = new Compile.ResponseBudgetSidecars(
            blocks, tables, chunks, mediaRefs, feed, screenshot, ExpectReceipt: true);
        // INV-7: allocation sees only fields that this request can serialize.
        var projectedInventory = Compile.ResponseProjection.Project(rawSidecars, request);
        var budgetPrepared = Compile.BudgetOwnership.PrepareSurfaceBudget(
            ctx.Options.MaxTokens,
            projectedInventory);
        request = Compile.BudgetOwnership.ApplyToRequest(request, budgetPrepared);
        var budgetCaps = budgetPrepared.Caps;

        var bundle = ExtractedKnowledgeAdapter.TryAdapt(requestUrl, raw)
            ?? new ExtractedKnowledgeBundle(
                SourceSurface.Markdown(raw.Markdown ?? string.Empty),
                WorkerKnowledgeMapper.FromExtract(blocks, tables),
                Canonical: null,
                FinalUrl: finalUrl,
                Backend: extractBackend,
                Blocks: blocks,
                Tables: tables,
                Chunks: chunks,
                MediaRefs: mediaRefs,
                Feed: feed,
                Meta: meta,
                Screenshot: screenshot,
                BrowserProvisioned: browserProvisioned);

        var planned = materializationPlanner.Plan(request, bundle);

        // Resolve codec via fail-closed selector (null id → configured default). No public MCP codec param.
        var selection = KnowledgeCodecSelector.Select(codecRegistry, requestedCodecId: null);
        var codec = selection.Ok && selection.Codec is not null
            ? selection.Codec
            : codecRegistry.Default;
        var encodedMarkdown = codec.Encode(planned.View, KnowledgeCodecEncodeOptions.None).Surface;

        if (!planned.SelectorsMatched && ctx.Options.ContentSelectors.Length > 0)
        {
            return new TranscodeOutcome(
                false,
                null,
                finalUrl,
                extractBackend,
                "content_selectors_miss",
                "None of the content_selectors matched any section.",
                latencyMs,
                Chunks: ProjectChunks(chunks, request),
                Blocks: ProjectBlocks(blocks, request),
                Tables: ProjectTables(tables, request),
                Feed: feed,
                Meta: meta,
                Screenshot: screenshot,
                BrowserProvisioned: browserProvisioned,
                Access: raw.Access,
                AccessAssessment: raw.AccessAssessment);
        }

        if (string.IsNullOrWhiteSpace(encodedMarkdown))
        {
            return new TranscodeOutcome(
                false,
                null,
                finalUrl,
                extractBackend,
                "thin_extract",
                "Compiled markdown is empty after prune or selectors.",
                latencyMs,
                Chunks: ProjectChunks(chunks, request),
                Blocks: ProjectBlocks(blocks, request),
                Tables: ProjectTables(tables, request),
                Feed: feed,
                Meta: meta,
                Screenshot: screenshot,
                BrowserProvisioned: browserProvisioned,
                Access: raw.Access,
                AccessAssessment: raw.AccessAssessment);
        }

        // SI-02 integrity: the planner may have pruned the surface (max_tokens / fit_markdown /
        // content_selectors / truncation); keep only the blocks whose text survived, so blocks[],
        // blockMerkleRoot, blockLeaves and contentHash all describe the SAME returned content.
        var sourceMarkdown = raw.Markdown ?? string.Empty;
        var reconciledBlocks = Compile.BlockReconciler.SurvivingBlocks(blocks, encodedMarkdown, sourceMarkdown);
        // Focus-aware structured budget: among genuine survivors, prefer salience over DOM order /
        // accidental literal matches (version notes must not displace relevant blocks).
        var focusBlocks = Compile.BlockReconciler.PrioritizeForFocus(reconciledBlocks, ctx.Options.FocusQuery);
        if (ctx.Options.FitMarkdown
            && !string.IsNullOrWhiteSpace(ctx.Options.FocusQuery)
            && focusBlocks is { Count: > 0 }
            && Compile.BlockReconciler.CountFocusRelevant(focusBlocks, ctx.Options.FocusQuery) == 0)
        {
            // Focus prune kept markdown but only boilerplate blocks survived literal reconcile —
            // do not ship version-notes as the sole structured evidence.
            focusBlocks = Array.Empty<Workers.WorkerExtractBlockInfo>();
        }

        Compile.ResponseBudgetAllocation? budget = null;
        Compile.OmittedManifest? omitted = planned.Omitted;
        var tokensEstimated = planned.TokensEstimated;
        var truncated = planned.Truncated;
        var truncationStrategy = planned.TruncationStrategy;
        var finalProjection = Compile.ResponseProjection.Project(
            new Compile.ResponseBudgetSidecars(
                focusBlocks, tables, chunks, mediaRefs, feed, screenshot, ExpectReceipt: true),
            request);
        var outBlocks = finalProjection.Blocks;
        var outTables = finalProjection.Tables;
        var outChunks = finalProjection.Chunks;
        var outMedia = finalProjection.MediaRefs;
        var outFeed = finalProjection.Feed;
        var outScreenshot = finalProjection.Screenshot;
        Compile.ResponseBudgetDiagnostics? budgetDiagnostics = null;

        if (budgetPrepared.IsCapped && budgetCaps is not null && budgetPrepared.WholeResponseMaxTokens is int totalBudget)
        {
            var trim = Compile.ResponseBudgetPlanner.TrimStructured(
                totalBudget,
                budgetCaps,
                encodedMarkdown,
                finalProjection);

            outBlocks = trim.Blocks;
            outTables = trim.Tables;
            outChunks = trim.Chunks;
            outMedia = trim.MediaRefs;
            outFeed = trim.Feed;
            outScreenshot = trim.Screenshot;
            budget = trim.Allocation;
            tokensEstimated = trim.Allocation.Total;
            var structuredTokens = trim.Allocation.Blocks + trim.Allocation.Tables
                + trim.Allocation.Chunks + trim.Allocation.Media + trim.Allocation.Feed;
            budgetDiagnostics = new Compile.ResponseBudgetDiagnostics(
                totalBudget,
                trim.Allocation.Total,
                trim.Allocation.Markdown,
                structuredTokens,
                trim.Allocation.Receipt,
                planned.Assessment?.PlannerRetries ?? 0,
                planned.Assessment?.SelectedAnswerUnitTokens,
                planned.Assessment?.Completeness ?? Knowledge.MaterializationCompleteness.Complete);

            outBlocks = Compile.BlockReconciler.DropFocusIrrelevantKeepers(
                outBlocks, focusBlocks, ctx.Options.FocusQuery, out var droppedIrrelevant);
            if (droppedIrrelevant > 0)
            {
                truncated = true;
                truncationStrategy ??= "response_budget";
                var prior = trim.Dropped;
                trim = trim with
                {
                    Blocks = outBlocks,
                    Dropped = prior with { Blocks = prior.Blocks + droppedIrrelevant },
                    StructuredTrimmed = true,
                };
            }

            if (trim.StructuredTrimmed)
            {
                truncated = true;
                truncationStrategy ??= "response_budget";
                omitted = MergeStructuredOmitted(omitted, trim.Dropped);
            }
        }

        // Hide internally-fetched IR from the public envelope unless the caller opted in.
        outBlocks = ProjectBlocks(outBlocks, request);
        outTables = ProjectTables(outTables, request);

        var outcome = new TranscodeOutcome(
            true,
            encodedMarkdown,
            finalUrl,
            extractBackend,
            null,
            null,
            latencyMs,
            TokensEstimated: tokensEstimated,
            Truncated: truncated,
            TruncationStrategy: truncationStrategy,
            Omitted: omitted,
            Budget: budget,
            MediaRefs: outMedia,
            Chunks: outChunks,
            Blocks: outBlocks,
            Tables: outTables,
            Feed: outFeed,
            Meta: meta,
            Screenshot: outScreenshot,
            BrowserProvisioned: browserProvisioned,
            MaterializationAssessment: planned.Assessment,
            BudgetDiagnostics: budgetDiagnostics,
            Access: raw.Access,
            AccessAssessment: raw.AccessAssessment);

        var quality = ExtractQualityEvaluator.EvaluateOutcome(outcome);
        var confidence = ExtractQualityEvaluator.ComputeConfidence(outcome);
        return outcome with { Confidence = confidence, Quality = quality };
    }

    private static IReadOnlyList<Workers.WorkerExtractBlockInfo>? ProjectBlocks(
        IReadOnlyList<Workers.WorkerExtractBlockInfo>? blocks,
        MaterializationRequest request) =>
        request.ExposePublicBlocks ? blocks : null;

    private static IReadOnlyList<Workers.WorkerExtractTableInfo>? ProjectTables(
        IReadOnlyList<Workers.WorkerExtractTableInfo>? tables,
        MaterializationRequest request) =>
        request.ExposePublicTables ? tables : null;

    private static IReadOnlyList<Workers.WorkerExtractChunkInfo>? ProjectChunks(
        IReadOnlyList<Workers.WorkerExtractChunkInfo>? chunks,
        MaterializationRequest request) =>
        chunks;

    private static Compile.OmittedManifest MergeStructuredOmitted(
        Compile.OmittedManifest? markdownOmitted,
        Compile.ResponseBudgetDropped dropped)
    {
        if (markdownOmitted is not null)
        {
            return markdownOmitted with { Structured = dropped };
        }

        // Structured-only trim: markdown fit its cap, but sidecars were cut.
        return new Compile.OmittedManifest(
            "response_budget",
            TokensDropped: 0,
            Regions: ["structured"],
            SectionsOmitted: null,
            Structured: dropped);
    }
}
