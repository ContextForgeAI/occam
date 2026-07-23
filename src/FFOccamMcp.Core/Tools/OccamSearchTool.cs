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

    [McpServerTool(Name = "occam_search"), Description("Open-web search (query -> result URLs) via a configured backend (SearXNG/Brave/Tavily). Your discovery step when you don't have URLs yet - feed results into probe/transcode/digest. Requires OCCAM_SEARCH_PROVIDER. Returns { title, url, snippet }.")]
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

        var suggested = results.Length > 0
            ? "occam_transcode (fetch a result URL) or occam_digest (compare several)"
            : "refine query or try another provider";
        if (rerank && results.Length > 0)
        {
            suggested = "results reranked by extractability — prefer the top (highest extractability) URLs for transcode";
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

    private static string DescribeFailure(string? code) => code switch
    {
        "search_unconfigured" => "Search is not configured. Set OCCAM_SEARCH_PROVIDER (searxng|brave|tavily) + OCCAM_SEARCH_URL (SearXNG) or OCCAM_SEARCH_API_KEY (Brave/Tavily).",
        "search_timeout" => "Search backend timed out. Retry or raise OCCAM_SEARCH_TIMEOUT_MS.",
        var c when c is not null && c.StartsWith("search_http_", StringComparison.Ordinal) => $"Search backend returned {c["search_http_".Length..]}. Check the endpoint/key.",
        _ => "Search backend call failed.",
    };

    private static string SerializeFailure(string query, string code, string message) =>
        JsonSerializer.Serialize(
            new OccamSearchFailureResponse(false, query, new OccamSearchFailureInfo(code, message)),
            OccamSearchJsonContext.Default.OccamSearchFailureResponse);
}
