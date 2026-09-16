using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Session;

/// <summary>Temp merged headers file for worker calls; deleted on dispose. Never log values.</summary>
public sealed class FetchHeadersScope : IDisposable
{
    private static readonly AsyncLocal<string?> CurrentPath = new();
    private static readonly AsyncLocal<string?> CurrentStorageStatePath = new();

    public static string? ActivePath => CurrentPath.Value;

    public static string? ActiveStorageStatePath => CurrentStorageStatePath.Value;

    private readonly string? _previousPath;
    private readonly string? _previousStorageStatePath;
    private readonly string? _tempFile;
#if OCCAM_GATE
    internal static Action<string>? CleanupFailureSinkForTests { get; set; }
#endif

    private FetchHeadersScope(string tempFile, string? storageStatePath)
    {
        _tempFile = tempFile;
        _previousPath = CurrentPath.Value;
        _previousStorageStatePath = CurrentStorageStatePath.Value;
        CurrentPath.Value = tempFile;
        CurrentStorageStatePath.Value = storageStatePath;
    }

    /// <summary>
    /// One HTTP retry after browser cookie harvest. Merges into the current session headers
    /// file when present. Harvested names win. Never log values.
    /// </summary>
    public static FetchHeadersScope CreateCookieRetryScope(string harvestedCookieHeader)
    {
        var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (ActivePath is { Length: > 0 } path && File.Exists(path))
        {
            try
            {
                using var stream = File.OpenRead(path);
                var existing = JsonSerializer.Deserialize(
                    stream,
                    FetchHeadersJsonContext.Default.DictionaryStringString);
                if (existing is { Count: > 0 })
                {
                    foreach (var (name, value) in existing)
                    {
                        headers[name] = value;
                    }
                }
            }
            catch
            {
                // Fall through with harvested cookies only.
            }
        }

        headers["Cookie"] = headers.TryGetValue("Cookie", out var prior)
            ? MergeCookieHeader(prior, harvestedCookieHeader)
            : harvestedCookieHeader;
        return Create(headers, ActiveStorageStatePath);
    }

    internal static string MergeCookieHeader(string existing, string harvested)
    {
        var map = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var header in new[] { existing, harvested })
        {
            if (string.IsNullOrWhiteSpace(header))
            {
                continue;
            }

            foreach (var part in header.Split(';', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
            {
                var eq = part.IndexOf('=');
                if (eq <= 0)
                {
                    continue;
                }

                map[part[..eq].Trim()] = part[(eq + 1)..];
            }
        }

        return string.Join("; ", map.Select(pair => $"{pair.Key}={pair.Value}"));
    }

    public static FetchHeadersScope Create(
        IReadOnlyDictionary<string, string> headers,
        string? storageStatePath = null)
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"occam-headers-{Guid.NewGuid():N}.json");
        var json = JsonSerializer.Serialize(headers, FetchHeadersJsonContext.Default.DictionaryStringString);
        File.WriteAllText(tempFile, json);
        return new FetchHeadersScope(tempFile, storageStatePath);
    }

    public void Dispose()
    {
        CurrentPath.Value = _previousPath;
        CurrentStorageStatePath.Value = _previousStorageStatePath;
        if (_tempFile is not null)
        {
            if (!TryDeleteTempFile(_tempFile))
            {
                // Never log header values; only file metadata.
                var safeName = Path.GetFileName(_tempFile);
                var message = $"[occam.session] warning: failed to delete temp headers file '{safeName}'.";
                Console.Error.WriteLine(message);
                ScheduleBackgroundDelete(_tempFile);
#if OCCAM_GATE
                CleanupFailureSinkForTests?.Invoke(message);
#endif
            }
        }
    }

    private static bool TryDeleteTempFile(string path)
    {
        if (!File.Exists(path))
        {
            return true;
        }

        for (var attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                File.Delete(path);
                if (!File.Exists(path))
                {
                    return true;
                }
            }
            catch (IOException) when (attempt < 2)
            {
                Thread.Sleep(10);
            }
            catch (UnauthorizedAccessException) when (attempt < 2)
            {
                Thread.Sleep(10);
            }
            catch
            {
                return false;
            }
        }

        return !File.Exists(path);
    }

    private static void ScheduleBackgroundDelete(string path)
    {
        ThreadPool.QueueUserWorkItem(static state =>
        {
            if (state is not string retryPath)
            {
                return;
            }

            for (var attempt = 0; attempt < 20 && File.Exists(retryPath); attempt++)
            {
                Thread.Sleep(100);
                try
                {
                    File.Delete(retryPath);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }, path);
    }
}

[JsonSerializable(typeof(Dictionary<string, string>))]
internal partial class FetchHeadersJsonContext : JsonSerializerContext;
