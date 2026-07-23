using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Transport;

namespace OccamMcp.L0Gate;

/// <summary>
/// Public MCP contract regression: C# tool signatures (required / defaults) must match the
/// documented contract for always-on tools — especially digest urls/source_url and
/// transcode if_none_match / diff_against.
/// </summary>
internal static class PublicMcpContractUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var methods = DiscoverToolMethods();
        assert("contract: discovered tool methods", methods.Count >= OccamMcpServerRegistration.OccamToolNames.Length);

        foreach (var name in OccamMcpServerRegistration.OccamToolNames)
        {
            assert($"contract: method registered for {name}", methods.ContainsKey(name));
        }

        AssertDigestContract(assert, methods["occam_digest"]);
        AssertTranscodeContract(assert, methods["occam_transcode"]);
        AssertHealContract(assert, methods["occam_playbook_heal"]);
        AssertDigestResponseShape(assert);
        AssertIfNoneMatchTokens(assert);
        AssertDiffAgainstParser(assert);
        AssertDigestInputContract(assert);

        Console.WriteLine("L_PUBLIC_MCP_CONTRACT_OK");
    }

    private static Dictionary<string, MethodInfo> DiscoverToolMethods()
    {
        var map = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        var asm = typeof(OccamDigestTool).Assembly;
        foreach (var type in asm.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null)
            {
                continue;
            }

            foreach (var method in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.DeclaredOnly))
            {
                var tool = method.GetCustomAttribute<McpServerToolAttribute>();
                if (tool?.Name is { Length: > 0 } name)
                {
                    map[name] = method;
                }
            }
        }

        return map;
    }

    private static void AssertDigestContract(Action<string, bool> assert, MethodInfo method)
    {
        var ps = method.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();
        var required = ps.Where(IsRequiredMcpParam).Select(p => p.Name!).ToArray();
        assert("contract digest: MCP required=[] (urls optional)", required.Length == 0);

        var byName = ps.ToDictionary(p => p.Name!, StringComparer.Ordinal);
        assert("contract digest: has urls", byName.ContainsKey("urls"));
        assert("contract digest: has source_url", byName.ContainsKey("source_url"));
        assert("contract digest: has if_none_match", byName.ContainsKey("if_none_match"));
        assert("contract digest: has max_links", byName.ContainsKey("max_links"));

        assert("contract digest: urls is JsonElement?", byName["urls"].ParameterType == typeof(JsonElement?));
        assert("contract digest: urls default null", byName["urls"].HasDefaultValue && byName["urls"].DefaultValue is null);
        assert("contract digest: source_url default null", byName["source_url"].HasDefaultValue && byName["source_url"].DefaultValue is null);
        assert(
            "contract digest: max_links default 8",
            byName["max_links"].ParameterType == typeof(int)
            && byName["max_links"].HasDefaultValue
            && Equals(byName["max_links"].DefaultValue, 8));
        assert(
            "contract digest: fit_markdown default true",
            byName["fit_markdown"].HasDefaultValue && Equals(byName["fit_markdown"].DefaultValue, true));
        assert(
            "contract digest: backend_policy default http_then_browser",
            Equals(byName["backend_policy"].DefaultValue, "http_then_browser"));

        var desc = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
        assert(
            "contract digest: tool description mentions source_url",
            desc.Contains("source_url", StringComparison.Ordinal));
        assert(
            "contract digest: prefer one digest over N×transcode (Agent DX)",
            desc.Contains("Prefer ONE digest", StringComparison.OrdinalIgnoreCase)
                || desc.Contains("over N separate", StringComparison.OrdinalIgnoreCase));
    }

    private static void AssertTranscodeContract(Action<string, bool> assert, MethodInfo method)
    {
        var ps = method.GetParameters()
            .Where(p => p.ParameterType != typeof(CancellationToken))
            .ToArray();
        var required = ps.Where(IsRequiredMcpParam).Select(p => p.Name!).ToArray();
        assert("contract transcode: only url required", required.Length == 1 && required[0] == "url");

        var byName = ps.ToDictionary(p => p.Name!, StringComparer.Ordinal);
        foreach (var name in new[]
                 {
                     "if_none_match", "diff_against", "delta_only", "json_blocks", "cache_ttl_s",
                     "emit_capsule", "rank_blocks", "tag_trust", "prefer_llms_txt",
                 })
        {
            assert($"contract transcode: has {name}", byName.ContainsKey(name));
        }

        assert(
            "contract transcode: fit_markdown default false",
            byName["fit_markdown"].HasDefaultValue && Equals(byName["fit_markdown"].DefaultValue, false));
        assert(
            "contract transcode: delta_only default false",
            byName["delta_only"].HasDefaultValue && Equals(byName["delta_only"].DefaultValue, false));

        var cacheDesc = byName["cache_ttl_s"].GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
        assert(
            "contract transcode: cache_ttl_s description mentions diff_against",
            cacheDesc.Contains("diff_against", StringComparison.Ordinal));
        assert(
            "contract transcode: cache_ttl_s description mentions if_none_match",
            cacheDesc.Contains("if_none_match", StringComparison.Ordinal));
        assert("contract transcode: auto_recover absent", !byName.ContainsKey("auto_recover"));

        var toolDesc = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
        assert(
            "contract transcode: default page reader framing (Agent DX)",
            toolDesc.Contains("default page reader", StringComparison.OrdinalIgnoreCase)
                && toolDesc.Contains("prefer it over any generic", StringComparison.OrdinalIgnoreCase));
        assert(
            "contract transcode: never renamed to occam_read",
            method.GetCustomAttribute<McpServerToolAttribute>()!.Name == "occam_transcode");
    }

    private static void AssertHealContract(Action<string, bool> assert, MethodInfo method)
    {
        var p = method.GetParameters().First(x => x.Name == "max_skeleton_nodes");
        assert(
            "contract heal: max_skeleton_nodes default 600",
            p.ParameterType == typeof(int) && p.HasDefaultValue && Equals(p.DefaultValue, 600));
    }

    private static void AssertDigestResponseShape(Action<string, bool> assert)
    {
        var json = JsonSerializer.Serialize(
            new OccamDigestSuccessResponse(
                true,
                "dg_test",
                [],
                "combined body",
                new OccamDigestStatsInfo(0, 0, 0, 0),
                null,
                "2026-07-20T00:00:00Z",
                SourceUrl: "https://example.com/",
                DiscoveredLinks: [new OccamDigestDiscoveredLinkInfo("https://example.com/a")],
                Unchanged: false),
            OccamDigestJsonContext.Default.OccamDigestSuccessResponse);

        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        assert("contract digest response: items (not pages)", root.TryGetProperty("items", out _));
        assert("contract digest response: no pages[]", !root.TryGetProperty("pages", out _));
        assert("contract digest response: combined (not combinedMarkdown)", root.TryGetProperty("combined", out _));
        assert("contract digest response: no combinedMarkdown", !root.TryGetProperty("combinedMarkdown", out _));
        assert("contract digest response: stats", root.TryGetProperty("stats", out _));
        assert("contract digest response: sourceUrl", root.TryGetProperty("sourceUrl", out _));
        assert("contract digest response: discoveredLinks[].url",
            root.TryGetProperty("discoveredLinks", out var links)
            && links.GetArrayLength() == 1
            && links[0].TryGetProperty("url", out _));
        assert("contract digest response: unchanged", root.TryGetProperty("unchanged", out _));
    }

    private static void AssertIfNoneMatchTokens(Action<string, bool> assert)
    {
        const string md = "# Hello\n\nworld";
        var bare = ContentHashToken.BareHex(md);
        assert("contract if_none_match: bare hex matches", ContentHashToken.Matches(md, bare));
        assert(
            "contract if_none_match: sha256: prefix matches",
            ContentHashToken.Matches(md, "sha256:" + bare));
        assert(
            "contract if_none_match: SHA256: prefix case-insensitive",
            ContentHashToken.Matches(md, "SHA256:" + bare.ToUpperInvariant()));
        assert(
            "contract if_none_match: wrong token fails",
            !ContentHashToken.Matches(md, new string('a', 64)));
    }

    private static void AssertDiffAgainstParser(Action<string, bool> assert)
    {
        assert(
            "contract diff_against: JSON array",
            OccamTranscodeTool.TryParseHashList("[\"aaa\",\"bbb\"]", out var hashes)
            && hashes is { Count: 2 }
            && hashes[0] == "aaa");
        assert(
            "contract diff_against: comma-separated",
            OccamTranscodeTool.TryParseHashList("aaa, bbb ,ccc", out var csv)
            && csv is { Count: 3 });
        assert(
            "contract diff_against: rejects non-array JSON",
            !OccamTranscodeTool.TryParseHashList("{\"a\":1}", out _));
        assert(
            "contract diff_against: rejects non-string array elems",
            !OccamTranscodeTool.TryParseHashList("[1,2]", out _));
        assert(
            "contract diff_against: rejects empty",
            !OccamTranscodeTool.TryParseHashList("   ", out _)
            && !OccamTranscodeTool.TryParseHashList("[]", out _));
    }

    private static void AssertDigestInputContract(Action<string, bool> assert)
    {
        assert(
            "contract digest input: neither → invalid_arguments",
            !DigestInputContract.TryValidate((JsonElement?)null, null, out _, out var code, out var msg)
            && code == "invalid_arguments"
            && msg == DigestInputContract.NeitherMessage);
        assert(
            "contract digest input: source_url only → useSourceUrl",
            DigestInputContract.TryValidate((JsonElement?)null, "https://example.com/", out var useSrc, out _, out _)
            && useSrc);
        assert(
            "contract digest input: both → source wins",
            DigestInputContract.TryValidate(
                JsonSerializer.SerializeToElement("[\"https://a.example/\"]"), "https://b.example/", out var both, out _, out _)
            && both);
        assert(
            "contract digest input: urls only → no source",
            DigestInputContract.TryValidate(JsonSerializer.SerializeToElement("[\"https://a.example/\"]"), null, out var urlsOnly, out _, out _)
            && !urlsOnly);
    }

    /// <summary>
    /// MCP JSON Schema marks a parameter required when C# has no default and the type is
    /// non-nullable (ModelContextProtocol convention used by this host).
    /// </summary>
    private static bool IsRequiredMcpParam(ParameterInfo p)
    {
        if (p.HasDefaultValue)
        {
            return false;
        }

        var t = p.ParameterType;
        if (!t.IsValueType && t != typeof(string))
        {
            // reference types without default are required unless annotated nullable —
            // ParameterInfo.HasDefaultValue already false for required string url.
            return true;
        }

        if (t == typeof(string))
        {
            // string without default → required; string? has default null in our tools.
            return !p.HasDefaultValue;
        }

        var underlying = Nullable.GetUnderlyingType(t);
        return underlying is null && !p.HasDefaultValue;
    }
}
