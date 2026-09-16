using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Handles;
using OccamMcp.Core.Services;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamSearchTool(ISearchService searchService, ProbeService probeService, SourceHandleStore sourceHandles)
{
    private const int DefaultMaxResults = 8;
    private const int MaxResultsCap = 20;
    private const int RerankProbeTimeoutMs = 6_000;
    private const int RerankMaxParallel = 5;

    [McpServerTool(Name = "occam_search"), Description("Open-web search → result URLs. Default keyless DuckDuckGo (OCCAM_SEARCH_PROVIDER). Multi-backend fan-out via OCCAM_SEARCH_PROVIDERS CSV. No URLs yet → search, then pass result.handle or url to probe/transcode/digest. S1 is latest-search only; H… survives later searches. Returns {id, handle, title, url, snippet}. Does not index the web.")]
    public async Task<string> Search(
        [Description("Search query.")] string query,
        [Description("Max results to return (1-20). Default 8.")] int max_results = DefaultMaxResults,
        [Description("Rerank by extractability (extra probe latency). Adds extractability + recommendedBackend. Opt-in.")] bool rerank = false,
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
        var remembered = sourceHandles.RememberSearch(
            query.Trim(),
            results.Select(r => (r.Title, r.Url)).ToArray());
        for (var i = 0; i < results.Length && i < remembered.Count; i++)
        {
            results[i] = results[i] with { Handle = remembered[i].Handle };
        }

        var suggested = results.Length > 0
            ? "Pass result.handle or url to occam_transcode (one page) or occam_digest (several). S1 is the latest search only; H… survives later searches until TTL."
            : "refine query or try another provider";
        if (rerank && results.Length > 0)
        {
            suggested = "Results reranked by extractability — prefer top handle/url for transcode. S1 is latest-search only.";
        }

        string[]? providersUsed = null;
        if (outcome.ProvidersUsed is { Count: > 0 })
        {
            providersUsed = outcome.ProvidersUsed as string[] ?? outcome.ProvidersUsed.ToArray();
        }

        return JsonSerializer.Serialize(
            new OccamSearchSuccessResponse(
                true,
                query.Trim(),
                outcome.Provider,
                results.Length,
                results,
                new OccamSearchAgentHintsInfo(suggested),
                HandleTtlS: (int)SourceHandleStore.DefaultTtl.TotalSeconds,
                HandleScope: "process",
                ProvidersUsed: providersUsed),
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

    /// <summary>Latest-search shorthand <c>S1</c>… after ranking. Durable identity is <c>handle</c>.</summary>
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
        "search_unconfigured" => "Search is disabled or incomplete. Default is keyless duckduckgo when OCCAM_SEARCH_PROVIDER is unset. Set OCCAM_SEARCH_PROVIDER=off to keep search off; or OCCAM_SEARCH_PROVIDERS=duckduckgo,brave for fan-out. searxng needs OCCAM_SEARCH_URL; brave/tavily need OCCAM_SEARCH_API_KEY; donsetch needs a local binary (OCCAM_DONSETCH_PATH optional).",
        "search_timeout" => "Search backend timed out. Retry, raise OCCAM_SEARCH_PROVIDER_TIMEOUT_MS (fan-out) or OCCAM_SEARCH_TIMEOUT_MS, or add another provider via OCCAM_SEARCH_PROVIDERS.",
        "search_rate_limited" => "Search provider(s) are rate-limited or temporarily degraded after 429/CAPTCHA/timeout. Wait for OCCAM_SEARCH_DEGRADE_MINUTES cooldown or try another backend in OCCAM_SEARCH_PROVIDERS.",
        var c when c is not null && c.StartsWith("search_http_", StringComparison.Ordinal) =>
            c is "search_http_202" or "search_http_403" or "search_http_429"
                ? "Search backend soft-blocked or rate-limited this egress (DuckDuckGo may show an anomaly challenge). Retry later or set OCCAM_SEARCH_PROVIDERS / OCCAM_SEARCH_PROVIDER to searxng/brave/tavily."
                : $"Search backend returned {c["search_http_".Length..]}. Check the endpoint/key, or set a dedicated provider (searxng/brave/tavily).",
        _ => "Search backend call failed (empty or blocked SERP, or parse miss). Retry, refine the query, or set OCCAM_SEARCH_PROVIDER / OCCAM_SEARCH_PROVIDERS to searxng/brave/tavily.",
    };

    private static string SerializeFailure(string query, string code, string message) =>
        JsonSerializer.Serialize(
            new OccamSearchFailureResponse(false, query, new OccamSearchFailureInfo(code, message)),
            OccamSearchJsonContext.Default.OccamSearchFailureResponse);
}
