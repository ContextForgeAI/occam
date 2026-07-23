using System.Collections.Concurrent;
using System.Diagnostics;

namespace OccamMcp.Core.Workers;

/// <summary>
/// Tracks consecutive Node worker spawns; forces recycle after 10 runs or any timeout.
/// Recycle = kill tree + reset counters (Playwright/browser state must not leak across failures).
/// </summary>
public sealed class NodeWorkerLifecycle
{
    private const int RecycleAfterConsecutiveRuns = 10;

    private static readonly ConcurrentDictionary<string, NodeWorkerLifecycle> Sessions = new();

    private int _consecutiveRuns;
    private volatile bool _recyclePending;

    public static NodeWorkerLifecycle For(string backend) =>
        Sessions.GetOrAdd(backend, _ => new NodeWorkerLifecycle());

    public bool ConsumeRecyclePending()
    {
        if (!_recyclePending)
        {
            return false;
        }

        _recyclePending = false;
        _consecutiveRuns = 0;
        return true;
    }

    public void OnRunStarted() { }

    public void OnRunCompleted(bool timedOut, bool crashed)
    {
        if (timedOut || crashed)
        {
            _recyclePending = true;
            _consecutiveRuns = 0;
            return;
        }

        _consecutiveRuns++;
        if (_consecutiveRuns >= RecycleAfterConsecutiveRuns)
        {
            _recyclePending = true;
            _consecutiveRuns = 0;
        }
    }

    public static void KillProcessTree(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    /// <summary>
    /// Kill the process tree (if still running), unregister it from the shutdown process group,
    /// AND release the native handle. Use at sites that
    /// relinquish ownership of the <see cref="Process"/> object — daemon/slot stop, idle recycle —
    /// where the field is about to be nulled. Plain <see cref="KillProcessTree"/> leaves the Win32
    /// process/wait handle to be reclaimed only when the finalizer eventually runs; a host that
    /// repeatedly starts and stops workers (health restart, idle TTL) accumulates those handles.
    /// Dispose is unconditional so an already-exited process is released too. (Not folded into
    /// KillProcessTree: other callers read process state after killing.)
    /// </summary>
    public static void TerminateAndDispose(Process? process)
    {
        if (process is null)
        {
            return;
        }

        try
        {
            KillProcessTree(process);
        }
        finally
        {
            try
            {
                WorkerProcessGroup.Release(process);
            }
            finally
            {
                process.Dispose();
            }
        }
    }
}
