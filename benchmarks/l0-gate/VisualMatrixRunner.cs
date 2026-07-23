using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Caching;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>Minimal factory for the ad-hoc tool wiring — time-anchoring is off in the gate, so this is never actually called.</summary>
internal sealed class StubHttpClientFactory : IHttpClientFactory
{
    public HttpClient CreateClient(string name) => new();
}

internal sealed class VisualMatrixCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("category")]
    public string Category { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("notes")]
    public string? Notes { get; init; }

    [JsonPropertyName("expect_ok")]
    public bool? ExpectOk { get; init; }

    [JsonPropertyName("timeout_ms")]
    public int? TimeoutMs { get; init; }

    [JsonPropertyName("include_social_meta")]
    public bool? IncludeSocialMeta { get; init; }

    [JsonPropertyName("backend")]
    public string? Backend { get; init; }

    [JsonPropertyName("max_tokens")]
    public int? MaxTokens { get; init; }

    [JsonPropertyName("fit_markdown")]
    public bool? FitMarkdown { get; init; }

    [JsonPropertyName("focus_query")]
    public string? FocusQuery { get; init; }

    [JsonPropertyName("content_selectors")]
    public string? ContentSelectors { get; init; }
}

internal sealed record VisualMatrixCaseResult(
    VisualMatrixCase Case,
    bool Pass,
    List<string> Failures,
    string CategoryDir,
    string CaseDir,
    string? ProbeJson,
    string? TranscodeJson,
    string? Markdown,
    Dictionary<string, string> Files);

