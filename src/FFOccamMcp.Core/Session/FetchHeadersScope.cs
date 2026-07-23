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
