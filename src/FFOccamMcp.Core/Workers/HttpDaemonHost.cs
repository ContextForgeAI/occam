using System.Diagnostics;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Workers;

/// <summary>Long-lived Node HTTP extract worker — amortizes process startup across requests.</summary>
public static class HttpDaemonHost
{
    private static readonly object Gate = new();
    private static readonly TimeSpan IdlePollInterval = TimeSpan.FromSeconds(15);
    private static Process? _daemonProcess;
    private static Timer? _idleTimer;
    private static long _lastActivityUtcTicks = DateTime.UtcNow.Ticks;
    private static bool _shutdownHooked;

    public static bool IsEnabled =>
        !string.Equals(Environment.GetEnvironmentVariable("OCCAM_HTTP_DAEMON"), "0", StringComparison.Ordinal);

    public static int Port =>
        int.TryParse(Environment.GetEnvironmentVariable("OCCAM_HTTP_DAEMON_PORT"), out var port) && port > 0
            ? port
            : 39_218;

    public static int IdleTtlMs =>
        int.TryParse(Environment.GetEnvironmentVariable("OCCAM_HTTP_DAEMON_IDLE_TTL_MS"), out var ttlMs) && ttlMs >= 0
            ? ttlMs
            : 120_000;

    public static bool TryEnsureRunning(WorkerPaths paths)
    {
        if (!IsEnabled)
        {
            return false;
        }

        var script = ResolveDaemonScript(paths);
        if (script is null)
        {
            return false;
        }

        EnsureShutdownHook();
        EnsureIdleMonitor();
        lock (Gate)
        {
            if (HttpDaemonClient.IsHealthy(Port))
            {
                MarkActivityUnsafe();
                return true;
            }

            if (_daemonProcess is { HasExited: false })
            {
                var healthy = WaitForHealthy(TimeSpan.FromSeconds(8));
                if (healthy)
                {
                    MarkActivityUnsafe();
                }

                return healthy;
            }

            var psi = new ProcessStartInfo
            {
                FileName = NodeRuntime.ResolveExecutable(),
                Arguments = NodeLaunchArguments.Build(browser: false, $"\"{script}\"", $"--port={Port}"),
                // stdout is captured + drained (must not corrupt the host's MCP stdout); stderr is inherited so
                // the daemon's diagnostics flow to the host's stderr instead of a never-read pipe that deadlocks.
                RedirectStandardOutput = true,
                RedirectStandardError = false,
                UseShellExecute = false,
                CreateNoWindow = true,
            };

            EgressProxyConfig.ApplyTo(psi);

            _daemonProcess = WorkerProcessGroup.Start(psi);
            WorkerProcessGroup.DrainStandardOutput(_daemonProcess);
            var started = _daemonProcess is not null && WaitForHealthy(TimeSpan.FromSeconds(12));
            if (started)
            {
                MarkActivityUnsafe();
            }

            return started;
        }
    }

    public static void MarkActivity()
    {
        lock (Gate)
        {
            MarkActivityUnsafe();
        }
    }

    public static void Stop()
    {
        lock (Gate)
        {
            NodeWorkerLifecycle.TerminateAndDispose(_daemonProcess);
            _daemonProcess = null;
        }
    }

    private static void EnsureShutdownHook()
    {
        if (_shutdownHooked)
        {
            return;
        }

        _shutdownHooked = true;
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Stop();
    }

    private static void EnsureIdleMonitor()
    {
        if (_idleTimer is not null)
        {
            return;
        }

        lock (Gate)
        {
            _idleTimer ??= new Timer(_ => OnIdleTimerTick(), null, IdlePollInterval, IdlePollInterval);
        }
    }

    private static void OnIdleTimerTick()
    {
        lock (Gate)
        {
            if (_daemonProcess is not { HasExited: false })
            {
                return;
            }

            var ttlMs = IdleTtlMs;
            if (ttlMs <= 0)
            {
                return;
            }

            var idleMs = (DateTime.UtcNow.Ticks - _lastActivityUtcTicks) / TimeSpan.TicksPerMillisecond;
            if (idleMs < ttlMs)
            {
                return;
            }

            NodeWorkerLifecycle.TerminateAndDispose(_daemonProcess);
            _daemonProcess = null;
        }
    }

