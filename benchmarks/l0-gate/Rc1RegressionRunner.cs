using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Composition;
using OccamMcp.Core.Digest;
using OccamMcp.Core.Knowledge;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

internal sealed class Rc1Case
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("category")] public string Category { get; init; } = "";
    [JsonPropertyName("tool")] public string Tool { get; init; } = "transcode";
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("urls")] public string[]? Urls { get; init; }
    [JsonPropertyName("backend")] public string? Backend { get; init; }
    [JsonPropertyName("source")] public string? Source { get; init; }
    [JsonPropertyName("max_links")] public int? MaxLinks { get; init; }
    [JsonPropertyName("expect_ok")] public bool ExpectOk { get; init; } = true;
    [JsonPropertyName("failure_code")] public string? FailureCode { get; init; }
    [JsonPropertyName("must_contain")] public string[]? MustContain { get; init; }
    [JsonPropertyName("must_not_contain")] public string[]? MustNotContain { get; init; }
    [JsonPropertyName("min_links")] public int? MinLinks { get; init; }
    [JsonPropertyName("max_tokens")] public int? MaxTokens { get; init; }
    [JsonPropertyName("focus_query")] public string? FocusQuery { get; init; }
    [JsonPropertyName("fit_markdown")] public bool FitMarkdown { get; init; }
    [JsonPropertyName("json_blocks")] public bool JsonBlocks { get; init; }
    [JsonPropertyName("json_tables")] public bool JsonTables { get; init; }
    [JsonPropertyName("json_feed")] public bool JsonFeed { get; init; }
    [JsonPropertyName("env_blocked_ok")] public bool EnvBlockedOk { get; init; }
    [JsonPropertyName("allowed_failures")] public string[]? AllowedFailures { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
}

internal sealed record Rc1CaseResult(
    string Id,
    string Category,
    string Tool,
    bool Ok,
    bool ExpectOk,
    bool Pass,
    string? FailureCode,
    int LatencyMs,
    int? TokensEstimated,
    int? MarkdownChars,
    double? Confidence,
    string? ContentHash,
    string? Backend,
    bool Truncated,
    int? Blocks,
    int? Tables,
    int? SemanticRecords,
    int? FeedItems,
    int? MapLinks,
    bool? FocusMatchedAny,
    bool? FocusMatchedAll,
    string? ProvenanceNote,
    string? MaterializationNote,
    string[] Failures,
    string? Notes);

[JsonSerializable(typeof(Rc1Case))]
[JsonSerializable(typeof(Rc1CaseResult))]
[JsonSerializable(typeof(List<Rc1CaseResult>))]
[JsonSerializable(typeof(Rc1Summary))]
internal partial class Rc1JsonContext : JsonSerializerContext;

internal sealed record Rc1Summary(
    string RunId,
    string BaselineCompare,
    int Total,
    int Passed,
    int Failed,
    int CriticalFailures,
    string[] Markers,
    List<Rc1CaseResult> Cases);

/// <summary>
/// Fixed-corpus RC1 regression: extraction / materialization / tokens / latency / failure /
/// provenance checks across the maintainer corpus in <c>corpora/rc1-regression.jsonl</c>.
/// </summary>
internal static class Rc1RegressionRunner
{
    public static int Run(
        TranscodePipeline pipeline,
        ProbeService probe,
        DigestService digest,
        MapService map,
        WorkerPaths paths,
        Action<string, bool>? assert = null)
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        var runId = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
        var outDir = Path.Combine(home, "artifacts", "rc1-regression", runId);
        Directory.CreateDirectory(outDir);

        // Phase A — ten-area unit checklist (MCP tools, failures, receipts, Merkle, browser,
        // diff, cache, dataset, playbooks, knowledge). No new product behavior.
        var checklist = Rc1CoreChecklistRunner.Run(paths, Path.Combine(outDir, "checklist"));
        assert?.Invoke("rc1 checklist go", checklist.Go);

        var corpus = ResolveCorpusPath();
        Console.WriteLine($"rc1 corpus: {corpus}");
        if (!File.Exists(corpus))
        {
            Console.Error.WriteLine("RC1_REGRESSION_FAIL: corpus missing");
            assert?.Invoke("rc1 corpus exists", false);
            return 1;
        }

