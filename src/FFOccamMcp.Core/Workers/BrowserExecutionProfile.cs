namespace OccamMcp.Core.Workers;

/// <summary>Browser execution strategy — shared daemon (low RAM) vs isolated one-shot workers (throughput).</summary>
internal enum BrowserExecutionMode
{
    /// <summary>Long-lived <c>browser-daemon.mjs</c> + serialized <c>BrowserPool</c> queue.</summary>
    SharedDaemon,

    /// <summary>Fresh Node + Chromium per extract — WebMCP-style parallel throughput.</summary>
    IsolatedWorkers,
}

/// <summary>Resolves <see cref="BrowserExecutionMode"/> from env (user-facing profile presets).</summary>
internal static class BrowserExecutionProfile
{
    public static BrowserExecutionMode ResolveMode()
    {
        var profile = Environment.GetEnvironmentVariable("OCCAM_BROWSER_PROFILE");
        if (!string.IsNullOrWhiteSpace(profile))
        {
            if (IsIsolatedAlias(profile))
            {
                return BrowserExecutionMode.IsolatedWorkers;
            }

            if (IsSharedAlias(profile))
            {
                return BrowserExecutionMode.SharedDaemon;
            }
        }

        if (string.Equals(Environment.GetEnvironmentVariable("OCCAM_BROWSER_DAEMON"), "0", StringComparison.Ordinal))
        {
            return BrowserExecutionMode.IsolatedWorkers;
        }

        return BrowserExecutionMode.SharedDaemon;
    }

    public static bool UseSharedDaemon() => ResolveMode() == BrowserExecutionMode.SharedDaemon;

    private static bool IsIsolatedAlias(string value) =>
        value.Equals("isolated", StringComparison.OrdinalIgnoreCase)
        || value.Equals("parallel", StringComparison.OrdinalIgnoreCase)
        || value.Equals("throughput", StringComparison.OrdinalIgnoreCase);

    private static bool IsSharedAlias(string value) =>
        value.Equals("shared", StringComparison.OrdinalIgnoreCase)
        || value.Equals("daemon", StringComparison.OrdinalIgnoreCase)
        || value.Equals("lean", StringComparison.OrdinalIgnoreCase);
}
