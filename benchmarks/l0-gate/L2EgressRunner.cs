using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L2EgressUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        assert("egress valid http proxy", EgressProxyConfig.IsValidProxyUrl("http://127.0.0.1:8080"));
        assert("egress valid https proxy", EgressProxyConfig.IsValidProxyUrl("https://proxy.corp:8443"));
        assert("egress valid socks5 proxy", EgressProxyConfig.IsValidProxyUrl("socks5://127.0.0.1:1080"));
        assert("egress invalid proxy url", !EgressProxyConfig.IsValidProxyUrl("not-a-url"));
        assert("egress invalid proxy scheme", !EgressProxyConfig.IsValidProxyUrl("ftp://proxy.corp"));

        var redacted = EgressProxyConfig.RedactCredentials("http://user:secret@proxy.corp:8080/path");
        assert("egress redact credentials", redacted.Contains("***", StringComparison.Ordinal));
        assert("egress redact keeps host", redacted.Contains("proxy.corp", StringComparison.Ordinal));

        var settings = new EgressProxySettings("http://127.0.0.1:8080", null, "localhost,127.0.0.1");
        assert("egress settings configured", settings.IsConfigured);

        var psi = new ProcessStartInfo
        {
            FileName = "node",
            UseShellExecute = false,
        };
        settings.ApplyTo(psi);
        assert("egress apply http proxy", psi.Environment[EgressProxyConfig.HttpProxyVar] == "http://127.0.0.1:8080");
        assert("egress apply no_proxy", psi.Environment[EgressProxyConfig.NoProxyVar] == "localhost,127.0.0.1");
    }
}

