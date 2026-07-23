using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using OccamMcp.Core.Configuration;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Telemetry;

/// <summary>Default stderr adapter — ANSI formatting for MCP-safe stderr output.</summary>
public sealed partial class OccamStderrAnsiSink : IOccamLogSink
{
    public const int MaxWidth = 52;
    public const int ShredderBlocks = 20;
    public const double HighEfficiencySavingsUsd = 0.02;

    private const string Reset = "\u001b[0m";
    private const string PureWhite = "\u001b[38;5;255m";
    private const string ElectricCyan = "\u001b[38;5;45m";
    private const string SpaceGray = "\u001b[38;5;244m";
    private const string IosGreen = "\u001b[38;5;46m";

    private const char BlockKept = '█';
    private const char BlockCut = '░';
    private const double DefaultUsdPerMillionTokens = 0.15;

    private readonly IBannerContentProvider _bannerContent;

    public OccamStderrAnsiSink(IBannerContentProvider? bannerContent = null)
    {
        _bannerContent = bannerContent ?? new DefaultBannerContentProvider();
    }

    public void Write(OccamLogEvent logEvent)
    {
        switch (logEvent.Kind)
        {
            case OccamLogEventKind.StartupBanner when logEvent.Paths is not null:
                foreach (var line in BuildStartupBanner(logEvent.Paths))
                {
                    Console.Error.WriteLine(line);
                }

                break;
            case OccamLogEventKind.TranscodeReport when logEvent.Transcode is not null:
                var telemetry = ComputeTelemetry(logEvent.Transcode, logEvent.OutputText ?? string.Empty);
                EmitTranscodeBlock(
                    FormatStatusLine(logEvent.Transcode),
                    FormatHostLine(logEvent.Transcode.Url),
                    FormatShredderLine(telemetry.ContextCutPercent, telemetry.TokensBefore, telemetry.TokensAfter),
                    FormatSavingsLine(telemetry));
                break;
            case OccamLogEventKind.TranscodeFailure when logEvent.Transcode is not null:
                var code = !string.IsNullOrWhiteSpace(logEvent.Transcode.Text)
                    ? logEvent.Transcode.Text
                    : "failed";
                EmitTranscodeBlock(FitVisible($"{SpaceGray}  fail  {code}{Reset}", MaxWidth));
                break;
            case OccamLogEventKind.BrowserPool:
                WriteBrowserPoolLine(logEvent);
                break;
            case OccamLogEventKind.StageBreakdown:
                Console.Error.WriteLine(
                    $"occam.stage backend={logEvent.StageBackend} preflight_ms={logEvent.StagePreflightMs} " +
                    $"route_extract_ms={logEvent.StageRouteMs} post_process_ms={logEvent.StagePostProcessMs} " +
                    $"compile_serialize_ms={logEvent.StageCompileMs}");
                break;
        }
    }

    internal IEnumerable<string> BuildStartupBanner(WorkerPaths paths)
    {
        var model = _bannerContent.Build(paths);
        yield return FormatTitleLine();
        yield return FormatRule();
        yield return FormatKvLine("ARCHITECTURE", model.Architecture);
        yield return FormatKvLine("MODE", model.Mode);
        yield return FormatKvLine("WORKERS", model.Workers);
        yield return FormatRule();
        foreach (var row in model.StatusRows)
        {
            yield return FormatStatusIndicator(row.Active, row.Label, row.Value);
        }

        yield return FormatRule();
        yield return string.Empty;
        yield return FitVisible($"{SpaceGray}  {model.ListeningHint}{Reset}", MaxWidth);
    }

    internal string FormatTitleLine(string? version = null)
    {
        version ??= ResolveProductVersion();
        var brand = $"{PureWhite} ⌥  F F ─ O C C A M  ·  M C P{Reset}";
        var ver = $"{SpaceGray}v{version}{Reset}";
        var brandVisible = VisibleLength(brand);
        var verVisible = VisibleLength(ver);
        var pad = Math.Max(1, MaxWidth - brandVisible - verVisible);
        return FitVisible($"{brand}{new string(' ', pad)}{ver}", MaxWidth);
    }

