using System.Diagnostics;
using System.Text.Json;
using OccamMcp.Core.Access;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.Abr512;

/// <summary>
/// Isolated ABR-512 Slice 1 harness. Local fixtures only — no network, no host allowlists.
/// Not part of run-l0-fast or the merge gate.
///
///   dotnet run --project benchmarks/abr-512
///   dotnet run --project benchmarks/abr-512 -- --desired
///     Asserts the Slice 1 GREEN contract.
///   dotnet run --project benchmarks/abr-512 -- --characterize
///     Documents current observations without the desired assertions.
/// </summary>
internal static class Program
{
    private static int _failed;

    public static int Main(string[] args)
    {
        var characterize = args.Any(a => string.Equals(a, "--characterize", StringComparison.OrdinalIgnoreCase));
        var desired = !characterize;
        var fixtures = ResolveFixtures();
        Console.WriteLine($"ABR-512 fixtures: {fixtures}");
        Console.WriteLine(desired ? "mode: desired" : "mode: characterize");

        var shortLegit = File.ReadAllText(Path.Combine(fixtures, "short-legit.md"));
        var errorShell = File.ReadAllText(Path.Combine(fixtures, "error-shell.md"));
        var troubleshooting = File.ReadAllText(Path.Combine(fixtures, "error-shell-troubleshooting.md"));

        CheckA1(shortLegit, desired);
        CheckA2(errorShell, desired);
        CheckA3Sim(errorShell, desired);
        CheckA3Worker(fixtures, desired);
        CheckA4(troubleshooting, desired);
        CheckCascadeInvariant(shortLegit, errorShell, desired);
        CheckR1R2(desired);

        if (_failed > 0)
        {
            Console.WriteLine($"ABR512_FAIL count={_failed}");
            return 1;
        }

        Console.WriteLine(desired ? "ABR512_DESIRED_OK" : "ABR512_CHARACTERIZE_OK");
        return 0;
    }

    private static void CheckA1(string markdown, bool desired)
    {
        var q = ExtractQualityEvaluator.Evaluate(markdown);
        var healthy = !q.IsBadExtraction
            && q.Verdict == "short_quality"
            && !ExtractQualityEvaluator.LooksLikeErrorShell(markdown);
        Record("A1", healthy, $"verdict={q.Verdict} bad={q.IsBadExtraction} score={q.Score} chars={q.TotalChars}");
        if (desired)
        {
            Assert("A1 not render_error class", healthy);
            Assert("A1 not IsBadExtraction due to length", !q.IsBadExtraction);
        }
    }

    private static void CheckA2(string markdown, bool desired)
    {
        var q = ExtractQualityEvaluator.Evaluate(markdown);
        var outcome = RunHostTerminal(markdown, workerUsable: false, backend: "http");
        if (desired)
        {
            Assert("A2 LooksLikeErrorShell", ExtractQualityEvaluator.LooksLikeErrorShell(markdown));
            Assert("A2 is bad extraction", q.IsBadExtraction);
            Assert("A2 is not short_quality", q.Verdict != "short_quality");
            AssertTerminal("A2", outcome);
        }
        else
        {
            Console.WriteLine($"NOTE A2 verdict={q.Verdict} bad={q.IsBadExtraction} fail={outcome.FailureCode}");
        }
    }

    private static void CheckA3Sim(string markdown, bool desired)
    {
        var worker = new WorkerAccessEvidenceInfo { HasUsableContent = true };
        var evidence = AccessEvidenceAdapters.FromTranscode(
            worker, markdown, "https://abr.local/error", "https://abr.local/error", 200);
        var access = AccessClassifier.Classify(evidence);
        var outcome = RunHostTerminal(markdown, workerUsable: true, backend: "http");
        if (desired)
        {
            Assert("A3-sim access unknown", access.Disposition == AccessDisposition.Unknown);
            Assert("A3-sim HasUsableContent false", !evidence.HasUsableContent);
            Assert("A3-sim evidence includes error_shell", access.EvidenceCodes.Contains("error_shell", StringComparer.Ordinal));
            AssertTerminal("A3-sim", outcome);
        }
        else
        {
            Console.WriteLine($"NOTE A3-sim access={access.Disposition} usable={evidence.HasUsableContent} fail={outcome.FailureCode}");
        }
    }

