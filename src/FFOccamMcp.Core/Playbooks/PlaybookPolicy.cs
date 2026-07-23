namespace OccamMcp.Core.Playbooks;

public static class PlaybookPolicy
{
    public const string Off = "off";
    public const string Auto = "auto";

    public static bool ShouldApply(string? policy) =>
        string.Equals(Normalize(policy), Auto, StringComparison.OrdinalIgnoreCase);

    public static string Normalize(string? policy)
    {
        if (string.IsNullOrWhiteSpace(policy)
            || string.Equals(policy, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Off;
        }

        return policy.Trim().ToLowerInvariant();
    }

    public static bool TryParse(string? policy, out string normalized, out string? error)
    {
        normalized = Normalize(policy);
        if (normalized is Off or Auto)
        {
            error = null;
            return true;
        }

        error = "playbook_policy must be off or auto.";
        return false;
    }
}
