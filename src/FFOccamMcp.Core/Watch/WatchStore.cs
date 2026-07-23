using System.Text.Json;

namespace OccamMcp.Core.Watch;

public interface IWatchStore
{
    WatchRecord? Get(string url);
    void Upsert(WatchRecord record);
    bool Remove(string url);
}

/// <summary>
/// Minimal JSON-file last-seen store for <c>occam_watch</c> (opt-in, stateful). All access is
/// serialized through one lock; the whole file is rewritten on each upsert (the watch set is small).
/// </summary>
public sealed class WatchStore : IWatchStore
{
    private readonly string _path;
    private readonly object _sync = new();
    private readonly Dictionary<string, WatchRecord> _records = new(StringComparer.Ordinal);
    private bool _initialized;

    public WatchStore()
        : this(DefaultPath())
    {
    }

    internal WatchStore(string path) => _path = path;

    internal static string DefaultPath()
    {
        var configured = Environment.GetEnvironmentVariable("OCCAM_WATCH_DB_PATH");
        if (!string.IsNullOrWhiteSpace(configured))
        {
            return configured.Trim();
        }
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(home, ".occam", "watch", "watch.json");
    }

    public WatchRecord? Get(string url)
    {
        lock (_sync)
        {
            EnsureInit();
            return _records.TryGetValue(Key(url), out var record) ? record : null;
        }
    }

    public void Upsert(WatchRecord record)
    {
        lock (_sync)
        {
            EnsureInit();
            _records[Key(record.Url)] = record;
            Persist();
        }
    }

    public bool Remove(string url)
    {
        lock (_sync)
        {
            EnsureInit();
            if (_records.Remove(Key(url)))
            {
                Persist();
                return true;
            }
            return false;
        }
    }

    private static string Key(string url) => url.Trim();

    private void EnsureInit()
    {
        if (_initialized)
        {
            return;
        }

        try
        {
            if (File.Exists(_path))
            {
                var json = File.ReadAllText(_path);
                var file = JsonSerializer.Deserialize(json, OccamWatchJsonContext.Default.WatchStoreFile);
                foreach (var record in file?.Records ?? [])
                {
                    _records[Key(record.Url)] = record;
                }
            }
        }
        catch
        {
            // Corrupt/unreadable store starts empty rather than failing the tool.
            _records.Clear();
        }

        _initialized = true;
    }

    private void Persist()
    {
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        var file = new WatchStoreFile { Records = [.. _records.Values] };
        var json = JsonSerializer.Serialize(file, OccamWatchJsonContext.Default.WatchStoreFile);
        File.WriteAllText(_path, json);
    }
}
