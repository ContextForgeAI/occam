using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Probe;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Services;

public sealed record DigestItemResult(
    string Url,
    bool Ok,
    string? Title,
    string? Excerpt,
    string? Backend,
    int TokensEstimated,
    string? FailureCode,
    string? FailureMessage,
    string? FocusQuery,
    bool? FocusMatched = null,
    IReadOnlyList<MediaRefInfo>? MediaRefs = null,
    double Confidence = 0.0,
    string? TruncationStrategy = null,
    int LatencyMs = 0,
    ReceiptEnvelope? Receipt = null,
    Access.AccessAssessment? AccessAssessment = null,
    Knowledge.MaterializationAssessment? MaterializationAssessment = null);

public sealed record DigestAnalysis(
    bool Ok,
    string? DigestId,
    IReadOnlyList<DigestItemResult> Items,
    string? Combined,
    int Requested,
    int Succeeded,
    int Failed,
    int TotalTokensEstimated,
    string? FailureCode,
    string? FailureMessage,
    string? SourceUrl = null,
    IReadOnlyList<string>? DiscoveredLinks = null,
    bool? Unchanged = null,
    /// <summary>True when focus_query was set and every ok item has focusMatched=false.</summary>
    bool FocusNotFound = false)
{
    public static DigestAnalysis CreateFailed(string code, string message) =>
        new(false, null, [], null, 0, 0, 0, 0, code, message);
}

