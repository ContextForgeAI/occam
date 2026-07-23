using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Claims;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Tools;

/// <summary>
/// SI-16 — claim-check: ground an assertion in a page's provable source. Given {claim, url}, returns
/// the top relevant block(s) each with a Merkle citation proof + the signed extraction receipt, or an
/// honest found:false. The tool proves WHICH source text is relevant; the caller judges support vs
/// refute (never inferred here). A fact-checking primitive for agent pipelines.
/// </summary>
[McpServerToolType]
public sealed class OccamClaimCheckTool(IClaimCheckService claimCheckService)
{
    [McpServerTool(Name = "occam_claim_check"), Description("Does THIS page back up a specific claim? Extracts the page and returns the top relevant source block(s), each with a Merkle citation proof + signed receipt, or found:false. It proves which block is relevant (verify via occam_verify citation); YOU judge support vs refute - it won't guess. Params: claim, url, backend_policy, session_profile, max_matches.")]
    public async Task<string> Check(
        [Description("The assertion to ground against the page (a sentence).")] string claim,
        [Description("HTTP or HTTPS URL to check the claim against.")] string url,
        [Description("Backend policy: http, browser, or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Optional session profile id for gated pages.")] string? session_profile = null,
        [Description("Max relevant blocks to return (1-10). Default 3.")] int max_matches = 3,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url))
        {
            return SerializeFailure(url ?? "", claim ?? "", "invalid_arguments", "url must not be empty.");
        }

        if (string.IsNullOrWhiteSpace(claim))
        {
            return SerializeFailure(url, claim ?? "", "invalid_arguments", "claim must not be empty.");
        }

        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return SerializeFailure(url, claim, "invalid_arguments", "backend_policy must be http, browser, or http_then_browser.");
        }

        var (success, failure) = await claimCheckService.CheckAsync(
            url.Trim(), claim.Trim(), policy, session_profile, max_matches, cancellationToken);

        return failure is not null
            ? JsonSerializer.Serialize(failure, OccamClaimCheckJsonContext.Default.OccamClaimCheckFailureResponse)
            : JsonSerializer.Serialize(success!, OccamClaimCheckJsonContext.Default.OccamClaimCheckSuccessResponse);
    }

    private static string SerializeFailure(string url, string claim, string code, string message) =>
        JsonSerializer.Serialize(
            new OccamClaimCheckFailureResponse(false, url, claim, new OccamClaimCheckFailureInfo(code, message), null, DateTimeOffset.UtcNow.ToString("O")),
            OccamClaimCheckJsonContext.Default.OccamClaimCheckFailureResponse);
}
