using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Channels;
using OccamMcp.Core.Workers;

namespace OccamMcp.RamStress;

internal static class Program
{
    private const int DefaultParallelism = 8;
    private const int RamBudgetMb = 250;

    public static async Task<int> Main(string[] args)
    {
        if (!HasFlag(args, "--stress-test") && !HasFlag(args, "--help") && Arg(args, "--corpus") is null)
        {
            PrintUsage();
            return 2;
        }

        if (HasFlag(args, "--help"))
        {
            PrintUsage();
            return 0;
        }

        var repoRoot = WorkerPaths.ResolveOccamHome()
            ?? throw new InvalidOperationException("Cannot resolve OCCAM_HOME.");
        var ramSmoke = HasFlag(args, "--ram-smoke");
        var corpusPath = Arg(args, "--corpus")
            ?? Path.Combine(repoRoot, "corpora", "l0-ram-stress.jsonl");
        var runId = Arg(args, "--run-id") ?? DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var parallelism = Math.Clamp(
            int.Parse(Arg(args, "--parallel") ?? (ramSmoke ? "2" : DefaultParallelism.ToString())),
            1,
            16);
        var loops = int.Parse(Arg(args, "--loops") ?? (ramSmoke ? "1" : "1"));
        var ramBudget = int.Parse(Arg(args, "--ram-budget-mb") ?? RamBudgetMb.ToString());
        var forceBrowser = !HasFlag(args, "--no-force-browser");
        if (HasFlag(args, "--force-browser"))
        {
            forceBrowser = true;
        }

        Environment.SetEnvironmentVariable("WT_BROWSER_MAX_PARALLEL", parallelism.ToString());

        var paths = WorkerPaths.Resolve();
        if (!paths.IsConfigured)
        {
            Console.Error.WriteLine("FAIL workers not configured — run scripts/occam-doctor.ps1");
            return 3;
        }

        var seeds = LoadCorpus(corpusPath);
        if (ramSmoke && seeds.Count > 3)
        {
            seeds = seeds.Take(3).ToList();
        }

        if (seeds.Count == 0)
        {
            Console.Error.WriteLine($"FAIL empty corpus: {corpusPath}");
            return 3;
        }

        var outDir = Path.Combine(repoRoot, "artifacts", "ram-stress", runId);
        Directory.CreateDirectory(outDir);
        var csvPath = Path.Combine(outDir, "memory-trace.csv");
        var summaryPath = Path.Combine(outDir, "summary.json");

        Console.WriteLine("=== FFOccam L0 RAM stress / leak hunt ===");
        Console.WriteLine(
            $"corpus={seeds.Count} parallel={parallelism} loops={loops} budget={ramBudget}MB " +
            $"force_browser={forceBrowser} ram_smoke={ramSmoke}");
        Console.WriteLine($"OUT={outDir}");

        NativeMemoryCompactor.EnsureConfigured();
        var analyzer = new MemoryTrendAnalyzer(ramBudget);
        var pageCounter = 0;
        var recordLock = new object();
        var runOptions = new StressRunOptions(forceBrowser);
        using var shutdownCts = new CancellationTokenSource();
        Console.CancelKeyPress += (_, e) =>
        {
            e.Cancel = true;
            shutdownCts.Cancel();
        };

        try
        {
            for (var loop = 1; loop <= loops; loop++)
            {
                shutdownCts.Token.ThrowIfCancellationRequested();
                Console.WriteLine($"--- loop {loop}/{loops} ---");
                var channel = Channel.CreateBounded<StressSeed>(new BoundedChannelOptions(seeds.Count)
                {
                    FullMode = BoundedChannelFullMode.Wait,
                    SingleWriter = true,
                    SingleReader = false,
                });

                foreach (var seed in seeds)
                {
                    await channel.Writer.WriteAsync(seed);
                }

                channel.Writer.Complete();

                var workerTasks = new Task[parallelism];
                for (var w = 0; w < parallelism; w++)
                {
                    var ctx = new L0RamStressWorkerContext(w + 1);
                    workerTasks[w] = WorkerLoopAsync(
                        channel.Reader,
                        ctx,
                        runOptions,
                        analyzer,
                        recordLock,
                        () => Interlocked.Increment(ref pageCounter),
                        shutdownCts.Token);
                }

                await Task.WhenAll(workerTasks);

                var checkpoint = analyzer.EvaluateCheckpoints();
                if (checkpoint is { LeakDetected: true } leak)
                {
                    await WriteSummaryAsync(summaryPath, analyzer, leak, loop, false, ramBudget, forceBrowser, parallelism, ramSmoke);
                    Console.Error.WriteLine($"CRITICAL {leak.Message}");
                    return 2;
                }
            }
        }
        catch (LeakAbortException ex)
        {
            await WriteSummaryAsync(summaryPath, analyzer, ex.Verdict, loops, false, ramBudget, forceBrowser, parallelism, ramSmoke);
            await MemoryCsvWriter.WriteAsync(csvPath, analyzer.Samples);
            Console.Error.WriteLine($"CRITICAL {ex.Verdict.Message}");
            return ex.Verdict.Kind == LeakKind.RamBudget ? 1 : 2;
        }
        catch (OperationCanceledException)
        {
            Console.Error.WriteLine("CANCELLED — shutdown requested");
            await MemoryCsvWriter.WriteAsync(csvPath, analyzer.Samples);
            await WriteSummaryAsync(summaryPath, analyzer, null, loops, false, ramBudget, forceBrowser, parallelism, ramSmoke);
            return 130;
        }
        finally
        {
            BrowserDaemonHost.Stop();
        }

        await MemoryCsvWriter.WriteAsync(csvPath, analyzer.Samples);
        await WriteSummaryAsync(summaryPath, analyzer, null, loops, true, ramBudget, forceBrowser, parallelism, ramSmoke);

        var peakWs = analyzer.Samples.Count == 0 ? 0 : analyzer.Samples.Max(s => s.WorkingSetMb);
        var peakNode = analyzer.Samples.Count == 0 ? 0 : analyzer.Samples.Max(s => s.NodeRssMb);
        var peakBrowser = analyzer.Samples.Count == 0 ? 0 : analyzer.Samples.Max(s => s.BrowserProcessCount);

        Console.WriteLine("=== SUMMARY ===");
        Console.WriteLine($"pages={analyzer.Samples.Count} peak_ws={peakWs:F1}MB peak_node={peakNode:F1}MB peak_browsers={peakBrowser}");
        Console.WriteLine($"RAM_STRESS_GATE {(peakWs <= ramBudget ? "PASS" : "FAIL")} (budget {ramBudget}MB)");
        Console.WriteLine($"csv={csvPath}");
        Console.WriteLine($"summary={summaryPath}");

        return peakWs <= ramBudget ? 0 : 1;
    }

