using System.Text.Json;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Digest;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>L8 gate: Agent-First Enhancements AF-1..AF-6 unit + live tests.</summary>
internal static class L8AgentFirstRunner
{
    public static void Run(TranscodePipeline pipeline, DigestService digest, Action<string, bool> assert)
    {
        RunAf1Confidence(assert);
        RunAf2SemanticTranscript(assert);
        RunAf3Receipt(assert);
        RunAf4AutoRecovery(assert);
        RunAf5IntentAwareDigest(assert);
        RunAf6DifferentialResponse(assert);
    }

    // ── AF-1: Confidence scoring ──────────────────────────────────────────

    private static void RunAf1Confidence(Action<string, bool> assert)
    {
        // Rich content → high confidence
        var richMd = string.Join("\n\n", new[]
        {
            "# Main Title",
            "This is a substantial paragraph with enough content to score well on length and density metrics.",
            "## Section One",
            "Another meaningful paragraph with real technical content about software engineering.",
            "## Section Two",
            "More detailed explanation with code references and implementation notes.",
            "- List item one with meaningful text",
            "- List item two with meaningful text",
            "- List item three with meaningful text",
        });
        var richOutcome = new TranscodeOutcome(true, richMd, "https://example.com", "http", null, null, 100, 500, false, null, null, StatusCode: 200, Confidence: 0.0);
        var richConfidence = ExtractQualityEvaluator.ComputeConfidence(richOutcome);
        assert("af1 confidence rich content >= 0.5", richConfidence >= 0.5);
        assert("af1 confidence rich content <= 1.0", richConfidence <= 1.0);

        // Thin content → low confidence
        var thinMd = "Short.";
        var thinOutcome = new TranscodeOutcome(true, thinMd, "https://example.com", "http", null, null, 50, 2, false, null, null, StatusCode: 200, Confidence: 0.0);
        var thinConfidence = ExtractQualityEvaluator.ComputeConfidence(thinOutcome);
        assert("af1 confidence thin content < 0.3", thinConfidence < 0.3);

        // Browser backend scores higher than http for same content
        var browserOutcome = new TranscodeOutcome(true, richMd, "https://example.com", "browser", null, null, 100, 500, false, null, null, StatusCode: 200, Confidence: 0.0);
        var browserConfidence = ExtractQualityEvaluator.ComputeConfidence(browserOutcome);
        assert("af1 confidence browser >= http for same content", browserConfidence >= richConfidence);

        // Truncated content scores lower
        var truncatedOutcome = new TranscodeOutcome(true, richMd, "https://example.com", "http", null, null, 100, 200, true, "head_safe", null, StatusCode: 200, Confidence: 0.0);
        var truncatedConfidence = ExtractQualityEvaluator.ComputeConfidence(truncatedOutcome);
        assert("af1 confidence truncated < non-truncated", truncatedConfidence < richConfidence);

        // Confidence is serialized in transcode success response
        var transcodeResponse = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com", "https://example.com"),
            richMd,
            "http",
            [],
            null,
            null,
            richConfidence,
            null,
            null,
            null);
        var json = JsonSerializer.Serialize(transcodeResponse, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("af1 confidence in transcode response JSON", json.Contains("\"confidence\"", StringComparison.OrdinalIgnoreCase));

        // Confidence is serialized in digest item info
        var digestItem = new OccamDigestItemInfo("https://example.com", true, "Title", "Excerpt", "http", 100, null, null, null, [], richConfidence, null);
        var digestJson = JsonSerializer.Serialize(digestItem, OccamDigestJsonContext.Default.OccamDigestItemInfo);
        assert("af1 confidence in digest item JSON", digestJson.Contains("\"confidence\"", StringComparison.OrdinalIgnoreCase));
    }

    // ── AF-2: Semantic transcript ─────────────────────────────────────────

