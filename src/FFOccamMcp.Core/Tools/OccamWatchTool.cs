using System.ComponentModel;
using System.Text.Json;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Watch;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Stateful page-change watch (opt-in; host sets <c>OCCAM_WATCH_MCP=1</c>). First call records the
/// page's content hash + block hashes; later calls report whether it changed and (optionally) the
/// block-level diff. The agent calls on its own cadence — there is no daemon in Core. This is the
/// stateful convenience form of Recipe W (<c>if_none_match</c> + <c>diff_against</c>).
/// </summary>
[McpServerToolType]
public sealed class OccamWatchTool(IWatchService watchService)
{
    [McpServerTool(Name = "occam_watch"), Description("Stateful page-change watch. First call records the page; later calls return changed:true/false plus a block-level diff when it changed. State is kept server-side keyed by URL. Opt-in — host must set OCCAM_WATCH_MCP=1.")]
    public async Task<string> Watch(
        [Description("HTTP or HTTPS URL to watch.")] string url,
        [Description("Backend policy: http, browser, or http_then_browser.")] string backend_policy = "http_then_browser",
        [Description("Focus keywords for the fit_markdown prune (narrows what counts as a change).")] string? focus_query = null,
        [Description("Optional session profile id.")] string? session_profile = null,
        [Description("Playbook merge policy: off or auto. Default auto.")] string playbook_policy = "auto",
        [Description("Include the block-level diff (addedBlocks/removedHashes) when the page changed. Default true.")] bool include_diff = true,
        [Description("Reset the stored baseline: treat this call as the first sighting (changed=false) and overwrite prior state.")] bool reset = false,
        [Description("Return the full signed change-history chain (SI-05) in `history`. Default false — the response always carries historyLength + the latest entry.")] bool include_history = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url))
        {
            return SerializeFailure(url ?? "", "invalid_arguments", "url must not be empty.");
        }

        if (!OccamBackendPolicyParser.TryParse(backend_policy, out var policy))
        {
            return SerializeFailure(url, "invalid_arguments", "backend_policy must be http, browser, or http_then_browser.");
        }

        if (!OccamTranscodeOptionsParser.TryBuild(
                max_tokens: null,
                fit_markdown: !string.IsNullOrWhiteSpace(focus_query),
                focus_query,
                content_selectors: null,
                session_profile,
                playbook_policy,
                if_none_match: null,
                out var options,
                out var optionsError))
        {
            return SerializeFailure(url, "invalid_arguments", optionsError ?? "Invalid watch options.");
        }

        var (success, failure) = await watchService.WatchAsync(url.Trim(), policy, options, reset, include_diff, include_history, cancellationToken);
        return failure is not null
            ? SerializeFailure(url, failure.Code, failure.Message)
            : JsonSerializer.Serialize(success!, OccamWatchJsonContext.Default.OccamWatchSuccessResponse);
    }

    private static string SerializeFailure(string url, string code, string message) =>
        JsonSerializer.Serialize(
            new OccamWatchFailureResponse(false, url, new OccamWatchFailureInfo(code, message)),
            OccamWatchJsonContext.Default.OccamWatchFailureResponse);
}
