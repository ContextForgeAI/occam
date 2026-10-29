using System.Text;
using System.Text.Json;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;

namespace OccamMcp.Core.Crawl;

/// <summary>
/// Env-gated crawl MVP (ADR-0018): map seed → queue → sequential transcode under budgets.
/// Resume token is caller-held queue state (not a durable job store).
/// </summary>
public interface ICrawlService
{
    Task<CrawlOutcome> CrawlAsync(
        string? seedUrl,
        string? resumeToken,
        int maxPages,
        int maxLinksDiscover,
        string mapSource,
        OccamBackendPolicy backendPolicy,
        string? focusQuery,
        bool fitMarkdown,
        int? perUrlMaxTokens,
        string? sessionProfile,
        string playbookPolicy,
        bool includeJsonl,
        CancellationToken cancellationToken);
}

public sealed class CrawlService(MapService mapService, TranscodePipeline pipeline) : ICrawlService
{
    public const int MaxPagesCap = 32;
    public const int DefaultMaxPages = 8;
    private const int ResumeVersion = 1;

    public async Task<CrawlOutcome> CrawlAsync(
        string? seedUrl,
        string? resumeToken,
        int maxPages,
        int maxLinksDiscover,
        string mapSource,
        OccamBackendPolicy backendPolicy,
        string? focusQuery,
        bool fitMarkdown,
        int? perUrlMaxTokens,
        string? sessionProfile,
        string playbookPolicy,
        bool includeJsonl,
        CancellationToken cancellationToken)
    {
        maxPages = Math.Clamp(maxPages, 1, MaxPagesCap);
        maxLinksDiscover = Math.Clamp(maxLinksDiscover, 1, MapService.MaxLinksCap);

        List<string> queue;
        string seed;
        if (!string.IsNullOrWhiteSpace(resumeToken))
        {
            if (!TryDecodeResume(resumeToken, out var state, out var decodeError))
            {
                return CrawlOutcome.Fail(seedUrl ?? "", "invalid_arguments", decodeError!);
            }

            seed = state.Seed;
            queue = [.. state.Queue];
        }
        else
        {
            if (string.IsNullOrWhiteSpace(seedUrl)
                || !Uri.TryCreate(seedUrl, UriKind.Absolute, out _))
            {
                return CrawlOutcome.Fail(seedUrl ?? "", "invalid_url", "seed url is required when resume_token is omitted.");
            }

            seed = seedUrl.Trim();
            queue = await BuildQueueAsync(seed, maxLinksDiscover, mapSource, sessionProfile, focusQuery, cancellationToken)
                .ConfigureAwait(false);
            if (queue.Count == 0)
            {
                return CrawlOutcome.Fail(seed, "crawl_empty", "No crawlable URLs discovered from seed.");
            }
        }

        var batch = queue.Take(maxPages).ToList();
        var remaining = queue.Skip(maxPages).ToList();
        var pages = new List<CrawlPageResult>(batch.Count);
        var succeeded = 0;
        var failed = 0;

        foreach (var url in batch)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var options = new OccamTranscodeOptions
            {
                MaxTokens = perUrlMaxTokens,
                FitMarkdown = fitMarkdown,
                FocusQuery = string.IsNullOrWhiteSpace(focusQuery) ? null : focusQuery.Trim(),
                SessionProfile = sessionProfile,
                PlaybookPolicy = playbookPolicy,
            };

            var outcome = await pipeline.TranscodeAsync(url, backendPolicy, options, cancellationToken)
                .ConfigureAwait(false);
            if (outcome.Ok)
            {
                succeeded++;
                pages.Add(new CrawlPageResult(
                    url,
                    true,
                    Title: null,
                    outcome.Markdown,
                    null,
                    outcome.Backend,
                    outcome.TokensEstimated ?? TokenEstimator.Estimate(outcome.Markdown ?? string.Empty),
                    outcome.LatencyMs));
            }
            else
            {
                failed++;
                pages.Add(new CrawlPageResult(
                    url,
                    false,
                    null,
                    null,
                    FailureCodeStrings.Normalize(outcome.FailureCode ?? "extraction_failed"),
                    outcome.Backend,
                    0,
                    outcome.LatencyMs));
            }
        }

        string? nextResume = remaining.Count == 0
            ? null
            : EncodeResume(new CrawlResumeState(ResumeVersion, seed, [.. remaining]));

        string? jsonl = null;
        if (includeJsonl)
        {
            var sb = new StringBuilder();
            foreach (var page in pages)
            {
                sb.AppendLine(JsonSerializer.Serialize(page, CrawlJsonContext.Default.CrawlPageResult));
            }

            jsonl = sb.ToString();
        }

        return new CrawlOutcome(
            Ok: true,
            SeedUrl: seed,
            Pages: pages,
            Requested: batch.Count,
            Succeeded: succeeded,
            Failed: failed,
            ResumeToken: nextResume,
            Remaining: remaining.Count,
            Jsonl: jsonl);
    }

    private async Task<List<string>> BuildQueueAsync(
        string seed,
        int maxLinks,
        string mapSource,
        string? sessionProfile,
        string? focusQuery,
        CancellationToken cancellationToken)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { seed };
        var queue = new List<string> { seed };

        var sources = string.Equals(mapSource, "homepage", StringComparison.OrdinalIgnoreCase)
            ? new[] { "homepage" }
            : new[] { "sitemap", "homepage" };

        foreach (var source in sources)
        {
            var map = await mapService.MapAsync(
                seed,
                maxLinks: maxLinks,
                sameDomainOnly: true,
                source: source,
                focusQuery: focusQuery,
                sessionProfile: sessionProfile,
                cancellationToken: cancellationToken).ConfigureAwait(false);
            if (!map.Ok || map.Links.Count == 0)
            {
                continue;
            }

            foreach (var link in map.Links)
            {
                if (string.IsNullOrWhiteSpace(link.Url) || !seen.Add(link.Url))
                {
                    continue;
                }

                queue.Add(link.Url);
                if (queue.Count >= maxLinks + 1)
                {
                    return queue;
                }
            }

            if (queue.Count > 1)
            {
                break;
            }
        }

        return queue;
    }

    private static string EncodeResume(CrawlResumeState state)
    {
        var json = JsonSerializer.Serialize(state, CrawlJsonContext.Default.CrawlResumeState);
        return Convert.ToBase64String(Encoding.UTF8.GetBytes(json));
    }

    private static bool TryDecodeResume(string token, out CrawlResumeState state, out string? error)
    {
        state = null!;
        error = null;
        try
        {
            var json = Encoding.UTF8.GetString(Convert.FromBase64String(token.Trim()));
            var parsed = JsonSerializer.Deserialize(json, CrawlJsonContext.Default.CrawlResumeState);
            if (parsed is null || parsed.Version != ResumeVersion || string.IsNullOrWhiteSpace(parsed.Seed))
            {
                error = "resume_token is malformed or unsupported version.";
                return false;
            }

            state = parsed with { Queue = parsed.Queue ?? [] };
            return true;
        }
        catch (Exception)
        {
            error = "resume_token must be a base64 CrawlResumeState payload from a prior crawl.";
            return false;
        }
    }
}