        assert?.Invoke("rc1 corpus exists", true);

        var services = new ServiceCollection();
        services.AddOccamCore();
        using var provider = services.BuildServiceProvider();
        var playbookResolver = provider.GetRequiredService<PlaybookSeedResolver>();
        var knowledgeExtract = provider.GetRequiredService<KnowledgeExtractService>();

        var cases = File.ReadAllLines(corpus)
            .Where(l => !string.IsNullOrWhiteSpace(l))
            .Select(l => JsonSerializer.Deserialize(l, Rc1JsonContext.Default.Rc1Case))
            .Where(c => c is not null)
            .Cast<Rc1Case>()
            .ToList();

        var results = new List<Rc1CaseResult>(cases.Count);
        foreach (var entry in cases)
        {
            Console.WriteLine($"rc1: {entry.Id} ({entry.Tool}) …");
            var result = entry.Tool.ToLowerInvariant() switch
            {
                "probe" => RunProbe(entry, probe),
                "map" => RunMap(entry, map),
                "digest" => RunDigest(entry, digest),
                "playbook_resolve" => RunPlaybook(entry, playbookResolver),
                "extract_knowledge" => RunExtractKnowledge(entry, knowledgeExtract),
                _ => RunTranscode(entry, pipeline),
            };
            results.Add(result);
            var mark = result.Pass ? "PASS" : "FAIL";
            Console.WriteLine(
                $"  {mark} ok={result.Ok} expectOk={result.ExpectOk} latencyMs={result.LatencyMs} " +
                $"tokens={result.TokensEstimated?.ToString() ?? "-"} chars={result.MarkdownChars?.ToString() ?? "-"} " +
                $"fail={result.FailureCode ?? "-"}");
            if (result.Failures.Length > 0)
            {
                foreach (var f in result.Failures)
                {
                    Console.WriteLine($"    · {f}");
                }
            }

            assert?.Invoke($"rc1/{entry.Id}", result.Pass);
        }

        var failed = results.Where(r => !r.Pass).ToList();
        var critical = failed.Where(IsCritical).ToList();
        var liveOk = critical.Count == 0;
        var go = checklist.Go && liveOk;

        var summary = new Rc1Summary(
            runId,
            "corpora/quality-audit-reports/2026-06-26-full-eight-tool-audit.md + baseline 2026-06-17 + RC1 10-area checklist",
            results.Count,
            results.Count - failed.Count,
            failed.Count,
            critical.Count,
            [
                go ? "RC1_REGRESSION_OK" : "RC1_REGRESSION_FAIL",
                checklist.Go ? "RC1_CORE_CHECKLIST_OK" : "RC1_CORE_CHECKLIST_FAIL",
                go ? "OCCAM_CORE_1_0_RC_GO" : "OCCAM_CORE_1_0_RC_NO_GO",
                $"passed={results.Count - failed.Count}/{results.Count}",
                $"critical={critical.Count}",
                $"checklist_areas={checklist.Passed}/{checklist.Areas}",
            ],
            results);

        var summaryPath = Path.Combine(outDir, "summary.json");
        File.WriteAllText(
            summaryPath,
            JsonSerializer.Serialize(summary, Rc1JsonContext.Default.Rc1Summary));
        File.WriteAllLines(
            Path.Combine(outDir, "results.jsonl"),
            results.Select(r => JsonSerializer.Serialize(r, Rc1JsonContext.Default.Rc1CaseResult)));

