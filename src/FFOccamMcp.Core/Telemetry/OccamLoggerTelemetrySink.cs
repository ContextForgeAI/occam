using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Telemetry;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Telemetry;

public sealed class OccamLoggerTelemetrySink : IOccamTelemetrySink
{
    public void OnTranscodeCompleted(TranscodeContext ctx, TranscodeOutcome outcome)
    {
        OccamLogger.TryWriteTranscodeReport(
            new TranscodeResult(
                true,
                ctx.Url,
                string.Empty,
                0,
                outcome.LatencyMs,
                outcome.Backend ?? "http"),
            outcome.Markdown ?? string.Empty);
    }

    public void OnTranscodeFailed(TranscodeContext ctx, TranscodeOutcome outcome)
    {
        OccamLogger.TryWriteTranscodeFailure(
            new TranscodeResult(
                false,
                ctx.Url,
                outcome.FailureCode ?? "failed",
                0,
                outcome.LatencyMs,
                outcome.Backend ?? string.Empty));
    }

    public void OnBrowserPoolAcquired(BrowserPoolSlot slot, int waitMs, int pendingDepth)
    {
        OccamLogger.TryWriteBrowserPoolEvent(
            "acquired",
            slot.SlotId,
            slot.Port,
            waitMs,
            pendingDepth,
            ok: true,
            extractMs: 0);
    }

    public void OnBrowserPoolReleased(BrowserPoolSlot slot, bool ok, int extractMs)
    {
        OccamLogger.TryWriteBrowserPoolEvent(
            "released",
            slot.SlotId,
            slot.Port,
            waitMs: 0,
            pendingDepth: 0,
            ok,
            extractMs);
    }
}
