using System.Runtime;

namespace OccamMcp.RamStress;

internal static class NativeMemoryCompactor
{
    private static int _configured;

    public static void EnsureConfigured()
    {
        if (Interlocked.Exchange(ref _configured, 1) == 1)
        {
            return;
        }

        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
    }

    public static void ForceOsRelease()
    {
        GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
        GC.WaitForPendingFinalizers();
        GC.Collect(2, GCCollectionMode.Forced, blocking: true, compacting: true);
    }
}
