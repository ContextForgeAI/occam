using System.Diagnostics;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// L9 golden set: deterministic extraction-fidelity regression net. A local fixture server serves
/// FROZEN HTML (benchmarks/l0-gate/fixtures/golden/*.html) so the assertions catch CODE regressions,
/// not live-site drift (the probe-nuxt lesson — a live URL turned into a Cloudflare wall and flaked
/// the gate). Each case pins the transcode outcome: ok / failure_code, a char-length band, and
/// must-contain / must-not-contain content markers (the fidelity + boilerplate-stripping check).
/// </summary>
internal static class L9GoldenRunner
{
    public static void Run(TranscodePipeline pipeline, Action<string, bool> assert)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"l9 golden corpus: {corpusPath}");
        assert("l9 golden corpus exists", File.Exists(corpusPath));
        if (!File.Exists(corpusPath))
        {
            return;
        }

        if (!OccamBackendPolicyParser.TryParse("http", out var defaultPolicy))
        {
            assert("l9 golden backend policy", false);
            return;
        }

        var savedPrivate = Environment.GetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS");
        var savedHttpDaemon = Environment.GetEnvironmentVariable("OCCAM_HTTP_DAEMON");
        var savedBrowserDaemon = Environment.GetEnvironmentVariable("OCCAM_BROWSER_DAEMON");
        Process? fixtureProcess = null;
        try
        {
            var fixture = StartGoldenFixtureProcess().GetAwaiter().GetResult();
            fixtureProcess = fixture.Process;
            assert("l9 golden fixture", fixture.Url is not null);
            if (fixture.Url is null)
            {
                return;
            }

            // 127.0.0.1 fixture is a private URL — allow it only for this deterministic gate.
            Environment.SetEnvironmentVariable("OCCAM_ALLOW_PRIVATE_URLS", "1");
            // One-shot workers so per-case features (json_tables/json_feed) reach the worker.
            Environment.SetEnvironmentVariable("OCCAM_HTTP_DAEMON", "0");
            HttpDaemonHost.Stop();
            // Same for the browser backend: force isolated one-shot workers so a browser golden case
            // spawns a fresh worker that INHERITS OCCAM_ALLOW_PRIVATE_URLS (the shared pool started in
            // L6 predates this env, so a daemon route would reject the 127.0.0.1 fixture).
            Environment.SetEnvironmentVariable("OCCAM_BROWSER_DAEMON", "0");

            foreach (var line in File.ReadAllLines(corpusPath))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                var entry = JsonSerializer.Deserialize(line, L9GoldenJsonContext.Default.L9GoldenCase);
                if (entry is null || string.IsNullOrWhiteSpace(entry.Fixture))
                {
                    assert("l9 golden/json parse", false);
                    continue;
                }

                var url = $"{fixture.Url.TrimEnd('/')}/{entry.Fixture}";
                // Per-case backend override (default http); a browser case locks the browser-worker
                // path (e.g. json_blocks collection, which lives in extract-html.mjs, not just the
                // http worker). Unknown values fall back to the http default with a flagged assert.
                var policy = defaultPolicy;
                if (!string.IsNullOrWhiteSpace(entry.Backend)
                    && !OccamBackendPolicyParser.TryParse(entry.Backend, out policy))
                {
                    assert($"l9 golden/{entry.Id} backend={entry.Backend}", false);
                    policy = defaultPolicy;
                }

                Console.WriteLine($"l9 golden: {entry.Id} ({entry.Fixture}) backend={entry.Backend ?? "http"}");
                var options = new OccamTranscodeOptions
                {
                    JsonTables = entry.JsonTables,
                    JsonBlocks = entry.JsonBlocks,
                    JsonFeed = entry.JsonFeed,
                };
                var result = pipeline.Transcode(url, policy, options, CancellationToken.None);

                assert($"l9 golden/{entry.Id} ok={entry.ExpectOk}", result.Ok == entry.ExpectOk);

                if (!entry.ExpectOk)
                {
                    if (!string.IsNullOrWhiteSpace(entry.FailureCode))
                    {
                        assert(
                            $"l9 golden/{entry.Id} failure={entry.FailureCode}",
                            string.Equals(result.FailureCode, entry.FailureCode, StringComparison.OrdinalIgnoreCase));
                    }

                    continue;
                }

                var md = result.Markdown ?? string.Empty;

                if (entry.MinChars is { } min)
                {
                    assert($"l9 golden/{entry.Id} min_chars>={min} (got {md.Length})", md.Length >= min);
                }

                if (entry.MaxChars is { } max)
                {
                    assert($"l9 golden/{entry.Id} max_chars<={max} (got {md.Length})", md.Length <= max);
                }

                foreach (var marker in entry.MustContain ?? Array.Empty<string>())
                {
                    assert(
                        $"l9 golden/{entry.Id} contains \"{marker}\"",
                        md.Contains(marker, StringComparison.Ordinal));
                }

                foreach (var marker in entry.MustNotContain ?? Array.Empty<string>())
                {
                    assert(
                        $"l9 golden/{entry.Id} strips \"{marker}\"",
                        !md.Contains(marker, StringComparison.Ordinal));
                }

                // Structured output (opt-in via json_blocks / json_tables / json_feed).
                if (entry.MinBlocks is { } minBlocks)
                {
                    assert(
                        $"l9 golden/{entry.Id} blocks>={minBlocks} (got {result.Blocks?.Count ?? 0})",
                        (result.Blocks?.Count ?? 0) >= minBlocks);
                }

                if (entry.MinTables is { } minTables)
                {
                    assert(
                        $"l9 golden/{entry.Id} tables>={minTables} (got {result.Tables?.Count ?? 0})",
                        (result.Tables?.Count ?? 0) >= minTables);
                }

                if (entry.TableContains is { Length: > 0 })
                {
                    var cells = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var table in result.Tables ?? Array.Empty<WorkerExtractTableInfo>())
                    {
                        foreach (var h in table.Headers)
                        {
                            cells.Add(h);
                        }

                        foreach (var row in table.Rows)
                        {
                            foreach (var cell in row)
                            {
                                cells.Add(cell);
                            }
                        }
                    }

                    foreach (var marker in entry.TableContains)
                    {
                        assert($"l9 golden/{entry.Id} table cell \"{marker}\"", cells.Contains(marker));
                    }
                }

                if (!string.IsNullOrEmpty(entry.FeedTitle))
                {
                    assert(
                        $"l9 golden/{entry.Id} feed title \"{entry.FeedTitle}\"",
                        result.Feed is not null && result.Feed.Title.Contains(entry.FeedTitle, StringComparison.Ordinal));
                }

                if (entry.MinFeedItems is { } minItems)
                {
                    assert(
                        $"l9 golden/{entry.Id} feed items>={minItems} (got {result.Feed?.Items.Length ?? 0})",
                        (result.Feed?.Items.Length ?? 0) >= minItems);
                }
            }
        }
        finally
        {
            if (fixtureProcess is { HasExited: false })
            {
                try
                {
                    fixtureProcess.Kill(entireProcessTree: true);
                }
                catch
                {
                    // best effort
                }
            }

            Environment.SetEnvironmentVariable(
                "OCCAM_ALLOW_PRIVATE_URLS",
                string.IsNullOrWhiteSpace(savedPrivate) ? null : savedPrivate);
            Environment.SetEnvironmentVariable(
                "OCCAM_HTTP_DAEMON",
                string.IsNullOrWhiteSpace(savedHttpDaemon) ? null : savedHttpDaemon);
            Environment.SetEnvironmentVariable(
                "OCCAM_BROWSER_DAEMON",
                string.IsNullOrWhiteSpace(savedBrowserDaemon) ? null : savedBrowserDaemon);
        }
    }

    private static async Task<(Process? Process, string? Url)> StartGoldenFixtureProcess()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var fixture = Path.Combine(home, "benchmarks", "l0-gate", "fixtures", "golden-http-fixture-launch.mjs");
        if (!File.Exists(fixture))
        {
            return (null, null);
        }

        var psi = new ProcessStartInfo
        {
            FileName = NodeRuntime.ResolveExecutable(),
            Arguments = $"\"{fixture}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true,
            StandardOutputEncoding = Encoding.UTF8,
        };

        var process = Process.Start(psi);
        if (process is null)
        {
            return (null, null);
        }

        var deadline = Environment.TickCount64 + 8_000;
        while (Environment.TickCount64 < deadline && !process.HasExited)
        {
            var line = await process.StandardOutput.ReadLineAsync();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            try
            {
                using var doc = JsonDocument.Parse(line);
                if (doc.RootElement.TryGetProperty("url", out var urlProp))
                {
                    return (process, urlProp.GetString());
                }
            }
            catch
            {
                // keep reading until the JSON {url} line arrives
            }
        }

        return (process, null);
    }

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        return Path.Combine(home, "corpora", "l9-golden.jsonl");
    }
}