    private static void RunAf2SemanticTranscript(Action<string, bool> assert)
    {
        // FitMarkdown with focus produces SNIP markers for dropped sections
        var md = string.Join("\n\n", new[]
        {
            "# API Reference",
            "This is the intro paragraph with enough words to pass the minimum length filter.",
            "## Authentication",
            "Use bearer tokens for API access. Send the Authorization header on every request for authentication.",
            "## Rate Limiting",
            "Requests are limited to one thousand per minute per key. This is a short paragraph.",
            "## Deprecated",
            "Old API v1 endpoints. These are no longer supported and should not be used in new projects.",
            "## Obsolete",
            "Legacy endpoints from 2019. These have been removed and return 404 responses now.",
        });

        var fitted = FitMarkdown.Apply(md, "authentication bearer tokens");
        assert("af2 fit_markdown produces SNIP markers", fitted.Contains("<!-- SNIP:", StringComparison.Ordinal));
        assert("af2 SNIP markers contain reason", fitted.Contains("reason:", StringComparison.OrdinalIgnoreCase));
        assert("af2 keeps focused section", fitted.Contains("Authentication", StringComparison.OrdinalIgnoreCase));

        // TokenBudget truncation produces SNIP markers
        var longText = string.Join("\n\n", Enumerable.Range(0, 30).Select(i =>
            $"## Section {i}\n\nThis is paragraph {i} with enough words to consume tokens and be meaningful."));
        var (truncated, didTruncate, strategy) = TokenBudget.Apply(longText, 128);
        assert("af2 token truncation produces SNIP", truncated.Contains("<!-- SNIP:", StringComparison.Ordinal));
        assert("af2 token truncation strategy is head_safe", strategy == "head_safe");
        assert("af2 token truncation SNIP contains head_safe reason", truncated.Contains("reason: head_safe", StringComparison.OrdinalIgnoreCase));

        // Focus-centered truncation produces SNIP for unchosen sections
        var focusText = string.Join("\n\n", Enumerable.Range(0, 20).Select(i =>
            $"## Topic {i}\n\nDetail paragraph {i} with enough words to consume tokens and stay relevant."));
        var (focusTruncated, focusDidTruncate, focusStrategy) = TokenBudget.Apply(focusText, 96, "Topic 5");
        assert("af2 focus truncation produces SNIP", focusTruncated.Contains("<!-- SNIP:", StringComparison.Ordinal));
        assert("af2 focus truncation strategy is focus_window", focusStrategy == "focus_window");
        assert("af2 focus truncation SNIP mentions unchosen", focusTruncated.Contains("unchosen", StringComparison.OrdinalIgnoreCase));

        // Sandwich truncation produces SNIP marker
        var sandwichText = string.Join("\n\n", Enumerable.Range(0, 40).Select(i =>
            $"## Part {i}\n\nContent paragraph {i} with enough words to fill the token budget completely."));
        var (_, sandwichDidTruncate, _) = TokenBudget.Apply(sandwichText, 64);
        assert("af2 sandwich truncation happens", sandwichDidTruncate);
    }

    // ── AF-3: Knowledge receipt ───────────────────────────────────────────

    private static void RunAf3Receipt(Action<string, bool> assert)
    {
        // Transcode receipt is present in success response
        var receipt = new OccamTranscodeReceiptInfo(
            512,
            "head_safe",
            0.75,
            1200,
            TokenEstimator: OccamMcp.Core.Compile.TokenEstimator.EstimatorId);
        var response = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com", "https://example.com"),
            "# Title",
            "http",
            [],
            null,
            null,
            0.75,
            Quality: null,
            Receipt: receipt,
            Recovery: null,
            Unchanged: null);
        var json = JsonSerializer.Serialize(response, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("af3 transcode receipt in JSON", json.Contains("\"receipt\"", StringComparison.OrdinalIgnoreCase));
        assert("af3 token estimator provenance in JSON", json.Contains("\"tokenEstimator\":\"heuristic-unicode-v1\"", StringComparison.Ordinal));
        assert("af3 receipt tokensUsed in JSON", json.Contains("\"tokensUsed\"", StringComparison.OrdinalIgnoreCase));
        assert("af3 receipt confidence in JSON", json.Contains("\"confidence\"", StringComparison.OrdinalIgnoreCase));
        assert("af3 receipt elapsedMs in JSON", json.Contains("\"elapsedMs\"", StringComparison.OrdinalIgnoreCase));

        // Knowledge extract receipt
        var knowledgeReceipt = new OccamExtractKnowledgeReceiptInfo(0.92, 842);
        var knowledgeResponse = new OccamExtractKnowledgeSuccessResponse(
            true,
            "https://example.com",
            "example",
            "docs",
            [],
            new OccamExtractKnowledgeMetaInfo("ko123"),
            842,
            "http",
            0.92,
            knowledgeReceipt);
        var knowledgeJson = JsonSerializer.Serialize(knowledgeResponse, OccamExtractKnowledgeJsonContext.Default.OccamExtractKnowledgeSuccessResponse);
        assert("af3 knowledge receipt in JSON", knowledgeJson.Contains("\"receipt\"", StringComparison.OrdinalIgnoreCase));

        // Digest item receipt
        var digestItem = new OccamDigestItemInfo(
            "https://example.com", true, "Title", "Excerpt", "http", 420,
            null, null, null, [], 0.85,
            new OccamTranscodeReceiptInfo(
                420,
                null,
                0.85,
                320,
                TokenEstimator: OccamMcp.Core.Compile.TokenEstimator.EstimatorId));
        var digestItemJson = JsonSerializer.Serialize(digestItem, OccamDigestJsonContext.Default.OccamDigestItemInfo);
        assert("af3 digest item receipt in JSON", digestItemJson.Contains("\"receipt\"", StringComparison.OrdinalIgnoreCase));
    }

