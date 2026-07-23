using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Playbooks;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamPlaybookHealTool(PlaybookHealService healService)
{
    [McpServerTool(Name = "occam_playbook_heal"), Description("When a transcode fails on a hard site with no recipe, capture the page's DOM skeleton + selector candidates so you can draft a playbook for it (then save with occam_playbook_save). This gathers the evidence; you write the recipe JSON.")]
    public async Task<string> Heal(
        [Description("Absolute HTTP(S) URL to heal.")] string url,
        [Description("Prior failure.code from occam_transcode (e.g. thin_extract).")] string failure_reason,
        [Description("Optional session profile id (same as occam_transcode).")] string? session_profile = null,
        [Description("Max skeleton nodes (default 600, cap 600).")] int max_skeleton_nodes = 600,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var request = new PlaybookHealRequest(
            url,
            failure_reason,
            session_profile,
            max_skeleton_nodes);

        var result = await healService.HealAsync(request);
        if (result.Ok)
        {
            return JsonSerializer.Serialize(
                OccamPlaybookHealResponseMapper.MapSuccess(result),
                OccamPlaybookHealJsonContext.Default.OccamPlaybookHealSuccessResponse);
        }

        return JsonSerializer.Serialize(
            OccamPlaybookHealResponseMapper.MapFailure(result),
            OccamPlaybookHealJsonContext.Default.OccamPlaybookHealFailureResponse);
    }
}

internal static class OccamPlaybookHealResponseMapper
{
    public static OccamPlaybookHealSuccessResponse MapSuccess(PlaybookHealResult result) =>
        new(
            true,
            result.Url,
            result.FailureReason,
            result.DomSkeleton is null
                ? null
                : new OccamDomSkeletonInfo(
                    MapNode(result.DomSkeleton.Root),
                    new OccamDomSkeletonStatsInfo(
                        result.DomSkeleton.Stats.NodeCount,
                        result.DomSkeleton.Stats.MaxDepth,
                        result.DomSkeleton.Stats.InteractiveCount)),
            result.Anchors is null
                ? null
                : new OccamHealAnchorsInfo(
                    result.Anchors.Landmarks,
                    result.Anchors.DataTestIds,
                    result.Anchors.MainCandidates
                        .Select(c => new OccamMainCandidateInfo(c.Selector, c.TextAnchor, c.Score))
                        .ToArray()),
            result.AgentHints is null
                ? null
                : new OccamHealAgentHintsInfo(
                    result.AgentHints.SuggestedNext,
                    result.AgentHints.DoNot,
                    result.AgentHints.MaxVerifyRetries));

    public static OccamPlaybookHealFailureResponse MapFailure(PlaybookHealResult result)
    {
        var code = result.FailureCode ?? "heal_failed";
        var hints = FailureAgentHints.ForCode(code);
        return new(
            false,
            result.Url,
            result.FailureReason,
            code,
            result.Message ?? "Heal failed.",
            hints is null ? null : new OccamHealFailureAgentHintsInfo(hints.Decisions));
    }

    private static OccamDomSkeletonNodeInfo MapNode(DomSkeletonNode node) =>
        new(
            node.Tag,
            node.Id,
            node.Class,
            node.Role,
            node.TestId,
            node.Aria,
            node.Text,
            node.Interactive,
            node.Children?.Select(MapNode).ToArray());
}

public sealed record OccamDomSkeletonStatsInfo(int NodeCount, int MaxDepth, int InteractiveCount);

public sealed record OccamDomSkeletonNodeInfo(
    string Tag,
    string? Id,
    IReadOnlyList<string>? Class,
    string? Role,
    string? TestId,
    string? Aria,
    string? Text,
    bool Interactive,
  [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDomSkeletonNodeInfo[]? Children);

public sealed record OccamDomSkeletonInfo(
    OccamDomSkeletonNodeInfo Root,
    OccamDomSkeletonStatsInfo Stats);

public sealed record OccamMainCandidateInfo(string Selector, string? TextAnchor, double Score);

public sealed record OccamHealAnchorsInfo(
    IReadOnlyList<string> Landmarks,
    IReadOnlyList<string> DataTestIds,
    OccamMainCandidateInfo[] MainCandidates);

public sealed record OccamHealAgentHintsInfo(
    string SuggestedNext,
    IReadOnlyList<string> DoNot,
    int MaxVerifyRetries);

public sealed record OccamPlaybookHealSuccessResponse(
    bool Ok,
    string Url,
    string FailureReason,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamDomSkeletonInfo? DomSkeleton,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamHealAnchorsInfo? Anchors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamHealAgentHintsInfo? AgentHints);

public sealed record OccamPlaybookHealFailureResponse(
    bool Ok,
    string Url,
    string FailureReason,
    string FailureCode,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamHealFailureAgentHintsInfo? AgentHints);

public sealed record OccamHealFailureAgentHintsInfo(ProbeDecision[] Decisions);

[JsonSerializable(typeof(OccamPlaybookHealSuccessResponse))]
[JsonSerializable(typeof(OccamPlaybookHealFailureResponse))]
[JsonSerializable(typeof(OccamDomSkeletonInfo))]
[JsonSerializable(typeof(OccamDomSkeletonNodeInfo))]
[JsonSerializable(typeof(OccamDomSkeletonStatsInfo))]
[JsonSerializable(typeof(OccamHealAnchorsInfo))]
[JsonSerializable(typeof(OccamMainCandidateInfo))]
[JsonSerializable(typeof(OccamHealAgentHintsInfo))]
[JsonSerializable(typeof(OccamHealFailureAgentHintsInfo))]
[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(ProbeDecision[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamPlaybookHealJsonContext : JsonSerializerContext;
