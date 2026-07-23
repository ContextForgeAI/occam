using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Attest;
using OccamMcp.Core.Claims;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Tools;

/// <summary>
/// SI-11 — attest: three-layer citation check. Retrieval (BM25) surfaces candidate blocks;
/// a semantic classifier returns status (supported/contradicted/related/unsupported/unknown);
/// Merkle proof proves only that a cited block existed in the signed extract. Fail-closed:
/// gate on status; never treat retrieval score or proof as claim support. grounded is a compat
/// alias for status=supported only.
/// </summary>
[McpServerToolType]
public sealed class OccamAttestTool(IAttestService attestService)
{
    private const int MaxClaims = 50;

    [McpServerTool(Name = "occam_attest"), Description("Before shipping a report, check citations with a fail-closed trust model: retrieval finds candidate blocks, a semantic classifier returns status (supported|contradicted|related|unsupported|unknown), and Merkle proof proves only that a block existed — never that the claim is true. grounded is true ONLY when status=supported (NOT from BM25/lexical score). Gate on status. Params: claims (JSON array, required), backend_policy, session_profile.")]
    public async Task<string> Attest(
        [Description("JSON array of claim rows, each {\"claim\":\"...\",\"sourceUrl\":\"https://...\"} (1-50).")] string claims,
        [Description("Backend policy applied to every claim: http, browser, or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Optional session profile id applied to every cited page.")] string? session_profile = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(claims))
        {
            return Fail("invalid_arguments", "claims must be a non-empty JSON array of {claim, sourceUrl}.");
        }

        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return Fail("invalid_arguments", "backend_policy must be http, browser, or http_then_browser.");
        }

        OccamAttestClaimInput[]? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize(claims, OccamAttestJsonContext.Default.OccamAttestClaimInputArray);
        }
        catch (JsonException ex)
        {
            return Fail("invalid_arguments", $"claims is not valid JSON: {ex.Message}");
        }

        if (parsed is null || parsed.Length == 0)
        {
            return Fail("invalid_arguments", "claims must be a non-empty JSON array of {claim, sourceUrl}.");
        }

        if (parsed.Length > MaxClaims)
        {
            return Fail("invalid_arguments", $"too many claims ({parsed.Length}); max {MaxClaims} per call.");
        }

        var response = await attestService.AttestAsync(parsed, policy, session_profile, cancellationToken);
        return JsonSerializer.Serialize(response, OccamAttestJsonContext.Default.OccamAttestResponse);
    }

    private static string Fail(string code, string message) =>
        JsonSerializer.Serialize(
            new OccamAttestFailureResponse(false, new OccamClaimCheckFailureInfo(code, message), DateTimeOffset.UtcNow.ToString("O")),
            OccamAttestJsonContext.Default.OccamAttestFailureResponse);
}
