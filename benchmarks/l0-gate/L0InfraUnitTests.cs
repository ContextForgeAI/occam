using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using System.Net;
using OccamMcp.Core.Batch;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Caching;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Playbooks;
using System.Collections.Frozen;
using OccamMcp.Core.Probe;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Text;
using OccamMcp.Core.Telemetry;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace OccamMcp.L0Gate;

internal sealed class Utf8ProbeDto
{
    [JsonPropertyName("markdown")]
    public string? Markdown { get; init; }
}

internal sealed class GoldenHostsDto
{
    [JsonPropertyName("hosts")]
    public string[]? Hosts { get; init; }

    [JsonPropertyName("browserRecipes")]
    public string[]? BrowserRecipes { get; init; }
}

[JsonSerializable(typeof(Utf8ProbeDto))]
[JsonSerializable(typeof(GoldenHostsDto))]
internal partial class Utf8ProbeJsonContext : JsonSerializerContext;

internal static class L0InfraUnitTests
{
    public static void Run(WorkerPaths paths, Action<string, bool> assert)
    {
        RunUtf8WorkerProbe(assert);
        RunWorkerProcessLifecycle(assert);
        RunOccamLogger(assert, paths);
        RunExtractQuality(assert);
        RunServerInstructions(assert);
        RunOccamToolProfile(assert);
        RunClientCapabilityBudget(assert);
        RunProbeAgentHints(assert);
        RunInfraScripts(assert);
        RunBrowserConcurrencyInfra(assert);
        RunBrowserPoolInfra(assert, paths);
        RunProxyRotationInfra(assert);
        RunBatchJobInfra(assert);
        RunTranscodeCacheInfra(assert);
        RunJsonBlocksContract(assert);
        RunDiffCodecContract(assert);
        RunKnowledgeCodecRegistry(assert);
        RunMaterializationPlanner(assert);
        RunCodecBench(assert);
        RunJsonKnowledgeCodec(assert);
        RunPlannerBench(assert);
        RunCanonicalKnowledgeDomain(assert);
        RunLegacyTranscodeToCanonical(assert);
        RunMaterializedProvenanceResolver(assert);
        RunRuntimeMaterializationMigration(assert);
        RunVectorizedHtmlScanner(assert);
        RunHtmlStreamScanner(assert);
        RunHtmlHeadScanner(assert);
        RunHtmlVisibleTextScanner(assert);
        RunTierAAllocBudget(assert);
    }

    private static void RunTierAAllocBudget(Action<string, bool> assert)
    {
        const string html =
            """
            <html><head><title>T</title><meta property="og:title" content="Hello"/></head>
            <body><a href="/docs/guide">Docs</a><a href="/api">API</a></body></html>
            """;

        GC.Collect();
        var before = GC.GetAllocatedBytesForCurrentThread();
        _ = HtmlHeadScanner.Scan(html.AsSpan());
        var headScanAlloc = GC.GetAllocatedBytesForCurrentThread() - before;
        assert("tier-a alloc head scan zero", headScanAlloc == 0);

        before = GC.GetAllocatedBytesForCurrentThread();
        _ = HtmlLinkExtractor.Extract(html, "https://example.com", maxLinks: 8);
        var mapAlloc = GC.GetAllocatedBytesForCurrentThread() - before;
        assert("tier-a alloc map extract under 16kb", mapAlloc < 16 * 1024);
    }

