using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Workers;

namespace OccamMcp.Rc2Regression;

internal static class McpBoundaryCharacterization
{
    public static async Task RunAsync(TestHarness test)
    {
        var exe = ResolvePublishedHostExecutable();
        test.Check("D12", "published host is available for the real MCP boundary", exe is not null,
            exe ?? "missing; run dotnet publish src/FFOccamMcp.Core -c Release -r win-x64");
        if (exe is null)
        {
            return;
        }

        await using var session = await McpSession.StartAsync(exe);
        var initialized = await session.InitializeAsync();
        test.Check("D12", "stdio MCP initialize succeeds", initialized, $"host={exe}");
        if (!initialized)
        {
            return;
        }

        var schema = await session.RequestAsync("tools/list", "{}");
        var urlsType = FindDigestUrlsType(schema);
        // RC.2 re-baseline (intentional D12 honesty): tools/list publishes a truthful oneOf so
        // native arrays bind. Do not revert to string-only schema.
        test.Check("D12", "RC.2 runtime schema publishes urls as truthful oneOf",
            urlsType is "oneOf" or "array|string" or "string|array",
            $"schemaType={urlsType ?? "missing"}; schemaAcceptance=native-array:true");

        foreach (var input in new[]
                 {
                     // Valid-shaped native array reaches the handler (typed JSON; extract may fail).
                     (Name: "native array", Args: "{\"urls\":[\"https://example.invalid/one\"],\"backend_policy\":\"http\"}", ExpectTypedJson: true, ExpectTypedInvalid: false),
                     (Name: "empty array", Args: "{\"urls\":[],\"backend_policy\":\"http\"}", ExpectTypedJson: true, ExpectTypedInvalid: true),
                     (Name: "mixed array", Args: "{\"urls\":[\"https://example.invalid/one\",7],\"backend_policy\":\"http\"}", ExpectTypedJson: true, ExpectTypedInvalid: true),
                     (Name: "single malformed string", Args: "{\"urls\":\"not-a-url\",\"backend_policy\":\"http\"}", ExpectTypedJson: true, ExpectTypedInvalid: true),
                     (Name: "JSON-encoded string array", Args: "{\"urls\":\"[\\\"not-a-url\\\"]\",\"backend_policy\":\"http\"}", ExpectTypedJson: true, ExpectTypedInvalid: true),
                     (Name: "delimiter string", Args: "{\"urls\":\"not-a-url,also-bad\",\"backend_policy\":\"http\"}", ExpectTypedJson: true, ExpectTypedInvalid: true),
                 })
        {
            var response = await session.RequestAsync("tools/call", $"{{\"name\":\"occam_digest\",\"arguments\":{input.Args}}}");
            var text = ExtractToolText(response);
            var opaque = string.IsNullOrWhiteSpace(text) || !text.TrimStart().StartsWith('{');
            var typedJson = !opaque;
            var typedInvalid = typedJson
                && text!.Contains("\"ok\":false", StringComparison.Ordinal)
                && (text.Contains("invalid_urls", StringComparison.Ordinal)
                    || text.Contains("invalid_arguments", StringComparison.Ordinal));
            var pass = input.ExpectTypedInvalid
                ? typedInvalid
                : (input.ExpectTypedJson && typedJson);
            test.Check("D12", $"{input.Name} captures current boundary disposition",
                pass,
                $"binding={(opaque ? "failed-before-handler" : "reached-handler")}; opaque={opaque}; typedInvalid={typedInvalid}");
        }
    }

    public static async Task RunDesiredContractAsync(TestHarness test)
    {
        var exe = ResolvePublishedHostExecutable();
        if (exe is null)
        {
            test.Check("D12", "native arrays must reach a typed boundary", false,
                "published host missing", intentionallyRed: true);
            return;
        }

        await using var session = await McpSession.StartAsync(exe);
        if (!await session.InitializeAsync())
        {
            test.Check("D12", "native arrays must reach a typed boundary", false,
                "initialize failed", intentionallyRed: true);
            return;
        }

        var schema = await session.RequestAsync("tools/list", "{}");
        var type = FindDigestUrlsType(schema);
        test.Check("D12", "tools/list must truthfully accept native arrays and legacy strings",
            type is "array|string" or "string|array" or "oneOf",
            $"schemaType={type ?? "missing"}", intentionallyRed: true);

        foreach (var input in new[]
                 {
                     (Name: "empty native array", Args: "{\"urls\":[]}"),
                     (Name: "mixed native array", Args: "{\"urls\":[\"https://example.invalid/one\",7]}"),
                     (Name: "nested native array", Args: "{\"urls\":[[\"https://example.invalid/one\"]]}"),
                     (Name: "malformed legacy string", Args: "{\"urls\":\"not-a-url\"}"),
                 })
        {
            var response = await session.RequestAsync(
                "tools/call",
                $"{{\"name\":\"occam_digest\",\"arguments\":{input.Args}}}");
            var text = ExtractToolText(response);
            test.Check("D12", $"{input.Name} must return typed invalid_arguments",
                text?.TrimStart().StartsWith('{') == true
                && text.Contains("\"ok\":false", StringComparison.Ordinal)
                && text.Contains("invalid_arguments", StringComparison.Ordinal),
                $"toolText={(text is null ? "missing" : text[..Math.Min(100, text.Length)])}", intentionallyRed: true);
        }

        await RunBindingGuardCasesAsync(test, session);
    }