    internal string FormatHeader() => FormatTitleLine();

    internal string FormatShredderLine(double contextCutPercent, int tokensBefore, int tokensAfter)
    {
        var keptBlocks = tokensBefore > 0
            ? Math.Clamp((int)Math.Round(tokensAfter / (double)tokensBefore * ShredderBlocks), 0, ShredderBlocks)
            : contextCutPercent > 0 ? 0 : ShredderBlocks;
        var bar = BuildShredderBar(keptBlocks);
        var cutLabel = $"-{contextCutPercent:F1}%";
        var suffix = TrySuffix("  [SHREDDER]  ", bar, cutLabel, " Context Cut")
            ?? TrySuffix("  [SHREDDER]  ", bar, cutLabel, " Ctx Cut")
            ?? $"  {cutLabel}";
        return FitVisible($"{SpaceGray}  [SHREDDER]{Reset}  {bar}{suffix}", MaxWidth);
    }

    internal string FormatSavingsLine(OccamTelemetry telemetry)
    {
        var dollarsText = FormatDollars(telemetry.SavingsDollars);
        var dollarsColor = telemetry.SavingsDollars > HighEfficiencySavingsUsd ? ElectricCyan : SpaceGray;
        var body =
            $"  {FormatTokenDelta(telemetry.TokensBefore, telemetry.TokensAfter)}  saved  " +
            $"{dollarsColor}{dollarsText}{Reset}";
        return FitVisible(body, MaxWidth);
    }

    internal static int VisibleLength(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        return AnsiPattern().Replace(text, string.Empty).Length;
    }

    internal static string FitVisible(string text, int maxWidth)
    {
        if (VisibleLength(text) <= maxWidth)
        {
            return text;
        }

        var visible = AnsiPattern().Replace(text, string.Empty);
        return visible.Length <= maxWidth ? visible[..maxWidth] : visible[..maxWidth];
    }

    internal OccamTelemetry ComputeTelemetry(TranscodeResult result, string outputText)
    {
        var tokensAfter = TokenEstimator.Estimate(outputText);
        var tokensBefore = result.HtmlBytes > 0
            ? TokenEstimator.EstimateFromByteCount(result.HtmlBytes)
            : tokensAfter;
        if (tokensBefore < tokensAfter)
        {
            tokensBefore = tokensAfter;
        }

        var cutPercent = tokensBefore > 0
            ? Math.Round((1.0 - tokensAfter / (double)tokensBefore) * 100.0, 1)
            : 0.0;
        var tokensSaved = Math.Max(0, tokensBefore - tokensAfter);
        var savingsDollars = tokensSaved / 1_000_000.0 * ResolveUsdPerMillionTokens();

        return new OccamTelemetry(
            tokensBefore,
            tokensAfter,
            cutPercent,
            savingsDollars,
            result.LatencyMs,
            result.Backend,
            ExtractHost(result.Url));
    }

    private void WriteBrowserPoolLine(OccamLogEvent logEvent)
    {
        var status = logEvent.PoolOk ? "ok" : "fail";
        var line = logEvent.PoolPhase switch
        {
            "acquired" => FitVisible(
                $"{ElectricCyan}  pool  slot={logEvent.PoolSlotId} port={logEvent.PoolPort} " +
                $"wait={logEvent.PoolWaitMs}ms depth={logEvent.PoolPendingDepth}{Reset}",
                MaxWidth),
            "released" => FitVisible(
                $"{ElectricCyan}  pool  slot={logEvent.PoolSlotId} {status} extract={logEvent.PoolExtractMs}ms{Reset}",
                MaxWidth),
            _ => FitVisible(
                $"{ElectricCyan}  pool  {logEvent.PoolPhase} slot={logEvent.PoolSlotId}{Reset}",
                MaxWidth),
        };
        Console.Error.WriteLine(line);
    }

    private static string FormatRule() =>
        FitVisible($"{SpaceGray}{new string('─', MaxWidth)}{Reset}", MaxWidth);

