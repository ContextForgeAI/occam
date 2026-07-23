using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Workers;

/// <summary>Caps concurrent browser extract operations (daemon + one-shot).</summary>
internal static class BrowserConcurrencyGate
{
    private static GateState _state = CreateState();

    public static int MaxParallel => Volatile.Read(ref _state).MaxParallel;

    public static T Run<T>(Func<T> work, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var state = Volatile.Read(ref _state);
        state.Gate.Wait(cancellationToken);
        try
        {
            return work();
        }
        finally
        {
            state.Gate.Release();
        }
    }

    internal static int ResolveMaxParallel()
        => OccamEnvironment.GetInt("OCCAM_BROWSER_MAX_PARALLEL", defaultValue: 2, min: 1, max: 16, fallback: "WT_BROWSER_MAX_PARALLEL");

    private static GateState CreateState()
    {
        var limit = ResolveMaxParallel();
        return new GateState(limit, new SemaphoreSlim(limit, limit));
    }

#if OCCAM_GATE
    internal static void ResetForTests() => Interlocked.Exchange(ref _state, CreateState());
#endif

    private sealed record GateState(int MaxParallel, SemaphoreSlim Gate);
}
