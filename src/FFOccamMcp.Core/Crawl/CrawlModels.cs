using System.Text.Json.Serialization;

namespace OccamMcp.Core.Crawl;

public sealed record CrawlPageResult(
    string Url,
    bool Ok,
    string? Title,
    string? Markdown,
    string? FailureCode,
    string? Backend,
    int TokensEstimated,
    int LatencyMs);

public sealed record CrawlOutcome(
    bool Ok,
    string SeedUrl,
    IReadOnlyList<CrawlPageResult> Pages,
    int Requested,
    int Succeeded,
    int Failed,
    string? ResumeToken,
    int Remaining,
    string? FailureCode = null,
    string? FailureMessage = null,
    string? Jsonl = null)
{
    public static CrawlOutcome Fail(string seed, string code, string message) =>
        new(false, seed, [], 0, 0, 0, null, 0, code, message);
}

/// <summary>Opaque resume payload (not a security boundary — caller-held queue state).</summary>
public sealed record CrawlResumeState(
    [property: JsonPropertyName("v")] int Version,
    [property: JsonPropertyName("seed")] string Seed,
    [property: JsonPropertyName("q")] string[] Queue);
