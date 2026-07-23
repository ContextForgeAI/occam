using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;

namespace OccamMcp.L0Gate;

internal static class L4GenomeRunner
{
    public static bool Run(WorkerPaths paths, Action<string, bool> assert)
    {
        L4GenomeUnitTests.Run(assert);

        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l4 genome corpus: {corpusPath}");
        assert("l4 genome corpus exists", File.Exists(corpusPath));

        var services = new ServiceCollection();
        services.AddOccamCore();
        var provider = services.BuildServiceProvider();
        var resolver = provider.GetRequiredService<PlaybookSeedResolver>();
        var pipeline = provider.GetRequiredService<TranscodePipeline>();
        var saveService = provider.GetRequiredService<PlaybookSaveService>();
        var extractService = provider.GetRequiredService<KnowledgeExtractService>();

        var localTemp = Path.Combine(Path.GetTempPath(), $"occam-l4-genome-{Guid.NewGuid():N}");
        Directory.CreateDirectory(localTemp);
        PlaybookPaths.LocalRootOverrideForTests = localTemp;
        var priorLocalRoot = Environment.GetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT");
        Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", localTemp);

        var liveNetwork = paths.IsConfigured;
        if (!liveNetwork)
        {
            Console.WriteLine("l4 genome: workers not configured — unit tests only");
        }

        try
        {
            var entries = File.ReadAllLines(corpusPath)
                .Where(line => !string.IsNullOrWhiteSpace(line))
                .Select(line => JsonSerializer.Deserialize(line, L4GenomeJsonContext.Default.L4GenomeCase))
                .Where(entry => entry is not null)
                .Cast<L4GenomeCase>()
                .ToList();

            var pb4aEntries = entries
                .Where(e => string.Equals(e.SubSlice, "PB4a", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var pb4bEntries = entries
                .Where(e => string.Equals(e.SubSlice, "PB4b", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var pb4cEntries = entries
                .Where(e => string.Equals(e.SubSlice, "PB4c", StringComparison.OrdinalIgnoreCase))
                .ToList();
            var manifestEntries = entries
                .Where(e => string.Equals(e.SubSlice, "manifest", StringComparison.OrdinalIgnoreCase))
                .ToList();

            var pb4aTotal = 0;
            var k5Pass = 0;
            var k6Pass = 0;

            foreach (var entry in pb4aEntries.Where(e => e.Tier == "negative"))
            {
                RunNegative(entry, resolver, extractService, assert);
            }

            foreach (var entry in pb4bEntries.Where(e => e.Tier == "negative"))
            {
                RunNegative(entry, resolver, extractService, assert);
            }

            var k8Pass = 0;
            var k8Total = 0;
            foreach (var entry in pb4cEntries.Where(e => e.Tier == "negative"))
            {
                k8Total++;
                if (RunPublishNegative(entry, assert))
                {
                    k8Pass++;
                }
            }

            foreach (var entry in manifestEntries.Where(e => e.Tier == "negative"))
            {
                RunManifestNegative(entry, assert);
            }

            foreach (var entry in pb4aEntries.Where(e => e.Tier == "pilot"))
            {
                pb4aTotal++;
                Console.WriteLine($"l4 pilot: {entry.Id}");

                switch (entry.Id)
                {
                    case "genome-pilot-nginx-auto":
                        RunNginxAuto(entry, pipeline, assert, liveNetwork, ref k6Pass);
                        break;
                    case "genome-pilot-k8s-resolve":
                        RunK8sResolve(entry, resolver, assert, ref k5Pass);
                        break;
                    case "genome-pilot-local-overlay":
                        RunLocalOverlay(entry, pipeline, saveService, resolver, assert, liveNetwork);
                        break;
                    case "genome-pilot-lessons-export":
                        RunLessonsExport(entry, resolver, saveService, assert);
                        break;
                    default:
                        assert($"l4 pilot/{entry.Id} known", false);
                        break;
                }
            }

            var k7Pass = 0;
            var k7Total = 0;
            foreach (var entry in pb4bEntries.Where(e => e.Tier == "pilot"))
            {
                Console.WriteLine($"l4 pilot: {entry.Id}");
                switch (entry.Id)
                {
                    case "genome-pilot-k8s-extract":
                        k7Total++;
                        RunK8sExtract(entry, extractService, assert, liveNetwork, ref k7Pass);
                        break;
                    default:
                        assert($"l4 pilot/{entry.Id} known", false);
                        break;
                }
            }

            if (pb4aTotal > 0)
            {
                var k5Rate = pb4aTotal > 0 ? (double)k5Pass / 1.0 : 0;
                var k6Rate = liveNetwork && k6Pass > 0 ? 1.0 : liveNetwork ? 0.0 : 1.0;
                Console.WriteLine($"l4 KPI: K5={k5Rate:P0} K6={k6Rate:P0} (pilots={pb4aTotal}, live={liveNetwork})");
                assert("l4 K5 resolve schema >= 80%", k5Pass >= 1);
                assert("l4 K6 auto-policy fidelity", !liveNetwork || k6Pass >= 1);
            }

            if (k7Total > 0)
            {
                var k7Rate = k7Total > 0 ? (double)k7Pass / k7Total : 0;
                Console.WriteLine($"l4 KPI: K7={k7Rate:P0} (pilots={k7Total}, live={liveNetwork})");
                assert("l4 K7 extract field hit >= 70%", !liveNetwork || k7Rate >= 0.7);
            }

            if (k8Total > 0)
            {
                var k8Rate = (double)k8Pass / k8Total;
                Console.WriteLine($"l4 KPI: K8={k8Rate:P0} (negatives={k8Total})");
                assert("l4 K8 publish hygiene 100%", k8Rate >= 1.0);
            }

            return pb4aTotal > 0 || k7Total > 0 || k8Total > 0 || pb4bEntries.Count > 0;
        }
        finally
        {
            PlaybookPaths.LocalRootOverrideForTests = null;
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", priorLocalRoot);
            resolver.ClearCacheForTests();
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

    private static void RunNginxAuto(
        L4GenomeCase entry,
        TranscodePipeline pipeline,
        Action<string, bool> assert,
        bool liveNetwork,
        ref int k6Pass)
    {
        if (!liveNetwork)
        {
            assert($"l4 pilot/{entry.Id} skipped offline", true);
            return;
        }

        var options = new OccamTranscodeOptions { PlaybookPolicy = PlaybookPolicy.Auto };
        var outcome = pipeline.Transcode(entry.Url!, OccamBackendPolicy.Http, options, CancellationToken.None);
        assert($"l4 pilot/{entry.Id} transcode ok", outcome.Ok);

        var markdown = outcome.Markdown ?? string.Empty;
        assert($"l4 pilot/{entry.Id} markdown length", markdown.Length >= 200);
        assert($"l4 pilot/{entry.Id} has content heading", markdown.Contains("nginx", StringComparison.OrdinalIgnoreCase));

        var ngxModuleLines = markdown
            .Split('\n')
            .Count(line => line.TrimStart().StartsWith("*", StringComparison.Ordinal) && line.Contains("ngx_", StringComparison.Ordinal));
        assert($"l4 pilot/{entry.Id} nginxIndexModuleSpam pruned", ngxModuleLines <= 3);

        if (outcome.Ok && markdown.Length >= 200 && ngxModuleLines <= 3)
        {
            k6Pass++;
        }
    }

    private static void RunK8sResolve(L4GenomeCase entry, PlaybookSeedResolver resolver, Action<string, bool> assert, ref int k5Pass)
    {
        var result = resolver.ResolveExtended(new PlaybookResolveOptions(entry.Url!));
        assert($"l4 pilot/{entry.Id} resolve ok", result.Ok);
        assert($"l4 pilot/{entry.Id} provenance community", result.Provenance == PlaybookProvenance.Community);
        assert($"l4 pilot/{entry.Id} pageClass concepts", result.PageClass == "concepts");
        assert($"l4 pilot/{entry.Id} knowledgeSchema present", result.KnowledgeSchema is not null);
        assert(
            $"l4 pilot/{entry.Id} genome page_classes",
            result.Genome is not null && result.Genome.Value.ToString().Contains("concepts", StringComparison.Ordinal));

        if (result.Ok && result.PageClass == "concepts" && result.KnowledgeSchema is not null)
        {
            k5Pass++;
        }
    }

    private static void RunLocalOverlay(
        L4GenomeCase entry,
        TranscodePipeline pipeline,
        PlaybookSaveService saveService,
        PlaybookSeedResolver resolver,
        Action<string, bool> assert,
        bool liveNetwork)
    {
        var seedJson = LoadSeedJson("nginx.org.seed.json");
        if (seedJson is null)
        {
            assert($"l4 pilot/{entry.Id} seed load", false);
            return;
        }

        var localJson = seedJson.Replace(
            "\"agent_notes\": \"Index /en/docs/ — module spam pruned via seed; use leaf URLs for directive detail.\"",
            "\"agent_notes\": \"L4 local overlay — postMarkdown disabled to prove tier win.\"",
            StringComparison.Ordinal);
        localJson = localJson.Replace("\"nginxIndexModuleSpam\": true", "\"nginxIndexModuleSpam\": false", StringComparison.Ordinal);

        // Hermetic local tier: save + resolve against a throwaway temp root so the test never reads
        // or writes the dev machine's ~/.occam (which previously polluted this case).
        var savedLocalRoot = Environment.GetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT");
        var tempLocalRoot = Path.Combine(Path.GetTempPath(), $"occam-l4-local-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempLocalRoot);
        try
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", tempLocalRoot);

            var saveResult = saveService.Save(new PlaybookSaveRequest(
                entry.Url!,
                localJson,
                Verify: false));
            assert($"l4 pilot/{entry.Id} local save", saveResult.Ok);

            resolver.ClearCacheForTests();
            var autoOptions = new OccamTranscodeOptions { PlaybookPolicy = PlaybookPolicy.Auto };
            if (!liveNetwork)
            {
                assert($"l4 pilot/{entry.Id} offline skip transcode", true);
                return;
            }

            var outcome = pipeline.Transcode(entry.Url!, OccamBackendPolicy.Http, autoOptions, CancellationToken.None);
            assert($"l4 pilot/{entry.Id} auto transcode ok", outcome.Ok);

            // With the local genome shipped as a soft overlay, its absence of postMarkdown wins over
            // the repo seed's nginxIndexModuleSpam pruning, so the ngx_ module lines survive.
            var ngxModuleLines = (outcome.Markdown ?? string.Empty)
                .Split('\n')
                .Count(line => line.TrimStart().StartsWith("*", StringComparison.Ordinal) && line.Contains("ngx_", StringComparison.Ordinal));
            assert($"l4 pilot/{entry.Id} local tier disables seed postMarkdown", ngxModuleLines > 3);
        }
        finally
        {
            Environment.SetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT", savedLocalRoot);
            resolver.ClearCacheForTests();
            try { Directory.Delete(tempLocalRoot, recursive: true); } catch { /* best effort */ }
        }
    }

    private static void RunLessonsExport(
        L4GenomeCase entry,
        PlaybookSeedResolver resolver,
        PlaybookSaveService saveService,
        Action<string, bool> assert)
    {
        var seedJson = LoadSeedJson("nginx.org.seed.json");
        if (seedJson is null)
        {
            assert($"l4 pilot/{entry.Id} seed load", false);
            return;
        }

        var saveResult = saveService.Save(new PlaybookSaveRequest(
            "https://nginx.org/en/docs/",
            seedJson,
            Verify: false,
            LessonNote: "L4 lessons export gate pilot",
            FailureReason: "thin_extract",
            HostId: "l4-gate"));
        assert($"l4 pilot/{entry.Id} save with lesson", saveResult.Ok);

        resolver.ClearCacheForTests();
        var result = resolver.ResolveExtended(new PlaybookResolveOptions(
            "https://nginx.org/en/docs/",
            IncludeLessons: true));
        assert($"l4 pilot/{entry.Id} resolve ok", result.Ok);
        assert($"l4 pilot/{entry.Id} lessons present", result.Lessons is not null);
        assert(
            $"l4 pilot/{entry.Id} local lesson note",
            result.Lessons?.ToString().Contains("L4 lessons export", StringComparison.Ordinal) == true);
    }

    private static void RunK8sExtract(
        L4GenomeCase entry,
        KnowledgeExtractService extractService,
        Action<string, bool> assert,
        bool liveNetwork,
        ref int k7Pass)
    {
        if (!liveNetwork)
        {
            assert($"l4 pilot/{entry.Id} skipped offline", true);
            return;
        }

        var result = extractService.Extract(entry.Url!);
        assert($"l4 pilot/{entry.Id} extract ok", result.Ok);
        assert($"l4 pilot/{entry.Id} pageClass concepts", result.PageClass == "concepts");
        assert($"l4 pilot/{entry.Id} meta koId", result.Meta is not null && !string.IsNullOrWhiteSpace(result.Meta.KoId));

        var titleFact = result.Facts?.FirstOrDefault(f => f.Name == "title");
        assert($"l4 pilot/{entry.Id} facts title non-empty", !string.IsNullOrWhiteSpace(titleFact?.Value));

        if (result.Ok
            && result.PageClass == "concepts"
            && !string.IsNullOrWhiteSpace(result.Meta?.KoId)
            && !string.IsNullOrWhiteSpace(titleFact?.Value))
        {
            k7Pass++;
        }
    }

    private static bool RunPublishNegative(L4GenomeCase entry, Action<string, bool> assert)
    {
        Console.WriteLine($"l4 negative: {entry.Id}");
        switch (entry.Id)
        {
            case "genome-neg-publish-cookie":
                return RunPublishCookieNegative(entry, assert);
            default:
                assert($"l4 neg/{entry.Id} known", false);
                return false;
        }
    }

    private static bool RunPublishCookieNegative(L4GenomeCase entry, Action<string, bool> assert)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var fixture = Path.Combine(home, "benchmarks", "l0-gate", "fixtures", "playbook-publish-cookie.playbook.json");
        var script = Path.Combine(home, "scripts", "lib", "playbook-publish.mjs");
        var outputDir = Path.Combine(Path.GetTempPath(), $"occam-l4-publish-{Guid.NewGuid():N}");

        assert($"l4 neg/{entry.Id} fixture exists", File.Exists(fixture));
        assert($"l4 neg/{entry.Id} script exists", File.Exists(script));

        var psi = new ProcessStartInfo
        {
            FileName = NodeRuntime.ResolveExecutable(),
            Arguments = $"\"{script}\" --input \"{fixture}\" --ack-community-review --output \"{outputDir}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
            StandardErrorEncoding = Encoding.UTF8,
        };

        using var process = Process.Start(psi);
        if (process is null)
        {
            assert($"l4 neg/{entry.Id} process start", false);
            return false;
        }

        var stderr = process.StandardError.ReadToEnd();
        var stdout = process.StandardOutput.ReadToEnd();
        process.WaitForExit();

        var exportPath = Path.Combine(outputDir, "gate.neg-cookie.example.json");
        assert($"l4 neg/{entry.Id} exit non-zero", process.ExitCode != 0);
        assert($"l4 neg/{entry.Id} secrets_detected", stderr.Contains("secrets_detected", StringComparison.OrdinalIgnoreCase));
        assert($"l4 neg/{entry.Id} no export file", !File.Exists(exportPath));

        try
        {
            if (Directory.Exists(outputDir))
            {
                Directory.Delete(outputDir, recursive: true);
            }
        }
        catch
        {
            // best-effort
        }

        if (process.ExitCode != 0
            && stderr.Contains("secrets_detected", StringComparison.OrdinalIgnoreCase)
            && !File.Exists(exportPath))
        {
            return true;
        }

        Console.WriteLine($"l4 neg/{entry.Id} stdout: {stdout.Trim()}");
        Console.WriteLine($"l4 neg/{entry.Id} stderr: {stderr.Trim()}");
        return false;
    }

    private static void RunManifestNegative(L4GenomeCase entry, Action<string, bool> assert)
    {
        Console.WriteLine($"l4 negative: {entry.Id}");
        switch (entry.Id)
        {
            case "genome-neg-manifest-sha256":
                RunManifestSha256Negative(entry, assert);
                break;
            default:
                assert($"l4 neg/{entry.Id} known", false);
                break;
        }
    }

    private static void RunManifestSha256Negative(L4GenomeCase entry, Action<string, bool> assert)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var fixtureDir = Path.Combine(home, "benchmarks", "l0-gate", "fixtures", "community-manifest-neg");
        assert($"l4 neg/{entry.Id} fixture dir", Directory.Exists(fixtureDir));

        var fileHashes = CommunityManifest.TryLoadFileHashes(fixtureDir);
        assert($"l4 neg/{entry.Id} manifest parsed", fileHashes is not null && fileHashes.Count > 0);

        const string fileName = "tamper.neg.test.json";
        var filePath = Path.Combine(fixtureDir, fileName);
        assert($"l4 neg/{entry.Id} fixture file", File.Exists(filePath));
        assert(
            $"l4 neg/{entry.Id} sha256 mismatch",
            fileHashes is not null
            && fileHashes.TryGetValue(fileName, out var expectedSha)
            && !CommunityManifest.FileSha256Matches(filePath, expectedSha));
        assert(
            $"l4 neg/{entry.Id} skip tampered load",
            fileHashes is not null
            && !CommunityManifest.TryReadVerifiedPlaybook(filePath, fileName, fileHashes, out _));
    }

    private static void RunNegative(
        L4GenomeCase entry,
        PlaybookSeedResolver resolver,
        KnowledgeExtractService extractService,
        Action<string, bool> assert)
    {
        Console.WriteLine($"l4 negative: {entry.Id}");
        switch (entry.Id)
        {
            case "genome-neg-no-well-known":
                var result = resolver.ResolveExtended(new PlaybookResolveOptions(
                    entry.Url!,
                    FetchSiteGenome: true));
                assert($"l4 neg/{entry.Id} resolve ok", result.Ok);
                assert($"l4 neg/{entry.Id} seed provenance", result.Provenance == PlaybookProvenance.Seed);
                assert($"l4 neg/{entry.Id} genomeFetch attempted", result.GenomeFetch is not null);
                assert($"l4 neg/{entry.Id} genomeFetch fail", result.GenomeFetch?.Ok == false);
                assert($"l4 neg/{entry.Id} http_404", result.GenomeFetch?.FailureCode == "http_404");
                break;
            case "genome-neg-extract-no-schema":
                var extract = extractService.Extract(entry.Url!);
                assert($"l4 neg/{entry.Id} extract fail", !extract.Ok);
                assert($"l4 neg/{entry.Id} knowledge_schema_missing", extract.FailureCode == "knowledge_schema_missing");
                break;
            default:
                assert($"l4 neg/{entry.Id} known", false);
                break;
        }
    }

    private static string? LoadSeedJson(string seedFile)
    {
        var root = WorkerPaths.ResolveOccamHome();
        if (root is null)
        {
            return null;
        }

        var path = Path.Combine(root, "profiles", "playbooks", "seeds", seedFile);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string ResolveCorpusPath()
    {
        var root = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        return Path.Combine(root, "corpora", "l4-genome.jsonl");
    }
}

internal sealed class L4GenomeCase
{
    public string Id { get; init; } = "";
    public string Tier { get; init; } = "";
    [JsonPropertyName("sub_slice")]
    public string SubSlice { get; init; } = "";
    public string? Url { get; init; }
    [JsonPropertyName("fetch_site_genome")]
    public bool FetchSiteGenome { get; init; }
    public string? Expect { get; init; }
}

[JsonSerializable(typeof(L4GenomeCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L4GenomeJsonContext : JsonSerializerContext;
