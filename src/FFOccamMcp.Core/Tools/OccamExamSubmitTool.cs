using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OccamMcp.Core.Exam;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Grades a capability-exam harness submission and, when <c>OCCAM_PROFILE</c> is not pinned,
/// applies the resulting tool surface then notifies <c>notifications/tools/list_changed</c>.
/// Opt-in — host must set <c>OCCAM_EXAM_MCP=1</c>.
/// </summary>
[McpServerToolType]
public sealed class OccamExamSubmitTool(ExamMcpRuntime exam)
{
    [McpServerTool(Name = SessionToolSurface.ExamSubmitToolName), Description(
        "Grade a capability-exam submission (same JSON as `occam exam grade`). Returns score/tier/recommended profile. " +
        "When OCCAM_PROFILE is not pinned, applies the tier surface and sends tools/list_changed. " +
        "Opt-in — host must set OCCAM_EXAM_MCP=1. Does not fetch the web; pass a behaviour record.")]
    public async Task<string> Submit(
        [Description("Harness submission JSON: clientInfo, modelHint, sessionId, canaryVerdict, basicCallArguments, focusBudgetArguments, chain.")] string submission,
        McpServer server,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(submission))
        {
            return Fail("invalid_arguments", "submission must not be empty.");
        }

        var outcome = exam.Submit(submission);
        if (!outcome.Ok)
        {
            return Fail(outcome.FailureCode ?? "invalid_arguments", outcome.FailureMessage ?? "invalid submission.");
        }

        if (outcome.Applied)
        {
            try
            {
                await server.SendNotificationAsync(
                        NotificationMethods.ToolListChangedNotification,
                        cancellationToken)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                // Surface already changed; notification is best-effort for clients that support it.
                Console.Error.WriteLine($"[occam.exam] list_changed notify failed: {ex.Message}");
            }
        }

        return JsonSerializer.Serialize(
            new OccamExamSubmitSuccessResponse(
                Ok: true,
                Score: outcome.Result.Score,
                MaxScore: ExamResult.MaxScore,
                Tier: outcome.Result.TierWire,
                Source: outcome.Result.Source,
                RecommendedProfile: outcome.RecommendedProfile,
                ToolCount: outcome.ToolCount,
                Applied: outcome.Applied,
                Pinned: outcome.Pinned,
                ActiveProfile: outcome.ActiveProfile,
                ListChangedSent: outcome.Applied,
                ClientInfo: outcome.Subject.ClientInfo,
                ModelHint: outcome.Subject.ModelHint,
                SessionId: outcome.Subject.SessionId,
                SelfReportTier: outcome.SelfReportTier is { } r ? ExamWire.ToWire(r) : null,
                StaticMapTier: ExamWire.ToWire(outcome.StaticMapTier),
                Outcomes: outcome.Result.Outcomes
                    .Select(o => new OccamExamTaskOutcomeWire(ExamWire.ToWire(o.Task), o.Passed, o.Detail))
                    .ToArray()),
            OccamExamJsonContext.Default.OccamExamSubmitSuccessResponse);
    }

    private static string Fail(string code, string message) =>
        JsonSerializer.Serialize(
            new OccamExamFailureResponse(false, new OccamExamFailureInfo(code, message), DateTimeOffset.UtcNow.ToString("O")),
            OccamExamJsonContext.Default.OccamExamFailureResponse);
}

public sealed record OccamExamTaskOutcomeWire(string Task, bool Passed, string Detail);

public sealed record OccamExamSubmitSuccessResponse(
    bool Ok,
    int Score,
    int MaxScore,
    string Tier,
    string Source,
    string RecommendedProfile,
    int ToolCount,
    bool Applied,
    bool Pinned,
    string ActiveProfile,
    bool ListChangedSent,
    string ClientInfo,
    string ModelHint,
    string SessionId,
    string? SelfReportTier,
    string StaticMapTier,
    OccamExamTaskOutcomeWire[] Outcomes);

public sealed record OccamExamFailureInfo(string Code, string Message);

public sealed record OccamExamFailureResponse(bool Ok, OccamExamFailureInfo Failure, string Timestamp);

[JsonSerializable(typeof(OccamExamSubmitSuccessResponse))]
[JsonSerializable(typeof(OccamExamFailureResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
public partial class OccamExamJsonContext : JsonSerializerContext;