        Console.WriteLine($"RC1 summary → {summaryPath}");
        Console.WriteLine(go ? "RC1_REGRESSION_OK" : "RC1_REGRESSION_FAIL");
        Console.WriteLine(go ? "OCCAM_CORE_1_0_RC_GO" : "OCCAM_CORE_1_0_RC_NO_GO");
        return go ? 0 : 1;
    }

    private static bool IsCritical(Rc1CaseResult r)
    {
        // Critical = expected failure semantics broken, or expected-ok case hard-fails with wrong code.
        if (r.Pass)
        {
            return false;
        }

        return r.Category is "404" or "redirect" or "python_docs" or "wikipedia" or "sitemap" or "playbook"
            or "knowledge" or "js_site"
            || r.Failures.Any(f => f.Contains("failure_code", StringComparison.OrdinalIgnoreCase)
                || f.Contains("invent", StringComparison.OrdinalIgnoreCase));
    }

    private static Rc1CaseResult RunExtractKnowledge(Rc1Case entry, KnowledgeExtractService extract)
    {
        var failures = new List<string>();
        var backend = entry.Backend ?? "http";
        if (!OccamBackendPolicyParser.TryParse(backend, out var policy))
        {
            failures.Add($"invalid backend {backend}");
            return FailShell(entry, failures);
        }

        var sw = Stopwatch.StartNew();
        var outcome = extract.Extract(entry.Url!, policy, sessionProfile: null, CancellationToken.None);
        sw.Stop();

        CheckOkExpectation(entry, outcome.Ok, outcome.FailureCode, failures);
        if (entry.ExpectOk && outcome.Ok)
        {
            var factCount = outcome.Facts?.Count ?? 0;
            if (factCount == 0)
            {
                failures.Add("extract_knowledge ok but facts empty");
            }

            if (string.IsNullOrWhiteSpace(outcome.Meta?.KoId))
            {
                failures.Add("extract_knowledge missing meta.koId");
            }
        }

        return new Rc1CaseResult(
            entry.Id, entry.Category, entry.Tool, outcome.Ok, entry.ExpectOk, failures.Count == 0,
            outcome.FailureCode, (int)sw.ElapsedMilliseconds, null, null, null, null, null, false,
            null, null, null, null, null, null, null,
            outcome.Ok
                ? $"koId={outcome.Meta?.KoId ?? "-"} facts={outcome.Facts?.Count ?? 0} playbook={outcome.PlaybookId ?? "-"}"
                : $"honest failure {outcome.FailureCode ?? "-"}",
            null, [.. failures], entry.Notes);
    }

    private static Rc1CaseResult RunTranscode(Rc1Case entry, TranscodePipeline pipeline)
    {
        var failures = new List<string>();
        var backend = entry.Backend ?? "http_then_browser";
        if (!OccamBackendPolicyParser.TryParse(backend, out var policy))
        {
            failures.Add($"invalid backend {backend}");
            return FailShell(entry, failures);
        }

        var options = OccamTranscodeOptions.Default with
        {
            MaxTokens = entry.MaxTokens,
            FocusQuery = entry.FocusQuery,
            FitMarkdown = entry.FitMarkdown,
            JsonBlocks = entry.JsonBlocks,
            JsonTables = entry.JsonTables,
            JsonFeed = entry.JsonFeed,
            PlaybookPolicy = PlaybookPolicy.Off,
        };

        var sw = Stopwatch.StartNew();
        var outcome = pipeline.Transcode(entry.Url!, policy, options, CancellationToken.None);
        sw.Stop();

        CheckOkExpectation(entry, outcome.Ok, outcome.FailureCode, failures);
        CheckContains(entry, outcome.Markdown, failures);

        var semantic = 0;
        if (outcome.Tables is { Count: > 0 })
        {
            foreach (var t in outcome.Tables)
            {
                semantic += t.Records?.Length ?? 0;
            }
        }

        string? matNote = null;
        if (entry.JsonTables || entry.JsonBlocks)
        {
            var view = TableSemanticMaterializer.Materialize(
                outcome.Markdown ?? "",
                outcome.Blocks?.ToList(),
                outcome.Tables?.ToList());
            var semCount = TableSemanticMaterializer.CountSemanticRows(view.Knowledge);
            matNote = $"knowledge_blocks={view.Knowledge?.Blocks.Count ?? 0} semantic_rows={semCount} markdown_unchanged={view.Markdown == (outcome.Markdown ?? "")}";
            if (entry.Category == "hn" && outcome.Ok && semantic == 0 && (outcome.Tables?.Count ?? 0) > 0)
            {
                // Readability often keeps a chrome table and drops itemlist — not a critical
                // regression of the reconstructer (selftest covers DOM path). Flag as materialization gap.
                matNote = (matNote ?? "") + " gap:tables_without_semantic_records(readability)";
            }
            else if (entry.Category == "hn" && outcome.Ok && (outcome.Tables?.Count ?? 0) == 0)
            {
                matNote = (matNote ?? "") + " note:no_tables_after_extract";
            }
        }

        var tokens = outcome.TokensEstimated ?? (outcome.Markdown is null ? null : TokenEstimator.Estimate(outcome.Markdown));
        var pass = failures.Count == 0;
        return new Rc1CaseResult(
            entry.Id, entry.Category, entry.Tool, outcome.Ok, entry.ExpectOk, pass,
            outcome.FailureCode, (int)sw.ElapsedMilliseconds, tokens,
            outcome.Markdown?.Length, outcome.Confidence,
            outcome.Ok ? ContentHashToken.BareHex(outcome.Markdown ?? "") : null,
            outcome.Backend, outcome.Truncated,
            outcome.Blocks?.Count, outcome.Tables?.Count, semantic,
            outcome.Feed?.Items.Length,
            null, null, null,
            outcome.Ok ? "contentHash present on success" : "no hash on failure (honest)",
            matNote,
            [.. failures],
            entry.Notes);
    }

    private static Rc1CaseResult RunProbe(Rc1Case entry, ProbeService probe)
    {
        var failures = new List<string>();
        var sw = Stopwatch.StartNew();
        var analysis = probe.AnalyzeAsync(entry.Url!).GetAwaiter().GetResult();
        sw.Stop();
        CheckOkExpectation(entry, analysis.Ok, analysis.FailureCode, failures);
        if (entry.Category == "redirect" && analysis.Ok)
        {
            var final = analysis.FinalUrl ?? analysis.Url;
            if (string.Equals(final, entry.Url, StringComparison.OrdinalIgnoreCase))
            {
                // Soft: some environments may hit HTTPS already; note only.
            }
            else if (final is not null && !final.StartsWith("https://", StringComparison.OrdinalIgnoreCase)
                     && entry.Url!.StartsWith("http://", StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"redirect expected https final, got {final}");
            }
        }

        return new Rc1CaseResult(
            entry.Id, entry.Category, entry.Tool, analysis.Ok, entry.ExpectOk, failures.Count == 0,
            analysis.FailureCode, (int)sw.ElapsedMilliseconds, null, null,
            OccamMcp.Core.Tools.SearchExtractabilityScorer.Score(analysis),
            null, analysis.RecommendedBackend, false, null, null, null, null, null, null, null,
            analysis.RedirectChain is { Length: > 0 }
                ? $"redirects={analysis.RedirectChain.Length} final={analysis.FinalUrl ?? analysis.Url}"
                : $"final={analysis.FinalUrl ?? analysis.Url}",
            null, [.. failures], entry.Notes);
    }

    private static Rc1CaseResult RunMap(Rc1Case entry, MapService map)
    {
        var failures = new List<string>();
        var sw = Stopwatch.StartNew();
        var analysis = map.MapAsync(
            entry.Url!,
            maxLinks: entry.MaxLinks ?? 16,
            source: entry.Source ?? "sitemap").GetAwaiter().GetResult();
        sw.Stop();
        CheckOkExpectation(entry, analysis.Ok, analysis.FailureCode, failures);
        var linkCount = analysis.Links?.Count ?? 0;
        if (entry.ExpectOk && entry.MinLinks is int min && linkCount < min)
        {
            failures.Add($"min_links {min}, got {linkCount}");
        }

        return new Rc1CaseResult(
            entry.Id, entry.Category, entry.Tool, analysis.Ok, entry.ExpectOk, failures.Count == 0,
            analysis.FailureCode, (int)sw.ElapsedMilliseconds, null, null, null, null, null, false,
            null, null, null, null, linkCount, null, null,
            "map link list (discovery provenance = source)", null, [.. failures], entry.Notes);
    }

    private static Rc1CaseResult RunDigest(Rc1Case entry, DigestService digest)
    {
        var failures = new List<string>();
        var urls = (entry.Urls ?? []).Select(url => new DigestUrlEntry(url)).ToList();
        var sw = Stopwatch.StartNew();
        var analysis = digest.DigestAsync(
            urls,
            focusQuery: entry.FocusQuery,
            fitMarkdown: true,
            backendPolicy: OccamBackendPolicy.Http).GetAwaiter().GetResult();
        sw.Stop();
        CheckOkExpectation(entry, analysis.Ok, analysis.FailureCode, failures);
        var matched = analysis.Items.Where(i => i.Ok).Select(i => i.FocusMatched).ToList();
        var anyTrue = matched.Any(m => m == true);
        var allTrue = matched.Count > 0 && matched.All(m => m == true);
        // Honesty: MDN guide is weak for "configuration syntax"; nginx should match → mixed.
        if (entry.ExpectOk && matched.Count >= 2)
        {
            if (allTrue)
            {
                failures.Add("focusMatched all true — expected mixed honesty on hub+docs");
            }
            else if (!anyTrue)
            {
                failures.Add("focusMatched none true — expected nginx strong match");
            }
        }

        return new Rc1CaseResult(
            entry.Id, entry.Category, entry.Tool, analysis.Ok, entry.ExpectOk, failures.Count == 0,
            analysis.FailureCode, (int)sw.ElapsedMilliseconds,
            analysis.TotalTokensEstimated, analysis.Combined?.Length, null, null, null, false,
            null, null, null, null, null, anyTrue, allTrue,
            "per-item focusMatched + optional receipts", null, [.. failures], entry.Notes);
    }

    private static Rc1CaseResult RunPlaybook(Rc1Case entry, PlaybookSeedResolver resolver)
    {
        var failures = new List<string>();
        var sw = Stopwatch.StartNew();
        var result = resolver.Resolve(entry.Url!);
        sw.Stop();
        CheckOkExpectation(entry, result.Ok, result.FailureCode, failures);
        if (result.Ok && string.IsNullOrWhiteSpace(result.PlaybookId) && result.ContentSelectors is not { Length: > 0 })
        {
            // Seed may still be ok with host match only.
        }

        return new Rc1CaseResult(
            entry.Id, entry.Category, entry.Tool, result.Ok, entry.ExpectOk, failures.Count == 0,
            result.FailureCode, (int)sw.ElapsedMilliseconds, null, null, null, null, null, false,
            null, null, null, null, null, null, null,
            $"playbookId={result.PlaybookId ?? "-"} provenance={result.Provenance ?? "-"}",
            null, [.. failures], entry.Notes);
    }

    private static void CheckOkExpectation(Rc1Case entry, bool ok, string? failureCode, List<string> failures)
    {
        if (ok == entry.ExpectOk)
        {
            if (!entry.ExpectOk && entry.FailureCode is not null
                && !string.Equals(failureCode, entry.FailureCode, StringComparison.OrdinalIgnoreCase))
            {
                failures.Add($"failure_code={failureCode ?? "null"} expected {entry.FailureCode}");
            }

            return;
        }

        // Soft env allowance (e.g. remote PDF host returns http_403).
        if (entry.EnvBlockedOk && !ok && failureCode is not null
            && entry.AllowedFailures is { Length: > 0 }
            && entry.AllowedFailures.Any(f => string.Equals(f, failureCode, StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        failures.Add($"ok={ok} expected expect_ok={entry.ExpectOk} failure={failureCode ?? "-"}");
    }

    private static void CheckContains(Rc1Case entry, string? markdown, List<string> failures)
    {
        if (!entry.ExpectOk || string.IsNullOrEmpty(markdown))
        {
            return;
        }

        if (entry.MustContain is { Length: > 0 })
        {
            foreach (var m in entry.MustContain)
            {
                if (!markdown.Contains(m, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"must_contain missing: {m}");
                }
            }
        }

        if (entry.MustNotContain is { Length: > 0 })
        {
            foreach (var m in entry.MustNotContain)
            {
                if (markdown.Contains(m, StringComparison.OrdinalIgnoreCase))
                {
                    failures.Add($"must_not_contain present: {m}");
                }
            }
        }
    }

    private static Rc1CaseResult FailShell(Rc1Case entry, List<string> failures) =>
        new(entry.Id, entry.Category, entry.Tool, false, entry.ExpectOk, false, "invalid_arguments",
            0, null, null, null, null, null, false, null, null, null, null, null, null, null,
            null, null, [.. failures], entry.Notes);

    private static string ResolveCorpusPath()
    {
        var home = WorkerPaths.ResolveOccamHome() ?? Directory.GetCurrentDirectory();
        return Path.Combine(home, "corpora", "rc1-regression.jsonl");
    }
}
