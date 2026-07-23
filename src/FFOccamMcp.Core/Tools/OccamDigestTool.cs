using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Json;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamDigestTool(
    DigestService digestService,
    OccamMcp.Core.Client.ClientCapabilityStore clientCapabilities)
{
    [McpServerTool(Name = "occam_digest"), Description("Research several pages at once: digests up to 8 URLs into per-page excerpts plus optional combined Markdown. Prefer ONE digest over N separate occam_transcode calls when you have multiple URLs. Supply urls and/or source_url (at least one). When source_url is set, urls is ignored and links are auto-discovered. Use focus_query to keep only the parts relevant to your question.")]
    public async Task<string> Digest(
        [Description("Preferred: array of URL strings. Deprecated compatibility: a JSON-array string or newline/comma-separated URL string. Optional when source_url is set (ignored in that case).")] JsonElement? urls = null,
        [Description("Backend policy applied to each URL: http, browser, or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Maximum URLs to process (1-8). Extra URLs are dropped.")] int max_urls = DigestService.MaxUrlsCap,
        [Description("Optional per-URL output token budget (minimum 128). Omit to use ambient client budget from occam_client_capabilities, or full markdown when none is set.")] int? per_url_max_tokens = null,
        [Description("Optional focus keywords applied to each URL (overridden by per-entry focus_query). Recommended for research digests.")] string? focus_query = null,
        [Description("BM25-style paragraph prune per URL. Default true.")] bool fit_markdown = true,
        [Description("Include combined markdown block with ## titles. Default true.")] bool include_combined = true,
        [Description("Optional session profile id applied to every URL in the batch.")] string? session_profile = null,
        [Description("AF-5: source URL for auto-link-discovery (sitemap or HTML links). When set, urls is ignored.")] string? source_url = null,
        [Description("AF-5: max links to discover from source_url (1-8). Default 8.")] int max_links = DigestService.MaxUrlsCap,
        [Description("AF-6: SHA-256 of prior combined markdown — bare hex or receipt sha256:-prefixed contentHash. If combined matches, returns unchanged:true with empty combined.")] string? if_none_match = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return SerializeFailure("invalid_arguments", "backend_policy must be http, browser, or http_then_browser.");
        }

        if (!DigestInputContract.TryValidate(urls, source_url, out var useSourceUrl, out var code, out var message))
        {
            return SerializeFailure(code!, message!);
        }

        IReadOnlyList<DigestUrlEntry>? normalizedUrls = null;
        if (!useSourceUrl
            && !DigestInputNormalizer.TryNormalize(urls, out normalizedUrls, out var normalizationError))
        {
            return SerializeFailure("invalid_arguments", normalizationError ?? "urls is invalid.");
        }

        var analysis = await digestService.DigestAsync(
            normalizedUrls,
            max_urls,
            clientCapabilities.ResolveMaxTokens(per_url_max_tokens),
            policy,
            focus_query,
            fit_markdown,
            include_combined,
            session_profile,
            source_url,
            max_links,
            if_none_match,
            cancellationToken);

        if (analysis.Ok)
        {
            return OccamJsonPrintableEscapes.Serialize(
                OccamDigestResponseMapper.MapSuccess(analysis),
                OccamDigestJsonContext.Default.OccamDigestSuccessResponse);
        }

        return OccamJsonPrintableEscapes.Serialize(
            OccamDigestResponseMapper.MapFailure(analysis),
            OccamDigestJsonContext.Default.OccamDigestFailureResponse);
    }

    private static string SerializeFailure(string code, string message)
    {
        var hints = FailureAgentHints.ForCode(code);
        return OccamJsonPrintableEscapes.Serialize(
            new OccamDigestFailureResponse(
                false,
                code,
                message,
                null,
                null,
                null,
                hints is null ? null : new OccamDigestFailureAgentHintsInfo(hints.Decisions),
                DateTimeOffset.UtcNow.ToString("O")),
            OccamDigestJsonContext.Default.OccamDigestFailureResponse);
    }
}
