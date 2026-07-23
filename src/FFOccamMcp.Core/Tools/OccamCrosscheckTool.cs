using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Consensus;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Tools;

/// <summary>
/// SI-14 (local foundation) — cross-check a URL across several vantage points and report whether the
/// witnesses agree. Divergence proves cloaking / personalization / geo-variance / access-walling.
/// Opt-in (host sets <c>OCCAM_CONSENSUS_MCP=1</c>): it runs 2+ full extracts per call, so it is not
/// always-on. Each vantage carries a signed receipt, so the verdict is independently re-derivable.
/// </summary>
[McpServerToolType]
public sealed class OccamCrosscheckTool(IConsensusService consensusService)
{
    [McpServerTool(Name = "occam_crosscheck"), Description("Cross-check a URL across vantage points (http vs browser, anon vs session) and report whether they agree: verdict consensus | divergent | access_divergent | inconclusive, plus per-vantage signed receipts. Detects cloaking/personalization. Opt-in — host must set OCCAM_CONSENSUS_MCP=1.")]
    public async Task<string> Crosscheck(
        [Description("HTTP or HTTPS URL to cross-check.")] string url,
        [Description("Comma list of backend vantages to compare: http, browser. Default \"http,browser\".")] string vantages = "http,browser",
        [Description("Optional session profile id — adds an authenticated vantage per backend (anon-vs-authed axis).")] string? session_profile = null,
        [Description("Focus keywords for the fit_markdown prune, applied identically to every vantage.")] string? focus_query = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url))
        {
            return SerializeFailure(url ?? "", "invalid_arguments", "url must not be empty.");
        }

        if (!TryParseBackends(vantages, out var backends, out var backendError))
        {
            return SerializeFailure(url, "invalid_arguments", backendError!);
        }

        var (success, failureCode, message) = await consensusService.CrosscheckAsync(
            url.Trim(), backends, session_profile, focus_query, cancellationToken);

        return failureCode is not null
            ? SerializeFailure(url, failureCode, message ?? "Cross-check failed.")
            : JsonSerializer.Serialize(success!, OccamCrosscheckJsonContext.Default.OccamCrosscheckSuccessResponse);
    }

    private static bool TryParseBackends(string vantages, out IReadOnlyList<OccamBackendPolicy> backends, out string? error)
    {
        error = null;
        var result = new List<OccamBackendPolicy>();
        var seen = new HashSet<OccamBackendPolicy>();
        foreach (var raw in (vantages ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var policy = raw.ToLowerInvariant() switch
            {
                "http" => OccamBackendPolicy.Http,
                "browser" => OccamBackendPolicy.Browser,
                _ => (OccamBackendPolicy?)null,
            };
            if (policy is null)
            {
                backends = [];
                error = $"vantages may only contain 'http' or 'browser' (got '{raw}').";
                return false;
            }

            if (seen.Add(policy.Value))
            {
                result.Add(policy.Value);
            }
        }

        if (result.Count == 0)
        {
            result.Add(OccamBackendPolicy.Http);
            result.Add(OccamBackendPolicy.Browser);
        }

        backends = result;
        return true;
    }

    private static string SerializeFailure(string url, string code, string message) =>
        JsonSerializer.Serialize(
            new OccamCrosscheckFailureResponse(false, url, code, message, DateTimeOffset.UtcNow.ToString("O")),
            OccamCrosscheckJsonContext.Default.OccamCrosscheckFailureResponse);
}
