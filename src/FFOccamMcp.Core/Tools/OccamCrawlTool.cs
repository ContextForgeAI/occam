using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Crawl;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Env-gated crawl MCP (ADR-0018). Opt-in: <c>OCCAM_CRAWL_MCP=1</c>. Not in default reader surface.
/// </summary>
[McpServerToolType]
public sealed class OccamCrawlTool(ICrawlService crawl)
{
    [McpServerTool(Name = "occam_crawl"), Description(
        "EXPERIMENTAL resumable same-domain crawl: discover links from a seed (sitemap→homepage), "
        + "then transcode up to max_pages. Pass resume_token from a prior response to continue. "
        + "Opt-in — host must set OCCAM_CRAWL_MCP=1. Not a CAPTCHA unlocker; use session_profile for gated sites.")]
    public async Task<string> Crawl(
        [Description("Seed URL to map and crawl. Required unless resume_token is set.")] string? url = null,
        [Description("Opaque resume token from a prior occam_crawl (caller-held queue).")] string? resume_token = null,
        [Description("Max pages to fetch this call (1–32, default 8).")] int max_pages = CrawlService.DefaultMaxPages,
        [Description("Max links to discover from seed (1–64, default 32).")] int max_links = 32,
        [Description("Map discovery preference: sitemap (default, falls back to homepage) or homepage.")] string map_source = "sitemap",
        [Description("Backend policy: http, browser, or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Optional focus query applied per page (BM25 prune when fit_markdown).")] string? focus_query = null,
        [Description("BM25-style paragraph prune per page. Default true.")] bool fit_markdown = true,
        [Description("Optional per-URL output token budget (minimum 128).")] int? per_url_max_tokens = null,
        [Description("Optional session profile id.")] string? session_profile = null,
        [Description("Playbook merge policy: off or auto. Default auto.")] string playbook_policy = "auto",
        [Description("Include a JSONL string of page results in the response.")] bool include_jsonl = true,
        CancellationToken cancellationToken = default)
    {
        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return SerializeError("invalid_policy", "backend_policy must be http, browser, or http_then_browser.");
        }

        var outcome = await crawl.CrawlAsync(
            url,
            resume_token,
            max_pages,
            max_links,
            map_source,
            policy,
            focus_query,
            fit_markdown,
            per_url_max_tokens,
            session_profile,
            playbook_policy,
            include_jsonl,
            cancellationToken).ConfigureAwait(false);

        if (!outcome.Ok)
        {
            return SerializeError(outcome.FailureCode ?? "crawl_failed", outcome.FailureMessage ?? "crawl failed.");
        }

        return JsonSerializer.Serialize(outcome, CrawlJsonContext.Default.CrawlOutcome);
    }

    private static string SerializeError(string code, string message) =>
        JsonSerializer.Serialize(
            new CrawlErrorResponse(new CrawlFailureInfo(code, message)),
            CrawlJsonContext.Default.CrawlErrorResponse);
}
