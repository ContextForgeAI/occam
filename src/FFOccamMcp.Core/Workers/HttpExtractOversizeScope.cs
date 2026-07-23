namespace OccamMcp.Core.Workers;

/// <summary>
/// Per-flow HTTP oversize handling for worker spawns (<c>OCCAM_HTTP_OVERSIZE_MODE</c>).
/// Default outside scope: <c>fail</c> → <c>response_too_large</c>.
/// </summary>
public sealed class HttpExtractOversizeScope : IDisposable
{
    public const string Fail = "fail";
    public const string Partial = "partial";

    private static readonly AsyncLocal<string?> CurrentMode = new();

    public static string? Current => CurrentMode.Value;

    private readonly string? _previous;

    private HttpExtractOversizeScope(string mode)
    {
        _previous = CurrentMode.Value;
        CurrentMode.Value = mode;
    }

    public static HttpExtractOversizeScope PushPartial() => new(Partial);

    public void Dispose() => CurrentMode.Value = _previous;
}
