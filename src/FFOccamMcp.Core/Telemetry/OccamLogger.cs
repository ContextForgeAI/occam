using OccamMcp.Core.Configuration;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Telemetry;

/// <summary>
/// FF-Occam terminal presentation layer — stderr only (MCP stdio safe).
/// Banner: default on (disable <c>OCCAM_BANNER=0</c> or legacy <c>WT_OCCAM_BANNER=0</c>).
/// Transcode profiler: <c>OCCAM_LOG=1</c> or legacy <c>WT_OCCAM_LOG=1</c>.
/// Max visible width: <see cref="OccamStderrAnsiSink.MaxWidth"/> (52).
/// </summary>
public static class OccamLogger
{
    private static readonly IOccamLogSink Sink = new OccamStderrAnsiSink();
    private static readonly object Gate = new();
    private static bool _bannerChecked;
    private static bool _bannerEnabled = true;
    private static bool _logChecked;
    private static bool _logEnabled;
    private static bool _bannerWritten;

    public static bool IsEnabled => ResolveLogEnabled();

    /// <summary>One-time FF-Occam signature banner on stderr at process start.</summary>
    public static void TryWriteStartupBanner(WorkerPaths? paths = null)
    {
        if (!ResolveBannerEnabled())
        {
            return;
        }

        lock (Gate)
        {
            if (_bannerWritten)
            {
                return;
            }

            paths ??= WorkerPaths.Resolve();
            Sink.Write(new OccamLogEvent(OccamLogEventKind.StartupBanner, Paths: paths));
            _bannerWritten = true;
        }
    }

    public static void TryWriteTranscodeReport(TranscodeResult result, string outputText)
    {
        if (!IsEnabled)
        {
            return;
        }

        Sink.Write(new OccamLogEvent(
            OccamLogEventKind.TranscodeReport,
            Transcode: result,
            OutputText: outputText));
    }

    public static void TryWriteTranscodeFailure(TranscodeResult result)
    {
        if (!IsEnabled)
        {
            return;
        }

        Sink.Write(new OccamLogEvent(OccamLogEventKind.TranscodeFailure, Transcode: result));
    }

    public static void TryWriteBrowserPoolEvent(
        string phase,
        int slotId,
        int port,
        int waitMs,
        int pendingDepth,
        bool ok,
        int extractMs)
    {
        if (!IsEnabled)
        {
            return;
        }

        Sink.Write(new OccamLogEvent(
            OccamLogEventKind.BrowserPool,
            PoolPhase: phase,
            PoolSlotId: slotId,
            PoolPort: port,
            PoolWaitMs: waitMs,
            PoolPendingDepth: pendingDepth,
            PoolOk: ok,
            PoolExtractMs: extractMs));
    }

    public static void TryWriteStageBreakdown(
        string backend,
        int preflightMs,
        int routeMs,
        int postProcessMs,
        int compileMs)
    {
        if (!IsEnabled)
        {
            return;
        }

        lock (Gate)
        {
            Sink.Write(new OccamLogEvent(
                OccamLogEventKind.StageBreakdown,
                StageBackend: backend,
                StagePreflightMs: preflightMs,
                StageRouteMs: routeMs,
                StagePostProcessMs: postProcessMs,
                StageCompileMs: compileMs));
        }
    }

    internal static OccamTelemetry ComputeTelemetry(TranscodeResult result, string outputText) =>
        ((OccamStderrAnsiSink)Sink).ComputeTelemetry(result, outputText);

    internal static string FormatTitleLine(string? version = null) =>
        ((OccamStderrAnsiSink)Sink).FormatTitleLine(version);

    internal static string FormatHeader() => FormatTitleLine();

    internal static string FormatShredderLine(double contextCutPercent, int tokensBefore, int tokensAfter) =>
        ((OccamStderrAnsiSink)Sink).FormatShredderLine(contextCutPercent, tokensBefore, tokensAfter);

    internal static string FormatSavingsLine(OccamTelemetry telemetry) =>
        ((OccamStderrAnsiSink)Sink).FormatSavingsLine(telemetry);

    internal static int VisibleLength(string text) => OccamStderrAnsiSink.VisibleLength(text);

    internal static string FitVisible(string text, int maxWidth) =>
        OccamStderrAnsiSink.FitVisible(text, maxWidth);

    internal static IEnumerable<string> BuildStartupBanner(WorkerPaths paths) =>
        ((OccamStderrAnsiSink)Sink).BuildStartupBanner(paths);

    public const int MaxWidth = OccamStderrAnsiSink.MaxWidth;
    public const int ShredderBlocks = OccamStderrAnsiSink.ShredderBlocks;
    public const double HighEfficiencySavingsUsd = OccamStderrAnsiSink.HighEfficiencySavingsUsd;

#if OCCAM_GATE
    internal static void ResetForTests()
    {
        lock (Gate)
        {
            _bannerChecked = false;
            _bannerEnabled = true;
            _logChecked = false;
            _logEnabled = false;
            _bannerWritten = false;
        }
    }

    internal static void ForceEnabledForTests(bool enabled)
    {
        lock (Gate)
        {
            _logChecked = true;
            _logEnabled = enabled;
            _bannerWritten = false;
        }
    }
#endif

    private static bool ResolveBannerEnabled()
    {
        if (_bannerChecked)
        {
            return _bannerEnabled;
        }

        lock (Gate)
        {
            if (_bannerChecked)
            {
                return _bannerEnabled;
            }

            _bannerEnabled = OccamEnvironment.GetFlag("OCCAM_BANNER", defaultValue: true, fallback: "WT_OCCAM_BANNER");
            _bannerChecked = true;
            return _bannerEnabled;
        }
    }

    private static bool ResolveLogEnabled()
    {
        if (_logChecked)
        {
            return _logEnabled;
        }

        lock (Gate)
        {
            if (_logChecked)
            {
                return _logEnabled;
            }

            _logEnabled = OccamEnvironment.GetFlag("OCCAM_LOG", defaultValue: false, fallback: "WT_OCCAM_LOG");
            _logChecked = true;
            return _logEnabled;
        }
    }
}

internal readonly record struct OccamTelemetry(
    int TokensBefore,
    int TokensAfter,
    double ContextCutPercent,
    double SavingsDollars,
    int LatencyMs,
    string Backend,
    string Host);
