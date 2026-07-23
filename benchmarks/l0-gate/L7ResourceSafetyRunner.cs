using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L7ResourceSafetyRunner
{
    public static void Run(TranscodePipeline pipeline, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l7 resource safety corpus: {corpusPath}");
        assert("l7 resource safety corpus exists", File.Exists(corpusPath));

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L7ResourceSafetyJsonContext.Default.L7ResourceSafetyCase);
            if (entry is null)
            {
                assert("l7 resource safety/json parse", false);
                continue;
            }

            Console.WriteLine($"l7 resource safety: {entry.Id} mode={entry.Mode}");
            switch (entry.Mode?.Trim().ToLowerInvariant())
            {
                case "worker":
                    RunWorkerCase(entry, assert).GetAwaiter().GetResult();
                    break;
                case "pipeline":
                    RunPipelineCase(pipeline, entry, assert).GetAwaiter().GetResult();
                    break;
                case "leak_soak":
                    RunLeakSoakCase(pipeline, entry, assert).GetAwaiter().GetResult();
                    break;
                default:
                    assert($"l7 resource safety/{entry.Id} mode", false);
                    break;
            }
        }
    }

    private static async Task RunWorkerCase(L7ResourceSafetyCase entry, Action<string, bool> assert)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var script = Path.Combine(home, "workers", "http-extract", "extract.mjs");
        assert($"l7 resource safety/{entry.Id} script", File.Exists(script));

        var savedCap = Environment.GetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES");
        var savedPrivate = Environment.GetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS");
        Process? fixtureProcess = null;
        try
        {
            var fixture = await StartOversizeFixtureProcess(entry.FixtureBodyBytes ?? 131_072);
            fixtureProcess = fixture.Process;
            assert($"l7 resource safety/{entry.Id} fixture", fixture.Url is not null);
            if (fixture.Url is null)
            {
                return;
            }

            Environment.SetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES", (entry.MaxResponseBytes ?? 65_536).ToString());
            // Allow private URLs for local fixture testing
            Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", "1");

            var psi = new ProcessStartInfo
            {
                FileName = NodeRuntime.ResolveExecutable(),
                Arguments = NodeLaunchArguments.Build(browser: false, $"\"{script}\" \"{fixture.Url}\""),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            using var process = WorkerProcessGroup.Start(psi);
            assert($"l7 resource safety/{entry.Id} started", process is not null);
            if (process is null)
            {
                return;
            }

            var capture = GateSyncBridge.Run(process, 45_000);
            var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(capture.StdOut);
            assert($"l7 resource safety/{entry.Id} json", jsonLine is not null);
            if (jsonLine is null)
            {
                return;
            }

            var payload = JsonSerializer.Deserialize(jsonLine, WorkerExtractJsonContext.Default.WorkerExtractResponse);
            assert($"l7 resource safety/{entry.Id} ok={entry.ExpectOk}", payload?.Ok == entry.ExpectOk);
            if (entry.ExpectOk == false && !string.IsNullOrWhiteSpace(entry.FailureKind))
            {
                assert(
                    $"l7 resource safety/{entry.Id} failure",
                    string.Equals(payload?.Failure, entry.FailureKind, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            if (fixtureProcess is { HasExited: false })
            {
                try
                {
                    fixtureProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort
                }
            }

            RestoreCap(savedCap);
            if (string.IsNullOrWhiteSpace(savedPrivate))
            {
                Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", savedPrivate);
            }
        }
    }

    private static async Task RunPipelineCase(TranscodePipeline pipeline, L7ResourceSafetyCase entry, Action<string, bool> assert)
    {
        if (!OccamBackendPolicyParser.TryParse(entry.BackendPolicy ?? "http", out var policy))
        {
            assert($"l7 resource safety/{entry.Id} backend", false);
            return;
        }

        var savedCap = Environment.GetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES");
        var savedHttpDaemon = Environment.GetEnvironmentVariable("OCCAM_HTTP_DAEMON");
        var savedPrivate = Environment.GetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS");
        Process? fixtureProcess = null;
        try
        {
            var fixture = await StartOversizeFixtureProcess(entry.FixtureBodyBytes ?? 131_072);
            fixtureProcess = fixture.Process;
            assert($"l7 resource safety/{entry.Id} fixture", fixture.Url is not null);
            if (fixture.Url is null)
            {
                return;
            }

            Environment.SetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES", (entry.MaxResponseBytes ?? 65_536).ToString());
            Environment.SetEnvironmentVariable("OCCAM_HTTP_DAEMON", "0");
            Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", "1");
            HttpDaemonHost.Stop();

            var result = pipeline.Transcode(fixture.Url, policy, new OccamTranscodeOptions(), CancellationToken.None);
            assert($"l7 resource safety/{entry.Id} ok={entry.ExpectOk}", result.Ok == entry.ExpectOk);
            if (entry.ExpectOk == false && !string.IsNullOrWhiteSpace(entry.FailureKind))
            {
                assert(
                    $"l7 resource safety/{entry.Id} failure",
                    string.Equals(result.FailureCode, entry.FailureKind, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            if (fixtureProcess is { HasExited: false })
            {
                try
                {
                    fixtureProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort
                }
            }

            RestoreCap(savedCap);
            if (string.IsNullOrWhiteSpace(savedHttpDaemon))
            {
                Environment.SetEnvironmentVariable("OCCAM_HTTP_DAEMON", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("OCCAM_HTTP_DAEMON", savedHttpDaemon);
            }

            if (string.IsNullOrWhiteSpace(savedPrivate))
            {
                Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", savedPrivate);
            }
        }
    }

    // Offline leak guard: hammer the in-process pipeline against a local fixture K times and assert
    // the managed heap doesn't climb after a forced GC. This is the gate-side, deterministic analog
    // of scripts/bench/leak-probe.mjs (which measures native RSS + process count on the live host —
    // that one needs internet + a sampler, so it stays a manual/scheduled tool, not a gate level).
    private static async Task RunLeakSoakCase(TranscodePipeline pipeline, L7ResourceSafetyCase entry, Action<string, bool> assert)
    {
        if (!OccamBackendPolicyParser.TryParse(entry.BackendPolicy ?? "http", out var policy))
        {
            assert($"l7 resource safety/{entry.Id} backend", false);
            return;
        }

        var iters = entry.Iters ?? 12;
        var maxGrowthMb = entry.MaxManagedGrowthMb ?? 16.0;
        var savedCap = Environment.GetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES");
        var savedHttpDaemon = Environment.GetEnvironmentVariable("OCCAM_HTTP_DAEMON");
        var savedPrivate = Environment.GetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS");
        Process? fixtureProcess = null;
        try
        {
            var fixture = await StartOversizeFixtureProcess(entry.FixtureBodyBytes ?? 4_096);
            fixtureProcess = fixture.Process;
            assert($"l7 resource safety/{entry.Id} fixture", fixture.Url is not null);
            if (fixture.Url is null)
            {
                return;
            }

            Environment.SetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES", (entry.MaxResponseBytes ?? 8_388_608).ToString());
            Environment.SetEnvironmentVariable("OCCAM_HTTP_DAEMON", "0");
            Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", "1");
            HttpDaemonHost.Stop();

            // Warmup so the baseline is steady-state (first call allocates one-time caches/JIT).
            pipeline.Transcode(fixture.Url, policy, new OccamTranscodeOptions(), CancellationToken.None);
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var baseline = GC.GetTotalMemory(forceFullCollection: true);

            for (var i = 0; i < iters; i++)
            {
                pipeline.Transcode(fixture.Url, policy, new OccamTranscodeOptions(), CancellationToken.None);
            }

            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            var after = GC.GetTotalMemory(forceFullCollection: true);
            var growthMb = (after - baseline) / (1024.0 * 1024.0);
            Console.WriteLine(
                $"l7 leak soak: iters={iters} managed baseline={baseline / 1048576.0:F1}MB after={after / 1048576.0:F1}MB growth={growthMb:F1}MB (max {maxGrowthMb}MB)");
            assert(
                $"l7 resource safety/{entry.Id} managed heap stable (<{maxGrowthMb}MB over {iters} iters)",
                growthMb < maxGrowthMb);
        }
        finally
        {
            if (fixtureProcess is { HasExited: false })
            {
                try
                {
                    fixtureProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort
                }
            }

            RestoreCap(savedCap);
            Environment.SetEnvironmentVariable("OCCAM_HTTP_DAEMON", string.IsNullOrWhiteSpace(savedHttpDaemon) ? null : savedHttpDaemon);
            Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", string.IsNullOrWhiteSpace(savedPrivate) ? null : savedPrivate);
        }
    }

    private static async Task<(Process? Process, string? Url)> StartOversizeFixtureProcess(int bodyBytes)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var fixture = Path.Combine(home, "benchmarks", "l0-gate", "fixtures", "oversize-http-fixture-launch.mjs");
        if (!File.Exists(fixture))
        {
            return (null, null);
        }

        var psi = new ProcessStartInfo
        {
            FileName = NodeRuntime.ResolveExecutable(),
            Arguments = $"\"{fixture}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };
        psi.Environment["OVERSIZE_FIXTURE_BYTES"] = bodyBytes.ToString();

        var process = Process.Start(psi);
        if (process is null)
        {
            return (null, null);
        }

        var deadline = Environment.TickCount64 + 8_000;
        while (Environment.TickCount64 < deadline && !process.HasExited)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    return (process, urlProp.GetString());
                }
            }
            catch
            {
                // wait for JSON line
            }
        }

        return (process, null);
    }

    private static void RestoreCap(string? saved)
    {
        if (string.IsNullOrWhiteSpace(saved))
        {
            Environment.SetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES", null);
        }
        else
        {
            Environment.SetEnvironmentVariable("OCCAM_MAX_RESPONSE_BYTES", saved);
        }
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var path = Path.Combine(home, "corpora", "l7-resource-safety.jsonl");
            if (File.Exists(path))
            {
                return path;
            }
        }

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l7-resource-safety.jsonl");
        if (File.Exists(cwd))
        {
            return cwd;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "corpora", "l7-resource-safety.jsonl"));
    }
}

internal sealed class L7ResourceSafetyCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("backend_policy")]
    public string? BackendPolicy { get; init; }

    [JsonPropertyName("expect_ok")]
    public bool? ExpectOk { get; init; }

    [JsonPropertyName("failure_kind")]
    public string? FailureKind { get; init; }

    [JsonPropertyName("max_response_bytes")]
    public int? MaxResponseBytes { get; init; }

    [JsonPropertyName("fixture_body_bytes")]
    public int? FixtureBodyBytes { get; init; }

    [JsonPropertyName("iters")]
    public int? Iters { get; init; }

    [JsonPropertyName("max_managed_growth_mb")]
    public double? MaxManagedGrowthMb { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }
}

[JsonSerializable(typeof(L7ResourceSafetyCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L7ResourceSafetyJsonContext : JsonSerializerContext;
