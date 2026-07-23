using OccamMcp.Core.Portable;

namespace OccamMcp.Core.Playbooks;

public static class PlaybookPaths
{
    public static string? LocalRootOverrideForTests { get; set; }
    public static string? UserPathOverrideForTests { get; set; }

    public static string ResolveLocalRoot()
    {
        if (LocalRootOverrideForTests is not null)
        {
            return LocalRootOverrideForTests;
        }

        var env = Environment.GetEnvironmentVariable("OCCAM_PLAYBOOKS_LOCAL_ROOT");
        if (!string.IsNullOrWhiteSpace(env))
        {
            return env.Trim();
        }

        return Path.Combine(OccamUserPaths.ResolveUserDataRoot(), "playbooks", "local");
    }

    public static string? ResolveUserPlaybooksPath()
    {
        if (UserPathOverrideForTests is not null)
        {
            return UserPathOverrideForTests;
        }

        var env = Environment.GetEnvironmentVariable("WT_PLAYBOOKS_PATH");
        return string.IsNullOrWhiteSpace(env) ? null : env.Trim();
    }
}
