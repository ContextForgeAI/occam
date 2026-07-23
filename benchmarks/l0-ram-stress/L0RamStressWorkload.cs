using OccamMcp.Core.Composition;
using OccamMcp.Core.Routing;

namespace OccamMcp.RamStress;

internal sealed class L0RamStressWorkerContext(int workerId)
{
    public int WorkerId { get; } = workerId;

    public TranscodePipeline Pipeline { get; } =
        OccamServiceCollectionExtensions.BuildOccamCore().Pipeline;
}

internal readonly record struct StressRunOptions(bool ForceBrowser);

internal sealed record StressSeed(string Id, string Url);

internal static class L0RamStressWorkload
{
    public static async Task RunSeedAsync(
        L0RamStressWorkerContext ctx,
        StressRunOptions options,
        StressSeed seed,
        MemoryTrendAnalyzer analyzer,
        object recordLock,
        Func<int> nextPageIndex,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var tag = $"W{ctx.WorkerId}";
        var policy = options.ForceBrowser
            ? OccamBackendPolicy.Browser
            : OccamBackendPolicy.HttpThenBrowser;

        // Pipeline API is async-only (TranscodeAsync). Await in-place so each worker still
        // processes one seed to completion before the next — same sequencing as the old sync call.
        var outcome = await ctx.Pipeline
            .TranscodeAsync(seed.Url, policy, cancellationToken)
            .ConfigureAwait(false);
        RecordPage(
            analyzer,
            recordLock,
            nextPageIndex,
            seed.Url,
            $"{tag} transcode:{policy}:{(outcome.Ok ? "ok" : outcome.FailureCode ?? "fail")}");

        if (!outcome.Ok)
        {
            Console.Error.WriteLine($"WARN W{ctx.WorkerId} {seed.Id}: {outcome.FailureCode} — {outcome.Message}");
        }
    }

    private static void RecordPage(
        MemoryTrendAnalyzer analyzer,
        object recordLock,
        Func<int> nextPageIndex,
        string url,
        string phase)
    {
        var pageIndex = nextPageIndex();
        var snapshot = MemorySnapshot.Capture(pageIndex, url, phase);
        lock (recordLock)
        {
            analyzer.Add(snapshot);
            Console.WriteLine(snapshot.ToConsoleLine());
        }
    }
}
