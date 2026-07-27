namespace OccamMcp.Core.Workers;

/// <summary>Resolves node executable — OCCAM_NODE_BIN, OCCAM_HOME/bin/node, common install paths, or PATH.</summary>
public static class NodeRuntime
{
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
            var bundled = Path.Combine(Path.GetFullPath(home.Trim()), "bin", "node");
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

        return "node";
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
        }
    }
}
