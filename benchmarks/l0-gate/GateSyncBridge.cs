using System.Diagnostics;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Backends;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Watch;
using OccamMcp.Core.Workers;

/// <summary>
/// Sync shims for the gate only.
/// <para>
/// C2 made the extract stack async end-to-end so the digest's parallel fan-out stops pinning thread-pool
/// threads. The gate is a synchronous console harness: one assertion at a time on the main thread, no fan-out
/// and no server to starve, so blocking here is correct — and keeping the ~39 call sites unchanged is what
/// makes them a real regression check of the async paths rather than a rewrite of the tests themselves.
/// </para>
/// <para>Do NOT use these from src/: blocking there is precisely what C2 removed.</para>
/// </summary>
internal static class GateSyncBridge
{
    public static TranscodeOutcome Transcode(
        this TranscodePipeline pipeline, string url, OccamBackendPolicy policy, CancellationToken cancellationToken) =>
        pipeline.TranscodeAsync(url, policy, cancellationToken).GetAwaiter().GetResult();

    public static TranscodeOutcome Transcode(
        this TranscodePipeline pipeline, string url, OccamBackendPolicy policy, OccamTranscodeOptions options, CancellationToken cancellationToken) =>
        pipeline.TranscodeAsync(url, policy, options, cancellationToken).GetAwaiter().GetResult();

    public static TranscodeOutcome Transcode(
        this OccamRouter router, string url, OccamBackendPolicy policy, CancellationToken cancellationToken) =>
        router.TranscodeAsync(url, policy, cancellationToken).GetAwaiter().GetResult();

    public static ExtractRunResult Extract(
        this IExtractBackend backend, string url, CancellationToken cancellationToken) =>
        backend.ExtractAsync(url, cancellationToken).GetAwaiter().GetResult();

    public static DigestAnalysis Digest(
        this DigestService service,
        string? urlsJson,
        int maxUrls = DigestService.MaxUrlsCap,
        int? perUrlMaxTokens = null,
        OccamBackendPolicy backendPolicy = OccamBackendPolicy.HttpThenBrowser,
        string? focusQuery = null,
        bool fitMarkdown = true,
        bool includeCombined = true,
        string? sessionProfile = null,
        string? sourceUrl = null,
        int? maxLinks = null,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<DigestUrlEntry>? entries = null;
        if (!string.IsNullOrWhiteSpace(urlsJson))
        {
            if (!DigestUrlParser.TryParse(urlsJson, out var parsed, out var parseError))
            {
                return DigestAnalysis.CreateFailed("invalid_urls", parseError ?? "URL list parse failed.");
            }

            entries = parsed;
        }

        return service.DigestAsync(entries, maxUrls, perUrlMaxTokens, backendPolicy, focusQuery, fitMarkdown,
            includeCombined, sessionProfile, sourceUrl, maxLinks, ifNoneMatch, cancellationToken)
            .GetAwaiter().GetResult();
    }

    /// <summary>Unwraps an async tool handler for the sync gate harness.</summary>
    public static string Sync(this Task<string> task) => task.GetAwaiter().GetResult();

    public static (OccamWatchSuccessResponse? Success, OccamWatchFailureInfo? Failure) Watch(
        this WatchService service, string url, OccamBackendPolicy policy, OccamTranscodeOptions options,
        bool reset, bool includeDiff, bool includeHistory, CancellationToken cancellationToken) =>
        service.WatchAsync(url, policy, options, reset, includeDiff, includeHistory, cancellationToken)
            .GetAwaiter().GetResult();

    public static PlaybookSaveResult Save(this PlaybookSaveService service, PlaybookSaveRequest request) =>
        service.SaveAsync(request).GetAwaiter().GetResult();

    public static NodeWorkerOutputCapture.CaptureResult Run(Process process, int timeoutMs, CancellationToken cancellationToken = default) =>
        NodeWorkerOutputCapture.RunAsync(process, timeoutMs, cancellationToken).GetAwaiter().GetResult();
}
