using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Canary;

namespace OccamMcp.Core.Tools;

/// <summary>
/// MCP wrapper over <see cref="CanaryService.Verify"/> — adjudicates a claimed sentinel.
/// </summary>
[McpServerToolType]
public sealed class OccamCanaryVerifyTool(CanaryMcpRuntime canary)
{
    [McpServerTool(Name = "occam_canary_verify"), Description(
        "Verify that an agent actually read a canary page. Pass session_id from occam_canary_issue and the sentinel quoted from the fetched page. Verdicts: READ_VERIFIED, READ_STALE, HALLUCINATED, REPLAY_SUSPECT. Faking the sentinel will be detected.")]
    public Task<string> Verify(
        [Description("Session id returned by occam_canary_issue.")] string session_id,
        [Description("Sentinel value quoted from the canary page (must have been fetched, not guessed).")] string sentinel,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(session_id))
        {
            return Task.FromResult(Fail("invalid_arguments", "session_id must not be empty."));
        }

        if (string.IsNullOrWhiteSpace(sentinel))
        {
            return Task.FromResult(Fail("invalid_arguments", "sentinel must not be empty."));
        }

        var result = canary.VerifyForAgent(session_id.Trim(), sentinel.Trim());
        return Task.FromResult(JsonSerializer.Serialize(
            new OccamCanaryVerifySuccessResponse(
                Ok: result.IsReadEvidence,
                Verdict: result.VerdictWire,
                Bucket: result.MatchedBucket,
                Reason: result.Detail,
                SessionId: result.SessionId,
                CurrentBucket: result.CurrentBucket,
                MatchedBucket: result.MatchedBucket,
                BucketDistance: result.BucketDistance),
            OccamCanaryJsonContext.Default.OccamCanaryVerifySuccessResponse));
    }

    private static string Fail(string code, string message) =>
        JsonSerializer.Serialize(
            new OccamCanaryFailureResponse(false, new OccamCanaryFailureInfo(code, message), DateTimeOffset.UtcNow.ToString("O")),
            OccamCanaryJsonContext.Default.OccamCanaryFailureResponse);
}
