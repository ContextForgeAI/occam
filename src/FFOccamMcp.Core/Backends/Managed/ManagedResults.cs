using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends.Managed;

internal static class ManagedElapsed
{
    public static int Ms(long startedTimestamp) =>
        (int)System.Diagnostics.Stopwatch.GetElapsedTime(startedTimestamp).TotalMilliseconds;
}

/// <summary>Normalizes managed-provider outcomes to <see cref="ExtractRunResult"/>.</summary>
internal static class ManagedResults
{
    public static string BackendName(string provider) => $"managed_{provider}";

    public static ExtractRunResult FromMarkdown(string provider, string? markdown, string url, int elapsedMs)
    {
        var trimmed = markdown?.Trim();
        if (string.IsNullOrEmpty(trimmed))
        {
            return new ExtractRunResult(false, null, BackendName(provider), "extraction_failed", elapsedMs, url, false);
        }

        return new ExtractRunResult(true, trimmed, BackendName(provider), null, elapsedMs, url, false);
    }

    public static ExtractRunResult Failure(string provider, int statusCode, int elapsedMs) =>
        new(false, null, BackendName(provider), $"http_{statusCode}", elapsedMs, null, false, statusCode);

    public static ExtractRunResult Exception(string provider, Exception ex, int elapsedMs)
    {
        var timedOut = ex is TaskCanceledException or OperationCanceledException;
        return new ExtractRunResult(
            false, null, BackendName(provider), timedOut ? "timeout" : "managed_error", elapsedMs, null, timedOut);
    }
}
