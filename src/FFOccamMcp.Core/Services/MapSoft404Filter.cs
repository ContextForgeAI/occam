namespace OccamMcp.Core.Services;

/// <summary>Heuristic soft-404 path filter for map links (cherry-pick P10-B2).</summary>
public static class MapSoft404Filter
{
    private static readonly string[] Soft404Titles =
    [
        "404", "not found", "page not found", "error", "oops",
    ];

    public static bool LooksLikeSoft404(string path, string? title)
    {
        if (!string.IsNullOrWhiteSpace(title))
        {
            var lowerTitle = title.Trim().ToLowerInvariant();
            if (Soft404Titles.Any(marker => lowerTitle.Contains(marker, StringComparison.Ordinal)))
            {
                return true;
            }
        }

        var lowerPath = path.ToLowerInvariant();
        if (lowerPath is "/" or "")
        {
            return false;
        }

        return lowerPath.Contains("/404", StringComparison.Ordinal)
            || lowerPath.Contains("/not-found", StringComparison.Ordinal)
            || lowerPath.Contains("/error", StringComparison.Ordinal);
    }
}
