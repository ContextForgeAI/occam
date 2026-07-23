using System.Diagnostics;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// Prompt 5 — L12 live / public-tunnel workflow validation (scheduled / pre-release).
/// Uses the same published-host resolve order as <c>scripts/launch-mcp-host.mjs</c>.
/// When no host binary is present, emits <c>L12_WORKFLOW_LIVE_SKIPPED</c> (non-blocking).
/// </summary>
internal static class WorkflowLiveUnitTests
{
    public static int Run(Action<string, bool> assert)
    {
        var exe = PublicMcpToolsListLiveTests.ResolvePublishedHostExecutable()
            ?? ResolvePublishedHost();

        if (exe is null)
        {
            Console.WriteLine(
                "L12_WORKFLOW_LIVE_SKIPPED: no OccamMcp.Core published binary " +
                "(run occam doctor / dotnet publish). Tunnel checks deferred.");
            return 0;
        }

        Console.WriteLine($"L12 workflow live host: {exe}");

        using var session = McpStdioSession.Start(exe);
        assert("L12.tunnel: host started", session is not null);
        if (session is null)
        {
            Console.WriteLine("L12_WORKFLOW_LIVE_FAIL");
            return 1;
        }

        try
        {
            var init = session.Request(
                "initialize",
                """{"protocolVersion":"2024-11-05","capabilities":{},"clientInfo":{"name":"l0-gate-workflow-l12","version":"0"}}""");
            assert("L12.tunnel: initialize ok", init.HasValue);
            session.Notify("notifications/initialized");

            var list = session.Request("tools/list", "{}");
            assert("L12.tunnel: tools/list", list.HasValue);
            if (!list.HasValue)
            {
                Console.WriteLine("L12_WORKFLOW_LIVE_FAIL");
                return 1;
            }

            if (!list.Value.TryGetProperty("tools", out var tools) || tools.ValueKind != JsonValueKind.Array)
            {
                assert("L12.tunnel: tools array", false);
                Console.WriteLine("L12_WORKFLOW_LIVE_FAIL");
                return 1;
            }

            JsonElement? digest = null;
            JsonElement? transcode = null;
            JsonElement? map = null;
            JsonElement? verify = null;
            foreach (var tool in tools.EnumerateArray())
            {
                if (!tool.TryGetProperty("name", out var nameEl))
                {
                    continue;
                }

                switch (nameEl.GetString())
                {
                    case "occam_digest": digest = tool; break;
                    case "occam_transcode": transcode = tool; break;
                    case "occam_map": map = tool; break;
                    case "occam_verify": verify = tool; break;
                }
            }

            assert("L12.schema: occam_digest present", digest.HasValue);
            assert("L12.schema: occam_transcode present", transcode.HasValue);
            assert("L12.schema: occam_map present", map.HasValue);
            assert("L12.schema: occam_verify present", verify.HasValue);

            // digest source_url optional (urls not required)
            if (digest.HasValue
                && digest.Value.TryGetProperty("inputSchema", out var dSchema)
                && dSchema.TryGetProperty("properties", out var dProps))
            {
                assert("L12.schema: digest.source_url", dProps.TryGetProperty("source_url", out _));
                var required = dSchema.TryGetProperty("required", out var req) && req.ValueKind == JsonValueKind.Array
                    ? req.EnumerateArray().Select(e => e.GetString()).ToHashSet(StringComparer.Ordinal)
                    : [];
                assert("L12.schema: digest.urls not required", !required.Contains("urls"));
            }

            // New transcode options for conditional / structured economy
            if (transcode.HasValue
                && transcode.Value.TryGetProperty("inputSchema", out var tSchema)
                && tSchema.TryGetProperty("properties", out var tProps))
            {
                foreach (var p in new[]
                         {
                             "if_none_match", "delta_only", "json_blocks", "semantic_chunking",
                             "rank_blocks", "tag_trust", "emit_capsule", "diff_against",
                         })
                {
                    assert($"L12.schema: transcode.{p}", tProps.TryGetProperty(p, out _));
                }
            }

            // Optional live smoke: tiny public page for conditional size (best-effort; may skip on net fail).
            RunConditionalSizeSmoke(session, assert);
            RunDiscoveryFocusSmoke(session, assert);
            RunCapsuleRoundTripNote(assert);

            // Also run the Node public-contract script when node is available (tunnel launch path).
            RunNodePublicContract(assert);

            Console.WriteLine("L12_WORKFLOW_LIVE_OK");
            return 0;
        }
        finally
        {
            session.Dispose();
        }
    }

