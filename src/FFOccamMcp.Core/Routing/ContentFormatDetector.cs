namespace OccamMcp.Core.Routing;

public static class ContentFormatDetector
{
    public static bool IsPdfUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return false;
        }

        return uri.AbsolutePath.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPdfContentType(string? contentType) =>
        contentType is not null
        && contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase);
}
