using OccamMcp.Core.Exam;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Tools;

namespace OccamMcp.Core.Agent;

/// <summary>
/// When heal is policy-eligible, point agents at a reachable next step:
/// heal tool if the live surface exposes it; otherwise widen to <c>full</c>
/// (or stop when <c>OCCAM_PROFILE_LOCK</c> freezes the surface).
/// </summary>
public static class HealSurfaceHints
{
    public const string HealToolName = "occam_playbook_heal";
    public const string WidenToolName = "occam_client_capabilities";
    public const string WidenAction = "widen_profile";
    public const string RelistAction = "relist_tools";

    /// <summary>
    /// Builds failure <c>agentHints</c> for a heal-eligible extract, or <c>null</c> when heal should not be offered.
    /// </summary>
    public static OccamTranscodeAgentHintsInfo? ForHealOffer(SessionToolSurface surface, bool healEligible)
    {
        if (!healEligible)
        {
            return null;
        }

        var doNot = new[]
        {
            "max_heal_per_url_per_turn=1",
            $"max_verify_retries={PlaybookHealPolicy.MaxVerifyRetries}",
        };

        if (surface.IsExposed(HealToolName))
        {
            return new OccamTranscodeAgentHintsInfo(
                HealToolName,
                doNot,
                Decisions:
                [
                    new ProbeDecision(
                        "heal",
                        "Playbook heal is available on the current surface — draft/fix a site recipe, then lint/save.",
                        Tool: HealToolName),
                ]);
        }

        if (surface.IsPinned)
        {
            return new OccamTranscodeAgentHintsInfo(
                "none",
                doNot,
                Warnings:
                [
                    $"heal_needs_full_locked: occam_playbook_heal requires profile=full, but OCCAM_PROFILE_LOCK=1 freezes '{surface.ProfileId}' — unlock or restart without LOCK to author playbooks.",
                ],
                Decisions:
                [
                    new ProbeDecision(
                        "heal_unavailable",
                        "Heal is eligible but the tool surface is locked below full — cannot widen mid-session.",
                        Tool: HealToolName,
                        Parameter: "OCCAM_PROFILE_LOCK"),
                ]);
        }

        return new OccamTranscodeAgentHintsInfo(
            WidenToolName,
            doNot,
            Warnings:
            [
                "heal_needs_full: occam_playbook_heal is not on the current surface — call occam_client_capabilities(profile=full), then tools/list (mustRelistTools), then occam_playbook_heal.",
            ],
            Decisions:
            [
                new ProbeDecision(
                    WidenAction,
                    "Widen the live tool surface to full so playbook heal/save are listed.",
                    Tool: WidenToolName,
                    Parameter: "profile=full"),
                new ProbeDecision(
                    RelistAction,
                    "After profileApplied/mustRelistTools, call tools/list before heal — do not use the previous tool set.",
                    Tool: "tools/list"),
                new ProbeDecision(
                    "heal",
                    "After tools/list shows occam_playbook_heal, call it once for this URL.",
                    Tool: HealToolName),
            ]);
    }
}
