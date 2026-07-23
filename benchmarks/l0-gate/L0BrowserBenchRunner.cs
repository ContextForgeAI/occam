using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Composition;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal sealed record BrowserBenchRow(
    string Phase,
    long WallMs,
    int WorkerLatencyMs,
    bool Ok,
    string? FailureCode,
    bool Daemon);

[JsonSerializable(typeof(BrowserBenchSummary))]
internal partial class BrowserBenchJsonContext : JsonSerializerContext;

internal sealed class BrowserBenchSummary
{
    public string Url { get; init; } = "";
    public string Timestamp { get; init; } = "";
    public BrowserBenchRowDto[] Rows { get; init; } = [];
    public long? ColdDaemonWallMs { get; init; }
    public long? WarmDaemonWallMsAvg { get; init; }
    public long? SpawnOneShotWallMs { get; init; }
    public double? DaemonWarmSpeedup { get; init; }
}

internal sealed class BrowserBenchRowDto
{
    public string Phase { get; init; } = "";
    public long WallMs { get; init; }
    public int WorkerLatencyMs { get; init; }
    public bool Ok { get; init; }
    public string? FailureCode { get; init; }
    public bool Daemon { get; init; }
}

internal static class L0BrowserBenchRunner
{
    private const string DefaultUrl = "https://nuxt.com/docs/getting-started/introduction";

    public static int Run(TranscodePipeline pipeline, string? url, int warmRounds, bool compareSpawn)
    {
        url = string.IsNullOrWhiteSpace(url) ? DefaultUrl : url;
        warmRounds = Math.Clamp(warmRounds, 1, 10);

        var rows = new List<BrowserBenchRow>();
        Console.WriteLine($"browser bench: {url}");
        Console.WriteLine($"warm rounds: {warmRounds}, compare spawn: {compareSpawn}");
        Console.WriteLine();

        BrowserDaemonHost.Stop();
        Thread.Sleep(300);

        rows.Add(RunPhase("cold_daemon", pipeline, url, daemon: true));

        for (var i = 1; i <= warmRounds; i++)
        {
            rows.Add(RunPhase($"warm_daemon_{i}", pipeline, url, daemon: true));
        }

        if (compareSpawn)
        {
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_DAEMON", "0");
            var (_, spawnPipeline, _, _, _) = OccamServiceCollectionExtensions.BuildOccamCore();
            rows.Add(RunPhase("spawn_oneshot", spawnPipeline, url, daemon: false));
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_DAEMON", null);
        }

        PrintTable(rows);

        var cold = rows.FirstOrDefault(r => r.Phase == "cold_daemon");
        var warm = rows.Where(r => r.Phase.StartsWith("warm_daemon_", StringComparison.Ordinal)).ToList();
        var spawn = rows.FirstOrDefault(r => r.Phase == "spawn_oneshot");

        var warmAvg = warm.Count > 0 ? (long)warm.Average(r => r.WallMs) : (long?)null;
        double? speedup = cold is not null && warmAvg is > 0
            ? Math.Round((double)cold.WallMs / warmAvg.Value, 2)
            : null;

        var summary = new BrowserBenchSummary
        {
            Url = url,
            Timestamp = DateTime.UtcNow.ToString("O"),
            Rows = rows.Select(r => new BrowserBenchRowDto
            {
                Phase = r.Phase,
                WallMs = r.WallMs,
                WorkerLatencyMs = r.WorkerLatencyMs,
                Ok = r.Ok,
                FailureCode = r.FailureCode,
                Daemon = r.Daemon,
            }).ToArray(),
            ColdDaemonWallMs = cold?.WallMs,
            WarmDaemonWallMsAvg = warmAvg,
            SpawnOneShotWallMs = spawn?.WallMs,
            DaemonWarmSpeedup = speedup,
        };

        var outDir = WriteSummary(summary);
        Console.WriteLine();
        Console.WriteLine($"BENCH_REPORT: {outDir}");

        if (rows.Any(r => !r.Ok))
        {
            Console.Error.WriteLine("BROWSER_BENCH_FAIL: one or more phases failed");
            return 1;
        }

        if (speedup is > 1.0)
        {
            Console.WriteLine($"BROWSER_BENCH_OK — daemon warm speedup {speedup}x vs cold");
        }
        else
        {
            Console.WriteLine("BROWSER_BENCH_OK");
        }

        return 0;
    }

    private static BrowserBenchRow RunPhase(
        string phase,
        TranscodePipeline pipeline,
        string url,
        bool daemon)
    {
        var sw = Stopwatch.StartNew();
        var outcome = pipeline.Transcode(url, OccamBackendPolicy.Browser, CancellationToken.None);
        sw.Stop();

        var row = new BrowserBenchRow(
            phase,
            sw.ElapsedMilliseconds,
            outcome.LatencyMs,
            outcome.Ok,
            outcome.FailureCode,
            daemon);

        Console.WriteLine(
            $"{phase,-16} wall={row.WallMs,6}ms ok={row.Ok} backend={outcome.Backend ?? "-"} failure={outcome.FailureCode ?? "-"}");

        if (!outcome.Ok)
        {
            Console.Error.WriteLine($"  message: {outcome.Message}");
        }

        return row;
    }

    private static void PrintTable(IReadOnlyList<BrowserBenchRow> rows)
    {
        Console.WriteLine();
        Console.WriteLine("phase            wall_ms  ok  mode");
        foreach (var row in rows)
        {
            var mode = row.Daemon ? "daemon" : "spawn";
            Console.WriteLine($"{row.Phase,-16} {row.WallMs,7} {row.Ok,-4} {mode}");
        }
    }

    private static string WriteSummary(BrowserBenchSummary summary)
    {
        var root = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var stamp = DateTime.UtcNow.ToString("yyyy-MM-dd_HHmmss");
        var dir = Path.Combine(root, "artifacts", "browser-bench", stamp);
        Directory.CreateDirectory(dir);

        var jsonPath = Path.Combine(dir, "summary.json");
        File.WriteAllText(
            jsonPath,
            JsonSerializer.Serialize(summary, BrowserBenchJsonContext.Default.BrowserBenchSummary));

        var latest = Path.Combine(root, "artifacts", "browser-bench", "LATEST.txt");
        Directory.CreateDirectory(Path.GetDirectoryName(latest)!);
        File.WriteAllText(latest, dir);

        return dir;
    }
}
