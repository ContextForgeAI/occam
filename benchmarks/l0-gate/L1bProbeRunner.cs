using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L1bProbeRunner
{
    public static void Run(ProbeService probe, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l1b probe corpus: {corpusPath}");
        assert("l1b probe corpus exists", File.Exists(corpusPath));

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L1bProbeJsonContext.Default.L1bProbeCase);
            if (entry is null)
            {
                assert($"l1b probe/{entry?.Id ?? "parse"} json", false);
                continue;
            }

            Console.WriteLine($"l1b probe: {entry.Id} ({entry.Url})");
            var analysis = probe.AnalyzeAsync(entry.Url, timeoutMs: 15_000).GetAwaiter().GetResult();
            assert($"l1b probe/{entry.Id} ok={entry.ExpectOk}", analysis.Ok == entry.ExpectOk);

            if (!entry.ExpectOk)
            {
                if (!string.IsNullOrWhiteSpace(entry.FailureCode))
                {
                    assert($"l1b probe/{entry.Id} failure", analysis.FailureCode == entry.FailureCode);
                }

                continue;
            }

            var signals = analysis.Classification?.Signals;
            if (!string.IsNullOrWhiteSpace(entry.PageClass))
            {
                assert($"l1b probe/{entry.Id} page_class", signals?.PageClass == entry.PageClass);
            }

            if (entry.RequiresJavascript is not null)
            {
                assert($"l1b probe/{entry.Id} requires_javascript", signals?.RequiresJavascript == entry.RequiresJavascript);
            }

            if (!string.IsNullOrWhiteSpace(entry.Backend))
            {
                assert($"l1b probe/{entry.Id} backend", analysis.RecommendedBackend == entry.Backend);
            }

            if (entry.BackendOneOf is { Length: > 0 })
            {
                assert($"l1b probe/{entry.Id} backend_one_of", entry.BackendOneOf.Contains(analysis.RecommendedBackend));
            }

            if (entry.ExpectRedirect == true)
            {
                assert($"l1b probe/{entry.Id} redirect_chain", analysis.RedirectChain is { Length: > 0 });
            }

            if (entry.ExpectFinalHttps == true)
            {
                assert($"l1b probe/{entry.Id} final_https", analysis.FinalUrl?.StartsWith("https://", StringComparison.OrdinalIgnoreCase) == true);
            }
        }
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var path = Path.Combine(home, "corpora", "l1b-probe.jsonl");
            if (File.Exists(path))
            {
                return path;
            }
        }

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l1b-probe.jsonl");
        if (File.Exists(cwd))
        {
            return cwd;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "corpora", "l1b-probe.jsonl"));
    }
}

internal sealed class L1bProbeCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("expect_ok")]
    public bool ExpectOk { get; init; }

    [JsonPropertyName("page_class")]
    public string? PageClass { get; init; }

    [JsonPropertyName("requires_javascript")]
    public bool? RequiresJavascript { get; init; }

    [JsonPropertyName("backend")]
    public string? Backend { get; init; }

    [JsonPropertyName("backend_one_of")]
    public string[]? BackendOneOf { get; init; }

    [JsonPropertyName("failure_code")]
    public string? FailureCode { get; init; }

    [JsonPropertyName("expect_redirect")]
    public bool? ExpectRedirect { get; init; }

    [JsonPropertyName("expect_final_https")]
    public bool? ExpectFinalHttps { get; init; }
}

[JsonSerializable(typeof(L1bProbeCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L1bProbeJsonContext : JsonSerializerContext;
