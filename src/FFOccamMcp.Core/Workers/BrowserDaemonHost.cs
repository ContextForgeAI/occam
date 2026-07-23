using System.Diagnostics;
using System.Threading.Tasks;
using OccamMcp.Core.Abstractions;

namespace OccamMcp.Core.Workers;

/// <summary>Backward-compatible facade over <see cref="IBrowserPoolManager"/>.</summary>
public static class BrowserDaemonHost
{
    public static bool IsEnabled => BrowserPoolManager.Shared.IsEnabled;

    public static int Port => BrowserPoolSettings.ReadFromEnvironment().ResolvePortForSlot(0);

    public static int IdleTtlMs => BrowserPoolSettings.ReadFromEnvironment().IdleTtlMs;

    public static async Task<bool> TryEnsureRunningAsync(WorkerPaths paths) =>
        await BrowserPoolManager.Shared.TryEnsureMinimumHealthyAsync(paths);

    public static void MarkActivity() =>
        BrowserPoolManager.Shared.MarkActivity(new BrowserPoolSlot(0, Port, DateTime.UtcNow.Ticks));

    public static void Stop() => BrowserPoolManager.Shared.StopAll();
}
