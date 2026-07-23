using System.Globalization;
using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;

namespace OccamMcp.Core.Tools;

public sealed record OccamTranscodeUrlInfo(string Url, string? FinalUrl);

public sealed record OccamTranscodeFailureInfo(
    string Code,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int StatusCode = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Retryable = null,
    // Browser-availability remedy: a human "why" + a machine-actionable fix. Present only when a
    // backend reported the page needs a browser occam can't currently launch (playwright_missing).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Reason = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeFixInfo? Fix = null);

/// <summary>Actionable remedy for a browser-availability failure. <c>command</c> is the exact thing to
/// run; <c>rootRequired</c> marks the boundary occam can't cross for the user (system libs need root).</summary>
public sealed record OccamTranscodeFixInfo(
    string? Kind,
    string? Command,
    bool RootRequired);

/// <summary>Branch-2 note on a success: occam auto-installed the user-level browser on this call, so the
/// caller knows why the first browser call was slower and that nothing was done silently.</summary>
public sealed record OccamTranscodeBrowserProvisionedInfo(
    bool Installed,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Channel,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Path,
    int TookMs);

public sealed record OccamTranscodeAgentMetaInfo(ProbeDecision[] Decisions);

/// <summary>
/// Per-stage wall-clock breakdown (ms) so an agent can see where a transcode spent its time:
/// <c>networkMs</c> is the with-internet leg (DNS+connect+TLS+download), <c>parseMs</c> is the
/// without-internet CPU leg (DOM+Readability+Turndown). The rest are host-side pipeline stages.
/// </summary>
public sealed record OccamTranscodeTimingsInfo(
    int TotalMs,
    int PreflightMs,
    int RouteMs,
    int NetworkMs,
    int ParseMs,
    int PostProcessMs,
    int CompileMs);

public sealed record OccamTranscodeAgentHintsInfo(
    string SuggestedNext,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? DoNot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? Warnings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ProbeDecision[]? Decisions = null);

public sealed record OccamTranscodeFailureResponse(
    bool Ok,
    OccamTranscodeUrlInfo Url,
    OccamTranscodeFailureInfo Failure,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeAgentMetaInfo? AgentMeta = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeAgentHintsInfo? AgentHints = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeTimingsInfo? Timings = null,
    // Receipt v1 (SI-03): a signed negative receipt on provable unavailability (captcha/login/
    // paywall/4xx) — a claim only an honest tool can make. Null on transient errors and when off.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptEnvelope? Receipt = null,
    // Branch-2 auto-provision telemetry survives onto a failure too: if the first browser need
    // installed chromium and the page then failed (thin/challenge/timeout), the caller still learns
    // the install happened (so the next call being fast is explained). Null unless we provisioned.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeBrowserProvisionedInfo? BrowserProvisioned = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeRecoveryInfo[]? Recovery = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticAccessInfo? Access = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticFocusInfo? Focus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticCompletenessInfo? Completeness = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Verdict = null);

public sealed record OccamTranscodeCompileInfo(
    int TokensEstimated,
    string TokenEstimator,
    bool Truncated,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TruncationStrategy = null,
    // #7 omitted-manifest: structured record of what max_tokens budgeting dropped. Present only when
    // Truncated is true, so the consumer can see the holes instead of inferring completeness.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeOmittedInfo? Omitted = null,
    // Unified response budget: per-bucket token spend when max_tokens capped the whole payload.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeBudgetInfo? Budget = null);

/// <summary>#7 omitted-manifest surfaced on the compile block. See <see cref="OccamMcp.Core.Compile.OmittedManifest"/>.</summary>
public sealed record OccamTranscodeOmittedInfo(
    string Reason,
    int TokensDropped,
    string[] Regions,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Sections = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeStructuredDroppedInfo? Structured = null);

/// <summary>Counts of structured sidecar items dropped to fit the shared max_tokens budget.</summary>
public sealed record OccamTranscodeStructuredDroppedInfo(
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int Blocks = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int Tables = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int Chunks = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int Media = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    int FeedItems = 0,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    bool Screenshot = false);

/// <summary>Factual per-bucket spend under the unified response budget planner.</summary>
public sealed record OccamTranscodeBudgetInfo(
    int Total,
    int Markdown,
    int Blocks,
    int Tables,
    int Chunks,
    int Media,
    int Feed,
    int Receipt);

/// <summary>AF-3: receipt metadata for extract responses.</summary>
public sealed record OccamTranscodeReceiptInfo(
    int? TokensUsed,
    string? TruncationStrategy,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    double Confidence = 0.0,
    int ElapsedMs = 0,
    // Receipt v1: the signed extraction envelope. Present when OCCAM_RECEIPTS is on; the telemetry
    // fields above stay for cost/latency tracking. The consumer verifies `Signed` via occam_verify.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptEnvelope? Signed = null,
    // SI-02: unsigned ordered block leaf hashes — authentic because RootFromLeafHashes reconstructs
    // the signed `Signed.blockMerkleRoot`. Kept OUT of the signature so the signed envelope stays
    // compact (root only) for O(log N) citation proofs; present when json_blocks produced blocks.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? BlockLeaves = null,
    // SI-15: independent RFC3161 time attestation over the signature (proves "existed no later than
    // T"). Unsigned sidecar; present only when time-anchoring is enabled and the TSA responded.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamMcp.Core.Receipts.ReceiptTimeAnchor? TimeAnchor = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TokenEstimator = null,
    // OKP proof-carrying capsule (emit_capsule=true): a single `occam://capsule/…` string that
    // bundles Signed + this markdown + BlockLeaves so another agent verifies it offline (occam_verify)
    // without re-fetching. Opt-in — it repeats the markdown, so it costs tokens; omitted by default.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Capsule = null);

public sealed record OccamTranscodeSessionInfo(
    string ProfileId,
    bool ProfileFound,
    string[] HeadersApplied);

public sealed record OccamTranscodeMediaRefInfo(
    string Url,
    string Kind,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Alt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContextHeading,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SelectorHint);

/// <summary>AF-4: auto-recovery metadata for a single backend attempt (PR-F dimensioned).</summary>
public sealed record OccamTranscodeRecoveryInfo(
    string Backend,
    bool Ok,
    int LatencyMs,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? TransportOk = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Usable = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailureCode = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? EscalationReason = null);

/// <summary>ADR-0004 extract quality model breakdown (success responses).</summary>
public sealed record OccamTranscodeQualityInfo(
    double Score,
    double Noise,
    double ContentDensity,
    double SemanticRichness,
    double LengthPrior,
    string Verdict);

public sealed record OccamTranscodeSuccessResponse(
    bool Ok,
    OccamTranscodeUrlInfo Url,
    string Markdown,
    string Backend,
    OccamTranscodeMediaRefInfo[] MediaRefs,
    OccamTranscodeCompileInfo? Compile = null,
    OccamTranscodeSessionInfo? Session = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
    double Confidence = 0.0,
    /// <summary>ADR-0004 EQM breakdown (additive; confidence ≈ quality.score on success).</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeQualityInfo? Quality = null,
    OccamTranscodeReceiptInfo? Receipt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeRecoveryInfo[]? Recovery = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Unchanged = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeAgentHintsInfo? AgentHints = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Workers.WorkerExtractChunkInfo[]? Chunks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Screenshot = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Cached = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? CacheAgeS = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Workers.WorkerExtractBlockInfo[]? Blocks = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Workers.WorkerExtractTableInfo[]? Tables = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Workers.WorkerExtractFeedInfo? Feed = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TranslatedMarkdown = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TranslatedTo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Workers.WorkerExtractMetaInfo? Meta = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeDiffInfo? Diff = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? LlmsTxt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeTimingsInfo? Timings = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeBrowserProvisionedInfo? BrowserProvisioned = null,
    // #11 KV-cache-stable-prefix: a cheap, always-on bare-hex SHA-256 of the returned markdown. Two
    // uses, no receipts required: (1) store it and pass as `if_none_match` next time for a 304-style
    // skip; (2) it is the KV-cache prefix key — an identical hash means byte-identical markdown, so a
    // harness can reuse the cached prompt tokens instead of re-encoding. On unchanged:true the body is
    // empty but this still echoes the matching hash so clients can confirm the conditional hit.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContentHash = null,
    // #6 delta-as-primary: true when delta_only suppressed the full markdown — the empty body is
    // intentional and the consumer reconstructs current content from `diff` + its prior blocks,
    // verifying against `contentHash`. Omitted (null) otherwise.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? DeltaOnly = null,
    // Deterministic identity of the materialization (URL + options that change semantic content).
    // Clients store materializationKey → contentHash (not merely URL → contentHash).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? MaterializationKey = null,
    // PR-F additive semantic dimensions (INV-9). Legacy ok/confidence/focusMatched retain old meanings.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticAccessInfo? Access = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticFocusInfo? Focus = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    Semantics.SemanticCompletenessInfo? Completeness = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Verdict = null);

/// <summary>diff-codec: block-level delta vs a prior set of block hashes.</summary>
public sealed record OccamTranscodeDiffInfo(
    OccamTranscodeDiffBlockInfo[] AddedBlocks,
    string[] RemovedHashes,
    string[] BlockHashes);

public sealed record OccamTranscodeDiffBlockInfo(
    string Hash,
    string Type,
    string Text,
    string SourceSelector);

internal static class OccamTranscodeResponseBuilder
{
    public static OccamTranscodeCompileInfo? BuildCompileInfo(TranscodeOutcome result, OccamTranscodeOptions options)
    {
        var showCompile = options.MaxTokens is not null
            || options.FitMarkdown
            || options.ContentSelectors.Length > 0
            || result.Truncated
            || result.Budget is not null;

        if (!showCompile)
        {
            return null;
        }

        return new OccamTranscodeCompileInfo(
            result.TokensEstimated ?? 0,
            OccamMcp.Core.Compile.TokenEstimator.EstimatorId,
            result.Truncated,
            result.TruncationStrategy,
            result.Omitted is { } om
                ? new OccamTranscodeOmittedInfo(
                    om.Reason,
                    om.TokensDropped,
                    [.. om.Regions],
                    om.SectionsOmitted,
                    om.Structured is { } d
                        ? new OccamTranscodeStructuredDroppedInfo(
                            d.Blocks, d.Tables, d.Chunks, d.Media, d.FeedItems, d.Screenshot)
                        : null)
                : null,
            result.Budget is { } b
                ? new OccamTranscodeBudgetInfo(
                    b.Total, b.Markdown, b.Blocks, b.Tables, b.Chunks, b.Media, b.Feed, b.Receipt)
                : null);
    }

    public static OccamTranscodeTimingsInfo? BuildTimings(TranscodeOutcome result)
    {
        if (result.Timings is null)
        {
            return null;
        }

        var t = result.Timings;
        return new OccamTranscodeTimingsInfo(
            t.TotalMs, t.PreflightMs, t.RouteMs, t.NetworkMs, t.ParseMs, t.PostProcessMs, t.CompileMs);
    }

    public static OccamTranscodeReceiptInfo? BuildReceipt(
        TranscodeOutcome result, string url, ReceiptSigner? signer, TimeAnchorService? timeAnchor = null,
        bool emitCapsule = false, bool leafSetComplete = false)
    {
        var telemetry = new OccamTranscodeReceiptInfo(
            result.TokensEstimated,
            result.TruncationStrategy,
            result.Confidence,
            result.LatencyMs,
            TokenEstimator: OccamMcp.Core.Compile.TokenEstimator.EstimatorId);

        // Receipt v1 (SI-01): sign the extraction envelope when a signer is supplied (OCCAM_RECEIPTS
        // on) and we have real content. Everything hashed here is already computed upstream.
        if (signer is null || !result.Ok || string.IsNullOrEmpty(result.Markdown))
        {
            return telemetry;
        }

        var blocks = result.Blocks is null
            ? Array.Empty<(string, string?)>()
            : [.. result.Blocks.Select(b => (b.Text, (string?)b.SourceSelector))];

        // Compute the ordered leaf hashes once; the signed block root is derived from them (a single
        // block-hashing pass instead of Root() + LeafHashesHex() both re-hashing every block). Blocks
        // are already reconciled to the compiled markdown upstream, so root/leaves/contentHash agree.
        var leaves = blocks.Length > 0 ? MerkleTree.LeafHashesHex(blocks) : null;

        var envelope = new ReceiptEnvelope(
            ReceiptEnvelope.CurrentVersion,
            ReceiptEnvelope.KindExtraction,
            url,
            result.FinalUrl ?? url,
            result.Backend ?? "http",
            DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ReceiptSigner.Toolchain,
            result.PlaybookId is null ? null : new ReceiptPlaybook(result.PlaybookId, result.PlaybookVersion ?? ""),
            ContentHash: ReceiptCanonicalizer.ContentHash(result.Markdown),
            BlockMerkleRoot: leaves is null ? null : MerkleTree.RootFromLeafHashes(leaves),
            // Only attest completeness when there ARE signed leaves to be complete over.
            LeafSetComplete: leafSetComplete && leaves is not null ? true : null,
            Tokens: result.TokensEstimated,
            FailureCode: null,
            StatusCode: null,
            Confidence: result.Confidence,
            KeyId: string.Empty,
            Alg: string.Empty,
            Sig: null);

        var signed = signer.Sign(envelope);

        // SI-15: attach an independent RFC3161 time anchor over the signature when anchoring is on
        // (fail-open — TryAnchor returns null on any error, and the receipt still ships).
        ReceiptTimeAnchor? anchor = null;
        if (timeAnchor is not null && signed.Sig is not null)
        {
            anchor = timeAnchor.TryAnchor(signed.Sig);
        }

        // OKP producer side: package the signed bundle + this markdown into a self-verifying capsule
        // so an agent can hand the fact to a peer that verifies offline (verified hand-off). Opt-in.
        var capsule = emitCapsule
            ? CapsuleCodec.Encode(CapsuleCodec.FromReceipt(signed, result.Markdown, leaves, anchor))
            : null;

        return telemetry with { Signed = signed, BlockLeaves = leaves, TimeAnchor = anchor, Capsule = capsule };
    }

    /// <summary>
    /// Receipt v1 (SI-03): sign a NEGATIVE receipt for provable unavailability — anti-bot challenge,
    /// login/paywall wall, or a 4xx-that-is-a-page-verdict. Returns null for transient errors
    /// (timeout/network/workers), which are not a claim about the page, and when signing is off.
    /// </summary>
    public static ReceiptEnvelope? BuildNegativeReceipt(
        string url, string? finalUrl, string? backend, string code, int statusCode, ReceiptSigner? signer)
    {
        if (signer is null)
        {
            return null;
        }

        var provable = code is "captcha_or_challenge" or "requires_login" or "paywall"
            || statusCode is 401 or 403 or 404 or 410;
        if (!provable)
        {
            return null;
        }

        var envelope = new ReceiptEnvelope(
            ReceiptEnvelope.CurrentVersion,
            ReceiptEnvelope.KindNegative,
            url,
            finalUrl ?? url,
            backend ?? "http",
            DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            ReceiptSigner.Toolchain,
            Playbook: null,
            ContentHash: null,
            BlockMerkleRoot: null,
            Tokens: null,
            FailureCode: code,
            StatusCode: statusCode == 0 ? null : statusCode,
            Confidence: null,
            KeyId: string.Empty,
            Alg: string.Empty,
            Sig: null);

        return signer.Sign(envelope);
    }

    public static OccamTranscodeSessionInfo? BuildSessionInfo(TranscodeOutcome result)
    {
        if (result.Session is null)
        {
            return null;
        }

        return new OccamTranscodeSessionInfo(
            result.Session.ProfileId,
            true,
            result.Session.HeadersApplied);
    }

    public static OccamTranscodeMediaRefInfo[] BuildMediaRefs(TranscodeOutcome result)
    {
        if (result.MediaRefs is null || result.MediaRefs.Count == 0)
        {
            return [];
        }

        var source = result.MediaRefs;
        var mapped = new OccamTranscodeMediaRefInfo[source.Count];
        for (var i = 0; i < source.Count; i++)
        {
            var m = source[i];
            mapped[i] = new OccamTranscodeMediaRefInfo(
                m.Url,
                m.Kind,
                m.Alt,
                m.ContextHeading,
                m.SelectorHint);
        }

        return mapped;
    }
}

[JsonSerializable(typeof(OccamTranscodeMediaRefInfo))]
[JsonSerializable(typeof(OccamTranscodeSessionInfo))]
[JsonSerializable(typeof(OccamTranscodeFixInfo))]
[JsonSerializable(typeof(OccamTranscodeBrowserProvisionedInfo))]
[JsonSerializable(typeof(OccamTranscodeFailureResponse))]
[JsonSerializable(typeof(OccamTranscodeSuccessResponse))]
[JsonSerializable(typeof(OccamTranscodeCompileInfo))]
[JsonSerializable(typeof(OccamTranscodeOmittedInfo))]
[JsonSerializable(typeof(OccamTranscodeBudgetInfo))]
[JsonSerializable(typeof(OccamTranscodeStructuredDroppedInfo))]
[JsonSerializable(typeof(OccamTranscodeReceiptInfo))]
[JsonSerializable(typeof(OccamMcp.Core.Receipts.ReceiptTimeAnchor))]
[JsonSerializable(typeof(OccamTranscodeTimingsInfo))]
[JsonSerializable(typeof(OccamTranscodeRecoveryInfo))]
[JsonSerializable(typeof(OccamTranscodeRecoveryInfo[]))]
[JsonSerializable(typeof(Semantics.SemanticAccessInfo))]
[JsonSerializable(typeof(Semantics.SemanticFocusInfo))]
[JsonSerializable(typeof(Semantics.SemanticCompletenessInfo))]
[JsonSerializable(typeof(OccamTranscodeQualityInfo))]
[JsonSerializable(typeof(OccamTranscodeAgentMetaInfo))]
[JsonSerializable(typeof(OccamTranscodeAgentHintsInfo))]
[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(OccamTransportErrorResponse))]
[JsonSerializable(typeof(Workers.WorkerExtractChunkInfo))]
[JsonSerializable(typeof(Workers.WorkerExtractChunkInfo[]))]
[JsonSerializable(typeof(Workers.WorkerExtractBlockInfo))]
[JsonSerializable(typeof(Workers.WorkerExtractBlockInfo[]))]
[JsonSerializable(typeof(Workers.WorkerExtractBlockLink))]
[JsonSerializable(typeof(Workers.WorkerExtractBlockLink[]))]
[JsonSerializable(typeof(Workers.WorkerExtractTableInfo))]
[JsonSerializable(typeof(Workers.WorkerExtractTableInfo[]))]
[JsonSerializable(typeof(Workers.WorkerExtractTableRecordInfo))]
[JsonSerializable(typeof(Workers.WorkerExtractTableRecordInfo[]))]
[JsonSerializable(typeof(Workers.WorkerExtractTableRowProvenanceInfo))]
[JsonSerializable(typeof(Workers.WorkerExtractFeedInfo))]
[JsonSerializable(typeof(Workers.WorkerExtractFeedItemInfo))]
[JsonSerializable(typeof(Workers.WorkerExtractFeedItemInfo[]))]
[JsonSerializable(typeof(Workers.WorkerExtractMetaInfo))]
[JsonSerializable(typeof(OccamTranscodeDiffInfo))]
[JsonSerializable(typeof(OccamTranscodeDiffBlockInfo))]
[JsonSerializable(typeof(OccamTranscodeDiffBlockInfo[]))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamTranscodeJsonContext : JsonSerializerContext;

public sealed record OccamTransportErrorResponse(string Error, string Message);
