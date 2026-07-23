using System.Text.Json;
using OccamMcp.Core.Portable;

namespace OccamMcp.Core.Session;

public enum SessionProfileStatus
{
    Ok,
    InvalidId,
    NotFound,
}

public sealed class SessionProfileResolveResult
{
    public required SessionProfileStatus Status { get; init; }
    public IReadOnlyDictionary<string, string> Headers { get; init; } =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

    public string? SanitizedId { get; init; }

    /// <summary>Playwright storage state JSON path (browser worker only).</summary>
    public string? StorageStatePath { get; init; }

    public string? FailureCode => Status switch
    {
        SessionProfileStatus.InvalidId => "invalid_session_profile",
        SessionProfileStatus.NotFound => "session_profile_not_found",
        _ => null,
    };
}

/// <summary>Resolves local session profile JSON to HTTP headers (P2-2).</summary>
public static class SessionProfileHeaders
{
    internal static string? SessionsRootOverrideForTests { get; set; }

    private static readonly HashSet<string> ReservedKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "storageState",
        "_occam",
    };

    private static readonly HashSet<string> BlockedNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "Host",
        "Content-Length",
        "Content-Type",
        "Transfer-Encoding",
        "Connection",
        "Expect",
        "Upgrade",
    };

    public static SessionProfileResolveResult Resolve(string? sessionProfile)
    {
        if (string.IsNullOrWhiteSpace(sessionProfile))
        {
            return new SessionProfileResolveResult { Status = SessionProfileStatus.Ok };
        }

        var raw = sessionProfile.Trim();
        if (ContainsPathTraversal(raw) || !IsAllowedId(raw))
        {
            return new SessionProfileResolveResult { Status = SessionProfileStatus.InvalidId };
        }

        var sanitized = SanitizeId(raw);
        if (string.IsNullOrWhiteSpace(sanitized))
        {
            return new SessionProfileResolveResult { Status = SessionProfileStatus.InvalidId };
        }

        var path = Path.Combine(GetSessionsRoot(), $"{sanitized}.json");
        if (!File.Exists(path))
        {
            return new SessionProfileResolveResult
            {
                Status = SessionProfileStatus.NotFound,
                SanitizedId = sanitized,
            };
        }

        try
        {
            using var doc = JsonDocument.Parse(File.ReadAllBytes(path));
            var headers = ParseHeadersObject(doc.RootElement);
            var storageState = TryResolveStorageStatePath(doc.RootElement, path);
            if (storageState is { Ok: false })
            {
                return new SessionProfileResolveResult
                {
                    Status = SessionProfileStatus.NotFound,
                    SanitizedId = sanitized,
                };
            }

            return new SessionProfileResolveResult
            {
                Status = SessionProfileStatus.Ok,
                SanitizedId = sanitized,
                Headers = headers,
                StorageStatePath = storageState?.Path,
            };
        }
        catch
        {
            return new SessionProfileResolveResult
            {
                Status = SessionProfileStatus.NotFound,
                SanitizedId = sanitized,
            };
        }
    }

    public static string GetSessionsRoot()
    {
        if (SessionsRootOverrideForTests is not null)
        {
            return SessionsRootOverrideForTests;
        }

        var env = Environment.GetEnvironmentVariable("OCCAM_SESSIONS_ROOT");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        return Path.Combine(OccamUserPaths.ResolveUserDataRoot(), "sessions");
    }

    private static bool ContainsPathTraversal(string id) =>
        id.Contains("..", StringComparison.Ordinal)
        || id.Contains('/', StringComparison.Ordinal)
        || id.Contains('\\', StringComparison.Ordinal);

    private static bool IsAllowedId(string id)
    {
        foreach (var c in id)
        {
            if (char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            {
                continue;
            }

            return false;
        }

        return true;
    }

    private static string SanitizeId(string profile)
    {
        var chars = profile
            .Where(c => char.IsLetterOrDigit(c) || c is '-' or '_' or '.')
            .ToArray();
        return new string(chars);
    }

    private static IReadOnlyDictionary<string, string> ParseHeadersObject(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        }

        var parsed = new Dictionary<string, string>(capacity: 8, comparer: StringComparer.OrdinalIgnoreCase);
        foreach (var property in root.EnumerateObject())
        {
            if (string.IsNullOrWhiteSpace(property.Name)
                || BlockedNames.Contains(property.Name)
                || ReservedKeys.Contains(property.Name))
            {
                continue;
            }

            if (property.Value.ValueKind != JsonValueKind.String)
            {
                continue;
            }

            var value = property.Value.GetString();
            if (string.IsNullOrWhiteSpace(value))
            {
                continue;
            }

            parsed[property.Name.Trim()] = value;
        }

        return parsed;
    }

    private sealed record StorageStateResolve(bool Ok, string? Path);

    private static StorageStateResolve? TryResolveStorageStatePath(JsonElement root, string profilePath)
    {
        if (root.ValueKind != JsonValueKind.Object
            || !root.TryGetProperty("storageState", out var prop)
            || prop.ValueKind != JsonValueKind.String)
        {
            return null;
        }

        var raw = prop.GetString();
        if (string.IsNullOrWhiteSpace(raw))
        {
            return null;
        }

        var resolved = ResolveStorageStatePath(raw.Trim(), profilePath);
        if (resolved is null || !File.Exists(resolved))
        {
            return new StorageStateResolve(false, null);
        }

        return new StorageStateResolve(true, resolved);
    }

    internal static string? ResolveStorageStatePath(string raw, string profilePath)
    {
        var expanded = raw.StartsWith('~')
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), raw.TrimStart('~', '/', '\\'))
            : raw;

        var full = Path.IsPathRooted(expanded)
            ? Path.GetFullPath(expanded)
            : Path.GetFullPath(Path.Combine(GetSessionsRoot(), expanded));

        var sessionsRoot = Path.GetFullPath(GetSessionsRoot());
        if (!full.StartsWith(sessionsRoot, OperatingSystem.IsWindows()
                ? StringComparison.OrdinalIgnoreCase
                : StringComparison.Ordinal))
        {
            return null;
        }

        return full;
    }
}
