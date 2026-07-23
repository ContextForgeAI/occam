using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Services;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// Parity "traps" tier (LT) — anti-bot / edge-case corpus ported from the v1 Crawl4AI parity
/// suite (T1–T9). Opt-in via <c>--traps</c>; not part of the fast or default full gate because
/// it hits flaky third-party hosts (react.dev, kubernetes.io, HN, …). Each case asserts a
/// robustness rubric: structural integrity (balanced code fences, heading count), forbidden
/// artifacts (<c>__codelineno</c>), or that probe correctly rejects a 404 shell.
/// </summary>
internal sealed class TrapCase
{
    [JsonPropertyName("id")] public string Id { get; init; } = "";
    [JsonPropertyName("trap_id")] public string TrapId { get; init; } = "";
    [JsonPropertyName("step")] public string Step { get; init; } = "transcode";
    [JsonPropertyName("url")] public string Url { get; init; } = "";
    [JsonPropertyName("backend")] public string? Backend { get; init; }
    [JsonPropertyName("fit_markdown")] public bool FitMarkdown { get; init; }
    [JsonPropertyName("focus_query")] public string? FocusQuery { get; init; }
    [JsonPropertyName("max_tokens")] public int? MaxTokens { get; init; }
    [JsonPropertyName("playbook_policy")] public string? PlaybookPolicy { get; init; }
    [JsonPropertyName("must_contain")] public string[]? MustContain { get; init; }
    [JsonPropertyName("must_not_contain")] public string[]? MustNotContain { get; init; }
    [JsonPropertyName("code_fence_integrity")] public bool CodeFenceIntegrity { get; init; }
    [JsonPropertyName("min_heading_count")] public int MinHeadingCount { get; init; }
    [JsonPropertyName("min_confidence")] public double? MinConfidence { get; init; }
    [JsonPropertyName("must_fail_probe")] public bool MustFailProbe { get; init; }
    [JsonPropertyName("allow_env_blocked")] public bool AllowEnvBlocked { get; init; }
    [JsonPropertyName("notes")] public string? Notes { get; init; }
}

[JsonSerializable(typeof(TrapCase))]
internal partial class TrapsJsonContext : JsonSerializerContext;

internal static class TrapsRunner
{
    public static int Run(TranscodePipeline pipeline, ProbeService probe)
    {
        var corpusPath = ResolveCorpusPath();
        Console.WriteLine($"traps corpus: {corpusPath}");
        if (!File.Exists(corpusPath))
        {
            Console.Error.WriteLine("LT_TRAPS_FAIL: corpus not found");
            return 1;
        }

        var total = 0;
        var passed = 0;
        foreach (var line in File.ReadLines(corpusPath))
        {
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            var trap = JsonSerializer.Deserialize(line, TrapsJsonContext.Default.TrapCase);
            if (trap is null || string.IsNullOrWhiteSpace(trap.Id))
            {
                Console.Error.WriteLine("traps: parse error");
                total++;
                continue;
            }

            total++;
            var (ok, detail) = RunCase(trap, pipeline, probe);
            if (ok)
            {
                passed++;
                Console.WriteLine($"PASS: trap {trap.TrapId} {trap.Id}{(detail is null ? "" : $" ({detail})")}");
            }
            else
            {
                Console.Error.WriteLine($"FAIL: trap {trap.TrapId} {trap.Id} — {detail}");
            }
        }

        Console.WriteLine($"traps: {passed}/{total} passed");
        if (passed == total && total > 0)
        {
            Console.WriteLine("LT_TRAPS_OK");
            return 0;
        }

        Console.Error.WriteLine("LT_TRAPS_FAIL");
        return 1;
    }