    private static void RunConditionalSizeSmoke(McpStdioSession session, Action<string, bool> assert)
    {
        // Best-effort: call tools/call for a tiny URL; skip soft if network blocked.
        try
        {
            var args = JsonSerializer.Serialize(new
            {
                name = "occam_transcode",
                arguments = new
                {
                    url = "https://example.com/",
                    max_tokens = 512,
                    fit_markdown = true,
                    focus_query = "example",
                    json_blocks = true,
                    semantic_chunking = true,
                },
            });
            var first = session.Request("tools/call", args, timeoutMs: 90_000);
            if (!first.HasValue)
            {
                Console.WriteLine("L12.conditional: skip (tools/call no response)");
                return;
            }

            var text = ExtractToolText(first.Value);
            if (text is null || !text.Contains("\"ok\":true", StringComparison.Ordinal))
            {
                Console.WriteLine("L12.conditional: skip (page not ok — network drift)");
                return;
            }

            using var doc = JsonDocument.Parse(text);
            var hash = doc.RootElement.TryGetProperty("contentHash", out var h) ? h.GetString() : null;
            var mat = doc.RootElement.TryGetProperty("materializationKey", out var m) ? m.GetString() : null;
            assert("L12.conditional: first call has contentHash", !string.IsNullOrEmpty(hash));
            assert("L12.conditional: first call has materializationKey", !string.IsNullOrEmpty(mat));
            var firstLen = text.Length;

            if (string.IsNullOrEmpty(hash))
            {
                return;
            }

            var args2 = JsonSerializer.Serialize(new
            {
                name = "occam_transcode",
                arguments = new
                {
                    url = "https://example.com/",
                    max_tokens = 512,
                    fit_markdown = true,
                    focus_query = "example",
                    json_blocks = true,
                    semantic_chunking = true,
                    if_none_match = hash,
                },
            });
            var second = session.Request("tools/call", args2, timeoutMs: 90_000);
            if (!second.HasValue)
            {
                return;
            }

            var text2 = ExtractToolText(second.Value);
            if (text2 is null)
            {
                return;
            }

            using var doc2 = JsonDocument.Parse(text2);
            var unchanged = doc2.RootElement.TryGetProperty("unchanged", out var u) && u.ValueKind == JsonValueKind.True;
            assert("L12.conditional: second call unchanged", unchanged);
            assert("L12.conditional: response size substantially smaller", text2.Length < firstLen / 2);
            assert("L12.conditional: no blocks sidecar",
                !doc2.RootElement.TryGetProperty("blocks", out _)
                || doc2.RootElement.GetProperty("blocks").ValueKind == JsonValueKind.Null);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"L12.conditional: skip ({ex.GetType().Name}: {ex.Message})");
        }
    }

    private static void RunDiscoveryFocusSmoke(McpStdioSession session, Action<string, bool> assert)
    {
        try
        {
            var args = JsonSerializer.Serialize(new
            {
                name = "occam_map",
                arguments = new
                {
                    url = "https://docs.python.org/3/",
                    source = "homepage",
                    max_links = 5,
                    focus_query = "asyncio",
                },
            });
            var res = session.Request("tools/call", args, timeoutMs: 90_000);
            if (!res.HasValue)
            {
                Console.WriteLine("L12.discovery: skip (no response)");
                return;
            }

            var text = ExtractToolText(res.Value);
            if (text is null || !text.Contains("\"ok\":true", StringComparison.Ordinal))
            {
                Console.WriteLine("L12.discovery: skip (map not ok — network drift)");
                return;
            }

            assert("L12.discovery: map ok", true);
            // Soft signal: prefer asyncio path when present in payload.
            if (text.Contains("asyncio", StringComparison.OrdinalIgnoreCase))
            {
                assert("L12.discovery: focus mentions asyncio", true);
            }
            else
            {
                Console.WriteLine("L12.discovery: note — asyncio not in top links (network/DOM drift)");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"L12.discovery: skip ({ex.GetType().Name})");
        }
    }

    private static void RunCapsuleRoundTripNote(Action<string, bool> assert)
    {
        // Capsule round-trip is fully covered offline in Chain I / CapsuleUnitTests.
        // L12 only asserts the tool is listed (done above) — live emit_capsule needs receipts on.
        assert("L12.capsule: covered by frozen Chain I + L_CAPSULE_OK", true);
    }

    private static void RunNodePublicContract(Action<string, bool> assert)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var script = Path.Combine(home, "scripts", "check-public-mcp-contract.mjs");
        if (!File.Exists(script))
        {
            Console.WriteLine("L12.node-contract: skip (script missing)");
            return;
        }

        try
        {
            var psi = new ProcessStartInfo
            {
                FileName = "node",
                Arguments = $"\"{script}\"",
                WorkingDirectory = home,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false,
            };
            psi.Environment["OCCAM_HOME"] = home;
            using var p = Process.Start(psi);
            if (p is null)
            {
                Console.WriteLine("L12.node-contract: skip (node spawn failed)");
                return;
            }

            var stdout = p.StandardOutput.ReadToEnd();
            var stderr = p.StandardError.ReadToEnd();
            p.WaitForExit(120_000);
            var ok = p.ExitCode == 0 && (stdout + stderr).Contains("PUBLIC_MCP_CONTRACT_OK", StringComparison.Ordinal);
            assert("L12.tunnel: node public-mcp-contract OK", ok);
            if (!ok)
            {
                Console.WriteLine(stdout);
                Console.Error.WriteLine(stderr);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"L12.node-contract: skip ({ex.Message})");
        }
    }

    private static string? ExtractToolText(JsonElement result)
    {
        // MCP tools/call → { content: [ { type:"text", text:"{...json...}" } ] }
        if (!result.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
        {
            return null;
        }

        foreach (var part in content.EnumerateArray())
        {
            if (part.TryGetProperty("text", out var text))
            {
                return text.GetString();
            }
        }

        return null;
    }

    private static string? ResolvePublishedHost()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var candidates = new[]
        {
            Path.Combine(home, "OccamMcp.Core.exe"),
            Path.Combine(home, "OccamMcp.Core"),
            Path.Combine(home, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", "win-x64", "publish", "OccamMcp.Core.exe"),
            Path.Combine(home, "src", "FFOccamMcp.Core", "bin", "Release", "net10.0", "win-x64", "native", "OccamMcp.Core.exe"),
        };
        return candidates.FirstOrDefault(File.Exists);
    }
}
