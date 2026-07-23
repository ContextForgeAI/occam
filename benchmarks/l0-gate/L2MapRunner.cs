using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L2MapRunner
{
    public static void Run(MapService map, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l2 map corpus: {corpusPath}");
        assert("l2 map corpus exists", File.Exists(corpusPath));

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L2MapJsonContext.Default.L2MapCase);
            if (entry is null)
            {
                assert("l2 map/json parse", false);
                continue;
            }

            Console.WriteLine($"l2 map: {entry.Id} ({entry.Url}) source={entry.Source}");
            var analysis = map.MapAsync(
                entry.Url,
                entry.MaxLinks ?? MapService.DefaultMaxLinks,
                entry.SameDomain ?? true,
                entry.TimeoutMs ?? MapService.DefaultTimeoutMs,
                entry.Source ?? "homepage",
                entry.FilterNonsense ?? true,
                entry.FocusQuery).GetAwaiter().GetResult();

            assert($"l2 map/{entry.Id} ok={entry.ExpectOk}", analysis.Ok == entry.ExpectOk);

            if (!entry.ExpectOk)
            {
                if (!string.IsNullOrWhiteSpace(entry.FailureCode))
                {
                    assert(
                        $"l2 map/{entry.Id} failure",
                        string.Equals(analysis.FailureCode, entry.FailureCode, StringComparison.OrdinalIgnoreCase));
                }

                continue;
            }

            if (entry.MinLinks is > 0)
            {
                assert($"l2 map/{entry.Id} min_links", analysis.LinkCount >= entry.MinLinks);
            }

            if (entry.MaxLinkCount is > 0)
            {
                assert($"l2 map/{entry.Id} max_link_count", analysis.LinkCount <= entry.MaxLinkCount);
            }

            if (!string.IsNullOrWhiteSpace(entry.ExpectSameHost))
            {
                assert(
                    $"l2 map/{entry.Id} same_host",
                    analysis.Links.All(link => HostMatches(link.Url, entry.ExpectSameHost!)));
            }

            if (entry.MustNotPathSuffix is { Length: > 0 })
            {
                foreach (var suffix in entry.MustNotPathSuffix)
                {
                    assert(
                        $"l2 map/{entry.Id} no_suffix/{suffix}",
                        analysis.Links.All(link => !link.Path.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)));
                }
            }

            if (entry.TopLinkPathContains is { Length: > 0 } && analysis.Links.Count > 0)
            {
                var topPath = analysis.Links[0].Path;
                var matched = entry.TopLinkPathContains.Any(marker =>
                    topPath.Contains(marker, StringComparison.OrdinalIgnoreCase));
                assert($"l2 map/{entry.Id} top_link_path", matched);
            }
        }
    }

    private static bool HostMatches(string url, string expectedHost) =>
        Uri.TryCreate(url, UriKind.Absolute, out var uri)
        && uri.Host.Equals(expectedHost, StringComparison.OrdinalIgnoreCase);

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome();
        if (home is not null)
        {
            var path = Path.Combine(home, "corpora", "l2-map.jsonl");
            if (File.Exists(path))
            {
                return path;
            }
        }

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l2-map.jsonl");
        if (File.Exists(cwd))
        {
            return cwd;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "corpora", "l2-map.jsonl"));
    }
}

internal sealed class L2MapCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("max_links")]
    public int? MaxLinks { get; init; }

    [JsonPropertyName("same_domain")]
    public bool? SameDomain { get; init; }

    [JsonPropertyName("filter_nonsense")]
    public bool? FilterNonsense { get; init; }

    [JsonPropertyName("focus_query")]
    public string? FocusQuery { get; init; }

    [JsonPropertyName("timeout_ms")]
    public int? TimeoutMs { get; init; }

    [JsonPropertyName("expect_ok")]
    public bool ExpectOk { get; init; } = true;

    [JsonPropertyName("min_links")]
    public int? MinLinks { get; init; }

    [JsonPropertyName("max_link_count")]
    public int? MaxLinkCount { get; init; }

    [JsonPropertyName("expect_same_host")]
    public string? ExpectSameHost { get; init; }

    [JsonPropertyName("must_not_path_suffix")]
    public string[]? MustNotPathSuffix { get; init; }

    [JsonPropertyName("top_link_path_contains")]
    public string[]? TopLinkPathContains { get; init; }

    [JsonPropertyName("failure_code")]
    public string? FailureCode { get; init; }
}

[JsonSerializable(typeof(L2MapCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L2MapJsonContext : JsonSerializerContext;
