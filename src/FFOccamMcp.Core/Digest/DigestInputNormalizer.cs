using System.Text.Json;

namespace OccamMcp.Core.Digest;

/// <summary>
/// Normalizes the public MCP union for <c>occam_digest.urls</c> into the domain URL collection.
/// Transport-specific JSON shapes terminate here.
/// </summary>
public static class DigestInputNormalizer
{
    public const int MaxInputCharacters = 65_536;
    public const int MaxInputItems = 256;

    public static bool TryNormalize(
        JsonElement? input,
        out IReadOnlyList<DigestUrlEntry> entries,
        out string? error)
    {
        entries = [];
        error = null;

        if (input is not { } value || value.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            error = "urls is required when source_url is not set.";
            return false;
        }

        if (value.ValueKind == JsonValueKind.String)
        {
            var legacy = value.GetString();
            if (legacy is { Length: > MaxInputCharacters })
            {
                error = $"urls exceeds the {MaxInputCharacters}-character input limit.";
                return false;
            }

            if (!DigestUrlParser.TryParse(legacy ?? string.Empty, out var parsed, out error))
            {
                return false;
            }

            if (parsed.Count > MaxInputItems)
            {
                error = $"urls contains more than {MaxInputItems} entries.";
                return false;
            }

            entries = parsed;
            return true;
        }

        if (value.ValueKind != JsonValueKind.Array)
        {
            error = "urls must be a string or an array of URL strings.";
            return false;
        }

        if (value.GetArrayLength() == 0)
        {
            error = "urls array is empty.";
            return false;
        }

        if (value.GetArrayLength() > MaxInputItems)
        {
            error = $"urls contains more than {MaxInputItems} entries.";
            return false;
        }

        var candidates = new List<DigestUrlEntry>(value.GetArrayLength());
        var characters = 0;
        foreach (var item in value.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.String)
            {
                error = "urls array entries must all be URL strings.";
                return false;
            }

            var url = item.GetString() ?? string.Empty;
            characters += url.Length;
            if (characters > MaxInputCharacters)
            {
                error = $"urls exceeds the {MaxInputCharacters}-character input limit.";
                return false;
            }

            candidates.Add(new DigestUrlEntry(url));
        }

        if (!DigestUrlParser.TryNormalize(candidates, out var normalized, out error))
        {
            return false;
        }

        entries = normalized;
        return true;
    }
}
