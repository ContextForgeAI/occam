namespace OccamMcp.Core.Workers;

/// <summary>Node/V8 launch flags for worker and daemon processes.</summary>
internal static class NodeLaunchArguments
{
    // Q-012: must be large enough to parse the response cap (OCCAM_MAX_RESPONSE_BYTES, 8 MiB)
    // into a DOM. The old 128 MiB heap OOM-crashed the http worker (V8 abort → workers_unavailable)
    // on heavy-HTML pages once the cap was raised — cnn/bloomberg/aol/… extract fine at 512 MiB.
    // This is a ceiling, not an allocation: small pages still use ~30–50 MiB, so the headroom is
    // only spent when actually parsing large HTML. Matches the browser worker default.
    private const int DefaultHttpMaxOldSpaceMb = 512;
    private const int DefaultBrowserMaxOldSpaceMb = 512;
    private const int MinMaxOldSpaceMb = 64;
    private const int MaxMaxOldSpaceMb = 1024;

    public static int ResolveMaxOldSpaceMb(bool browser = false)
    {
        if (browser
            && int.TryParse(Environment.GetEnvironmentVariable("OCCAM_BROWSER_NODE_MAX_OLD_SPACE_MB"), out var browserMb))
        {
            return Math.Clamp(browserMb, MinMaxOldSpaceMb, MaxMaxOldSpaceMb);
        }

        if (int.TryParse(Environment.GetEnvironmentVariable("OCCAM_NODE_MAX_OLD_SPACE_MB"), out var mb))
        {
            return Math.Clamp(mb, MinMaxOldSpaceMb, MaxMaxOldSpaceMb);
        }

        return browser ? DefaultBrowserMaxOldSpaceMb : DefaultHttpMaxOldSpaceMb;
    }

    public static string Build(params string[] tailArgs) => Build(browser: false, tailArgs);

    public static string Build(bool browser, params string[] tailArgs)
    {
        var parts = new List<string> { $"--max-old-space-size={ResolveMaxOldSpaceMb(browser)}" };
        parts.AddRange(tailArgs);
        return string.Join(' ', parts);
    }
}
