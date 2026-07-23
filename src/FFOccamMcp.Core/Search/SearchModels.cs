namespace OccamMcp.Core.Search;

/// <summary>One normalized web-search hit. Provider-agnostic shape.</summary>
public sealed record SearchResultItem(string Title, string Url, string? Snippet);

/// <summary>Outcome of a search provider call: either results or a typed failure.</summary>
public sealed record SearchOutcome(
    bool Ok,
    string Provider,
    IReadOnlyList<SearchResultItem> Results,
    string? FailureCode,
    int LatencyMs)
{
    public static SearchOutcome Success(string provider, IReadOnlyList<SearchResultItem> results, int latencyMs) =>
        new(true, provider, results, null, latencyMs);

    public static SearchOutcome Failure(string provider, string failureCode, int latencyMs) =>
        new(false, provider, [], failureCode, latencyMs);
}

internal static class SearchElapsed
{
    public static int Ms(long startedTimestamp) =>
        (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;

    /// <summary>Maps a thrown exception to a typed failure code.</summary>
    public static string FailureFor(Exception ex) =>
        ex is TaskCanceledException or OperationCanceledException ? "search_timeout" : "search_error";

    public static string? Trim(string? value)
    {
        var t = value?.Replace('\n', ' ').Replace('\r', ' ').Trim();
        return string.IsNullOrEmpty(t) ? null : t;
    }
}