    // ── AF-4: Auto-recovery ───────────────────────────────────────────────

    private static void RunAf4AutoRecovery(Action<string, bool> assert)
    {
        // Recovery info record serializes correctly
        var recovery = new OccamTranscodeRecoveryInfo("http", true, 1240);
        var recoveryJson = JsonSerializer.Serialize(recovery, OccamTranscodeJsonContext.Default.OccamTranscodeRecoveryInfo);
        assert("af4 recovery backend in JSON", recoveryJson.Contains("\"backend\"", StringComparison.OrdinalIgnoreCase));
        assert("af4 recovery ok in JSON", recoveryJson.Contains("\"ok\"", StringComparison.OrdinalIgnoreCase));
        assert("af4 recovery latencyMs in JSON", recoveryJson.Contains("\"latencyMs\"", StringComparison.OrdinalIgnoreCase));

        // Recovery array in transcode response
        var responseWithRecovery = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com", "https://example.com"),
            "# Title",
            "browser",
            [],
            null,
            null,
            0.8,
            Quality: null,
            Receipt: null,
            Recovery: [new OccamTranscodeRecoveryInfo("http", false, 35000), new OccamTranscodeRecoveryInfo("browser", true, 2400)],
            Unchanged: null);
        var json = JsonSerializer.Serialize(responseWithRecovery, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("af4 recovery array in JSON", json.Contains("\"recovery\"", StringComparison.OrdinalIgnoreCase));
        assert("af4 recovery shows http attempt", json.Contains("\"http\"", StringComparison.OrdinalIgnoreCase));
        assert("af4 recovery shows browser attempt", json.Contains("\"browser\"", StringComparison.OrdinalIgnoreCase));
    }

    // ── AF-5: Intent-aware digest ─────────────────────────────────────────

    private static void RunAf5IntentAwareDigest(Action<string, bool> assert)
    {
        // DigestAnalysis with sourceUrl and discoveredLinks
        var analysis = new DigestAnalysis(
            true,
            "abc123",
            [
                new DigestItemResult("https://nginx.org/en/docs/", true, "nginx docs", "## nginx", "http", 420, null, null, null, null, [], 0.85, null, 320),
            ],
            "## nginx docs\n\n## nginx",
            1, 1, 0, 420, null, null,
            "https://nginx.org/en/docs/",
            ["https://nginx.org/en/docs/", "https://nginx.org/en/docs/syntax.html"]);

        var response = OccamDigestResponseMapper.MapSuccess(analysis);
        assert("af5 sourceUrl in digest response", response.SourceUrl == "https://nginx.org/en/docs/");
        assert("af5 discoveredLinks in digest response", response.DiscoveredLinks is { Length: > 0 });

        // DiscoveredLinks serialize correctly
        var json = JsonSerializer.Serialize(response, OccamDigestJsonContext.Default.OccamDigestSuccessResponse);
        assert("af5 discoveredLinks in JSON", json.Contains("\"discoveredLinks\"", StringComparison.OrdinalIgnoreCase));
        assert("af5 sourceUrl in JSON", json.Contains("\"sourceUrl\"", StringComparison.OrdinalIgnoreCase));

        // DigestDiscoveredLinkInfo serializes correctly
        var linkInfo = new OccamDigestDiscoveredLinkInfo("https://example.com/page");
        var linkJson = JsonSerializer.Serialize(linkInfo, OccamDigestJsonContext.Default.OccamDigestDiscoveredLinkInfo);
        assert("af5 discoveredLink has url", linkJson.Contains("\"url\"", StringComparison.OrdinalIgnoreCase));

        // Digest without sourceUrl has null discoveredLinks
        var plainAnalysis = new DigestAnalysis(true, "def456", [], "## combined", 0, 0, 0, 0, null, null);
        var plainResponse = OccamDigestResponseMapper.MapSuccess(plainAnalysis);
        assert("af5 no sourceUrl → null discoveredLinks", plainResponse.DiscoveredLinks is null);
        assert("af5 no sourceUrl → null sourceUrl", plainResponse.SourceUrl is null);
    }

