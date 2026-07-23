namespace OccamMcp.Core.Workers;

/// <summary>N-slot browser-daemon pool — one Chromium per slot, host-side acquire/release.</summary>
public interface IBrowserPoolManager
{
  int PoolSize { get; }
  bool IsEnabled { get; }

  Task<int> GetHealthySlotsAsync();

  Task<bool> TryEnsureMinimumHealthyAsync(WorkerPaths paths);

  ValueTask<BrowserPoolSlot> AcquireSlotAsync(CancellationToken cancellationToken);

  void ReleaseSlot(BrowserPoolSlot slot, bool ok = true, int extractMs = 0);

  void MarkActivity(BrowserPoolSlot slot);

  void StopAll();

  void StopSlot(BrowserPoolSlot slot);
}
