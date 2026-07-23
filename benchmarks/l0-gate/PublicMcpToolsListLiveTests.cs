using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// Live public MCP contract: spawn the <b>published</b> host binary (same resolve order as
/// <c>scripts/launch-mcp-host.mjs</c> / ChatGPT tunnel) and assert <c>tools/list</c> JSON Schema —
/// not C# reflection. Catches stale AOT binaries that still advertise an older surface while
/// receipts claim <c>1.0.0-rc.2</c>.
/// </summary>
internal static class PublicMcpToolsListLiveTests
{
    private static readonly string[] TranscodeRc1Params =
    [
        "rank_blocks", "tag_trust", "delta_only", "emit_capsule",
        "json_blocks", "semantic_chunking", "diff_against", "if_none_match", "cache_ttl_s",
    ];

    public static void Run(Action<string, bool> assert)
    {
        var exe = ResolvePublishedHostExecutable();
        assert("live tools/list: published host binary present", exe is not null);
        if (exe is null)
        {
            Console.Error.WriteLine(
                "live tools/list SKIPPED: no OccamMcp.Core at OCCAM_HOME root or publish path. " +
                "Run occam doctor / dotnet publish so tunnel parity can be checked.");
            return;
        }

        Console.WriteLine($"live tools/list host: {exe}");

        using var session = McpStdioSession.Start(exe);
        assert("live tools/list: host started", session is not null);
        if (session is null)
        {
            return;
        }

        try
        {
            var init = session.Request(
                "initialize",
                """{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"l0-gate-public-contract","version":"0"}}""");
            assert("live tools/list: initialize ok", init.HasValue);

            session.Notify("notifications/initialized");

            var list = session.Request("tools/list", "{}");
            assert("live tools/list: response", list.HasValue);
            if (!list.HasValue)
            {
                return;
            }

            if (!list.Value.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)
            {
                assert("live tools/list: tools array", false);
                return;
            }

            JsonElement? digest = null;
            JsonElement? transcode = null;
            foreach (var tool in tools.EnumerateArray())
            {
                if (!tool.TryGetProperty("name", out var nameEl))
                {
                    continue;
                }

                var name = nameEl.GetString();
                if (name == "occam_digest")
                {
                    digest = tool;
                }
                else if (name == "occam_transcode")
                {
                    transcode = tool;
                }
            }

            assert("live tools/list: occam_digest present", digest.HasValue);
            assert("live tools/list: occam_transcode present", transcode.HasValue);
            if (!digest.HasValue || !transcode.HasValue)
            {
                return;
            }

            AssertDigestSchema(assert, digest.Value);
            AssertTranscodeSchema(assert, transcode.Value);

            var fingerprint = PublicMcpSchemaFingerprint.Compute(tools);
            var expectedPath = ResolveFingerprintPath();
            assert("live tools/list: fingerprint corpus present", File.Exists(expectedPath));
            if (File.Exists(expectedPath))
            {
                var expected = File.ReadAllText(expectedPath).Trim();
                assert(
                    $"live tools/list: schemaFingerprint matches corpus ({fingerprint[..12]}…)",
                    string.Equals(expected, fingerprint, StringComparison.Ordinal));
                if (!string.Equals(expected, fingerprint, StringComparison.Ordinal))
                {
                    Console.Error.WriteLine($"expected fingerprint: {expected}");
                    Console.Error.WriteLine($"actual fingerprint:   {fingerprint}");
                    Console.Error.WriteLine(
                        "Update corpora/public-mcp-schema-fingerprint.txt after an intentional schema change " +
                        "(node scripts/check-public-mcp-contract.mjs --write-fingerprint).");
                }
            }

            Console.WriteLine($"L_PUBLIC_MCP_TOOLS_LIST_OK fingerprint={fingerprint}");
        }
        finally
        {
            session.Dispose();
        }
    }

    private static void AssertDigestSchema(Action<string, bool> assert, JsonElement tool)
    {
        if (!tool.TryGetProperty("inputSchema", out var schema))
        {
            assert("live digest: inputSchema", false);
            return;
        }

        var required = ReadRequired(schema);
        assert("live digest: urls not in required[]", !required.Contains("urls", StringComparer.Ordinal));
        assert("live digest: required[] empty or omits urls", required.Count == 0 || !required.Contains("urls"));

        assert("live digest: source_url property", HasProperty(schema, "source_url"));
        assert("live digest: urls property", HasProperty(schema, "urls"));
        assert("live digest: max_links property", HasProperty(schema, "max_links"));

        if (TryGetPropertySchema(schema, "max_links", out var maxLinks))
        {
            assert(
                "live digest: max_links default is 8",
                maxLinks.TryGetProperty("default", out var def)
                && def.ValueKind == JsonValueKind.Number
                && def.GetInt32() == 8);
        }
    }

