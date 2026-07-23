using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Telemetry;

/// <summary>
/// SI-10: a decorating telemetry sink that feeds the <see cref="FailureAtlasStore"/> per-host outcome
/// aggregation, then delegates to the real inner sink (so logging is unchanged). Registered only when
/// the failure atlas is enabled (<c>OCCAM_ATLAS_MCP=1</c>); by default the plain logger sink is used and
/// there is no aggregation cost.
/// </summary>
public sealed class FailureAtlasSink(IOccamTelemetrySink inner, FailureAtlasStore store) : IOccamTelemetrySink
{
    public void OnTranscodeCompleted(TranscodeContext ctx, TranscodeOutcome outcome)
    {
        store.RecordSuccess(ctx.Url);
        inner.OnTranscodeCompleted(ctx, outcome);
    }

    public void OnTranscodeFailed(TranscodeContext ctx, TranscodeOutcome outcome)
    {
        store.RecordFailure(ctx.Url, outcome.FailureCode);
        inner.OnTranscodeFailed(ctx, outcome);
    }

    public void OnBrowserPoolAcquired(BrowserPoolSlot slot, int waitMs, int pendingDepth) =>
        inner.OnBrowserPoolAcquired(slot, waitMs, pendingDepth);

    public void OnBrowserPoolReleased(BrowserPoolSlot slot, bool ok, int extractMs) =>
        inner.OnBrowserPoolReleased(slot, ok, extractMs);
}
