using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Role-scoped MCP tool surface via <c>OCCAM_PROFILE</c>. Default <see cref="Reader"/> exposes
/// nine day-to-day tools; <see cref="Full"/> keeps all sixteen core tools. Narrower profiles
/// hide playbook-authoring (and other) tools so agents do not drift into heal/save on a simple read.
/// </summary>
public static class OccamToolProfile
{
    public const string Full = "full";
    public const string Reader = "reader";
    public const string Researcher = "researcher";
    public const string Auditor = "auditor";

    /// <summary>
    /// One tool. The surface a client assessed as <see cref="Exam.AgentTier.Weak"/> sees: there is
    /// nothing to select between, so tool-selection failure is impossible by construction.
    /// </summary>
    public const string Minimal = "minimal";

    /// <summary>
    /// Three tools — read one page, read several, find pages. The surface for
    /// <see cref="Exam.AgentTier.Medium"/>.
    /// </summary>
    public const string Basic = "basic";

    private static readonly string[] MinimalTools =
    [
        "occam",
    ];

    /// <summary>
    /// Read one page, read several, find pages. <c>occam_client_capabilities</c> is deliberately
    /// absent: declaring a context budget is valuable, but for a narrow surface it is one more tool
    /// to pick wrong, and an operator can set <c>OCCAM_CLIENT_CONTEXT_TOKENS</c> instead.
    /// </summary>
    private static readonly string[] BasicTools =
    [
        "occam",
        "occam_digest",
        "occam_search",
    ];

    private static readonly string[] ReaderTools =
    [
        "occam_client_capabilities",
        "occam",
        "occam_transcode",
        "occam_probe",
        "occam_digest",
        "occam_map",
        "occam_search",
        "occam_extract_knowledge",
        "occam_verify",
    ];

    private static readonly string[] ResearcherExtra =
    [
        "occam_claim_check",
    ];

    private static readonly string[] AuditorExtra =
    [
        "occam_attest",
        "occam_dataset_export",
        "occam_playbook_lint",
    ];

    /// <summary>Resolved profile id: <c>full</c> | <c>reader</c> | <c>researcher</c> | <c>auditor</c>.</summary>
    public static string Resolve()
    {
        var raw = OccamEnvironment.Get("OCCAM_PROFILE");
        if (string.IsNullOrWhiteSpace(raw))
        {
            // Default surface is reader (not all 16 core tools) — playbook authoring stays opt-in via full.
            return Reader;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is Full or Reader or Researcher or Auditor or Minimal or Basic)
        {
            return normalized;
        }

        Console.Error.WriteLine(
            $"[occam.config] OCCAM_PROFILE='{raw}' is not minimal|basic|reader|researcher|auditor|full — using default reader.");
        return Reader;
    }

    /// <summary>Core tool names exposed for the active (or given) profile.</summary>
    public static string[] GetExposedToolNames(string? profile = null)
    {
        var id = string.IsNullOrWhiteSpace(profile) ? Resolve() : profile.Trim().ToLowerInvariant();
        return id switch
        {
            Minimal => (string[])MinimalTools.Clone(),
            Basic => (string[])BasicTools.Clone(),
            Reader => (string[])ReaderTools.Clone(),
            Researcher => Concat(ReaderTools, ResearcherExtra),
            Auditor => Concat(ReaderTools, ResearcherExtra, AuditorExtra),
            _ => (string[])OccamMcpServerRegistration.OccamToolNames.Clone(),
        };
    }

    public static bool IsExposed(string toolName, string? profile = null)
    {
        var names = GetExposedToolNames(profile);
        for (var i = 0; i < names.Length; i++)
        {
            if (string.Equals(names[i], toolName, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private static string[] Concat(params string[][] parts)
    {
        var total = 0;
        foreach (var p in parts)
        {
            total += p.Length;
        }

        var result = new string[total];
        var offset = 0;
        foreach (var p in parts)
        {
            Array.Copy(p, 0, result, offset, p.Length);
            offset += p.Length;
        }

        return result;
    }
}