    private static string FormatKvLine(string label, string value)
    {
        var gap = Math.Max(1, 16 - label.Length);
        var line = $"  {PureWhite}{label}{Reset}{new string(' ', gap)}{SpaceGray}{value}{Reset}";
        return FitVisible(line, MaxWidth);
    }

    private static string FormatStatusIndicator(bool active, string label, string value)
    {
        var dot = active
            ? $"{IosGreen}●{Reset}"
            : $"{SpaceGray}○{Reset}";
        var gap = Math.Max(1, 14 - label.Length);
        var line = $"  {dot}  {PureWhite}{label}{Reset}{new string(' ', gap)}{SpaceGray}{value}{Reset}";
        return FitVisible(line, MaxWidth);
    }

    private static string FormatStatusLine(TranscodeResult result)
    {
        var line = result.Ok
            ? $"{IosGreen}  ok{Reset}  {result.Backend}  {result.LatencyMs}ms"
            : $"  fail  {result.Backend}  {result.LatencyMs}ms";
        return FitVisible($"{SpaceGray}{line}{Reset}", MaxWidth);
    }

    private static string FormatHostLine(string url)
    {
        var host = ExtractHost(url);
        if (host.Length > MaxWidth - 2)
        {
            host = host[..(MaxWidth - 5)] + "…";
        }

        return FitVisible($"{SpaceGray}  {host}{Reset}", MaxWidth);
    }

    private static string BuildShredderBar(int keptBlocks)
    {
        var sb = new StringBuilder(ShredderBlocks * 16);
        if (keptBlocks > 0)
        {
            sb.Append(ElectricCyan);
            sb.Append(BlockKept, keptBlocks);
            sb.Append(Reset);
        }

        var cutBlocks = ShredderBlocks - keptBlocks;
        if (cutBlocks > 0)
        {
            sb.Append(SpaceGray);
            sb.Append(BlockCut, cutBlocks);
            sb.Append(Reset);
        }

        return sb.ToString();
    }

    private static string? TrySuffix(string prefix, string bar, string cutLabel, string tail)
    {
        var candidate = $"  {cutLabel}{tail}";
        return VisibleLength($"{prefix}{bar}{candidate}") <= MaxWidth ? candidate : null;
    }

    private static string FormatTokenDelta(int tokensBefore, int tokensAfter) =>
        $"{CompactTokens(tokensBefore)}→{CompactTokens(tokensAfter)} tok";

    private static string CompactTokens(int tokens)
    {
        if (tokens >= 1_000_000)
        {
            return $"{tokens / 1_000_000.0:F1}M";
        }

        if (tokens >= 10_000)
        {
            return $"{tokens / 1_000.0:F1}k";
        }

        if (tokens >= 1_000)
        {
            return $"{tokens / 1_000.0:F1}k";
        }

        return tokens.ToString();
    }

    private static string FormatDollars(double value) => $"${value:F2}";

    private static double ResolveUsdPerMillionTokens()
    {
        var raw = OccamEnvironment.Get("WT_TOKEN_USD_PER_M");
        return double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var parsed)
            && parsed > 0
            ? parsed
            : DefaultUsdPerMillionTokens;
    }

    private static string ResolveProductVersion()
    {
        var attr = typeof(OccamStderrAnsiSink).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(attr))
        {
            var plus = attr.IndexOf('+', StringComparison.Ordinal);
            return plus > 0 ? attr[..plus] : attr;
        }

        return typeof(OccamStderrAnsiSink).Assembly.GetName().Version?.ToString(3) ?? "0.0.0";
    }

    private static string ExtractHost(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return url.Length > MaxWidth - 2 ? url[..(MaxWidth - 2)] : url;
        }

        return uri.Host;
    }

    private static void EmitTranscodeBlock(params string[] bodyLines)
    {
        Console.Error.WriteLine(FormatRule());
        foreach (var line in bodyLines)
        {
            if (!string.IsNullOrEmpty(line))
            {
                Console.Error.WriteLine(line);
            }
        }
    }

    [GeneratedRegex(@"\u001b\[[0-9;]*m")]
    private static partial Regex AnsiPattern();
}
