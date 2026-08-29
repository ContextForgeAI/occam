using System.ComponentModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.BrowserActions;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Json;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Session;
using OccamMcp.Core.Workers;
using ModelContextProtocol;
using ModelContextProtocol.Server;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Opt-in browser interaction + materialization (<c>OCCAM_BROWSER_ACTIONS_MCP=1</c>).
/// Never cached. Typed text is redacted from traces. No raw page JS surface.
/// </summary>
[McpServerToolType]
public sealed class OccamBrowserInteractTool(
    WorkerPaths workerPaths,
    IBrowserPoolManager browserPool,
    IBrowserDaemonClient browserDaemonClient,
    ReceiptSigner receiptSigner,
    OccamMcp.Core.Client.ClientCapabilityStore clientCapabilities)
{
    [McpServerTool(Name = "occam_browser_interact"), Description("Run a short declarative browser action plan (click/type/scroll/wait/…) then materialize the resulting page as Markdown with a signed receipt. Opt-in — host must set OCCAM_BROWSER_ACTIONS_MCP=1. Max 16 steps; first failure stops the plan. Typed text is never returned in traces. Not cached.")]
    public async Task<string> Interact(
        [Description("HTTP or HTTPS URL to open before running actions.")] string url,
        [Description("JSON array of actions: {do, selector?, text?, key?, to?, ms?, px?, timeout_ms?}. do ∈ wait|wait_selector|wait_text|click|hover|type|press|scroll.")] string actions,
        [Description("Optional session profile id.")] string? session_profile = null,
        [Description("Focus keywords for optional fit_markdown prune after extract.")] string? focus_query = null,
        [Description("Optional whole-response token budget (min 128).")] int? max_tokens = null,
        [Description("Overall action deadline in milliseconds (default 90000).")] int? deadline_ms = null,
        IProgress<ProgressNotificationValue>? progress = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (string.IsNullOrWhiteSpace(url))
        {
            return Fail("", "invalid_arguments", "url must not be empty.");
        }

        JsonElement actionsEl;
        try
        {
            using var doc = JsonDocument.Parse(string.IsNullOrWhiteSpace(actions) ? "null" : actions);
            actionsEl = doc.RootElement.Clone();
        }
        catch (JsonException)
        {
            return Fail(url, "invalid_arguments", "actions must be valid JSON.");
        }

        var validated = BrowserActionPlan.Validate(actionsEl);
        if (!validated.Ok || validated.Actions is null)
        {
            return Fail(url, validated.FailureCode ?? "invalid_arguments", validated.Message ?? "invalid actions");
        }

        var planHash = BrowserActionPlan.PlanHash(validated.Actions);
        var stepTotal = validated.Actions.Count;
        progress?.Report(new ProgressNotificationValue
        {
            Progress = 0,
            Total = stepTotal + 1,
            Message = $"actions validated ({stepTotal})",
        });

        var deadline = deadline_ms is > 0
            ? Math.Clamp(deadline_ms.Value, 1_000, 180_000)
            : BrowserActionPlan.DefaultDeadlineMs;

        var preflight = FetchPreflight.Prepare(url.Trim(), session_profile);
        if (!preflight.Ok)
        {
            return Fail(url, preflight.FailureCode ?? "invalid_arguments", preflight.FailureMessage ?? "preflight failed");
        }

        using var headersScope = preflight.HeadersScope;
        var workerActionsJson = BrowserActionPlan.SerializeForWorker(validated.Actions);
        var interactResult = await RunInteractAsync(
            url.Trim(),
            workerActionsJson,
            deadline,
            preflight.ActiveHeadersFile,
            preflight.ActiveStorageStatePath,
            cancellationToken).ConfigureAwait(false);

        progress?.Report(new ProgressNotificationValue
        {
            Progress = interactResult.StepsRun,
            Total = stepTotal + 1,
            Message = interactResult.Ok
                ? $"actions done ({interactResult.StepsRun}/{stepTotal}); materializing"
                : $"action_failed at step {interactResult.FailedIndex?.ToString() ?? "?"}",
        });

        if (!interactResult.Ok)
        {
            return JsonSerializer.Serialize(
                new OccamBrowserInteractFailureResponse(
                    false,
                    url.Trim(),
                    interactResult.FinalUrl,
                    interactResult.Failure ?? "action_failed",
                    interactResult.Message ?? "browser interact failed",
                    planHash,
                    interactResult.FailedIndex,
                    interactResult.StepsRun,
                    interactResult.ActionTrace),
                OccamBrowserInteractJsonContext.Default.OccamBrowserInteractFailureResponse);
        }

        var markdown = interactResult.Markdown ?? "";
        var budget = clientCapabilities.ResolveMaxTokens(max_tokens);
        var (fitted, truncated, truncationStrategy) = TokenBudget.Apply(
            markdown,
            budget,
            focus_query);

        var contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(fitted))).ToLowerInvariant();
        OccamTranscodeReceiptInfo? receipt = null;
        if (ReceiptsPolicy.Enabled() && !string.IsNullOrEmpty(fitted))
        {
            var outcome = new TranscodeOutcome(
                Ok: true,
                Markdown: fitted,
                FinalUrl: interactResult.FinalUrl,
                Backend: interactResult.Backend ?? "browser_playwright",
                FailureCode: null,
                Message: null,
                LatencyMs: interactResult.LatencyMs,
                TokensEstimated: TokenEstimator.Estimate(fitted),
                Truncated: truncated,
                TruncationStrategy: truncationStrategy);
            receipt = OccamTranscodeResponseBuilder.BuildReceipt(
                outcome, url.Trim(), receiptSigner, actionPlanHash: planHash);
        }

        return OccamJsonPrintableEscapes.Serialize(
            new OccamBrowserInteractSuccessResponse(
                true,
                url.Trim(),
                interactResult.FinalUrl ?? url.Trim(),
                fitted,
                interactResult.Backend ?? "browser_playwright",
                planHash,
                interactResult.StepsRun,
                interactResult.ActionTrace,
                contentHash,
                truncated,
                truncationStrategy,
                receipt,
                Cached: false),
            OccamBrowserInteractJsonContext.Default.OccamBrowserInteractSuccessResponse);
    }

    private async Task<BrowserInteractWorkerResult> RunInteractAsync(
        string url,
        string actionsJson,
        int deadlineMs,
        string? headersFile,
        string? storageStateFile,
        CancellationToken cancellationToken)
    {
        if (browserPool.IsEnabled && await browserPool.TryEnsureMinimumHealthyAsync(workerPaths).ConfigureAwait(false))
        {
            BrowserPoolSlot? slot = null;
            try
            {
                slot = await browserPool.AcquireSlotAsync(cancellationToken).ConfigureAwait(false);
                var daemon = await browserDaemonClient.TryInteractAsync(
                    url,
                    actionsJson,
                    deadlineMs,
                    BrowserExtractTimeouts.ResolveDaemonWaitTimeoutMs(
                        BrowserExtractTimeouts.ResolvePerExtractTimeoutMs(provisionExpected: false)),
                    headersFile,
                    storageStateFile,
                    cancellationToken,
                    slot.Port).ConfigureAwait(false);
                if (daemon is not null)
                {
                    browserPool.ReleaseSlot(slot, daemon.Ok, daemon.LatencyMs);
                    slot = null;
                    return daemon;
                }

                browserPool.StopSlot(slot);
            }
            finally
            {
                if (slot is not null)
                {
                    browserPool.ReleaseSlot(slot, ok: false, extractMs: 0);
                }
            }
        }

        var temp = Path.Combine(Path.GetTempPath(), $"occam-actions-{Guid.NewGuid():N}.json");
        try
        {
            await File.WriteAllTextAsync(temp, actionsJson, cancellationToken).ConfigureAwait(false);
            return await RunOneShotInteractAsync(url, temp, deadlineMs, headersFile, storageStateFile, cancellationToken)
                .ConfigureAwait(false);
        }
        finally
        {
            try { File.Delete(temp); } catch { /* best effort */ }
        }
    }

    private async Task<BrowserInteractWorkerResult> RunOneShotInteractAsync(
        string url,
        string actionsFile,
        int deadlineMs,
        string? headersFile,
        string? storageStateFile,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(workerPaths.BrowserExtractScript)
            || !File.Exists(workerPaths.BrowserExtractScript))
        {
            return new BrowserInteractWorkerResult(
                false, null, null, "workers_unavailable", "browser extract script missing",
                null, 0, 0, null);
        }

        var argList = new List<string>
        {
            workerPaths.BrowserExtractScript!,
            url,
            $"--mcp-actions-file={actionsFile}",
            $"--action-deadline-ms={deadlineMs}",
        };
        if (!string.IsNullOrWhiteSpace(headersFile))
        {
            argList.Add($"--headers-file={headersFile}");
        }

        if (!string.IsNullOrWhiteSpace(storageStateFile))
        {
            argList.Add($"--storage-state-file={storageStateFile}");
        }

        var timeoutMs = BrowserExtractTimeouts.ResolvePerExtractTimeoutMs(provisionExpected: false);
        var psi = new ProcessStartInfo
        {
            FileName = NodeRuntime.ResolveExecutable(),
            Arguments = NodeLaunchArguments.Build(browser: true, argList.ToArray()),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        PlaywrightEnvironment.ApplyTo(psi);

        using var process = WorkerProcessGroup.Start(psi);
        if (process is null)
        {
            return new BrowserInteractWorkerResult(
                false, null, null, "spawn_failed", "failed to spawn browser worker", null, 0, 0, null);
        }

        var capture = await NodeWorkerOutputCapture.RunAsync(process, timeoutMs, cancellationToken).ConfigureAwait(false);
        if (capture.TimedOut)
        {
            return new BrowserInteractWorkerResult(
                false, null, null, "timeout", "browser interact timed out", null, timeoutMs, 0, null);
        }

        var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(capture.StdOut);
        if (jsonLine is null)
        {
            return new BrowserInteractWorkerResult(
                false, null, null, "extraction_failed", "worker returned no JSON", null, 0, 0, null);
        }

        return BrowserInteractWorkerResult.FromJson(jsonLine);
    }

    private static string Fail(string? url, string code, string message) =>
        JsonSerializer.Serialize(
            new OccamBrowserInteractFailureResponse(
                false, url ?? "", null, code, message, null, null, 0, null),
            OccamBrowserInteractJsonContext.Default.OccamBrowserInteractFailureResponse);
}

public sealed record OccamBrowserInteractSuccessResponse(
    bool Ok,
    string Url,
    string FinalUrl,
    string Markdown,
    string Backend,
    string ActionPlanHash,
    int StepsRun,
    JsonElement? ActionTrace,
    string ContentHash,
    bool Truncated,
    string? TruncationStrategy,
    OccamTranscodeReceiptInfo? Receipt,
    bool Cached);

public sealed record OccamBrowserInteractFailureResponse(
    bool Ok,
    string Url,
    string? FinalUrl,
    string FailureCode,
    string Message,
    string? ActionPlanHash,
    int? FailedIndex,
    int StepsRun,
    JsonElement? ActionTrace);

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(OccamBrowserInteractSuccessResponse))]
[JsonSerializable(typeof(OccamBrowserInteractFailureResponse))]
internal partial class OccamBrowserInteractJsonContext : JsonSerializerContext;