    private static void AssertTranscodeSchema(Action<string, bool> assert, JsonElement tool)
    {
        if (!tool.TryGetProperty("inputSchema", out var schema))
        {
            assert("live transcode: inputSchema", false);
            return;
        }

        var required = ReadRequired(schema);
        assert("live transcode: only url required", required.Count == 1 && required[0] == "url");
        assert("live transcode: auto_recover absent", !HasProperty(schema, "auto_recover"));

        foreach (var name in TranscodeRc1Params)
        {
            assert($"live transcode: has {name}", HasProperty(schema, name));
        }

        foreach (var boolParam in new[] { "rank_blocks", "tag_trust", "delta_only", "emit_capsule", "json_blocks", "semantic_chunking" })
        {
            if (!TryGetPropertySchema(schema, boolParam, out var prop))
            {
                continue;
            }

            assert(
                $"live transcode: {boolParam} default false",
                prop.TryGetProperty("default", out var def)
                && def.ValueKind == JsonValueKind.False);
        }
    }

    private static List<string> ReadRequired(JsonElement schema)
    {
        var list = new List<string>();
        if (!schema.TryGetProperty("required", out var req) || req.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        foreach (var item in req.EnumerateArray())
        {
            if (item.ValueKind == JsonValueKind.String && item.GetString() is { } s)
            {
                list.Add(s);
            }
        }

        return list;
    }

    private static bool HasProperty(JsonElement schema, string name) =>
        schema.TryGetProperty("properties", out var props)
        && props.ValueKind == JsonValueKind.Object
        && props.TryGetProperty(name, out _);

    private static bool TryGetPropertySchema(JsonElement schema, string name, out JsonElement prop)
    {
        prop = default;
        return schema.TryGetProperty("properties", out var props)
            && props.ValueKind == JsonValueKind.Object
            && props.TryGetProperty(name, out prop);
    }

    /// <summary>Same candidate order as <c>scripts/lib/resolve-host-binary.mjs</c> (tunnel launch path).</summary>
    internal static string? ResolvePublishedHostExecutable()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var rid = OperatingSystem.IsWindows() ? "win-x64"
            : OperatingSystem.IsMacOS() ? (System.Runtime.InteropServices.RuntimeInformation.ProcessArchitecture
                == System.Runtime.InteropServices.Architecture.Arm64 ? "osx-arm64" : "osx-x64")
            : "linux-x64";
        var exe = OperatingSystem.IsWindows() ? "OccamMcp.Core.exe" : "OccamMcp.Core";
        var legacy = OperatingSystem.IsWindows() ? "FFOccamMcp.Core.exe" : "FFOccamMcp.Core";

        foreach (var candidate in new[]
                 {
                     Path.Combine(home, exe),
                     Path.Combine(home, legacy),
                     Path.Combine(home, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", rid, "publish", exe),
                     Path.Combine(home, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", rid, "publish", legacy),
                     Path.Combine(home, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", "publish", exe),
                 })
        {
            if (File.Exists(candidate))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string ResolveFingerprintPath()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        return Path.Combine(home, "corpora", "public-mcp-schema-fingerprint.txt");
    }
}

/// <summary>Stable SHA-256 over canonicalized tools/list (names + required + property types/defaults; no descriptions).</summary>
internal static class PublicMcpSchemaFingerprint
{
    public static string Compute(JsonElement toolsArray)
    {
        var tools = new List<CanonicalTool>();
        foreach (var tool in toolsArray.EnumerateArray())
        {
            var name = tool.GetProperty("name").GetString() ?? "";
            var schema = tool.TryGetProperty("inputSchema", out var s) ? s : default;
            var required = new List<string>();
            if (schema.ValueKind == JsonValueKind.Object
                && schema.TryGetProperty("required", out var req)
                && req.ValueKind == JsonValueKind.Array)
            {
                foreach (var r in req.EnumerateArray())
                {
                    if (r.GetString() is { } rs)
                    {
                        required.Add(rs);
                    }
                }
            }

            required.Sort(StringComparer.Ordinal);
            var props = new SortedDictionary<string, CanonicalProp>(StringComparer.Ordinal);
            if (schema.ValueKind == JsonValueKind.Object
                && schema.TryGetProperty("properties", out var properties)
                && properties.ValueKind == JsonValueKind.Object)
            {
                foreach (var p in properties.EnumerateObject())
                {
                    props[p.Name] = new CanonicalProp(
                        TypeToken(p.Value),
                        DefaultToken(p.Value));
                }
            }

            tools.Add(new CanonicalTool(name, required, props));
        }

        tools.Sort((a, b) => string.CompareOrdinal(a.Name, b.Name));
        var json = JsonSerializer.Serialize(
            tools,
            new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            });
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(json));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static string TypeToken(JsonElement prop)
    {
        if (!prop.TryGetProperty("type", out var t))
        {
            return "any";
        }

        return t.ValueKind switch
        {
            JsonValueKind.String => t.GetString() ?? "any",
            JsonValueKind.Array => string.Join("|", t.EnumerateArray().Select(x => x.GetString() ?? "?")),
            _ => t.GetRawText(),
        };
    }

    private static string? DefaultToken(JsonElement prop)
    {
        if (!prop.TryGetProperty("default", out var d))
        {
            return null;
        }

        return d.ValueKind switch
        {
            JsonValueKind.Null => "null",
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Number => d.GetRawText(),
            JsonValueKind.String => d.GetString(),
            _ => d.GetRawText(),
        };
    }

    private sealed record CanonicalTool(
        string Name,
        List<string> Required,
        SortedDictionary<string, CanonicalProp> Properties);

    private sealed record CanonicalProp(string Type, string? Default);
}

/// <summary>Minimal newline-delimited JSON-RPC client for a stdio MCP host subprocess.</summary>
internal sealed class McpStdioSession : IDisposable
{
    private readonly Process _process;
    private readonly StreamWriter _stdin;
    private readonly StreamReader _stdout;
    private int _nextId = 1;
    private readonly StringBuilder _buffer = new();

    private McpStdioSession(Process process, StreamWriter stdin, StreamReader stdout)
    {
        _process = process;
        _stdin = stdin;
        _stdout = stdout;
    }

    public static McpStdioSession? Start(string exe)
    {
        try
        {
            var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
            var psi = new ProcessStartInfo
            {
                FileName = exe,
                WorkingDirectory = home,
                UseShellExecute = false,
                RedirectStandardInput = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                CreateNoWindow = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardInputEncoding = Encoding.UTF8,
            };
            psi.Environment["OCCAM_HOME"] = home;
            psi.Environment["OCCAM_BANNER"] = "0";

            var process = Process.Start(psi);
            if (process is null)
            {
                return null;
            }

            // Drain stderr so the pipe never blocks the host.
            process.ErrorDataReceived += (_, _) => { };
            process.BeginErrorReadLine();

            var stdin = process.StandardInput;
            stdin.AutoFlush = true;
            return new McpStdioSession(process, stdin, process.StandardOutput);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"live tools/list host start failed: {ex.Message}");
            return null;
        }
    }

    public void Notify(string method, string paramsJson = "{}")
    {
        _stdin.WriteLine($"{{\"jsonrpc\":\"2.0\",\"method\":\"{method}\",\"params\":{paramsJson}}}");
    }

    public JsonElement? Request(string method, string paramsJson, int timeoutMs = 30_000)
    {
        var id = _nextId++;
        _stdin.WriteLine($"{{\"jsonrpc\":\"2.0\",\"id\":{id},\"method\":\"{method}\",\"params\":{paramsJson}}}");

        var deadline = DateTime.UtcNow.AddMilliseconds(timeoutMs);
        while (DateTime.UtcNow < deadline)
        {
            if (_process.HasExited)
            {
                Console.Error.WriteLine($"live tools/list: host exited {_process.ExitCode} during {method}");
                return null;
            }

            var remaining = (int)(deadline - DateTime.UtcNow).TotalMilliseconds;
            if (remaining <= 0)
            {
                break;
            }

            // Read available lines without indefinite block: peek via ReadLine with short waits.
            if (!_stdout.EndOfStream || _process.StandardOutput.BaseStream.CanRead)
            {
                var lineTask = _stdout.ReadLineAsync();
                if (!lineTask.Wait(Math.Min(remaining, 2000)))
                {
                    continue;
                }

                var line = lineTask.Result;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    using var doc = JsonDocument.Parse(line);
                    var root = doc.RootElement;
                    if (root.TryGetProperty("id", out var idEl)
                        && idEl.ValueKind == JsonValueKind.Number
                        && idEl.GetInt32() == id)
                    {
                        if (root.TryGetProperty("error", out var err))
                        {
                            Console.Error.WriteLine($"live tools/list RPC error: {err}");
                            return null;
                        }

                        if (root.TryGetProperty("result", out var result))
                        {
                            return result.Clone();
                        }
                    }
                }
                catch (JsonException)
                {
                    // ignore non-JSON stderr bleed
                }
            }
        }

        Console.Error.WriteLine($"live tools/list: timeout waiting for {method}");
        return null;
    }

    public void Dispose()
    {
        try
        {
            if (!_process.HasExited)
            {
                _stdin.Close();
                if (!_process.WaitForExit(2000))
                {
                    _process.Kill(entireProcessTree: true);
                }
            }
        }
        catch
        {
            // best effort
        }

        _process.Dispose();
    }
}
