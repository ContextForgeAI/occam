using System.Diagnostics.CodeAnalysis;

namespace OccamMcp.Core.Services;

/// <summary>
/// Polite multi-URL scheduling for <c>occam_digest</c>: same registrable host is always
/// sequential; different hosts may fan out up to <c>maxParallel</c>. Avoids stampeding a
/// single origin when map→digest feeds many same-site URLs.
/// </summary>
internal static class DigestHostScheduler
{
    /// <summary>
    /// Groups entry indices by normalized host, preserving first-seen host order and
    /// within-host original URL order.
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<int>> GroupIndicesByHost(IReadOnlyList<string> urls)
    {
        var groups = new List<List<int>>();
        var indexByHost = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        for (var i = 0; i < urls.Count; i++)
        {
            var host = HostKey(urls[i]);
            if (!indexByHost.TryGetValue(host, out var groupIndex))
            {
                groupIndex = groups.Count;
                indexByHost[host] = groupIndex;
                groups.Add([]);
            }

            groups[groupIndex].Add(i);
        }

        return groups;
    }

    public static string HostKey(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri)
            || string.IsNullOrWhiteSpace(uri.Host))
        {
            return $"#{url}";
        }

        var host = uri.Host.Trim().TrimEnd('.').ToLowerInvariant();
        if (host.StartsWith("www.", StringComparison.Ordinal))
        {
            host = host["www.".Length..];
        }

        return host;
    }

    /// <summary>
    /// True when every URL shares one host (map→digest same-site case) — fan-out collapses
    /// to sequential under host-aware scheduling.
    /// </summary>
    public static bool IsSingleHost([NotNullWhen(true)] IReadOnlyList<string>? urls)
    {
        if (urls is null || urls.Count == 0)
        {
            return false;
        }

        var first = HostKey(urls[0]);
        for (var i = 1; i < urls.Count; i++)
        {
            if (!string.Equals(first, HostKey(urls[i]), StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        return true;
    }
}
