using System.Text.Json;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L2DigestUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        assert("digest parser json array", DigestUrlParser.TryParse(
            "[\"https://example.com/a\",\"https://example.com/b\"]",
            out var entries,
            out _) && entries.Count == 2);

        assert("digest parser rejects empty", !DigestUrlParser.TryParse("[]", out _, out var emptyError)
            && emptyError is not null);

        assert("digest parser rejects invalid url", !DigestUrlParser.TryParse(
            "[\"not-a-url\"]",
            out _,
            out _));

        assert("digest max_urls cap", DigestService.MaxUrlsCap == 8);

        // urls / source_url MCP input contract (schema: neither is required alone; at least one needed).
        assert(
            "digest contract: urls only ok",
            DigestInputContract.TryValidate(
                JsonSerializer.SerializeToElement("[\"https://example.com/\"]"), null, out var useSrcUrlsOnly, out _, out _)
            && !useSrcUrlsOnly);
        assert(
            "digest contract: source_url only ok",
            DigestInputContract.TryValidate(
                (JsonElement?)null, "https://example.com/", out var useSrcSourceOnly, out _, out _)
            && useSrcSourceOnly);
        assert(
            "digest contract: both → source_url wins (urls ignored)",
            DigestInputContract.TryValidate(
                JsonSerializer.SerializeToElement("[\"https://ignored.example/\"]"), "https://example.com/", out var useSrcBoth, out _, out _)
            && useSrcBoth);
        assert(
            "digest contract: neither → invalid_arguments",
            !DigestInputContract.TryValidate((JsonElement?)null, null, out _, out var neitherCode, out var neitherMsg)
            && neitherCode == "invalid_arguments"
            && neitherMsg == DigestInputContract.NeitherMessage);
        assert(
            "digest contract: empty urls + empty source → invalid_arguments",
            !DigestInputContract.TryValidate((JsonElement?)null, "", out _, out var emptyCode, out _)
            && emptyCode == "invalid_arguments");
        assert(
            "digest contract: whitespace urls with source_url → source wins",
            DigestInputContract.TryValidate(JsonSerializer.SerializeToElement("   "), "https://example.com/", out var useSrcWs, out _, out _)
            && useSrcWs);

        // Typed failure envelope for neither (tool SerializeFailure shape).
        var neitherJson = JsonSerializer.Serialize(
            new OccamDigestFailureResponse(
                false,
                "invalid_arguments",
                DigestInputContract.NeitherMessage,
                null,
                null,
                null,
                null,
                "2026-01-01T00:00:00Z"),
            OccamDigestJsonContext.Default.OccamDigestFailureResponse);
        assert(
            "digest contract neither failure is typed",
            neitherJson.Contains("\"ok\":false", StringComparison.Ordinal)
                && neitherJson.Contains("\"failureCode\":\"invalid_arguments\"", StringComparison.Ordinal)
                && neitherJson.Contains("urls and/or source_url", StringComparison.Ordinal));
        assert(
            "digest contract empty-discovery message is stable",
            DigestInputContract.EmptyDiscoveryMessage.Contains("discoverable links", StringComparison.Ordinal));

        assert("digest parallel http default", ResolveDigestParallel(OccamBackendPolicy.Http, 8) == 4);
        assert("digest parallel http single", ResolveDigestParallel(OccamBackendPolicy.Http, 1) == 1);
        assert(
            "digest parallel browser capped",
            ResolveDigestParallel(OccamBackendPolicy.Browser, 8) <= BrowserConcurrencyGate.MaxParallel);
        assert("digest parallel opt out", WithDigestParallelEnv("0", null, () =>
            DigestParallelism.ResolveMaxParallel(OccamBackendPolicy.Http, 8) == 1));

        assert("digest id stable", DigestService.ComputeDigestId(["https://b.example/", "https://a.example/"])
            == DigestService.ComputeDigestId(["https://a.example", "https://b.example/"]));

        var mdnExcerpt = """
            ## Grammar and types

            - [Basic syntax & comments](/en-US/docs/Web/JavaScript/Guide/Grammar_and_types)
            """;
        assert("digest focus mdn weak syntax only", !FocusMatcher.MatchesForDigest(mdnExcerpt, "configuration syntax"));
        assert(
            "digest focus mdn tier none",
            FocusMatcher.EvaluateForDigest(mdnExcerpt, "configuration syntax").Tier == "none");

        var nginxExcerpt = """
            ## Configuration file measurement units

            [syntax.html](http://nginx.org/en/docs/syntax.html) — configuration directives
            """;
        assert("digest focus nginx configuration syntax", FocusMatcher.MatchesForDigest(nginxExcerpt, "configuration syntax"));
        assert(
            "digest focus nginx tier ideal",
            FocusMatcher.EvaluateForDigest(nginxExcerpt, "configuration syntax").Tier is "ideal" or "phrase");

        // --- focusMatched scoring matrix ---
        // Ideal: contiguous phrase.
        var phraseExcerpt = "This page covers configuration syntax for nginx modules in depth.";
        var phraseEval = FocusMatcher.EvaluateForDigest(phraseExcerpt, "configuration syntax");
        assert("focus ideal phrase matched", phraseEval.Matched);
        assert("focus ideal phrase tier", phraseEval.Tier == "phrase");
        assert("focus ideal phrase score 1", phraseEval.Score >= 1.0);

        // Ideal: all terms via stem (excerpt uses Configure, query asks configuration).
        var stemExcerpt = """
            ## Configure the module

            Follow the syntax rules when you configure directives.
            """;
        var stemEval = FocusMatcher.EvaluateForDigest(stemExcerpt, "configuration syntax");
        assert("focus ideal stem matched", stemEval.Matched);
        assert("focus ideal stem was false under old all-substring rule", stemEval.Hits == 2);
        assert("focus ideal stem tier", stemEval.Tier is "ideal" or "phrase");

        // Partial: 3-term query, 2 of 3 present (no accidental single-word hub hit).
        var partialExcerpt = "PostgreSQL SELECT statement syntax and examples for beginners.";
        var partialEval = FocusMatcher.EvaluateForDigest(partialExcerpt, "select query syntax");
        assert("focus partial matched", partialEval.Matched);
        assert("focus partial tier", partialEval.Tier == "partial");
        assert("focus partial hits 2 of 3", partialEval.Hits == 2 && partialEval.Terms == 3);
        assert("focus partial threshold", FocusMatcher.PartialHitThreshold(3) == 2);

        // No match: hub noise shares one weak word only.
        var noneEval = FocusMatcher.EvaluateForDigest(mdnExcerpt, "configuration syntax");
        assert("focus none matched", !noneEval.Matched);
        assert("focus none tier", noneEval.Tier == "none");

        // Synonyms: auth ≈ authentication (still needs the other term on a 2-word query).
        var synExcerpt = "Enable authentication before calling the protected API endpoint.";
        var synEval = FocusMatcher.EvaluateForDigest(synExcerpt, "auth token");
        assert("focus synonym auth alone not enough", !synEval.Matched && synEval.Hits == 1);
        assert("focus synonym two-term needs both", !FocusMatcher.MatchesForDigest(synExcerpt, "auth token"));
        var synBoth = FocusMatcher.EvaluateForDigest(
            "Enable authentication and rotate API tokens monthly.", "auth token");
        assert("focus synonym both terms", synBoth.Matched && synBoth.Tier is "ideal" or "phrase");
        assert("focus synonym auth resolved", synBoth.Hits == 2);

        var dockerHub = """
            # Get started | Docker Docs

            # Get started

            If you're new to Docker, this section guides you through the essential resources to get started.

            Follow the guides to help you get started and learn how Docker can optimize your development workflows.
            """;
        var dockerFit = FitMarkdown.Apply(dockerHub, "select query syntax");
        assert("digest fit docker hub fallback non-empty", !string.IsNullOrWhiteSpace(dockerFit));
        assert("digest fit docker hub no js shell", !dockerFit.Contains("getCurrentPlaintextUrl", StringComparison.Ordinal));
        assert("digest focus docker select query syntax", !FocusMatcher.MatchesForDigest(dockerFit, "select query syntax"));
        assert(
            "digest focus docker tier none",
            FocusMatcher.EvaluateForDigest(dockerFit, "select query syntax").Tier == "none");

        var item = new OccamDigestItemInfo(
            "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide",
            true,
            "JavaScript Guide",
            "TOC excerpt",
            "local_http",
            120,
            null,
            "configuration syntax",
            false,
            []);
        var json = JsonSerializer.Serialize(item, OccamDigestJsonContext.Default.OccamDigestItemInfo);
        assert("digest focusMatched false serialized", json.Contains("\"focusMatched\":false", StringComparison.Ordinal));

        var analysis = new DigestAnalysis(
            true,
            "abc123",
            [
                new DigestItemResult(
                    "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide",
                    true,
                    "JavaScript Guide",
                    "## Guide\n\n* [Functions](url)",
                    "http",
                    100,
                    null,
                    null,
                    "configuration syntax",
                    false),
                new DigestItemResult(
                    "https://nginx.org/en/docs/",
                    true,
                    "nginx",
                    "[syntax.html](http://nginx.org/en/docs/syntax.html)",
                    "http",
                    80,
                    null,
                    null,
                    "configuration syntax",
                    true),
            ],
            "## combined",
            2,
            2,
            0,
            180,
            null,
            null);
        var response = OccamDigestResponseMapper.MapSuccess(analysis);
        var responseJson = JsonSerializer.Serialize(response, OccamDigestJsonContext.Default.OccamDigestSuccessResponse);
        assert("digest agentHints serialized", responseJson.Contains("\"agentHints\"", StringComparison.Ordinal));
        assert("digest agentHints check combined warning", responseJson.Contains("check_items_before_combined", StringComparison.Ordinal));

        // All focusMatched=false → focus_not_found honesty.
        var unfocused = new DigestAnalysis(
            true,
            "nofocus",
            [
                new DigestItemResult(
                    "https://docs.python.org/3.12/",
                    true,
                    "3.12",
                    "Python 3.12 documentation root",
                    "http",
                    40,
                    null,
                    null,
                    "asyncio event loop",
                    false),
                new DigestItemResult(
                    "https://docs.python.org/3.11/",
                    true,
                    "3.11",
                    "Python 3.11 documentation root",
                    "http",
                    40,
                    null,
                    null,
                    "asyncio event loop",
                    false),
            ],
            "## combined",
            2,
            2,
            0,
            80,
            null,
            null,
            FocusNotFound: true);
        var unfocusedHints = DigestAgentHints.ForDigest(unfocused);
        assert(
            "digest focus_not_found warning",
            unfocusedHints.Warnings.Any(w => w.StartsWith("focus_not_found:", StringComparison.Ordinal)));
        assert(
            "digest focus_not_found decision",
            unfocusedHints.Decisions.Any(d => d.Action == "focus_not_found"));
        assert("digest focus_not_found read order", unfocusedHints.SuggestedReadOrder == "items_only");
        var unfocusedJson = JsonSerializer.Serialize(
            OccamDigestResponseMapper.MapSuccess(unfocused),
            OccamDigestJsonContext.Default.OccamDigestSuccessResponse);
        assert("digest focus_not_found in agentHints json", unfocusedJson.Contains("focus_not_found", StringComparison.Ordinal));

        // SI-01 flagship for the research path: an ok digest item carries a signed receipt envelope,
        // serialized under receipt.signed (so it's independently verifiable, like a single transcode).
        var digestSigner = ReceiptSigner.CreateEphemeral();
        var itemEnvelope = digestSigner.Sign(new ReceiptEnvelope(
            ReceiptEnvelope.CurrentVersion, ReceiptEnvelope.KindExtraction,
            "https://nginx.org/en/docs/", "https://nginx.org/en/docs/", "http",
            "2026-01-01T00:00:00Z", "ff-occam/test", null,
            ContentHash: ReceiptCanonicalizer.ContentHash("nginx body"), BlockMerkleRoot: null, Tokens: 80,
            FailureCode: null, StatusCode: null, Confidence: 0.9, KeyId: "", Alg: "", Sig: null));
        var signedAnalysis = new DigestAnalysis(true, "abc0", [
            new DigestItemResult("https://nginx.org/en/docs/", true, "nginx", "nginx body", "http", 80,
                null, null, null, null, null, 0.9, null, 0, itemEnvelope),
        ], "## nginx", 1, 1, 0, 80, null, null);
        var signedJson = JsonSerializer.Serialize(
            OccamDigestResponseMapper.MapSuccess(signedAnalysis), OccamDigestJsonContext.Default.OccamDigestSuccessResponse);
        assert("digest item carries a signed receipt",
            signedJson.Contains("\"signed\"", StringComparison.Ordinal)
                && signedJson.Contains(itemEnvelope.KeyId, StringComparison.Ordinal)
                && signedJson.Contains(itemEnvelope.Sig!, StringComparison.Ordinal));
    }

    private static int ResolveDigestParallel(OccamBackendPolicy policy, int urlCount) =>
        WithDigestParallelEnv(null, null, () => DigestParallelism.ResolveMaxParallel(policy, urlCount));

    private static bool WithDigestParallelEnv(string? parallel, string? maxParallel, Func<bool> test)
    {
        var savedParallel = Environment.GetEnvironmentVariable("OCCAM_DIGEST_PARALLEL");
        var savedMax = Environment.GetEnvironmentVariable("OCCAM_DIGEST_MAX_PARALLEL");
        Environment.SetEnvironmentVariable("OCCAM_DIGEST_PARALLEL", parallel);
        Environment.SetEnvironmentVariable("OCCAM_DIGEST_MAX_PARALLEL", maxParallel);
        try
        {
            return test();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_DIGEST_PARALLEL", savedParallel);
            Environment.SetEnvironmentVariable("OCCAM_DIGEST_MAX_PARALLEL", savedMax);
        }
    }

    private static int WithDigestParallelEnv(string? parallel, string? maxParallel, Func<int> test)
    {
        var savedParallel = Environment.GetEnvironmentVariable("OCCAM_DIGEST_PARALLEL");
        var savedMax = Environment.GetEnvironmentVariable("OCCAM_DIGEST_MAX_PARALLEL");
        Environment.SetEnvironmentVariable("OCCAM_DIGEST_PARALLEL", parallel);
        Environment.SetEnvironmentVariable("OCCAM_DIGEST_MAX_PARALLEL", maxParallel);
        try
        {
            return test();
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_DIGEST_PARALLEL", savedParallel);
            Environment.SetEnvironmentVariable("OCCAM_DIGEST_MAX_PARALLEL", savedMax);
        }
    }
}
