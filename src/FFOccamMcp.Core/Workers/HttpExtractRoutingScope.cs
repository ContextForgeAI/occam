namespace OccamMcp.Core.Workers;

/// <summary>
/// Per-async-flow HTTP extract routing. Parallel digest sets <see cref="PreferOneShot"/>
/// so each slot spawns one-shot Node instead of sharing the single-process HTTP daemon.
/// </summary>
public sealed class HttpExtractRoutingScope : IDisposable
{
    private static readonly AsyncLocal<bool> CurrentPreferOneShot = new();

    public static bool PreferOneShot => CurrentPreferOneShot.Value;

    private readonly bool _previous;

    private HttpExtractRoutingScope(bool preferOneShot)
    {
        _previous = CurrentPreferOneShot.Value;
        CurrentPreferOneShot.Value = preferOneShot;
    }

    public static HttpExtractRoutingScope PushOneShot() => new(true);

    public void Dispose()
    {
        CurrentPreferOneShot.Value = _previous;
    }
}
