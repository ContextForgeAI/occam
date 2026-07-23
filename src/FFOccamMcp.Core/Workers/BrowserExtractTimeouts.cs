using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Workers;

/// <summary>Browser extract timeout resolution — per-page vs daemon queue wait.</summary>
internal static class BrowserExtractTimeouts
{
    private const int DefaultPerExtractMs = 60_000;
    private const int MinPerExtractMs = 15_000;
    private const int MaxPerExtractMs = 180_000;
    internal const int MaxDaemonWaitMs = 900_000;

    // A cold branch-2 chromium provision (~130MB download + first render) does not fit the normal
    // per-page budget; without this grace the provisioning attempt times out and only the retry (with
    // chromium now present) succeeds — losing the browserProvisioned telemetry and wasting the first try.
    private const int ProvisionGraceMs = 240_000;

    /// <param name="provisionExpected">
    /// True when this call will trigger a one-time chromium auto-provision (branch 2); grants download headroom.
    /// </param>
    public static int ResolvePerExtractTimeoutMs(bool provisionExpected = false)
    {
        var baseMs = OccamEnvironment.GetInt("OCCAM_BROWSER_TIMEOUT_MS", DefaultPerExtractMs, MinPerExtractMs, MaxPerExtractMs);
        return provisionExpected ? Math.Max(baseMs, ProvisionGraceMs) : baseMs;
    }

    /// <summary>
    /// Daemon <c>BrowserPool</c> serializes <c>/extract</c>; parallel gate slots may queue behind each other.
    /// </summary>
    public static int ResolveDaemonWaitTimeoutMs(int? perExtractMs = null)
    {
        var per = perExtractMs ?? ResolvePerExtractTimeoutMs();
        var slots = Math.Max(1, BrowserConcurrencyGate.MaxParallel);
        var total = per * slots;
        return Math.Clamp(total, per, MaxDaemonWaitMs);
    }
}
