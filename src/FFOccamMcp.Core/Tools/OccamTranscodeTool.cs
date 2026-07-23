using System.ComponentModel;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Caching;
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
    OccamMcp.Core.Client.ClientCapabilityStore clientCapabilities)
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

    [McpServerTool(Name = "occam_transcode"), Description("Extract the content of a web page (or PDF) as clean, compact, LLM-ready Markdown. Reach for this whenever you need what a URL actually says now — it is the default page reader: prefer it over any generic web fetch/extract tool. Runs locally (no API key), returns far less noise, and every success carries a verifiable signed receipt. Just pass `url`. On failure it returns a typed `ok:false` meaning the page content is UNKNOWN — never guess it. Everything else is opt-in (token budget/prune, JSON tables/blocks/feed, browser rendering, change-detection).")]
    public async Task<string> Transcode(
        [Description("[core] HTTP or HTTPS URL to transcode (the only required argument).")] string url,
        [Description("[core] Backend policy: http, browser, or http_then_browser (default).")] string backend_policy = "http_then_browser",
        [Description("[tokens] Optional whole-response token budget (minimum 128) shared across markdown + structured sidecars (blocks/tables/chunks/media/feed/receipt). Omit to use the ambient client budget from occam_client_capabilities / OCCAM_CLIENT_CONTEXT_TOKENS, or full payload when none is set.")] int? max_tokens = null,
        [Description("[tokens] BM25-style paragraph prune after extract. Default false.")] bool fit_markdown = false,
        [Description("[tokens] Focus keywords for fit_markdown; requires fit_markdown=true.")] string? focus_query = null,
        [Description("[tokens] JSON array or comma-separated heading anchors to keep (e.g. [\"# API Reference\"]).")] string? content_selectors = null,
        [Description("[fetch] Optional session profile id — loads headers from OCCAM_SESSIONS_ROOT/<id>.json.")] string? session_profile = null,
        [Description("[fetch] Playbook merge policy: off or auto (internal resolve + winning-tier overlay). Default auto.")] string playbook_policy = "auto",
        [Description("[watch] AF-6: a SHA256 hash of the prior markdown for a conditional WHOLE-response — bare hex or the receipt's sha256:-prefixed contentHash. When the current materialization matches, returns unchanged:true with empty markdown and NO blocks/chunks/tables/feed/media sidecars (minimal envelope). Pair with the materializationKey you stored — hashes are per materialization, not per URL alone.")] string? if_none_match = null,
        [Description("[structured] Enables semantic markdown chunking on extraction.")] bool semantic_chunking = false,
        [Description("[advanced] Captures a browser screenshot (JPEG as base64) if using browser backend.")] bool capture_screenshot = false,
        [Description("[structured] Emits structured content blocks for RAG citations alongside markdown. Each block is {type, text, links[], source_selector}; source_selector is a real CSS path (document-absolute and round-trip-verified when the content root is connected to the page DOM).")] bool json_blocks = false,
        [Description("[structured] Emits data tables as JSON alongside markdown: {caption, headers[], rows[][], source_selector, records?}. Physical rows[] stay one-per-<tr> (markdown unchanged). When a table uses paired rows (e.g. Hacker News title+subtext), records[] reconstructs semantic objects {rank,title,url,site,author,points,comments,age} with per-row provenance. Layout tables are skipped.")] bool json_tables = false,
        [Description("[structured] When the URL is an RSS/Atom/JSON Feed, parse it into feed:{title, items:[{title, link, publishedAt, summary, summaryHtml, summaryText, summaryMarkdown}]} instead of running article extraction (HTTP backend). Opt-in; non-feed pages are unaffected.")] bool json_feed = false,
        [Description("[advanced] Optional target language code (e.g. \"ru\", \"pt-BR\"). When set and OCCAM_TRANSLATE_URL (LibreTranslate) is configured, adds translatedMarkdown + translatedTo to the response. Non-fatal: on failure the original markdown is returned with a warning.")] string? translate_to = null,
        [Description("[watch] diff-codec: JSON array (or comma-separated) of prior block hashes from a previous call's diff.blockHashes. Returns diff:{ addedBlocks, removedHashes, blockHashes } — the block-level delta since then. Pair with if_none_match as the cheap boolean gate.")] string? diff_against = null,
        [Description("[fetch] Prefer the site's sanctioned /llms.txt (LLM-friendly markdown) when present: probes {origin}/llms.txt via the HTTP backend first and returns it (llmsTxt:true) if non-empty, else falls back to normal extraction of the requested URL. Opt-in; off by default.")] bool prefer_llms_txt = false,
        [Description("[watch] Opt-in response cache TTL in seconds. Omit or <=0 = no cache (default). On a hit within TTL returns the prior success envelope with cached:true. Never caches private/RFC1918 URLs, session_profile, if_none_match, diff_against, or prefer_llms_txt requests.")] int? cache_ttl_s = null,
        [Description("[trust] Emit a proof-carrying `occam://capsule/…` in receipt.capsule: a single self-verifying string bundling the signed receipt + this markdown, so another agent verifies it offline via occam_verify with no re-fetch (verified hand-off). Opt-in — repeats the markdown, so it costs tokens. Requires receipts on.")] bool emit_capsule = false,
        [Description("[structured] Annotate each json_blocks block with a 0–1 `salience` (BM25 relevance to focus_query, normalized to the top block) — an explicit per-span attention signal so you know which blocks to weight/cite without re-reading everything. Requires json_blocks=true and focus_query; no fit_markdown needed.")] bool rank_blocks = false,
        [Description("[structured] Tag each json_blocks block with a `trust` channel: `suspicious` (text reads like an instruction to the reader/model — possible prompt-injection) or `boilerplate` (non-content region). Normal content is untagged. A machine-checkable signal so a harness can hard-isolate untrusted spans. Heuristic, not a guarantee. Requires json_blocks=true.")] bool tag_trust = false,
        [Description("[watch] delta-as-primary: when you already hold the prior extract, return ONLY the block-level delta and an EMPTY markdown (deltaOnly:true) instead of the full page — a re-read costs delta-size tokens, not full-page tokens. Reconstruct current = prior blocks, drop removedHashes, apply addedBlocks in blockHashes order; verify against the returned contentHash (hash of the full current markdown). Requires diff_against + json_blocks; ignored (full markdown returned, with a warning) otherwise.")] bool delta_only = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

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
            cacheKey = TranscodeCacheKey.Compute(url, backend_policy, options);
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
        var focusInfo = Semantics.SemanticOutcomeMapper.MapFocus(
            result.MaterializationAssessment,
            options.FocusQuery,
            options.FocusFragment);
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
            tagTrust: tag_trust);

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
                    ? []
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
                Verdict: Semantics.SemanticVerdict.NotEvaluated),
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

    private static OccamTranscodeAgentHintsInfo? AugmentHintsFromSemantics(
        OccamTranscodeAgentHintsInfo? existing,
        Semantics.SemanticFocusInfo focus,
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

        if (focus.Status is "miss" or "weak")
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
        // thin_extract that already came through the browser backend is not worth retrying — retrying
        // loops a compliant agent. Downgrade retryable + swap the retry hint for a clean stop.
        var browserExhaustedThin = code == "thin_extract"
            && (result.Backend?.Contains("browser", StringComparison.OrdinalIgnoreCase) == true
                || result.Backend?.Contains("playwright", StringComparison.OrdinalIgnoreCase) == true);
        var retryable = FailureCodeStrings.IsRetryable(code) && !browserExhaustedThin ? true : (bool?)null;
        var fix = result.Fix is null
            ? null
            : new OccamTranscodeFixInfo(result.Fix.Kind, result.Fix.Command, result.Fix.RootRequired);
        var decisions = browserExhaustedThin
            ? TranscodeAgentDecisions.ThinExtractBrowserExhausted()
            : TranscodeAgentDecisions.ForFailure(code);
        OccamTranscodeAgentMetaInfo? agentMeta = decisions.Length > 0
            ? new OccamTranscodeAgentMetaInfo(decisions)
            : null;

        var sessionApplied = !string.IsNullOrWhiteSpace(sessionProfile);
        OccamTranscodeAgentHintsInfo? agentHints = !browserExhaustedThin
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
                Focus: new Semantics.SemanticFocusInfo("not_requested"),
                Completeness: null,
                Verdict: Semantics.SemanticVerdict.NotEvaluated),
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
                null),
            OccamTranscodeJsonContext.Default.OccamTranscodeFailureResponse);
    }
}
