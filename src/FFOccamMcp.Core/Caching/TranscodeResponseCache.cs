using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Caching;

/// <summary>Opt-in, TTL'd, on-disk cache of successful transcode response envelopes.</summary>
public interface ITranscodeResponseCache
{
    /// <summary>
    /// Returns the stored success-response JSON for <paramref name="key"/> if a non-expired entry
    /// exists. <paramref name="ageSeconds"/> is the entry's age (clamped to &gt;= 0).
    /// </summary>
    bool TryGet(string key, int ttlSeconds, out string responseJson, out int ageSeconds);

    /// <summary>Writes (or overwrites) the success-response JSON for <paramref name="key"/>.</summary>
    void Set(string key, string responseJson);
}

/// <summary>
/// File-backed cache: one JSON file per key under the cache directory. Entirely best-effort —
/// any IO failure degrades to a cache miss and never disturbs the transcode. Mirrors the
/// atomic temp-file + move durability used by <c>JsonFileBatchJobStore</c>.
/// </summary>
public sealed class FileTranscodeResponseCache : ITranscodeResponseCache
{
    internal const int CurrentSchemaVersion = 1;

    private readonly string _dir;

    public FileTranscodeResponseCache()
        : this(ResolveDefaultDir())
    {
    }

    internal FileTranscodeResponseCache(string dir) => _dir = dir;

    internal static string ResolveDefaultDir()
    {
        var configured = Environment.GetEnvironmentVariable("OCCAM_CACHE_DIR");
        return string.IsNullOrWhiteSpace(configured)
            ? Path.Combine(Path.GetTempPath(), "occam-cache")
            : configured;
    }

    public bool TryGet(string key, int ttlSeconds, out string responseJson, out int ageSeconds)
    {
        responseJson = string.Empty;
        ageSeconds = 0;

        if (ttlSeconds <= 0)
        {
            return false;
        }

        var path = PathForKey(key);
        OccamCacheEntry? entry;
        try
        {
            if (!File.Exists(path))
            {
                return false;
            }

            entry = JsonSerializer.Deserialize(File.ReadAllText(path), OccamCacheJsonContext.Default.OccamCacheEntry);
        }
        catch
        {
            // Corrupt/locked entry — treat as a miss.
            return false;
        }

        if (entry is null
            || entry.SchemaVersion != CurrentSchemaVersion
            || string.IsNullOrEmpty(entry.ResponseJson))
        {
            return false;
        }

        var age = DateTimeOffset.UtcNow.ToUnixTimeSeconds() - entry.CreatedAtUnixSeconds;
        if (age < 0)
        {
            age = 0;
        }

        if (age > ttlSeconds)
        {
            TryDelete(path);
            return false;
        }

        responseJson = entry.ResponseJson;
        ageSeconds = (int)Math.Min(age, int.MaxValue);
        return true;
    }

    public void Set(string key, string responseJson) =>
        Set(key, responseJson, DateTimeOffset.UtcNow.ToUnixTimeSeconds());

    /// <summary>Test seam: write an entry stamped with an explicit creation time.</summary>
    internal void Set(string key, string responseJson, long createdAtUnixSeconds)
    {
        if (string.IsNullOrEmpty(responseJson))
        {
            return;
        }

        try
        {
            Directory.CreateDirectory(_dir);
            var entry = new OccamCacheEntry(CurrentSchemaVersion, createdAtUnixSeconds, responseJson);
            var json = JsonSerializer.Serialize(entry, OccamCacheJsonContext.Default.OccamCacheEntry);
            var path = PathForKey(key);
            var tmp = path + ".tmp";
            File.WriteAllText(tmp, json);
            File.Move(tmp, path, overwrite: true);
        }
        catch
        {
            // Best-effort: a failed write simply means the next read is a miss.
        }
    }

    private string PathForKey(string key) => Path.Combine(_dir, key + ".json");

    private static void TryDelete(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch
        {
            // Ignore — a stale entry is harmless; it failed the TTL check anyway.
        }
    }
}

/// <summary>On-disk cache entry: schema version, creation time, and the stored response JSON.</summary>
public sealed record OccamCacheEntry(
    [property: JsonPropertyName("schemaVersion")] int SchemaVersion,
    [property: JsonPropertyName("createdAtUnixSeconds")] long CreatedAtUnixSeconds,
    [property: JsonPropertyName("responseJson")] string ResponseJson);

[JsonSerializable(typeof(OccamCacheEntry))]
internal partial class OccamCacheJsonContext : JsonSerializerContext;
