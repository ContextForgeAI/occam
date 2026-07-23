namespace OccamMcp.Core.Workers;

/// <summary>Lease on one browser-daemon slot in the pool.</summary>
public sealed class BrowserPoolSlot
{
    internal BrowserPoolSlot(int slotId, int port, long acquireTimestampUtcTicks)
    {
        SlotId = slotId;
        Port = port;
        AcquireTimestampUtcTicks = acquireTimestampUtcTicks;
    }

    public int SlotId { get; }
    public int Port { get; }
    public long AcquireTimestampUtcTicks { get; }

    /// <summary>
    /// Concurrency-limiter lease held for the lifetime of this slot. Disposed by
    /// <see cref="BrowserPoolManager.ReleaseSlot"/> to free the global + pool gates. Null for
    /// non-lease activity markers (e.g. daemon heartbeat).
    /// </summary>
    internal IDisposable? Releaser { get; init; }
}
