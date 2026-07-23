namespace OccamMcp.Core.Routing;

public static class PrivacyClassifier
{
    public static bool IsPrivateUrlBlocked() =>
        !string.Equals(
            Environment.GetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS"),
            "1",
            StringComparison.Ordinal);

    public static PrivacyClassification Classify(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return new PrivacyClassification
            {
                Mode = PrivacyMode.BlockedByPolicy,
                IsPrivateHost = false,
                BlockReason = ProbeFailureKind.InvalidArguments,
            };
        }

        var isPrivate = IsPrivateHost(uri);
        return new PrivacyClassification
        {
            Mode = isPrivate ? PrivacyMode.LocalPrivate : PrivacyMode.LocalPublic,
            IsPrivateHost = isPrivate,
        };
    }

    private static bool IsPrivateHost(Uri uri)
    {
        if (!uri.Scheme.Equals("http", StringComparison.OrdinalIgnoreCase)
            && !uri.Scheme.Equals("https", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var host = uri.Host;
        if (host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".local", StringComparison.OrdinalIgnoreCase)
            || host.EndsWith(".internal", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (System.Net.IPAddress.TryParse(host, out var ip))
        {
            return IsPrivateIp(ip);
        }

        return false;
    }

    public static bool IsPrivateIp(System.Net.IPAddress ip)
    {
        if (System.Net.IPAddress.IsLoopback(ip))
        {
            return true;
        }

        // IPv4-mapped IPv6 (::ffff:a.b.c.d) — validate the embedded IPv4 so a mapped private/
        // loopback address (e.g. ::ffff:169.254.169.254) can't slip past the v6 checks.
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6 && ip.IsIPv4MappedToIPv6)
        {
            return IsPrivateIp(ip.MapToIPv4());
        }

        var bytes = ip.GetAddressBytes();
        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            return bytes[0] == 0                 // 0.0.0.0/8 — "this host", routes to localhost on Linux
                || bytes[0] == 10
                || (bytes[0] == 172 && bytes[1] >= 16 && bytes[1] <= 31)
                || (bytes[0] == 192 && bytes[1] == 168)
                || bytes[0] == 127
                || (bytes[0] == 169 && bytes[1] == 254);
        }

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetworkV6)
        {
            // Link-local (fe80::/10), deprecated site-local (fec0::/10), and unique-local fc00::/7
            // (first byte 0xFC/0xFD) — the last was previously missed (same gap the worker had).
            return ip.IsIPv6LinkLocal
                || ip.IsIPv6SiteLocal
                || (bytes[0] & 0xFE) == 0xFC;
        }

        return false;
    }
}
