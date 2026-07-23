namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Representation-neutral pointer into a <see cref="SourceSurface"/> (char offsets, UTF-16).
/// Never carries Markdown/HTML syntax — only location.
/// </summary>
public sealed record SurfaceSpan(int Start, int Length)
{
    public int End => Start + Length;

    public static SurfaceSpan? TryFind(string surface, string fragment, int startIndex = 0)
    {
        if (string.IsNullOrEmpty(surface) || string.IsNullOrEmpty(fragment))
        {
            return null;
        }

        var idx = surface.IndexOf(fragment, startIndex, StringComparison.Ordinal);
        return idx < 0 ? null : new SurfaceSpan(idx, fragment.Length);
    }
}
