using OccamMcp.Core.Transport;

namespace OccamMcp.Core.Exam;

/// <summary>
/// Session-scoped tool surface used when <c>OCCAM_EXAM_MCP=1</c>. Starts at reader (or an
/// operator-pinned <c>OCCAM_PROFILE</c>); exam submit may narrow/widen unless pinned.
/// </summary>
public sealed class SessionToolSurface
{
    /// <summary>Opt-in MCP exam submit tool — always visible while the feature flag is on.</summary>
    public const string ExamSubmitToolName = "occam_exam_submit";

    private readonly object _gate = new();
    private string _profileId;
    private int _generation;

    public SessionToolSurface(string initialProfile, bool isPinned)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(initialProfile);
        _profileId = initialProfile.Trim().ToLowerInvariant();
        IsPinned = isPinned;
    }

    /// <summary>True when the operator set <c>OCCAM_PROFILE</c> — exam must not change the surface.</summary>
    public bool IsPinned { get; }

    /// <summary>Active profile id (<c>minimal|basic|reader|…</c>).</summary>
    public string ProfileId
    {
        get
        {
            lock (_gate)
            {
                return _profileId;
            }
        }
    }

    /// <summary>Increments when the advertised surface actually changes.</summary>
    public int Generation
    {
        get
        {
            lock (_gate)
            {
                return _generation;
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="toolName"/> is advertised / callable under the current surface.
    /// The exam submit tool stays reachable so a weak agent can re-submit.
    /// </summary>
    public bool IsExposed(string toolName)
    {
        if (string.Equals(toolName, ExamSubmitToolName, StringComparison.Ordinal))
        {
            return true;
        }

        string profile;
        lock (_gate)
        {
            profile = _profileId;
        }

        return OccamToolProfile.IsExposed(toolName, profile);
    }

    /// <summary>
    /// Applies a tier-derived profile. No-op when pinned or when the profile is unchanged.
    /// </summary>
    /// <returns><c>true</c> when the surface changed (caller should send <c>list_changed</c>).</returns>
    public bool TryApplyExamTier(AgentTier tier)
    {
        if (IsPinned)
        {
            return false;
        }

        var next = ExamScoring.ProfileFor(tier);
        lock (_gate)
        {
            if (string.Equals(_profileId, next, StringComparison.Ordinal))
            {
                return false;
            }

            _profileId = next;
            _generation++;
            return true;
        }
    }

    /// <summary>Snapshot of exposed core tool names (excludes the opt-in exam submit tool).</summary>
    public string[] GetExposedCoreToolNames()
    {
        string profile;
        lock (_gate)
        {
            profile = _profileId;
        }

        return OccamToolProfile.GetExposedToolNames(profile);
    }
}
