using OccamMcp.Core.Configuration;

namespace OccamMcp.Core.Transport;

/// <summary>
/// Role-scoped MCP tool surface via <c>OCCAM_PROFILE</c>. Default <see cref="Full"/> keeps all
/// fifteen core tools. Narrower profiles hide playbook-authoring (and other) tools so agents
/// do not drift into heal/save on a simple read.
/// </summary>
public static class OccamToolProfile
{
    public const string Full = "full";
    public const string Reader = "reader";
    public const string Researcher = "researcher";
    public const string Auditor = "auditor";

    private static readonly string[] ReaderTools =
    [
        "occam_client_capabilities",
        "occam_transcode",
        "occam_probe",
        "occam_digest",
        "occam_map",
        "occam_search",
        "occam_extract_knowledge",
    ];

    private static readonly string[] ResearcherExtra =
    [
        "occam_claim_check",
        "occam_verify",
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
            return Full;
        }

        var normalized = raw.Trim().ToLowerInvariant();
        if (normalized is Full or Reader or Researcher or Auditor)
        {
            return normalized;
        }

        Console.Error.WriteLine(
            $"[occam.config] OCCAM_PROFILE='{raw}' is not full|reader|researcher|auditor — using default full.");
        return Full;
    }

    /// <summary>Core tool names exposed for the active (or given) profile.</summary>
    public static string[] GetExposedToolNames(string? profile = null)
    {
        var id = string.IsNullOrWhiteSpace(profile) ? Resolve() : profile.Trim().ToLowerInvariant();
        return id switch
        {
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