    private static void CheckA3Worker(string fixtures, bool desired)
    {
        var html = Path.Combine(fixtures, "error-shell-large-dom.html");
        var payload = RunHttpExtract(html);
        if (payload is null)
        {
            Assert("A3-worker produced JSON", false);
            return;
        }

        var markdown = payload.Value.TryGetProperty("markdown", out var mdEl) ? mdEl.GetString() ?? "" : "";
        var accessEl = payload.Value.TryGetProperty("access", out var a) ? a : default;
        var usable = accessEl.ValueKind == JsonValueKind.Object
            && accessEl.TryGetProperty("has_usable_content", out var usableEl)
            && usableEl.ValueKind is JsonValueKind.True;
        var errorShellFlag = accessEl.ValueKind == JsonValueKind.Object
            && accessEl.TryGetProperty("error_shell", out var es)
            && es.ValueKind is JsonValueKind.True;
        var tiny = markdown.Length > 0 && markdown.Length < 200;
        var outcome = RunHostTerminal(
            markdown,
            workerUsable: usable,
            backend: "http",
            workerErrorShell: errorShellFlag);

        Console.WriteLine($"A3-worker extractChars={markdown.Length} has_usable_content={usable} error_shell={errorShellFlag}");
        if (desired)
        {
            Assert("A3-worker extract tiny", tiny);
            Assert("A3-worker extract not ~900 chars", markdown.Length < 200);
            Assert("A3-worker has_usable_content false", !usable);
            AssertTerminal("A3-worker host", outcome);
        }
    }

    private static void CheckA4(string markdown, bool desired)
    {
        var q = ExtractQualityEvaluator.Evaluate(markdown);
        var healthy = !q.IsBadExtraction
            && markdown.Length >= 800
            && !ExtractQualityEvaluator.LooksLikeErrorShell(markdown);
        Record("A4", healthy, $"verdict={q.Verdict} bad={q.IsBadExtraction} chars={markdown.Length}");
        if (desired)
        {
            Assert("A4 stays healthy", healthy);
            var outcome = RunHostTerminal(markdown, workerUsable: true, backend: "http");
            Assert("A4 not render_error", outcome.Ok && outcome.FailureCode != "render_error");
        }
    }

    private static void CheckCascadeInvariant(string usableMarkdown, string errorShellMarkdown, bool desired)
    {
        var httpUsable = new ExtractRunResult(true, usableMarkdown, "http", null, 10, "https://abr.local/docs", false, 200);
        var browserError = new ExtractRunResult(true, errorShellMarkdown, "browser", null, 20, "https://abr.local/docs", false, 200);
        var usableOk = !ExtractQualityEvaluator.LooksLikeThinExtract(usableMarkdown)
            && !ExtractQualityEvaluator.LooksLikeErrorShell(usableMarkdown);
        if (desired)
        {
            Assert("cascade usable HTTP markdown remains usable", usableOk);
            Assert("cascade error-shell markdown is unusable", ExtractQualityEvaluator.LooksLikeErrorShell(errorShellMarkdown));
            Assert("cascade usable HTTP would short-circuit browser error", usableOk);
            Assert("cascade browser error shell is not a successful extract", ExtractQualityEvaluator.LooksLikeErrorShell(errorShellMarkdown));
            _ = httpUsable;
            _ = browserError;
        }
        else
        {
            Console.WriteLine($"NOTE cascade usableOk={usableOk}");
        }
    }

