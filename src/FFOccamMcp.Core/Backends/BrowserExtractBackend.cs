using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends;

public sealed class BrowserExtractBackend(
    IBrowserExtractRunner runner,
    WorkerPaths workerPaths,
    OccamMcp.Core.Services.FeatureDiscoveryService featureDiscovery) : IExtractBackend
{
    public string Name => "browser";

    public bool IsReady => workerPaths.IsConfigured;

    public async ValueTask<ExtractRunResult> ExtractAsync(string url, CancellationToken cancellationToken)
    {
        var features = OccamMcp.Core.Routing.OccamFeaturesScope.ActiveFeatures;
        var options = new ExtractOptions(
            Url: url,
            // Same session-profile plumbing as the HTTP backend: without these the browser renders
            // anonymously and session_profile is silently ignored on the transcode path.
            HeadersFile: OccamMcp.Core.Session.FetchHeadersScope.ActivePath,
            StorageStateFile: OccamMcp.Core.Session.FetchHeadersScope.ActiveStorageStatePath,
            PlaybookOverlayPath: OccamMcp.Core.Playbooks.PlaybookVerifyScope.ActivePath,
            PlaybookOverlayStrict: OccamMcp.Core.Playbooks.PlaybookVerifyScope.ActiveStrict,
            // A3: inline genome for the daemon /extract path (one-shot still reads PlaybookOverlayPath).
            PlaybookOverlayJson: OccamMcp.Core.Playbooks.PlaybookVerifyScope.ActiveJson,
            Features: features);
        // A cold branch-2 provision (chromium absent + occam will install it) needs download headroom, or
        // this attempt times out and only the chromium-now-present retry succeeds — dropping browserProvisioned.
        var provisionExpected = !featureDiscovery.IsBrowserAvailable() && featureDiscovery.WillAutoProvisionBrowser();
        var timeoutMs = BrowserExtractTimeouts.ResolvePerExtractTimeoutMs(provisionExpected);
        return await runner.RunAsync(workerPaths.BrowserExtractScript!, options, timeoutMs, cancellationToken).ConfigureAwait(false);
    }
}
