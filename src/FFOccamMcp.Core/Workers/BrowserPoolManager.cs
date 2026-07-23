using System.Diagnostics;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Workers;

/// <summary>Manages N long-lived <c>browser-daemon.mjs</c> slots with round-robin assignment.</summary>
public sealed class BrowserPoolManager : IBrowserPoolManager
{
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(15);
    private static BrowserPoolManager? _shared;

    private readonly BrowserPoolSettings _settings;
    private readonly IOccamTelemetrySink _telemetry;
    private readonly IBrowserDaemonClient _browserDaemonClient;
    private readonly BrowserConcurrencyLimiter _concurrencyLimiter;
    private readonly object _gate = new();
    private readonly SlotState[] _slots;
    private int _roundRobinIndex;
    private int _pendingAcquires;
    private Timer? _idleTimer;
    private bool _shutdownHooked;
    private WorkerPaths? _paths;

    public BrowserPoolManager()
        : this(BrowserPoolSettings.ReadFromEnvironment(), NullOccamTelemetrySink.Instance, NullBrowserDaemonClient.Instance)
    {
    }

    public BrowserPoolManager(BrowserPoolSettings settings, IOccamTelemetrySink telemetry, IBrowserDaemonClient browserDaemonClient)
    {
        _settings = settings;
        _telemetry = telemetry;
        _browserDaemonClient = browserDaemonClient;
        _concurrencyLimiter = new BrowserConcurrencyLimiter(settings);
        _slots = new SlotState[settings.PoolSize];
        for (var i = 0; i < settings.PoolSize; i++)
        {
            _slots[i] = new SlotState(i, settings.ResolvePortForSlot(i));
        }
    }

    public static BrowserPoolManager Shared => _shared ??= new BrowserPoolManager();

    internal static void InstallShared(BrowserPoolManager manager)
    {
        _shared?.StopAll();
        _shared = manager;
    }

    public int PoolSize => _settings.PoolSize;

    public bool IsEnabled => _settings.IsEnabled;

    public async Task<int> GetHealthySlotsAsync()
    {
        var count = 0;
        foreach (var slot in _slots)
        {
            if (await slot.IsHealthyUnsafeAsync(_browserDaemonClient))
            {
                count++;
            }
        }
        return count;
    }

    public async Task<bool> TryEnsureMinimumHealthyAsync(WorkerPaths paths)
    {
        if (!IsEnabled)
        {
            return false;
        }

        _paths = paths;
        EnsureShutdownHook();
        EnsureIdleMonitor();

        var anyHealthy = false;
        for (var i = 0; i < _slots.Length; i++)
        {
            SlotState slot;
            lock (_gate)
            {
                slot = _slots[i];
            }
            if (await EnsureSlotRunningUnsafeAsync(slot, paths))
            {
                anyHealthy = true;
            }
        }

        return anyHealthy;
    }

    public async ValueTask<BrowserPoolSlot> AcquireSlotAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var waitStarted = Stopwatch.GetTimestamp();
        // Depth snapshot for telemetry: how many acquirers are waiting on the gate right now.
        // Decrement the instant we stop waiting (the AcquireAsync returns or throws) so the
        // counter reflects live queue depth and never leaks on cancellation.
        Interlocked.Increment(ref _pendingAcquires);
        IDisposable releaser;
        try
        {
            releaser = await _concurrencyLimiter.AcquireAsync(cancellationToken);
        }
        finally
        {
            Interlocked.Decrement(ref _pendingAcquires);
        }

        try
        {
            var waitMs = (int)((Stopwatch.GetTimestamp() - waitStarted) * 1000 / Stopwatch.Frequency);
            var paths = _paths ?? WorkerPaths.Resolve();
            if (!paths.IsConfigured)
            {
                throw new InvalidOperationException("Browser pool requires configured worker paths.");
            }

            _paths = paths;
            EnsureShutdownHook();
            EnsureIdleMonitor();

            var slot = await PickHealthySlotUnsafeAsync(paths) ?? throw new InvalidOperationException("No healthy browser pool slot available.");
            slot.MarkActivityUnsafe();
            var lease = new BrowserPoolSlot(slot.SlotId, slot.Port, DateTime.UtcNow.Ticks) { Releaser = releaser };
            _telemetry.OnBrowserPoolAcquired(lease, waitMs, Volatile.Read(ref _pendingAcquires));
            return lease;
        }
        catch
        {
            releaser.Dispose();
            throw;
        }
    }

    public void ReleaseSlot(BrowserPoolSlot slot, bool ok = true, int extractMs = 0)
    {
        _telemetry.OnBrowserPoolReleased(slot, ok, extractMs);
        // Free the global + pool concurrency gates. Idempotent (see Releaser.Dispose) so the
        // runner's catch+finally double-release on cancellation is safe.
        slot.Releaser?.Dispose();
    }

    public void MarkActivity(BrowserPoolSlot slot)
    {
        lock (_gate)
        {
            if (slot.SlotId >= 0 && slot.SlotId < _slots.Length)
            {
                _slots[slot.SlotId].MarkActivityUnsafe();
            }
        }
    }

    public void StopAll()
    {
        lock (_gate)
        {
            foreach (var slot in _slots)
            {
                slot.StopUnsafe();
            }
        }
    }

    public void StopSlot(BrowserPoolSlot slot)
    {
        lock (_gate)
        {
            if (slot.SlotId >= 0 && slot.SlotId < _slots.Length)
            {
                _slots[slot.SlotId].StopUnsafe();
            }
        }
    }