    private static void MarkActivityUnsafe()
    {
        _lastActivityUtcTicks = DateTime.UtcNow.Ticks;
    }

    private static bool WaitForHealthy(TimeSpan timeout)
    {
        var deadline = Environment.TickCount64 + (long)timeout.TotalMilliseconds;
        while (Environment.TickCount64 < deadline)
        {
            if (HttpDaemonClient.IsHealthy(Port))
            {
                return true;
            }

            Thread.Sleep(200);
        }

        return HttpDaemonClient.IsHealthy(Port);
    }

    private static string? ResolveDaemonScript(WorkerPaths paths)
    {
        var overridePath = Environment.GetEnvironmentVariable("OCCAM_HTTP_DAEMON_SCRIPT");
        if (!string.IsNullOrWhiteSpace(overridePath) && File.Exists(overridePath))
        {
            return overridePath;
        }

        var root = WorkerPaths.ResolveOccamHome();
        if (root is null)
        {
            return null;
        }

        var candidate = Path.Combine(root, "workers", "http-extract", "http-daemon.mjs");
        return File.Exists(candidate) ? candidate : null;
    }
}

internal static class HttpDaemonClient
{
    // P1-6: Configurable timeout instead of InfiniteTimeSpan to prevent hangs
    private static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(3);
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = DefaultTimeout,
    };

    public static bool IsHealthy(int port)
    {
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(2));
            var response = SharedClient.GetAsync($"http://127.0.0.1:{port}/health", cts.Token)
                .GetAwaiter()
                .GetResult();
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }

    public static ExtractRunResult? TryExtract(
        string url,
        int timeoutMs,
        bool forceRecycle,
        string? headersFile,
        string? features,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var port = HttpDaemonHost.Port;
        if (!IsHealthy(port))
        {
            return null;
        }
        try
        {
            if (forceRecycle)
            {
                TryRecycle(port, cancellationToken);
            }

            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(timeoutMs);

            var request = new HttpDaemonExtractRequest
            {
                Url = url,
                ForceRecycle = forceRecycle,
                HeadersFile = headersFile,
                Features = features,
            };

            var json = JsonSerializer.Serialize(request, HttpDaemonJsonContext.Default.HttpDaemonExtractRequest);
            using var content = new StringContent(json, Encoding.UTF8, "application/json");
            using var response = SharedClient.PostAsync($"http://127.0.0.1:{port}/extract", content, cts.Token)
                .GetAwaiter()
                .GetResult();

            using var stream = response.Content.ReadAsStream(cts.Token);
            var payload = JsonSerializer.Deserialize(stream, WorkerExtractJsonContext.Default.WorkerExtractResponse);
            if (payload is null)
            {
                return null;
            }

            HttpDaemonHost.MarkActivity();
            return WorkerExtractPayloadMapper.Map(payload);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return new ExtractRunResult(false, null, null, "timeout", timeoutMs, null, true);
        }
        catch
        {
            return null;
        }
    }

    private static void TryRecycle(int port, CancellationToken cancellationToken)
    {
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));
            using var content = new StringContent("{}", Encoding.UTF8, "application/json");
            SharedClient.PostAsync($"http://127.0.0.1:{port}/recycle", content, cts.Token)
                .GetAwaiter()
                .GetResult();
        }
        catch
        {
            // best effort
        }
    }
}

internal sealed class HttpDaemonExtractRequest
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("force_recycle")]
    public bool ForceRecycle { get; init; }

    [JsonPropertyName("headers_file")]
    public string? HeadersFile { get; init; }

    [JsonPropertyName("features")]
    public string? Features { get; init; }
}

[JsonSerializable(typeof(HttpDaemonExtractRequest))]
internal partial class HttpDaemonJsonContext : JsonSerializerContext;