    private static void CheckR1R2(bool desired)
    {
        const string url = "https://abr.local/error";
        var r1Json = OccamTranscodeTool.SerializePipelineFailureForTests(
            url,
            new TranscodeOutcome(
                false,
                File.ReadAllText(Path.Combine(ResolveFixtures(), "error-shell.md")),
                url,
                "http",
                "render_error",
                "error shell",
                StatusCode: 200,
                Recovery:
                [
                    new TranscodeAttempt("http", true, 12, true, false, "render_error"),
                    new TranscodeAttempt("browser", false, 30, false, false, "timeout", "render_error"),
                ]),
            [
                new OccamTranscodeRecoveryInfo("http", true, 12, true, false, "render_error"),
                new OccamTranscodeRecoveryInfo("browser", false, 30, false, false, "timeout", "render_error"),
            ]);
        var r2Json = OccamTranscodeTool.SerializePipelineFailureForTests(
            url,
            new TranscodeOutcome(
                false,
                "Short.",
                url,
                "http",
                "thin_extract",
                "thin",
                StatusCode: 200,
                Recovery:
                [
                    new TranscodeAttempt("http", true, 10, true, false, "thin_extract"),
                    new TranscodeAttempt("browser", false, 25, false, false, "timeout", "thin_extract"),
                ]),
            [
                new OccamTranscodeRecoveryInfo("http", true, 10, true, false, "thin_extract"),
                new OccamTranscodeRecoveryInfo("browser", false, 25, false, false, "timeout", "thin_extract"),
            ]);
        var httpOnlyJson = OccamTranscodeTool.SerializePipelineFailureForTests(
            url,
            new TranscodeOutcome(false, null, url, "http", "render_error", "error shell", StatusCode: 200));
        var directBrowserJson = OccamTranscodeTool.SerializePipelineFailureForTests(
            url,
            new TranscodeOutcome(false, null, url, "browser", "render_error", "error shell", StatusCode: 200));

        if (!desired)
        {
            Console.WriteLine($"NOTE R1 retryable={EnvelopeRetryable(r1Json)} stop={EnvelopeHas(r1Json, "stop")} retryBrowser={EnvelopeHas(r1Json, "retry_transcode", "browser")}");
            Console.WriteLine($"NOTE R2 retryable={EnvelopeRetryable(r2Json)} stop={EnvelopeHas(r2Json, "stop")} retryBrowser={EnvelopeHas(r2Json, "retry_transcode", "browser")}");
            return;
        }

        AssertStopNoBrowserRetry("R1", r1Json);
        AssertStopNoBrowserRetry("R2", r2Json);
        Assert("HTTP-only render_error retryable", EnvelopeRetryable(httpOnlyJson) == true);
        Assert("HTTP-only render_error retries browser", EnvelopeHas(httpOnlyJson, "retry_transcode", "browser"));
        AssertStopNoBrowserRetry("direct-browser-render_error", directBrowserJson);
        Assert(
            "usable HTTP short-circuit helper",
            !TranscodeAttemptHistory.BrowserWasAttempted("http", ["http"]));
        Assert(
            "403 outranks render_error",
            FailureRanking.Informativeness("http_403") > FailureRanking.Informativeness("render_error")
            && FailureRanking.Informativeness("http_404") > FailureRanking.Informativeness("render_error"));
    }

    private static void AssertStopNoBrowserRetry(string id, string json)
    {
        Assert($"{id} retryable is not true", EnvelopeRetryable(json) != true);
        Assert($"{id} stop", EnvelopeHas(json, "stop"));
        Assert($"{id} no retry_transcode browser", !EnvelopeHas(json, "retry_transcode", "browser"));
    }

