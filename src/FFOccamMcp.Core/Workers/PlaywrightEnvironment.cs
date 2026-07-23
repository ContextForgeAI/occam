using System.Diagnostics;

namespace OccamMcp.Core.Workers;

/// <summary>
/// Playwright resolves browser binaries via PLAYWRIGHT_BROWSERS_PATH. When the .NET host spawns Node
/// workers, inherit operator overrides or default to the same cache doctor checks (ms-playwright).
/// Cache path rules: keep in sync with scripts/lib/playwright-cache.mjs
/// </summary>
public static class PlaywrightEnvironment
{
    public static void ApplyTo(ProcessStartInfo psi)
    {
        if (psi.UseShellExecute)
        {
            return;
        }

        var existing = Environment.GetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH");
        if (!string.IsNullOrWhiteSpace(existing))
        {
            try
            {
                var full = Path.GetFullPath(existing.Trim());
                if (HasChromiumInstall(full))
                {
                    return;
                }
            }
            catch
            {
                // fall through — operator/sandbox path may be stale
            }
        }

        var resolved = ResolveDefaultBrowsersPath();
        if (resolved is null)
        {
            return;
        }

        psi.Environment["PLAYWRIGHT_BROWSERS_PATH"] = resolved;
    }

    public static string? ResolveDefaultBrowsersPath()
    {
        var overridePath = Environment.GetEnvironmentVariable("OCCAM_PLAYWRIGHT_BROWSERS_PATH");
        if (!string.IsNullOrWhiteSpace(overridePath))
        {
            var full = Path.GetFullPath(overridePath.Trim());
            return Directory.Exists(full) ? full : null;
        }

        if (OperatingSystem.IsWindows())
        {
            var localAppData = Environment.GetEnvironmentVariable("LOCALAPPDATA");
            if (!string.IsNullOrWhiteSpace(localAppData))
            {
                var candidate = Path.Combine(localAppData, "ms-playwright");
                if (HasChromiumInstall(candidate))
                {
                    return candidate;
                }
            }

            return null;
        }

        var home = Environment.GetEnvironmentVariable("HOME");
        if (string.IsNullOrWhiteSpace(home))
        {
            return null;
        }

        var unixCandidate = OperatingSystem.IsMacOS()
            ? Path.Combine(home, "Library", "Caches", "ms-playwright")
            : Path.Combine(home, ".cache", "ms-playwright");

        return HasChromiumInstall(unixCandidate) ? unixCandidate : null;
    }

    internal static bool HasChromiumInstall(string root)
    {
        if (!Directory.Exists(root))
        {
            return false;
        }

        try
        {
            return Directory.EnumerateDirectories(root)
                .Any(d => Path.GetFileName(d).StartsWith("chromium", StringComparison.OrdinalIgnoreCase));
        }
        catch
        {
            return false;
        }
    }
}
