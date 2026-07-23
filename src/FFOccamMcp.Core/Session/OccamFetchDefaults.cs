using System.Text.Json;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Session;

/// <summary>Canonical HTTP fetch identity — keep in sync with profiles/occam-fetch-defaults.json.</summary>
public static class OccamFetchDefaults
{
    public const string FallbackUserAgent =
        "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/120.0.0.0 Safari/537.36";

    public const string FallbackAccept = "text/html,application/xhtml+xml";

    private static readonly Lazy<FetchDefaults> Cached = new(Load);

    public static string UserAgent => Cached.Value.UserAgent;

    public static string Accept => Cached.Value.Accept;

    private static FetchDefaults Load()
    {
        var root = WorkerPaths.ResolveOccamHome();
        if (root is not null)
        {
            var path = Path.Combine(root, "profiles", "occam-fetch-defaults.json");
            if (File.Exists(path))
            {
                try
                {
                    using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
                    var rootEl = doc.RootElement;
                    return new FetchDefaults(
                        rootEl.TryGetProperty("userAgent", out var ua) && ua.ValueKind == JsonValueKind.String
                            ? ua.GetString() ?? FallbackUserAgent
                            : FallbackUserAgent,
                        rootEl.TryGetProperty("accept", out var acc) && acc.ValueKind == JsonValueKind.String
                            ? acc.GetString() ?? FallbackAccept
                            : FallbackAccept);
                }
                catch
                {
                    // fall through
                }
            }
        }

        return new FetchDefaults(FallbackUserAgent, FallbackAccept);
    }

    private sealed record FetchDefaults(string UserAgent, string Accept);
}