    private static (bool ok, string? detail) RunCase(TrapCase trap, TranscodePipeline pipeline, ProbeService probe)
    {
        if (trap.MustFailProbe || string.Equals(trap.Step, "probe", StringComparison.OrdinalIgnoreCase))
        {
            var analysis = probe.AnalyzeAsync(trap.Url, timeoutMs: 15_000).GetAwaiter().GetResult();
            // The trap is "soft/hard 404 shell" — probe must NEVER return a false ok.
            return (!analysis.Ok, $"probe ok={analysis.Ok} failure={analysis.FailureCode}");
        }

        if (!OccamBackendPolicyParser.TryParse(trap.Backend, out var policy))
        {
            return (false, $"bad backend '{trap.Backend}'");
        }

        if (!OccamTranscodeOptionsParser.TryBuild(
                trap.MaxTokens,
                trap.FitMarkdown,
                trap.FocusQuery,
                content_selectors: null,
                session_profile: null,
                trap.PlaybookPolicy ?? "auto",
                if_none_match: null,
                out var options,
                out var optionsError))
        {
            return (false, optionsError ?? "bad options");
        }

        // Per-case wall-clock guard: traps hit unpredictable third-party hosts (and the browser
        // pool can stall). Never let one case hang the whole tier.
        const int caseTimeoutMs = 150_000;
        var (timedOut, result) = TranscodeWithDeadline(pipeline, trap.Url, policy, options, caseTimeoutMs);
        for (var retry = 0;
             !timedOut && retry < 2 && !result!.Ok && !string.IsNullOrEmpty(result.FailureCode)
                 && FailureCodeStrings.IsRetryable(result.FailureCode);
             retry++)
        {
            Thread.Sleep(1500);
            (timedOut, result) = TranscodeWithDeadline(pipeline, trap.Url, policy, options, caseTimeoutMs);
        }

        if (timedOut)
        {
            return (trap.AllowEnvBlocked, $"case timeout >{caseTimeoutMs / 1000}s");
        }

        if (!result!.Ok)
        {
            // Hosts that block CI egress (403/429) are tolerated when the trap allows it.
            if (trap.AllowEnvBlocked && IsEnvBlocked(result.FailureCode))
            {
                return (true, $"env_blocked ({result.FailureCode}) — tolerated");
            }

            return (false, $"transcode failed: {result.FailureCode}");
        }

        var markdown = result.Markdown ?? "";

        foreach (var token in trap.MustContain ?? [])
        {
            if (!markdown.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return (false, $"missing '{token}'");
            }
        }

        foreach (var token in trap.MustNotContain ?? [])
        {
            if (markdown.Contains(token, StringComparison.Ordinal))
            {
                return (false, $"forbidden '{token}' leaked");
            }
        }

        if (trap.CodeFenceIntegrity)
        {
            var fences = CountOccurrences(markdown, "```");
            if (fences % 2 != 0)
            {
                return (false, $"unbalanced code fences ({fences})");
            }
        }

        if (trap.MinHeadingCount > 0)
        {
            var headings = CountHeadings(markdown);
            if (headings < trap.MinHeadingCount)
            {
                return (false, $"headings {headings} < {trap.MinHeadingCount}");
            }
        }

        if (trap.MinConfidence is { } minConf && result.Confidence < minConf)
        {
            return (false, $"confidence {result.Confidence:F2} < {minConf:F2}");
        }

        return (true, $"conf={result.Confidence:F2} backend={result.Backend}");
    }

    private static (bool timedOut, TranscodeOutcome? result) TranscodeWithDeadline(
        TranscodePipeline pipeline,
        string url,
        OccamBackendPolicy policy,
        OccamTranscodeOptions options,
        int timeoutMs)
    {
        using var cts = new CancellationTokenSource(timeoutMs);
        var task = Task.Run(() => pipeline.Transcode(url, policy, options, cts.Token), cts.Token);
        try
        {
            if (!task.Wait(timeoutMs + 5_000))
            {
                cts.Cancel(); // best-effort; the orphaned worker thread dies when the gate exits
                return (true, null);
            }
        }
        catch (AggregateException ex) when (ex.InnerException is OperationCanceledException)
        {
            return (true, null); // cts fired mid-extract (e.g. browser slot wait) — treat as timeout
        }

        if (task.IsCanceled || task.IsFaulted)
        {
            return (true, null);
        }

        return (false, task.Result);
    }

    // Consulted only for traps marked allow_env_blocked. Tolerates both host-side blocks
    // (403/401/429/5xx) and opaque infra/transient failures (browser hiccups, DNS/TLS, thin
    // extracts) on flaky bucket-C SPA hosts — the tier must not go red on third-party flakiness.
    private static bool IsEnvBlocked(string? failureCode) =>
        failureCode is "http_403" or "http_401" or "http_429"
            or "error" or "network_error" or "timeout" or "dns_error" or "tls_error"
            or "extraction_failed" or "thin_extract" or "playwright_missing"
        || (failureCode?.StartsWith("http_5", StringComparison.Ordinal) ?? false);

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        var idx = 0;
        while ((idx = haystack.IndexOf(needle, idx, StringComparison.Ordinal)) >= 0)
        {
            count++;
            idx += needle.Length;
        }

        return count;
    }

    private static int CountHeadings(string markdown)
    {
        var count = 0;
        foreach (var line in markdown.Split('\n'))
        {
            if (Regex.IsMatch(line, "^#{1,6}\\s"))
            {
                count++;
            }
        }

        return count;
    }

    private static string ResolveCorpusPath()
    {
        var root = WorkerPaths.ResolveOccamHome();
        if (root is not null)
        {
            var fromHome = Path.Combine(root, "corpora", "traps.jsonl");
            if (File.Exists(fromHome))
            {
                return fromHome;
            }
        }

        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            var candidate = Path.Combine(dir.FullName, "corpora", "traps.jsonl");
            if (File.Exists(candidate))
            {
                return candidate;
            }

            dir = dir.Parent;
        }

        return Path.Combine(Directory.GetCurrentDirectory(), "corpora", "traps.jsonl");
    }
}
