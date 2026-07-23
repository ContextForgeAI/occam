namespace OccamMcp.Core.Workers;

/// <summary>Resolves node executable — system PATH, OCCAM_NODE_BIN, or OCCAM_HOME/bin/node.</summary>
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

        return "node";
    }
}
