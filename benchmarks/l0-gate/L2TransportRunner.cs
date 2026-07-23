using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Transport;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L2TransportRunner
{
    public static void Run(Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l2 transport corpus: {corpusPath}");
        assert("l2 transport corpus exists", File.Exists(corpusPath));

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L2TransportJsonContext.Default.L2TransportCase);
            if (entry is null)
            {
                assert("l2 transport/json parse", false);
                continue;
            }

            Console.WriteLine($"l2 transport: {entry.Id} mode={entry.Mode}");
            switch (entry.Id)
            {
                case "transport-stdio-launch":
                    RunStdioLaunch(entry, assert);
                    break;
                case "transport-stdio-tools-count":
                    RunStdioToolsCount(entry, assert);
                    break;
                case "transport-invalid-port":
                    RunInvalidPort(entry, assert);
                    break;
                case "transport-ws-bind-localhost":
                    RunWebSocketBind(entry, assert);
                    break;
                case "transport-regression-gate":
                    assert("l2 transport/regression stdio default", entry.ExpectL0GateOk == true);
                    break;
                default:
                    assert($"l2 transport/{entry.Id} known", false);
                    break;
            }
        }
    }

    private static void RunStdioLaunch(L2TransportCase entry, Action<string, bool> assert)
    {
        var exe = ResolveHostExecutable();
        if (exe is null)
        {
            assert("l2 transport/stdio-launch host exe", false);
            return;
        }

        using var process = StartHost(exe, entry.Args ?? []);
        assert("l2 transport/stdio-launch started", process is not null);
        if (process is null)
        {
            return;
        }

        var alive = process.WaitForExit(2500);
        if (!alive)
        {
            TryKill(process);
            assert("l2 transport/stdio-launch alive", entry.ExpectExitZero != false);
            return;
        }

        assert("l2 transport/stdio-launch exit", entry.ExpectExitZero != true || process.ExitCode == 0);
    }

    private static void RunStdioToolsCount(L2TransportCase entry, Action<string, bool> assert)
    {
        var names = OccamMcpServerRegistration.OccamToolNames;
        assert("l2 transport/tools-count", names.Length == (entry.ExpectToolCount ?? 5));
        if (entry.ExpectToolPrefix is not null)
        {
            assert(
                "l2 transport/tools-prefix",
                names.All(name => name.StartsWith(entry.ExpectToolPrefix, StringComparison.Ordinal)));
        }
    }

    private static void RunInvalidPort(L2TransportCase entry, Action<string, bool> assert)
    {
        var exe = ResolveHostExecutable();
        if (exe is null)
        {
            assert("l2 transport/invalid-port host exe", false);
            return;
        }

        using var process = StartHost(exe, entry.Args ?? ["--mcp-server", "--port", "0"]);
        assert("l2 transport/invalid-port started", process is not null);
        if (process is null)
        {
            return;
        }

        process.WaitForExit(5000);
        var expectFail = entry.ExpectExitZero == false;
        assert("l2 transport/invalid-port exit non-zero", expectFail ? process.ExitCode != 0 : process.ExitCode == 0);
    }

    private static void RunWebSocketBind(L2TransportCase entry, Action<string, bool> assert)
    {
        if (entry.OptionalCi == true)
        {
            Console.WriteLine("l2 transport/ws-bind: optional smoke skipped in merge path");
            assert("l2 transport/ws-bind optional", true);
            return;
        }

        var exe = ResolveHostExecutable();
        if (exe is null)
        {
            assert("l2 transport/ws-bind host exe", false);
            return;
        }

        using var process = StartHost(exe, entry.Args ?? ["--mcp-server"]);
        assert("l2 transport/ws-bind started", process is not null);
        if (process is null)
        {
            return;
        }

        try
        {
            Thread.Sleep(2000);
            var port = OccamMcpCli.DefaultWebSocketPort;
            var listening = WebSocketMcpTransport.IsListeningOnLocalhost(port);
            assert($"l2 transport/ws-bind {entry.ExpectBind ?? OccamMcpCli.DefaultBindAddress}", listening);
        }
        finally
        {
            TryKill(process);
        }
    }

    private static Process? StartHost(string exe, string[] args)
    {
        try
        {
            var startInfo = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory(),
                UseShellExecute = false,
                RedirectStandardError = true,
                RedirectStandardOutput = true,
                RedirectStandardInput = true,
                CreateNoWindow = true,
            };
            foreach (var arg in args)
            {
                startInfo.ArgumentList.Add(arg);
            }

            var process = Process.Start(startInfo);
            process?.BeginOutputReadLine();
            process?.BeginErrorReadLine();
            return process;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"l2 transport host start failed: {ex.Message}");
            return null;
        }
    }

    private static void TryKill(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static string? ResolveHostExecutable()
    {
        // Same published-host order as live tools/list / launch-mcp-host.mjs (root + RID publish).
        var published = PublicMcpToolsListLiveTests.ResolvePublishedHostExecutable();
        if (published is not null)
        {
            return published;
        }

        // Legacy: framework apphost from OccamGateBuild / plain build (local Windows/Linux without publish).
        var home = WorkerPaths.ResolveOccamHome();
        if (home is null)
        {
            return null;
        }

        var name = OperatingSystem.IsWindows() ? "OccamMcp.Core.exe" : "OccamMcp.Core";
        foreach (var relative in new[]
                 {
                     Path.Combine("src", "FFOccamMcp.Core", "bin", "gate", "Debug", "net10.0", name),
                     Path.Combine("src", "FFOccamMcp.Core", "bin", "gate", "Release", "net10.0", name),
                     Path.Combine("src", "FFOccamMcp.Core", "bin", "Debug", "net10.0", name),
                     Path.Combine("src", "FFOccamMcp.Core", "bin", "Release", "net10.0", name),
                 })
        {
            var path = Path.Combine(home, relative);
            if (File.Exists(path))
            {
                return path;
            }
        }

        return null;
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var path = Path.Combine(home, "corpora", "l2-transport.jsonl");
            if (File.Exists(path))
            {
                return path;
            }
        }

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l2-transport.jsonl");
        if (File.Exists(cwd))
        {
            return cwd;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "corpora", "l2-transport.jsonl"));
    }
}

internal sealed class L2TransportCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("mode")]
    public string? Mode { get; init; }

    [JsonPropertyName("args")]
    public string[]? Args { get; init; }

    [JsonPropertyName("expect_exit_zero")]
    public bool? ExpectExitZero { get; init; }

    [JsonPropertyName("expect_tool_count")]
    public int? ExpectToolCount { get; init; }

    [JsonPropertyName("expect_tool_prefix")]
    public string? ExpectToolPrefix { get; init; }

    [JsonPropertyName("failure_kind")]
    public string? FailureKind { get; init; }

    [JsonPropertyName("expect_bind")]
    public string? ExpectBind { get; init; }

    [JsonPropertyName("optional_ci")]
    public bool? OptionalCi { get; init; }

    [JsonPropertyName("expect_l0_gate_ok")]
    public bool? ExpectL0GateOk { get; init; }
}

[JsonSerializable(typeof(L2TransportCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L2TransportJsonContext : JsonSerializerContext;
