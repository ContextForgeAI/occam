using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Services;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamMapTool(MapService mapService)
{
    [McpServerTool(Name = "occam_map"), Description("Discover a site's same-domain links from its homepage, sitemap, or robots.txt (HTTP-only, up to 64 URLs). Use to find pages to feed into occam_digest when you don't have the URLs yet.")]
    public async Task<string> Map(
        [Description("HTTP or HTTPS seed URL.")] string url,
        [Description("Link source: homepage (default), sitemap, or robots.")] string source = "homepage",
        [Description("Maximum links to return (1-64). Default 32.")] int max_links = MapService.DefaultMaxLinks,
        [Description("When true, drop off-origin links. Default true.")] bool same_domain = true,
        [Description("Drop asset/webpack/mailto links. Default true.")] bool filter_nonsense = true,
        [Description("Optional focus keywords: entity-first re-rank (primary identifiers over supporting terms; path/title before BM25). When the seed page has no strong hit, homepage source may expand one hub level.")] string? focus_query = null,
        [Description("Total map/discovery timeout in milliseconds (3000-30000), including response bodies and sitemap traversal. Default 15000.")] int timeout_ms = MapService.DefaultTimeoutMs,
        [Description("Optional session profile id — loads headers from OCCAM_SESSIONS_ROOT/<id>.json.")] string? session_profile = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (max_links < 1 || max_links > MapService.MaxLinksCap)
        {
            return SerializeFailure(
                "invalid_arguments",
                $"max_links must be between 1 and {MapService.MaxLinksCap}.",
                url);
        }

        if (timeout_ms < MapService.MinTimeoutMs || timeout_ms > MapService.MaxTimeoutMs)
        {
            return SerializeFailure(
                "invalid_arguments",
                $"timeout_ms must be between {MapService.MinTimeoutMs} and {MapService.MaxTimeoutMs}.",
                url);
        }

        var analysis = await mapService.MapAsync(
            url,
            max_links,
            same_domain,
            timeout_ms,
            source,
            filter_nonsense,
            focus_query,
            session_profile,
            cancellationToken).ConfigureAwait(false);

        if (analysis.Ok)
        {
            return JsonSerializer.Serialize(
                OccamMapResponseMapper.MapSuccess(analysis),
                OccamMapJsonContext.Default.OccamMapSuccessResponse);
        }

        return JsonSerializer.Serialize(
            OccamMapResponseMapper.MapFailure(analysis),
            OccamMapJsonContext.Default.OccamMapFailureResponse);
    }

    private static string SerializeFailure(string code, string message, string url)
    {
        var hints = FailureAgentHints.ForCode(code);
        return JsonSerializer.Serialize(
            new OccamMapFailureResponse(
                false,
                code,
                message,
                url,
                null,
                null,
                hints is null ? null : new OccamMapFailureAgentHintsInfo(hints.Decisions),
                DateTimeOffset.UtcNow.ToString("O")),
            OccamMapJsonContext.Default.OccamMapFailureResponse);
    }
}
