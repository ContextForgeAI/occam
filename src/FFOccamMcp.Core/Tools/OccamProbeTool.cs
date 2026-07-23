using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Services;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamProbeTool(ProbeService probeService)
{
    [McpServerTool(Name = "occam_probe"), Description("Before paying for a full fetch, cheaply diagnose a URL: page class, risks, redirect chain, an extractability score (0-1, low = paywall/anti-bot/JS-stub/dead), and the recommended backend for occam_transcode. Use to decide whether a page is worth transcoding.")]
    public async Task<string> Probe(
        [Description("HTTP or HTTPS URL to probe.")] string url,
        [Description("Probe timeout in milliseconds.")] int timeout_ms = 10_000,
        [Description("Extract OpenGraph/Twitter meta from HTML head.")] bool include_social_meta = false,
        [Description("Optional session profile id — loads headers from OCCAM_SESSIONS_ROOT/<id>.json.")] string? session_profile = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url) || !Uri.TryCreate(url, UriKind.Absolute, out _))
        {
            var hints = ProbeAgentHints.ForFailure("invalid_arguments");
            return JsonSerializer.Serialize(
                new OccamProbeFailureResponse(
                    false,
                    new OccamProbeUrlInfo(url, null),
                    "invalid_arguments",
                    "url must be a valid absolute HTTP or HTTPS URL.",
                    new OccamProbePolicyInfo("local_public"),
                    0,
                    null,
                    0,
                    hints.Decisions.Length > 0
                        ? new OccamProbeAgentHintsInfo(hints.SuggestedNextTool, hints.Warnings, hints.Decisions)
                        : null,
                    DateTimeOffset.UtcNow.ToString("O")),
                OccamProbeJsonContext.Default.OccamProbeFailureResponse);
        }

        var analysis = await probeService.AnalyzeAsync(
            url,
            timeout_ms,
            include_social_meta,
            session_profile,
            cancellationToken).ConfigureAwait(false);
        if (!analysis.Ok || analysis.Classification is null)
        {
            return JsonSerializer.Serialize(
                OccamProbeResponseMapper.MapFailure(analysis),
                OccamProbeJsonContext.Default.OccamProbeFailureResponse);
        }

        return JsonSerializer.Serialize(
            OccamProbeResponseMapper.MapSuccess(analysis),
            OccamProbeJsonContext.Default.OccamProbeSuccessResponse);
    }
}
