using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Playbooks;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

[McpServerToolType]
public sealed class OccamPlaybookSaveTool(PlaybookSaveService saveService)
{
    [McpServerTool(Name = "occam_playbook_save"), Description("Save an extraction playbook/genome JSON you drafted (local only). Default verify=true dry-runs a transcode and rejects a recipe that fails the quality gate. Lint it first with occam_playbook_lint to catch schema errors without a fetch.")]
    public async Task<string> Save(
        [Description("Host key URL for playbook id resolution.")] string url,
        [Description("Full playbook JSON document (schema_version 1.x).")] string playbook_json,
        [Description("Dry-run transcode before write. Default true.")] bool verify = true,
        [Description("URL for verify transcode (default: url).")] string? verify_url = null,
        [Description("Optional lesson note appended on verified save (1-500 chars).")] string? lesson_note = null,
        [Description("Optional failure_reason echo for lesson entry.")] string? failure_reason = null,
        [Description("Optional host id for lesson entry (never secrets).")] string? host_id = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!string.IsNullOrWhiteSpace(lesson_note) && lesson_note.Trim().Length > 500)
        {
            return SerializeFailure(url, "playbook_schema_invalid", "lesson_note must be 1-500 characters.");
        }

        var request = new PlaybookSaveRequest(
            url,
            playbook_json,
            verify,
            verify_url,
            lesson_note,
            failure_reason,
            host_id);

        var result = await saveService.SaveAsync(request);
        if (result.Ok)
        {
            return JsonSerializer.Serialize(
                new OccamPlaybookSaveSuccessResponse(
                    true,
                    result.PlaybookId!,
                    result.WrittenPath!,
                    result.Verify is null
                        ? null
                        : new OccamPlaybookSaveVerifyInfo(
                            result.Verify.PassesGate,
                            result.Verify.Score,
                            result.Verify.NoiseLeakage),
                    result.LessonAppended,
                    result.SignedKeyId),
                OccamPlaybookSaveJsonContext.Default.OccamPlaybookSaveSuccessResponse);
        }

        var code = result.FailureCode ?? "playbook_save_rejected";
        var hints = FailureAgentHints.ForCode(code);
        return JsonSerializer.Serialize(
            new OccamPlaybookSaveFailureResponse(
                false,
                url,
                code,
                result.Message ?? "Save failed.",
                result.Verify is null
                    ? null
                    : new OccamPlaybookSaveVerifyInfo(
                        result.Verify.PassesGate,
                        result.Verify.Score,
                        result.Verify.NoiseLeakage),
                hints is null ? null : new OccamSaveFailureAgentHintsInfo(hints.Decisions)),
            OccamPlaybookSaveJsonContext.Default.OccamPlaybookSaveFailureResponse);
    }

    private static string SerializeFailure(string url, string code, string message)
    {
        var hints = FailureAgentHints.ForCode(code);
        return JsonSerializer.Serialize(
            new OccamPlaybookSaveFailureResponse(
                false,
                url,
                code,
                message,
                null,
                hints is null ? null : new OccamSaveFailureAgentHintsInfo(hints.Decisions)),
            OccamPlaybookSaveJsonContext.Default.OccamPlaybookSaveFailureResponse);
    }
}

public sealed record OccamPlaybookSaveVerifyInfo(bool PassesGate, int Score, double NoiseLeakage);

public sealed record OccamPlaybookSaveSuccessResponse(
    bool Ok,
    string PlaybookId,
    string WrittenPath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamPlaybookSaveVerifyInfo? Verify,
    bool LessonAppended,
    // SI-08: key id the playbook was signed with — the recipe is self-authenticating.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SignedKeyId = null);

public sealed record OccamPlaybookSaveFailureResponse(
    bool Ok,
    string Url,
    string FailureCode,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamPlaybookSaveVerifyInfo? Verify,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamSaveFailureAgentHintsInfo? AgentHints);

public sealed record OccamSaveFailureAgentHintsInfo(ProbeDecision[] Decisions);

[JsonSerializable(typeof(OccamPlaybookSaveSuccessResponse))]
[JsonSerializable(typeof(OccamPlaybookSaveFailureResponse))]
[JsonSerializable(typeof(OccamPlaybookSaveVerifyInfo))]
[JsonSerializable(typeof(OccamSaveFailureAgentHintsInfo))]
[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(ProbeDecision[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamPlaybookSaveJsonContext : JsonSerializerContext;
