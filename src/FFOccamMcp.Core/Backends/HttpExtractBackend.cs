using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends;

public sealed class HttpExtractBackend(IHttpExtractRunner runner, WorkerPaths workerPaths) : IExtractBackend
{
    private const int DefaultHttpTimeoutMs = 35_000;

    public string Name => "http";

    public bool IsReady => workerPaths.IsConfigured;

    public async ValueTask<ExtractRunResult> ExtractAsync(string url, CancellationToken cancellationToken)
    {
        var features = OccamMcp.Core.Routing.OccamFeaturesScope.ActiveFeatures;
        var options = new ExtractOptions(
            Url: url,
            // Session-profile headers/storage-state reach the worker via the ambient scope the pipeline
            // opens (using preflight.HeadersScope). Without threading these the whole session layer is dead
            // on the transcode path — the worker fetches anonymously and login/paywall retries never work.
            HeadersFile: OccamMcp.Core.Session.FetchHeadersScope.ActivePath,
            StorageStateFile: OccamMcp.Core.Session.FetchHeadersScope.ActiveStorageStatePath,
            PlaybookOverlayPath: OccamMcp.Core.Playbooks.PlaybookVerifyScope.ActivePath,
            PlaybookOverlayStrict: OccamMcp.Core.Playbooks.PlaybookVerifyScope.ActiveStrict,
            Features: features);
        return await runner.RunAsync(workerPaths.HttpExtractScript!, options, DefaultHttpTimeoutMs, cancellationToken).ConfigureAwait(false);
    }
}
