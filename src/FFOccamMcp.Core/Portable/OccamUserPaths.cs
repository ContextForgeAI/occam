namespace OccamMcp.Core.Portable;

/// <summary>Per-user Occam data paths (~/.occam).</summary>
public static class OccamUserPaths
{
    public const string UserDataDirName = ".occam";

    public static string ResolveUserDataRoot()
    {
        var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        if (string.IsNullOrWhiteSpace(home))
        {
            return Path.Combine(Directory.GetCurrentDirectory(), UserDataDirName);
        }

        return Path.Combine(home, UserDataDirName);
    }
}