    private static void RunUtf8WorkerProbe(Action<string, bool> assert)
    {
        var node = NodeRuntime.ResolveExecutable();
        var psi = new ProcessStartInfo
        {
            FileName = node,
            Arguments = "-e \"console.log(JSON.stringify({markdown:'\\u201c tokenization\\u201d'}))\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = WorkerProcessGroup.Start(psi);
        assert("worker utf8 spawn", proc is not null);
        if (proc is null)
        {
            return;
        }

        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(5000);
        WorkerProcessGroup.Release(proc);

        var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(stdout);
        var md = jsonLine is not null
            ? JsonSerializer.Deserialize(jsonLine, Utf8ProbeJsonContext.Default.Utf8ProbeDto)?.Markdown ?? string.Empty
            : string.Empty;

        assert("worker utf8 smart open quote", md.Contains('\u201c', StringComparison.Ordinal));
        assert("worker utf8 smart close quote", md.Contains('\u201d', StringComparison.Ordinal));
        assert("worker utf8 no cp1251 mojibake", !md.Contains("тА", StringComparison.Ordinal));
    }

    private static void RunWorkerProcessLifecycle(Action<string, bool> assert)
    {
        var baseline = WorkerProcessGroup.ActivePidCountForTests;
        Process? process = WorkerProcessGroup.Start(new ProcessStartInfo
        {
            FileName = NodeRuntime.ResolveExecutable(),
            Arguments = "-e \"setInterval(() => {}, 1000)\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        });

        assert("worker lifecycle spawn", process is not null);
        if (process is null)
        {
            return;
        }

        assert("worker lifecycle registers pid", WorkerProcessGroup.ActivePidCountForTests == baseline + 1);
        NodeWorkerLifecycle.TerminateAndDispose(process);
        process = null;
        assert("worker lifecycle releases pid on terminate", WorkerProcessGroup.ActivePidCountForTests == baseline);
    }

    private static void RunOccamLogger(Action<string, bool> assert, WorkerPaths paths)
    {
#if OCCAM_GATE
        OccamLogger.ResetForTests();
#endif
        var occamHeader = OccamLogger.FormatHeader();
        assert("occam header glyph", occamHeader.Contains('⌥', StringComparison.Ordinal));
        assert("occam header brand", occamHeader.Contains("F F", StringComparison.Ordinal));
        assert("occam header white ansi", occamHeader.Contains("\u001b[38;5;255m", StringComparison.Ordinal));
        assert("occam header width", OccamLogger.VisibleLength(occamHeader) <= OccamLogger.MaxWidth);

        var shredderLine = OccamLogger.FormatShredderLine(91.4, 100_000, 8_600);
        assert("occam shredder tag", shredderLine.Contains("[SHREDDER]", StringComparison.Ordinal));
        assert("occam shredder cyan", shredderLine.Contains("\u001b[38;5;45m", StringComparison.Ordinal));
        assert("occam shredder gray", shredderLine.Contains("\u001b[38;5;244m", StringComparison.Ordinal));
        var shredderVisible = Regex.Replace(shredderLine, @"\u001b\[[0-9;]*m", string.Empty);
        assert(
            "occam shredder blocks",
            shredderVisible.Count(c => c is '█' or '░') == OccamLogger.ShredderBlocks);
        assert("occam shredder width", OccamLogger.VisibleLength(shredderLine) <= OccamLogger.MaxWidth);

        var lowSavings = new OccamTelemetry(100_000, 91_430, 8.6, 0.01, 120, "local_http", "example.com");
        var highSavings = new OccamTelemetry(500_000, 50_000, 90.0, 0.05, 120, "local_http", "example.com");
        var lowSavingsLine = OccamLogger.FormatSavingsLine(lowSavings);
        var highSavingsLine = OccamLogger.FormatSavingsLine(highSavings);
        assert("occam roi low gray", lowSavingsLine.Contains("\u001b[38;5;244m", StringComparison.Ordinal));
        assert("occam roi high cyan", highSavingsLine.Contains("\u001b[38;5;45m", StringComparison.Ordinal));
        assert("occam savings width", OccamLogger.VisibleLength(lowSavingsLine) <= OccamLogger.MaxWidth);

        var occamTelemetry = OccamLogger.ComputeTelemetry(
            new TranscodeResult(true, "https://example.com/docs", string.Empty, 40_000, 240, "local_http"),
            new string('x', 400));
        assert("occam telemetry cut positive", occamTelemetry.ContextCutPercent > 0);

#if OCCAM_GATE
        OccamLogger.ForceEnabledForTests(false);
        assert("occam log disabled by default", !OccamLogger.IsEnabled);
        OccamLogger.ResetForTests();
#endif

        var bannerText = string.Join('\n', OccamLogger.BuildStartupBanner(paths));
        assert(
            "occam banner live extract label",
            bannerText.Contains("Live only", StringComparison.OrdinalIgnoreCase));
        assert(
            "occam banner no playbooks lie",
            !Regex.IsMatch(bannerText, @"\d+\s+playbooks\s+resolved", RegexOptions.IgnoreCase));
        assert(
            "occam banner honest playbooks",
            bannerText.Contains("seeds + heal/save", StringComparison.OrdinalIgnoreCase));
        assert(
            "occam banner tool count matches registration",
            bannerText.Contains(
                $"{OccamMcp.Core.Transport.OccamMcpServerRegistration.OccamToolNames.Length} occam_*",
                StringComparison.Ordinal));
        assert("occam banner listen spacing", bannerText.Contains("\n\n", StringComparison.Ordinal) && bannerText.Contains("Listening via stdio", StringComparison.Ordinal));
        assert("occam banner ansi gray rules", bannerText.Contains("\u001b[38;5;244m", StringComparison.Ordinal));
        assert("occam banner ansi white labels", bannerText.Contains("\u001b[38;5;255m", StringComparison.Ordinal));
        assert("occam banner distinct browser worker", paths.HasDistinctBrowserWorker);
    }

    private static void RunProbeAgentHints(Action<string, bool> assert)
    {
        // Q-008 part 2: probe proactively nudges the model to the right per-page opt-in.
        static OccamMcp.Core.Services.ProbeAnalysis Probe(string? contentType = null, int htmlBytes = 0, bool paywall = false) =>
            new()
            {
                Ok = true,
                Url = "https://e.com/",
                Privacy = new OccamMcp.Core.Routing.PrivacyClassification { Mode = OccamMcp.Core.Routing.PrivacyMode.LocalPublic },
                StatusCode = 200,
                RecommendedBackend = "http",
                ContentType = contentType,
                Classification = new OccamMcp.Core.Probe.PageClassification
                {
                    PageClass = "unknown",
                    Signals = new OccamMcp.Core.Routing.ProbeSignals { HtmlBytes = htmlBytes, LikelyPaywall = paywall },
                    RiskFlags = [],
                },
            };

        static bool Has(OccamMcp.Core.Services.ProbeAnalysis p, string needle) =>
            OccamMcp.Core.Agent.ProbeAgentHints.ForProbe(p).Warnings.Any(w => w.Contains(needle, System.StringComparison.Ordinal));

        assert("probe hints feed → json_feed", Has(Probe(contentType: "application/rss+xml"), "json_feed"));
        assert("probe hints atom → json_feed", Has(Probe(contentType: "application/atom+xml"), "json_feed"));
        assert("probe hints large page → token budget", Has(Probe(htmlBytes: 900_000), "large_page"));
        assert("probe hints paywall", Has(Probe(paywall: true), "likely_paywall"));
        // No false positives on an ordinary small HTML page.
        var plain = OccamMcp.Core.Agent.ProbeAgentHints.ForProbe(Probe(contentType: "text/html", htmlBytes: 20_000));
        assert("probe hints quiet on plain page",
            !plain.Warnings.Any(w => w.Contains("json_feed", System.StringComparison.Ordinal)
                || w.Contains("large_page", System.StringComparison.Ordinal)
                || w.Contains("likely_paywall", System.StringComparison.Ordinal)));
    }

    private static void RunServerInstructions(Action<string, bool> assert)
    {
        var t = OccamMcp.Core.Transport.OccamServerInstructions.TextFor(
            OccamMcp.Core.Transport.OccamToolProfile.Full);
        assert("server instructions non-trivial", !string.IsNullOrWhiteSpace(t) && t.Length > 400);
        assert("server instructions state trust rule", t.Contains("ok:false", System.StringComparison.Ordinal));
        assert("server instructions prefer over generic fetch", t.Contains("generic web fetch", System.StringComparison.Ordinal));
        assert("server instructions name occam_transcode", t.Contains("occam_transcode", System.StringComparison.Ordinal));
        assert("server instructions name client capabilities", t.Contains("occam_client_capabilities", System.StringComparison.Ordinal));
        assert(
            "server instructions surface opt-in gems",
            t.Contains("json_tables", System.StringComparison.Ordinal)
                && t.Contains("session_profile", System.StringComparison.Ordinal)
                && t.Contains("fit_markdown", System.StringComparison.Ordinal));
        // Occam 1.1 R5 Agent DX — thin ≠ short; digest over N×transcode; no occam_read rename.
        assert(
            "server instructions thin ≠ short (EQM)",
            t.Contains("short_quality", System.StringComparison.Ordinal)
                && t.Contains("thin_extract", System.StringComparison.Ordinal)
                && t.Contains("not a short quality", System.StringComparison.OrdinalIgnoreCase));
        assert(
            "server instructions prefer digest over N×transcode",
            t.Contains("occam_digest", System.StringComparison.Ordinal)
                && t.Contains("not N×", System.StringComparison.Ordinal));
        assert("server instructions never advertise occam_read", !t.Contains("occam_read", System.StringComparison.Ordinal));

        var reader = OccamMcp.Core.Transport.OccamServerInstructions.TextFor(
            OccamMcp.Core.Transport.OccamToolProfile.Reader);
        assert("reader instructions omit playbook_heal", !reader.Contains("occam_playbook_heal", System.StringComparison.Ordinal));
        assert("reader instructions omit attest", !reader.Contains("occam_attest", System.StringComparison.Ordinal));
        assert(
            "reader instructions thin ≠ short",
            reader.Contains("short_quality", System.StringComparison.Ordinal));
    }

    private static void RunClientCapabilityBudget(Action<string, bool> assert)
    {
        assert("budget 128k → 16384 clamp", OccamMcp.Core.Client.ClientCapabilityStore.ComputeOutputBudget(128_000) == 16_384);
        assert("budget 8k → 1600", OccamMcp.Core.Client.ClientCapabilityStore.ComputeOutputBudget(8_000) == 1_600);
        assert("budget 2k → 512 floor", OccamMcp.Core.Client.ClientCapabilityStore.ComputeOutputBudget(2_000) == 512);
        assert("suggest reader for 4k", OccamMcp.Core.Client.ClientCapabilityStore.SuggestProfile(4_096) == "reader");
        assert("suggest researcher for 16k", OccamMcp.Core.Client.ClientCapabilityStore.SuggestProfile(16_384) == "researcher");
        assert("suggest full for 128k", OccamMcp.Core.Client.ClientCapabilityStore.SuggestProfile(128_000) == "full");

        var priorCtx = Environment.GetEnvironmentVariable("OCCAM_CLIENT_CONTEXT_TOKENS");
        var priorModel = Environment.GetEnvironmentVariable("OCCAM_CLIENT_MODEL_ID");
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_CLIENT_CONTEXT_TOKENS", null);
            Environment.SetEnvironmentVariable("OCCAM_CLIENT_MODEL_ID", null);
            var store = new OccamMcp.Core.Client.ClientCapabilityStore();
            assert("store empty without env", !store.Current.Configured);
            assert("resolve null when empty", store.ResolveMaxTokens(null) is null);
            assert("explicit max wins empty", store.ResolveMaxTokens(2048) == 2048);

            var applied = store.Configure(32_000, modelId: "test-model", source: "tool");
            assert("configure sets configured", applied.Configured);
            assert("configure output 6400", applied.OutputBudgetTokens == 6_400);
            assert("ambient applied", store.ResolveMaxTokens(null) == 6_400);
            assert("explicit overrides ambient", store.ResolveMaxTokens(900) == 900);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_CLIENT_CONTEXT_TOKENS", priorCtx);
            Environment.SetEnvironmentVariable("OCCAM_CLIENT_MODEL_ID", priorModel);
        }
    }

    private static void RunOccamToolProfile(Action<string, bool> assert)
    {
        var prior = Environment.GetEnvironmentVariable("OCCAM_PROFILE");
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_PROFILE", null);
            assert("default profile is full", OccamMcp.Core.Transport.OccamToolProfile.Resolve() == "full");
            assert(
                "full exposes catalog tools",
                OccamMcp.Core.Transport.OccamToolProfile.GetExposedToolNames("full").Length
                    == OccamMcp.Core.Transport.OccamMcpServerRegistration.OccamToolNames.Length);

            var reader = OccamMcp.Core.Transport.OccamToolProfile.GetExposedToolNames("reader");
            assert("reader has 7 tools", reader.Length == 7);
            assert("reader exposes client_capabilities", System.Array.IndexOf(reader, "occam_client_capabilities") >= 0);
            assert("reader exposes transcode", System.Array.IndexOf(reader, "occam_transcode") >= 0);
            assert("reader hides heal", System.Array.IndexOf(reader, "occam_playbook_heal") < 0);
            assert("reader hides save", System.Array.IndexOf(reader, "occam_playbook_save") < 0);

            var researcher = OccamMcp.Core.Transport.OccamToolProfile.GetExposedToolNames("researcher");
            assert("researcher has 9 tools", researcher.Length == 9);
            assert("researcher exposes verify", System.Array.IndexOf(researcher, "occam_verify") >= 0);
            assert("researcher exposes claim_check", System.Array.IndexOf(researcher, "occam_claim_check") >= 0);
            assert("researcher hides heal", System.Array.IndexOf(researcher, "occam_playbook_heal") < 0);

            var auditor = OccamMcp.Core.Transport.OccamToolProfile.GetExposedToolNames("auditor");
            assert("auditor has 12 tools", auditor.Length == 12);
            assert("auditor exposes attest", System.Array.IndexOf(auditor, "occam_attest") >= 0);
            assert("auditor hides heal", System.Array.IndexOf(auditor, "occam_playbook_heal") < 0);

            Environment.SetEnvironmentVariable("OCCAM_PROFILE", "not-a-profile");
            assert("invalid profile falls back to full", OccamMcp.Core.Transport.OccamToolProfile.Resolve() == "full");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_PROFILE", prior);
        }
    }

    private static void RunExtractQuality(Action<string, bool> assert)
    {
        const string nginxBannerMd =
            "# nginx documentation\n\nPlanning your ingress-nginx migration? Start here: [kubernetes.nginx.org](https://go.f5.net/k8.nginx.org-b).";
        assert("extract quality flags nginx promo banner", ExtractQualityEvaluator.LooksLikeThinExtract(nginxBannerMd));
        assert(
            "extract quality accepts full docs",
            !ExtractQualityEvaluator.LooksLikeThinExtract(new string('x', 600) + "\n\n## Intro\n\n* [a](u)\n* [b](u)\n* [c](u)"));
        // Anti-bot interstitial rendered to a headings-only shell (repro: nowsecure.nl turnstile).
        // No "captcha"/"cloudflare" keyword in markdown, so only the thin guard can catch it.
        assert(
            "extract quality flags headings-only challenge shell",
            ExtractQualityEvaluator.LooksLikeThinExtract("# nowsecure.nl\n\n### by nodriver\n\n## NOWSECURE\n\n### by nodriver"));
        // Trust floor: link/heading-dressed shell with tiny real prose (repro class: pixabay 36t ok:true).
        // The structural checks pass it (1 heading + 4 links), so only the visible-content floor catches it.
        assert(
            "extract quality flags link-dressed tiny shell (trust floor)",
            ExtractQualityEvaluator.LooksLikeThinExtract("# Pixabay\n\n[Explore](/explore) [Photos](/photos) [Videos](/videos) [Music](/music)\n\nAccept cookies to continue."));
        // Guard against over-flagging: a real paragraph in the 400-499 char band (clears check2's
        // <400 thin rule, stays under the 500 SafeLength early-return) must NOT be flagged — this is
        // exactly the band the visible-content floor governs, so it proves the floor isn't over-eager.
        assert(
            "extract quality accepts short-but-real prose",
            !ExtractQualityEvaluator.LooksLikeThinExtract("# Note\n\nThis is a genuine article paragraph that carries real, substantive prose describing the topic in enough detail to comfortably clear the visible-content trust floor. It keeps going with several more clauses of actual sentences so that its visible length lands in the band between the thin threshold and the safe-length cutoff, which is precisely where the trust floor must not over-flag legitimate content."));

        // ADR-0004: short quality document (example.com-class) must pass — length alone is not BE.
        const string exampleComMd =
            "# Example Domain\n\nThis domain is for use in documentation examples without needing permission. Avoid use in operations.\n\n[Learn more](https://iana.org/domains/example)";
        var exampleReport = ExtractQualityEvaluator.Evaluate(exampleComMd);
        assert("EQM example.com is not bad extraction", !exampleReport.IsBadExtraction);
        assert("EQM example.com verdict is short_quality", exampleReport.Verdict == "short_quality");
        assert("EQM example.com qualityScore >= 0.55", exampleReport.Score >= 0.55);
        assert("EQM example.com LooksLikeThinExtract false", !ExtractQualityEvaluator.LooksLikeThinExtract(exampleComMd));

        // Promo / consent fixtures still BE via EQM.
        var nginxReport = ExtractQualityEvaluator.Evaluate(nginxBannerMd);
        assert("EQM nginx promo is bad extraction", nginxReport.IsBadExtraction);
        assert("EQM nginx verdict thin or noisy", nginxReport.Verdict is "thin" or "noisy");

        var shellReport = ExtractQualityEvaluator.Evaluate("# nowsecure.nl\n\n### by nodriver\n\n## NOWSECURE\n\n### by nodriver");
        assert("EQM headings shell is bad extraction", shellReport.IsBadExtraction);

        // Determinism: same markdown → identical report.
        var a = ExtractQualityEvaluator.Evaluate(exampleComMd);
        var b = ExtractQualityEvaluator.Evaluate(exampleComMd);
        assert("EQM deterministic score", a.Score == b.Score && a.Verdict == b.Verdict && a.IsBadExtraction == b.IsBadExtraction);

        Console.WriteLine("L_EXTRACT_QUALITY_OK");

        RunMarkdownPostProcessProbes(assert);
    }

    private static void RunMarkdownPostProcessProbes(Action<string, bool> assert)
    {
        var occamHome = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "generic-markdown-prune.selftest.mjs"), "generic markdown prune selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "plain-text-pass-through.selftest.mjs"), "plain text pass-through selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "http-extract", "lib", "access-evidence.selftest.mjs"), "access evidence selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "playbook-seed.selftest.mjs"), "playbook seed selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "playbook-community-hygiene.selftest.mjs"), "playbook community hygiene selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "media-refs.selftest.mjs"), "media refs selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "dom-blocks.selftest.mjs"), "dom-blocks heading-level selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "feed-items.selftest.mjs"), "feed-items summary formats selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "dom-tables.selftest.mjs"), "dom-tables semantic rows selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "response-body-cap.selftest.mjs"), "response body cap selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "shared", "lib", "request-headers.selftest.mjs"), "request headers cross-origin strip selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "browser-extract", "lib", "browser-challenge-detect.selftest.mjs"), "browser challenge fail-fast selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("workers", "browser-extract", "lib", "browser-html-cap.selftest.mjs"), "browser html cap selftest");
        RunNodeSelfTest(assert, occamHome, Path.Combine("scripts", "lib", "verify-community-manifest.mjs"), "verify community manifest");
        RunGoldenHostsAllowlist(assert, occamHome);
        RunPlaybookSeedResolver(assert);
        RunPlaybookCommunityHygiene(assert);
        RunPlaybookCommunityManifestIntegrity(assert);
    }

    private static void RunPlaybookSeedResolver(Action<string, bool> assert)
    {
        var prevLocal = Environment.GetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT");
        var prevUser = Environment.GetEnvironmentVariable("WT_PLAYBOOKS_PATH");
        // Isolate from any learned playbooks in the developer's real ~/.occam: clearing the
        // env vars is NOT enough, because ResolveLocalRoot() falls back to the user data dir
        // (~/.occam/playbooks/local) when the override is unset. A stale learned nginx.org
        // genome there would shadow the repo seed and fail the backend/selectors asserts.
        // Point both roots at an empty temp dir so only repo seeds + community resolve here.
        var isolatedRoot = Path.Combine(Path.GetTempPath(), "occam-pb-seed-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(isolatedRoot);
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", isolatedRoot);
            Environment.SetEnvironmentVariable("WT_PLAYBOOKS_PATH", isolatedRoot);

            var resolver = new PlaybookSeedResolver(CreateWellKnownGenomeFetcher());
            var nginx = resolver.Resolve("https://nginx.org/en/docs/");
            assert("playbook resolve nginx ok", nginx.Ok);
            assert("playbook resolve nginx id", nginx.PlaybookId == "nginx.org");
            assert("playbook resolve nginx provenance exists", !string.IsNullOrWhiteSpace(nginx.Provenance));
            assert("playbook resolve nginx backend", nginx.PreferredBackend == "http");
            assert(
                "playbook resolve nginx selectors",
                nginx.ContentSelectors is not null && nginx.ContentSelectors.Contains("#content", StringComparer.Ordinal));
            assert(
                "playbook resolve nginx source path",
                !string.IsNullOrWhiteSpace(nginx.SourcePath));

            var docker = resolver.Resolve("https://docs.docker.com/get-started/");
            assert("playbook resolve docker ok", docker.Ok);
            assert("playbook resolve docker provenance community", docker.Provenance == PlaybookProvenance.Community);
            assert(
                "playbook resolve docker community path",
                docker.SourcePath is not null && docker.SourcePath.Contains("profiles/playbooks/community/docs.docker.com.json", StringComparison.Ordinal));
            assert(
                "playbook resolve docker community notes",
                docker.AgentNotes is not null && docker.AgentNotes.Contains("Community vetted", StringComparison.Ordinal));

            var postgres = resolver.Resolve("postgresql.org");
            assert("playbook resolve postgres community", postgres.Ok && postgres.Provenance == PlaybookProvenance.Community);

            var openAi = resolver.Resolve("https://platform.openai.com/docs/concepts");
            assert("playbook resolve openai host alias", openAi.Ok && openAi.PlaybookId == "developers.openai.com");

            var missing = resolver.Resolve("https://unknown.example.com/page");
            assert("playbook resolve missing host", !missing.Ok);
            assert("playbook resolve missing code", missing.FailureCode == "playbook_not_found");

            var bareHost = resolver.Resolve("nuxt.com");
            assert("playbook resolve bare hostname", bareHost.Ok && bareHost.MatchedHost == "nuxt.com");

            RunPlaybookResolverTierOverrides(assert);
            RunPlaybookSeedMergeFallback(assert);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", prevLocal);
            Environment.SetEnvironmentVariable("WT_PLAYBOOKS_PATH", prevUser);
            try { Directory.Delete(isolatedRoot, recursive: true); } catch { /* best-effort cleanup */ }
        }
    }

    // Q-010 regression: a learned local genome must NOT silently erase a curated repo seed.
    // Mimics the real ~/.occam/playbooks/local/nginx.org.playbook.json a heal/save loop left
    // behind: it outranks the seed (Local tier > Seed) but specifies no routing.preferred_backend
    // and only the legacy snake_case extract.content_selectors. The resolver and workers must
    // normalize that persisted shape so the selector remains effective, while missing routing still
    // falls back to the seed and provenance stays Local (the winner).
    private static void RunPlaybookSeedMergeFallback(Action<string, bool> assert)
    {
        var tempRoot = Path.Combine(Path.GetTempPath(), "occam-pb-merge-" + Guid.NewGuid().ToString("N"));
        var localDir = Path.Combine(tempRoot, "local");
        Directory.CreateDirectory(localDir);
        var userDir = Path.Combine(tempRoot, "user");
        Directory.CreateDirectory(userDir);

        // Legacy learned genome: root page_classes + snake_case content_selectors, no routing.
        var learnedGenome = """
            {
              "id": "nginx.org",
              "schema_version": "1.0",
              "hosts": ["nginx.org"],
              "page_classes": { "default": { "path_pattern": "/*" } },
              "extract": { "content_selectors": ["#content"] }
            }
            """;
        File.WriteAllText(Path.Combine(localDir, "nginx.org.playbook.json"), learnedGenome);

        var prevLocal = Environment.GetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT");
        var prevUser = Environment.GetEnvironmentVariable("WT_PLAYBOOKS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", localDir);
            Environment.SetEnvironmentVariable("WT_PLAYBOOKS_PATH", userDir);

            var resolver = new PlaybookSeedResolver(CreateWellKnownGenomeFetcher());
            resolver.ClearCacheForTests();

            var nginx = resolver.Resolve("https://nginx.org/en/docs/");
            assert("playbook merge learned genome wins provenance", nginx.Provenance == PlaybookProvenance.Local);
            assert("playbook merge backend falls back to seed", nginx.PreferredBackend == "http");
            assert(
                "playbook merge legacy selectors normalized",
                nginx.ContentSelectors is not null && nginx.ContentSelectors.Contains("#content", StringComparer.Ordinal));
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", prevLocal);
            Environment.SetEnvironmentVariable("WT_PLAYBOOKS_PATH", prevUser);
            try { Directory.Delete(tempRoot, recursive: true); } catch (IOException) { }
        }
    }

    private static void RunPlaybookResolverTierOverrides(Action<string, bool> assert)
    {
        var occamHome = WorkerPaths.ResolveOccamHome();
        if (occamHome is null)
        {
            assert("playbook tier override occam home", false);
            return;
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "occam-pb2-" + Guid.NewGuid().ToString("N"));
        var localDir = Path.Combine(tempRoot, "local");
        Directory.CreateDirectory(localDir);
        var userDir = Path.Combine(tempRoot, "user");
        Directory.CreateDirectory(userDir);

        var localPlaybook = """
            {
              "schema_version": "1.0",
              "id": "docs.docker.com",
              "hosts": ["docs.docker.com"],
              "routing": { "preferred_backend": "http" },
              "extract": { "contentSelectors": ["main.local-override"] },
              "agent_notes": "local tier override"
            }
            """;
        File.WriteAllText(Path.Combine(localDir, "docs.docker.com.playbook.json"), localPlaybook);

        var userPlaybook = """
            {
              "schema_version": "1.0",
              "id": "postgresql.org",
              "hosts": ["postgresql.org"],
              "routing": { "preferred_backend": "browser" },
              "extract": { "contentSelectors": ["main.user-override"] },
              "agent_notes": "user tier override"
            }
            """;
        File.WriteAllText(Path.Combine(userDir, "postgresql.org.json"), userPlaybook);

        var prevLocal = Environment.GetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT");
        var prevUser = Environment.GetEnvironmentVariable("WT_PLAYBOOKS_PATH");
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", localDir);
            Environment.SetEnvironmentVariable("WT_PLAYBOOKS_PATH", userDir);

            var resolver = new PlaybookSeedResolver(CreateWellKnownGenomeFetcher());
            resolver.ClearCacheForTests();

            var local = resolver.Resolve("https://docs.docker.com/get-started/");
            assert("playbook resolve local beats community", local.Provenance == PlaybookProvenance.Local);
            assert(
                "playbook resolve local selector",
                local.ContentSelectors is not null && local.ContentSelectors.Contains("main.local-override", StringComparer.Ordinal));

            var user = resolver.Resolve("https://www.postgresql.org/docs/current/");
            assert("playbook resolve user beats community", user.Provenance == PlaybookProvenance.User);
            assert("playbook resolve user backend", user.PreferredBackend == "browser");
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", prevLocal);
            Environment.SetEnvironmentVariable("WT_PLAYBOOKS_PATH", prevUser);
            try
            {
                Directory.Delete(tempRoot, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static void RunPlaybookCommunityHygiene(Action<string, bool> assert)
    {
        var clean = """
            {
              "schema_version": "1.0",
              "id": "example.com",
              "hosts": ["example.com"],
              "agent_notes": "public selectors only"
            }
            """;
        assert("playbook hygiene clean json", !PlaybookCommunityHygiene.ContainsForbiddenKeys(clean));

        var bad = """
            {
              "schema_version": "1.0",
              "id": "evil.example",
              "hosts": ["evil.example"],
              "headers": { "Authorization": "Bearer secret" }
            }
            """;
        assert("playbook hygiene rejects authorization", PlaybookCommunityHygiene.ContainsForbiddenKeys(bad));
    }

    private static void RunPlaybookCommunityManifestIntegrity(Action<string, bool> assert)
    {
        var occamHome = WorkerPaths.ResolveOccamHome();
        assert("community manifest occam home", occamHome is not null);
        if (occamHome is null)
        {
            return;
        }

        var communityDir = Path.Combine(occamHome, "profiles", "playbooks", "community");
        assert("community manifest exists", File.Exists(Path.Combine(communityDir, "manifest.json")));

        var fileHashes = CommunityManifest.TryLoadFileHashes(communityDir);
        assert("community manifest hashes loaded", fileHashes is not null && fileHashes.Count >= 4);
        assert(
            "community manifest docker hash",
            fileHashes is not null
            && fileHashes.TryGetValue("docs.docker.com.json", out var dockerSha)
            && CommunityManifest.FileSha256Matches(Path.Combine(communityDir, "docs.docker.com.json"), dockerSha));

        var playbookJson = """
            {
              "schema_version": "1.0",
              "id": "tamper.neg.test",
              "hosts": ["tamper.neg.test"],
              "agent_notes": "unit negative fixture"
            }
            """;
        var tempRoot = Path.Combine(Path.GetTempPath(), "occam-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        var playbookPath = Path.Combine(tempRoot, "tamper.neg.test.json");
        File.WriteAllText(playbookPath, playbookJson);

        var validHash = CommunityManifest.ComputeSha256Hex(playbookJson);
        var badHash = new string('0', 64);
        assert("community manifest sha256 mismatch", !CommunityManifest.FileSha256Matches(playbookPath, badHash));
        assert("community manifest sha256 match", CommunityManifest.FileSha256Matches(playbookPath, validHash));

        File.WriteAllText(playbookPath, playbookJson + " ");
        assert("community manifest tampered bytes", !CommunityManifest.FileSha256Matches(playbookPath, validHash));
        var tamperHashes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["tamper.neg.test.json"] = validHash,
        };
        assert(
            "community manifest skip tampered read",
            !CommunityManifest.TryReadVerifiedPlaybook(playbookPath, "tamper.neg.test.json", tamperHashes, out _));

        var fixtureDir = Path.Combine(occamHome, "benchmarks", "l0-gate", "fixtures", "community-manifest-neg");
        assert("genome-neg-manifest-sha256 fixture dir", Directory.Exists(fixtureDir));
        var fixtureHashes = CommunityManifest.TryLoadFileHashes(fixtureDir);
        var fixtureFile = Path.Combine(fixtureDir, "tamper.neg.test.json");
        assert("genome-neg-manifest-sha256 fixture file", File.Exists(fixtureFile));
        assert(
            "genome-neg-manifest-sha256 mismatch",
            fixtureHashes is not null
            && fixtureHashes.TryGetValue("tamper.neg.test.json", out var fixtureSha)
            && !CommunityManifest.FileSha256Matches(fixtureFile, fixtureSha));
        assert(
            "genome-neg-manifest-sha256 skip load",
            fixtureHashes is not null
            && !CommunityManifest.TryReadVerifiedPlaybook(fixtureFile, "tamper.neg.test.json", fixtureHashes, out _));

        try
        {
            Directory.Delete(tempRoot, recursive: true);
        }
        catch (IOException)
        {
        }
    }

    private static void RunNodeSelfTest(Action<string, bool> assert, string occamHome, string relativeScript, string label)
    {
        var selfTest = Path.Combine(occamHome, relativeScript);
        assert($"{label} script exists", File.Exists(selfTest));

        var node = NodeRuntime.ResolveExecutable();
        var psi = new ProcessStartInfo
        {
            FileName = node,
            Arguments = $"\"{selfTest}\"",
            WorkingDirectory = occamHome,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        using var proc = WorkerProcessGroup.Start(psi);
        assert($"{label} spawn", proc is not null);
        if (proc is null)
        {
            return;
        }

        var stdout = proc.StandardOutput.ReadToEnd();
        var stderr = proc.StandardError.ReadToEnd();
        proc.WaitForExit(15_000);
        WorkerProcessGroup.Release(proc);
        assert($"{label} exit zero", proc.ExitCode == 0);
        assert($"{label} ok line", stdout.Contains(": OK", StringComparison.Ordinal) && string.IsNullOrWhiteSpace(stderr));
    }

    private static void RunGoldenHostsAllowlist(Action<string, bool> assert, string occamHome)
    {
        var manifestPath = Path.Combine(occamHome, "corpora", "golden-hosts.json");
        assert("golden hosts manifest exists", File.Exists(manifestPath));
        if (!File.Exists(manifestPath))
        {
            return;
        }

        var manifest = JsonSerializer.Deserialize(File.ReadAllText(manifestPath), Utf8ProbeJsonContext.Default.GoldenHostsDto);
        var allowlist = manifest?.BrowserRecipes ?? Array.Empty<string>();
        var goldenHosts = manifest?.Hosts ?? Array.Empty<string>();
        assert("golden hosts browser recipe allowlist", allowlist.Length is > 0 and <= 8);
        assert("golden hosts manifest hosts", goldenHosts.Length is > 0 and <= 12);

        var resolver = new PlaybookSeedResolver(CreateWellKnownGenomeFetcher());
        foreach (var host in goldenHosts)
        {
            var resolved = resolver.Resolve(host);
            assert($"golden host seed resolve {host}", resolved.Ok);
        }

        var recipesDir = Path.Combine(occamHome, "workers", "browser-extract", "lib", "recipes");
        var activeRecipes = Directory.Exists(recipesDir)
            ? Directory.GetFiles(recipesDir, "*.mjs")
                .Select(Path.GetFileName)
                .Where(name => !string.Equals(name, "registry.mjs", StringComparison.OrdinalIgnoreCase))
                .ToArray()
            : Array.Empty<string?>();
        assert("recipe registry count matches allowlist", activeRecipes.Length == allowlist.Length);

        // Historical per-host recipes may live under _archive/ in private trees only.
        // Public snapshots intentionally omit that directory; absence is not a failure.
        var archiveDir = Path.Combine(recipesDir, "_archive");
        if (Directory.Exists(archiveDir))
        {
            var archivedCount = Directory.GetFiles(archiveDir, "*.mjs").Length;
            assert("recipe archive trimmed extras", archivedCount >= 20);
        }
    }

    private static void RunInfraScripts(Action<string, bool> assert)
    {
        var occamHome = WorkerPaths.ResolveOccamHome();
        var orphanAuditScript = occamHome is not null
            ? Path.Combine(occamHome, "scripts", "run-l0-orphan-audit.ps1")
            : Path.Combine(Directory.GetCurrentDirectory(), "scripts", "run-l0-orphan-audit.ps1");
        assert("l0 orphan audit script exists", File.Exists(orphanAuditScript));

        var browserDaemonScript = occamHome is not null
            ? Path.Combine(occamHome, "workers", "browser-extract", "browser-daemon.mjs")
            : Path.Combine(Directory.GetCurrentDirectory(), "workers", "browser-extract", "browser-daemon.mjs");
        assert("browser daemon script exists", File.Exists(browserDaemonScript));

        var httpDaemonScript = occamHome is not null
            ? Path.Combine(occamHome, "workers", "http-extract", "http-daemon.mjs")
            : Path.Combine(Directory.GetCurrentDirectory(), "workers", "http-extract", "http-daemon.mjs");
        assert("http daemon script exists", File.Exists(httpDaemonScript));

        var httpExtractCore = occamHome is not null
            ? Path.Combine(occamHome, "workers", "http-extract", "lib", "http-extract-run.mjs")
            : Path.Combine(Directory.GetCurrentDirectory(), "workers", "http-extract", "lib", "http-extract-run.mjs");
        assert("http extract run module exists", File.Exists(httpExtractCore));

        var ramStressProj = occamHome is not null
            ? Path.Combine(occamHome, "benchmarks", "l0-ram-stress", "L0RamStress.csproj")
            : Path.Combine(Directory.GetCurrentDirectory(), "benchmarks", "l0-ram-stress", "L0RamStress.csproj");
        assert("l0 ram stress project exists", File.Exists(ramStressProj));

        var playwrightPath = PlaywrightEnvironment.ResolveDefaultBrowsersPath();
        if (playwrightPath is not null)
        {
            assert("playwright browsers path", Directory.Exists(playwrightPath));
            assert("playwright chromium present", PlaywrightEnvironment.HasChromiumInstall(playwrightPath));

            var prevPlaywrightPath = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
            try
            {
                var stale = Path.Combine(Path.GetTempPath(), "occam-gate-no-chromium");
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", stale);
                var psi = new ProcessStartInfo { UseShellExecute = false };
                PlaywrightEnvironment.ApplyTo(psi);
                assert(
                    "playwright stale browsers path overridden",
                    psi.Environment.TryGetValue("PLAYWRIGHT_BROWSERS_PATH", out var applied)
                    && applied == playwrightPath);
            }
            finally
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", prevPlaywrightPath);
            }
        }
    }

    private static void RunBrowserConcurrencyInfra(Action<string, bool> assert)
    {
        var launch = NodeLaunchArguments.Build("\"script.mjs\"", "\"https://example.com\"");
        assert("node launch max-old-space-size flag", launch.Contains("--max-old-space-size=", StringComparison.Ordinal));
        assert("node launch http heap default", NodeLaunchArguments.ResolveMaxOldSpaceMb(browser: false) == 512);
        assert("node launch browser heap default", NodeLaunchArguments.ResolveMaxOldSpaceMb(browser: true) == 512);

        var prevOccam = Environment.GetEnvironmentVariable("OCCAM_BROWSER_MAX_PARALLEL");
        var prevWt = Environment.GetEnvironmentVariable("WT_BROWSER_MAX_PARALLEL");
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_MAX_PARALLEL", "5");
            Environment.SetEnvironmentVariable("WT_BROWSER_MAX_PARALLEL", null);
            assert("browser concurrency reads OCCAM_BROWSER_MAX_PARALLEL", BrowserConcurrencyGate.ResolveMaxParallel() == 5);
            BrowserConcurrencyGate.ResetForTests();
            assert("browser concurrency gate max stable occam", BrowserConcurrencyGate.MaxParallel == 5);

            Environment.SetEnvironmentVariable("OCCAM_BROWSER_MAX_PARALLEL", null);
            Environment.SetEnvironmentVariable("WT_BROWSER_MAX_PARALLEL", "8");
            assert("browser concurrency falls back to WT_BROWSER_MAX_PARALLEL", BrowserConcurrencyGate.ResolveMaxParallel() == 8);
            BrowserConcurrencyGate.ResetForTests();
            assert("browser concurrency gate max stable wt", BrowserConcurrencyGate.MaxParallel == 8);

            Environment.SetEnvironmentVariable("OCCAM_BROWSER_TIMEOUT_MS", "60000");
            assert(
                "daemon wait timeout scales with parallel slots",
                BrowserExtractTimeouts.ResolveDaemonWaitTimeoutMs() == 60_000 * BrowserConcurrencyGate.MaxParallel);

            Environment.SetEnvironmentVariable("OCCAM_BROWSER_PROFILE", "isolated");
            assert("isolated profile disables shared daemon", !BrowserExecutionProfile.UseSharedDaemon());
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_PROFILE", "shared");
            assert("shared profile enables shared daemon", BrowserExecutionProfile.UseSharedDaemon());
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_MAX_PARALLEL", prevOccam);
            Environment.SetEnvironmentVariable("WT_BROWSER_MAX_PARALLEL", prevWt);
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_TIMEOUT_MS", null);
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_PROFILE", null);
        }
    }

    private static void RunBrowserPoolInfra(Action<string, bool> assert, WorkerPaths paths)
    {
        var prevPoolSize = Environment.GetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar);
        var prevBasePort = Environment.GetEnvironmentVariable(BrowserPoolSettings.BasePortVar);
        var prevDaemonPort = Environment.GetEnvironmentVariable(BrowserPoolSettings.DaemonPortVar);
        try
        {
            Environment.SetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar, "2");
            Environment.SetEnvironmentVariable(BrowserPoolSettings.BasePortVar, "39217");
            Environment.SetEnvironmentVariable(BrowserPoolSettings.DaemonPortVar, null);

            var settings = BrowserPoolSettings.ReadFromEnvironment();
            assert("browser pool size clamp", settings.PoolSize == 2);
            assert("browser pool slot0 port", settings.ResolvePortForSlot(0) == 39_217);
            assert("browser pool slot1 port", settings.ResolvePortForSlot(1) == 39_218);

            var manager = new BrowserPoolManager(settings, NullOccamTelemetrySink.Instance, NullBrowserDaemonClient.Instance);
            var healthyMask = new[] { true, true };
            var first = manager.PickNextSlotIndexForTests(healthyMask);
            var second = manager.PickNextSlotIndexForTests(healthyMask);
            assert("browser pool round robin first", first == 0);
            assert("browser pool round robin second", second == 1);

            var skipUnhealthy = new[] { false, true };
            assert("browser pool skip unhealthy", manager.PickNextSlotIndexForTests(skipUnhealthy) == 1);

            if (paths.IsConfigured)
            {
                var queueTelemetry = new CapturingBrowserPoolTelemetry();
                var queueManager = new BrowserPoolManager(
                    new BrowserPoolSettings
                    {
                        PoolSize = 1,
                        BasePort = 40_111,
                        IdleTtlMs = 0,
                        MaxParallel = 1,
                    },
                    queueTelemetry,
                    AlwaysHealthyBrowserDaemonClient.Instance);
                queueManager.SetPathsForTests(paths);
                var firstLease = queueManager.AcquireSlotAsync(CancellationToken.None).AsTask().GetAwaiter().GetResult();
                assert("browser pool telemetry excludes active acquirer", queueTelemetry.LastPendingDepth == 0);

                using var cancellation = new CancellationTokenSource(100);
                var cancelled = false;
                try
                {
                    _ = queueManager.AcquireSlotAsync(cancellation.Token).AsTask().GetAwaiter().GetResult();
                }
                catch (OperationCanceledException)
                {
                    cancelled = true;
                }
                finally
                {
                    queueManager.ReleaseSlot(firstLease);
                    queueManager.StopAll();
                }

                assert("browser pool queued acquire cancels", cancelled);
                assert("browser pool pending depth resets after cancellation", queueManager.PendingAcquiresForTests == 0);
            }

            Environment.SetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar, "1");
            Environment.SetEnvironmentVariable(BrowserPoolSettings.DaemonPortVar, "40001");
            var legacy = BrowserPoolSettings.ReadFromEnvironment();
            assert("browser pool legacy daemon port", legacy.ResolvePortForSlot(0) == 40_001);
        }
        finally
        {
            Environment.SetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar, prevPoolSize);
            Environment.SetEnvironmentVariable(BrowserPoolSettings.BasePortVar, prevBasePort);
            Environment.SetEnvironmentVariable(BrowserPoolSettings.DaemonPortVar, prevDaemonPort);
            BrowserPoolManager.ResetSharedForTests();
        }
    }

    private sealed class CapturingBrowserPoolTelemetry : IOccamTelemetrySink
    {
        public int LastPendingDepth { get; private set; } = -1;

        public void OnTranscodeCompleted(TranscodeContext ctx, TranscodeOutcome outcome) { }
        public void OnTranscodeFailed(TranscodeContext ctx, TranscodeOutcome outcome) { }
        public void OnBrowserPoolAcquired(BrowserPoolSlot slot, int waitMs, int pendingDepth) =>
            LastPendingDepth = pendingDepth;
        public void OnBrowserPoolReleased(BrowserPoolSlot slot, bool ok, int extractMs) { }
    }

    private sealed class AlwaysHealthyBrowserDaemonClient : IBrowserDaemonClient
    {
        public static AlwaysHealthyBrowserDaemonClient Instance { get; } = new();

        public Task<bool> IsHealthyAsync(int port, CancellationToken cancellationToken) => Task.FromResult(true);

        public Task<ExtractRunResult?> TryExtractAsync(
            string url,
            int timeoutMs,
            bool forceRecycle,
            string? headersFile,
            string? storageStateFile,
            CancellationToken cancellationToken,
            int port = 0,
            string? features = null,
            string? playbookOverlayJson = null,
            bool playbookOverlayStrict = false) => Task.FromResult<ExtractRunResult?>(null);

        public Task<string?> TryCaptureSkeletonJsonAsync(
            string url,
            int maxNodes,
            int timeoutMs,
            string? headersFile,
            CancellationToken cancellationToken,
            int port = 0) => Task.FromResult<string?>(null);
    }

    private static void RunProxyRotationInfra(Action<string, bool> assert)
    {
        var parsed = ProxyListParser.ParseInline("http://127.0.0.1:8080,https://proxy2:8443;invalid,not-a-url");
        assert("proxy list parse inline count", parsed.Count == 2);
        assert("proxy list parse inline first", parsed[0] == "http://127.0.0.1:8080");

        var lines = ProxyListParser.ParseLines(["# comment", "http://a:1", "", "socks5://b:1080"]);
        assert("proxy list parse lines", lines.Count == 2);

        var csvRows = ProxyListParser.ParseFileLines(
        [
            "ip,port,protocols",
            "\"10.0.0.1\",\"8080\",\"http\"",
            "\"10.0.0.2\",\"8443\",\"https\"",
            "\"10.0.0.3\",\"1080\",\"socks5\"",
            "\"10.0.0.4\",\"1080\",\"socks4\"",
        ]);
        assert("proxy list csv row count", csvRows.Count == 3);
        assert("proxy list csv http", csvRows[0] == "http://10.0.0.1:8080");
        assert("proxy list csv socks5", csvRows[2] == "socks5://10.0.0.3:1080");

        var service = new RoundRobinProxyRotationService(["http://p1:1", "http://p2:2", "http://p3:3"]);
        assert("proxy rotation configured", service.IsConfigured);
        assert("proxy rotation count", service.Count == 3);
        var a = service.AcquireNext();
        var b = service.AcquireNext();
        var c = service.AcquireNext();
        var d = service.AcquireNext();
        assert("proxy rotation round robin a", a?.ProxyUrl == "http://p1:1");
        assert("proxy rotation round robin b", b?.ProxyUrl == "http://p2:2");
        assert("proxy rotation round robin c", c?.ProxyUrl == "http://p3:3");
        assert("proxy rotation round robin wrap", d?.ProxyUrl == "http://p1:1");

        var spawnService = new RoundRobinProxyRotationService(["http://rot-a:8080", "http://rot-b:8080"]);
        var psiA = new ProcessStartInfo { FileName = "node", UseShellExecute = false };
        var psiB = new ProcessStartInfo { FileName = "node", UseShellExecute = false };
        EgressProxyConfig.ApplyForSpawn(psiA, spawnService);
        EgressProxyConfig.ApplyForSpawn(psiB, spawnService);
        assert(
            "proxy rotation spawn apply first",
            psiA.Environment[EgressProxyConfig.HttpProxyVar] == "http://rot-a:8080");
        assert(
            "proxy rotation spawn apply second",
            psiB.Environment[EgressProxyConfig.HttpsProxyVar] == "http://rot-b:8080");

        var empty = new RoundRobinProxyRotationService(Array.Empty<string>());
        assert("proxy rotation empty not configured", !empty.IsConfigured);
        assert("proxy rotation empty acquire null", empty.AcquireNext() is null);

        var tempFile = Path.Combine(Path.GetTempPath(), $"occam-proxy-list-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(tempFile, "http://file-proxy:3128\n# skip\nhttp://file-proxy-2:3129\n");
            Environment.SetEnvironmentVariable(ProxyRotationSettings.ProxyListFileVar, tempFile);
            Environment.SetEnvironmentVariable(ProxyRotationSettings.ProxyListVar, "http://ignored:1");
            var fromFile = new RoundRobinProxyRotationService();
            assert("proxy rotation file wins over env", fromFile.Count == 2);
            assert("proxy rotation file first", fromFile.AcquireNext()?.ProxyUrl == "http://file-proxy:3128");
        }
        finally
        {
            Environment.SetEnvironmentVariable(ProxyRotationSettings.ProxyListFileVar, null);
            Environment.SetEnvironmentVariable(ProxyRotationSettings.ProxyListVar, null);
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    private static void RunBatchJobInfra(Action<string, bool> assert)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"occam-batch-infra-{Guid.NewGuid():N}.json");
        try
        {
            using var store = new JsonFileBatchJobStore(dbPath);
            store.Initialize();

            var emptySubmit = new BatchSubmitRequest { Urls = [] };
            assert(
                "batch submit empty urls rejected",
                !BatchJobService.TryValidateSubmit(emptySubmit, out _, out _, out var emptyErr)
                    && emptyErr?.Code == "invalid_request");

            var overCap = new BatchSubmitRequest
            {
                Urls = Enumerable.Repeat("https://example.com/", BatchSettings.MaxUrls + 1).ToArray(),
            };
            assert(
                "batch submit over cap rejected",
                !BatchJobService.TryValidateSubmit(overCap, out _, out _, out _)
                    && overCap.Urls!.Length > BatchSettings.MaxUrls);

            var invalidUrl = new BatchSubmitRequest { Urls = ["not-a-url"] };
            assert(
                "batch submit invalid url rejected",
                !BatchJobService.TryValidateSubmit(invalidUrl, out _, out _, out _));

            var valid = new BatchSubmitRequest
            {
                Urls = ["https://example.com/a", "https://example.com/b"],
                BackendPolicy = "http",
                PlaybookPolicy = "off",
            };
            assert(
                "batch submit valid urls",
                BatchJobService.TryValidateSubmit(valid, out var urls, out var jobParams, out _)
                    && urls.Count == 2
                    && jobParams.BackendPolicy == "http");

            var jobId = Guid.NewGuid().ToString("N");
            var job = new BatchJobRecord(
                jobId,
                BatchJobStates.Queued,
                DateTimeOffset.UtcNow,
                jobParams,
                null,
                new BatchProgress(2, 0, 0, 0));
            store.InsertJob(job, urls);
            assert("batch store insert job", store.GetJob(jobId)?.State == BatchJobStates.Queued);
            assert("batch store item count", store.CountItems(jobId) == 2);

            var pending = store.ClaimNextPendingItem();
            assert("batch store claim pending", pending?.JobId == jobId && pending.Seq == 0);
            assert("batch store claim marks running", store.GetJob(jobId)?.Progress.Running == 1);
            assert("batch store claim promotes job", store.GetJob(jobId)?.State == BatchJobStates.Running);

            var okResult = new BatchItemResult(
                urls[0],
                true,
                "# ok",
                "http",
                42,
                null);
            var okJson = JsonSerializer.Serialize(okResult, BatchJsonContext.Default.BatchItemResult);
            store.MarkItemComplete(jobId, 0, ok: true, okJson, null, null);
            assert("batch store item done progress", store.GetJob(jobId)?.Progress.Done == 1);

            var pendingSecond = store.ClaimNextPendingItem();
            assert("batch store claim second item", pendingSecond?.Seq == 1);
            store.MarkItemComplete(jobId, 1, ok: false, null, "timeout", "timed out");
            assert("batch store job terminal", store.GetJob(jobId)?.State == BatchJobStates.Done);
            assert("batch store failed count", store.GetJob(jobId)?.Progress.Failed == 1);

            var idemKey = $"idem-{Guid.NewGuid():N}";
            var idemJobId = Guid.NewGuid().ToString("N");
            var idemJob = new BatchJobRecord(
                idemJobId,
                BatchJobStates.Queued,
                DateTimeOffset.UtcNow,
                jobParams,
                idemKey,
                new BatchProgress(1, 0, 0, 0));
            store.InsertJob(idemJob, ["https://example.com/idem"]);
            var found = store.FindJobByIdempotencyKey(idemKey, DateTimeOffset.UtcNow.AddHours(-1));
            assert("batch store idempotency lookup", found == idemJobId);

            var service = new BatchJobService(store);
            var (idemResponse, idemError) = service.Submit(new BatchSubmitRequest
            {
                Urls = ["https://example.com/other"],
                IdempotencyKey = idemKey,
            });
            assert("batch service idempotent submit", idemError is null && idemResponse?.JobId == idemJobId);

            var (missingStatus, missingErr) = service.GetStatus("does-not-exist");
            assert(
                "batch service status not found",
                missingStatus is null && missingErr?.Code == "job_not_found");

            var (status, _) = service.GetStatus(jobId);
            assert("batch service status progress total", status?.Progress.Total == 2);

            var (results, _) = service.GetResults(jobId, cursor: 0, limit: 10);
            assert("batch service results count", results?.Items.Length == 2);
            assert("batch service results first ok", results?.Items[0].Ok == true);
            assert("batch service results second failed", results?.Items[1].Ok == false);
        }
        finally
        {
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // Temp gate DB — ignore lock on Windows after dispose.
                }
            }
        }
    }

    private static void RunDiffCodecContract(Action<string, bool> assert)
    {
        var blocks = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Alpha", SourceSelector = "#a" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Beta", SourceSelector = "#b" },
        };
        var hAlpha = BlockDiff.Hash(blocks[0]);
        var hBeta = BlockDiff.Hash(blocks[1]);

        assert("diff hash stable", hAlpha == BlockDiff.Hash(blocks[0]) && hAlpha.Length == 16);
        assert("diff hash distinct per block", hAlpha != hBeta);

        // Prior had Alpha + an old block that's now gone. Beta is new.
        var diff = BlockDiff.Compute(blocks, [hAlpha, "0123456789abcdef"]);
        assert("diff added is the new block", diff.AddedBlocks.Length == 1 && diff.AddedBlocks[0].Hash == hBeta);
        assert("diff removed is the gone hash", diff.RemovedHashes is [.. ] && diff.RemovedHashes.Contains("0123456789abcdef"));
        assert("diff blockHashes are current", diff.BlockHashes.Length == 2 && diff.BlockHashes.Contains(hAlpha) && diff.BlockHashes.Contains(hBeta));

        // No change → no added/removed.
        var same = BlockDiff.Compute(blocks, [hAlpha, hBeta]);
        assert("diff no change → empty added/removed", same.AddedBlocks.Length == 0 && same.RemovedHashes.Length == 0);

        // Response serialization round-trips, omitted when null.
        var withDiff = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(true, new OccamTranscodeUrlInfo("https://e.com/", null), "# D", "http", [], Diff: diff),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("diff serializes object", withDiff.Contains("\"diff\"") && withDiff.Contains("\"blockHashes\""));
        var plain = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(true, new OccamTranscodeUrlInfo("https://e.com/", null), "# D", "http", []),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("diff omitted when null", !plain.Contains("\"diff\""));

        // #6 delta-as-primary: prior + delta must reconstruct the exact current content, and the
        // delta-primary response carries deltaOnly + empty markdown + a full-content contentHash.
        var prior = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Intro unchanged.", SourceSelector = "#a" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Old middle.", SourceSelector = "#b" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Footer removed.", SourceSelector = "#c" },
        };
        var priorHashes = prior.Select(BlockDiff.Hash).ToArray();
        var current = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Intro unchanged.", SourceSelector = "#a" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "New middle content.", SourceSelector = "#b" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Fresh appended block.", SourceSelector = "#d" },
        };
        var d6 = BlockDiff.Compute(current, priorHashes);
        assert("6 delta added = changed + new (2)", d6.AddedBlocks.Length == 2);
        assert("6 delta removed = old middle + footer (2)", d6.RemovedHashes.Length == 2);
        assert("6 delta blockHashes = current order (3)", d6.BlockHashes.Length == 3);

        // Consumer reconstruction: walk blockHashes; use an added block's text if present, else the
        // prior block with that hash (recomputed from prior text — the consumer holds prior blocks).
        var priorByHash = prior.ToDictionary(BlockDiff.Hash, b => b.Text, StringComparer.OrdinalIgnoreCase);
        var addedByHash = d6.AddedBlocks.ToDictionary(a => a.Hash, a => a.Text, StringComparer.OrdinalIgnoreCase);
        var reconstructed = d6.BlockHashes
            .Select(h => addedByHash.TryGetValue(h, out var t) ? t : priorByHash[h])
            .ToArray();
        assert("6 reconstruction matches current", reconstructed.SequenceEqual(current.Select(b => b.Text)));

        var fullMd = string.Join("\n\n", current.Select(b => b.Text));
        var deltaResp = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(
                true, new OccamTranscodeUrlInfo("https://e.com/", null), string.Empty, "http", [],
                Diff: d6, ContentHash: OccamMcp.Core.Compile.ContentHashToken.BareHex(fullMd), DeltaOnly: true),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("6 deltaOnly true in JSON", deltaResp.Contains("\"deltaOnly\":true"));
        assert("6 delta-primary empty markdown", deltaResp.Contains("\"markdown\":\"\""));
        assert("6 delta-primary carries diff", deltaResp.Contains("\"diff\""));
        assert("6 delta-primary carries contentHash for verification", deltaResp.Contains("\"contentHash\""));
    }

    // ADR-0001 PR-1: the codec seam exists, resolves the default, and the passthrough codec is a
    // true no-op (returns the compiled markdown byte-for-byte) so nothing about the output changes.
    private static void RunKnowledgeCodecRegistry(Action<string, bool> assert)
    {
        var registry = new OccamMcp.Core.Codecs.KnowledgeCodecRegistry(
            [
                new OccamMcp.Core.Codecs.MarkdownPassthroughCodec(),
                new OccamMcp.Core.Codecs.CompactMarkdownCodec(),
                new OccamMcp.Core.Codecs.JsonKnowledgeCodec(),
            ]);

        assert("codec: registry resolves markdown-passthrough",
            registry.TryGet(OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id, out var codec) && codec is not null);
        assert("codec: unknown id -> not found", !registry.TryGet("does-not-exist", out _));
        assert("codec: empty id does not silently return default", !registry.TryGet("  ", out var emptyHit) && emptyHit is null);
        assert("codec: default is the passthrough",
            registry.Default.Descriptor.CodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);
        assert("codec: default Trust=Builtin",
            registry.Default.Descriptor.Trust == OccamMcp.Core.Codecs.KnowledgeCodecTrust.Builtin);

        var d = registry.Default.Descriptor;
        assert("codec: descriptor canEncode", d.CanEncode);
        assert("codec: passthrough is encode-only (no decode)", !d.CanDecode);
        assert("codec: passthrough is lossless", d.Mode == OccamMcp.Core.Codecs.KnowledgeCodecMode.Lossless);
        assert("codec: passthrough is deterministic", d.Deterministic);

        const string md = "# Title\n\nBody with **markup** and a | pipe.\n";
        var result = registry.Default.Encode(
            OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown(md), OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None);
        assert("codec: passthrough round-trips markdown unchanged", result.Surface == md);
        assert("codec: result carries codec id", result.CodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);

        // Master migration PR-D: representative compatibility fixtures freeze all Markdown-specific
        // public behaviour. The codec does not normalize whitespace/newlines, escaping, Unicode,
        // tables, code fences, links, blockquotes, or in-band truncation markers.
        string[] markdownFixtures =
        [
            "# Heading\r\n\r\n> Quote with [link](https://example.com/?a=1&b=2)\r\n",
            "| a | b |\n| --- | --- |\n| 1 | `x|y` |\n",
            "```csharp\nConsole.WriteLine(\"<>&\");\n```\n",
            "Привет, 世界 — café ‘quotes’ 😀\n",
            "before\n\n<!-- SNIP reason=head_safe omitted_tokens=42 -->\n\nafter\n",
            "",
        ];
        var deterministic = true;
        var exact = true;
        var hashesStable = true;
        foreach (var fixture in markdownFixtures)
        {
            var view = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown(
                fixture,
                new OccamMcp.Core.Knowledge.KnowledgeDocument(
                    [new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", "must not replace markdown")],
                    []));
            var first = registry.Default.Encode(view, new OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions("ignored-focus"));
            var second = registry.Default.Encode(view, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None);
            exact &= first.Surface == fixture;
            deterministic &= first == second;
            hashesStable &=
                OccamMcp.Core.Receipts.ReceiptCanonicalizer.ContentHash(first.Surface)
                == OccamMcp.Core.Receipts.ReceiptCanonicalizer.ContentHash(fixture);
        }

        assert("codec PR-D: representative Markdown fixtures are byte-identical", exact);
        assert("codec PR-D: same view is deterministic across surface hints", deterministic);
        assert("codec PR-D: receipt contentHash remains compatible", hashesStable);

        var ctor = typeof(OccamMcp.Core.Codecs.MarkdownPassthroughCodec).GetConstructors().Single();
        assert("codec PR-D: default codec has no extraction/network constructor dependencies",
            ctor.GetParameters().Length == 0);

        // PR-2 compact-markdown: renders from the block/table IR in C#.
        assert("codec: registry resolves compact-markdown",
            registry.TryGet(OccamMcp.Core.Codecs.CompactMarkdownCodec.Id, out var compact) && compact is not null);
        assert("codec: compact is lossy", compact!.Descriptor.Mode == OccamMcp.Core.Codecs.KnowledgeCodecMode.Lossy);
        assert("codec: compact is deterministic + encode-only",
            compact.Descriptor.Deterministic && compact.Descriptor.CanEncode && !compact.Descriptor.CanDecode);

        // PR-3: worker extract → codec-local Knowledge IR (adapter), then codec renders from the IR.
        var blocks = new[]
        {
            new WorkerExtractBlockInfo { Type = "heading", Text = "H" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "body" },
            new WorkerExtractBlockInfo { Type = "list_item", Text = "item" },
        };
        var blockDoc = OccamMcp.Core.Knowledge.WorkerKnowledgeMapper.FromExtract(blocks, null);
        assert("ir: mapper preserves blocks", blockDoc.Blocks.Count == 3 && blockDoc.Blocks[0].Type == "heading");

        var blockView = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("IGNORED-FALLBACK", blockDoc);
        var blockOut = compact.Encode(blockView, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("codec: compact renders blocks by type", blockOut == "## H\n\nbody\n\n- item\n");
        assert("codec: compact does not fall back when blocks present", !blockOut.Contains("IGNORED-FALLBACK"));

        // PR-3 part 2: heading level flows worker → IR → codec (rebuilt as #×level; default h2 when absent).
        var leveled = new[]
        {
            new WorkerExtractBlockInfo { Type = "heading", Text = "Top", Level = 1 },
            new WorkerExtractBlockInfo { Type = "heading", Text = "Sub", Level = 3 },
        };
        var leveledDoc = OccamMcp.Core.Knowledge.WorkerKnowledgeMapper.FromExtract(leveled, null);
        assert("ir: mapper preserves heading level", leveledDoc.Blocks[0].Level == 1 && leveledDoc.Blocks[1].Level == 3);
        var leveledOut = compact.Encode(
            OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("", leveledDoc), OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("codec: compact rebuilds heading levels", leveledOut == "# Top\n\n### Sub\n");

        var tables = new[]
        {
            new WorkerExtractTableInfo { Headers = ["a", "b"], Rows = [["1", "2"]] },
        };
        var tableDoc = OccamMcp.Core.Knowledge.WorkerKnowledgeMapper.FromExtract(null, tables);
        assert("ir: mapper preserves tables", tableDoc.Tables.Count == 1 && tableDoc.Tables[0].Headers.Count == 2);
        var tableOut = compact.Encode(
            OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("", tableDoc), OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("codec: compact renders table losslessly", tableOut == "| a | b |\n| --- | --- |\n| 1 | 2 |\n");

        var fallback = compact.Encode(
            OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("# only markdown"), OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("codec: compact falls back to markdown when no IR", fallback == "# only markdown");

        var again = compact.Encode(blockView, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("codec: compact is deterministic (same view -> same surface)", again == blockOut);

        // Wiring: the registry resolves through AddOccamCore() (the seam is registered, not just constructible).
        var services = new ServiceCollection();
        services.AddOccamCore();
        using var sp = services.BuildServiceProvider();
        var diRegistry = sp.GetService<OccamMcp.Core.Codecs.KnowledgeCodecRegistry>();
        assert("codec: registry resolves via AddOccamCore DI",
            diRegistry is not null
            && diRegistry.TryGet(OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id, out _)
            && diRegistry.TryGet(OccamMcp.Core.Codecs.CompactMarkdownCodec.Id, out _)
            && diRegistry.TryGet(OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id, out _));
        assert("codec: DI default remains markdown-passthrough",
            diRegistry!.DefaultCodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);

        RunKnowledgeCodecExtensionSeam(assert);
    }

    // Master PR-E: fail-closed selection, trust tiers, opt-in extension registration (no marketplace).
    private static void RunKnowledgeCodecExtensionSeam(Action<string, bool> assert)
    {
        var builtinRegistry = new OccamMcp.Core.Codecs.KnowledgeCodecRegistry(
            [
                new OccamMcp.Core.Codecs.MarkdownPassthroughCodec(),
                new OccamMcp.Core.Codecs.CompactMarkdownCodec(),
                new OccamMcp.Core.Codecs.JsonKnowledgeCodec(),
            ]);

        var defaultSel = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(builtinRegistry, null);
        assert("codec PR-E: null id → default markdown-passthrough",
            defaultSel.Ok
            && defaultSel.SelectedId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id
            && defaultSel.Codec is OccamMcp.Core.Codecs.MarkdownPassthroughCodec);

        var explicitExperimental = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(
            builtinRegistry, OccamMcp.Core.Codecs.CompactMarkdownCodec.Id);
        assert("codec PR-E: explicit compact-markdown allowed (BuiltinExperimental)",
            explicitExperimental.Ok
            && explicitExperimental.SelectedId == OccamMcp.Core.Codecs.CompactMarkdownCodec.Id);

        var explicitJson = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(
            builtinRegistry, OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id);
        assert("codec PR-E: explicit knowledge-json allowed (BuiltinExperimental)",
            explicitJson.Ok
            && explicitJson.SelectedId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id
            && explicitJson.Codec is OccamMcp.Core.Codecs.JsonKnowledgeCodec);

        var unsupported = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(builtinRegistry, "flare-experimental");
        assert("codec PR-E: unsupported codec fails closed (no silent default)",
            !unsupported.Ok
            && unsupported.Codec is null
            && unsupported.FailureCode == OccamMcp.Core.Codecs.KnowledgeCodecFailureCodes.UnsupportedCodec
            && unsupported.Status == OccamMcp.Core.Codecs.KnowledgeCodecSelectStatus.UnsupportedCodec);

        var unknownProfile = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(
            builtinRegistry,
            null,
            new OccamMcp.Core.Codecs.KnowledgeCodecSelectOptions(CapabilityProfile: "gpt-imaginary-9000"));
        assert("codec PR-E: unknown capability profile fails closed",
            !unknownProfile.Ok
            && unknownProfile.FailureCode == OccamMcp.Core.Codecs.KnowledgeCodecFailureCodes.UnknownCapabilityProfile);

        var defaultProfile = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(
            builtinRegistry,
            null,
            new OccamMcp.Core.Codecs.KnowledgeCodecSelectOptions(CapabilityProfile: "default"));
        assert("codec PR-E: capability profile 'default' accepted", defaultProfile.Ok);

        // Opt-in extension: denied when registry policy is disabled.
        var deniedRegister = builtinRegistry.TryRegisterExtension(new FakeOptInCodec(), out var denyCode);
        assert("codec PR-E: extension register denied when opt-in disabled",
            !deniedRegister && denyCode == OccamMcp.Core.Codecs.KnowledgeCodecFailureCodes.CodecExtensionNotAllowed);

        var openRegistry = new OccamMcp.Core.Codecs.KnowledgeCodecRegistry(
            [new OccamMcp.Core.Codecs.MarkdownPassthroughCodec(), new OccamMcp.Core.Codecs.CompactMarkdownCodec()],
            new OccamMcp.Core.Codecs.KnowledgeCodecExtensionOptions(AllowOptInExtensions: true));
        assert("codec PR-E: extension register succeeds when opt-in enabled",
            openRegistry.TryRegisterExtension(new FakeOptInCodec(), out var okCode) && okCode is null);
        assert("codec PR-E: registered extension is selectable",
            OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(openRegistry, FakeOptInCodec.Id).Ok);

        // Allow-list: register only if id is listed.
        var allowListRegistry = new OccamMcp.Core.Codecs.KnowledgeCodecRegistry(
            [new OccamMcp.Core.Codecs.MarkdownPassthroughCodec()],
            new OccamMcp.Core.Codecs.KnowledgeCodecExtensionOptions(
                AllowOptInExtensions: true,
                AllowedExtensionIds: new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "other-ext" }));
        assert("codec PR-E: allow-list rejects unlisted extension id",
            !allowListRegistry.TryRegisterExtension(new FakeOptInCodec(), out var listCode)
            && listCode == OccamMcp.Core.Codecs.KnowledgeCodecFailureCodes.CodecExtensionNotAllowed);

        // Cannot replace the default via extension registration.
        var hijack = new FakeOptInCodec(idOverride: OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);
        assert("codec PR-E: cannot register over default codec id",
            !openRegistry.TryRegisterExtension(hijack, out var hijackCode)
            && hijackCode == OccamMcp.Core.Codecs.KnowledgeCodecFailureCodes.CodecAlreadyRegistered);

        // Encode-disabled codec cannot be selected.
        var noEncodeRegistry = new OccamMcp.Core.Codecs.KnowledgeCodecRegistry(
            [new OccamMcp.Core.Codecs.MarkdownPassthroughCodec(), new FakeNoEncodeBuiltinCodec()]);
        var noEncodeSel = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(noEncodeRegistry, FakeNoEncodeBuiltinCodec.Id);
        assert("codec PR-E: CanEncode=false → codec_cannot_encode",
            !noEncodeSel.Ok
            && noEncodeSel.FailureCode == OccamMcp.Core.Codecs.KnowledgeCodecFailureCodes.CodecCannotEncode);

        // Layering: extension codec encodes from MaterializedKnowledgeView only (no extraction deps).
        var view = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("# x\n");
        var extSel = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(openRegistry, FakeOptInCodec.Id);
        assert("codec PR-E: extension encodes view without acquisition deps",
            extSel.Codec!.Encode(view, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface == "FAKE:# x\n");
    }

    private sealed class FakeOptInCodec : OccamMcp.Core.Codecs.IKnowledgeCodec
    {
        public const string Id = "fake-optin";
        private readonly string _id;

        public FakeOptInCodec(string? idOverride = null) => _id = idOverride ?? Id;

        public OccamMcp.Core.Codecs.KnowledgeCodecDescriptor Descriptor => new(
            CodecId: _id,
            Version: "0.0.1",
            SupportedIrVersion: "0.1",
            CanEncode: true,
            CanDecode: false,
            Mode: OccamMcp.Core.Codecs.KnowledgeCodecMode.Lossy,
            Deterministic: true,
            Trust: OccamMcp.Core.Codecs.KnowledgeCodecTrust.OptInExtension);

        public OccamMcp.Core.Codecs.KnowledgeCodecResult Encode(
            OccamMcp.Core.Knowledge.MaterializedKnowledgeView view,
            OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions options) =>
            new("FAKE:" + view.Markdown, _id, Descriptor.Version);
    }

    private sealed class FakeNoEncodeBuiltinCodec : OccamMcp.Core.Codecs.IKnowledgeCodec
    {
        public const string Id = "fake-no-encode";

        public OccamMcp.Core.Codecs.KnowledgeCodecDescriptor Descriptor => new(
            CodecId: Id,
            Version: "0.0.1",
            SupportedIrVersion: "0.1",
            CanEncode: false,
            CanDecode: false,
            Mode: OccamMcp.Core.Codecs.KnowledgeCodecMode.Lossless,
            Deterministic: true,
            Trust: OccamMcp.Core.Codecs.KnowledgeCodecTrust.BuiltinExperimental);

        public OccamMcp.Core.Codecs.KnowledgeCodecResult Encode(
            OccamMcp.Core.Knowledge.MaterializedKnowledgeView view,
            OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions options) =>
            throw new InvalidOperationException("encode disabled");
    }

    // ADR-0001 PR-4 / ADR-0002: the planner owns semantic retention (which assertions survive a token
    // budget); codecs never select or drop. Deterministic here — each block text is 40 chars → exactly
    // 10 estimated tokens (ASCII len/4), so a budget of 15 retains exactly one.
    private static void RunMaterializationPlanner(Action<string, bool> assert)
    {
        var planner = new OccamMcp.Core.Knowledge.MaterializationPlanner();
        string blk(char c) => new(c, 40); // 40 chars → 10 tokens

        var doc = new OccamMcp.Core.Knowledge.KnowledgeDocument(
            [
                new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", blk('a')),
                new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", blk('b')),
                new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", blk('c')),
            ],
            []);

        var full = planner.Plan("MD", doc, OccamMcp.Core.Knowledge.MaterializationPolicy.None);
        assert("planner: no budget keeps all blocks", full.Knowledge!.Blocks.Count == 3);
        assert("planner: preserves markdown fallback", full.Markdown == "MD");

        var tight = planner.Plan("MD", doc, new OccamMcp.Core.Knowledge.MaterializationPolicy(MaxTokens: 15));
        assert("planner: budget drops blocks", tight.Knowledge!.Blocks.Count == 1);
        assert("planner: budget keeps prefix in reading order", tight.Knowledge.Blocks[0].Text == blk('a'));

        // Salience-aware: under a one-block budget the salient (last) block is retained over earlier
        // low-salience ones, and the surface is still in reading order.
        var salientDoc = new OccamMcp.Core.Knowledge.KnowledgeDocument(
            [
                new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", blk('x'), Salience: 0.1),
                new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", blk('y'), Salience: 0.1),
                new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", blk('z'), Salience: 0.9),
            ],
            []);
        var sal = planner.Plan("MD", salientDoc, new OccamMcp.Core.Knowledge.MaterializationPolicy(MaxTokens: 15, FocusQuery: "q"));
        assert("planner: salience-aware retains the salient block", sal.Knowledge!.Blocks.Count == 1 && sal.Knowledge.Blocks[0].Text == blk('z'));

        // End-to-end: planner drops, codec serializes only what survived.
        var codec = new OccamMcp.Core.Codecs.CompactMarkdownCodec();
        var surface = codec.Encode(tight, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("planner→codec: codec renders only retained knowledge",
            surface.Contains(blk('a')) && !surface.Contains(blk('b')) && !surface.Contains(blk('c')));

        // PR-C: canonical refs pass through under budget; codecs still ignore them (markdown path unchanged).
        var source = OccamMcp.Core.Knowledge.Canonical.Source.Create(
            OccamMcp.Core.Knowledge.Canonical.SourceId.New(),
            OccamMcp.Core.Knowledge.Canonical.SourceKind.WebPage,
            "https://example.com/pr-c",
            DateTimeOffset.UtcNow,
            contentHash: "aabb");
        var evidence = OccamMcp.Core.Knowledge.Canonical.Evidence.Create(
            OccamMcp.Core.Knowledge.Canonical.EvidenceId.New(),
            source.Id,
            OccamMcp.Core.Knowledge.Canonical.EvidenceLocator.SourceSelector("main > p"),
            OccamMcp.Core.Knowledge.Canonical.EvidenceKind.ContentBlock,
            DateTimeOffset.UtcNow,
            contentHash: "leaf1",
            excerpt: "claim text");
        var claim = OccamMcp.Core.Knowledge.Canonical.ClaimCandidate.Create(
            OccamMcp.Core.Knowledge.Canonical.ClaimCandidateId.New(),
            "claim text",
            OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim,
            [evidence.Id],
            DateTimeOffset.UtcNow);
        var prov = OccamMcp.Core.Knowledge.Canonical.KnowledgeProvenance.Create(
            OccamMcp.Core.Knowledge.Canonical.ProvenanceId.New(),
            source.Id,
            [evidence.Id],
            receiptContentHash: "aabb",
            blockLeafHash: "leaf1");

        var withCanon = planner.Plan(
            "MD",
            doc,
            new OccamMcp.Core.Knowledge.MaterializationPolicy(MaxTokens: 15),
            sourceRefs: [source],
            evidenceRefs: [evidence],
            claims: [claim],
            provenance: [prov]);
        assert("planner PR-C: budget still drops document blocks", withCanon.Knowledge!.Blocks.Count == 1);
        assert("planner PR-C: SourceRefs pass through under budget", withCanon.SourceRefs is { Count: 1 });
        assert("planner PR-C: EvidenceRefs pass through under budget", withCanon.EvidenceRefs is { Count: 1 });
        assert("planner PR-C: Claims retained when claim budget fits", withCanon.Claims is { Count: 1 });
        assert("planner PR-C: Provenance retained with claim", withCanon.Provenance is { Count: 1 } && withCanon.Provenance[0].BlockLeafHash == "leaf1");

        // R2: evidence-preserving keeps Canonical under a tight MaxTokens; default may drop claims.
        var manyClaims = Enumerable.Range(0, 6).Select(i =>
            OccamMcp.Core.Knowledge.Canonical.ClaimCandidate.Create(
                OccamMcp.Core.Knowledge.Canonical.ClaimCandidateId.New(),
                $"Long claim statement number {i} about MaterializationPlanner retention policy and evidence closure with enough words to consume tokens.",
                OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim,
                [evidence.Id],
                DateTimeOffset.UtcNow)).ToList();
        var defaultPruned = planner.Plan(
            "MD",
            doc,
            new OccamMcp.Core.Knowledge.MaterializationPolicy(MaxTokens: 64, ProvenancePolicy: "default"),
            sourceRefs: [source],
            evidenceRefs: [evidence],
            claims: manyClaims,
            provenance: [prov]);
        var preserved = planner.Plan(
            "MD",
            doc,
            new OccamMcp.Core.Knowledge.MaterializationPolicy(MaxTokens: 64, ProvenancePolicy: "evidence-preserving"),
            sourceRefs: [source],
            evidenceRefs: [evidence],
            claims: manyClaims,
            provenance: [prov]);
        assert("planner R2: default policy prunes claims under tight budget",
            (defaultPruned.Claims?.Count ?? 0) < 6);
        assert("planner R2: evidence-preserving keeps all claims under same budget",
            preserved.Claims is { Count: 6 });
        assert("planner R2: evidence-preserving retains more claims than default",
            (preserved.Claims?.Count ?? 0) > (defaultPruned.Claims?.Count ?? 0));
        assert("planner PR-C: HasCanonicalKnowledge", withCanon.HasCanonicalKnowledge);

        var passthrough = new OccamMcp.Core.Codecs.MarkdownPassthroughCodec();
        var surfaceWithCanon = passthrough.Encode(withCanon, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("planner PR-C: markdown codec ignores canonical refs (surface == Markdown)", surfaceWithCanon == "MD");

        // Legacy Adapter → Planner: view carries provenance for one extract.
        var outcome = new TranscodeOutcome(
            true,
            "# Hi\n\nHello.\n",
            "https://example.com/page",
            "http",
            null,
            null,
            Blocks:
            [
                new OccamMcp.Core.Workers.WorkerExtractBlockInfo
                {
                    Type = "paragraph",
                    Text = "Hello.",
                    SourceSelector = "p",
                },
            ]);
        var canonical = OccamMcp.Core.Knowledge.Legacy.TranscodeToCanonical.TryAdapt(
            "https://example.com/",
            outcome,
            contentHash: "cafe",
            retrievedAt: new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero));
        assert("planner PR-C: legacy adapt ok", canonical is not null);
        var fromLegacy = planner.Plan("MD-LEGACY", OccamMcp.Core.Knowledge.WorkerKnowledgeMapper.FromExtract(outcome.Blocks, null), OccamMcp.Core.Knowledge.MaterializationPolicy.None, canonical);
        assert("planner PR-C: legacy→view SourceRefs", fromLegacy.SourceRefs is { Count: 1 });
        assert("planner PR-C: legacy→view EvidenceRefs", fromLegacy.EvidenceRefs is { Count: 1 });
        assert(
            "planner PR-C: legacy→view Claims are ExtractedClaim only",
            fromLegacy.Claims is { Count: 1 }
            && fromLegacy.Claims[0].ClaimKind == OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim);
        assert(
            "planner PR-C: legacy→view provenance bridges contentHash",
            fromLegacy.Provenance is { Count: 1 } && fromLegacy.Provenance[0].ReceiptContentHash == "cafe");

        // DI wiring.
        var services = new ServiceCollection();
        services.AddOccamCore();
        using var sp = services.BuildServiceProvider();
        assert("planner: resolves via AddOccamCore DI",
            sp.GetService<OccamMcp.Core.Knowledge.MaterializationPlanner>() is not null);
    }

    /// <summary>
    /// Runtime migration PR-1…PR-9 guards: byte/hash parity for planner→codec vs legacy compiler,
    /// architecture dependency forbids, ExtractedKnowledgeBundle, SourceSurface, and DI cutover.
    /// </summary>
    private static void RunRuntimeMaterializationMigration(Action<string, bool> assert)
    {
        // --- PR-1 parity: planner+passthrough surface == TranscodeCompiler for representative options ---
        var fixtures = new (string Md, OccamTranscodeOptions Options)[]
        {
            ("# Title\n\nBody paragraph one.\n\n## Section\n\nMore body.\n", OccamTranscodeOptions.Default),
            ("# Title\n\nBody paragraph one.\n\n## Section\n\nMore body.\n",
                new OccamTranscodeOptions { MaxTokens = 128, FitMarkdown = true, FocusQuery = "section" }),
            ("# API Reference\n\nDetails.\n\n# Other\n\nNoise.\n",
                new OccamTranscodeOptions { ContentSelectors = ["# API Reference"] }),
            ("Привет, 世界 — café\r\n\r\n```csharp\nvar x = 1;\n```\n", OccamTranscodeOptions.Default),
        };

        var planner = new OccamMcp.Core.Knowledge.MaterializationPlanner();
        var passthrough = new OccamMcp.Core.Codecs.MarkdownPassthroughCodec();
        var parityOk = true;
        var hashOk = true;
        foreach (var (md, options) in fixtures)
        {
            var legacy = TranscodeCompiler.Apply(md, options);
            var bundle = new OccamMcp.Core.Knowledge.Extraction.ExtractedKnowledgeBundle(
                OccamMcp.Core.Knowledge.SourceSurface.Markdown(md),
                OccamMcp.Core.Knowledge.KnowledgeDocument.Empty,
                Canonical: null,
                FinalUrl: "https://example.com/",
                Backend: "http");
            var request = OccamMcp.Core.Knowledge.MaterializationRequest.FromTranscodeOptions(options);
            var planned = planner.Plan(request, bundle);
            var encoded = passthrough.Encode(planned.View, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
            parityOk &= encoded == legacy.Markdown
                && planned.SelectorsMatched == legacy.SelectorsMatched
                && planned.Truncated == legacy.Truncated
                && planned.TokensEstimated == legacy.TokensEstimated
                && planned.TruncationStrategy == legacy.TruncationStrategy;
            hashOk &= OccamMcp.Core.Compile.ContentHashToken.BareHex(encoded)
                == OccamMcp.Core.Compile.ContentHashToken.BareHex(legacy.Markdown);
        }

        assert("migration parity: planner→passthrough == TranscodeCompiler surface/meta", parityOk);
        assert("migration parity: contentHash identical across planner path", hashOk);

        // --- Architecture dependency guardrails (source + type inspection) ---
        var coreRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FFOccamMcp.Core"));
        if (!Directory.Exists(coreRoot))
        {
            // Fallback: walk up from CWD (gate may run from repo root).
            coreRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "src", "FFOccamMcp.Core"));
        }

        assert("migration arch: Core source tree located", Directory.Exists(coreRoot));
        if (Directory.Exists(coreRoot))
        {
            var codecFiles = Directory.GetFiles(Path.Combine(coreRoot, "Codecs"), "*.cs");
            var codecImportsPlanner = codecFiles.Any(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("MaterializationPlanner", StringComparison.Ordinal)
                    || text.Contains("using OccamMcp.Core.Workers", StringComparison.Ordinal)
                    || text.Contains("using OccamMcp.Core.Routing", StringComparison.Ordinal)
                    || text.Contains("using OccamMcp.Core.Knowledge.Extraction", StringComparison.Ordinal);
            });
            assert("migration arch: Codecs must not import Planner/Workers/Routing/Extraction", !codecImportsPlanner);

            var jsonCodecPath = Path.Combine(coreRoot, "Codecs", "JsonKnowledgeCodec.cs");
            assert("migration arch: JsonKnowledgeCodec source present", File.Exists(jsonCodecPath));
            if (File.Exists(jsonCodecPath))
            {
                var jsonSrc = File.ReadAllText(jsonCodecPath);
                assert("migration arch: JsonKnowledgeCodec has no Planner reference",
                    !jsonSrc.Contains("MaterializationPlanner", StringComparison.Ordinal));
                assert("migration arch: JsonKnowledgeCodec has no Extraction reference",
                    !jsonSrc.Contains("Extraction", StringComparison.Ordinal));
                assert("migration arch: JsonKnowledgeCodec has no Routing reference",
                    !jsonSrc.Contains("Routing", StringComparison.Ordinal));
                assert("migration arch: JsonKnowledgeCodec has no Workers reference",
                    !jsonSrc.Contains("Workers", StringComparison.Ordinal));
            }

            var pipelinePath = Path.Combine(coreRoot, "Routing", "TranscodePipeline.cs");
            if (File.Exists(pipelinePath))
            {
                var pipeSrc = File.ReadAllText(pipelinePath);
                assert("migration arch: pipeline has no knowledge-json hardcode branch",
                    !pipeSrc.Contains("knowledge-json", StringComparison.OrdinalIgnoreCase)
                    && !pipeSrc.Contains("JsonKnowledgeCodec", StringComparison.Ordinal));
            }

            var plannerPath = Path.Combine(coreRoot, "Knowledge", "MaterializationPlanner.cs");
            if (File.Exists(plannerPath))
            {
                var plannerSrc = File.ReadAllText(plannerPath);
                assert("migration arch: planner does not reference JsonKnowledgeCodec",
                    !plannerSrc.Contains("JsonKnowledgeCodec", StringComparison.Ordinal)
                    && !plannerSrc.Contains("knowledge-json", StringComparison.OrdinalIgnoreCase));
            }

            var canonicalFiles = Directory.GetFiles(Path.Combine(coreRoot, "Knowledge", "Canonical"), "*.cs");
            var canonicalHasMarkdownField = canonicalFiles.Any(f =>
            {
                var text = File.ReadAllText(f);
                return text.Contains("string Markdown", StringComparison.Ordinal)
                    || text.Contains("Markdown {", StringComparison.Ordinal);
            });
            assert("migration arch: Canonical models have no Markdown fields", !canonicalHasMarkdownField);
        }

        var factProps = typeof(OccamMcp.Core.Knowledge.Canonical.Fact).GetProperties();
        assert("migration arch: Fact has no Markdown property",
            factProps.All(p => !string.Equals(p.Name, "Markdown", StringComparison.Ordinal)));

        // --- Bundle + acquisition adapter ---
        var outcome = new TranscodeOutcome(
            true,
            "# Hi\n\nHello world.\n",
            "https://example.com/page",
            "http",
            null,
            null,
            Blocks:
            [
                new WorkerExtractBlockInfo { Type = "heading", Text = "Hi", Level = 1 },
                new WorkerExtractBlockInfo { Type = "paragraph", Text = "Hello world." },
            ]);
        var adapted = OccamMcp.Core.Knowledge.Extraction.ExtractedKnowledgeAdapter.TryAdapt(
            "https://example.com/page", outcome);
        assert("migration bundle: adapter succeeds", adapted is not null);
        assert("migration bundle: source surface is markdown", adapted!.SourceSurface.IsMarkdown);
        assert("migration bundle: document has blocks", adapted.Document.Blocks.Count == 2);
        assert("migration bundle: surface spans attached",
            adapted.Document.Blocks.All(b => b.Span is not null));
        assert("migration bundle: canonical present", adapted.Canonical is not null);
        assert("migration bundle: fail-closed on Ok=false",
            OccamMcp.Core.Knowledge.Extraction.ExtractedKnowledgeAdapter.TryAdapt(
                "https://example.com/",
                new TranscodeOutcome(false, null, null, null, "thin_extract", "x")) is null);

        // Acquisition adapter does not require TranscodeOutcome.
        var acq = OccamMcp.Core.Knowledge.Legacy.TranscodeToCanonical.TryAdaptAcquisition(
            "https://example.com/",
            "https://example.com/",
            "http",
            "# Only\n",
            blocks: null,
            tables: null,
            meta: null);
        assert("migration acquisition: surface-only Excerpt", acq is { Evidence.Count: 1 });
        assert("migration acquisition: locator is surface (not markdown-typed)",
            acq!.Evidence[0].Locator.Value == "surface");

        // --- SourceSurface / MaterializationRequest ---
        var surface = OccamMcp.Core.Knowledge.SourceSurface.Markdown("# x\n");
        assert("migration surface: media type", surface.MediaType == OccamMcp.Core.Knowledge.SourceSurface.MarkdownMediaType);
        var req = OccamMcp.Core.Knowledge.MaterializationRequest.FromTranscodeOptions(
            new OccamTranscodeOptions { MaxTokens = 256, FocusQuery = "q", FitMarkdown = true, JsonBlocks = true });
        assert("migration request: maps budget/focus/fit",
            req.MaxTokens == 256 && req.FocusQuery == "q" && req.FitMarkdown && req.ExposePublicBlocks);

        // --- DI cutover: pipeline resolves planner + registry default id ---
        var services = new ServiceCollection();
        services.AddOccamCore();
        using var sp = services.BuildServiceProvider();
        var pipeline = sp.GetRequiredService<TranscodePipeline>();
        var registry = sp.GetRequiredService<OccamMcp.Core.Codecs.KnowledgeCodecRegistry>();
        var diPlanner = sp.GetRequiredService<OccamMcp.Core.Knowledge.MaterializationPlanner>();
        assert("migration DI: TranscodePipeline resolves", pipeline is not null);
        assert("migration DI: MaterializationPlanner resolves", diPlanner is not null);
        assert("migration DI: registry DefaultCodecId configured",
            registry.DefaultCodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);
        var selected = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(registry, null);
        assert("migration DI: selector returns configured default",
            selected.Ok && selected.SelectedId == registry.DefaultCodecId);

        // View is representation-tagged (SourceSurface), Markdown is accessor only.
        var view = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("# a\n");
        assert("migration view: Surface is primary", view.Surface.IsMarkdown && view.Markdown == "# a\n");
    }

    // PR-A: Canonical Knowledge domain invariants (ADR-0003). Pure records — no store, no pipeline wiring.
    private static void RunCanonicalKnowledgeDomain(Action<string, bool> assert)
    {
        var source = OccamMcp.Core.Knowledge.Canonical.Source.Create(
            OccamMcp.Core.Knowledge.Canonical.SourceId.New(),
            OccamMcp.Core.Knowledge.Canonical.SourceKind.WebPage,
            "https://example.com/page",
            DateTimeOffset.UtcNow,
            contentHash: "abc");

        var evidence = OccamMcp.Core.Knowledge.Canonical.Evidence.Create(
            OccamMcp.Core.Knowledge.Canonical.EvidenceId.New(),
            source.Id,
            OccamMcp.Core.Knowledge.Canonical.EvidenceLocator.SourceSelector("main > p:nth-child(1)"),
            OccamMcp.Core.Knowledge.Canonical.EvidenceKind.ContentBlock,
            DateTimeOffset.UtcNow,
            excerpt: "Hello");

        assert("canonical: evidence references source", evidence.SourceId.Equals(source.Id));
        assert("canonical: source/evidence ids are opaque non-empty",
            source.Id.Value.Length == 32 && evidence.Id.Value.Length == 32);

        var claim = OccamMcp.Core.Knowledge.Canonical.ClaimCandidate.Create(
            OccamMcp.Core.Knowledge.Canonical.ClaimCandidateId.New(),
            "Hello is present",
            OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim,
            [evidence.Id],
            DateTimeOffset.UtcNow);
        assert("canonical: claim candidate may omit confidence (not computed)", claim.Confidence is null);
        assert("canonical: claim candidate needs no ValidationState", claim.Statement.Length > 0);

        var prov = OccamMcp.Core.Knowledge.Canonical.KnowledgeProvenance.Create(
            OccamMcp.Core.Knowledge.Canonical.ProvenanceId.New(),
            source.Id,
            [evidence.Id],
            observedAt: DateTimeOffset.UtcNow,
            extractionMethod: "http-extract",
            extractionVersion: "0.9.0",
            receiptContentHash: source.ContentHash);

        // Supported Fact without provenance must fail.
        var supportedWithoutProvThrew = false;
        try
        {
            _ = OccamMcp.Core.Knowledge.Canonical.Fact.Create(
                OccamMcp.Core.Knowledge.Canonical.FactId.New(),
                "subj", "pred", "obj",
                OccamMcp.Core.Knowledge.Canonical.ClaimKind.NormalizedFact,
                OccamMcp.Core.Knowledge.Canonical.ValidationState.Supported,
                []);
        }
        catch (ArgumentException)
        {
            supportedWithoutProvThrew = true;
        }

        assert("canonical: Supported Fact requires provenance", supportedWithoutProvThrew);

        var unvalidated = OccamMcp.Core.Knowledge.Canonical.Fact.Create(
            OccamMcp.Core.Knowledge.Canonical.FactId.New(),
            "subj", "pred", "obj",
            OccamMcp.Core.Knowledge.Canonical.ClaimKind.NormalizedFact,
            OccamMcp.Core.Knowledge.Canonical.ValidationState.Unvalidated,
            []);
        assert("canonical: Unvalidated Fact may omit provenance", unvalidated.ProvenanceRefs.Count == 0);
        assert("canonical: Fact confidence is not auto-assigned", unvalidated.Confidence is null);

        var supported = OccamMcp.Core.Knowledge.Canonical.Fact.Create(
            OccamMcp.Core.Knowledge.Canonical.FactId.New(),
            source.Id.Value, "mentions", "Hello",
            OccamMcp.Core.Knowledge.Canonical.ClaimKind.NormalizedFact,
            OccamMcp.Core.Knowledge.Canonical.ValidationState.Supported,
            [prov.Id],
            confidence: OccamMcp.Core.Knowledge.Canonical.ConfidenceLevel.Medium);
        assert("canonical: Supported Fact accepts explicit provenance+confidence",
            supported.ProvenanceRefs.Count == 1 && supported.Confidence == OccamMcp.Core.Knowledge.Canonical.ConfidenceLevel.Medium);

        var e1 = OccamMcp.Core.Knowledge.Canonical.Entity.Create(
            OccamMcp.Core.Knowledge.Canonical.EntityId.New(),
            OccamMcp.Core.Knowledge.Canonical.SemanticType.Concept,
            "Example");
        var e2 = OccamMcp.Core.Knowledge.Canonical.Entity.Create(
            OccamMcp.Core.Knowledge.Canonical.EntityId.New(),
            OccamMcp.Core.Knowledge.Canonical.SemanticType.Artifact,
            "Page");
        var relSupportedWithoutProvThrew = false;
        try
        {
            _ = OccamMcp.Core.Knowledge.Canonical.Relationship.Create(
                OccamMcp.Core.Knowledge.Canonical.RelationshipId.New(),
                e1.Id, "describes", e2.Id,
                OccamMcp.Core.Knowledge.Canonical.ValidationState.Supported,
                []);
        }
        catch (ArgumentException)
        {
            relSupportedWithoutProvThrew = true;
        }

        assert("canonical: Supported Relationship requires provenance", relSupportedWithoutProvThrew);

        var rel = OccamMcp.Core.Knowledge.Canonical.Relationship.Create(
            OccamMcp.Core.Knowledge.Canonical.RelationshipId.New(),
            e1.Id, "describes", e2.Id,
            OccamMcp.Core.Knowledge.Canonical.ValidationState.Unvalidated,
            []);
        assert("canonical: Relationship has no markdown/codec surface fields",
            rel.RelationType == "describes" && rel.Confidence is null);

        // Empty locator / empty statement rejected.
        var emptyLocatorThrew = false;
        try
        {
            _ = OccamMcp.Core.Knowledge.Canonical.EvidenceLocator.SourceSelector("  ");
        }
        catch (ArgumentException)
        {
            emptyLocatorThrew = true;
        }

        assert("canonical: empty source-selector locator rejected", emptyLocatorThrew);

        var emptyProvThrew = false;
        try
        {
            _ = OccamMcp.Core.Knowledge.Canonical.KnowledgeProvenance.Create(
                OccamMcp.Core.Knowledge.Canonical.ProvenanceId.New(),
                source.Id,
                []);
        }
        catch (ArgumentException)
        {
            emptyProvThrew = true;
        }

        assert("canonical: KnowledgeProvenance requires evidence ids", emptyProvThrew);

        // Domain types must not force a JSON serializer dependency (no System.Text.Json attrs on Canonical).
        var factType = typeof(OccamMcp.Core.Knowledge.Canonical.Fact);
        var hasJsonAttr = factType.GetProperties()
            .SelectMany(p => p.GetCustomAttributes(false))
            .Any(a => a.GetType().FullName?.StartsWith("System.Text.Json", StringComparison.Ordinal) == true);
        assert("canonical: Fact has no System.Text.Json attributes (serialize via external adapter later)", !hasJsonAttr);
    }

    // Migration PR-B: Legacy Adapter — TranscodeOutcome → Source/Evidence/ClaimCandidate (never Fact).
    private static void RunLegacyTranscodeToCanonical(Action<string, bool> assert)
    {
        var fail = new TranscodeOutcome(false, null, null, null, "thin_extract", "unknown");
        assert(
            "legacy adapter: Ok=false returns null (fail-closed)",
            OccamMcp.Core.Knowledge.Legacy.TranscodeToCanonical.TryAdapt("https://example.com/", fail) is null);

        var blockA = new OccamMcp.Core.Workers.WorkerExtractBlockInfo
        {
            Type = "paragraph",
            Text = "Hello world from example.",
            SourceSelector = "main > p:nth-child(1)",
        };
        var blockB = new OccamMcp.Core.Workers.WorkerExtractBlockInfo
        {
            Type = "heading",
            Text = "Title",
            SourceSelector = "h1",
            Level = 1,
        };
        var table = new OccamMcp.Core.Workers.WorkerExtractTableInfo
        {
            Caption = "Directives",
            Headers = ["name", "ctx"],
            Rows = [["a", "http"]],
            SourceSelector = "table.dirs",
        };
        var md = "# Title\n\nHello world from example.\n";
        var ok = new TranscodeOutcome(
            true,
            md,
            "https://example.com/page",
            "http",
            null,
            null,
            Blocks: [blockA, blockB],
            Tables: [table],
            Meta: new OccamMcp.Core.Workers.WorkerExtractMetaInfo { Lang = "en", PublishedAt = "2026-07-20T12:00:00Z" });

        var fixedAt = new DateTimeOffset(2026, 7, 20, 15, 0, 0, TimeSpan.Zero);
        var extract = OccamMcp.Core.Knowledge.Legacy.TranscodeToCanonical.TryAdapt(
            "https://example.com/",
            ok,
            contentHash: "sha256:deadbeef",
            retrievedAt: fixedAt,
            extractionVersion: "1.0.0-rc.2");

        assert("legacy adapter: success yields extract", extract is not null);
        assert("legacy adapter: one Source", extract!.Source.Kind == OccamMcp.Core.Knowledge.Canonical.SourceKind.WebPage);
        assert("legacy adapter: prefers FinalUrl as locator", extract.Source.Locator == "https://example.com/page");
        assert("legacy adapter: strips sha256: prefix on contentHash", extract.Source.ContentHash == "deadbeef");
        assert("legacy adapter: publishedAt parsed from meta", extract.Source.PublishedAt is not null);
        assert("legacy adapter: evidence = 2 blocks + 1 table", extract.Evidence.Count == 3);
        assert(
            "legacy adapter: claims = 2 block texts + 1 table caption (ExtractedClaim)",
            extract.Claims.Count == 3
            && extract.Claims.All(c => c.ClaimKind == OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim));
        assert("legacy adapter: never emits Fact", true); // structural: API has no Fact field
        assert(
            "legacy adapter: claim confidence never invented",
            extract.Claims.All(c => c.Confidence is null));

        var expectedLeaf = Convert.ToHexString(
            OccamMcp.Core.Receipts.MerkleTree.LeafHash(blockA.Text, blockA.SourceSelector)).ToLowerInvariant();
        var blockEv = extract.Evidence.First(e =>
            e.Kind == OccamMcp.Core.Knowledge.Canonical.EvidenceKind.ContentBlock
            && e.Locator.Value == "main > p:nth-child(1)");
        assert("legacy adapter: Evidence contentHash = Merkle leaf", blockEv.ContentHash == expectedLeaf);
        assert(
            "legacy adapter: provenance carries matching blockLeafHash",
            extract.Provenance.Any(p => p.BlockLeafHash == expectedLeaf && p.ReceiptContentHash == "deadbeef"));

        var tableEv = extract.Evidence.Single(e => e.Kind == OccamMcp.Core.Knowledge.Canonical.EvidenceKind.Table);
        assert("legacy adapter: table evidence has no invented leaf hash", tableEv.ContentHash is null);

        // Markdown-only: one Excerpt evidence, zero claims (do not invent a page-level assertion).
        var mdOnly = new TranscodeOutcome(true, "# Only\n\nBody.\n", "https://example.com/", "http", null, null);
        var mdExtract = OccamMcp.Core.Knowledge.Legacy.TranscodeToCanonical.TryAdapt("https://example.com/", mdOnly, retrievedAt: fixedAt);
        assert("legacy adapter: markdown-only yields one Excerpt evidence", mdExtract is { Evidence.Count: 1 });
        assert(
            "legacy adapter: markdown-only EvidenceKind.Excerpt",
            mdExtract!.Evidence[0].Kind == OccamMcp.Core.Knowledge.Canonical.EvidenceKind.Excerpt);
        assert("legacy adapter: markdown-only emits no claims", mdExtract.Claims.Count == 0);

        // Leaf order matches receipt MerkleTree.LeafHashesHex for the same blocks.
        var leafHexes = OccamMcp.Core.Receipts.MerkleTree.LeafHashesHex(
            [(blockA.Text, blockA.SourceSelector), (blockB.Text, blockB.SourceSelector)]);
        var adapterLeaves = extract.Evidence
            .Where(e => e.Kind == OccamMcp.Core.Knowledge.Canonical.EvidenceKind.ContentBlock)
            .Select(e => e.ContentHash!)
            .ToArray();
        assert(
            "legacy adapter: block leaf order matches receipt LeafHashesHex",
            adapterLeaves.SequenceEqual(leafHexes));
    }

    // Migration PR-D: Claim → Evidence → Source → receipt leaf (+ Merkle membership via occam_verify primitives).
    private static void RunMaterializedProvenanceResolver(Action<string, bool> assert)
    {
        var blockA = new OccamMcp.Core.Workers.WorkerExtractBlockInfo
        {
            Type = "paragraph",
            Text = "Hello world from example.",
            SourceSelector = "main > p:nth-child(1)",
        };
        var blockB = new OccamMcp.Core.Workers.WorkerExtractBlockInfo
        {
            Type = "heading",
            Text = "Title",
            SourceSelector = "h1",
            Level = 1,
        };
        var outcome = new TranscodeOutcome(
            true,
            "# Title\n\nHello world from example.\n",
            "https://example.com/page",
            "http",
            null,
            null,
            Blocks: [blockA, blockB]);
        var fixedAt = new DateTimeOffset(2026, 7, 21, 1, 0, 0, TimeSpan.Zero);
        var canonical = OccamMcp.Core.Knowledge.Legacy.TranscodeToCanonical.TryAdapt(
            "https://example.com/",
            outcome,
            contentHash: "abcd",
            retrievedAt: fixedAt)!;
        var planner = new OccamMcp.Core.Knowledge.MaterializationPlanner();
        var view = planner.Plan(
            outcome.Markdown!,
            OccamMcp.Core.Knowledge.WorkerKnowledgeMapper.FromExtract(outcome.Blocks, null),
            OccamMcp.Core.Knowledge.MaterializationPolicy.None,
            canonical);

        var claim = view.Claims![0];
        var structural = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.Resolve(view, claim.Id);
        assert("prov PR-D: structural status Resolved", structural.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.Resolved);
        assert("prov PR-D: ChainResolved", structural.ChainResolved);
        assert("prov PR-D: Source locator is FinalUrl", structural.Source!.Locator == "https://example.com/page");
        assert("prov PR-D: Evidence selector preserved", structural.Evidence!.Locator.Value == "main > p:nth-child(1)");
        assert("prov PR-D: leaf bridge present", !string.IsNullOrWhiteSpace(structural.BlockLeafHash));
        assert("prov PR-D: receipt contentHash bridge", structural.ReceiptContentHash == "abcd");

        var leaves = OccamMcp.Core.Receipts.MerkleTree.LeafHashesHex(
            [(blockA.Text, blockA.SourceSelector), (blockB.Text, blockB.SourceSelector)]);
        var root = OccamMcp.Core.Receipts.MerkleTree.RootFromLeafHashes(leaves)!;
        var verified = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.ResolveAndVerify(
            view, claim.Id, leaves, root);
        assert("prov PR-D: membership Status Resolved", verified.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.Resolved);
        assert("prov PR-D: MembershipVerified true", verified.MembershipVerified == true);
        assert("prov PR-D: LeafIndex is 0 for first claim", verified.LeafIndex == 0);
        assert(
            "prov PR-D: MembershipProof verifies via MerkleTree (occam_verify primitive)",
            verified.MembershipProof is not null
            && OccamMcp.Core.Receipts.MerkleTree.VerifyProof(verified.BlockLeafHash!, verified.MembershipProof, root));

        // Honesty: membership ≠ truth — we only assert structural/crypto fields, never invent support.
        assert("prov PR-D: claim remains ExtractedClaim (not Fact)",
            verified.Claim!.ClaimKind == OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim);

        var missingClaim = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.Resolve(
            view, OccamMcp.Core.Knowledge.Canonical.ClaimCandidateId.New());
        assert("prov PR-D: unknown claim → ClaimNotFound",
            missingClaim.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.ClaimNotFound
            && !missingClaim.ChainResolved);

        var orphanView = view with
        {
            EvidenceRefs = [],
        };
        var missingEvidence = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.Resolve(orphanView, claim.Id);
        assert("prov PR-D: missing evidence → EvidenceMissing",
            missingEvidence.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.EvidenceMissing);

        var noSourceView = view with { SourceRefs = [] };
        var missingSource = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.Resolve(noSourceView, claim.Id);
        assert("prov PR-D: missing source → SourceMissing",
            missingSource.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.SourceMissing);

        var wrongLeaves = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.ResolveAndVerify(
            view, claim.Id, ["00" + new string('a', 62)], root);
        assert("prov PR-D: wrong leaves → RootMismatch",
            wrongLeaves.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.RootMismatch
            && wrongLeaves.MembershipVerified == false);

        var alienLeaf = Convert.ToHexString(
            OccamMcp.Core.Receipts.MerkleTree.LeafHash("not-in-page", "div")).ToLowerInvariant();
        var alienLeaves = leaves.ToArray();
        alienLeaves[0] = alienLeaf; // keep root reconstruction possible only if we rebuild root — use matching root
        var alienRoot = OccamMcp.Core.Receipts.MerkleTree.RootFromLeafHashes(alienLeaves)!;
        var notInReceipt = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.ResolveAndVerify(
            view, claim.Id, alienLeaves, alienRoot);
        assert("prov PR-D: leaf absent from receipt → LeafNotInReceipt",
            notInReceipt.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.LeafNotInReceipt
            && notInReceipt.MembershipVerified == false);

        // Markdown-only extract: Source+Excerpt evidence, no claims → ClaimNotFound; excerpt has no leaf.
        var mdOnly = OccamMcp.Core.Knowledge.Legacy.TranscodeToCanonical.TryAdapt(
            "https://example.com/",
            new TranscodeOutcome(true, "# Only\n", "https://example.com/", "http", null, null),
            retrievedAt: fixedAt)!;
        var mdView = planner.Plan("# Only\n", null, OccamMcp.Core.Knowledge.MaterializationPolicy.None, mdOnly);
        assert("prov PR-D: markdown-only view has no claims", mdView.Claims is null or { Count: 0 });
        // Synthesize a claim pointing at the excerpt evidence to exercise NoLeafBridge.
        var excerptClaim = OccamMcp.Core.Knowledge.Canonical.ClaimCandidate.Create(
            OccamMcp.Core.Knowledge.Canonical.ClaimCandidateId.New(),
            "Only",
            OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim,
            [mdView.EvidenceRefs![0].Id],
            fixedAt);
        var excerptView = mdView with { Claims = [excerptClaim] };
        var noLeaf = OccamMcp.Core.Knowledge.MaterializedProvenanceResolver.ResolveAndVerify(
            excerptView, excerptClaim.Id, leaves, root);
        assert("prov PR-D: excerpt evidence → NoLeafBridge",
            noLeaf.Status == OccamMcp.Core.Knowledge.ProvenanceTraceStatus.NoLeafBridge
            && noLeaf.ChainResolved
            && noLeaf.MembershipVerified == false);
    }

    // ADR-0002 codec-benchmark mode: one fixed view → every codec, comparing surface efficiency in
    // isolation from any materialization policy. Planner runs once; codecs share the planned view.
    private static void RunCodecBench(Action<string, bool> assert)
    {
        var passthroughCodec = new OccamMcp.Core.Codecs.MarkdownPassthroughCodec();
        var compactCodec = new OccamMcp.Core.Codecs.CompactMarkdownCodec();
        var jsonCodec = new OccamMcp.Core.Codecs.JsonKnowledgeCodec();
        var registry = new OccamMcp.Core.Codecs.KnowledgeCodecRegistry(
            [passthroughCodec, compactCodec, jsonCodec]);

        // Verbose markdown fallback vs a compact table the block codec rebuilds leanly — so the two
        // codecs, on the SAME view, produce measurably different surfaces.
        var verboseMd = string.Concat(Enumerable.Repeat(
            "| Syntax: | directive value here; |\n| --- | --- |\n| Default: | directive off; |\n| Context: | http, server, location |\n\n", 6));
        var doc = new OccamMcp.Core.Knowledge.KnowledgeDocument(
            [new OccamMcp.Core.Knowledge.KnowledgeBlock("paragraph", "Directive context note.")],
            [new OccamMcp.Core.Knowledge.KnowledgeTable("dirs", ["name", "ctx"], [["a", "http"], ["b", "server"]])]);

        // Plan once — codecs must encode the same planned view instance.
        var planner = new OccamMcp.Core.Knowledge.MaterializationPlanner();
        var view = planner.Plan(
            verboseMd,
            doc,
            OccamMcp.Core.Knowledge.MaterializationPolicy.None);

        var rows = OccamMcp.Core.Codecs.CodecBench.RunFixedView(
            view, [passthroughCodec, compactCodec, jsonCodec]);
        assert("bench: one row per encoding codec", rows.Count == 3);
        assert("bench: all three codecs present",
            rows.Any(r => r.CodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id)
            && rows.Any(r => r.CodecId == OccamMcp.Core.Codecs.CompactMarkdownCodec.Id)
            && rows.Any(r => r.CodecId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id));
        assert("bench: all rows carry token/char/utf8 metrics",
            rows.All(r => r.Tokens > 0 && r.Chars > 0 && r.Utf8Bytes > 0));
        assert("bench: all rows report deterministic+valid",
            rows.All(r => r.DeterministicOk && r.ValidOutputOk));
        assert("bench: semantic structures listed",
            rows.All(r => r.SemanticStructures is { Count: > 0 } && r.SemanticStructures.Contains("surface")));

        var passthrough = rows.First(r => r.CodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);
        var compact = rows.First(r => r.CodecId == OccamMcp.Core.Codecs.CompactMarkdownCodec.Id);
        var json = rows.First(r => r.CodecId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id);
        assert("bench: compact surface is leaner than verbose passthrough on this view",
            compact.Tokens < passthrough.Tokens);
        assert("bench: JSON parseable + schema ok",
            json.JsonParseable == true && json.JsonSchemaOk == true);
        assert("bench: registry Run also includes knowledge-json",
            OccamMcp.Core.Codecs.CodecBench.Run(view, registry)
                .Any(r => r.CodecId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id));
        assert("bench: is deterministic",
            OccamMcp.Core.Codecs.CodecBench.RunFixedView(view, [passthroughCodec])[0].Tokens == passthrough.Tokens);

        // Emit measurements for the post-1.0 validation report (stderr / gate log).
        Console.Error.WriteLine(
            $"CODEC_BENCH markdown-passthrough chars={passthrough.Chars} utf8={passthrough.Utf8Bytes} tokens~={passthrough.Tokens} ({OccamMcp.Core.Compile.TokenEstimator.EstimatorId}) ms={passthrough.EncodingDurationMs:F3}");
        Console.Error.WriteLine(
            $"CODEC_BENCH compact-markdown chars={compact.Chars} utf8={compact.Utf8Bytes} tokens~={compact.Tokens} ({OccamMcp.Core.Compile.TokenEstimator.EstimatorId}) ms={compact.EncodingDurationMs:F3}");
        Console.Error.WriteLine(
            $"CODEC_BENCH knowledge-json chars={json.Chars} utf8={json.Utf8Bytes} tokens~={json.Tokens} ({OccamMcp.Core.Compile.TokenEstimator.EstimatorId}) ms={json.EncodingDurationMs:F3}");

        // R4: multi-fixture usefulness matrix + disposition (no MCP exposure).
        var codecs = new OccamMcp.Core.Codecs.IKnowledgeCodec[] { passthroughCodec, compactCodec, jsonCodec };
        var materializer = new OccamMcp.Core.Knowledge.MaterializationPlanner();
        var rowsByFixture = new Dictionary<string, IReadOnlyList<OccamMcp.Core.Codecs.CodecBenchRow>>(StringComparer.Ordinal);
        var compsByFixture = new Dictionary<string, IReadOnlyList<OccamMcp.Core.Codecs.CodecBenchComparison>>(StringComparer.Ordinal);

        foreach (var (id, bundle, _) in OccamMcp.Core.Knowledge.PlannerBenchFixtures.All())
        {
            var planned = materializer.Plan(OccamMcp.Core.Knowledge.PlannerBenchProfiles.Compat, bundle);
            var fixtureRows = OccamMcp.Core.Codecs.CodecBench.RunFixedView(planned.View, codecs);
            rowsByFixture[id] = fixtureRows;
            compsByFixture[id] = OccamMcp.Core.Codecs.CodecBench.CompareToPassthrough(fixtureRows);
            assert($"codec-r4: {id} all deterministic", fixtureRows.All(r => r.DeterministicOk && r.ValidOutputOk));
        }

        // Canonical fixture must exercise knowledge-json id/ref preservation.
        var canonPlanned = materializer.Plan(
            OccamMcp.Core.Knowledge.PlannerBenchProfiles.EvidencePreserving,
            OccamMcp.Core.Knowledge.PlannerBenchFixtures.CanonicalRefs());
        var canonRows = OccamMcp.Core.Codecs.CodecBench.RunFixedView(canonPlanned.View, codecs);
        rowsByFixture["canonical-refs-evidence"] = canonRows;
        compsByFixture["canonical-refs-evidence"] = OccamMcp.Core.Codecs.CodecBench.CompareToPassthrough(canonRows);
        var canonJson = canonRows.First(r => r.CodecId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id);
        assert("codec-r4: knowledge-json preserves Canonical ids on evidence-preserving view",
            canonJson.CarriesCanonicalIds && canonJson.IdsPreserved == true && canonJson.RefsPreserved == true);

        var dispositions = OccamMcp.Core.Codecs.CodecBench.EvaluateDispositions(compsByFixture);
        assert("codec-r4: disposition covers passthrough + compact + knowledge-json",
            dispositions.Any(d => d.CodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id)
            && dispositions.Any(d => d.CodecId == OccamMcp.Core.Codecs.CompactMarkdownCodec.Id)
            && dispositions.Any(d => d.CodecId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id));
        assert("codec-r4: passthrough remains KeepAsDefault",
            dispositions.First(d => d.CodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id).Verdict
            == OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepAsDefault);
        assert("codec-r4: compact stays experimental (not default)",
            dispositions.First(d => d.CodecId == OccamMcp.Core.Codecs.CompactMarkdownCodec.Id).Verdict
            is OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepExperimental
                or OccamMcp.Core.Codecs.CodecUsefulnessVerdict.DoNotPromote);
        assert("codec-r4: knowledge-json not promoted as agent default",
            dispositions.First(d => d.CodecId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id).Verdict
            is OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepExperimentalNotForAgents
                or OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepExperimental
                or OccamMcp.Core.Codecs.CodecUsefulnessVerdict.DoNotPromote);
        assert("codec-r4: no disposition makes experimental codec KeepAsDefault",
            dispositions.Where(d => d.CodecId != OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id)
                .All(d => d.Verdict != OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepAsDefault));

        // Public MCP still has no codec param.
        var transcode = typeof(OccamTranscodeTool).GetMethod(nameof(OccamTranscodeTool.Transcode));
        assert("codec-r4 regression: no public codec MCP param",
            !transcode!.GetParameters().Any(p =>
                string.Equals(p.Name, "codec", StringComparison.OrdinalIgnoreCase)
                || string.Equals(p.Name, "codec_id", StringComparison.OrdinalIgnoreCase)));

        var report = OccamMcp.Core.Codecs.CodecBench.FormatEvaluationReport(rowsByFixture, dispositions);
        Console.Error.WriteLine(report);
        assert("codec-r4: evaluation report non-empty", report.Contains("Disposition", StringComparison.Ordinal));

        // Persist measured disposition for maintainers (gitignored docs-internal may be written in CI/local).
        try
        {
            var root = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
            var dest = Path.Combine(root, "docs-internal", "CODEC-USEFULNESS-DISPOSITION.md");
            var dir = Path.GetDirectoryName(dest);
            if (!string.IsNullOrEmpty(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var body = new StringBuilder();
            body.AppendLine("# Codec usefulness disposition (Occam 1.1 R4)");
            body.AppendLine();
            body.AppendLine($"- **Generated:** {DateTimeOffset.UtcNow:yyyy-MM-dd} (from `RunCodecBench`)");
            body.AppendLine("- **Authority:** offline CodecBench + PlannerBench fixtures — heuristic tokens only");
            body.AppendLine("- **Not claimed:** downstream task accuracy / model-class wins");
            body.AppendLine();
            body.AppendLine("## Verdicts");
            body.AppendLine();
            body.AppendLine("| Codec | Verdict | Action |");
            body.AppendLine("|-------|---------|--------|");
            foreach (var d in dispositions)
            {
                var action = d.Verdict switch
                {
                    OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepAsDefault => "Live default; do not replace",
                    OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepExperimental => "Registered experimental; no public MCP `codec` param",
                    OccamMcp.Core.Codecs.CodecUsefulnessVerdict.KeepExperimentalNotForAgents => "Tooling/tests only; not agent default",
                    OccamMcp.Core.Codecs.CodecUsefulnessVerdict.DoNotPromote => "Do not expand product surface",
                    _ => d.Verdict.ToString(),
                };
                body.AppendLine($"| `{d.CodecId}` | `{d.Verdict}` | {action} |");
            }

            body.AppendLine();
            body.AppendLine("## Rationales");
            body.AppendLine();
            foreach (var d in dispositions)
            {
                body.AppendLine($"### `{d.CodecId}`");
                body.AppendLine();
                body.AppendLine(d.Rationale);
                body.AppendLine();
                if (d.MedianTokenReductionVsPassthrough is double med)
                {
                    body.AppendLine($"- median Δ vs passthrough: **{med:0.00}** (positive = fewer tokens)");
                    body.AppendLine($"- range: {d.MinTokenReductionVsPassthrough:0.00} … {d.MaxTokenReductionVsPassthrough:0.00}");
                    body.AppendLine($"- fixtures ≥5% cheaper: {d.FixturesCheaperThanPassthrough}/{d.FixturesMeasured}");
                    body.AppendLine($"- fixtures carrying Canonical ids: {d.FixturesCarryingCanonical}/{d.FixturesMeasured}");
                    body.AppendLine();
                }
            }

            body.AppendLine("## Policy");
            body.AppendLine();
            body.AppendLine("1. **Do not** add a public MCP `codec` parameter without a measured agent-task win.");
            body.AppendLine("2. Receipt `contentHash` continues to hash the live markdown-passthrough surface.");
            body.AppendLine("3. Model-class accuracy arms (if ever) are external eval — not this harness.");
            body.AppendLine();
            body.AppendLine("## Raw evaluation report");
            body.AppendLine();
            body.AppendLine("```");
            body.AppendLine(report.TrimEnd());
            body.AppendLine("```");
            body.AppendLine();

            File.WriteAllText(dest, body.ToString());
            Console.Error.WriteLine($"CODEC_DISPOSITION_WRITTEN {dest}");
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"CODEC_DISPOSITION_WRITE_SKIPPED {ex.GetType().Name}: {ex.Message}");
        }

        Console.WriteLine("L_CODEC_BENCH_OK");
    }

    /// <summary>
    /// Post-1.0 validation: experimental JsonKnowledgeCodec is representation-only, deterministic,
    /// AOT source-gen path, selectable by id, unreachable via public MCP codec param.
    /// </summary>
    private static void RunJsonKnowledgeCodec(Action<string, bool> assert)
    {
        var codec = new OccamMcp.Core.Codecs.JsonKnowledgeCodec();
        assert("json-codec: id is knowledge-json", codec.Descriptor.CodecId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id);
        assert("json-codec: BuiltinExperimental + encode-only + deterministic + lossless",
            codec.Descriptor.Trust == OccamMcp.Core.Codecs.KnowledgeCodecTrust.BuiltinExperimental
            && codec.Descriptor.CanEncode
            && !codec.Descriptor.CanDecode
            && codec.Descriptor.Deterministic
            && codec.Descriptor.Mode == OccamMcp.Core.Codecs.KnowledgeCodecMode.Lossless);

        // 1. Empty view
        var empty = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("");
        var emptyJson = codec.Encode(empty, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        using (var emptyDoc = JsonDocument.Parse(emptyJson))
        {
            assert("json-codec: empty view is valid JSON", emptyDoc.RootElement.ValueKind == JsonValueKind.Object);
            assert("json-codec: empty surface text is empty string",
                emptyDoc.RootElement.GetProperty("surface").GetProperty("text").GetString() == "");
            assert("json-codec: absent knowledge omitted (WhenWritingNull)",
                !emptyDoc.RootElement.TryGetProperty("knowledge", out _));
            assert("json-codec: absent canonical sidecars omitted",
                !emptyDoc.RootElement.TryGetProperty("sources", out _)
                && !emptyDoc.RootElement.TryGetProperty("claims", out _)
                && !emptyDoc.RootElement.TryGetProperty("provenance", out _));
        }

        // 2. Surface-only
        var surfaceOnly = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("# Hello\n");
        var surfaceJson = codec.Encode(surfaceOnly, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        using (var surfaceDoc = JsonDocument.Parse(surfaceJson))
        {
            assert("json-codec: surface-only preserves markdown text",
                surfaceDoc.RootElement.GetProperty("surface").GetProperty("text").GetString() == "# Hello\n");
            assert("json-codec: surface mediaType is markdown",
                surfaceDoc.RootElement.GetProperty("surface").GetProperty("mediaType").GetString()
                == OccamMcp.Core.Knowledge.SourceSurface.MarkdownMediaType);
        }

        // 3. KnowledgeDocument with text blocks
        var blockDoc = new OccamMcp.Core.Knowledge.KnowledgeDocument(
            [
                new OccamMcp.Core.Knowledge.KnowledgeBlock("heading", "Title", Level: 1),
                new OccamMcp.Core.Knowledge.KnowledgeBlock(
                    "paragraph",
                    "Body with \"quotes\" and /slashes/\tand\ncontrols.",
                    Links: [new OccamMcp.Core.Knowledge.KnowledgeLink("ref", "https://example.com/a")]),
            ],
            []);
        var blockView = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("fallback", blockDoc);
        var blockJson = codec.Encode(blockView, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        using (var blockParsed = JsonDocument.Parse(blockJson))
        {
            var blocks = blockParsed.RootElement.GetProperty("knowledge").GetProperty("blocks");
            assert("json-codec: blocks count", blocks.GetArrayLength() == 2);
            assert("json-codec: heading level preserved",
                blocks[0].GetProperty("type").GetString() == "heading"
                && blocks[0].GetProperty("level").GetInt32() == 1);
            assert("json-codec: escaping round-trips quotes/slashes/controls",
                blocks[1].GetProperty("text").GetString() == "Body with \"quotes\" and /slashes/\tand\ncontrols.");
            assert("json-codec: empty tables array present when doc non-null",
                blockParsed.RootElement.GetProperty("knowledge").GetProperty("tables").GetArrayLength() == 0);
        }

        // 4. Table data
        var tableDoc = new OccamMcp.Core.Knowledge.KnowledgeDocument(
            [],
            [new OccamMcp.Core.Knowledge.KnowledgeTable("cap", ["a", "b"], [["1", "2"], ["3", "4"]])]);
        var tableJson = codec.Encode(
            OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("t", tableDoc),
            OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        using (var tableParsed = JsonDocument.Parse(tableJson))
        {
            var table = tableParsed.RootElement.GetProperty("knowledge").GetProperty("tables")[0];
            assert("json-codec: table caption/headers/rows",
                table.GetProperty("caption").GetString() == "cap"
                && table.GetProperty("headers").GetArrayLength() == 2
                && table.GetProperty("rows").GetArrayLength() == 2);
        }

        // 5–6. Claims + evidence + sources + provenance
        var fixedAt = new DateTimeOffset(2026, 7, 21, 0, 0, 0, TimeSpan.Zero);
        var sourceId = OccamMcp.Core.Knowledge.Canonical.SourceId.From("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa");
        var evidenceId = OccamMcp.Core.Knowledge.Canonical.EvidenceId.From("bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb");
        var claimId = OccamMcp.Core.Knowledge.Canonical.ClaimCandidateId.From("cccccccccccccccccccccccccccccccc");
        var provId = OccamMcp.Core.Knowledge.Canonical.ProvenanceId.From("dddddddddddddddddddddddddddddddd");
        var source = OccamMcp.Core.Knowledge.Canonical.Source.Create(
            sourceId,
            OccamMcp.Core.Knowledge.Canonical.SourceKind.WebPage,
            "https://example.com/",
            fixedAt,
            contentHash: "sha256:deadbeef",
            title: "Example",
            metadata: new Dictionary<string, string> { ["z"] = "last", ["a"] = "first" });
        var evidence = OccamMcp.Core.Knowledge.Canonical.Evidence.Create(
            evidenceId,
            sourceId,
            OccamMcp.Core.Knowledge.Canonical.EvidenceLocator.SourceSelector("#main > p"),
            OccamMcp.Core.Knowledge.Canonical.EvidenceKind.ContentBlock,
            fixedAt,
            excerpt: "Hello");
        var claim = OccamMcp.Core.Knowledge.Canonical.ClaimCandidate.Create(
            claimId,
            "Hello",
            OccamMcp.Core.Knowledge.Canonical.ClaimKind.ExtractedClaim,
            [evidenceId],
            fixedAt);
        var provenance = OccamMcp.Core.Knowledge.Canonical.KnowledgeProvenance.Create(
            provId,
            sourceId,
            [evidenceId],
            observedAt: fixedAt,
            extractionMethod: "legacy-adapter",
            receiptContentHash: "sha256:deadbeef",
            blockLeafHash: "abcd");
        // Unstable input order — codec must sort by id for determinism.
        var canonView = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown(
            "# Hello\n",
            null,
            sourceRefs: [source],
            evidenceRefs: [evidence],
            claims: [claim],
            provenance: [provenance]);
        var canonJson = codec.Encode(canonView, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        using (var canonParsed = JsonDocument.Parse(canonJson))
        {
            var root = canonParsed.RootElement;
            assert("json-codec: sources id preserved",
                root.GetProperty("sources")[0].GetProperty("id").GetString() == sourceId.Value);
            assert("json-codec: metadata keys sorted stably",
                root.GetProperty("sources")[0].GetProperty("metadata")[0].GetProperty("key").GetString() == "a");
            assert("json-codec: evidence + claim refs preserved",
                root.GetProperty("evidence")[0].GetProperty("id").GetString() == evidenceId.Value
                && root.GetProperty("claims")[0].GetProperty("id").GetString() == claimId.Value
                && root.GetProperty("claims")[0].GetProperty("evidenceRefs")[0].GetString() == evidenceId.Value);
            assert("json-codec: provenance present",
                root.GetProperty("provenance")[0].GetProperty("id").GetString() == provId.Value
                && root.GetProperty("provenance")[0].GetProperty("blockLeafHash").GetString() == "abcd");
        }

        // 7. Unicode
        var unicode = OccamMcp.Core.Knowledge.MaterializedKnowledgeView.FromMarkdown("Привет, 世界 — café 😀\n");
        var unicodeJson = codec.Encode(unicode, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        using (var unicodeDoc = JsonDocument.Parse(unicodeJson))
        {
            assert("json-codec: unicode preserved",
                unicodeDoc.RootElement.GetProperty("surface").GetProperty("text").GetString()
                == "Привет, 世界 — café 😀\n");
        }

        // 8–10. Deterministic repeated encoding + valid parse (escaping covered above)
        var a = codec.Encode(canonView, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        var b = codec.Encode(canonView, OccamMcp.Core.Codecs.KnowledgeCodecEncodeOptions.None).Surface;
        assert("json-codec: deterministic repeated encoding", a == b);
        assert("json-codec: valid JSON parsing of full fixture", JsonDocument.Parse(a).RootElement.ValueKind == JsonValueKind.Object);

        // 11. Source-generated serialization path (no reflection JsonSerializerOptions overload)
        var envelope = OccamMcp.Core.Codecs.KnowledgeJsonProjection.FromView(canonView);
        var viaContext = JsonSerializer.Serialize(envelope, OccamMcp.Core.Codecs.KnowledgeJsonContext.Default.KnowledgeJsonEnvelope);
        assert("json-codec: source-gen context matches Encode output", viaContext == a);

        // Registration without planner edits: construct registry with JSON codec.
        var registry = new OccamMcp.Core.Codecs.KnowledgeCodecRegistry(
            [
                new OccamMcp.Core.Codecs.MarkdownPassthroughCodec(),
                codec,
            ]);
        assert("json-codec: registered without planner changes",
            registry.TryGet(OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id, out var got) && got is OccamMcp.Core.Codecs.JsonKnowledgeCodec);
        var sel = OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(registry, OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id);
        assert("json-codec: selectable by id when registered",
            sel.Ok && sel.SelectedId == OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id);

        // Regression: public MCP has no codec parameter; default remains markdown.
        var transcode = typeof(OccamTranscodeTool).GetMethod(nameof(OccamTranscodeTool.Transcode));
        assert("json-codec regression: Transcode method found", transcode is not null);
        var paramNames = transcode!.GetParameters().Select(p => p.Name).ToArray();
        assert("json-codec regression: no public codec MCP param",
            !paramNames.Contains("codec", StringComparer.OrdinalIgnoreCase)
            && !paramNames.Contains("codec_id", StringComparer.OrdinalIgnoreCase)
            && !paramNames.Contains("knowledge_codec", StringComparer.OrdinalIgnoreCase));

        var services = new ServiceCollection();
        services.AddOccamCore();
        using var sp = services.BuildServiceProvider();
        var diRegistry = sp.GetRequiredService<OccamMcp.Core.Codecs.KnowledgeCodecRegistry>();
        assert("json-codec regression: default codec remains markdown-passthrough",
            diRegistry.DefaultCodecId == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id
            && OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(diRegistry, null).SelectedId
                == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);
        assert("json-codec regression: knowledge-json reachable only by explicit id",
            OccamMcp.Core.Codecs.KnowledgeCodecSelector.Select(diRegistry, OccamMcp.Core.Codecs.JsonKnowledgeCodec.Id).Ok);
    }

    /// <summary>
    /// 1.1 Planner Benchmark Foundation: same ExtractedKnowledgeBundle → different MaterializationRequest
    /// policies; optionally matrix with CodecBench. Evaluation harness only — not live runtime.
    /// </summary>
    private static void RunPlannerBench(Action<string, bool> assert)
    {
        var bench = new OccamMcp.Core.Knowledge.PlannerBench();
        var codecs = new OccamMcp.Core.Codecs.IKnowledgeCodec[]
        {
            new OccamMcp.Core.Codecs.MarkdownPassthroughCodec(),
            new OccamMcp.Core.Codecs.CompactMarkdownCodec(),
            new OccamMcp.Core.Codecs.JsonKnowledgeCodec(),
        };

        // Architecture source guards
        var coreRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "src", "FFOccamMcp.Core"));
        if (!Directory.Exists(coreRoot))
        {
            coreRoot = Path.GetFullPath(Path.Combine(Environment.CurrentDirectory, "src", "FFOccamMcp.Core"));
        }

        assert("planner-bench arch: Core source tree located", Directory.Exists(coreRoot));
        if (Directory.Exists(coreRoot))
        {
            var plannerBenchFiles = Directory.GetFiles(Path.Combine(coreRoot, "Knowledge"), "PlannerBench*.cs");
            assert("planner-bench arch: PlannerBench sources present", plannerBenchFiles.Length >= 1);
            foreach (var f in plannerBenchFiles)
            {
                var text = File.ReadAllText(f);
                assert($"planner-bench arch: {Path.GetFileName(f)} does not import Routing",
                    !text.Contains("using OccamMcp.Core.Routing", StringComparison.Ordinal));
                assert($"planner-bench arch: {Path.GetFileName(f)} does not import Workers",
                    !text.Contains("using OccamMcp.Core.Workers", StringComparison.Ordinal));
                assert($"planner-bench arch: {Path.GetFileName(f)} does not import Tools/MCP",
                    !text.Contains("using OccamMcp.Core.Tools", StringComparison.Ordinal)
                    && !text.Contains("using ModelContextProtocol", StringComparison.Ordinal));
                assert($"planner-bench arch: {Path.GetFileName(f)} does not call TranscodeCompiler/FitMarkdown directly",
                    !text.Contains("TranscodeCompiler.", StringComparison.Ordinal)
                    && !text.Contains("FitMarkdown.", StringComparison.Ordinal)
                    && !text.Contains("RetainWithinBudget(", StringComparison.Ordinal));
            }

            var codecBenchPath = Path.Combine(coreRoot, "Codecs", "CodecBench.cs");
            if (File.Exists(codecBenchPath))
            {
                var cb = File.ReadAllText(codecBenchPath);
                assert("planner-bench arch: CodecBench does not reference MaterializationPlanner",
                    !cb.Contains("MaterializationPlanner", StringComparison.Ordinal));
                assert("planner-bench arch: CodecBench does not reference PlannerBench",
                    !cb.Contains("PlannerBench", StringComparison.Ordinal));
            }

            var pipelinePath = Path.Combine(coreRoot, "Routing", "TranscodePipeline.cs");
            if (File.Exists(pipelinePath))
            {
                var pipe = File.ReadAllText(pipelinePath);
                assert("planner-bench arch: live TranscodePipeline does not reference PlannerBench",
                    !pipe.Contains("PlannerBench", StringComparison.Ordinal));
            }

            foreach (var codecFile in Directory.GetFiles(Path.Combine(coreRoot, "Codecs"), "*Codec.cs"))
            {
                var text = File.ReadAllText(codecFile);
                assert($"planner-bench arch: {Path.GetFileName(codecFile)} does not reference PlannerBench",
                    !text.Contains("PlannerBench", StringComparison.Ordinal));
            }
        }

        // Empty / minimal bundle
        var emptyResults = bench.Run(
            OccamMcp.Core.Knowledge.PlannerBenchFixtures.Empty(),
            [new OccamMcp.Core.Knowledge.PlannerBenchCase("compat", OccamMcp.Core.Knowledge.PlannerBenchProfiles.Compat)]);
        assert("planner-bench: empty bundle handled",
            emptyResults.Count == 1
            && emptyResults[0].Metrics.SurfaceEmpty
            && emptyResults[0].Metrics.DeterministicOk
            && emptyResults[0].Metrics.IntegrityOk);

        // Same bundle reused across all cases (reference identity + no mutation of surface)
        var longArticle = OccamMcp.Core.Knowledge.PlannerBenchFixtures.LongArticle();
        var surfaceBefore = longArticle.SourceSurface.Text;
        var blockCountBefore = longArticle.Document.Blocks.Count;
        var cases = OccamMcp.Core.Knowledge.PlannerBenchProfiles.StandardCases("materialization");
        var longResults = bench.Run(longArticle, cases);
        assert("planner-bench: all standard cases run", longResults.Count == 4);
        assert("planner-bench: bundle surface unchanged after multi-case run",
            ReferenceEquals(longArticle.SourceSurface.Text, surfaceBefore)
            || longArticle.SourceSurface.Text == surfaceBefore);
        assert("planner-bench: bundle document block count unchanged",
            longArticle.Document.Blocks.Count == blockCountBefore);
        assert("planner-bench: all cases deterministic", longResults.All(r => r.Metrics.DeterministicOk));
        assert("planner-bench: all cases stable across N Plan invocations", longResults.All(r => r.Metrics.StabilityOk));
        assert("planner-bench: planner invocations ≥ 3 per case (determinism + stability)",
            longResults.All(r => r.Metrics.PlannerInvocations >= 3));

        // Budget differences visible on long article
        var compat = longResults.First(r => r.CaseId == "compat");
        var compact = longResults.First(r => r.CaseId == "compact");
        assert("planner-bench: compact retains fewer or equal IR blocks than compat",
            compact.Metrics.RetainedBlocks <= compat.Metrics.RetainedBlocks);
        assert("planner-bench: compact surface tokens ≤ compat (or truncated)",
            compact.Metrics.SurfaceTokensEstimated <= compat.Metrics.SurfaceTokensEstimated
            || compact.PlanningResult.Truncated);
        assert("planner-bench: compact reports token reduction vs input",
            compact.Metrics.TokenReductionRatio is > 0);
        assert("planner-bench: compact TokenReductionRatio ≥ compat",
            (compact.Metrics.TokenReductionRatio ?? 0) >= (compat.Metrics.TokenReductionRatio ?? 0));

        // Focus metrics deterministic on technical docs
        var tech = OccamMcp.Core.Knowledge.PlannerBenchFixtures.TechnicalDocs();
        var focusCases = OccamMcp.Core.Knowledge.PlannerBenchProfiles.StandardCases(
            OccamMcp.Core.Knowledge.PlannerBenchFixtures.TechnicalDocsFocusQuery);
        var techA = bench.Run(tech, focusCases);
        var techB = bench.Run(tech, focusCases);
        assert("planner-bench: focus metrics deterministic across harness runs",
            techA.First(r => r.CaseId == "focus").Metrics.FocusTermCoverage
            == techB.First(r => r.CaseId == "focus").Metrics.FocusTermCoverage
            && techA.First(r => r.CaseId == "focus").Metrics.FocusMatchingBlocks
            == techB.First(r => r.CaseId == "focus").Metrics.FocusMatchingBlocks);
        var focusRow = techA.First(r => r.CaseId == "focus");
        assert("planner-bench: focus case reports coverage",
            focusRow.Metrics.FocusTermCoverage is >= 0 and <= 1);
        assert("planner-bench: focus case StabilityOk", focusRow.Metrics.StabilityOk);

        // Tables fixture
        var tableResults = bench.Run(
            OccamMcp.Core.Knowledge.PlannerBenchFixtures.Tables(),
            [new OccamMcp.Core.Knowledge.PlannerBenchCase("compat", OccamMcp.Core.Knowledge.PlannerBenchProfiles.Compat)]);
        assert("planner-bench: tables retained under compat",
            tableResults[0].Metrics.RetainedTables >= 1);

        // Canonical-driven retention (R2) + retention ratios (R3)
        var canonBundle = OccamMcp.Core.Knowledge.PlannerBenchFixtures.CanonicalRefs();
        var inputClaims = canonBundle.Canonical!.Claims.Count;
        var inputEvidence = canonBundle.Canonical.Evidence.Count;
        assert("planner-bench: canonical fixture has multiple claims for budget pressure", inputClaims >= 6);

        var canonResults = bench.Run(
            canonBundle,
            OccamMcp.Core.Knowledge.PlannerBenchProfiles.StandardCases("MaterializationPlanner"));
        var canonCompat = canonResults.First(r => r.CaseId == "compat");
        var canonCompact = canonResults.First(r => r.CaseId == "compact");
        var canonEvidence = canonResults.First(r => r.CaseId == "evidence-preserving");
        var canonFocus = canonResults.First(r => r.CaseId == "focus");

        assert("planner-bench: compat retains full Canonical under generous budget",
            canonCompat.Metrics.Claims == inputClaims
            && canonCompat.Metrics.EvidenceRefs == inputEvidence
            && canonCompat.Metrics.SourceRefs == 1
            && canonCompat.Metrics.IntegrityOk
            && canonCompat.Metrics.ClaimRetentionRatio == 1.0);
        assert("planner-bench: evidence-preserving retains full Canonical under tight surface budget",
            canonEvidence.Metrics.Claims == inputClaims
            && canonEvidence.Metrics.EvidenceRefs == inputEvidence
            && canonEvidence.Metrics.ProvenanceItems >= 1
            && canonEvidence.Metrics.IntegrityOk
            && canonEvidence.Metrics.ClaimRetentionRatio == 1.0);
        assert("planner-bench: compact (default policy) prunes Canonical claims vs evidence-preserving",
            canonCompact.Metrics.Claims < canonEvidence.Metrics.Claims
            && canonCompact.Metrics.ClaimRetentionRatio is < 1.0);
        assert("planner-bench: evidence-preserving notes policy honesty",
            canonEvidence.Metrics.Notes.Any(n =>
                n.Contains("evidence-preserving", StringComparison.OrdinalIgnoreCase)));
        assert("planner-bench: focus Canonical still IntegrityOk",
            canonFocus.Metrics.IntegrityOk
            && canonFocus.Metrics.SourceRefs == 1);
        assert("planner-bench: focus retains at least one claim under default budget",
            canonFocus.Metrics.Claims >= 1);
        var focusView = canonFocus.PlanningResult.View;
        assert("planner-bench: focus-ranked claim mentions MaterializationPlanner when present",
            focusView.Claims is null
            || focusView.Claims.Count == 0
            || focusView.Claims.Any(c =>
                c.Statement.Contains("MaterializationPlanner", StringComparison.Ordinal)));
        assert("planner-bench: focus claim hit ratio reported when claims retained",
            canonFocus.Metrics.FocusClaimHitRatio is null or >= 0);

        // R3: CompareToBaseline deltas
        var deltas = OccamMcp.Core.Knowledge.PlannerBench.CompareToBaseline(canonResults);
        assert("planner-bench: baseline comparison yields non-compat rows", deltas.Count >= 3);
        assert("planner-bench: compact reduces tokens vs compat baseline (or equal under tiny surfaces)",
            deltas.First(d => d.CaseId == "compact").RelativeTokenReduction is null or >= 0);
        assert("planner-bench: evidence-preserving claim retention vs compat is 1.0",
            deltas.First(d => d.CaseId == "evidence-preserving").RelativeClaimRetention == 1.0);

        // Planner × codec matrix: plan once per case (N for stability), codecs on planned view
        var matrix = bench.RunWithCodecs(longArticle, cases, codecs);
        assert("planner-bench matrix: one row per planner case", matrix.Count == 4);
        assert("planner-bench matrix: three codecs per planned view",
            matrix.All(r => r.CodecResults.Count == 3));
        assert("planner-bench matrix: planner not re-run per codec (invocations stay ≥ 3)",
            matrix.All(r => r.PlannerMetrics.PlannerInvocations >= 3));
        assert("planner-bench matrix: codec rows deterministic",
            matrix.All(r => r.CodecResults.All(c => c.DeterministicOk)));
        assert("planner-bench matrix: every policy StabilityOk",
            matrix.All(r => r.PlannerMetrics.StabilityOk));

        // content_selectors miss represented honestly
        var missReq = OccamMcp.Core.Knowledge.PlannerBenchProfiles.Compat with
        {
            ContentSelectors = ["# Does Not Exist Anywhere"],
        };
        var miss = bench.Run(
            tech,
            [new OccamMcp.Core.Knowledge.PlannerBenchCase("selectors-miss", missReq)]);
        assert("planner-bench: selectors miss reported",
            !miss[0].PlanningResult.SelectorsMatched
            && miss[0].Metrics.Notes.Any(n => n.Contains("content_selectors_miss", StringComparison.Ordinal)));

        // Public MCP unchanged + default codec
        var transcode = typeof(OccamTranscodeTool).GetMethod(nameof(OccamTranscodeTool.Transcode));
        var names = transcode!.GetParameters().Select(p => p.Name!).ToArray();
        assert("planner-bench regression: no public planner_policy MCP param",
            !names.Any(n => n.Contains("planner", StringComparison.OrdinalIgnoreCase)
                && n.Contains("policy", StringComparison.OrdinalIgnoreCase)));
        assert("planner-bench regression: no public codec MCP param",
            !names.Contains("codec", StringComparer.OrdinalIgnoreCase));

        var services = new ServiceCollection();
        services.AddOccamCore();
        using var sp = services.BuildServiceProvider();
        assert("planner-bench regression: default codec remains markdown-passthrough",
            sp.GetRequiredService<OccamMcp.Core.Codecs.KnowledgeCodecRegistry>().DefaultCodecId
            == OccamMcp.Core.Codecs.MarkdownPassthroughCodec.Id);
        assert("planner-bench regression: MaterializationPlanner still resolves via DI",
            sp.GetService<OccamMcp.Core.Knowledge.MaterializationPlanner>() is not null);

        // Emit reports for all fixtures (stderr) — fixture-based, not universal ranking claims.
        foreach (var (id, bundle, focus) in OccamMcp.Core.Knowledge.PlannerBenchFixtures.All())
        {
            var reportRows = bench.RunWithCodecs(
                bundle,
                OccamMcp.Core.Knowledge.PlannerBenchProfiles.StandardCases(focus),
                codecs);
            var report = OccamMcp.Core.Knowledge.PlannerBench.FormatReport(id, reportRows);
            Console.Error.WriteLine(report);
            assert($"planner-bench report: {id} non-empty", report.Contains("Planner policy", StringComparison.Ordinal));
            assert($"planner-bench report: {id} includes vs baseline section or codec matrix",
                report.Contains("Codec surfaces", StringComparison.Ordinal));
            assert($"planner-bench report: {id} all StabilityOk",
                reportRows.All(r => r.PlannerMetrics.StabilityOk && r.PlannerMetrics.DeterministicOk));
        }

        Console.WriteLine("L_PLANNER_BENCH_OK");
    }

    private static void RunJsonBlocksContract(Action<string, bool> assert)
    {
        // R3 fork B: json_blocks is an output-affecting option (must split the cache key) and the
        // worker block payload must flow through to the success envelope as camelCase "blocks".
        var baseOptions = new OccamTranscodeOptions { PlaybookPolicy = "off" };
        var keyPlain = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions);
        var keyBlocks = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions with { JsonBlocks = true });
        assert("json_blocks splits cache key", keyPlain != keyBlocks);

        assert(
            "json_blocks option parses",
            OccamTranscodeOptionsParser.TryBuild(
                null, false, null, null, null, "off", null,
                semantic_chunking: false,
                capture_screenshot: false,
                json_blocks: true,
                json_tables: false,
                json_feed: false,
                translate_to: null,
                out var parsed,
                out _)
            && parsed.JsonBlocks);

        var response = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com/", "https://example.com/"),
            "# Doc",
            "node_readability_turndown",
            [],
            Blocks:
            [
                new WorkerExtractBlockInfo
                {
                    Type = "paragraph",
                    Text = "Hello world.",
                    Links = [new WorkerExtractBlockLink { Text = "ref", Href = "https://example.com/ref" }],
                    SourceSelector = "#main > p:nth-of-type(1)",
                },
            ]);
        var json = JsonSerializer.Serialize(response, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("json_blocks serializes blocks array", json.Contains("\"blocks\""));
        // Round-trip rather than substring-match: the default encoder escapes '>' in selectors.
        var roundTrip = JsonSerializer.Deserialize(json, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        var block = roundTrip?.Blocks is { Length: > 0 } b ? b[0] : null;
        assert("json_blocks round-trips type", block?.Type == "paragraph");
        assert("json_blocks round-trips source_selector", block?.SourceSelector == "#main > p:nth-of-type(1)");
        assert("json_blocks round-trips link href", block?.Links is { Length: 1 } && block.Links[0].Href == "https://example.com/ref");

        // Absent by default (JsonIgnore when null) — markdown-only responses stay unchanged.
        var plain = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com/", "https://example.com/"),
            "# Doc",
            "node_readability_turndown",
            []);
        var plainJson = JsonSerializer.Serialize(plain, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("json_blocks omitted when null", !plainJson.Contains("\"blocks\""));

        RunTranslationContract(assert);
        RunManagedAdapterContract(assert);
        RunMetadataContract(assert);
        RunSearchContract(assert);
        RunPoliteFetchContract(assert);
        RunBatchMcpContract(assert);
        RunSearchRerankContract(assert);
        RunWatchContract(assert);
        RunOutboundGuardContract(assert);
    }

    private static void RunOutboundGuardContract(Action<string, bool> assert)
    {
        var prior = Environment.GetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS");
        Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", null);
        try
        {
            assert("outbound guard blocks 127.0.0.1", OutboundGuardBlocks("127.0.0.1"));
            assert("outbound guard blocks 10.0.0.1", OutboundGuardBlocks("10.0.0.1"));
            assert("outbound guard blocks 169.254.1.1", OutboundGuardBlocks("169.254.1.1"));
            assert("outbound guard blocks IPv6 ::1", OutboundGuardBlocks("::1"));
            assert("outbound guard blocks IPv6 ULA fc00::1", OutboundGuardBlocks("fc00::1"));
            assert("outbound guard blocks 0.0.0.0 (0.0.0.0/8)", OutboundGuardBlocks("0.0.0.0"));
            assert("outbound guard blocks IPv4-mapped ::ffff:127.0.0.1", OutboundGuardBlocks("::ffff:127.0.0.1"));
            assert("outbound guard blocks IPv4-mapped ::ffff:169.254.169.254", OutboundGuardBlocks("::ffff:169.254.169.254"));
            assert("outbound guard blocks localhost (DNS→loopback)", OutboundGuardBlocks("localhost"));

            // Public literal resolves without DNS or network and is allowed (returns the pinned address).
            var pub = OutboundHttpGuard.ResolveAndValidateAsync("8.8.8.8", CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();
            assert("outbound guard allows public 8.8.8.8", pub.Length == 1 && pub[0].ToString() == "8.8.8.8");

            // Opt-out env disables the block (maintainer-only).
            Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", "1");
            var allowed = OutboundHttpGuard.ResolveAndValidateAsync("127.0.0.1", CancellationToken.None)
                .AsTask().GetAwaiter().GetResult();
            assert("outbound guard honors OCCAM_ALLOW_PRIVATE_URLS=1", allowed.Length == 1);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", prior);
        }
    }

    private static bool OutboundGuardBlocks(string host)
    {
        try
        {
            OutboundHttpGuard.ResolveAndValidateAsync(host, CancellationToken.None).AsTask().GetAwaiter().GetResult();
            return false;
        }
        catch (OutboundUrlBlockedException ex)
        {
            return ex.FailureCode == "private_url_blocked";
        }
    }

    private static void RunWatchContract(Action<string, bool> assert)
    {
        static OccamMcp.Core.Workers.WorkerExtractBlockInfo Block(string text) =>
            new() { Type = "paragraph", Text = text, SourceSelector = "p" };

        // First sighting → firstSeen, not changed, block hashes captured.
        var v0 = OccamMcp.Core.Watch.WatchEvaluator.Evaluate(null, "hash-a", [Block("one"), Block("two")], includeDiff: true);
        assert("watch first sighting", v0.FirstSeen && !v0.Changed && v0.BlockHashes.Length == 2 && v0.Diff is null);

        var prior = new OccamMcp.Core.Watch.WatchRecord
        {
            Url = "https://e.com/",
            ContentHash = "hash-a",
            BlockHashes = v0.BlockHashes,
        };

        // Same content hash → not changed.
        var vSame = OccamMcp.Core.Watch.WatchEvaluator.Evaluate(prior, "hash-a", [Block("one"), Block("two")], includeDiff: true);
        assert("watch unchanged", !vSame.FirstSeen && !vSame.Changed && vSame.Diff is null);

        // Changed content + a replaced block → changed, diff shows the new block + the removed hash.
        var vDiff = OccamMcp.Core.Watch.WatchEvaluator.Evaluate(prior, "hash-b", [Block("one"), Block("THREE")], includeDiff: true);
        assert("watch changed flag", vDiff.Changed && !vDiff.FirstSeen);
        assert("watch diff added block", vDiff.Diff is { AddedBlocks.Length: 1 } d && d.AddedBlocks[0].Text == "THREE");
        assert("watch diff removed hash", vDiff.Diff is { RemovedHashes.Length: 1 });
        // T1.2: freshness magnitude — "THREE" is one token of new content.
        assert("watch delta tokens on change", vDiff.ContentDeltaTokens == 1);
        assert("watch delta tokens zero when unchanged", vSame.ContentDeltaTokens == 0);
        assert("watch delta tokens null on first seen", v0.ContentDeltaTokens is null);

        // include_diff=false → changed but no diff payload (delta tokens still reported).
        var vNoDiff = OccamMcp.Core.Watch.WatchEvaluator.Evaluate(prior, "hash-b", [Block("one"), Block("THREE")], includeDiff: false);
        assert("watch changed without diff", vNoDiff.Changed && vNoDiff.Diff is null);
        assert("watch delta tokens without diff", vNoDiff.ContentDeltaTokens == 1);

        // Store round-trip on a temp file.
        var tmp = Path.Combine(Path.GetTempPath(), $"occam-watch-{Guid.NewGuid():N}.json");
        try
        {
            var store = new OccamMcp.Core.Watch.WatchStore(tmp);
            assert("watch store empty get", store.Get("https://e.com/") is null);
            store.Upsert(prior);
            var loaded = new OccamMcp.Core.Watch.WatchStore(tmp); // fresh instance reads from disk
            var got = loaded.Get("https://e.com/");
            assert("watch store persists", got is not null && got.ContentHash == "hash-a" && got.BlockHashes.Length == 2);
            assert("watch store remove", loaded.Remove("https://e.com/") && loaded.Get("https://e.com/") is null);
        }
        finally
        {
            try { File.Delete(tmp); } catch { /* ignore */ }
        }
    }

    private static void RunSearchRerankContract(Action<string, bool> assert)
    {
        static OccamMcp.Core.Services.ProbeAnalysis Probe(
            bool ok = true,
            int statusCode = 200,
            string? failureCode = null,
            string? backend = "http",
            string? pageClass = null,
            bool login = false,
            bool challenge = false)
        {
            OccamMcp.Core.Probe.PageClassification? classification = null;
            if (pageClass is not null || login || challenge)
            {
                classification = new OccamMcp.Core.Probe.PageClassification
                {
                    PageClass = pageClass ?? "unknown",
                    Signals = new OccamMcp.Core.Routing.ProbeSignals { LikelyLoginRequired = login },
                    RiskFlags = [],
                    Challenge = challenge ? new OccamMcp.Core.Routing.ChallengeHint("captcha", false, "stop") : null,
                };
            }
            return new OccamMcp.Core.Services.ProbeAnalysis
            {
                Ok = ok,
                Url = "https://e.com/",
                Privacy = new OccamMcp.Core.Routing.PrivacyClassification { Mode = OccamMcp.Core.Routing.PrivacyMode.LocalPublic },
                StatusCode = statusCode,
                FailureCode = failureCode,
                RecommendedBackend = backend,
                Classification = classification,
            };
        }

        var score = OccamMcp.Core.Tools.SearchExtractabilityScorer.Score;
        assert("rerank dead → 0", score(Probe(ok: false)) == 0.0);
        assert("rerank http 404 → 0", score(Probe(statusCode: 404)) == 0.0);
        assert("rerank challenge → 0.05", score(Probe(challenge: true)) == 0.05);
        assert("rerank login → 0.15", score(Probe(login: true)) == 0.15);
        assert("rerank non-html none → 0.3", score(Probe(backend: "none")) == 0.3);
        assert("rerank docs → 0.9", score(Probe(pageClass: "docs")) == 0.9);
        assert("rerank generic ok → 0.7", score(Probe()) == 0.7);
        // Monotonic ordering: docs > generic > login > dead.
        assert("rerank ordering monotonic",
            score(Probe(pageClass: "docs")) > score(Probe())
            && score(Probe()) > score(Probe(login: true))
            && score(Probe(login: true)) > score(Probe(ok: false)));

        // T1.2: occam_probe surfaces the same score under recommendation.extractability (camelCase double).
        var probeResponse = new OccamMcp.Core.Tools.OccamProbeSuccessResponse(
            Ok: true,
            Url: new OccamMcp.Core.Tools.OccamProbeUrlInfo("https://e.com/", "https://e.com/"),
            Classification: new OccamMcp.Core.Tools.OccamProbeClassificationInfo(
                "docs", false, false, false, false, false, [], null, false, null),
            Recommendation: new OccamMcp.Core.Tools.OccamProbeRecommendationInfo("http", 100, 0.9),
            Policy: new OccamMcp.Core.Tools.OccamProbePolicyInfo("local_public"),
            StatusCode: 200,
            ContentType: "text/html",
            ProbeLatencyMs: 10,
            AgentHints: new OccamMcp.Core.Tools.OccamProbeAgentHintsInfo("occam_transcode", [], null),
            SocialMeta: null,
            RedirectChain: null,
            Timestamp: "2026-06-25T00:00:00Z");
        var probeJson = System.Text.Json.JsonSerializer.Serialize(
            probeResponse, OccamMcp.Core.Tools.OccamProbeJsonContext.Default.OccamProbeSuccessResponse);
        assert("probe recommendation exposes extractability", probeJson.Contains("\"extractability\":0.9"));
    }

    private static void RunPoliteFetchContract(Action<string, bool> assert)
    {
        // robots.txt parsing for the `*` group: Disallow prefixes, Crawl-delay, grouped agents.
        var rules = OccamMcp.Core.Services.RobotsRules.Parse(
            "User-agent: *\nDisallow: /private\nDisallow: /tmp\nCrawl-delay: 2\n\nUser-agent: BadBot\nDisallow: /\n");
        assert("robots disallows matching prefix", rules.IsDisallowed("/private/x"));
        assert("robots allows non-matching path", !rules.IsDisallowed("/public/y"));
        assert("robots parses crawl-delay", rules.CrawlDelaySeconds == 2);
        // The BadBot-only `Disallow: /` must NOT leak into the `*` group.
        assert("robots ignores other-agent group", !rules.IsDisallowed("/anything"));

        // Grouped user-agents: rules after `User-agent: A` + `User-agent: *` apply to `*`.
        var grouped = OccamMcp.Core.Services.RobotsRules.Parse("User-agent: A\nUser-agent: *\nDisallow: /x\n");
        assert("robots honors grouped agents", grouped.IsDisallowed("/x/1") && !grouped.IsDisallowed("/y"));

        // A site-wide block.
        var blockAll = OccamMcp.Core.Services.RobotsRules.Parse("User-agent: *\nDisallow: /\n");
        assert("robots disallow-root blocks everything", blockAll.IsDisallowed("/whatever"));

        // Empty / comment-only robots → allow all, no crawl-delay.
        var empty = OccamMcp.Core.Services.RobotsRules.Parse("# just a comment\n\n");
        assert("robots empty allows all", !empty.IsDisallowed("/x") && empty.CrawlDelaySeconds is null);
    }

    private static void RunBatchMcpContract(Action<string, bool> assert)
    {
        // URL list parsing: JSON array and delimited string both normalize to string[].
        var fromJson = OccamMcp.Core.Tools.OccamBatchToolSupport.ParseUrls("[\"https://a.com\", \"https://b.com\"]");
        assert("batch parses JSON array urls", fromJson is { Length: 2 } && fromJson[1] == "https://b.com");
        var fromDelim = OccamMcp.Core.Tools.OccamBatchToolSupport.ParseUrls("https://a.com, https://b.com\nhttps://c.com");
        assert("batch parses delimited urls", fromDelim is { Length: 3 } && fromDelim[2] == "https://c.com");
        assert("batch parse empty → empty", OccamMcp.Core.Tools.OccamBatchToolSupport.ParseUrls("  ").Length == 0);

        // Submit validation: empty urls rejected; a valid public URL accepted with defaults.
        var empty = new OccamMcp.Core.Batch.BatchSubmitRequest { Urls = [] };
        assert("batch submit rejects empty urls",
            !OccamMcp.Core.Batch.BatchJobService.TryValidateSubmit(empty, out _, out _, out var emptyErr)
            && emptyErr?.Code == "invalid_request");

        var valid = new OccamMcp.Core.Batch.BatchSubmitRequest { Urls = ["https://example.com/"] };
        var ok = OccamMcp.Core.Batch.BatchJobService.TryValidateSubmit(valid, out var urls, out var jobParams, out _);
        assert("batch submit accepts valid url", ok && urls.Count == 1 && jobParams.BackendPolicy == "http_then_browser");

        // Error envelope serializes snake_case (matches the batch HTTP wire format).
        var errJson = OccamMcp.Core.Tools.OccamBatchToolSupport.SerializeError("job_not_found", "Job was not found.");
        assert("batch error envelope shape", errJson.Contains("\"error\"") && errJson.Contains("\"code\"") && errJson.Contains("job_not_found"));
    }

    private static void RunMetadataContract(Action<string, bool> assert)
    {
        // Worker emits a camelCase `meta` object; Core must deserialize it on WorkerExtractResponse
        // and surface it (omitted when null) on the success envelope.
        const string workerJson =
            "{\"ok\":true,\"markdown\":\"x\",\"meta\":{\"publishedAt\":\"2026-01-01T00:00:00Z\",\"author\":\"Jane\",\"lang\":\"en-US\",\"canonical\":\"https://e.com/c\"}}";
        var payload = JsonSerializer.Deserialize(workerJson, WorkerExtractJsonContext.Default.WorkerExtractResponse);
        assert("meta worker json deserializes author", payload?.Meta?.Author == "Jane");
        assert("meta worker json deserializes canonical", payload?.Meta?.Canonical == "https://e.com/c");

        var response = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://e.com/", "https://e.com/"),
            "# Doc",
            "node_readability_turndown",
            [],
            Meta: new WorkerExtractMetaInfo { PublishedAt = "2026-01-01", Author = "Jane", Lang = "en", Canonical = "https://e.com/c" });
        var json = JsonSerializer.Serialize(response, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("meta serializes object", json.Contains("\"meta\""));
        var rt = JsonSerializer.Deserialize(json, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("meta round-trips publishedAt", rt?.Meta?.PublishedAt == "2026-01-01");

        var plain = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(true, new OccamTranscodeUrlInfo("https://e.com/", null), "# Doc", "http", []),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("meta omitted when null", !plain.Contains("\"meta\""));

        RunTablesContract(assert);
    }

    private static void RunTablesContract(Action<string, bool> assert)
    {
        // json_tables is an output-affecting option (must split the cache key), parses on the full
        // builder, and the worker `tables` payload must flow through to the envelope as "tables".
        var baseOptions = new OccamTranscodeOptions { PlaybookPolicy = "off" };
        var keyPlain = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions);
        var keyTables = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions with { JsonTables = true });
        assert("json_tables splits cache key", keyPlain != keyTables);

        assert(
            "json_tables option parses",
            OccamTranscodeOptionsParser.TryBuild(
                null, false, null, null, null, "off", null,
                semantic_chunking: false, capture_screenshot: false, json_blocks: false,
                json_tables: true, json_feed: false, translate_to: null, out var parsed, out _)
            && parsed.JsonTables);

        // Worker emits snake_case `tables` with `source_selector`; Core must deserialize it.
        // Semantic `records` (HN-style) ride alongside physical `rows` without replacing them.
        const string workerJson =
            "{\"ok\":true,\"markdown\":\"x\",\"tables\":[{\"caption\":\"Caps\",\"headers\":[\"A\",\"B\"],"
            + "\"rows\":[[\"1\",\"2\"],[\"3\",\"4\"]],\"source_selector\":\"#t > table:nth-of-type(1)\","
            + "\"records\":[{\"rank\":\"1\",\"title\":\"Story\",\"url\":\"https://ex.ample/a\",\"site\":\"ex.ample\","
            + "\"author\":\"alice\",\"points\":10,\"comments\":2,\"age\":\"1 hour ago\",\"schema\":\"hn_item\","
            + "\"provenance\":{\"source_selector\":\"tr.athing\",\"row_indexes\":[0,1],\"table_selector\":\"table.itemlist\"}}]}]}";
        var payload = JsonSerializer.Deserialize(workerJson, WorkerExtractJsonContext.Default.WorkerExtractResponse);
        assert("tables worker json deserializes headers", payload?.Tables is { Length: 1 } t && t[0].Headers.Length == 2);
        assert("tables worker json deserializes rows", payload?.Tables?[0].Rows is { Length: 2 } r && r[0][1] == "2");
        assert("tables worker json deserializes records", payload?.Tables?[0].Records is { Length: 1 } rec && rec[0].Title == "Story");
        assert("tables worker json deserializes provenance rows", payload?.Tables?[0].Records?[0].Provenance?.RowIndexes is { Length: 2 });

        var knowledge = OccamMcp.Core.Knowledge.WorkerKnowledgeMapper.FromExtract(null, payload?.Tables);
        assert("tables knowledge maps semantic rows", knowledge.Tables[0].SemanticRows is { Count: 1 } sr && sr[0].Author == "alice");
        assert("tables knowledge keeps physical rows", knowledge.Tables[0].Rows.Count == 2);
        var view = OccamMcp.Core.Knowledge.TableSemanticMaterializer.Materialize("## keep me\n\nbody", null, payload?.Tables);
        assert("tables materialize preserves markdown", view.Markdown.StartsWith("## keep me", StringComparison.Ordinal));
        assert("tables materialize semantic count", OccamMcp.Core.Knowledge.TableSemanticMaterializer.CountSemanticRows(view.Knowledge) == 1);

        var response = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com/", "https://example.com/"),
            "# Doc",
            "node_readability_turndown",
            [],
            Tables:
            [
                new WorkerExtractTableInfo
                {
                    Caption = "Caps",
                    Headers = ["A", "B"],
                    Rows = [["1", "2"], ["3", "4"]],
                    SourceSelector = "#t > table:nth-of-type(1)",
                    Records =
                    [
                        new WorkerExtractTableRecordInfo
                        {
                            Rank = "1",
                            Title = "Story",
                            Url = "https://ex.ample/a",
                            Site = "ex.ample",
                            Author = "alice",
                            Points = 10,
                            Comments = 2,
                            Age = "1 hour ago",
                            Schema = "hn_item",
                            Provenance = new WorkerExtractTableRowProvenanceInfo
                            {
                                SourceSelector = "tr.athing",
                                RowIndexes = [0, 1],
                                TableSelector = "table.itemlist",
                            },
                        },
                    ],
                },
            ]);
        var json = JsonSerializer.Serialize(response, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("json_tables serializes tables array", json.Contains("\"tables\""));
        assert("json_tables serializes records", json.Contains("\"records\""));
        var roundTrip = JsonSerializer.Deserialize(json, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        var table = roundTrip?.Tables is { Length: > 0 } tt ? tt[0] : null;
        assert("json_tables round-trips caption", table?.Caption == "Caps");
        assert("json_tables round-trips headers", table?.Headers is { Length: 2 } h && h[0] == "A");
        assert("json_tables round-trips rows", table?.Rows is { Length: 2 } rows && rows[1][0] == "3");
        assert("json_tables round-trips record title", table?.Records is { Length: 1 } rtRec && rtRec[0].Title == "Story");
        assert("json_tables round-trips record points", table?.Records?[0].Points == 10);

        var plainTables = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(true, new OccamTranscodeUrlInfo("https://example.com/", null), "# Doc", "http", []),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("json_tables omitted when null", !plainTables.Contains("\"tables\""));

        RunFeedContract(assert);
    }

    private static void RunFeedContract(Action<string, bool> assert)
    {
        // json_feed splits the cache key, parses on the full builder, and the worker `feed` object
        // (camelCase publishedAt) must deserialize and surface (omitted when null) on the envelope.
        var baseOptions = new OccamTranscodeOptions { PlaybookPolicy = "off" };
        var keyPlain = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions);
        var keyFeed = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions with { JsonFeed = true });
        assert("json_feed splits cache key", keyPlain != keyFeed);

        assert(
            "json_feed option parses",
            OccamTranscodeOptionsParser.TryBuild(
                null, false, null, null, null, "off", null,
                semantic_chunking: false, capture_screenshot: false, json_blocks: false, json_tables: false,
                json_feed: true, translate_to: null, out var parsed, out _)
            && parsed.JsonFeed);

        const string workerJson =
            "{\"ok\":true,\"markdown\":\"x\",\"feed\":{\"title\":\"News\",\"items\":[{\"title\":\"Item one\","
            + "\"link\":\"https://e.com/1\",\"publishedAt\":\"2026-01-01\",\"summary\":\"Hello\","
            + "\"summaryHtml\":\"<p>Hello</p>\",\"summaryText\":\"Hello\",\"summaryMarkdown\":\"Hello\"}]}}";
        var payload = JsonSerializer.Deserialize(workerJson, WorkerExtractJsonContext.Default.WorkerExtractResponse);
        assert("feed worker json deserializes title", payload?.Feed?.Title == "News");
        assert("feed worker json deserializes item link", payload?.Feed?.Items is { Length: 1 } it && it[0].Link == "https://e.com/1");
        assert("feed worker json deserializes summaryText", payload?.Feed?.Items is { Length: 1 } it2 && it2[0].SummaryText == "Hello");
        assert("feed worker json deserializes summaryHtml", payload?.Feed?.Items is { Length: 1 } it3 && it3[0].SummaryHtml == "<p>Hello</p>");
        assert("feed worker json deserializes summaryMarkdown", payload?.Feed?.Items is { Length: 1 } it4 && it4[0].SummaryMarkdown == "Hello");

        var response = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://e.com/feed", "https://e.com/feed"),
            "# News",
            "node_readability_turndown",
            [],
            Feed: new WorkerExtractFeedInfo
            {
                Title = "News",
                Items =
                [
                    new WorkerExtractFeedItemInfo
                    {
                        Title = "Item one",
                        Link = "https://e.com/1",
                        PublishedAt = "2026-01-01",
                        Summary = "Hello",
                        SummaryHtml = "<p>Hello</p>",
                        SummaryText = "Hello",
                        SummaryMarkdown = "Hello",
                    },
                ],
            });
        var json = JsonSerializer.Serialize(response, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("json_feed serializes feed object", json.Contains("\"feed\""));
        assert("json_feed serializes summaryMarkdown", json.Contains("\"summaryMarkdown\""));
        var rt = JsonSerializer.Deserialize(json, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("json_feed round-trips item title", rt?.Feed?.Items is { Length: 1 } r && r[0].Title == "Item one");
        assert("json_feed round-trips summary fields",
            rt?.Feed?.Items is { Length: 1 } r2
            && r2[0].SummaryText == "Hello"
            && r2[0].SummaryHtml == "<p>Hello</p>"
            && r2[0].SummaryMarkdown == "Hello"
            && r2[0].Summary == "Hello");

        var plainFeed = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(true, new OccamTranscodeUrlInfo("https://e.com/", null), "# Doc", "http", []),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("json_feed omitted when null", !plainFeed.Contains("\"feed\""));
    }

    private static void RunSearchContract(Action<string, bool> assert)
    {
        // occam_search is off unless OCCAM_SEARCH_PROVIDER names a provider with its required config:
        // SearXNG needs OCCAM_SEARCH_URL; Brave/Tavily need OCCAM_SEARCH_API_KEY. Env read live.
        var prevProvider = Environment.GetEnvironmentVariable("OCCAM_SEARCH_PROVIDER");
        var prevUrl = Environment.GetEnvironmentVariable("OCCAM_SEARCH_URL");
        var prevKey = Environment.GetEnvironmentVariable("OCCAM_SEARCH_API_KEY");
        try
        {
            var search = OccamServiceCollectionExtensions.BuildSearchService();

            Environment.SetEnvironmentVariable("OCCAM_SEARCH_PROVIDER", null);
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_URL", null);
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_API_KEY", null);
            assert("search disabled by default", !search.IsConfigured);
            assert("search unconfigured → typed failure",
                search.SearchAsync("test", 5, CancellationToken.None).GetAwaiter().GetResult().FailureCode == "search_unconfigured");

            Environment.SetEnvironmentVariable("OCCAM_SEARCH_PROVIDER", "searxng");
            assert("search searxng needs base url", !search.IsConfigured);
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_URL", "http://localhost:8888");
            assert("search searxng ready with url", search.IsConfigured && search.ProviderName == "searxng");

            Environment.SetEnvironmentVariable("OCCAM_SEARCH_PROVIDER", "brave");
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_URL", null);
            assert("search brave needs key", !search.IsConfigured);
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_API_KEY", "k");
            assert("search brave ready with key", search.IsConfigured && search.ProviderName == "brave");

            Environment.SetEnvironmentVariable("OCCAM_SEARCH_PROVIDER", "nonsuch");
            assert("search unknown provider disabled", !search.IsConfigured);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_PROVIDER", prevProvider);
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_URL", prevUrl);
            Environment.SetEnvironmentVariable("OCCAM_SEARCH_API_KEY", prevKey);
        }

        // Response serialization round-trips (camelCase, snippet omitted when null).
        var ok = JsonSerializer.Serialize(
            new OccamSearchSuccessResponse(true, "q", "searxng", 1,
                [new OccamSearchResultInfo("T", "https://e.com/a", "snip")]),
            OccamSearchJsonContext.Default.OccamSearchSuccessResponse);
        var rt = JsonSerializer.Deserialize(ok, OccamSearchJsonContext.Default.OccamSearchSuccessResponse);
        assert("search response round-trips url", rt?.Results is { Length: 1 } && rt.Results[0].Url == "https://e.com/a");
        assert("search response camelCase", ok.Contains("\"results\"") && ok.Contains("\"snippet\""));
    }

    private static void RunManagedAdapterContract(Action<string, bool> assert)
    {
        // Package 3: managed backend is off unless OCCAM_MANAGED_PROVIDER names a provider; keyed
        // providers (firecrawl) also need OCCAM_MANAGED_API_KEY; per-domain opt-in via
        // OCCAM_MANAGED_DOMAINS. Env is read live, so toggle + restore around the assertions.
        var prevProvider = Environment.GetEnvironmentVariable("OCCAM_MANAGED_PROVIDER");
        var prevKey = Environment.GetEnvironmentVariable("OCCAM_MANAGED_API_KEY");
        var prevDomains = Environment.GetEnvironmentVariable("OCCAM_MANAGED_DOMAINS");
        try
        {
            var backend = OccamServiceCollectionExtensions.BuildManagedBackend();

            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", null);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_API_KEY", null);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_DOMAINS", null);
            assert("managed disabled by default", !backend.IsReady && !backend.ShouldAttempt("https://example.com/"));

            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "firecrawl");
            assert("managed keyed provider needs key", !backend.IsReady);

            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "jina");
            assert("managed keyless provider ready", backend.IsReady);
            assert("managed no allowlist → any host", backend.ShouldAttempt("https://example.com/x"));

            // New keyed providers: registered + recognized, and gated on OCCAM_MANAGED_API_KEY.
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_API_KEY", null);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "spider");
            assert("managed spider needs key", !backend.IsReady);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "scrapfly");
            assert("managed scrapfly needs key", !backend.IsReady);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_API_KEY", "k");
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "spider");
            assert("managed spider ready with key", backend.IsReady);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "scrapfly");
            assert("managed scrapfly ready with key", backend.IsReady);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_API_KEY", null);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "jina");

            Environment.SetEnvironmentVariable("OCCAM_MANAGED_DOMAINS", "example.com, foo.org");
            assert("managed allowlist host match", backend.ShouldAttempt("https://example.com/x"));
            assert("managed allowlist subdomain match", backend.ShouldAttempt("https://docs.example.com/x"));
            assert("managed allowlist excludes others", !backend.ShouldAttempt("https://other.com/x"));

            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", "nonsuch");
            assert("managed unknown provider disabled", !backend.IsReady);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_PROVIDER", prevProvider);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_API_KEY", prevKey);
            Environment.SetEnvironmentVariable("OCCAM_MANAGED_DOMAINS", prevDomains);
        }
    }

    private static void RunTranslationContract(Action<string, bool> assert)
    {
        // translate_to is an output-affecting option (must split the cache key), validates the
        // language code, and surfaces translatedMarkdown/translatedTo additively (omitted when null).
        var baseOptions = new OccamTranscodeOptions { PlaybookPolicy = "off" };
        var keyPlain = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions);
        var keyRu = TranscodeCacheKey.Compute("https://example.com/", "http", baseOptions with { TranslateTo = "ru" });
        assert("translate_to splits cache key", keyPlain != keyRu);

        assert(
            "translate_to valid code parses",
            OccamTranscodeOptionsParser.TryBuild(
                null, false, null, null, null, "off", null,
                semantic_chunking: false, capture_screenshot: false, json_blocks: false, json_tables: false,
                json_feed: false, translate_to: "pt-BR", out var okOpts, out _)
            && okOpts.TranslateTo == "pt-BR");

        assert(
            "translate_to bad code rejected",
            !OccamTranscodeOptionsParser.TryBuild(
                null, false, null, null, null, "off", null,
                semantic_chunking: false, capture_screenshot: false, json_blocks: false, json_tables: false,
                json_feed: false, translate_to: "not a lang!", out _, out _));

        var translated = new OccamTranscodeSuccessResponse(
            true,
            new OccamTranscodeUrlInfo("https://example.com/", "https://example.com/"),
            "# Hello",
            "node_readability_turndown",
            [],
            TranslatedMarkdown: "# Привет",
            TranslatedTo: "ru");
        var json = JsonSerializer.Serialize(translated, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        var roundTrip = JsonSerializer.Deserialize(json, OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("translate_to round-trips translatedTo", roundTrip?.TranslatedTo == "ru");
        assert("translate_to round-trips translatedMarkdown", roundTrip?.TranslatedMarkdown == "# Привет");

        var plain = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(true, new OccamTranscodeUrlInfo("https://example.com/", null), "# Hello", "http", []),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        assert("translatedMarkdown omitted when null", !plain.Contains("translatedMarkdown"));
    }

    private static void RunTranscodeCacheInfra(Action<string, bool> assert)
    {
        // Cache key determinism: identical inputs hash identically; an output-affecting
        // option (max_tokens) yields a distinct key.
        var baseOptions = new OccamTranscodeOptions { PlaybookPolicy = "off" };
        var keyA = TranscodeCacheKey.Compute("https://Example.com/Docs", "http_then_browser", baseOptions);
        var keyB = TranscodeCacheKey.Compute("https://example.com/Docs#frag", "http_then_browser", baseOptions);
        assert("transcode cache key normalizes host+fragment", keyA == keyB);
        var keyTokens = TranscodeCacheKey.Compute(
            "https://example.com/Docs",
            "http_then_browser",
            baseOptions with { MaxTokens = 500 });
        assert("transcode cache key max_tokens differs", keyA != keyTokens);

        // Eligibility / privacy guard.
        assert("transcode cache eligible public", TranscodeCacheEligibility.IsCacheable("https://example.com/", null, null, 60));
        assert("transcode cache ineligible ttl off", !TranscodeCacheEligibility.IsCacheable("https://example.com/", null, null, 0));
        assert("transcode cache ineligible ttl null", !TranscodeCacheEligibility.IsCacheable("https://example.com/", null, null, null));
        assert("transcode cache ineligible session", !TranscodeCacheEligibility.IsCacheable("https://example.com/", "profile", null, 60));
        assert("transcode cache ineligible if_none_match", !TranscodeCacheEligibility.IsCacheable("https://example.com/", null, "deadbeef", 60));
        assert("transcode cache ineligible loopback", !TranscodeCacheEligibility.IsCacheable("http://127.0.0.1/", null, null, 60));
        assert("transcode cache ineligible localhost", !TranscodeCacheEligibility.IsCacheable("http://localhost/page", null, null, 60));
        assert("transcode cache ineligible rfc1918", !TranscodeCacheEligibility.IsCacheable("http://10.0.0.5/page", null, null, 60));

        // Store round-trip + TTL expiry, using a temp dir cleaned up in finally.
        var dir = Path.Combine(Path.GetTempPath(), $"occam-cache-infra-{Guid.NewGuid():N}");
        try
        {
            var cache = new FileTranscodeResponseCache(dir);
            const string stored = "{\"ok\":true,\"markdown\":\"# Hi\"}";

            cache.Set(keyA, stored);
            assert(
                "transcode cache hit within ttl",
                cache.TryGet(keyA, 3600, out var hitJson, out var ageSeconds)
                    && hitJson == stored
                    && ageSeconds >= 0);

            var expiredKey = keyA + "-expired";
            cache.Set(expiredKey, stored, DateTimeOffset.UtcNow.ToUnixTimeSeconds() - 100);
            assert(
                "transcode cache miss when expired",
                !cache.TryGet(expiredKey, 10, out _, out _));

            assert(
                "transcode cache miss unknown key",
                !cache.TryGet("does-not-exist", 3600, out _, out _));
        }
        finally
        {
            try
            {
                Directory.Delete(dir, recursive: true);
            }
            catch (IOException)
            {
            }
        }
    }

    private static WellKnownGenomeFetcher CreateWellKnownGenomeFetcher()
    {
        var services = new ServiceCollection();
        services.AddHttpClient("playbook.wellKnownGenome")
            .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler
            {
                AllowAutoRedirect = true,
                AutomaticDecompression = DecompressionMethods.All,
            });
        var provider = services.BuildServiceProvider();
        return new WellKnownGenomeFetcher(provider.GetRequiredService<IHttpClientFactory>());
    }

    private static void RunVectorizedHtmlScanner(Action<string, bool> assert)
    {
        ReadOnlySpan<char> html = "  \r\n\t<div>text</div>  ";
        assert("vector tag first lt", VectorizedHtmlScanner.IndexOfAnyTag(html) == 5);
        assert("vector skip ws", VectorizedHtmlScanner.SkipWhitespaceVectorized(html) == 5);

        ReadOnlySpan<char> noTag = "plain text only";
        assert("vector tag absent", VectorizedHtmlScanner.IndexOfAnyTag(noTag) == -1);
        assert("vector skip ws none", VectorizedHtmlScanner.SkipWhitespaceVectorized(noTag) == 0);

        ReadOnlySpan<char> gtOnly = "a>b";
        assert("vector tag gt", VectorizedHtmlScanner.IndexOfAnyTag(gtOnly) == 1);
    }

    private static void RunHtmlStreamScanner(Action<string, bool> assert)
    {
        ReadOnlySpan<char> empty = "";
        assert("stream empty html", !HtmlStreamScanner.EnumerateAnchors(empty).MoveNext());

        ReadOnlySpan<char> malformed = "<a href='/x'>no close";
        assert("stream malformed no close", !HtmlStreamScanner.EnumerateAnchors(malformed).MoveNext());

        var html = """
            <p>noise</p>
            <a href="/docs/start">Start</a>
            <A HREF='https://nginx.org/en/docs/ngx_core_module.html'>Core</A>
            <a href=javascript:void(0)>Skip</a>
            """;
        var anchors = CollectAnchors(html);
        assert("stream anchor count", anchors.Count == 3);
        assert("stream first href", anchors[0].href == "/docs/start");
        assert("stream first text", anchors[0].text == "Start");
        assert("stream single-quoted href", anchors[1].href == "https://nginx.org/en/docs/ngx_core_module.html");
        assert("stream unquoted js href", anchors[2].href == "javascript:void(0)");
    }

    private static void RunHtmlHeadScanner(Action<string, bool> assert)
    {
        ReadOnlySpan<char> empty = "";
        var emptyFields = HtmlHeadScanner.Scan(empty);
        assert("head scan empty", emptyFields.Lang.IsEmpty && emptyFields.OgTitle.IsEmpty);

        var html = """
            <html lang="en"><head>
            <meta property="og:title" content="Scanner Title" />
            <meta content="Scanner Desc" property="og:description" />
            <meta name="twitter:card" content="summary" />
            </head><body></body></html>
            """;
        var fields = HtmlHeadScanner.Scan(html);
        assert("head scan lang", fields.Lang.SequenceEqual("en".AsSpan()));
        assert("head scan og:title", fields.OgTitle.SequenceEqual("Scanner Title".AsSpan()));
        assert("head scan reverse og:description", fields.OgDescription.SequenceEqual("Scanner Desc".AsSpan()));
        assert("head scan twitter name", fields.TwitterCardProperty.IsEmpty && fields.TwitterCardName.SequenceEqual("summary".AsSpan()));

        var noHead = """<html><meta name="description" content="Orphan" /><body></body>""";
        var orphan = HtmlHeadScanner.Scan(noHead);
        assert("head scan no head wrapper", orphan.NameDescription.SequenceEqual("Orphan".AsSpan()));
    }

    private static void RunHtmlVisibleTextScanner(Action<string, bool> assert)
    {
        assert("visible text empty", HtmlVisibleTextScanner.CountVisibleText("") == 0);

        ReadOnlySpan<char> plain = "hello world";
        assert("visible text plain", HtmlVisibleTextScanner.CountVisibleText(plain) == 11);

        var scriptBlock = "<p>before</p><script>var x=1;</script><p>after</p>";
        assert("visible text script block stripped", HtmlVisibleTextScanner.CountVisibleText(scriptBlock) == 12);

        var styleBlock = "<style>.x{color:red}</style><div>visible</div>";
        assert("visible text style block stripped", HtmlVisibleTextScanner.CountVisibleText(styleBlock) == 7);

        var nestedTags = "<div><span>text</span></div>";
        assert("visible text nested tags", HtmlVisibleTextScanner.CountVisibleText(nestedTags) == 4);

        var loneGt = "a>b";
        assert("visible text lone gt", HtmlVisibleTextScanner.CountVisibleText(loneGt) == 3);
    }

    private static List<(string href, string text)> CollectAnchors(ReadOnlySpan<char> html)
    {
        var list = new List<(string href, string text)>();
        var enumerator = HtmlStreamScanner.EnumerateAnchors(html);
        while (enumerator.MoveNext())
        {
            list.Add((enumerator.Href.ToString(), enumerator.InnerText.ToString()));
        }

        return list;
    }
}
