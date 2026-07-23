using System.Text.Json;

namespace OccamMcp.Core.Digest;

public sealed record DigestUrlEntry(string Url, string? FocusQuery = null);

public static class DigestUrlParser
{
    public static bool TryParse(string input, out List<DigestUrlEntry> entries, out string? error)
    {
        entries = [];
        error = null;
        if (string.IsNullOrWhiteSpace(input))
        {
            error = "urls is required.";
            return false;
        }

        var trimmed = input.Trim();
        if (trimmed.StartsWith('['))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                if (doc.RootElement.ValueKind != JsonValueKind.Array || doc.RootElement.GetArrayLength() == 0)
                {
                    error = "urls JSON array is empty.";
                    return false;
                }

                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    if (el.ValueKind == JsonValueKind.String)
                    {
                        if (!TryAddEntry(el.GetString(), null, entries, out error))
                        {
                            return false;
                        }

                        continue;
                    }

                    if (el.ValueKind == JsonValueKind.Object)
                    {
                        var url = el.TryGetProperty("url", out var u) ? u.GetString() : null;
                        var focus = el.TryGetProperty("focus_query", out var f) ? f.GetString() : null;
                        if (!TryAddEntry(url, focus, entries, out error))
                        {
                            return false;
                        }

                        continue;
                    }

                    error = "urls array entries must be strings or {url, focus_query?} objects.";
                    return false;
                }

                return DeduplicateEntries(entries, out error);
            }
            catch
            {
                error = "urls must be a JSON array of strings or objects.";
                return false;
            }
        }

        foreach (var part in trimmed.Split(['\n', '\r', ',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!TryAddEntry(part, null, entries, out error))
            {
                return false;
            }
        }

        return DeduplicateEntries(entries, out error);
    }

    /// <summary>Validates and deduplicates an already shape-normalized URL collection.</summary>
    public static bool TryNormalize(
        IEnumerable<DigestUrlEntry> candidates,
        out IReadOnlyList<DigestUrlEntry> entries,
        out string? error)
    {
        var normalized = new List<DigestUrlEntry>();
        foreach (var candidate in candidates)
        {
            if (!TryAddEntry(candidate.Url, candidate.FocusQuery, normalized, out error))
            {
                entries = [];
                return false;
            }
        }

        if (!DeduplicateEntries(normalized, out error))
        {
            entries = [];
            return false;
        }

        entries = normalized;
        return true;
    }

    private static bool TryAddEntry(
        string? rawUrl,
        string? focusQuery,
        List<DigestUrlEntry> entries,
        out string? error)
    {
        error = null;
        if (string.IsNullOrWhiteSpace(rawUrl))
        {
            return true;
        }

        if (!Uri.TryCreate(rawUrl.Trim(), UriKind.Absolute, out var uri)
            || uri.Scheme is not ("http" or "https"))
        {
            error = $"Invalid URL: {rawUrl}";
            return false;
        }

        entries.Add(new DigestUrlEntry(
            uri.ToString(),
            string.IsNullOrWhiteSpace(focusQuery) ? null : focusQuery.Trim()));
        return true;
    }

    private static bool DeduplicateEntries(List<DigestUrlEntry> entries, out string? error)
    {
        error = null;
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var unique = new List<DigestUrlEntry>();
        foreach (var entry in entries)
        {
            if (!seen.Add(entry.Url))
            {
                continue;
            }

            unique.Add(entry);
        }

        entries.Clear();
        entries.AddRange(unique);
        if (entries.Count == 0)
        {
            error = "Provide at least one URL.";
            return false;
        }

        return true;
    }
}
