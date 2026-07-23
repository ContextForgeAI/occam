namespace OccamMcp.Core.Playbooks;

/// <summary>Playbook resolver tier — higher rank shadows lower on host match.</summary>
public static class PlaybookProvenance
{
    public const string Local = "local";
    public const string User = "user";
    public const string Community = "community";
    public const string Seed = "seed";

    internal static int TierRank(string provenance) => provenance switch
    {
        Local => 4,
        User => 3,
        Community => 2,
        Seed => 1,
        _ => 0,
    };
}
