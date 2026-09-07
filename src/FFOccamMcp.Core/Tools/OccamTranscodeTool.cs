using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Caching;
using OccamMcp.Core.Handles;
using OccamMcp.Core.Json;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamTranscodeTool(
    WorkerPaths workerPaths,
    TranscodePipeline pipeline,
    OccamMcp.Core.Services.FeatureDiscoveryService featureDiscovery,
    OccamMcp.Core.Services.ITranslationService translationService,
    ITranscodeResponseCache responseCache,
    OccamMcp.Core.Receipts.ReceiptSigner receiptSigner,
    OccamMcp.Core.Receipts.TimeAnchorService timeAnchorService,
    OccamMcp.Core.Client.ClientCapabilityStore clientCapabilities,
    SourceHandleStore sourceHandles)
{
    /// <summary>An llms.txt shorter than this is treated as absent/placeholder; fall back to normal extract.</summary>
    private const int MinLlmsTxtLength = 32;

    /// <summary>Builds {scheme}://{authority}/llms.txt for an http(s) URL. False for non-http URLs.</summary>
    private static bool TryBuildLlmsTxtUrl(string url, out string llmsTxtUrl)
    {
        llmsTxtUrl = string.Empty;
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps))
        {
            return false;
        }

        llmsTxtUrl = $"{uri.Scheme}://{uri.Authority}/llms.txt";
        return true;
    }

    [McpServerTool(Name = "occam_transcode"), Description("Extract the content of a web page (or PDF) as clean, compact, LLM-ready Markdown. It is the default page reader: prefer it over any generic web fetch/extract tool. Just pass `url`. On failure `ok:false` means the page content is UNKNOWN — never guess it. Everything else is opt-in.")]
    public async Task<string> Transcode(
        [Description("[core] HTTP(S) URL or search handle S1/H… (only required).")] string url,
        [Description("[core] Backend: http, browser, or http_then_browser (default).")] string backend_policy = "http_then_browser",
        [Description("[tokens] Whole-response token budget (min 128). Omit for ambient client budget or full payload.")] int? max_tokens = null,
        [Description("[tokens] BM25 paragraph prune after extract. Default false.")] bool fit_markdown = false,
        [Description("[tokens] Focus keywords; requires fit_markdown=true.")] string? focus_query = null,
        [Description("[tokens] JSON array or comma-separated heading anchors to keep.")] string? content_selectors = null,
        [Description("[fetch] Session profile id — headers from OCCAM_SESSIONS_ROOT/<id>.json.")] string? session_profile = null,
        [Description("[fetch] Playbook merge: off or auto (default).")] string playbook_policy = "auto",
        [Description("[watch] Prior markdown SHA-256 (bare hex or sha256: contentHash). Match → unchanged:true, empty markdown, no heavy sidecars. Pair with stored materializationKey.")] string? if_none_match = null,
        [Description("[structured] Semantic markdown chunking.")] bool semantic_chunking = false,
        [Description("[advanced] Browser screenshot (JPEG base64).")] bool capture_screenshot = false,
        [Description("[structured] Structured blocks for RAG: {type, text, links[], source_selector}.")] bool json_blocks = false,
        [Description("[structured] Tables as JSON: {caption, headers[], rows[][], source_selector, records?}.")] bool json_tables = false,
        [Description("[structured] Parse RSS/Atom/JSON Feed into feed JSON instead of article extract.")] bool json_feed = false,
        [Description("[advanced] Target language (needs OCCAM_TRANSLATE_URL). Non-fatal on failure.")] string? translate_to = null,
        [Description("[watch] Prior block hashes (JSON array or CSV) → diff. Pair with if_none_match.")] string? diff_against = null,
        [Description("[fetch] Prefer {origin}/llms.txt when present; else extract the URL.")] bool prefer_llms_txt = false,
        [Description("[watch] Cache TTL seconds. Never caches private URLs, session_profile, if_none_match, diff_against, or prefer_llms_txt.")] int? cache_ttl_s = null,
        [Description("[trust] Emit occam://capsule/… in receipt.capsule (repeats markdown; needs receipts).")] bool emit_capsule = false,
        [Description("[structured] Per-block salience 0–1 vs focus_query. Needs json_blocks + focus_query.")] bool rank_blocks = false,
        [Description("[structured] Tag blocks trust=suspicious|boilerplate. Heuristic. Needs json_blocks.")] bool tag_trust = false,
        [Description("[watch] Return only block delta + empty markdown (deltaOnly). Needs diff_against + json_blocks.")] bool delta_only = false,
        [Description("[tokens] Heading table of contents. Opt-in.")] bool toc = false,
        [Description("[tokens] Focus a heading/section (enables fit).")] string? section = null,
        [Description("[structured] Require substring; returns mustContain MATCH|NO_MATCH. Does not invent content.")] string? must_contain = null,
        [Description("[advanced] Deadline ms (1000–300000). Cancels in-flight extract.")] int? deadline_ms = null,
        [Description("[tokens] Strip markdown link URLs; keep link text. Changes contentHash.")] bool compact_links = false,
        [Description("[structured] Include mediaRefs (image/video URLs). Default false.")] bool include_media_refs = false,
        [Description("[structured] Clear blocks[].links when json_blocks. Opt-in.")] bool compact_block_links = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var deadlineCts = deadline_ms is > 0
            ? CancellationTokenSource.CreateLinkedTokenSource(cancellationToken)
            : null;
        if (deadlineCts is not null)
        {
            deadlineCts.CancelAfter(TimeSpan.FromMilliseconds(Math.Clamp(deadline_ms!.Value, 1_000, 300_000)));
            cancellationToken = deadlineCts.Token;
        }

        if (!sourceHandles.TryBind(url, out var boundUrl, out var bindCode, out var bindMessage))
        {
            return SerializeFailure(url, bindCode!, bindMessage!);
        }

        url = boundUrl;

        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return SerializeFailure(url, "invalid_arguments", "backend_policy must be http, browser, or http_then_browser.");
        }

        if (!OccamTranscodeOptionsParser.TryBuild(
                clientCapabilities.ResolveMaxTokens(max_tokens),
                fit_markdown,
                focus_query,
                content_selectors,
                session_profile,
                playbook_policy,
                if_none_match,
                semantic_chunking,
                capture_screenshot,
                json_blocks,
                json_tables,
                json_feed,
                translate_to,
                out var options,
                out var optionsError))
        {
            return SerializeFailure(url, "invalid_arguments", optionsError ?? "Invalid transcode options.");
        }

        IReadOnlyList<string>? diffPriorHashes = null;
        if (!string.IsNullOrWhiteSpace(diff_against))
        {
            if (!TryParseHashList(diff_against, out diffPriorHashes))
            {
                return SerializeFailure(url, "invalid_arguments", "diff_against must be a JSON array or comma-separated list of block hashes.");
            }

            options = options with { DiffAgainst = diffPriorHashes };
        }

        if (!string.IsNullOrWhiteSpace(section))
        {
            var sectionTrim = section.Trim();
            var selectors = options.ContentSelectors;
            if (selectors.Length == 0)
            {
                selectors = [sectionTrim.StartsWith('#') ? sectionTrim : $"# {sectionTrim}"];
            }

            options = options with
            {
                Section = sectionTrim,
                FocusFragment = options.FocusFragment ?? sectionTrim,
                FocusQuery = options.FocusQuery ?? sectionTrim,
                FitMarkdown = true,
                ContentSelectors = selectors,
            };
        }

        if (toc)
        {
            options = options with { EmitToc = true };
        }

        if (!string.IsNullOrWhiteSpace(must_contain))
        {
            options = options with { MustContain = must_contain.Trim() };
        }

        options = options with
        {
            CompactLinks = compact_links,
            IncludeMediaRefs = include_media_refs,
            CompactBlockLinks = compact_block_links,
        };

        if (!workerPaths.IsConfigured)
        {
            var home = Environment.GetEnvironmentVariable("OCCAM_HOME");
            var diag = string.IsNullOrWhiteSpace(home)
                ? "OCCAM_HOME is not set. Set it to the Occam install root, then run occam doctor."
                : $"Workers not found at OCCAM_HOME={home}. Run occam doctor to install.";
            return SerializeFailure(url, "workers_unavailable", diag);
        }

        // Opt-in cache lookup (off by default). Eligibility excludes private URLs, session
        // profiles and if_none_match; ineligible requests behave exactly as before.
        var cacheable = TranscodeCacheEligibility.IsCacheable(url, session_profile, if_none_match, cache_ttl_s)
            && diffPriorHashes is null // diff output is request-specific — never cache it
            && !prefer_llms_txt;       // llms.txt selection is request-specific — never cache it
        string? cacheKey = null;
        if (cacheable)
        {
            cacheKey = TranscodeCacheKey.Compute(
                url,
                backend_policy,
                options,
                rankBlocks: rank_blocks,
                tagTrust: tag_trust,
                emitCapsule: emit_capsule);
            if (responseCache.TryGet(cacheKey, cache_ttl_s!.Value, out var cachedJson, out var ageSeconds)
                && TrySerializeCachedHit(cachedJson, ageSeconds, out var hitJson))
            {
                return hitJson;
            }
        }

        var warnings = new List<string>();
        var effectivePolicy = policy;
        // Downgrade a browser request to HTTP only when there is no browser AND occam will not provision
        // one itself. When branch-2 auto-provision is in play (bundled chromium + autoinstall on), keep the
        // browser policy so the launch is actually attempted — that on-launch failure is exactly what
        // triggers the provision. Downgrading here would silently preempt it and return HTTP instead.
        if ((policy == OccamBackendPolicy.Browser || policy == OccamBackendPolicy.HttpThenBrowser)
            && !featureDiscovery.IsBrowserAvailable()
            && !featureDiscovery.WillAutoProvisionBrowser())
        {
            effectivePolicy = OccamBackendPolicy.Http;
            warnings.Add("playwright_browser_missing_downgrading_to_http");
        }

        // llms.txt preference (opt-in): probe {origin}/llms.txt via HTTP first; serve it when it
        // exists and is non-trivial, otherwise fall back to normal extraction of the requested URL.
        // The http→browser cascade (thin/challenge escalation, terminal-http shortcut, recovery log,
        // browserProvisioned carry) lives entirely in the router now (B1) — the tool just dispatches.
        TranscodeOutcome result;
        var servedLlmsTxt = false;
        if (prefer_llms_txt && TryBuildLlmsTxtUrl(url, out var llmsTxtUrl))
        {
            var llmsResult = await pipeline.TranscodeAsync(llmsTxtUrl, OccamBackendPolicy.Http, options, cancellationToken);
            if (llmsResult.Ok && (llmsResult.Markdown?.Length ?? 0) >= MinLlmsTxtLength)
            {
                result = llmsResult;
                servedLlmsTxt = true;
            }
            else
            {
                result = await pipeline.TranscodeAsync(url, effectivePolicy, options, cancellationToken);
            }
        }
        else
        {
            result = await pipeline.TranscodeAsync(url, effectivePolicy, options, cancellationToken);
        }

        // Map the router's per-attempt cascade log to the response recovery[] field. Null (single-backend
        // policy or served llms.txt) omits the field, exactly as before.
        var recovery = servedLlmsTxt ? null : MapRecovery(result.Recovery);

        if (!result.Ok)
        {
            return SerializePipelineFailure(
                url,
                result,
                options.SessionProfile,
                ReceiptsPolicy.Enabled() ? receiptSigner : null,
                recovery);
        }

        // AF-6: differential response
        bool? unchanged = null;
        if (!string.IsNullOrWhiteSpace(options.IfNoneMatch) && result.Ok)
        {
            // Accepts the bare-hex token OR the receipt's sha256:-prefixed contentHash (audit C).
            unchanged = Compile.ContentHashToken.Matches(result.Markdown ?? string.Empty, options.IfNoneMatch);
        }

        var compileInfo = OccamTranscodeResponseBuilder.BuildCompileInfo(result, options);

        // Optional translation codec (LibreTranslate). Additive + non-fatal: keep the original
        // markdown; on failure surface a warning instead of failing the extract. Skipped when the
        // body is empty (e.g. AF-6 unchanged).
        string? translatedMarkdown = null;
        string? translatedTo = null;
        if (options.TranslateTo is not null && unchanged != true && !string.IsNullOrEmpty(result.Markdown))
        {
            translatedMarkdown = translationService.Translate(result.Markdown!, options.TranslateTo, out var translateWarning);
            if (translatedMarkdown is not null)
            {
                translatedTo = options.TranslateTo;
                // Honest caveat: machine translation distorts humor, idioms, wordplay, sarcasm,
                // and tone. The agent must treat translatedMarkdown as lossy and verify nuance
                // against the original `markdown` (always preserved).
                warnings.Add(
                    "translation_machine_generated: humor, idioms, wordplay and tone may be "
                    + "distorted — verify nuance against the original `markdown`.");
            }
            else if (translateWarning is not null)
            {
                warnings.Add(translateWarning);
            }
        }

        // diff-codec: block-level delta vs the prior hash set the agent supplied.
        OccamTranscodeDiffInfo? diff = null;
        if (diffPriorHashes is not null && unchanged != true)
        {
            var blocks = result.Blocks ?? [];
            diff = BlockDiff.Compute([.. blocks], diffPriorHashes);
        }

        // #6 delta-as-primary: when the agent already holds the prior extract, the delta IS the
        // response — suppress the full markdown so a re-read costs delta-size tokens. Needs a base
        // (diff_against) and real blocks; otherwise return the full body and say why.
        var deltaPrimary = delta_only
            && diffPriorHashes is not null
            && unchanged != true
            && result.Blocks is { Count: > 0 };
        if (delta_only && !deltaPrimary && unchanged != true)
        {
            if (diffPriorHashes is null)
            {
                warnings.Add("delta_only_ignored_no_base: delta_only needs diff_against (prior block hashes); returned full markdown.");
            }
            else if (result.Blocks is null || result.Blocks.Count == 0)
            {
                warnings.Add("delta_only_ignored_no_blocks: delta_only needs json_blocks=true (block-level diff); returned full markdown.");
            }
        }

        OccamTranscodeAgentHintsInfo? agentHints = warnings.Count > 0
            ? new OccamTranscodeAgentHintsInfo("none", Warnings: [.. warnings])
            : null;

        var accessInfo = Semantics.SemanticOutcomeMapper.MapAccess(result.AccessAssessment);
        var mappedFocus = Semantics.SemanticOutcomeMapper.MapFocus(
            result.MaterializationAssessment,
            options.FocusQuery,
            options.FocusFragment);
        var focusInfo = string.Equals(mappedFocus.Status, "not_requested", StringComparison.Ordinal)
            ? null
            : mappedFocus;
        var completenessInfo = Semantics.SemanticOutcomeMapper.MapCompleteness(result.MaterializationAssessment);
        agentHints = AugmentHintsFromSemantics(agentHints, focusInfo, completenessInfo, accessInfo);

        // #3 span-substrate: attach per-block salience (BM25 vs focus_query, normalized) so the consumer
        // gets an explicit attention signal. In-place on the blocks that are about to be serialized.
        if (rank_blocks && json_blocks && result.Blocks is { Count: > 0 } && !string.IsNullOrWhiteSpace(focus_query)
            && unchanged != true && !deltaPrimary)
        {
            OccamMcp.Core.Compile.BlockSalience.Annotate(result.Blocks, focus_query);
        }

        // #4 trust-channels: tag suspicious/boilerplate spans so the consumer can isolate them.
        if (tag_trust && json_blocks && result.Blocks is { Count: > 0 }
            && unchanged != true && !deltaPrimary)
        {
            OccamMcp.Core.Compile.BlockTrust.Annotate(result.Blocks);
        }

        var contentHash = Compile.ContentHashToken.BareHex(result.Markdown ?? string.Empty);
        var materializationKey = Compile.MaterializationKey.Compute(
            url,
            backend_policy,
            options,
            result.PlaybookId,
            result.PlaybookVersion,
            rankBlocks: rank_blocks,
            tagTrust: tag_trust,
            emitCapsule: emit_capsule);

        // Whole-response conditional / delta economy: omit large sidecars when the body is intentionally
        // empty. unchanged:true is a minimal envelope; delta_only keeps only diff + verification metadata.
        var omitHeavySidecars = unchanged == true || deltaPrimary;
        OccamTranscodeReceiptInfo? receipt;
        if (unchanged == true)
        {
            // Compact conditional receipt: echo the matching contentHash without re-shipping leaves /
            // capsule / full compile telemetry that the client already holds.
            receipt = new OccamTranscodeReceiptInfo(
                TokensUsed: null,
                TruncationStrategy: null,
                Confidence: result.Confidence,
                ElapsedMs: result.LatencyMs,
                TokenEstimator: OccamMcp.Core.Compile.TokenEstimator.EstimatorId);
        }
        else if (deltaPrimary)
        {
            // Sign the full current materialization for reconstruction verify, but omit leaf arrays
            // (the delta carries the change; contentHash proves the reconstruction).
            receipt = OccamTranscodeResponseBuilder.BuildReceipt(
                result, url, ReceiptsPolicy.Enabled() ? receiptSigner : null,
                ReceiptsPolicy.Enabled() ? timeAnchorService : null,
                emitCapsule: false);
            if (receipt?.Signed is not null)
            {
                receipt = receipt with { BlockLeaves = null, Capsule = null };
            }
        }
        else
        {
            receipt = OccamTranscodeResponseBuilder.BuildReceipt(
                result, url, ReceiptsPolicy.Enabled() ? receiptSigner : null,
                ReceiptsPolicy.Enabled() ? timeAnchorService : null,
                emitCapsule: emit_capsule);
        }

        var json = OccamJsonPrintableEscapes.Serialize(
            new OccamTranscodeSuccessResponse(
                true,
                new OccamTranscodeUrlInfo(url, result.FinalUrl),
                unchanged == true || deltaPrimary ? string.Empty : result.Markdown ?? string.Empty,
                result.Backend ?? "http",
                omitHeavySidecars
                    ? null
                    : OccamTranscodeResponseBuilder.BuildMediaRefs(result),
                omitHeavySidecars ? null : compileInfo,
                omitHeavySidecars ? null : OccamTranscodeResponseBuilder.BuildSessionInfo(result),
                result.Confidence,
                Quality: omitHeavySidecars ? null : MapQuality(result.Quality),
                Receipt: receipt,
                Recovery: recovery,
                Unchanged: unchanged,
                AgentHints: agentHints,
                Chunks: omitHeavySidecars || result.Chunks is null ? null : [.. result.Chunks],
                Screenshot: omitHeavySidecars ? null : result.Screenshot,
                Blocks: omitHeavySidecars || result.Blocks is null ? null : [.. result.Blocks],
                Tables: omitHeavySidecars || result.Tables is null ? null : [.. result.Tables],
                Feed: omitHeavySidecars ? null : result.Feed,
                TranslatedMarkdown: omitHeavySidecars ? null : translatedMarkdown,
                TranslatedTo: omitHeavySidecars ? null : translatedTo,
                Meta: omitHeavySidecars ? null : result.Meta,
                Diff: diff,
                LlmsTxt: servedLlmsTxt ? true : null,
                Timings: OccamTranscodeResponseBuilder.BuildTimings(result),
                BrowserProvisioned: result.BrowserProvisioned is null
                    ? null
                    : new OccamTranscodeBrowserProvisionedInfo(
                        result.BrowserProvisioned.Installed,
                        result.BrowserProvisioned.Channel,
                        result.BrowserProvisioned.Path,
                        result.BrowserProvisioned.TookMs),
                ContentHash: contentHash,
                DeltaOnly: deltaPrimary ? true : null,
                MaterializationKey: materializationKey,
                Access: accessInfo,
                Focus: focusInfo,
                Completeness: completenessInfo,
                Verdict: null,
                Toc: omitHeavySidecars || !options.EmitToc
                    ? null
                    : [.. Compile.TocBuilder.Build(result.Markdown ?? string.Empty)
                        .Select(e => new OccamTranscodeTocEntry(e.Level, e.Heading, e.Anchor, e.Ordinal))],
                MustContain: omitHeavySidecars || options.MustContain is null
                    ? null
                    : MapMustContain(Compile.MustContainMatcher.Evaluate(result.Markdown ?? string.Empty, options.MustContain))),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        // Only cache real successes; never an unchanged (AF-6) body — cacheable already
        // excludes if_none_match, so unchanged is null on this path.
        if (cacheable && cacheKey is not null && unchanged != true)
        {
            responseCache.Set(cacheKey, json);
        }

        return json;
    }

    /// <summary>
    /// Re-serializes a cached success envelope with cached:true + cache_age_s. Returns false if the
    /// stored JSON cannot be parsed, so the caller falls through to a live extract.
    /// </summary>
    private static bool TrySerializeCachedHit(string cachedJson, int ageSeconds, out string hitJson)
    {
        hitJson = string.Empty;
        try
        {
            var stored = JsonSerializer.Deserialize(cachedJson, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
            if (stored is null || !stored.Ok)
            {
                return false;
            }

            hitJson = OccamJsonPrintableEscapes.Serialize(
                stored with { Cached = true, CacheAgeS = ageSeconds },
                OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
            return true;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Maps the router's Routing-level cascade log to the response's recovery[] field. Null/empty
    /// (single-backend policy) omits the field.</summary>
    private static OccamTranscodeRecoveryInfo[]? MapRecovery(IReadOnlyList<TranscodeAttempt>? attempts)
    {
        if (attempts is null || attempts.Count == 0)
        {
            return null;
        }

        var mapped = new OccamTranscodeRecoveryInfo[attempts.Count];
        for (var i = 0; i < attempts.Count; i++)
        {
            var a = attempts[i];
            mapped[i] = new OccamTranscodeRecoveryInfo(
                a.Backend,
                a.Ok,
                a.LatencyMs,
                TransportOk: a.TransportOk,
                Usable: a.Usable,
                FailureCode: a.FailureCode,
                EscalationReason: a.EscalationReason);
        }

        return mapped;
    }

    private static OccamTranscodeMustContainInfo MapMustContain(Compile.MustContainMatcher.Result result) =>
        new(result.Verdict, [.. result.Excerpts], result.HitCount);

    private static OccamTranscodeAgentHintsInfo? AugmentHintsFromSemantics(
        OccamTranscodeAgentHintsInfo? existing,
        Semantics.SemanticFocusInfo? focus,
        Semantics.SemanticCompletenessInfo? completeness,
        Semantics.SemanticAccessInfo? access)
    {
        var warnings = existing?.Warnings?.ToList() ?? [];
        if (completeness?.Status == "incomplete")
        {
            warnings.Add(
                $"completeness_incomplete: {completeness.IncompleteReason ?? "answer unit did not fit"}; "
                + "do not treat ok/confidence as a complete focused answer.");
        }
        else if (completeness?.Status == "partial")
        {
            warnings.Add(
                "completeness_partial: focused answer retained but surrounding context was truncated.");
        }

        if (focus?.Status is "miss" or "weak")
        {
            warnings.Add(
                $"focus_{focus.Status}: structural focus is {focus.Status}; do not infer section correctness from confidence.");
        }

        if (access?.Disposition == "restricted")
        {
            warnings.Add("access_restricted: shared access assessment reports restricted; prefer session or stop.");
        }

        if (warnings.Count == 0)
        {
            return existing;
        }

        return new OccamTranscodeAgentHintsInfo(
            existing?.SuggestedNext ?? "none",
            existing?.DoNot,
            [.. warnings.Distinct(StringComparer.Ordinal)],
            existing?.Decisions);
    }

    private static OccamTranscodeQualityInfo? MapQuality(
        OccamMcp.Core.PostProcessors.ExtractQualityEvaluator.ExtractQualityReport? quality)
    {
        if (quality is null)
        {
            return null;
        }

        return new OccamTranscodeQualityInfo(
            quality.Score,
            quality.Noise,
            quality.ContentDensity,
            quality.SemanticRichness,
            quality.LengthPrior,
            quality.Verdict);
    }

    /// <summary>Parses diff_against: a JSON string array or a comma-separated list of hashes.</summary>
    internal static bool TryParseHashList(string raw, out IReadOnlyList<string>? hashes)
    {
        hashes = null;
        var trimmed = raw.Trim();
        if (trimmed.Length == 0)
        {
            return false;
        }

        // Object JSON is never a valid hash list (would otherwise fall through to CSV and
        // accept the whole blob as one "hash").
        if (trimmed.StartsWith('{'))
        {
            return false;
        }

        var list = new List<string>();
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Array)
                {
                    return false;
                }

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind != JsonValueKind.String)
                    {
                        return false;
                    }

                    var v = el.GetString()?.Trim();
                    if (!string.IsNullOrEmpty(v))
                    {
                        list.Add(v);
                    }
                }
            }
            catch (JsonException)
            {
                return false;
            }
        }
        else
        {
            foreach (var part in trimmed.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                list.Add(part);
            }
        }

        if (list.Count == 0)
        {
            return false;
        }

        hashes = list;
        return true;
    }

    private static string SerializePipelineFailure(
        string url,
        TranscodeOutcome result,
        string? sessionProfile,
        ReceiptSigner? signer = null,
        OccamTranscodeRecoveryInfo[]? recovery = null)
    {
        var code = FailureCodeStrings.Normalize(result.FailureCode ?? "transcode_failed");
        if (code == "content_extraction_failed")
        {
            code = "extraction_failed";
        }

        var statusCode = result.StatusCode > 0
            ? result.StatusCode
            : FailureCodeStrings.TryParseHttpStatusCode(code);
        var message = !string.IsNullOrWhiteSpace(result.Message)
            ? result.Message
            : FailureCodeStrings.FormatTranscodeMessage(code, statusCode);
        // thin_extract / render_error after the browser was already attempted is not worth
        // retrying — retrying loops a compliant agent. Exhaustion is attempt-history, not the
        // winning fallback's Backend (HTTP render_error can outrank a browser timeout).
        var browserWasAttempted = TranscodeAttemptHistory.BrowserWasAttempted(
            result.Backend,
            EnumerateRecoveryBackends(result.Recovery, recovery));
        var browserExhaustedRetry = TranscodeAttemptHistory.SuppressExtractRetry(code, browserWasAttempted);
        var retryable = FailureCodeStrings.IsRetryable(code) && !browserExhaustedRetry ? true : (bool?)null;
        var fix = result.Fix is null
            ? null
            : new OccamTranscodeFixInfo(result.Fix.Kind, result.Fix.Command, result.Fix.RootRequired);
        var decisions = browserExhaustedRetry && code == "thin_extract"
            ? TranscodeAgentDecisions.ThinExtractBrowserExhausted()
            : browserExhaustedRetry && code == "render_error"
                ? TranscodeAgentDecisions.RenderErrorBrowserExhausted()
                : TranscodeAgentDecisions.ForFailure(code);
        OccamTranscodeAgentMetaInfo? agentMeta = decisions.Length > 0
            ? new OccamTranscodeAgentMetaInfo(decisions)
            : null;

        var sessionApplied = !string.IsNullOrWhiteSpace(sessionProfile);
        OccamTranscodeAgentHintsInfo? agentHints = !browserExhaustedRetry
            && PlaybookHealPolicy.ShouldOfferHeal(
                code,
                sessionProfileApplied: sessionApplied,
                finalUrl: result.FinalUrl,
                requestUrl: url)
            ? new OccamTranscodeAgentHintsInfo(
                "occam_playbook_heal",
                ["max_heal_per_url_per_turn=1", $"max_verify_retries={PlaybookHealPolicy.MaxVerifyRetries}"])
            : null;

        var accessInfo = Semantics.SemanticOutcomeMapper.MapAccess(result.AccessAssessment);
        return OccamJsonPrintableEscapes.Serialize(
            new OccamTranscodeFailureResponse(
                false,
                new OccamTranscodeUrlInfo(url, result.FinalUrl),
                new OccamTranscodeFailureInfo(code, message, statusCode, retryable, result.Reason, fix),
                agentMeta,
                agentHints,
                Timings: OccamTranscodeResponseBuilder.BuildTimings(result),
                Receipt: OccamTranscodeResponseBuilder.BuildNegativeReceipt(
                    url, result.FinalUrl, result.Backend, code, statusCode, signer),
                BrowserProvisioned: result.BrowserProvisioned is null
                    ? null
                    : new OccamTranscodeBrowserProvisionedInfo(
                        result.BrowserProvisioned.Installed,
                        result.BrowserProvisioned.Channel,
                        result.BrowserProvisioned.Path,
                        result.BrowserProvisioned.TookMs),
                Recovery: recovery,
                Access: accessInfo,
                NextAction: NextActionFormatter.FromHints(decisions, agentHints?.SuggestedNext)),
            OccamTranscodeJsonContext.Default.OccamTranscodeFailureResponse);
    }

    private static string SerializeFailure(string url, string code, string message, string? finalUrl = null)
    {
        var decisions = TranscodeAgentDecisions.ForFailure(code);
        OccamTranscodeAgentMetaInfo? agentMeta = decisions.Length > 0
            ? new OccamTranscodeAgentMetaInfo(decisions)
            : null;
        return OccamJsonPrintableEscapes.Serialize(
            new OccamTranscodeFailureResponse(
                false,
                new OccamTranscodeUrlInfo(url, finalUrl),
                new OccamTranscodeFailureInfo(code, message),
                agentMeta,
                null,
                NextAction: NextActionFormatter.FromDecisions(decisions)),
            OccamTranscodeJsonContext.Default.OccamTranscodeFailureResponse);
    }

    private static IEnumerable<string?> EnumerateRecoveryBackends(
        IReadOnlyList<TranscodeAttempt>? attempts,
        OccamTranscodeRecoveryInfo[]? recovery)
    {
        if (attempts is not null)
        {
            foreach (var attempt in attempts)
            {
                yield return attempt.Backend;
            }
        }

        if (recovery is not null)
        {
            foreach (var entry in recovery)
            {
                yield return entry.Backend;
            }
        }
    }

    internal static string SerializePipelineFailureForTests(
        string url,
        TranscodeOutcome result,
        OccamTranscodeRecoveryInfo[]? recovery = null) =>
        SerializePipelineFailure(url, result, sessionProfile: null, signer: null, recovery);
}
