using System.Diagnostics;
using System.Text.Json;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Workers;

/// <summary>Low-level process spawn and output capture.</summary>
public sealed class NodeWorkerProcessSpawner : IWorkerProcessSpawner
{
    public ExtractRunResult SpawnAsync(
        ProcessStartInfo psi,
        int timeoutMs,
        CancellationToken cancellationToken,
        NodeWorkerLifecycle lifecycle)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var process = WorkerProcessGroup.Start(psi);
        if (process is null)
        {
            lifecycle.OnRunCompleted(timedOut: false, crashed: true);
            return new ExtractRunResult(false, null, null, "spawn_failed", 0, null, false);
        }

        try
        {
            var capture = NodeWorkerOutputCapture.RunAsync(process, timeoutMs, cancellationToken).GetAwaiter().GetResult();
            if (capture.TimedOut)
            {
                lifecycle.OnRunCompleted(timedOut: true, crashed: false);
                return new ExtractRunResult(false, null, null, "timeout", timeoutMs, null, true);
            }

            var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(capture.StdOut);
            if (jsonLine is null)
            {
                lifecycle.OnRunCompleted(timedOut: false, crashed: true);
                return new ExtractRunResult(
                    false,
                    null,
                    null,
                    SummarizeNoJsonFailure(capture.StdErr, capture.ExitCode),
                    0,
                    null,
                    false,
                    0,
                    null);
            }

            var payload = JsonSerializer.Deserialize(jsonLine, WorkerExtractJsonContext.Default.WorkerExtractResponse);
            if (payload is null)
            {
                lifecycle.OnRunCompleted(timedOut: false, crashed: true);
                return new ExtractRunResult(false, null, null, "bad_json", 0, null, false);
            }

            if (!payload.Ok)
            {
                lifecycle.OnRunCompleted(
                    timedOut: IsTimeoutFailure(payload.Failure),
                    crashed: !IsTimeoutFailure(payload.Failure));
                return new ExtractRunResult(
                    false,
                    payload.Markdown,
                    payload.Backend,
                    payload.Failure,
                    payload.LatencyMs,
                    payload.Url?.Final,
                    IsTimeoutFailure(payload.Failure),
                    payload.StatusCode,
                    MediaRefs: MediaRefMapper.Map(payload.MediaRefs));
            }

            lifecycle.OnRunCompleted(timedOut: false, crashed: false);
            return new ExtractRunResult(
                payload.Ok,
                payload.Markdown,
                payload.Backend,
                payload.Failure,
                payload.LatencyMs,
                payload.Url?.Final,
                false,
                payload.StatusCode,
                MediaRefs: MediaRefMapper.Map(payload.MediaRefs));
        }
        catch
        {
            lifecycle.OnRunCompleted(timedOut: false, crashed: true);
            return new ExtractRunResult(false, null, null, "spawn_failed", 0, null, false);
        }
        finally
        {
            WorkerProcessGroup.Release(process);
        }
    }

    private static bool IsTimeoutFailure(string? failure) =>
        failure is "timeout" or "aborterror"
        || (failure?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string SummarizeNoJsonFailure(string stderr, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return exitCode == 0 ? "no_json" : $"no_json:exit_{exitCode}";
        }

        var tail = stderr.Trim();
        if (tail.Length > 240)
        {
            tail = tail[^240..];
        }

        tail = tail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(tail) ? "no_json" : $"no_json:{tail}";
    }
}