    /// <summary>
    /// RC.2 overnight Issue 3: missing/wrong-type required args must become typed
    /// <c>invalid_arguments</c> without opaque invoke errors or host crash.
    /// </summary>
    private static async Task RunBindingGuardCasesAsync(TestHarness test, McpSession session)
    {
        foreach (var input in new[]
                 {
                     (Name: "claim_check missing url",
                         Call: "{\"name\":\"occam_claim_check\",\"arguments\":{\"claim\":\"x\"}}",
                         Needle: "url"),
                     (Name: "attest missing claims",
                         Call: "{\"name\":\"occam_attest\",\"arguments\":{\"backend_policy\":\"http\"}}",
                         Needle: "claims"),
                     (Name: "dataset_export missing urls",
                         Call: "{\"name\":\"occam_dataset_export\",\"arguments\":{\"backend_policy\":\"http\"}}",
                         Needle: "urls"),
                     (Name: "claim_check wrong-type url",
                         Call: "{\"name\":\"occam_claim_check\",\"arguments\":{\"claim\":\"x\",\"url\":123}}",
                         Needle: "could not be converted"),
                 })
        {
            var response = await session.RequestAsync("tools/call", input.Call);
            var text = ExtractToolText(response);
            var isErrorTrue = response is { } root
                && root.TryGetProperty("result", out var result)
                && result.TryGetProperty("isError", out var err)
                && err.ValueKind == JsonValueKind.True;
            var isErrorAbsentOrFalse = !isErrorTrue;
            var typed = text?.TrimStart().StartsWith('{') == true
                && text.Contains("\"ok\":false", StringComparison.Ordinal)
                && text.Contains("invalid_arguments", StringComparison.Ordinal)
                && text.Contains(input.Needle, StringComparison.OrdinalIgnoreCase);
            test.Check(
                "D12",
                $"{input.Name} returns typed invalid_arguments (not opaque invoke error)",
                typed && isErrorAbsentOrFalse,
                $"isErrorTrue={isErrorTrue}; toolText={(text is null ? "missing" : text[..Math.Min(160, text.Length)])}",
                intentionallyRed: true);
            test.Check(
                "D12",
                $"{input.Name} keeps MCP isError unset/false with ok=false envelope",
                typed && isErrorAbsentOrFalse,
                $"isErrorTrue={isErrorTrue}; hasOkFalse={text?.Contains("\"ok\":false", StringComparison.Ordinal) == true}",
                intentionallyRed: true);
        }

        // Host continuity: after bad calls, tools/list and a valid probe still succeed.
        var listed = await session.RequestAsync("tools/list", "{}");
        var toolCount = 0;
        if (listed is { } listRoot
            && listRoot.TryGetProperty("result", out var listResult)
            && listResult.TryGetProperty("tools", out var tools)
            && tools.ValueKind == JsonValueKind.Array)
        {
            toolCount = tools.GetArrayLength();
        }

        test.Check(
            "D12",
            "host remains alive for tools/list after binding failures",
            toolCount >= 10,
            $"toolCount={toolCount}",
            intentionallyRed: true);

        var probe = await session.RequestAsync(
            "tools/call",
            "{\"name\":\"occam_probe\",\"arguments\":{\"url\":\"https://example.com/\"}}");
        var probeText = ExtractToolText(probe);
        test.Check(
            "D12",
            "valid tool call succeeds after binding failures",
            probeText?.TrimStart().StartsWith('{') == true
            && (probeText.Contains("\"ok\":", StringComparison.Ordinal)
                || probeText.Contains("classification", StringComparison.OrdinalIgnoreCase)
                || probeText.Contains("failureCode", StringComparison.Ordinal)),
            $"toolText={(probeText is null ? "missing" : probeText[..Math.Min(120, probeText.Length)])}",
            intentionallyRed: true);
    }

    private static string? FindDigestUrlsType(JsonElement? response)
    {
        if (response is not { } root
            || !root.TryGetProperty("result", out var result)
            || !result.TryGetProperty("tools", out var tools))
        {
            return null;
        }

        foreach (var tool in tools.EnumerateArray())
        {
            if (tool.GetProperty("name").GetString() != "occam_digest")
            {
                continue;
            }

            var urls = tool.GetProperty("inputSchema").GetProperty("properties").GetProperty("urls");
            if (urls.TryGetProperty("oneOf", out _)) return "oneOf";
            if (!urls.TryGetProperty("type", out var type)) return "missing";
            if (type.ValueKind == JsonValueKind.Array)
            {
                return string.Join('|', type.EnumerateArray().Select(item => item.GetString()));
            }
            return type.GetString();
        }
        return null;
    }

    private static string? ExtractToolText(JsonElement? response)
    {
        if (response is not { } root || !root.TryGetProperty("result", out var result)) return null;
        if (!result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array) return null;
        foreach (var item in content.EnumerateArray())
        {
            if (item.TryGetProperty("text", out var text)) return text.GetString();
        }
        return null;
    }

    private static string? ResolvePublishedHostExecutable()
    {
        var explicitHost = Environment.GetEnvironmentVariable("OCCAM_RC2_HOST");
        if (!string.IsNullOrWhiteSpace(explicitHost) && File.Exists(explicitHost))
        {
            return Path.GetFullPath(explicitHost);
        }

        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var rid = OperatingSystem.IsWindows() ? "win-x64"
            : OperatingSystem.IsMacOS() ? "osx-arm64"
            : "linux-x64";
        var exe = OperatingSystem.IsWindows() ? "OccamMcp.Core.exe" : "OccamMcp.Core";
        foreach (var candidate in new[]
                 {
                     Path.Combine(home, exe),
                     Path.Combine(home, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", rid, "publish", exe),
                     Path.Combine(home, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", "publish", exe),
                 })
        {
            if (File.Exists(candidate)) return candidate;
        }
        return null;
    }

    private sealed class McpSession : IAsyncDisposable
    {
        private readonly Process _process;
        private readonly StreamWriter _input;
        private readonly StreamReader _output;
        private int _id;

        private McpSession(Process process)
        {
            _process = process;
            _input = process.StandardInput;
            _output = process.StandardOutput;
            _input.AutoFlush = true;
        }

        public static Task<McpSession> StartAsync(string executable)
        {
            var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
            var start = new ProcessStartInfo
            {
                FileName = executable,
                WorkingDirectory = home,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardInputEncoding = Encoding.UTF8,
                StandardOutputEncoding = Encoding.UTF8,
            };
            start.Environment["OCCAM_HOME"] = home;
            start.Environment["OCCAM_BANNER"] = "0";
            var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start MCP host.");
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();
            return Task.FromResult(new McpSession(process));
        }

        public async Task<bool> InitializeAsync()
        {
            var response = await RequestAsync(
                "initialize",
                "{\"protocolVersion\":\"2024-11-05\",\"capabilities\":{},\"clientInfo\":{\"name\":\"rc2-pr-a\",\"version\":\"0\"}}");
            if (response is null) return false;
            await _input.WriteLineAsync("{\"jsonrpc\":\"2.0\",\"method\":\"notifications/initialized\",\"params\":{}}");
            return true;
        }

        public async Task<JsonElement?> RequestAsync(string method, string paramsJson)
        {
            var id = Interlocked.Increment(ref _id);
            await _input.WriteLineAsync($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",\"params\":{paramsJson}}}");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(60));
            while (!timeout.IsCancellationRequested)
            {
                string? line;
                try
                {
                    line = await _output.ReadLineAsync(timeout.Token);
                }
                catch (OperationCanceledException)
                {
                    return null;
                }
                if (string.IsNullOrWhiteSpace(line)) continue;
                using var document = JsonDocument.Parse(line);
                var root = document.RootElement;
                if (root.TryGetProperty("id", out var responseId) && responseId.GetInt32() == id)
                {
                    return root.Clone();
                }
            }
            return null;
        }

        public async ValueTask DisposeAsync()
        {
            try { _input.Close(); } catch { }
            if (!_process.HasExited)
            {
                using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(2));
                try { await _process.WaitForExitAsync(timeout.Token); } catch { _process.Kill(entireProcessTree: true); }
            }
            _process.Dispose();
        }
    }
}