#if OCCAM_GATE
    internal static void ResetSharedForTests()
    {
        _shared?.StopAll();
        _shared = null;
    }

    internal int PickNextSlotIndexForTests(bool[] healthyMask)
    {
        lock (_gate)
        {
            return PickNextSlotIndexUnsafe(healthyMask);
        }
    }

    internal int ResolvePortForSlotForTests(int slotId) => _settings.ResolvePortForSlot(slotId);

    internal int PendingAcquiresForTests => Volatile.Read(ref _pendingAcquires);

    internal void SetPathsForTests(WorkerPaths paths) => _paths = paths;
#endif

    private async Task<SlotState?> PickHealthySlotUnsafeAsync(WorkerPaths paths)
    {
        if (_slots.Length == 0)
        {
            return null;
        }

        var healthyMask = new bool[_slots.Length];
        for (var i = 0; i < _slots.Length; i++)
        {
            healthyMask[i] = await _slots[i].IsHealthyUnsafeAsync(_browserDaemonClient);
        }

        for (var attempt = 0; attempt < _slots.Length; attempt++)
        {
            var index = PickNextSlotIndexUnsafe(healthyMask);
            var slot = _slots[index];
            if (healthyMask[index] || await EnsureSlotRunningUnsafeAsync(slot, paths))
            {
                return slot;
            }

            healthyMask[index] = false;
        }

        return null;
    }

    private int PickNextSlotIndexUnsafe(bool[] healthyMask)
    {
        for (var offset = 0; offset < healthyMask.Length; offset++)
        {
            var index = (_roundRobinIndex + offset) % healthyMask.Length;
            if (healthyMask[index])
            {
                _roundRobinIndex = (index + 1) % healthyMask.Length;
                return index;
            }
        }

        var fallback = _roundRobinIndex % healthyMask.Length;
        _roundRobinIndex = (fallback + 1) % healthyMask.Length;
        return fallback;
    }

        private async Task<bool> EnsureSlotRunningUnsafeAsync(SlotState slot, WorkerPaths paths)
        {
            if (await slot.IsHealthyUnsafeAsync(_browserDaemonClient))
            {
                slot.MarkActivityUnsafe();
                return true;
            }

            // Serialise the spawn per slot so concurrent first-time callers don't each launch a daemon on
            // the same port (the EADDRINUSE race). Losers wait here, then the double-check below finds the
            // winner's healthy daemon instead of spawning a doomed duplicate.
            await slot.SpawnGate.WaitAsync().ConfigureAwait(false);
            try
            {
                if (await slot.IsHealthyUnsafeAsync(_browserDaemonClient))
                {
                    slot.MarkActivityUnsafe();
                    return true;
                }

                return await SpawnAndWaitHealthyAsync(slot, paths);
            }
            finally
            {
                slot.SpawnGate.Release();
            }
        }

        private async Task<bool> SpawnAndWaitHealthyAsync(SlotState slot, WorkerPaths paths)
        {
            if (slot.Process is { HasExited: false })
            {
                if (await WaitForHealthyAsync(slot.Port, TimeSpan.FromSeconds(8)))
                {
                    slot.MarkActivityUnsafe();
                    return true;
                }
            }

            var script = ResolveDaemonScript(paths);
            if (script is null)
            {
                return false;
            }

            slot.StopUnsafe();

            var psi = new ProcessStartInfo
            {
                FileName = NodeRuntime.ResolveExecutable(),
                Arguments = NodeLaunchArguments.Build(
                    browser: true,
                    $"\"{script}\"",
                    $"--port={slot.Port}"),
                // stdout captured + drained (never corrupt the host's MCP stdout); stderr inherited so the
                // daemon's diagnostics reach the host's stderr rather than a never-read pipe that deadlocks.
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            psi.Environment["OCCAM_BROWSER_POOL_SLOT_ID"] = slot.SlotId.ToString();
            PlaywrightEnvironment.ApplyTo(psi);
            EgressProxyConfig.ApplyTo(psi);

            slot.Process = WorkerProcessGroup.Start(psi);
            WorkerProcessGroup.DrainStandardOutput(slot.Process);
            if (slot.Process is null)
            {
                return false;
            }

            var started = await WaitForHealthyAsync(slot.Port, TimeSpan.FromSeconds(12));
            if (started)
            {
                slot.MarkActivityUnsafe();
            }

            return started;
        }

    private void EnsureShutdownHook()
    {
        if (_shutdownHooked)
        {
            return;
        }

        _shutdownHooked = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopAll();
    }

    private void EnsureIdleMonitor()
    {
        if (_idleTimer is not null || _settings.IdleTtlMs <= 0)
        {
            return;
        }

        lock (_gate)
        {
            _idleTimer ??= new Timer(_ => OnIdleTimerTick(), null, IdlePollInterval, IdlePollInterval);
        }
    }

    private void OnIdleTimerTick()
    {
        var ttlMs = _settings.IdleTtlMs;
        if (ttlMs <= 0)
        {
            return;
        }

        lock (_gate)
        {
            var nowTicks = DateTime.UtcNow.Ticks;
            foreach (var slot in _slots)
            {
                if (slot.Process is not { HasExited: false })
                {
                    continue;
                }

                var idleMs = (nowTicks - slot.LastActivityUtcTicks) / TimeSpan.TicksPerMillisecond;
                if (idleMs >= ttlMs)
                {
                    slot.StopUnsafe();
                }
            }
        }
    }

    private async Task<bool> WaitForHealthyAsync(int port, TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (await _browserDaemonClient.IsHealthyAsync(port, CancellationToken.None))
            {
                return true;
            }

            await Task.Delay(200);
        }

        return await _browserDaemonClient.IsHealthyAsync(port, CancellationToken.None);
    }

    private static string? ResolveDaemonScript(WorkerPaths paths)
    {
        var overridePath = Environment.GetEnvironmentVariable("OCCAM_BROWSER_DAEMON_SCRIPT");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var root = WorkerPaths.ResolveOccamHome();
        if (root is null)
        {
            return null;
        }

        var candidate = Path.Combine(root, "workers", "browser-extract", "browser-daemon.mjs");
        return File.Exists(candidate) ? candidate : null;
    }

    private sealed class SlotState(int slotId, int port)
    {
        public int SlotId { get; } = slotId;
        public int Port { get; } = port;
        public Process? Process { get; set; }
        public long LastActivityUtcTicks { get; private set; } = DateTime.UtcNow.Ticks;

        // Serialises the daemon SPAWN for this slot. The manager's lock (_gate) can't cover it — spawning
        // awaits a health probe, and you can't await under a lock — so without this two concurrent
        // first-time callers both see "unhealthy" and each launch a daemon on the same port, and the losers
        // crash with EADDRINUSE. An async per-slot gate lets the losers wait and then find the winner's daemon.
        public SemaphoreSlim SpawnGate { get; } = new(1, 1);

        public async Task<bool> IsHealthyUnsafeAsync(IBrowserDaemonClient client) => await client.IsHealthyAsync(Port, CancellationToken.None);

        public void MarkActivityUnsafe() => LastActivityUtcTicks = DateTime.UtcNow.Ticks;

        public void StopUnsafe()
        {
            NodeWorkerLifecycle.TerminateAndDispose(Process);
            Process = null;
        }
    }
}

