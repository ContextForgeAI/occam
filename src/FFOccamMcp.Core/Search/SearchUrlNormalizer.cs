namespace OccamMcp.Core.Search;

/// <summary>
/// Dedup key for search hits: absolute http(s) URL, lowercase host, no fragment,
/// trailing slash stripped. Titles differ across engines; URLs are the stable identity.
/// </summary>
internal static class SearchUrlNormalizer
{
    public static string? Normalize(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        if (!Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri))
        {
            return null;
        }

        if (uri.Scheme is not ("http" or "https"))
        {
            return null;
        }

        var host = uri.IdnHost;
        if (string.IsNullOrEmpty(host))
        {
            host = uri.Host;
        }

        if (string.IsNullOrEmpty(host))
        {
            return null;
        }

        host = host.ToLowerInvariant();

        var path = uri.AbsolutePath.TrimEnd('/');
        var port = uri.IsDefaultPort ? -1 : uri.Port;
        var builder = new UriBuilder(uri.Scheme, host, port, path)
        {
            Query = uri.Query.TrimStart('?'),
            Fragment = string.Empty,
        };

        var normalized = builder.Uri.AbsoluteUri;
        // UriBuilder may re-add a trailing slash for empty path; strip for stable keys.
        if (normalized.EndsWith('/') && string.IsNullOrEmpty(path) && string.IsNullOrEmpty(builder.Query))
        {
            normalized = normalized.TrimEnd('/');
        }

        return normalized;
    }
}
