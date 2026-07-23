using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Batch;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Workers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace OccamMcp.L0Gate;

internal static class L5BatchRunner
{
    public static void Run(WorkerPaths paths, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l5 batch corpus: {corpusPath}");
        assert("l5 batch corpus exists", File.Exists(corpusPath));

        if (!paths.IsConfigured)
        {
            Console.WriteLine("l5 batch: workers not configured — skipping live integration");
            assert("l5 batch workers skip", true);
            return;
        }

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L5BatchJsonContext.Default.L5BatchCase);
            if (entry is null)
            {
                assert("l5 batch/json parse", false);
                continue;
            }

            Console.WriteLine($"l5 batch: {entry.Id} ({entry.Urls.Length} urls)");
            RunLiveCase(entry, assert).GetAwaiter().GetResult();
        }
    }

    private static async Task RunLiveCase(L5BatchCase entry, Action<string, bool> assert)
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"occam-l5-batch-{Guid.NewGuid():N}.db");
        var prevDb = Environment.GetEnvironmentVariable("OCCAM_BATCH_DB_PATH");
        Environment.SetEnvironmentVariable("OCCAM_BATCH_DB_PATH", dbPath);
        Environment.SetEnvironmentVariable("OCCAM_BATCH_PARALLEL", "2");

        var services = new ServiceCollection();
        services.AddOccamBatch();
        using var provider = services.BuildServiceProvider();
        var store = provider.GetRequiredService<IBatchJobStore>();
        store.Initialize();

        var hosted = provider.GetServices<IHostedService>().ToArray();
        foreach (var service in hosted)
        {
            await service.StartAsync(CancellationToken.None).ConfigureAwait(false);
        }

        try
        {
            var batch = provider.GetRequiredService<IBatchJobService>();
            var submit = new BatchSubmitRequest
            {
                Urls = entry.Urls,
                BackendPolicy = entry.Backend,
                PlaybookPolicy = "off",
                FitMarkdown = true,
            };
            var (accepted, submitError) = batch.Submit(submit);
            assert($"l5 batch/{entry.Id} submit", submitError is null && accepted is not null);
            if (accepted is null)
            {
                return;
            }

            var timeout = TimeSpan.FromSeconds(entry.TimeoutSeconds ?? 90);
            var deadline = DateTimeOffset.UtcNow + timeout;
            BatchStatusResponse? status = null;
            while (DateTimeOffset.UtcNow < deadline)
            {
                var (current, _) = batch.GetStatus(accepted.JobId);
                status = current;
                if (current?.State is BatchJobStates.Done or BatchJobStates.Failed)
                {
                    break;
                }

                await Task.Delay(500).ConfigureAwait(false);
            }

            assert($"l5 batch/{entry.Id} terminal state", status?.State == BatchJobStates.Done);
            if (status is null)
            {
                return;
            }

            var (results, _) = batch.GetResults(accepted.JobId, 0, 50);
            assert($"l5 batch/{entry.Id} results present", results?.Items.Length == entry.Urls.Length);

            if (entry.ExpectOk)
            {
                var succeeded = results?.Items.Count(i => i.Ok) ?? 0;
                assert(
                    $"l5 batch/{entry.Id} min_succeeded",
                    succeeded >= (entry.MinSucceeded ?? 1));
            }
        }
        finally
        {
            foreach (var service in hosted)
            {
                await service.StopAsync(CancellationToken.None).ConfigureAwait(false);
            }

            if (store is IDisposable disposable)
            {
                disposable.Dispose();
            }

            Environment.SetEnvironmentVariable("OCCAM_BATCH_DB_PATH", prevDb);
            Environment.SetEnvironmentVariable("OCCAM_BATCH_PARALLEL", null);
            if (File.Exists(dbPath))
            {
                try
                {
                    File.Delete(dbPath);
                }
                catch (IOException)
                {
                    // Temp gate DB — ignore lock after dispose.
                }
            }
        }
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        return Path.Combine(home, "corpora", "l5-batch.jsonl");
    }
}

internal sealed class L5BatchCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("urls")]
    public string[] Urls { get; init; } = [];

    [JsonPropertyName("backend")]
    public string Backend { get; init; } = "http";

    [JsonPropertyName("expect_ok")]
    public bool ExpectOk { get; init; } = true;

    [JsonPropertyName("min_succeeded")]
    public int? MinSucceeded { get; init; }

    [JsonPropertyName("timeout_seconds")]
    public int? TimeoutSeconds { get; init; }
}

[JsonSerializable(typeof(L5BatchCase))]
internal partial class L5BatchJsonContext : JsonSerializerContext;
