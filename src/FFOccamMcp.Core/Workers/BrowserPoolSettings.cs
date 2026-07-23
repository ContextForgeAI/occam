using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Workers;

/// <summary>Env-driven browser daemon pool sizing and port layout.</summary>
public sealed class BrowserPoolSettings
{
    public const string PoolSizeVar = "OCCAM_BROWSER_POOL_SIZE";
    public const string BasePortVar = "OCCAM_BROWSER_POOL_BASE_PORT";
    public const string DaemonPortVar = "OCCAM_BROWSER_DAEMON_PORT";
    public const string IdleTtlVar = "OCCAM_BROWSER_DAEMON_IDLE_TTL_MS";
    public const string MaxParallelVar = "OCCAM_BROWSER_MAX_PARALLEL";

    public int PoolSize { get; init; } = 1;
    public int BasePort { get; init; } = 39_217;
    public int IdleTtlMs { get; init; } = 120_000;
    public int MaxParallel { get; init; } = 2;

    public static BrowserPoolSettings ReadFromEnvironment()
    {
        return new BrowserPoolSettings
        {
            PoolSize = OccamEnvironment.GetInt(PoolSizeVar, defaultValue: 1, min: 1, max: 8),
            BasePort = OccamEnvironment.GetInt(BasePortVar, defaultValue: 39_217, min: 1024, max: 65535),
            IdleTtlMs = OccamEnvironment.GetInt(IdleTtlVar, defaultValue: 120_000, min: 0, max: 3_600_000),
            MaxParallel = OccamEnvironment.GetInt(MaxParallelVar, defaultValue: 2, min: 1, max: 16, fallback: "WT_BROWSER_MAX_PARALLEL"),
        };
    }

    /// <summary>Port for slot 0…N-1. When <see cref="PoolSize"/> is 1, honors legacy <c>OCCAM_BROWSER_DAEMON_PORT</c>.</summary>
    public int ResolvePortForSlot(int slotId)
    {
        if (PoolSize == 1 && slotId == 0)
        {
            var legacy = OccamEnvironment.Get(DaemonPortVar);
            if (int.TryParse(legacy, out var port) && port is > 0 and <= 65535)
            {
                return port;
            }
        }

        return BasePort + slotId;
    }

    public bool IsEnabled =>
        BrowserExecutionProfile.UseSharedDaemon()
        && !string.Equals(Environment.GetEnvironmentVariable("OCCAM_BROWSER_DAEMON"), "0", StringComparison.Ordinal);
}
