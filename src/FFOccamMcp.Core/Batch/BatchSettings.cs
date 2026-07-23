namespace OccamMcp.Core.Batch;

public static class BatchSettings
{
    public const int DefaultPort = 5051;
    public const int DefaultMaxUrls = 64;
    public const int DefaultParallel = 4;
    public const string DefaultBindAddress = "127.0.0.1";

    public static int Port =>
        int.TryParse(Environment.GetEnvironmentVariable("OCCAM_BATCH_PORT"), out var parsed)
            ? Math.Clamp(parsed, 1, 65535)
            : DefaultPort;

    public static int MaxUrls =>
        int.TryParse(Environment.GetEnvironmentVariable("OCCAM_BATCH_MAX_URLS"), out var parsed)
            ? Math.Clamp(parsed, 1, 256)
            : DefaultMaxUrls;

    public static int Parallel =>
        int.TryParse(Environment.GetEnvironmentVariable("OCCAM_BATCH_PARALLEL"), out var parsed)
            ? Math.Clamp(parsed, 1, 16)
            : DefaultParallel;

    public static string DbPath
    {
        get
        {
            var overridePath = Environment.GetEnvironmentVariable("OCCAM_BATCH_DB_PATH");
            if (!string.IsNullOrWhiteSpace(overridePath))
            {
                return overridePath.Trim();
            }

            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, ".occam", "jobs", "jobs.db");
        }
    }
}
