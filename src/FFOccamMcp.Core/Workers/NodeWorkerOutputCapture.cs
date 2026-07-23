using System.Diagnostics;

namespace OccamMcp.Core.Workers;

public static class NodeWorkerOutputCapture
{
    public sealed record CaptureResult(string StdOut, string StdErr, bool TimedOut, int ExitCode);

    /// <summary>
    /// Drains redirected stdout/stderr while waiting for exit — avoids pipe-buffer deadlock with Playwright workers.
    /// <para>
    /// C2: fully async. This used to burn THREE thread-pool threads per worker call — one blocked in
    /// <c>WaitForExitAsync().GetAwaiter().GetResult()</c> plus two <c>Task.Run(() =&gt; ReadToEnd())</c> readers —
    /// for the whole life of the child process (up to minutes). Under the digest's parallel fan-out that was
    /// 3 × maxParallel threads pinned doing nothing. Awaiting instead costs zero threads while the worker runs.
    /// </para>
    /// </summary>
    public static async Task<CaptureResult> RunAsync(Process process, int timeoutMs, CancellationToken cancellationToken = default)
    {
        var stdoutTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        var stderrTask = process.StandardError.ReadToEndAsync(cancellationToken);

        using var timeoutCts = new CancellationTokenSource(timeoutMs);
        using var linkedCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutCts.Token);
        var timedOut = false;
        try
        {
            await process.WaitForExitAsync(linkedCts.Token).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            NodeWorkerLifecycle.KillProcessTree(process);
            cancellationToken.ThrowIfCancellationRequested();
            throw;
        }
        catch (OperationCanceledException)
        {
            timedOut = true;
            NodeWorkerLifecycle.KillProcessTree(process);
        }

        string stdout;
        string stderr;
        if (timedOut)
        {
            // The tree is killed, so the pipes are closing; give the readers a moment, never block on them.
            stdout = await SafeReadAsync(stdoutTask).ConfigureAwait(false);
            stderr = await SafeReadAsync(stderrTask).ConfigureAwait(false);
        }
        else
        {
            stdout = await stdoutTask.ConfigureAwait(false);
            stderr = await stderrTask.ConfigureAwait(false);
        }

        return new CaptureResult(stdout, stderr, timedOut, timedOut ? -1 : process.ExitCode);
    }

    public static string? TryParseLastJsonLine(string stdout)
    {
        if (string.IsNullOrWhiteSpace(stdout))
        {
            return null;
        }

        foreach (var line in stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).Reverse())
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith('{') && trimmed.EndsWith('}'))
            {
                return trimmed;
            }
        }

        return null;
    }

    private static async Task<string> SafeReadAsync(Task<string> task)
    {
        try
        {
            return await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
        }
        catch
        {
            return string.Empty;
        }
    }

}
