using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Services;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamSearchTool(ISearchService searchService, ProbeService probeService)
{
    private const int DefaultMaxResults = 8;
    private const int MaxResultsCap = 20;
    private const int RerankProbeTimeoutMs = 6_000;
    private const int RerankMaxParallel = 5;

    [McpServerTool(Name = "occam_search"), Description("Open-web search (query -> result URLs). Default keyless backend is DuckDuckGo HTML (provider=duckduckgo disclosed); override with OCCAM_SEARCH_PROVIDER=searxng|brave|tavily|donsetch, or off to disable. Your discovery step when you don't have URLs yet - feed result urls into probe/transcode/digest. Each hit gets a label id S1..Sn (notes only; Occam does not resolve handles). Returns { id, title, url, snippet, provider }. Occam does not index the web.")]
    public async Task<string> Search(
        [Description("Search query.")] string query,
        [Description("Max results to return (1-20). Default 8.")] int max_results = DefaultMaxResults,
        [Description("Rerank results by extractability: cheaply probes each hit and reorders so clean HTTP-extractable pages rank above paywalls, anti-bot walls, JS stubs and dead links. Adds extractability (0-1) + recommendedBackend per result. Opt-in (extra probe latency); off by default.")] bool rerank = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(query))
        {
            return SerializeFailure(query ?? "", "invalid_arguments", "query must not be empty.");
        }

        if (max_results < 1 || max_results > MaxResultsCap)
        {
            return SerializeFailure(query, "invalid_arguments", $"max_results must be between 1 and {MaxResultsCap}.");
        }

        var outcome = await searchService.SearchAsync(
            query.Trim(),
            max_results,
            cancellationToken).ConfigureAwait(false);
        if (!outcome.Ok)
        {
            return SerializeFailure(query, outcome.FailureCode ?? "search_error", DescribeFailure(outcome.FailureCode));
        }

        var results = outcome.Results
            .Select(r => new OccamSearchResultInfo(r.Title, r.Url, r.Snippet))
            .ToArray();

        if (rerank && results.Length > 1)
        {
            results = await RerankAsync(results, cancellationToken).ConfigureAwait(false);
        }

        // Assign S1…Sn after final order so labels match what the agent sees.
        results = AssignResultIds(results);

        var suggested = results.Length > 0
            ? "Pass a result url to occam_transcode (one page) or occam_digest (several). Labels S1…Sn are for your notes only — Occam does not resolve handles server-side."
            : "refine query or try another provider";
        if (rerank && results.Length > 0)
        {
            suggested = "Results reranked by extractability — prefer top urls for transcode. Labels S1…Sn are notes only; always pass the url field.";
        }

        return JsonSerializer.Serialize(
            new OccamSearchSuccessResponse(true, query.Trim(), outcome.Provider, results.Length, results, new OccamSearchAgentHintsInfo(suggested)),
            OccamSearchJsonContext.Default.OccamSearchSuccessResponse);
    }

    /// <summary>
    /// Probes each result (bounded parallelism + short timeout) and returns a stable sort by
    /// extractability descending, annotating each result with its score + recommended backend.
    /// Original search rank breaks ties (stable).
    /// </summary>
    private async Task<OccamSearchResultInfo[]> RerankAsync(
        OccamSearchResultInfo[] results,
        CancellationToken cancellationToken)
    {
        var scored = new (OccamSearchResultInfo Result, double Score, int Rank)[results.Length];
        using var gate = new SemaphoreSlim(RerankMaxParallel, RerankMaxParallel);
        var tasks = Enumerable.Range(0, results.Length).Select(async i =>
        {
            await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var result = results[i];
                double score;
                string? backend;
                try
                {
                    var probe = await probeService.AnalyzeAsync(
                        result.Url,
                        RerankProbeTimeoutMs,
                        cancellationToken: cancellationToken).ConfigureAwait(false);
                    score = SearchExtractabilityScorer.Score(probe);
                    backend = probe.RecommendedBackend;
                }
                catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
                {
                    throw;
                }
                catch
                {
                    // A probe failure must not drop the result — keep it, mid-low score, unannotated.
                    score = 0.4;
                    backend = null;
                }

                scored[i] = (
                    result with { Extractability = Math.Round(score, 2), RecommendedBackend = backend },
                    score,
                    i);
            }
            finally
            {
                gate.Release();
            }
        }).ToArray();

        await Task.WhenAll(tasks).ConfigureAwait(false);

        return [.. scored
            .OrderByDescending(s => s.Score)
            .ThenBy(s => s.Rank)
            .Select(s => s.Result)];
    }

    /// <summary>Stable <c>S1</c>… labels after ranking. Not a server-side handle store.</summary>
    internal static OccamSearchResultInfo[] AssignResultIds(OccamSearchResultInfo[] results)
    {
        var labeled = new OccamSearchResultInfo[results.Length];
        for (var i = 0; i < results.Length; i++)
        {
            labeled[i] = results[i] with { Id = $"S{i + 1}" };
        }

        return labeled;
    }

    private static string DescribeFailure(string? code) => code switch
    {
        "search_unconfigured" => "Search is disabled or incomplete. Default is keyless duckduckgo when OCCAM_SEARCH_PROVIDER is unset. Set OCCAM_SEARCH_PROVIDER=off to keep search off; searxng needs OCCAM_SEARCH_URL; brave/tavily need OCCAM_SEARCH_API_KEY; donsetch needs a local binary (OCCAM_DONSETCH_PATH optional).",
        "search_timeout" => "Search backend timed out. Retry or raise OCCAM_SEARCH_TIMEOUT_MS.",
        var c when c is not null && c.StartsWith("search_http_", StringComparison.Ordinal) =>
            c is "search_http_202" or "search_http_403" or "search_http_429"
                ? "Search backend soft-blocked or rate-limited this egress (DuckDuckGo may show an anomaly challenge). Retry later or set OCCAM_SEARCH_PROVIDER to searxng/brave/tavily."
                : $"Search backend returned {c["search_http_".Length..]}. Check the endpoint/key, or set a dedicated provider (searxng/brave/tavily).",
        _ => "Search backend call failed (empty or blocked SERP, or parse miss). Retry, refine the query, or set OCCAM_SEARCH_PROVIDER to searxng/brave/tavily.",
    };

    private static string SerializeFailure(string query, string code, string message) =>
        JsonSerializer.Serialize(
            new OccamSearchFailureResponse(false, query, new OccamSearchFailureInfo(code, message)),
            OccamSearchJsonContext.Default.OccamSearchFailureResponse);
}
