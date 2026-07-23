using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Services;

/// <summary>Loads and validates proxy URLs from env or file (AOT-safe, no reflection).</summary>
public static class ProxyListParser
{
    public static IReadOnlyList<string> LoadFromEnvironment()
    {
        var filePath = Environment.GetEnvironmentVariable(ProxyRotationSettings.ProxyListFileVar)?.Trim();
        if (!string.IsNullOrWhiteSpace(filePath) && File.Exists(filePath))
        {
            return ParseFileLines(File.ReadAllLines(filePath));
        }

        var inline = Environment.GetEnvironmentVariable(ProxyRotationSettings.ProxyListVar);
        return string.IsNullOrWhiteSpace(inline) ? Array.Empty<string>() : ParseInline(inline);
    }

    /// <summary>URL-per-line or CSV proxy-scraper export (header with ip, port, protocols).</summary>
    public static IReadOnlyList<string> ParseFileLines(IEnumerable<string> lines)
    {
        var rows = new List<string>();
        foreach (var raw in lines)
        {
            var trimmed = raw.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            rows.Add(trimmed);
        }

        if (rows.Count == 0)
        {
            return Array.Empty<string>();
        }

        if (LooksLikeCsvHeader(rows[0]))
        {
            return ParseCsvExport(rows);
        }

        return ParseUrlLines(rows);
    }

    public static IReadOnlyList<string> ParseLines(IEnumerable<string> lines) =>
        ParseUrlLines(lines.Where(l => !string.IsNullOrWhiteSpace(l)).Select(l => l.Trim()));

    public static IReadOnlyList<string> ParseInline(string value)
    {
        var parts = value.Split([',', ';', '\n', '\r'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var list = new List<string>(parts.Length);
        foreach (var part in parts)
        {
            if (EgressProxyConfig.IsValidProxyUrl(part))
            {
                list.Add(part);
            }
        }

        return list;
    }

    private static IReadOnlyList<string> ParseUrlLines(IEnumerable<string> lines)
    {
        var list = new List<string>();
        foreach (var trimmed in lines)
        {
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            if (EgressProxyConfig.IsValidProxyUrl(trimmed))
            {
                list.Add(trimmed);
            }
        }

        return list;
    }

    private static bool LooksLikeCsvHeader(string line)
    {
        if (EgressProxyConfig.IsValidProxyUrl(line))
        {
            return false;
        }

        return line.Contains("ip", StringComparison.OrdinalIgnoreCase)
            && line.Contains("port", StringComparison.OrdinalIgnoreCase)
            && (line.Contains("protocol", StringComparison.OrdinalIgnoreCase)
                || line.Contains("protocols", StringComparison.OrdinalIgnoreCase));
    }

    private static IReadOnlyList<string> ParseCsvExport(IReadOnlyList<string> rows)
    {
        var headerFields = SplitCsvLine(rows[0]);
        var ipIdx = IndexOfColumn(headerFields, "ip");
        var portIdx = IndexOfColumn(headerFields, "port");
        var protoIdx = IndexOfColumn(headerFields, "protocols", "protocol");
        if (ipIdx < 0 || portIdx < 0 || protoIdx < 0)
        {
            return Array.Empty<string>();
        }

        var list = new List<string>();
        for (var i = 1; i < rows.Count; i++)
        {
            var fields = SplitCsvLine(rows[i]);
            if (fields.Count <= Math.Max(ipIdx, Math.Max(portIdx, protoIdx)))
            {
                continue;
            }

            var url = BuildProxyUrl(fields[ipIdx], fields[portIdx], fields[protoIdx]);
            if (url is not null)
            {
                list.Add(url);
            }
        }

        return list;
    }

    private static int IndexOfColumn(IReadOnlyList<string> header, params string[] names)
    {
        for (var i = 0; i < header.Count; i++)
        {
            var col = header[i].Trim();
            foreach (var name in names)
            {
                if (col.Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    return i;
                }
            }
        }

        return -1;
    }

    internal static string? BuildProxyUrl(string ip, string port, string protocol)
    {
        var host = ip.Trim().Trim('"');
        var portText = port.Trim().Trim('"');
        var scheme = protocol.Trim().Trim('"').ToLowerInvariant();
        if (host.Length == 0 || portText.Length == 0)
        {
            return null;
        }

        var prefix = scheme switch
        {
            "http" => "http://",
            "https" => "https://",
            "socks5" => "socks5://",
            "socks4" => null,
            _ => null,
        };
        if (prefix is null)
        {
            return null;
        }

        var url = $"{prefix}{host}:{portText}";
        return EgressProxyConfig.IsValidProxyUrl(url) ? url : null;
    }

    internal static List<string> SplitCsvLine(string line)
    {
        var fields = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;
        for (var i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (c == ',' && !inQuotes)
            {
                fields.Add(current.ToString());
                current.Clear();
                continue;
            }

            current.Append(c);
        }

        fields.Add(current.ToString());
        return fields;
    }
}
