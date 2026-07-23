using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace OccamMcp.L0Gate;

internal static class L3HealLearnRunner
{
    public static bool Run(WorkerPaths paths, Action<string, bool> assert)
    {
        L3HealLearnUnitTests.Run(assert);

        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l3 heal-learn corpus: {corpusPath}");
        assert("l3 heal-learn corpus exists", File.Exists(corpusPath));

        if (!paths.IsConfigured)
        {
            Console.WriteLine("l3 heal-learn: workers not configured — policy unit only");
            return true;
        }

        var services = new ServiceCollection();
        services.AddOccamCore();
        var provider = services.BuildServiceProvider();
        var pipeline = provider.GetRequiredService<TranscodePipeline>();
        var healService = provider.GetRequiredService<PlaybookHealService>();
        var saveService = provider.GetRequiredService<PlaybookSaveService>();
        var seedResolver = provider.GetRequiredService<PlaybookSeedResolver>();
        var browserDaemonClient = provider.GetRequiredService<IBrowserDaemonClient>();

        var localTemp = Path.Combine(Path.GetTempPath(), $"occam-l3-heal-{Guid.NewGuid():N}");
        Directory.CreateDirectory(localTemp);
        PlaybookPaths.LocalRootOverrideForTests = localTemp;
        var priorLocalRoot = Environment.GetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT");
        Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", localTemp);

        BrowserDaemonHost.TryEnsureRunningAsync(paths).GetAwaiter().GetResult();
        _ = pipeline.Transcode(
            "https://nuxt.com/docs/getting-started/introduction",
            OccamBackendPolicy.Browser,
            CancellationToken.None);
        var warmSkeleton = browserDaemonClient.TryCaptureSkeletonJsonAsync(
            "https://nginx.org/en/docs/",
            200,
            120_000,
            null,
            CancellationToken.None).GetAwaiter().GetResult();
        var liveHeal = !string.IsNullOrWhiteSpace(warmSkeleton)
            && warmSkeleton.Contains("\"Ok\":true", StringComparison.OrdinalIgnoreCase);
        if (!liveHeal)
        {
            Console.WriteLine("l3 heal-live: SKIPPED — Playwright skeleton unavailable (policy + save path only)");
        }

        try
        {
            var pilotTotal = 0;
            var k1Pass = 0;
            var k2Pass = 0;
            var k3Pass = 0;

            foreach (var line in File.ReadAllLines(corpusPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize(line, L3HealLearnJsonContext.Default.L3HealLearnCase);
                if (entry is null)
                {
                    assert("l3 heal-learn/json parse", false);
                    continue;
                }

                if (entry.Tier == "negative")
                {
                    RunNegative(entry, assert);
                    continue;
                }

                if (entry.Tier != "pilot" || string.IsNullOrWhiteSpace(entry.Url))
                {
                    continue;
                }

                pilotTotal++;
                Console.WriteLine($"l3 pilot: {entry.Id}");
                var seedJson = LoadSeedJson(entry.Seed);
                if (seedJson is null)
                {
                    assert($"l3 pilot/{entry.Id} seed", false);
                    continue;
                }

                var brokenJson = SimulateFail(seedJson, entry.SimulatedFail);
                TranscodeOutcome brokenOutcome;
                using (PlaybookVerifyScope.Push(brokenJson))
                {
                    brokenOutcome = pipeline.Transcode(entry.Url, OccamBackendPolicy.HttpThenBrowser, CancellationToken.None);
                }

                // Use the production default MaxSkeletonNodes (600), not a hardcoded 400: the 400 cap
                // could be exhausted before the DFS reached main on content-heavy pages (MDN),
                // flaking mainCandidates — the gate must exercise the same cap production heals with.
                var healResult = healService.HealAsync(new PlaybookHealRequest(
                    entry.Url,
                    "thin_extract")).GetAwaiter().GetResult();
                var healOk = healResult.Ok
                    && healResult.Anchors?.MainCandidates.Count > 0;
                if (healOk)
                {
                    k1Pass++;
                }

                assert($"l3 pilot/{entry.Id} K1 heal capture", !liveHeal || healOk);

                var saveResult = saveService.Save(new PlaybookSaveRequest(
                    entry.Url,
                    seedJson,
                    Verify: true,
                    VerifyUrl: entry.Url,
                    LessonNote: $"L3 gate pilot {entry.Id}",
                    FailureReason: "thin_extract",
                    HostId: "l3-gate"));
                if (saveResult.Ok)
                {
                    k2Pass++;
                }

                assert($"l3 pilot/{entry.Id} K2 save verify", saveResult.Ok);

                seedResolver.ClearCacheForTests();
                var retryOutcome = pipeline.Transcode(entry.Url, OccamBackendPolicy.HttpThenBrowser, CancellationToken.None);
                if (saveResult.Ok && retryOutcome.Ok && (retryOutcome.Markdown?.Length ?? 0) >= 200)
                {
                    k3Pass++;
                }

                assert($"l3 pilot/{entry.Id} K3 retry transcode", saveResult.Ok && retryOutcome.Ok);
            }

            if (pilotTotal > 0)
            {
                var k1Rate = (double)k1Pass / pilotTotal;
                var k2Rate = (double)k2Pass / pilotTotal;
                var k3Rate = k2Pass > 0 ? (double)k3Pass / k2Pass : 0;
                Console.WriteLine($"l3 KPI: K1={k1Rate:P0} K2={k2Rate:P0} K3={k3Rate:P0} (n={pilotTotal})");
                assert("l3 K1 heal capture >= 85%", !liveHeal || k1Rate >= 0.85);
                assert("l3 K2 save verify >= 60%", k2Rate >= 0.60);
                assert("l3 K3 transcode after learn >= 50% of K2", k3Rate >= 0.50);
            }

            return pilotTotal > 0;
        }
        finally
        {
            PlaybookPaths.LocalRootOverrideForTests = null;
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", priorLocalRoot);
            try
            {
                Directory.Delete(localTemp, recursive: true);
            }
            catch
            {
                // best-effort
            }
        }
    }

    private static void RunNegative(L3HealLearnCase entry, Action<string, bool> assert)
    {
        Console.WriteLine($"l3 negative: {entry.Id}");
        switch (entry.Id)
        {
            case "heal-neg-captcha":
                assert($"l3 neg/{entry.Id}", !PlaybookHealPolicy.ShouldOfferHeal("captcha_or_challenge"));
                break;
            case "heal-neg-404":
                assert($"l3 neg/{entry.Id}", !PlaybookHealPolicy.ShouldOfferHeal("http_404"));
                break;
            case "heal-neg-private":
                assert($"l3 neg/{entry.Id}", !PlaybookHealPolicy.ShouldOfferHeal("private_url_blocked"));
                break;
            case "heal-neg-login-no-session":
                assert($"l3 neg/{entry.Id}", !PlaybookHealPolicy.ShouldOfferHeal("requires_login"));
                break;
            case "heal-neg-bad-draft":
                RunBadDraftNegative(assert);
                break;
            case "heal-neg-secrets-in-json":
                RunSecretsNegative(assert);
                break;
            case "heal-neg-retry-cap":
                assert($"l3 neg/{entry.Id}", PlaybookHealPolicy.MaxVerifyRetries == 3);
                break;
            case "heal-neg-workers":
                assert($"l3 neg/{entry.Id}", PlaybookHealPolicy.IsTerminalFailure("workers_unavailable"));
                break;
            case "heal-neg-challenge-url":
                assert($"l3 neg/{entry.Id}", !PlaybookHealPolicy.ShouldOfferHeal(
                    entry.FailureCode ?? "extraction_failed",
                    finalUrl: entry.Url,
                    requestUrl: entry.Url));
                break;
            default:
                assert($"l3 neg/{entry.Id} known", false);
                break;
        }
    }

    private static void RunBadDraftNegative(Action<string, bool> assert)
    {
        var services = new ServiceCollection();
        services.AddOccamCore();
        var provider = services.BuildServiceProvider();
        var saveService = provider.GetRequiredService<PlaybookSaveService>();
        var localTemp = Path.Combine(Path.GetTempPath(), $"occam-l3-neg-{Guid.NewGuid():N}");
        Directory.CreateDirectory(localTemp);
        PlaybookPaths.LocalRootOverrideForTests = localTemp;
        try
        {
            var badJson = """
                {
                  "schema_version": "1.0",
                  "id": "bad.test",
                  "hosts": ["nginx.org"],
                  "extract": { "contentSelectors": ["#nonexistent-selector-xyz"] }
                }
                """;
            var result = saveService.Save(new PlaybookSaveRequest(
                "https://nginx.org/en/docs/",
                badJson,
                Verify: true,
                VerifyUrl: "https://nginx.org/en/docs/"));
            assert("l3 neg/heal-neg-bad-draft verify failed", !result.Ok);
            assert("l3 neg/heal-neg-bad-draft code",
                result.FailureCode is "playbook_verify_failed" or "playbook_verify_low_score" or "playbook_verify_high_noise");
            assert("l3 neg/heal-neg-bad-draft disk unchanged", !File.Exists(Path.Combine(localTemp, "bad.test.playbook.json")));
        }
        finally
        {
            PlaybookPaths.LocalRootOverrideForTests = null;
            try { Directory.Delete(localTemp, recursive: true); } catch { }
        }
    }

    private static void RunSecretsNegative(Action<string, bool> assert)
    {
        var services = new ServiceCollection();
        services.AddOccamCore();
        var saveService = new ServiceCollection().AddOccamCore().BuildServiceProvider().GetRequiredService<PlaybookSaveService>();
        var secretJson = """
            {
              "schema_version": "1.0",
              "id": "secret.test",
              "hosts": ["example.com"],
              "request": { "headers": { "Cookie": "session=abc" } }
            }
            """;
        var result = saveService.Save(new PlaybookSaveRequest("https://example.com/", secretJson, Verify: false));
        assert("l3 neg/heal-neg-secrets-in-json rejected", !result.Ok);
        assert("l3 neg/heal-neg-secrets code", result.FailureCode == "playbook_save_rejected");
    }

    private static string? LoadSeedJson(string? seedFile)
    {
        if (string.IsNullOrWhiteSpace(seedFile))
        {
            return null;
        }

        var root = WorkerPaths.ResolveOccamHome();
        if (root is null)
        {
            return null;
        }

        var path = Path.Combine(root, "profiles", "playbooks", "seeds", seedFile);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string SimulateFail(string seedJson, string? mode)
    {
        var node = JsonNode.Parse(seedJson)?.AsObject() ?? new JsonObject();
        switch (mode)
        {
            case "strip contentSelectors":
                if (node["extract"] is JsonObject extract)
                {
                    extract.Remove("contentSelectors");
                }

                break;
            case "wrong selector #sidebar":
                node["extract"] ??= new JsonObject();
                node["extract"]!["contentSelectors"] = new JsonArray("#sidebar");
                break;
            case "preferred_backend http only":
                node["routing"] ??= new JsonObject();
                node["routing"]!["preferred_backend"] = "http";
                break;
            case "omit domStripSelectors":
                if (node["extract"] is JsonObject extract2)
                {
                    extract2.Remove("domStripSelectors");
                }
                node["extract"] ??= new JsonObject();
                node["extract"]!["contentSelectors"] = new JsonArray("#occam-gate-nonexistent-docker");
                break;
            case "over-broad selector footer leakage":
                node["extract"] ??= new JsonObject();
                node["extract"]!["contentSelectors"] = new JsonArray("body");
                break;
        }

        return node.ToJsonString();
    }

    private static string ResolveCorpusPath()
    {
        var root = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, "corpora", "l3-heal-learn.jsonl");
    }
}

internal sealed class L3HealLearnCase
{
    public string Id { get; init; } = "";
    public string Tier { get; init; } = "";
    public string? Url { get; init; }
    public string? Seed { get; init; }
    public string? SimulatedFail { get; init; }
    public string? Expect { get; init; }
    public string? FailureCode { get; init; }
}

[JsonSerializable(typeof(L3HealLearnCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L3HealLearnJsonContext : JsonSerializerContext;
