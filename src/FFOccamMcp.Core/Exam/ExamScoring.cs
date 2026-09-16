using OccamMcp.Core.Transport;

namespace OccamMcp.Core.Exam;

/// <summary>
/// The two mappings that turn an exam into a tool surface: score → tier, and tier → profile.
/// </summary>
/// <remarks>
/// <para>
/// Both are deliberately coarse. A four-point exam cannot support finer resolution than three
/// tiers, and a mapping with more steps than the evidence justifies invites tuning that looks like
/// progress. If H3 (docs/research/hypothesis.md) survives measurement, the thresholds are the first
/// thing worth fitting to data; until then they are a stated default, not a result.
/// </para>
/// <para>
/// Note the asymmetry: a score of 0 and a score of 1 both yield <see cref="AgentTier.Weak"/>, while
/// only a perfect 4 yields <see cref="AgentTier.Strong"/>. Widening the surface is the risky
/// direction — a client that mis-selects among fifteen tools burns the user's turn — so the bar for
/// the widest surface is the highest.
/// </para>
/// </remarks>
public static class ExamScoring
{
    /// <summary>Highest score that still maps to <see cref="AgentTier.Weak"/>.</summary>
    public const int WeakCeiling = 1;

    /// <summary>Highest score that still maps to <see cref="AgentTier.Medium"/>.</summary>
    public const int MediumCeiling = 3;

    /// <summary>Maps an exam score to a tier, clamping out-of-range input rather than throwing.</summary>
    /// <param name="score">Points awarded, normally 0–4.</param>
    /// <returns>The tier for that score.</returns>
    public static AgentTier TierFor(int score)
    {
        var clamped = Math.Clamp(score, 0, ExamResult.MaxScore);
        return clamped <= WeakCeiling ? AgentTier.Weak
            : clamped <= MediumCeiling ? AgentTier.Medium
            : AgentTier.Strong;
    }

    /// <summary>
    /// Maps a tier to the <c>OCCAM_PROFILE</c> whose surface that tier should see:
    /// one tool, three, or all fifteen.
    /// </summary>
    /// <param name="tier">Tier to map.</param>
    /// <returns>A profile id accepted by <see cref="OccamToolProfile"/>.</returns>
    public static string ProfileFor(AgentTier tier) => tier switch
    {
        AgentTier.Weak => OccamToolProfile.Minimal,
        AgentTier.Strong => OccamToolProfile.Full,
        _ => OccamToolProfile.Basic,
    };

    /// <summary>Number of tools a tier is allowed to see.</summary>
    /// <param name="tier">Tier to size.</param>
    /// <returns>Count of exposed core tools.</returns>
    public static int SurfaceSizeFor(AgentTier tier) =>
        OccamToolProfile.GetExposedToolNames(ProfileFor(tier)).Length;
}