    // ── AF-6: Differential response ──────────────────────────────────────

    private static void RunAf6DifferentialResponse(Action<string, bool> assert)
    {
        // Unchanged response has true, empty markdown, echoed contentHash + materializationKey,
        // and no heavy sidecars (whole-response conditional economy).
        const string priorMd = "# Prior\n\nStable page body.";
        var priorHash = OccamMcp.Core.Compile.ContentHashToken.BareHex(priorMd);
        var matKey = OccamMcp.Core.Compile.MaterializationKey.Compute(
            "https://example.com",
            "http",
            OccamMcp.Core.Routing.OccamTranscodeOptions.Default);
        var unchangedResponse = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com", "https://example.com"),
            string.Empty,
            "http",
            [],
            Unchanged: true,
            ContentHash: priorHash,
            MaterializationKey: matKey);
        var unchangedJson = JsonSerializer.Serialize(unchangedResponse, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("af6 unchanged in JSON", jsonContains(unchangedJson, "\"unchanged\":true"));
        assert("af6 unchanged has empty markdown", jsonContains(unchangedJson, "\"markdown\":\"\""));

        // Normal response has null unchanged
        var normalResponse = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com", "https://example.com"),
            "# Title\n\nContent",
            "http",
            [],
            null,
            null,
            0.8,
            null,
            null,
            null);
        var normalJson = JsonSerializer.Serialize(normalResponse, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("af6 normal response unchanged is null", !normalJson.Contains("\"unchanged\"", StringComparison.OrdinalIgnoreCase));

        // #11 KV-cache-stable-prefix: a normal success carries a bare-hex contentHash the client can
        // store (if_none_match) and use as a KV-cache prefix key; it round-trips as an if_none_match
        // token. On unchanged:true the body is empty but contentHash is still echoed with materializationKey.
        const string cacheMd = "# Title\n\nStable body for cache keying.";
        var cacheHash = OccamMcp.Core.Compile.ContentHashToken.BareHex(cacheMd);
        var cacheResponse = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com", "https://example.com"),
            cacheMd,
            "http",
            [],
            null,
            null,
            0.9,
            ContentHash: cacheHash);
        var cacheJson = JsonSerializer.Serialize(cacheResponse, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("11 contentHash in JSON", jsonContains(cacheJson, "\"contentHash\":\"" + cacheHash + "\""));
        assert("11 contentHash is 64-hex", cacheHash.Length == 64);
        assert("11 contentHash round-trips as if_none_match", OccamMcp.Core.Compile.ContentHashToken.Matches(cacheMd, cacheHash));
        assert("11 identical content -> identical key (KV-prefix)", OccamMcp.Core.Compile.ContentHashToken.BareHex(cacheMd) == cacheHash);
        assert("11 different content -> different key", OccamMcp.Core.Compile.ContentHashToken.BareHex(cacheMd + " x") != cacheHash);
        assert("11 unchanged body echoes contentHash", unchangedJson.Contains("\"contentHash\"", StringComparison.OrdinalIgnoreCase));
        assert("11 unchanged body has materializationKey", unchangedJson.Contains("\"materializationKey\"", StringComparison.OrdinalIgnoreCase));
        assert("11 unchanged omits blocks sidecar", !unchangedJson.Contains("\"blocks\"", StringComparison.OrdinalIgnoreCase)
            || unchangedJson.Contains("\"blocks\":null", StringComparison.OrdinalIgnoreCase));

        // DigestAnalysis with unchanged
        var unchangedDigest = new DigestAnalysis(
            true, "abc", [], string.Empty, 0, 0, 0, 0, null, null,
            null, null, true);
        var unchangedDigestResponse = OccamDigestResponseMapper.MapSuccess(unchangedDigest);
        assert("af6 digest unchanged is true", unchangedDigestResponse.Unchanged == true);
        assert("af6 digest unchanged has empty combined", unchangedDigestResponse.Combined == string.Empty);

        // IfNoneMatch option parses correctly
        var ifNoneMatchOption = new OccamTranscodeOptions { IfNoneMatch = "abc123def456" };
        assert("af6 IfNoneMatch option set", ifNoneMatchOption.IfNoneMatch == "abc123def456");

        var defaultOption = OccamTranscodeOptions.Default;
        assert("af6 IfNoneMatch default is null", defaultOption.IfNoneMatch is null);
    }

    private static bool jsonContains(string json, string expected) =>
        json.Contains(expected, StringComparison.Ordinal);
}