[JsonSerializable(typeof(VisualMatrixCase))]
[JsonSerializable(typeof(VisualMatrixMetaDto))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class VisualMatrixJsonContext : JsonSerializerContext;

internal sealed class VisualMatrixMetaDto
{
    public string Id { get; init; } = "";
    public string Category { get; init; } = "";
    public string Url { get; init; } = "";
    public string? Notes { get; init; }
    public bool ExpectOk { get; init; }
    public bool Pass { get; init; }
    public string[] Failures { get; init; } = [];
    public Dictionary<string, object?> Parameters { get; init; } = new();
    public Dictionary<string, string> Files { get; init; } = new();
}

internal static class VisualMatrixRunner
{
    public static int Run(
        WorkerPaths paths,
        TranscodePipeline pipeline,
        ProbeService probeService,
        bool openReport)
    {
        if (!paths.IsConfigured)
        {
            Console.Error.WriteLine("workers not configured — set OCCAM_HOME and run occam-doctor");
            return 1;
        }

        var corpusPath = ResolveCorpusPath();
        if (!File.Exists(corpusPath))
        {
            Console.Error.WriteLine($"visual matrix corpus missing: {corpusPath}");
            return 1;
        }

        var probeTool = new OccamProbeTool(probeService);
        var transcodeTool = new OccamTranscodeTool(paths, pipeline, new FeatureDiscoveryService(paths), new NoOpTranslationService(), new FileTranscodeResponseCache(), OccamMcp.Core.Receipts.ReceiptSigner.CreateEphemeral(), new OccamMcp.Core.Receipts.TimeAnchorService(new StubHttpClientFactory()), new OccamMcp.Core.Client.ClientCapabilityStore());
        var runDir = VisualMatrixWriter.CreateRunDirectory();
        var results = new List<VisualMatrixCaseResult>();

        Console.WriteLine($"visual matrix corpus: {corpusPath}");
        Console.WriteLine($"artifacts → {runDir}");

        foreach (var line in File.ReadLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, VisualMatrixJsonContext.Default.VisualMatrixCase);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                Console.Error.WriteLine("visual matrix: skip invalid line");
                continue;
            }

            Console.WriteLine($"visual: [{entry.Category}] {entry.Id} ({entry.Url})");
            VisualMatrixCaseResult result = entry.Category.ToLowerInvariant() switch
            {
                "occam_probe" => RunProbeCase(entry, probeTool),
                "occam_transcode" => RunTranscodeCase(entry, transcodeTool),
                "recipe_a" => RunRecipeCase(entry, probeTool, transcodeTool),
                _ => RunUnsupported(entry),
            };

            VisualMatrixWriter.WriteCaseArtifacts(runDir, result);
            results.Add(result);
            Console.WriteLine(result.Pass ? $"  PASS {entry.Id}" : $"  FAIL {entry.Id}: {string.Join("; ", result.Failures)}");
        }

        var indexPath = VisualMatrixWriter.WriteMasterIndex(runDir, results);
        VisualMatrixWriter.WriteHowToRead(runDir, results.Count);
        VisualMatrixWriter.WriteLatestPointer(runDir);

        Console.WriteLine();
        Console.WriteLine($"VISUAL_MATRIX_REPORT: {indexPath}");
        Console.WriteLine($"HOW-TO-READ: {Path.Combine(runDir, "HOW-TO-READ.ru.md")}");

        if (openReport)
        {
            TryOpenFile(indexPath);
        }

        return results.All(r => r.Pass) ? 0 : 1;
    }

    private static VisualMatrixCaseResult RunProbeCase(VisualMatrixCase entry, OccamProbeTool tool)
    {
        var failures = new List<string>();
        var expectOk = entry.ExpectOk ?? true;
        var timeout = entry.TimeoutMs ?? 15_000;
        var social = entry.IncludeSocialMeta ?? false;
        var json = tool.Probe(entry.Url, timeout, social).GetAwaiter().GetResult();

        var ok = TryReadOk(json);
        if (ok != expectOk)
        {
            failures.Add($"expected ok={expectOk}, got ok={ok}");
        }

        var categoryDir = "occam_probe";
        var caseDir = Path.Combine(categoryDir, SanitizeId(entry.Id));
        var files = new Dictionary<string, string> { ["response.json"] = "response.json" };

        return new VisualMatrixCaseResult(
            entry,
            failures.Count == 0,
            failures,
            categoryDir,
            caseDir,
            ProbeJson: json,
            TranscodeJson: null,
            Markdown: null,
            Files: files);
    }

    private static VisualMatrixCaseResult RunTranscodeCase(VisualMatrixCase entry, OccamTranscodeTool tool)
    {
        var failures = new List<string>();
        var expectOk = entry.ExpectOk ?? true;
        var backend = entry.Backend ?? "http_then_browser";
        var json = tool.Transcode(
            entry.Url,
            backend,
            entry.MaxTokens,
            entry.FitMarkdown ?? false,
            entry.FocusQuery,
            entry.ContentSelectors).Sync();

        var ok = TryReadOk(json);
        if (ok != expectOk)
        {
            failures.Add($"expected ok={expectOk}, got ok={ok}");
        }

        var markdown = ok ? TryReadMarkdown(json) : null;
        if (expectOk && ok && string.IsNullOrWhiteSpace(markdown))
        {
            failures.Add("expected non-empty markdown");
        }

        var categoryDir = "occam_transcode";
        var caseDir = Path.Combine(categoryDir, SanitizeId(entry.Id));
        var files = new Dictionary<string, string>
        {
            ["response.json"] = "response.json",
        };
        if (!string.IsNullOrWhiteSpace(markdown))
        {
            files["output.md"] = "output.md";
        }

        return new VisualMatrixCaseResult(
            entry,
            failures.Count == 0,
            failures,
            categoryDir,
            caseDir,
            ProbeJson: null,
            TranscodeJson: json,
            Markdown: markdown,
            Files: files);
    }

    private static VisualMatrixCaseResult RunRecipeCase(
        VisualMatrixCase entry,
        OccamProbeTool probeTool,
        OccamTranscodeTool transcodeTool)
    {
        var failures = new List<string>();
        var expectOk = entry.ExpectOk ?? true;
        var probeJson = probeTool.Probe(
            entry.Url,
            entry.TimeoutMs ?? 15_000,
            entry.IncludeSocialMeta ?? false).GetAwaiter().GetResult();
        var backend = entry.Backend ?? "http_then_browser";
        var transcodeJson = transcodeTool.Transcode(
            entry.Url,
            backend,
            entry.MaxTokens,
            entry.FitMarkdown ?? false,
            entry.FocusQuery,
            entry.ContentSelectors).Sync();

        var probeOk = TryReadOk(probeJson);
        var transcodeOk = TryReadOk(transcodeJson);
        if (expectOk && !probeOk)
        {
            failures.Add("probe failed in recipe_a");
        }

        if (transcodeOk != expectOk)
        {
            failures.Add($"transcode expected ok={expectOk}, got ok={transcodeOk}");
        }

        var markdown = transcodeOk ? TryReadMarkdown(transcodeJson) : null;
        var categoryDir = "recipe_a";
        var caseDir = Path.Combine(categoryDir, SanitizeId(entry.Id));
        var files = new Dictionary<string, string>
        {
            ["01-probe.json"] = "01-probe.json",
            ["02-transcode.json"] = "02-transcode.json",
        };
        if (!string.IsNullOrWhiteSpace(markdown))
        {
            files["02-transcode.md"] = "02-transcode.md";
        }

        return new VisualMatrixCaseResult(
            entry,
            failures.Count == 0,
            failures,
            categoryDir,
            caseDir,
            ProbeJson: probeJson,
            TranscodeJson: transcodeJson,
            Markdown: markdown,
            Files: files);
    }

    private static VisualMatrixCaseResult RunUnsupported(VisualMatrixCase entry) =>
        new(
            entry,
            false,
            ["unsupported category"],
            entry.Category,
            SanitizeId(entry.Id),
            null,
            null,
            null,
            []);

    private static bool TryReadOk(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("ok", out var ok) && ok.GetBoolean();
        }
        catch
        {
            return false;
        }
    }

    private static string? TryReadMarkdown(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.TryGetProperty("markdown", out var md) ? md.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    private static string SanitizeId(string id)
    {
        var invalid = Path.GetInvalidFileNameChars();
        var chars = id.Select(ch => invalid.Contains(ch) ? '_' : ch).ToArray();
        return chars.Length > 0 ? new string(chars) : "case";
    }

    public static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var fromHome = Path.Combine(home, "corpora", "visual-matrix.jsonl");
            if (File.Exists(fromHome))
            {
                return fromHome;
            }
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "corpora", "visual-matrix.jsonl");
    }

    private static void TryOpenFile(string path)
    {
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = path,
                UseShellExecute = true,
            });
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"Could not open {path}: {ex.Message}");
        }
    }
}

/// <summary>Gate-only no-op translation service for manual tool construction (no DI container).</summary>
internal sealed class NoOpTranslationService : ITranslationService
{
    public bool IsConfigured => false;
    public string? Translate(string text, string targetLang, out string? warning)
    {
        warning = "translate_endpoint_unconfigured";
        return null;
    }
}
