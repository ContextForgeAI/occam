using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Telemetry;

public enum OccamLogEventKind
{
    StartupBanner,
    TranscodeReport,
    TranscodeFailure,
    BrowserPool,
    StageBreakdown,
}

public sealed record OccamLogEvent(
    OccamLogEventKind Kind,
    WorkerPaths? Paths = null,
    TranscodeResult? Transcode = null,
    string? OutputText = null,
    string? PoolPhase = null,
    int PoolSlotId = 0,
    int PoolPort = 0,
    int PoolWaitMs = 0,
    int PoolPendingDepth = 0,
    bool PoolOk = true,
    int PoolExtractMs = 0,
    string? StageBackend = null,
    int StagePreflightMs = 0,
    int StageRouteMs = 0,
    int StagePostProcessMs = 0,
    int StageCompileMs = 0);