internal static class L2EgressRunner
{
    public static void Run(TranscodePipeline pipeline, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l2 egress corpus: {corpusPath}");
        assert("l2 egress corpus exists", File.Exists(corpusPath));

        var runnerEnv = SaveProxyEnv();
        RestoreProxyEnv(new ProxyEnvSnapshot(null, null, null));

        try
        {
            foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L2EgressJsonContext.Default.L2EgressCase);
            if (entry is null)
            {
                assert("l2 egress/json parse", false);
                continue;
            }

            Console.WriteLine($"l2 egress: {entry.Id} mode={entry.Mode}");
            switch (entry.Mode?.Trim().ToLowerInvariant())
            {
                case "baseline":
                    assert("l2 egress/no-proxy configured", !EgressProxyConfig.ReadFromEnvironment().IsConfigured);
                    break;
                case "worker":
                    RunWorkerCase(entry, assert);
                    break;
                case "live":
                    RunLiveCase(pipeline, entry, assert).GetAwaiter().GetResult();
                    break;
                default:
                    assert($"l2 egress/{entry.Id} mode", false);
                    break;
            }
        }
        }
        finally
        {
            RestoreProxyEnv(runnerEnv);
        }
    }

    private static void RunWorkerCase(L2EgressCase entry, Action<string, bool> assert) =>
        RunWorkerCaseAsync(entry, assert).GetAwaiter().GetResult();

    private static async Task RunWorkerCaseAsync(L2EgressCase entry, Action<string, bool> assert)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var script = Path.Combine(home, "workers", "http-extract", "extract.mjs");
        assert($"l2 egress/{entry.Id} script", File.Exists(script));

        var saved = SaveProxyEnv();
        Process? proxyProcess = null;
        try
        {
            if (entry.UseMockProxy == true)
            {
                var mock = await StartMockProxyProcess(entry.MockProxyReject == true);
                proxyProcess = mock.Process;
                assert($"l2 egress/{entry.Id} mock proxy", mock.ProxyUrl is not null);
                if (mock.ProxyUrl is null)
                {
                    return;
                }

                Environment.SetEnvironmentVariable(EgressProxyConfig.HttpProxyVar, mock.ProxyUrl);
                Environment.SetEnvironmentVariable(EgressProxyConfig.HttpsProxyVar, mock.ProxyUrl);
            }
            else if (!string.IsNullOrWhiteSpace(entry.ProxyUrl))
            {
                Environment.SetEnvironmentVariable(EgressProxyConfig.HttpProxyVar, entry.ProxyUrl);
                Environment.SetEnvironmentVariable(EgressProxyConfig.HttpsProxyVar, entry.ProxyUrl);
            }

            var psi = new ProcessStartInfo
            {
                FileName = NodeRuntime.ResolveExecutable(),
                Arguments = NodeLaunchArguments.Build(browser: false, $"\"{script}\" \"{entry.Url}\""),
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            EgressProxyConfig.ApplyTo(psi);

            using var process = WorkerProcessGroup.Start(psi);
            assert($"l2 egress/{entry.Id} started", process is not null);
            if (process is null)
            {
                return;
            }

            var capture = GateSyncBridge.Run(process, 45_000);
            var jsonLine = NodeWorkerOutputCapture.TryParseLastJsonLine(capture.StdOut);
            assert($"l2 egress/{entry.Id} json", jsonLine is not null);
            if (jsonLine is null)
            {
                return;
            }

            var payload = JsonSerializer.Deserialize(jsonLine, WorkerExtractJsonContext.Default.WorkerExtractResponse);
            assert($"l2 egress/{entry.Id} ok={entry.ExpectOk}", payload?.Ok == entry.ExpectOk);
            if (entry.ExpectOk == false && !string.IsNullOrWhiteSpace(entry.FailureKind) && payload?.Failure is not null)
            {
                assert(
                    $"l2 egress/{entry.Id} failure",
                    payload.Failure.Contains(entry.FailureKind, StringComparison.OrdinalIgnoreCase));
            }
        }
        finally
        {
            if (proxyProcess is { HasExited: false })
            {
                try
                {
                    proxyProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort
                }
            }

            RestoreProxyEnv(saved);
        }
    }

    private static async Task RunLiveCase(TranscodePipeline pipeline, L2EgressCase entry, Action<string, bool> assert)
    {
        if (!OccamBackendPolicyParser.TryParse(entry.BackendPolicy ?? "http", out var policy))
        {
            assert($"l2 egress/{entry.Id} backend", false);
            return;
        }

        var saved = SaveProxyEnv();
        Process? proxyProcess = null;
        string? savedDaemon = null;
        try
        {
            if (entry.UseMockProxy == true)
            {
                var mock = await StartMockProxyProcess();
                proxyProcess = mock.Process;
                assert($"l2 egress/{entry.Id} mock proxy", mock.ProxyUrl is not null);
                if (mock.ProxyUrl is null)
                {
                    return;
                }

                Environment.SetEnvironmentVariable(EgressProxyConfig.HttpProxyVar, mock.ProxyUrl);
                Environment.SetEnvironmentVariable(EgressProxyConfig.HttpsProxyVar, mock.ProxyUrl);
            }

            if (string.Equals(entry.BackendPolicy, "browser", StringComparison.OrdinalIgnoreCase))
            {
                savedDaemon = Environment.GetEnvironmentVariable("OCCAM_BROWSER_DAEMON");
                Environment.SetEnvironmentVariable("OCCAM_BROWSER_DAEMON", "0");
                BrowserDaemonHost.Stop();
            }

            var result = pipeline.Transcode(entry.Url!, policy, new OccamTranscodeOptions(), CancellationToken.None);
            assert($"l2 egress/{entry.Id} ok={entry.ExpectOk}", result.Ok == entry.ExpectOk);
            if (entry.ExpectOk == true && entry.MinMarkdownChars is > 0)
            {
                assert(
                    $"l2 egress/{entry.Id} min_markdown_chars",
                    (result.Markdown?.Length ?? 0) >= entry.MinMarkdownChars);
            }
        }
        finally
        {
            if (proxyProcess is { HasExited: false })
            {
                try
                {
                    proxyProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort
                }
            }

            RestoreProxyEnv(saved);
            if (savedDaemon is null)
            {
                Environment.SetEnvironmentVariable("OCCAM_BROWSER_DAEMON", null);
            }
            else
            {
                Environment.SetEnvironmentVariable("OCCAM_BROWSER_DAEMON", savedDaemon);
            }
        }
    }

    private static async Task<(Process? Process, string? ProxyUrl)> StartMockProxyProcess(bool reject = false)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var fixture = Path.Combine(home, "benchmarks", "l0-gate", "fixtures", "egress-mock-proxy-launch.mjs");
        if (!File.Exists(fixture))
        {
            return (null, null);
        }

        var psi = new ProcessStartInfo
        {
            FileName = NodeRuntime.ResolveExecutable(),
            Arguments = reject ? $"\"{fixture}\" --reject" : $"\"{fixture}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        var process = Process.Start(psi);
        if (process is null)
        {
            return (null, null);
        }

        var deadline = Environment.TickCount64 + 8_000;
        while (Environment.TickCount64 < deadline && !process.HasExited)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    return (process, urlProp.GetString());
                }
            }
            catch
            {
                // wait for JSON line
            }
        }

        return (process, null);
    }

    private sealed record ProxyEnvSnapshot(string? Http, string? Https, string? NoProxy);

    private static ProxyEnvSnapshot SaveProxyEnv() =>
        new(
            Environment.GetEnvironmentVariable(EgressProxyConfig.HttpProxyVar),
            Environment.GetEnvironmentVariable(EgressProxyConfig.HttpsProxyVar),
            Environment.GetEnvironmentVariable(EgressProxyConfig.NoProxyVar));

    private static void RestoreProxyEnv(ProxyEnvSnapshot saved)
    {
        SetOrClear(EgressProxyConfig.HttpProxyVar, saved.Http);
        SetOrClear(EgressProxyConfig.HttpsProxyVar, saved.Https);
        SetOrClear(EgressProxyConfig.NoProxyVar, saved.NoProxy);
    }

    private static void SetOrClear(string name, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            Environment.SetEnvironmentVariable(name, null);
        }
        else
        {
            Environment.SetEnvironmentVariable(name, value);
        }
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var path = Path.Combine(home, "corpora", "l2-egress.jsonl");
            if (File.Exists(path))
            {
                return path;
            }
        }

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l2-egress.jsonl");
        if (File.Exists(cwd))
        {
            return cwd;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "corpora", "l2-egress.jsonl"));
    }
}

internal sealed class L2EgressCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("backend_policy")]
    public string? BackendPolicy { get; init; }

    [JsonPropertyName("proxy_url")]
    public string? ProxyUrl { get; init; }

    [JsonPropertyName("use_mock_proxy")]
    public bool? UseMockProxy { get; init; }

    [JsonPropertyName("mock_proxy_reject")]
    public bool? MockProxyReject { get; init; }

    [JsonPropertyName("expect_ok")]
    public bool? ExpectOk { get; init; }

    [JsonPropertyName("failure_kind")]
    public string? FailureKind { get; init; }

    [JsonPropertyName("min_markdown_chars")]
    public int? MinMarkdownChars { get; init; }

    [JsonPropertyName("note")]
    public string? Note { get; init; }
}

[JsonSerializable(typeof(L2EgressCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L2EgressJsonContext : JsonSerializerContext;
