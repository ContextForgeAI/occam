using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal static class L2SessionRunner
{
    public static void Run(
        TranscodePipeline pipeline,
        ProbeService probe,
        DigestService digest,
        MapService map,
        Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l2 session corpus: {corpusPath}");
        assert("l2 session corpus exists", File.Exists(corpusPath));

        foreach (var line in File.ReadAllLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var entry = JsonSerializer.Deserialize(line, L2SessionJsonContext.Default.L2SessionCase);
            if (entry is null)
            {
                assert("l2 session/json parse", false);
                continue;
            }

            Console.WriteLine($"l2 session: {entry.Id} tool={entry.Tool}");
            switch (entry.Tool.Trim().ToLowerInvariant())
            {
                case "transcode":
                    RunTranscode(pipeline, entry, assert);
                    break;
                case "probe":
                    RunProbe(probe, entry, assert);
                    break;
                case "digest":
                    RunDigest(digest, entry, assert);
                    break;
                case "map":
                    RunMap(map, entry, assert);
                    break;
                default:
                    assert($"l2 session/{entry.Id} tool", false);
                    break;
            }
        }
    }

    private static void RunTranscode(TranscodePipeline pipeline, L2SessionCase entry, Action<string, bool> assert)
    {
        if (!OccamBackendPolicyParser.TryParse(entry.BackendPolicy ?? "http_then_browser", out var policy))
        {
            assert($"l2 session/{entry.Id} backend", false);
            return;
        }

        var options = new OccamTranscodeOptions
        {
            SessionProfile = entry.SessionProfile,
        };
        var result = pipeline.Transcode(entry.Url!, policy, options, CancellationToken.None);
        assert($"l2 session/{entry.Id} ok={entry.ExpectOk}", result.Ok == entry.ExpectOk);

        if (!entry.ExpectOk)
        {
            if (!string.IsNullOrWhiteSpace(entry.FailureCode))
            {
                assert(
                    $"l2 session/{entry.Id} failure",
                    string.Equals(result.FailureCode, entry.FailureCode, StringComparison.OrdinalIgnoreCase));
            }

            return;
        }

        if (entry.MinMarkdownChars is > 0)
        {
            assert(
                $"l2 session/{entry.Id} min_markdown_chars",
                (result.Markdown?.Length ?? 0) >= entry.MinMarkdownChars);
        }
    }

    private static void RunProbe(ProbeService probe, L2SessionCase entry, Action<string, bool> assert)
    {
        var analysis = probe.AnalyzeAsync(entry.Url!, sessionProfile: entry.SessionProfile).GetAwaiter().GetResult();
        assert($"l2 session/{entry.Id} ok={entry.ExpectOk}", analysis.Ok == entry.ExpectOk);
        if (!entry.ExpectOk && !string.IsNullOrWhiteSpace(entry.FailureCode))
        {
            assert(
                $"l2 session/{entry.Id} failure",
                string.Equals(analysis.FailureCode, entry.FailureCode, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void RunDigest(DigestService digest, L2SessionCase entry, Action<string, bool> assert)
    {
        if (!OccamBackendPolicyParser.TryParse(entry.BackendPolicy ?? "http_then_browser", out var policy))
        {
            assert($"l2 session/{entry.Id} backend", false);
            return;
        }

        var urlsJson = JsonSerializer.Serialize(entry.Urls ?? []);
        var analysis = digest.Digest(
            urlsJson,
            backendPolicy: policy,
            sessionProfile: entry.SessionProfile);
        assert($"l2 session/{entry.Id} ok={entry.ExpectOk}", analysis.Ok == entry.ExpectOk);
        if (!entry.ExpectOk && !string.IsNullOrWhiteSpace(entry.FailureCode))
        {
            assert(
                $"l2 session/{entry.Id} failure",
                string.Equals(analysis.FailureCode, entry.FailureCode, StringComparison.OrdinalIgnoreCase));
        }
    }

    private static void RunMap(MapService map, L2SessionCase entry, Action<string, bool> assert)
    {
        var analysis = map.MapAsync(
            entry.Url!,
            entry.MaxLinks ?? MapService.DefaultMaxLinks,
            source: entry.Source ?? "homepage",
            sessionProfile: entry.SessionProfile).GetAwaiter().GetResult();
        assert($"l2 session/{entry.Id} ok={entry.ExpectOk}", analysis.Ok == entry.ExpectOk);

        if (!entry.ExpectOk)
        {
            if (!string.IsNullOrWhiteSpace(entry.FailureCode))
            {
                assert(
                    $"l2 session/{entry.Id} failure",
                    string.Equals(analysis.FailureCode, entry.FailureCode, StringComparison.OrdinalIgnoreCase));
            }

            return;
        }

        if (entry.MinLinks is > 0)
        {
            assert($"l2 session/{entry.Id} min_links", analysis.LinkCount >= entry.MinLinks);
        }

        if (!string.IsNullOrWhiteSpace(entry.ExpectSameHost))
        {
            assert(
                $"l2 session/{entry.Id} same_host",
                analysis.Links.All(link => HostMatches(link.Url, entry.ExpectSameHost!)));
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
            var path = Path.Combine(home, "corpora", "l2-session.jsonl");
            if (File.Exists(path))
            {
                return path;
            }
        }

        var cwd = Path.Combine(Directory.GetCurrentDirectory(), "corpora", "l2-session.jsonl");
        if (File.Exists(cwd))
        {
            return cwd;
        }

        return Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "corpora", "l2-session.jsonl"));
    }
}

internal sealed class L2SessionCase
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = "";

    [JsonPropertyName("tool")]
    public string Tool { get; init; } = "";

    [JsonPropertyName("url")]
    public string? Url { get; init; }

    [JsonPropertyName("urls")]
    public string[]? Urls { get; init; }

    [JsonPropertyName("backend_policy")]
    public string? BackendPolicy { get; init; }

    [JsonPropertyName("session_profile")]
    public string? SessionProfile { get; init; }

    [JsonPropertyName("source")]
    public string? Source { get; init; }

    [JsonPropertyName("max_links")]
    public int? MaxLinks { get; init; }

    [JsonPropertyName("expect_ok")]
    public bool ExpectOk { get; init; } = true;

    [JsonPropertyName("failure_code")]
    public string? FailureCode { get; init; }

    [JsonPropertyName("min_markdown_chars")]
    public int? MinMarkdownChars { get; init; }

    [JsonPropertyName("min_links")]
    public int? MinLinks { get; init; }

    [JsonPropertyName("expect_same_host")]
    public string? ExpectSameHost { get; init; }
}

[JsonSerializable(typeof(L2SessionCase))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class L2SessionJsonContext : JsonSerializerContext;