    private static async Task WorkerLoopAsync(
        ChannelReader<StressSeed> reader,
        L0RamStressWorkerContext ctx,
        StressRunOptions options,
        MemoryTrendAnalyzer analyzer,
        object recordLock,
        Func<int> nextPageIndex,
        CancellationToken cancellationToken)
    {
        while (await reader.WaitToReadAsync(cancellationToken))
        {
            while (reader.TryRead(out var seed))
            {
                cancellationToken.ThrowIfCancellationRequested();
                try
                {
                    await L0RamStressWorkload.RunSeedAsync(
                        ctx,
                        options,
                        seed,
                        analyzer,
                        recordLock,
                        nextPageIndex,
                        cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (LeakAbortException)
                {
                    throw;
                }
                catch (Exception ex)
                {
                    Console.Error.WriteLine($"WARN W{ctx.WorkerId} seed {seed.Id} failed: {ex.Message}");
                }
            }
        }
    }

    private static List<StressSeed> LoadCorpus(string path)
    {
        var rows = new List<StressSeed>();
        foreach (var line in File.ReadAllLines(path))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var node = JsonNode.Parse(line)?.AsObject();
            if (node is null)
            {
                continue;
            }

            var url = node["url"]?.GetValue<string>();
            if (string.IsNullOrWhiteSpace(url))
            {
                continue;
            }

            rows.Add(new StressSeed(
                node["id"]?.GetValue<string>() ?? url,
                url));
        }

        return rows;
    }

    private static async Task WriteSummaryAsync(
        string path,
        MemoryTrendAnalyzer analyzer,
        LeakVerdict? leak,
        int loops,
        bool pass,
        int ramBudgetMb,
        bool forceBrowser,
        int parallelism,
        bool ramSmoke)
    {
        var samples = analyzer.Samples;
        JsonObject? leakNode = null;
        if (leak is { } detectedLeak)
        {
            leakNode = new JsonObject
            {
                ["kind"] = detectedLeak.Kind.ToString(),
                ["checkpoint_page"] = detectedLeak.CheckpointPage,
                ["message"] = detectedLeak.Message,
            };
        }

        var payload = new JsonObject
        {
            ["schema_version"] = "l0-1.0",
            ["captured_at"] = DateTimeOffset.UtcNow.ToString("O"),
            ["ram_smoke"] = ramSmoke,
            ["loops"] = loops,
            ["pages_sampled"] = samples.Count,
            ["parallelism"] = parallelism,
            ["force_browser"] = forceBrowser,
            ["ram_budget_mb"] = ramBudgetMb,
            ["peak_working_set_mb"] = samples.Count == 0 ? 0 : samples.Max(s => s.WorkingSetMb),
            ["peak_managed_mb"] = samples.Count == 0 ? 0 : samples.Max(s => s.ManagedMb),
            ["peak_node_rss_mb"] = samples.Count == 0 ? 0 : samples.Max(s => s.NodeRssMb),
            ["peak_browser_processes"] = samples.Count == 0 ? 0 : samples.Max(s => s.BrowserProcessCount),
            ["gate_pass"] = pass && leak is null,
            ["leak"] = leakNode,
        };

        await File.WriteAllTextAsync(path, payload.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void PrintUsage()
    {
        Console.WriteLine("""
            FFOccam L0 RAM stress / leak hunter

            Usage:
              dotnet run --project benchmarks/l0-ram-stress -- --stress-test
              powershell -File scripts/run-l0-ram-stress.ps1 -StressTest

            Flags:
              --stress-test          required entry flag
              --ram-smoke            short run: 3 URLs, parallel=2
              --force-browser        force browser backend (default ON)
              --no-force-browser     use http_then_browser
              --corpus <path>        default corpora/l0-ram-stress.jsonl
              --parallel <n>         default 8
              --loops <n>            repeat corpus (default 1)
              --run-id <id>          output folder name
              --ram-budget-mb <n>    hard fail threshold (default 250)

            Exit codes:
              0 = gate pass
              1 = RAM budget exceeded
              2 = leak trend detected
              3 = workers missing
            """);
    }

    private static string? Arg(string[] args, string name)
    {
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i].Equals(name, StringComparison.OrdinalIgnoreCase) && i + 1 < args.Length)
            {
                return args[i + 1];
            }
        }

        return null;
    }

    private static bool HasFlag(string[] args, string name) =>
        args.Any(a => a.Equals(name, StringComparison.OrdinalIgnoreCase));
}
