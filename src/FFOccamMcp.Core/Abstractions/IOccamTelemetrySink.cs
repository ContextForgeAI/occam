using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Abstractions;

public interface IOccamTelemetrySink
{
    void OnTranscodeCompleted(TranscodeContext ctx, TranscodeOutcome outcome);
    void OnTranscodeFailed(TranscodeContext ctx, TranscodeOutcome outcome);
    void OnBrowserPoolAcquired(BrowserPoolSlot slot, int waitMs, int pendingDepth);
    void OnBrowserPoolReleased(BrowserPoolSlot slot, bool ok, int extractMs);
}
