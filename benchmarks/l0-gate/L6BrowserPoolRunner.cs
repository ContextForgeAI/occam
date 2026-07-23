using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L6BrowserPoolRunner
{
    public static void Run(WorkerPaths paths, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l6 browser pool corpus: {corpusPath}");
        assert("l6 browser pool corpus exists", File.Exists(corpusPath));

        if (!paths.IsConfigured)
        {
            Console.WriteLine("l6 browser pool: workers not configured — skipping live integration");
            assert("l6 browser pool workers skip", true);
            return;
        }

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L6BrowserPoolJsonContext.Default.L6BrowserPoolCase);
            if (entry is null)
            {
                assert("l6 browser pool/json parse", false);
                continue;
            }

            Console.WriteLine($"l6 browser pool: {entry.Id} ({entry.Urls.Length} urls, pool={entry.PoolSize ?? 2})");
            RunLiveCase(entry, assert).GetAwaiter().GetResult();
        }
    }

    private static async Task RunLiveCase(L6BrowserPoolCase entry, Action<string, bool> assert)
    {
        var prevPoolSize = Environment.GetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar);
        var prevBasePort = Environment.GetEnvironmentVariable(BrowserPoolSettings.BasePortVar);
        var prevMaxParallel = Environment.GetEnvironmentVariable("OCCAM_BROWSER_MAX_PARALLEL");
        var poolSize = entry.PoolSize ?? 2;

        Environment.SetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar, poolSize.ToString());
        Environment.SetEnvironmentVariable(BrowserPoolSettings.BasePortVar, "39217");
        Environment.SetEnvironmentVariable("OCCAM_BROWSER_MAX_PARALLEL", poolSize.ToString());
        BrowserPoolManager.ResetSharedForTests();
        BrowserConcurrencyGate.ResetForTests();

        try
        {
            var (_, pipeline, _, _, _) = OccamServiceCollectionExtensions.BuildOccamCore();
            if (!OccamBackendPolicyParser.TryParse(entry.Backend, out var policy))
            {
                assert($"l6 browser pool/{entry.Id} backend parse", false);
                return;
            }

            var serialStarted = Stopwatch.GetTimestamp();
            var serialResults = new List<bool>();
            foreach (var url in entry.Urls)
            {
                var result = pipeline.Transcode(url, policy, CancellationToken.None);
                serialResults.Add(result.Ok);
            }

            var serialMs = (int)((Stopwatch.GetTimestamp() - serialStarted) * 1000 / Stopwatch.Frequency);
            BrowserPoolManager.ResetSharedForTests();
            BrowserConcurrencyGate.ResetForTests();
            var (_, parallelPipeline, _, _, _) = OccamServiceCollectionExtensions.BuildOccamCore();

            var parallelStarted = Stopwatch.GetTimestamp();
            var tasks = entry.Urls.Select(url => Task.Run(() =>
            {
                var result = parallelPipeline.Transcode(url, policy, CancellationToken.None);
                return result.Ok;
            })).ToArray();
            var parallelResults = await Task.WhenAll(tasks).ConfigureAwait(false);
            var parallelMs = (int)((Stopwatch.GetTimestamp() - parallelStarted) * 1000 / Stopwatch.Frequency);

            if (entry.ExpectOk)
            {
                var succeeded = parallelResults.Count(r => r);
                assert(
                    $"l6 browser pool/{entry.Id} min_succeeded",
                    succeeded >= (entry.MinSucceeded ?? entry.Urls.Length));

                if (entry.Urls.Length >= 2 && parallelResults.All(r => r) && serialResults.All(r => r))
                {
                    assert(
                        $"l6 browser pool/{entry.Id} parallel faster than serial",
                        parallelMs <= serialMs + 5_000);
                }
            }
        }
        finally
        {
            BrowserPoolManager.ResetSharedForTests();
            Environment.SetEnvironmentVariable(BrowserPoolSettings.PoolSizeVar, prevPoolSize);
            Environment.SetEnvironmentVariable(BrowserPoolSettings.BasePortVar, prevBasePort);
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_MAX_PARALLEL", prevMaxParallel);
            BrowserConcurrencyGate.ResetForTests();
        }
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        return Path.Combine(home, "corpora", "l6-browser-pool.jsonl");
    }
}

internal sealed class L6BrowserPoolCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("urls")]
    public string[] Urls { get; init; } = [];

    [JsonPropertyName("backend")]
    public string Backend { get; init; } = "browser";

    [JsonPropertyName("expect_ok")]
    public bool ExpectOk { get; init; } = true;

    [JsonPropertyName("min_succeeded")]
    public int? MinSucceeded { get; init; }

    [JsonPropertyName("timeout_seconds")]
    public int? TimeoutSeconds { get; init; }

    [JsonPropertyName("pool_size")]
    public int? PoolSize { get; init; }
}

[JsonSerializable(typeof(L6BrowserPoolCase))]
internal partial class L6BrowserPoolJsonContext : JsonSerializerContext;