internal sealed class NullOccamTelemetrySink : IOccamTelemetrySink
{
    public static NullOccamTelemetrySink Instance { get; } = new();

    public void OnTranscodeCompleted(TranscodeContext ctx, TranscodeOutcome outcome)
    {
    }

    public void OnTranscodeFailed(TranscodeContext ctx, TranscodeOutcome outcome)
    {
    }

    public void OnBrowserPoolAcquired(BrowserPoolSlot slot, int waitMs, int pendingDepth)
    {
    }

    public void OnBrowserPoolReleased(BrowserPoolSlot slot, bool ok, int extractMs)
    {
    }
}

internal sealed class NullBrowserDaemonClient : IBrowserDaemonClient
{
    public static NullBrowserDaemonClient Instance { get; } = new();

    public Task<bool> IsHealthyAsync(int port, CancellationToken cancellationToken) => Task.FromResult(false);

    public Task<ExtractRunResult?> TryExtractAsync(string url, int timeoutMs, bool forceRecycle, string? headersFile, string? storageStateFile, CancellationToken cancellationToken, int port = 0, string? features = null, string? playbookOverlayJson = null, bool playbookOverlayStrict = false) => Task.FromResult<ExtractRunResult?>(null);

    public Task<string?> TryCaptureSkeletonJsonAsync(string url, int maxNodes, int timeoutMs, string? headersFile, CancellationToken cancellationToken, int port = 0) => Task.FromResult<string?>(null);
}
