using System.Diagnostics;
using System.Text.Json;
using OccamMcp.Core.Abstractions;
using OccamMcp.Core.Configuration;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Services;
using OccamMcp.Core.Session;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Workers;

/// <summary>Runs HTTP extract operations (daemon + one-shot).</summary>
public sealed class HttpExtractRunner : IHttpExtractRunner
{
    private readonly WorkerPaths _workerPaths;
    private readonly IProxyRotationService _proxyRotation;

    public HttpExtractRunner(WorkerPaths workerPaths, IProxyRotationService proxyRotation)
    {
        _workerPaths = workerPaths;
        _proxyRotation = proxyRotation;
    }

    private bool SkipDaemonForRotation => _proxyRotation.IsConfigured;

    public async ValueTask<ExtractRunResult> RunAsync(
        string scriptPath,
        ExtractOptions options,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        if (!File.Exists(scriptPath))
        {
            return new ExtractRunResult(false, null, null, "worker_missing", 0, null, false);
        }

        var lifecycle = NodeWorkerLifecycle.For("http");
        var forceRecycle = options.ForceRecycle || lifecycle.ConsumeRecyclePending();
        lifecycle.OnRunStarted();

        // Try daemon first if not skipped
        if (!SkipDaemonForRotation
            && string.IsNullOrWhiteSpace(options.PlaybookOverlayPath)
            && !HttpExtractRoutingScope.PreferOneShot
            && HttpDaemonHost.TryEnsureRunning(_workerPaths))
        {
            var daemonResult = HttpDaemonClient.TryExtract(
                options.Url,
                timeoutMs,
                forceRecycle,
                options.HeadersFile,
                options.Features,
                cancellationToken);

            if (daemonResult is not null)
            {
                CompleteLifecycle(lifecycle, daemonResult);
                return daemonResult;
            }
        }

        return await RunOneShotAsync(scriptPath, options, timeoutMs, lifecycle, cancellationToken).ConfigureAwait(false);
    }

    private async ValueTask<ExtractRunResult> RunOneShotAsync(
        string scriptPath,
        ExtractOptions options,
        int timeoutMs,
        NodeWorkerLifecycle lifecycle,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var args = BuildArguments(scriptPath, options);

        var psi = new ProcessStartInfo
        {
            FileName = NodeRuntime.ResolveExecutable(),
            Arguments = NodeLaunchArguments.Build(browser: false, args),
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
        };

        EgressProxyConfig.ApplyForSpawn(psi, _proxyRotation);

        if (!string.IsNullOrWhiteSpace(options.OversizeMode))
        {
            psi.Environment["OCCAM_HTTP_OVERSIZE_MODE"] = options.OversizeMode;
        }

        if (!string.IsNullOrWhiteSpace(options.Features))
        {
            psi.Environment["OCCAM_FEATURES"] = options.Features;
        }

        using var process = WorkerProcessGroup.Start(psi);
        if (process is null)
        {
            lifecycle.OnRunCompleted(timedOut: false, crashed: true);
            return new ExtractRunResult(false, null, null, "spawn_failed", 0, null, false);
        }

        try
        {
            // Need timeout from somewhere - for now use default
            var capture = await NodeWorkerOutputCapture.RunAsync(process, timeoutMs, cancellationToken).ConfigureAwait(false);
            if (capture.TimedOut)
            {
                lifecycle.OnRunCompleted(timedOut: true, crashed: false);
                return new ExtractRunResult(false, null, null, "timeout", timeoutMs, null, true);
            }

            var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(capture.StdOut);
            if (jsonLine is null)
            {
                lifecycle.OnRunCompleted(timedOut: false, crashed: true);
                return new ExtractRunResult(
                    false,
                    null,
                    null,
                    SummarizeNoJsonFailure(capture.StdErr, capture.ExitCode),
                    0,
                    null,
                    false,
                    0,
                    null);
            }

            var payload = JsonSerializer.Deserialize(jsonLine, WorkerExtractJsonContext.Default.WorkerExtractResponse);
            if (payload is null)
            {
                lifecycle.OnRunCompleted(timedOut: false, crashed: true);
                return new ExtractRunResult(false, null, null, "bad_json", 0, null, false);
            }

            if (!payload.Ok)
            {
                lifecycle.OnRunCompleted(
                    timedOut: IsTimeoutFailure(payload.Failure),
                    crashed: !IsTimeoutFailure(payload.Failure));
            }
            else
            {
                lifecycle.OnRunCompleted(timedOut: false, crashed: false);
            }

            // Single mapping for daemon + one-shot so results never drift (one-shot previously zeroed
            // networkMs/parseMs).
            return WorkerExtractPayloadMapper.Map(payload);
        }
        catch
        {
            lifecycle.OnRunCompleted(timedOut: false, crashed: true);
            return new ExtractRunResult(false, null, null, "spawn_failed", 0, null, false);
        }
        finally
        {
            WorkerProcessGroup.Release(process);
        }
    }

    private static void CompleteLifecycle(NodeWorkerLifecycle lifecycle, ExtractRunResult result)
    {
        if (result.TimedOut)
        {
            lifecycle.OnRunCompleted(timedOut: true, crashed: false);
            return;
        }

        if (!result.Ok)
        {
            lifecycle.OnRunCompleted(
                timedOut: IsTimeoutFailure(result.Failure),
                crashed: !IsTimeoutFailure(result.Failure));
            return;
        }

        lifecycle.OnRunCompleted(timedOut: false, crashed: false);
    }

    private static bool IsTimeoutFailure(string? failure) =>
        failure is "timeout" or "aborterror"
        || (failure?.Contains("timeout", StringComparison.OrdinalIgnoreCase) ?? false);

    private static string SummarizeNoJsonFailure(string stderr, int exitCode)
    {
        if (string.IsNullOrWhiteSpace(stderr))
        {
            return exitCode == 0 ? "no_json" : $"no_json:exit_{exitCode}";
        }

        var tail = stderr.Trim();
        if (tail.Length > 240)
        {
            tail = tail[^240..];
        }

        tail = tail.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return string.IsNullOrWhiteSpace(tail) ? "no_json" : $"no_json:{tail}";
    }

    private string BuildArguments(string scriptPath, ExtractOptions options)
    {
        var args = $"\"{scriptPath}\" \"{options.Url}\"";
        
        if (!string.IsNullOrWhiteSpace(options.HeadersFile) && File.Exists(options.HeadersFile))
        {
            args += $" --headers-file=\"{options.HeadersFile}\"";
        }

        if (!string.IsNullOrWhiteSpace(options.PlaybookOverlayPath) && File.Exists(options.PlaybookOverlayPath))
        {
            var overlayFlag = options.PlaybookOverlayStrict ? "--playbook-overlay" : "--playbook-overlay-soft";
            args += $" {overlayFlag}=\"{options.PlaybookOverlayPath}\"";
        }

        return args;
    }
}