internal sealed class L9GoldenCase
{
    [JsonPropertyName("id")] public string? Id { get; set; }
    [JsonPropertyName("fixture")] public string? Fixture { get; set; }
    [JsonPropertyName("backend")] public string? Backend { get; set; }
    [JsonPropertyName("expect_ok")] public bool ExpectOk { get; set; }
    [JsonPropertyName("failure_code")] public string? FailureCode { get; set; }
    [JsonPropertyName("min_chars")] public int? MinChars { get; set; }
    [JsonPropertyName("max_chars")] public int? MaxChars { get; set; }
    [JsonPropertyName("must_contain")] public string[]? MustContain { get; set; }
    [JsonPropertyName("must_not_contain")] public string[]? MustNotContain { get; set; }
    [JsonPropertyName("json_tables")] public bool JsonTables { get; set; }
    [JsonPropertyName("json_blocks")] public bool JsonBlocks { get; set; }
    [JsonPropertyName("json_feed")] public bool JsonFeed { get; set; }
    [JsonPropertyName("min_blocks")] public int? MinBlocks { get; set; }
    [JsonPropertyName("min_tables")] public int? MinTables { get; set; }
    [JsonPropertyName("table_contains")] public string[]? TableContains { get; set; }
    [JsonPropertyName("feed_title")] public string? FeedTitle { get; set; }
    [JsonPropertyName("min_feed_items")] public int? MinFeedItems { get; set; }
}

[JsonSerializable(typeof(L9GoldenCase))]
internal sealed partial class L9GoldenJsonContext : JsonSerializerContext
{
}
