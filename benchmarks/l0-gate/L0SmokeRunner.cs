using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal sealed class L0SmokeCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("url")]
    public string Url { get; init; } = string.Empty;

    [JsonPropertyName("must_contain")]
    public string[]? MustContain { get; init; }

    [JsonPropertyName("must_not_contain")]
    public string[]? MustNotContain { get; init; }

    [JsonPropertyName("backend")]
    public string? Backend { get; init; }

    [JsonPropertyName("expect_ok")]
    public bool? ExpectOk { get; init; }

    [JsonPropertyName("failure_code")]
    public string? FailureCode { get; init; }

    [JsonPropertyName("env_blocked_ok")]
    public bool? EnvBlockedOk { get; init; }
}

[JsonSerializable(typeof(L0SmokeCase))]
internal partial class L0SmokeJsonContext : JsonSerializerContext;

internal static class L0SmokeRunner
{
    private static readonly HashSet<string> FastSmokeIds =
        new(StringComparer.OrdinalIgnoreCase) { "mdn-js-guide", "nginx-doc", "not-found" };

    public static void Run(
        string corpusPath,
        TranscodePipeline pipeline,
        Action<string, bool> assert,
        bool visual,
        string? visualRunDir,
        bool smokeFast = false)
    {
        var artifacts = new List<L0CaseArtifact>();

        if (!File.Exists(corpusPath))
        {
            assert("smoke corpus exists", false);
            return;
        }

        assert("smoke corpus exists", true);
        var skipIds = ParseSkipList(Environment.GetEnvironmentVariable("OCCAM_L0_SMOKE_SKIP"));

        foreach (var line in File.ReadLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L0SmokeJsonContext.Default.L0SmokeCase);
            if (entry is null || string.IsNullOrWhiteSpace(entry.Id))
            {
                assert("smoke case parse", false);
                continue;
            }

            if (skipIds.Contains(entry.Id))
            {
                Console.WriteLine($"smoke: {entry.Id} (skipped via OCCAM_L0_SMOKE_SKIP)");
                assert($"smoke/{entry.Id} skipped", true);
                continue;
            }

            if (smokeFast && !FastSmokeIds.Contains(entry.Id))
            {
                Console.WriteLine($"smoke: {entry.Id} (skipped — fast tier)");
                assert($"smoke/{entry.Id} fast-skipped", true);
                continue;
            }

            if (!OccamBackendPolicyParser.TryParse(entry.Backend, out var policy))
            {
                assert($"smoke/{entry.Id} backend", false);
                continue;
            }

            Console.WriteLine($"smoke: {entry.Id} ({entry.Url})");
            // Live external hosts flake transiently in CI (connect/DNS/timeout). Retry only
            // retryable failures so genuine failures (4xx, extraction, etc.) still fail fast.
            var result = pipeline.Transcode(entry.Url, policy, CancellationToken.None);
            for (var retry = 0;
                 retry < 2
                     && !result.Ok
                     && !string.IsNullOrEmpty(result.FailureCode)
                     && FailureCodeStrings.IsRetryable(result.FailureCode);
                 retry++)
            {
                Console.WriteLine($"smoke: {entry.Id} retry {retry + 1} (transient {result.FailureCode})");
                Thread.Sleep(1500);
                result = pipeline.Transcode(entry.Url, policy, CancellationToken.None);
            }
            var expectOk = entry.ExpectOk ?? true;
            var caseFailures = new List<string>();

            if (expectOk && !result.Ok && entry.EnvBlockedOk == true
                && string.Equals(result.FailureCode, "http_403", StringComparison.OrdinalIgnoreCase))
            {
                Console.WriteLine($"smoke: {entry.Id} ENV_BLOCKED (http_403) — env_blocked_ok");
                assert($"smoke/{entry.Id} env_blocked_ok", true);
                continue;
            }

            if (result.Ok != expectOk)
            {
                assert($"smoke/{entry.Id} ok={expectOk}", false);
                caseFailures.Add($"ok={expectOk}");
                Console.Error.WriteLine($"  expected ok={expectOk}, got ok={result.Ok}, failure={result.FailureCode}");
            }
            else
            {
                assert($"smoke/{entry.Id} ok={expectOk}", true);
            }

            if (expectOk && result.Ok)
            {
                var markdown = result.Markdown ?? string.Empty;
                foreach (var token in entry.MustContain ?? [])
                {
                    var found = markdown.Contains(token, StringComparison.OrdinalIgnoreCase);
                    assert($"smoke/{entry.Id} contains '{token}'", found);
                    if (!found)
                    {
                        caseFailures.Add($"missing '{token}'");
                    }
                }

                foreach (var token in entry.MustNotContain ?? [])
                {
                    var absent = !markdown.Contains(token, StringComparison.Ordinal);
                    assert($"smoke/{entry.Id} not contains '{token}'", absent);
                    if (!absent)
                    {
                        caseFailures.Add($"forbidden '{token}'");
                    }
                }
            }

            if (!expectOk && !string.IsNullOrWhiteSpace(entry.FailureCode))
            {
                assert($"smoke/{entry.Id} failure_code", result.FailureCode == entry.FailureCode);
                if (result.FailureCode != entry.FailureCode)
                {
                    caseFailures.Add($"failure_code={entry.FailureCode}");
                    Console.Error.WriteLine($"  expected failure={entry.FailureCode}, got {result.FailureCode}");
                }
            }

            if (visual && visualRunDir is not null)
            {
                var pass = caseFailures.Count == 0 && (result.Ok == expectOk);
                var artifact = L0ArtifactWriter.WriteCase(visualRunDir, entry, result, pass, caseFailures);
                artifacts.Add(artifact);
                L0ArtifactWriter.PrintConsoleExcerpt(artifact);
            }
        }

        if (visual && visualRunDir is not null && artifacts.Count > 0)
        {
            var indexPath = L0ArtifactWriter.WriteIndexHtml(visualRunDir, artifacts);
            L0ArtifactWriter.WriteLatestPointer(visualRunDir);
            Console.WriteLine($"VISUAL_REPORT: {indexPath}");
        }
    }

    public static string ResolveCorpusPath()
    {
        var root = WorkerPaths.ResolveOccamHome();
        if (root is not null)
        {
            var fromHome = Path.Combine(root, "corpora", "l0-smoke.jsonl");
            if (File.Exists(fromHome))
            {
                return fromHome;
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "corpora", "l0-smoke.jsonl");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l0-smoke.jsonl");
    }

    private static HashSet<string> ParseSkipList(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return [];
        }

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
    }
}
