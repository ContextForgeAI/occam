namespace OccamMcp.Core.Probe;

public sealed class ProbeFetchResult
{
    public required bool Ok { get; init; }
    public int StatusCode { get; init; }
    public string? ContentType { get; init; }
    public string RequestedUrl { get; init; } = "";
    public string? FinalUrl { get; init; }
    public IReadOnlyList<string>? RedirectChain { get; init; }
    public int HtmlBytes { get; init; }
    public string? HtmlSample { get; init; }
    public string? FailureCode { get; init; }
    public int LatencyMs { get; init; }
    public bool IsPdf { get; init; }
    public bool HasAuthenticationChallenge { get; init; }
}
