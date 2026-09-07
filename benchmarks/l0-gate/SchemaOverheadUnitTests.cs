using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Transport;

namespace OccamMcp.L0Gate;

/// <summary>
/// U2: model-visible schema and default-envelope size stay bounded. Descriptions are
/// counted from live <c>[Description]</c> attributes; tools are never removed.
/// </summary>
internal static class SchemaOverheadUnitTests
{
    // Heuristic-unicode-v1 is chars/4; ceilings are character counts so the gate
    // does not depend on tokenizer drift.
    private const int ReaderInstructionsMaxChars = 2_600;
    private const int FullInstructionsMaxChars = 3_200;
    private const int TranscodeSchemaMaxChars = 2_700;
    private const int CoreSchemaMaxChars = 13_000;
    private const int DefaultEnvelopeMaxChars = 700;

    public static void Run(Action<string, bool> assert)
    {
        var reader = OccamServerInstructions.TextFor(OccamToolProfile.Reader);
        var full = OccamServerInstructions.TextFor(OccamToolProfile.Full);
        Console.WriteLine($"schema-overhead: reader instructions {reader.Length} chars (max {ReaderInstructionsMaxChars})");
        Console.WriteLine($"schema-overhead: full instructions {full.Length} chars (max {FullInstructionsMaxChars})");
        assert("schema overhead: reader instructions bounded", reader.Length <= ReaderInstructionsMaxChars);
        assert("schema overhead: full instructions bounded", full.Length <= FullInstructionsMaxChars);
        assert("schema overhead: reader still states trust rule", reader.Contains("ok:false", StringComparison.Ordinal));
        assert("schema overhead: reader still names transcode", reader.Contains("occam_transcode", StringComparison.Ordinal));

        var methods = DiscoverToolMethods();
        foreach (var name in OccamMcpServerRegistration.OccamToolNames)
        {
            assert($"schema overhead: tool kept {name}", methods.ContainsKey(name));
        }

        var transcodeChars = DescriptionChars(methods["occam_transcode"]);
        var coreChars = 0;
        foreach (var name in OccamMcpServerRegistration.OccamToolNames)
        {
            coreChars += DescriptionChars(methods[name]);
        }

        Console.WriteLine($"schema-overhead: transcode descriptions {transcodeChars} chars (max {TranscodeSchemaMaxChars})");
        Console.WriteLine($"schema-overhead: core tool descriptions {coreChars} chars (max {CoreSchemaMaxChars})");
        assert("schema overhead: transcode descriptions bounded", transcodeChars <= TranscodeSchemaMaxChars);
        assert("schema overhead: core descriptions bounded", coreChars <= CoreSchemaMaxChars);

        var envelope = JsonSerializer.Serialize(
            new OccamTranscodeSuccessResponse(
                true,
                new OccamTranscodeUrlInfo("https://example.com/", "https://example.com/"),
                "Hello",
                "http",
                MediaRefs: null,
                Confidence: 0.82,
                Quality: new OccamTranscodeQualityInfo(0.82, 0.1, 0.7, 0.6, 0.5, "rich"),
                ContentHash: "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
                MaterializationKey: "mk1",
                Completeness: new OccamMcp.Core.Semantics.SemanticCompletenessInfo("complete")),
            OccamTranscodeJsonContext.Default.OccamTranscodeSuccessResponse);
        Console.WriteLine($"schema-overhead: default envelope {envelope.Length} chars (max {DefaultEnvelopeMaxChars})");
        assert("schema overhead: default envelope omits empty mediaRefs", !envelope.Contains("\"mediaRefs\"", StringComparison.Ordinal));
        assert("schema overhead: default envelope omits unused verdict", !envelope.Contains("not_evaluated", StringComparison.Ordinal));
        assert("schema overhead: default envelope omits unused focus", !envelope.Contains("not_requested", StringComparison.Ordinal));
        assert("schema overhead: default envelope keeps quality verdict", envelope.Contains("\"verdict\":\"rich\"", StringComparison.Ordinal));
        assert("schema overhead: default envelope bounded", envelope.Length <= DefaultEnvelopeMaxChars);

        var includeMedia = typeof(OccamTranscodeTool)
            .GetMethod(nameof(OccamTranscodeTool.Transcode))!
            .GetParameters()
            .First(p => p.Name == "include_media_refs");
        assert(
            "schema overhead: include_media_refs default false",
            includeMedia.HasDefaultValue && Equals(includeMedia.DefaultValue, false));
    }

    private static Dictionary<string, MethodInfo> DiscoverToolMethods()
    {
        var map = new Dictionary<string, MethodInfo>(StringComparer.Ordinal);
        var asm = typeof(OccamTranscodeTool).Assembly;
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

    private static int DescriptionChars(MethodInfo method)
    {
        var total = method.GetCustomAttribute<DescriptionAttribute>()?.Description?.Length ?? 0;
        foreach (var p in method.GetParameters())
        {
            if (p.ParameterType == typeof(CancellationToken))
            {
                continue;
            }

            total += p.GetCustomAttribute<DescriptionAttribute>()?.Description?.Length ?? 0;
        }

        return total;
    }
}