public sealed class DigestService(
    WorkerPaths workerPaths,
    TranscodePipeline pipeline,
    HttpProbeFetcher fetcher,
    ReceiptSigner signer,
    MapService mapService)
{
    public const int MaxUrlsCap = 8;
    private const int MinTokenBudget = 128;

    public async ValueTask<DigestAnalysis> DigestAsync(
        IReadOnlyList<DigestUrlEntry>? entries,
        int maxUrls = MaxUrlsCap,
        int? perUrlMaxTokens = null,
        OccamBackendPolicy backendPolicy = OccamBackendPolicy.HttpThenBrowser,
        string? focusQuery = null,
        bool fitMarkdown = true,
        bool includeCombined = true,
        string? sessionProfile = null,
        string? sourceUrl = null,
        int? maxLinks = null,
        string? ifNoneMatch = null,
        CancellationToken cancellationToken = default)
    {
        // AF-5: source_url link discovery — when set, urls is ignored (no silent fallback).
        IReadOnlyList<string>? discoveredLinks = null;
        if (!string.IsNullOrWhiteSpace(sourceUrl))
        {
            // Tool default max_links=8; callers that omit still get MaxUrlsCap via coalesce.
            discoveredLinks = await DiscoverLinksFromSourceAsync(
                sourceUrl,
                Math.Clamp(maxLinks ?? MaxUrlsCap, 1, MaxUrlsCap),
                focusQuery,
                cancellationToken).ConfigureAwait(false);
            if (discoveredLinks.Count == 0)
            {
                return DigestAnalysis.CreateFailed(
                    "invalid_urls",
                    DigestInputContract.EmptyDiscoveryMessage);
            }

            entries = discoveredLinks.Select(url => new DigestUrlEntry(url)).ToList();
        }
        else if (entries is null || entries.Count == 0)
        {
            return DigestAnalysis.CreateFailed(
                "invalid_arguments",
                DigestInputContract.NeitherMessage);
        }

        maxUrls = Math.Clamp(maxUrls, 1, MaxUrlsCap);
        if (entries.Count > maxUrls)
        {
            entries = entries.Take(maxUrls).ToList();
        }

        foreach (var entry in entries)
        {
            var preflight = FetchPreflight.Prepare(entry.Url, sessionProfile);
            if (!preflight.Ok)
            {
                return DigestAnalysis.CreateFailed(
                    preflight.FailureCode!,
                    preflight.FailureMessage ?? "URL blocked.");
            }
        }

        if (!string.IsNullOrWhiteSpace(sessionProfile))
        {
            var session = SessionProfileHeaders.Resolve(sessionProfile);
            if (session.Status != SessionProfileStatus.Ok)
            {
                return DigestAnalysis.CreateFailed(
                    session.FailureCode!,
                    session.Status == SessionProfileStatus.InvalidId
                        ? "session_profile id is invalid."
                        : "session_profile file was not found or could not be read.");
            }
        }

        if (perUrlMaxTokens is < MinTokenBudget)
        {
            return DigestAnalysis.CreateFailed(
                "invalid_arguments",
                $"per_url_max_tokens must be at least {MinTokenBudget}.");
        }

        if (!workerPaths.IsConfigured)
        {
            return DigestAnalysis.CreateFailed(
                "workers_unavailable",
                "Occam workers are not installed. Run occam doctor.");
        }

        var digestId = ComputeDigestId(entries.Select(e => e.Url).ToList());
        var items = await TranscodeAllEntriesAsync(
            entries,
            backendPolicy,
            focusQuery,
            fitMarkdown,
            perUrlMaxTokens,
            sessionProfile,
            cancellationToken).ConfigureAwait(false);

        var okCount = items.Count(i => i.Ok);
        var totalTokens = items.Where(i => i.Ok).Sum(i => i.TokensEstimated);
        string? combined = null;
        if (includeCombined && okCount > 0)
        {
            combined = string.Join(
                "\n\n",
                items
                    .Where(i => i.Ok && !string.IsNullOrEmpty(i.Excerpt))
                    .Select(i => $"## {i.Title ?? i.Url}\n\n{i.Excerpt}"));
        }

        if (okCount == 0)
        {
            return new DigestAnalysis(
                false,
                digestId,
                items,
                null,
                entries.Count,
                0,
                items.Count,
                0,
                "digest_failed",
                "All URLs failed to transcode.");
        }

        // AF-6: differential response for digest
        bool? unchanged = null;
        if (!string.IsNullOrWhiteSpace(ifNoneMatch) && okCount > 0)
        {
            unchanged = ContentHashToken.Matches(combined ?? string.Empty, ifNoneMatch);
        }

        var focusNotFound = FocusNotFound(items, focusQuery);

        return new DigestAnalysis(
            true,
            digestId,
            items,
            unchanged == true ? string.Empty : combined,
            entries.Count,
            okCount,
            items.Count - okCount,
            totalTokens,
            null,
            null,
            sourceUrl,
            discoveredLinks,
            unchanged,
            focusNotFound);
    }

    private static bool FocusNotFound(IReadOnlyList<DigestItemResult> items, string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return false;
        }

        var ok = items.Where(i => i.Ok).ToList();
        return ok.Count > 0 && ok.All(i => i.FocusMatched == false);
    }

    private async ValueTask<IReadOnlyList<DigestItemResult>> TranscodeAllEntriesAsync(
        IReadOnlyList<DigestUrlEntry> entries,
        OccamBackendPolicy backendPolicy,
        string? focusQuery,
        bool fitMarkdown,
        int? perUrlMaxTokens,
        string? sessionProfile,
        CancellationToken cancellationToken)
    {
        var maxParallel = DigestParallelism.ResolveMaxParallel(backendPolicy, entries.Count);
        if (maxParallel <= 1)
        {
            var sequential = new List<DigestItemResult>(entries.Count);
            foreach (var entry in entries)
            {
                cancellationToken.ThrowIfCancellationRequested();
                sequential.Add(await TranscodeEntryAsync(
                    entry,
                    focusQuery,
                    fitMarkdown,
                    perUrlMaxTokens,
                    sessionProfile,
                    backendPolicy,
                    cancellationToken).ConfigureAwait(false));
            }

            return sequential;
        }

        // C2: Task.WhenAll + a semaphore instead of Parallel.For. The old shape blocked `maxParallel`
        // thread-pool threads (each worker run pinned up to three) for the whole fan-out; awaiting costs none.
        // The scope MUST be pushed inside each task: AsyncLocal copy-on-write is per async flow, so a scope
        // pushed out here would leak across siblings instead of giving each URL its own routing decision.
        var parallelResults = new DigestItemResult[entries.Count];
        using var gate = new SemaphoreSlim(maxParallel, maxParallel);
        var tasks = new Task[entries.Count];
        for (var i = 0; i < entries.Count; i++)
        {
            var index = i;
            tasks[index] = Task.Run(async () =>
            {
                await gate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try
                {
                    using (HttpExtractRoutingScope.PushOneShot())
                    {
                        parallelResults[index] = await TranscodeEntryAsync(
                            entries[index],
                            focusQuery,
                            fitMarkdown,
                            perUrlMaxTokens,
                            sessionProfile,
                            backendPolicy,
                            cancellationToken).ConfigureAwait(false);
                    }
                }
                finally
                {
                    gate.Release();
                }
            }, cancellationToken);
        }

        await Task.WhenAll(tasks).ConfigureAwait(false);
        return parallelResults;
    }

    private async ValueTask<DigestItemResult> TranscodeEntryAsync(
        DigestUrlEntry entry,
        string? focusQuery,
        bool fitMarkdown,
        int? perUrlMaxTokens,
        string? sessionProfile,
        OccamBackendPolicy backendPolicy,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var perUrlFocus = entry.FocusQuery ?? focusQuery;
        var options = new OccamTranscodeOptions
        {
            MaxTokens = perUrlMaxTokens,
            FitMarkdown = fitMarkdown,
            FocusQuery = string.IsNullOrWhiteSpace(perUrlFocus) ? null : perUrlFocus.Trim(),
            SessionProfile = sessionProfile,
        };

        var outcome = await pipeline.TranscodeAsync(entry.Url, backendPolicy, options, cancellationToken).ConfigureAwait(false);
        if (!outcome.Ok)
        {
            var code = FailureCodeStrings.Normalize(outcome.FailureCode ?? "transcode_failed");
            if (code == "content_extraction_failed")
            {
                code = "extraction_failed";
            }

            return new DigestItemResult(
                entry.Url,
                false,
                null,
                null,
                outcome.Backend,
                0,
                code,
                outcome.Message,
                perUrlFocus,
                AccessAssessment: outcome.AccessAssessment,
                MaterializationAssessment: outcome.MaterializationAssessment);
        }

        var text = outcome.Markdown ?? string.Empty;
        var tokens = outcome.TokensEstimated ?? TokenEstimator.Estimate(text);
        bool? focusMatched = string.IsNullOrWhiteSpace(perUrlFocus)
            ? null
            : FocusMatcher.MatchesForDigest(text, perUrlFocus);

        // SI-01 for the research path: sign each per-URL extraction so digest items are independently
        // verifiable (occam_verify), like a single transcode. No per-item time anchor (would be N TSA
        // calls); json_blocks isn't requested here, so the envelope binds contentHash + provenance.
        var signed = OccamMcp.Core.Tools.OccamTranscodeResponseBuilder
            .BuildReceipt(outcome, entry.Url, ReceiptsPolicy.Enabled() ? signer : null, timeAnchor: null)?.Signed;

        return new DigestItemResult(
            entry.Url,
            true,
            ExtractTitle(text),
            text,
            outcome.Backend,
            tokens,
            null,
            null,
            perUrlFocus,
            focusMatched,
            outcome.MediaRefs,
            outcome.Confidence,
            outcome.TruncationStrategy,
            outcome.LatencyMs,
            signed,
            outcome.AccessAssessment,
            outcome.MaterializationAssessment);
    }

    public static string ComputeDigestId(IReadOnlyList<string> urls)
    {
        var normalized = urls
            .Select(u => u.Trim().TrimEnd('/'))
            .OrderBy(u => u, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var payload = string.Join("|", normalized) + "|occam_digest_slim";
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash)[..16].ToLowerInvariant();
    }

    private static string? ExtractTitle(string markdown)
    {
        foreach (var line in markdown.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('#'))
            {
                return trimmed.TrimStart('#').Trim();
            }
        }

        return null;
    }

    /// <summary>
    /// AF-5: discover links from source_url. With focus_query: shared map homepage (+ hub expand)
    /// merged with sitemap candidates, then <see cref="MapLinkRanker"/>, then max_links cap.
    /// Without focus: lighter sitemap → HTML path (intentional difference — documented in tests).
    /// </summary>
    private async Task<IReadOnlyList<string>> DiscoverLinksFromSourceAsync(
        string sourceUrl,
        int maxLinks,
        string? focusQuery,
        CancellationToken cancellationToken)
    {
        maxLinks = Math.Clamp(maxLinks, 1, MaxUrlsCap);

        if (!string.IsNullOrWhiteSpace(focusQuery))
        {
            return await DiscoverFocusedLinksAsync(sourceUrl, maxLinks, focusQuery, cancellationToken)
                .ConfigureAwait(false);
        }

        var candidateCap = maxLinks;
        try
        {
            var discovery = await SitemapDiscovery.DiscoverAsync(
                fetcher,
                sourceUrl,
                candidateCap,
                sameDomainOnly: true,
                timeoutMs: 15_000,
                robotsOnly: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            var filtered = discovery.Links
                .Where(l => !MapLinkFilter.IsNonsense(l.Url, l.Path, l.Title))
                .ToList();

            if (filtered.Count > 0)
            {
                return RankDiscoveryUrls(filtered, focusQuery, maxLinks);
            }

            return await DiscoverViaHtmlAsync(sourceUrl, maxLinks, focusQuery, candidateCap, cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // If discovery fails, return empty — caller will handle
        }

        return [];
    }

    /// <summary>
    /// Focused discovery: map homepage (hub expand) ∪ sitemap → rank all → cap.
    /// Never caps before ranking.
    /// </summary>
    private async Task<IReadOnlyList<string>> DiscoverFocusedLinksAsync(
        string sourceUrl,
        int maxLinks,
        string focusQuery,
        CancellationToken cancellationToken)
    {
        var pool = new List<MappedLink>();

        // Map homepage path: over-fetches, hub-expands when needed, returns ranked top MaxLinksCap.
        // We request the full cap so the entity page is not truncated before we merge sitemap.
        var map = await mapService.MapAsync(
            sourceUrl,
            maxLinks: MapService.MaxLinksCap,
            sameDomainOnly: true,
            timeoutMs: MapService.DefaultTimeoutMs,
            source: "homepage",
            filterNonsense: true,
            focusQuery: focusQuery,
            sessionProfile: null,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        if (map.Ok)
        {
            pool.AddRange(map.Links);
        }

        try
        {
            var sitemapCap = Math.Min(120, Math.Max(maxLinks * 8, 32));
            var discovery = await SitemapDiscovery.DiscoverAsync(
                fetcher,
                sourceUrl,
                sitemapCap,
                sameDomainOnly: true,
                timeoutMs: 15_000,
                robotsOnly: false,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            pool.AddRange(
                discovery.Links.Where(l => !MapLinkFilter.IsNonsense(l.Url, l.Path, l.Title)));
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            // Sitemap optional when homepage map already populated the pool.
        }

        if (pool.Count == 0)
        {
            return await DiscoverViaHtmlAsync(
                    sourceUrl,
                    maxLinks,
                    focusQuery,
                    Math.Min(120, Math.Max(maxLinks * 8, 32)),
                    cancellationToken)
                .ConfigureAwait(false);
        }

        var deduped = DeduplicateMapped(pool);
        return RankDiscoveryUrls(deduped, focusQuery, maxLinks);
    }

    private static List<MappedLink> DeduplicateMapped(IReadOnlyList<MappedLink> links)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<MappedLink>();
        foreach (var link in links)
        {
            if (!seen.Add(link.Url))
            {
                continue;
            }

            unique.Add(link);
        }

        return unique;
    }

    private async Task<IReadOnlyList<string>> DiscoverViaHtmlAsync(
        string sourceUrl,
        int maxLinks,
        string? focusQuery,
        int candidateCap,
        CancellationToken cancellationToken)
    {
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            var fetch = await fetcher.FetchAsync(
                sourceUrl,
                timeoutMs: 15_000,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!fetch.Ok || string.IsNullOrWhiteSpace(fetch.HtmlSample))
            {
                return [];
            }

            var htmlLinks = HtmlLinkExtractor.Extract(
                fetch.HtmlSample,
                fetch.FinalUrl ?? sourceUrl,
                candidateCap);
            var kept = htmlLinks
                .Where(l => !MapLinkFilter.IsNonsense(l.Url, l.Path, l.Title))
                .ToList();
            return RankDiscoveryUrls(kept, focusQuery, maxLinks);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch
        {
            return [];
        }
    }

    private static IReadOnlyList<string> RankDiscoveryUrls(
        IReadOnlyList<MappedLink> links,
        string? focusQuery,
        int maxLinks) =>
        MapLinkRanker.Rank(links, focusQuery, maxLinks).Select(l => l.Url).ToList();
}
