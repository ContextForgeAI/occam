using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Canary;

namespace OccamMcp.Core.Tools;

/// <summary>
/// MCP wrapper: mint a canary probe URL + session for proof-of-read. Never returns the sentinel —
/// the agent must fetch <c>url</c> (typically via <c>occam_transcode</c>) and quote what it read.
/// </summary>
[McpServerToolType]
public sealed class OccamCanaryIssueTool(CanaryMcpRuntime canary)
{
    [McpServerTool(Name = "occam_canary_issue"), Description(
        "Issue a proof-of-read canary URL + session. Returns a canary URL. Quote the sentinel only if you actually fetched the page. Faking it will be detected. Never invent the sentinel. Next: occam_transcode(url) → occam_canary_verify(session_id, sentinel).")]
    public async Task<string> Issue(
        [Description("Optional session id (1..128 chars of [A-Za-z0-9._-]). Generated when omitted.")] string? session_id = null,
        [Description("Optional advisory TTL seconds for expires_at (30–86400). Crypto buckets are unchanged.")] int? ttl_seconds = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (ttl_seconds is < 30 or > 86_400)
        {
            return Fail("invalid_arguments", "ttl_seconds must be between 30 and 86400 when set.");
        }

        try
        {
            var issued = await canary.IssueForAgentAsync(session_id, ttl_seconds, cancellationToken)
                .ConfigureAwait(false);
            return JsonSerializer.Serialize(
                new OccamCanaryIssueSuccessResponse(
                    true,
                    issued.Url,
                    issued.SessionId,
                    issued.ExpiresAt.ToString("O"),
                    issued.Bucket),
                OccamCanaryJsonContext.Default.OccamCanaryIssueSuccessResponse);
        }
        catch (ArgumentException ex)
        {
            return Fail("invalid_arguments", ex.Message);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Fail("canary_unavailable", $"Canary probe host failed to start: {ex.Message}");
        }
    }

    private static string Fail(string code, string message) =>
        JsonSerializer.Serialize(
            new OccamCanaryFailureResponse(false, new OccamCanaryFailureInfo(code, message), DateTimeOffset.UtcNow.ToString("O")),
            OccamCanaryJsonContext.Default.OccamCanaryFailureResponse);
}
