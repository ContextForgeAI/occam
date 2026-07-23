using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L2DigestRunner
{
    public static void Run(DigestService digest, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l2 digest corpus: {corpusPath}");
        assert("l2 digest corpus exists", File.Exists(corpusPath));

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L2DigestJsonContext.Default.L2DigestCase);
            if (entry is null)
            {
                assert($"l2 digest/{entry?.Id ?? "parse"} json", false);
                continue;
            }

            Console.WriteLine($"l2 digest: {entry.Id} ({entry.Urls.Length} urls)");
            if (!OccamBackendPolicyParser.TryParse(entry.Backend, out var policy))
            {
                assert($"l2 digest/{entry.Id} backend", false);
                continue;
            }

            var urlsJson = JsonSerializer.Serialize(entry.Urls);
            var analysis = digest.Digest(
                urlsJson,
                maxUrls: entry.MaxUrls ?? DigestService.MaxUrlsCap,
                perUrlMaxTokens: entry.PerUrlMaxTokens,
                backendPolicy: policy,
                focusQuery: entry.FocusQuery,
                fitMarkdown: entry.FitMarkdown ?? true,
                includeCombined: entry.ExpectCombined ?? true);

            assert($"l2 digest/{entry.Id} ok={entry.ExpectOk}", analysis.Ok == entry.ExpectOk);

            if (!entry.ExpectOk)
            {
                if (!string.IsNullOrWhiteSpace(entry.FailureCode))
                {
                    assert($"l2 digest/{entry.Id} failure", analysis.FailureCode == entry.FailureCode);
                }

                continue;
            }

            assert($"l2 digest/{entry.Id} min_succeeded", analysis.Succeeded >= entry.MinSucceeded);

            if (entry.ExpectCombined == true)
            {
                assert($"l2 digest/{entry.Id} combined", !string.IsNullOrWhiteSpace(analysis.Combined));
            }

            if (entry.PerUrlMaxTokens is not null)
            {
                foreach (var item in analysis.Items.Where(i => i.Ok))
                {
                    assert(
                        $"l2 digest/{entry.Id} tokens/{item.Url}",
                        item.TokensEstimated <= entry.PerUrlMaxTokens.Value + 32);
                }
            }

            if (!string.IsNullOrWhiteSpace(entry.FocusQuery))
            {
                foreach (var item in analysis.Items.Where(i => i.Ok))
                {
                    assert(
                        $"l2 digest/{entry.Id} focusMatched/{item.Url}",
                        item.FocusMatched is not null);
                }

                if (entry.FocusMatchedByUrl is { Length: > 0 })
                {
                    foreach (var expect in entry.FocusMatchedByUrl)
                    {
                        var matchItem = analysis.Items.FirstOrDefault(i =>
                            i.Ok && i.Url.Contains(expect.UrlContains, StringComparison.OrdinalIgnoreCase));
                        if (matchItem is null)
                        {
                            assert($"l2 digest/{entry.Id} focusMatched expect/{expect.UrlContains}", false);
                            continue;
                        }

                        assert(
                            $"l2 digest/{entry.Id} focusMatched/{expect.UrlContains}={expect.Expect}",
                            matchItem.FocusMatched == expect.Expect);
                    }
                }
            }
        }
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var path = Path.Combine(home, "corpora", "l2-digest.jsonl");
            if (File.Exists(path))
            {
                return path;
            }
        }

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l2-digest.jsonl");
        if (File.Exists(cwd))
        {
            return cwd;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "corpora", "l2-digest.jsonl"));
    }
}

internal sealed class L2DigestCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("urls")]
    public string[] Urls { get; init; } = [];

    [JsonPropertyName("backend")]
    public string Backend { get; init; } = "http_then_browser";

    [JsonPropertyName("max_urls")]
    public int? MaxUrls { get; init; }

    [JsonPropertyName("per_url_max_tokens")]
    public int? PerUrlMaxTokens { get; init; }

    [JsonPropertyName("fit_markdown")]
    public bool? FitMarkdown { get; init; }

    [JsonPropertyName("expect_ok")]
    public bool ExpectOk { get; init; } = true;

    [JsonPropertyName("min_succeeded")]
    public int MinSucceeded { get; init; } = 1;

    [JsonPropertyName("expect_combined")]
    public bool? ExpectCombined { get; init; }

    [JsonPropertyName("focus_query")]
    public string? FocusQuery { get; init; }

    [JsonPropertyName("focus_matched_by_url")]
    public L2DigestFocusMatchExpect[]? FocusMatchedByUrl { get; init; }

    [JsonPropertyName("failure_code")]
    public string? FailureCode { get; init; }
}

internal sealed class L2DigestFocusMatchExpect
{
    [JsonPropertyName("url_contains")]
    public string UrlContains { get; init; } = "";

    [JsonPropertyName("expect")]
    public bool Expect { get; init; }
}

[JsonSerializable(typeof(L2DigestCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L2DigestJsonContext : JsonSerializerContext;
