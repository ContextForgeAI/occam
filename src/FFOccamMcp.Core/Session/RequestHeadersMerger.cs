using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Session;

/// <summary>Merges OCCAM_REQUEST_HEADERS_FILE with session profile headers (session wins on clash).</summary>
public static class RequestHeadersMerger
{
    public static IReadOnlyDictionary<string, string> Merge(
        IReadOnlyDictionary<string, string>? envHeaders,
        IReadOnlyDictionary<string, string>? sessionHeaders)
    {
        if (sessionHeaders is null || sessionHeaders.Count == 0)
        {
            return envHeaders is { Count: > 0 }
                ? envHeaders
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        if (envHeaders is null || envHeaders.Count == 0)
        {
            return sessionHeaders;
        }

        var merged = new Dictionary<string, string>(envHeaders, StringComparer.OrdinalIgnoreCase);
        foreach (var (name, value) in sessionHeaders)
        {
            merged[name] = value;
        }

        return merged;
    }

    public static IReadOnlyDictionary<string, string> ReadEnvHeaders()
    {
        var path = OccamEnvironment.Get("OCCAM_REQUEST_HEADERS_FILE");
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        try
        {
            using var stream = File.OpenRead(path);
            var parsed = JsonSerializer.Deserialize(
                stream,
                RequestHeadersJsonContext.Default.DictionaryStringString);
            return parsed is { Count: > 0 }
                ? parsed
                : new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
        catch
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }
    }

    public static FetchHeadersScope? CreateScope(IReadOnlyDictionary<string, string>? sessionHeaders)
    {
        var merged = Merge(ReadEnvHeaders(), sessionHeaders);
        return merged.Count == 0 ? null : FetchHeadersScope.Create(merged);
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class RequestHeadersJsonContext : JsonSerializerContext;
