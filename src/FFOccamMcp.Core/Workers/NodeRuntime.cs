namespace OccamMcp.Core.Workers;

/// <summary>
/// Canonical Node executable resolution for worker/daemon spawns.
/// Precedence: OCCAM_NODE_BIN → {OCCAM_HOME}/runtime/node-bin → {OCCAM_HOME}/bin/node →
/// well-known platform paths → bare "node" on PATH.
/// </summary>
public static class NodeRuntime
{
    public const string RuntimeNodeBinRelativePath = "runtime/node-bin";

    public static string ResolveExecutable()
    {
        var env = Environment.GetEnvironmentVariable("OCCAM_NODE_BIN");
        if (!string.IsNullOrWhiteSpace(env))
        {
            var path = Path.GetFullPath(env.Trim());
            if (File.Exists(path))
            {
                return path;
            }
        }

        var home = WorkerPaths.ResolveOccamHome();
        if (!string.IsNullOrWhiteSpace(home))
        {
            var root = Path.GetFullPath(home.Trim());
            var recorded = TryReadInstallNodeBin(root);
            if (!string.IsNullOrWhiteSpace(recorded) && File.Exists(recorded))
            {
                return recorded;
            }

            var bundledName = OperatingSystem.IsWindows() ? "node.exe" : "node";
            var bundled = Path.Combine(root, "bin", bundledName);
            if (File.Exists(bundled))
            {
                return bundled;
            }
        }

        // GUI MCP hosts often inherit PATH=/usr/bin:/bin (no Homebrew). Prefer well-known
        // absolute locations before falling back to a bare "node" PATH lookup.
        foreach (var candidate in WellKnownNodePaths())
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return OperatingSystem.IsWindows() ? "node.exe" : "node";
    }

    /// <summary>Human message when an explicit/recorded Node path is missing (stderr diagnostics).</summary>
    public static string FormatMissingNodeMessage(string path) =>
        $"Occam's Node runtime is no longer available at:{Environment.NewLine}  {path}{Environment.NewLine}{Environment.NewLine}" +
        "Reinstall Occam or set OCCAM_NODE_BIN to a working Node 20+ executable.";

    private static string? TryReadInstallNodeBin(string occamHome)
    {
        try
        {
            var file = Path.Combine(occamHome, "runtime", "node-bin");
            if (!File.Exists(file))
            {
                return null;
            }

            foreach (var raw in File.ReadLines(file))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith('#'))
                {
                    continue;
                }

                return line;
            }
        }
        catch
        {
            // best-effort
        }

        return null;
    }

    private static IEnumerable<string> WellKnownNodePaths()
    {
        if (OperatingSystem.IsMacOS())
        {
            yield return "/opt/homebrew/bin/node";
            yield return "/usr/local/bin/node";
        }
        else if (OperatingSystem.IsLinux())
        {
            yield return "/usr/bin/node";
            yield return "/usr/local/bin/node";
        }
        else if (OperatingSystem.IsWindows())
        {
            var programFiles = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
            if (!string.IsNullOrWhiteSpace(programFiles))
            {
                yield return Path.Combine(programFiles, "nodejs", "node.exe");
            }

            var programFilesX86 = Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86);
            if (!string.IsNullOrWhiteSpace(programFilesX86))
            {
                yield return Path.Combine(programFilesX86, "nodejs", "node.exe");
            }
        }
    }
}