    private static bool? EnvelopeRetryable(string json)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("failure", out var failure)
            || !failure.TryGetProperty("retryable", out var retryable)
            || retryable.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return null;
        }

        return retryable.ValueKind == JsonValueKind.True;
    }

    private static bool EnvelopeHas(string json, string action, string? parameterNeedle = null)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("agentMeta", out var meta)
            || !meta.TryGetProperty("decisions", out var decisions)
            || decisions.ValueKind != JsonValueKind.Array)
        {
            return false;
        }

        foreach (var decision in decisions.EnumerateArray())
        {
            if (decision.GetProperty("action").GetString() != action)
            {
                continue;
            }

            if (parameterNeedle is null)
            {
                return true;
            }

            if (decision.TryGetProperty("parameter", out var parameter)
                && parameter.GetString()?.Contains(parameterNeedle, StringComparison.Ordinal) == true)
            {
                return true;
            }
        }

        return false;
    }

    private static TranscodeOutcome RunHostTerminal(
        string markdown,
        bool workerUsable,
        string backend,
        bool workerErrorShell = false)
    {
        var worker = new WorkerAccessEvidenceInfo
        {
            HasUsableContent = workerUsable,
            ErrorShell = workerErrorShell,
        };
        var outcome = new TranscodeOutcome(
            true,
            markdown,
            "https://abr.local/error",
            backend,
            null,
            null,
            StatusCode: 200,
            Access: worker);
        var ctx = new TranscodeContext(
            "https://abr.local/error",
            OccamBackendPolicy.HttpThenBrowser,
            OccamTranscodeOptions.Default);
        outcome = new RequiresLoginPostProcessor().Process(outcome, ctx);
        return new ThinExtractPostProcessor().Process(outcome, ctx);
    }

    private static void AssertTerminal(string label, TranscodeOutcome outcome)
    {
        Assert($"{label} ok=false", !outcome.Ok);
        Assert($"{label} failure.render_error", outcome.FailureCode == "render_error");
        Assert($"{label} access unknown", outcome.AccessAssessment?.Disposition == AccessDisposition.Unknown);
        Assert($"{label} confidence 0", outcome.Confidence == 0);
        Assert($"{label} not open/short_quality", outcome.Quality?.Verdict != "short_quality");
        Assert(
            $"{label} evidence error_shell",
            outcome.AccessAssessment?.EvidenceCodes.Contains("error_shell", StringComparer.Ordinal) == true);
    }

    private static JsonElement? RunHttpExtract(string htmlFile)
    {
        var repo = ResolveRepoRoot();
        var script = Path.Combine(repo, "workers", "http-extract", "extract.mjs");
        if (!File.Exists(script))
        {
            Console.WriteLine($"A3-worker missing {script}");
            return null;
        }

        var url = "https://abr.local/error-shell";
        var psi = new ProcessStartInfo
        {
            FileName = "node",
            ArgumentList = { script, url, $"--html-file={htmlFile}", $"--final-url={url}" },
            WorkingDirectory = repo,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
        };
        using var proc = Process.Start(psi);
        if (proc is null)
        {
            return null;
        }

        var stdout = proc.StandardOutput.ReadToEnd();
        proc.WaitForExit(60_000);
        try
        {
            return JsonSerializer.Deserialize<JsonElement>(stdout.Trim());
        }
        catch (JsonException)
        {
            Console.WriteLine($"A3-worker JSON parse failed stdout={stdout[..Math.Min(200, stdout.Length)]}");
            return null;
        }
    }

    private static void Record(string id, bool ok, string detail)
    {
        Console.WriteLine($"{(ok ? "CTRL" : "FAIL")} {id}  {detail}");
        if (!ok)
        {
            _failed++;
        }
    }

    private static void Assert(string name, bool ok)
    {
        Console.WriteLine($"{(ok ? "PASS" : "FAIL")} {name}");
        if (!ok)
        {
            _failed++;
        }
    }

    private static string ResolveFixtures()
    {
        var candidates = new[]
        {
            Path.Combine(AppContext.BaseDirectory, "fixtures"),
            Path.Combine(Directory.GetCurrentDirectory(), "benchmarks", "abr-512", "fixtures"),
            Path.Combine(Directory.GetCurrentDirectory(), "fixtures"),
        };
        foreach (var c in candidates)
        {
            if (File.Exists(Path.Combine(c, "error-shell.md")))
            {
                return c;
            }
        }

        throw new DirectoryNotFoundException("ABR-512 fixtures not found.");
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "workers", "http-extract", "extract.mjs")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        return Directory.GetCurrentDirectory();
    }
}
