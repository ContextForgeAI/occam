namespace OccamMcp.Core.Services;

/// <summary>Drop asset/webpack links from map output (cherry-pick P10-B3).</summary>
public static class MapLinkFilter
{
    private static readonly string[] NonsensePathTerms =
    [
        "/_next/static", "/webpack", "/chunk", "/hot-update", "/__webpack",
        "/assets/", "/static/js/", "/static/css/", "/node_modules/",
    ];

    private static readonly string[] NonsenseExtensions =
    [
        ".js", ".mjs", ".css", ".map", ".woff", ".woff2", ".ttf", ".eot",
        ".png", ".jpg", ".jpeg", ".gif", ".svg", ".ico", ".webp", ".avif",
        ".pdf", ".zip", ".gz", ".xml.gz",
    ];

    public static bool IsNonsense(string url, string? path = null, string? title = null)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return true;
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || uri.Scheme is not "http" and not "https")
        {
            return true;
        }

        var resolvedPath = path ?? uri.AbsolutePath;
        var lowerPath = resolvedPath.ToLowerInvariant();
        var lowerUrl = url.ToLowerInvariant();

        if (NonsensePathTerms.Any(term => lowerPath.Contains(term, StringComparison.Ordinal)))
        {
            return true;
        }

        foreach (var ext in NonsenseExtensions)
        {
            if (lowerPath.EndsWith(ext, StringComparison.Ordinal)
                || lowerUrl.Contains(ext + "?", StringComparison.Ordinal))
            {
                return true;
            }
        }

        if (lowerPath.Contains("/undefined", StringComparison.Ordinal)
            || lowerPath.Contains("/null", StringComparison.Ordinal))
        {
            return true;
        }

        if (MapSoft404Filter.LooksLikeSoft404(resolvedPath, title))
        {
            return true;
        }

        if (IsNginxVersionChangelog(uri, lowerPath))
        {
            return true;
        }

        return false;
    }

    /// <summary>nginx.org sitemap lists /en/CHANGES* version logs — not doc pages.</summary>
    private static bool IsNginxVersionChangelog(Uri uri, string lowerPath)
    {
        if (!uri.Host.EndsWith("nginx.org", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        foreach (var segment in lowerPath.Split('/', StringSplitOptions.RemoveEmptyEntries))
        {
            if (segment == "changes" || segment.StartsWith("changes-", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
