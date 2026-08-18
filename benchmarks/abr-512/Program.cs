using System.Diagnostics;
using System.Text.Json;
using OccamMcp.Core.Access;
using OccamMcp.Core.PostProcessors;
using OccamMcp.Core.Workers;

namespace OccamMcp.Abr512;

/// <summary>
/// Isolated ABR-512 Slice 1 harness. Local fixtures only — no network, no host allowlists.
/// Not part of run-l0-fast or the merge gate.
///
///   dotnet run --project benchmarks/abr-512 -- --characterize
///     Documents current RED/CTRL without failing the process on known Slice 1 holes.
///   dotnet run --project benchmarks/abr-512 -- --desired
///     Asserts the Slice 1 GREEN contract (fails until production lands).
/// </summary>
internal static class Program
{
    private static int _failed;

    public static int Main(string[] args)
    {
        var desired = args.Any(a => string.Equals(a, "--desired", StringComparison.OrdinalIgnoreCase));
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
        var healthy = !q.IsBadExtraction && q.Verdict == "short_quality";
        Record("A1", healthy, $"verdict={q.Verdict} bad={q.IsBadExtraction} score={q.Score} chars={q.TotalChars}");
        if (desired)
        {
            Assert("A1 not error-shell class", healthy);
        }
    }

    private static void CheckA2(string markdown, bool desired)
    {
        var q = ExtractQualityEvaluator.Evaluate(markdown);
        var falseOpen = !q.IsBadExtraction && q.Verdict == "short_quality";
        if (desired)
        {
            Assert("A2 is bad extraction", q.IsBadExtraction);
            Assert("A2 is not short_quality", q.Verdict != "short_quality");
        }
        else
        {
            Console.WriteLine(falseOpen
                ? $"RED  A2  verdict={q.Verdict} bad={q.IsBadExtraction} score={q.Score}"
                : $"NOTE A2  verdict={q.Verdict} bad={q.IsBadExtraction} score={q.Score}");
        }
    }

    private static void CheckA3Sim(string markdown, bool desired)
    {
        var worker = new WorkerAccessEvidenceInfo { HasUsableContent = true };
        var evidence = AccessEvidenceAdapters.FromTranscode(
            worker, markdown, "https://abr.local/error", "https://abr.local/error", 200);
        var access = AccessClassifier.Classify(evidence);
        var q = ExtractQualityEvaluator.Evaluate(markdown);
        var falseOpen = access.Disposition == AccessDisposition.Open && q.Verdict == "short_quality";
        if (desired)
        {
            Assert("A3-sim access unknown", access.Disposition == AccessDisposition.Unknown);
            Assert("A3-sim not usable", !evidence.HasUsableContent);
            Assert("A3-sim evidence includes error_shell", access.EvidenceCodes.Contains("error_shell", StringComparer.Ordinal));
            Assert("A3-sim not short_quality", q.Verdict != "short_quality");
        }
        else
        {
            Console.WriteLine(falseOpen
                ? $"RED  A3-sim  access={access.Disposition} verdict={q.Verdict} usable={evidence.HasUsableContent}"
                : $"NOTE A3-sim  access={access.Disposition} verdict={q.Verdict} usable={evidence.HasUsableContent}");
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
        var access = payload.Value.TryGetProperty("access", out var accessEl) ? accessEl : default;
        var usable = access.ValueKind == JsonValueKind.Object
            && access.TryGetProperty("has_usable_content", out var usableEl)
            && usableEl.ValueKind is JsonValueKind.True;
        var tiny = markdown.Length > 0 && markdown.Length < 200;
        var workerRed = tiny && usable;

        if (desired)
        {
            Assert("A3-worker extract tiny", tiny);
            Assert($"A3-worker extract not ~900 chars ({markdown.Length})", markdown.Length < 200);
            Assert("A3-worker has_usable_content false", !usable);
        }
        else
        {
            Console.WriteLine(workerRed
                ? $"RED  A3-worker extractChars={markdown.Length} has_usable_content={usable}"
                : $"NOTE A3-worker extractChars={markdown.Length} has_usable_content={usable}");
        }
    }

    private static void CheckA4(string markdown, bool desired)
    {
        var q = ExtractQualityEvaluator.Evaluate(markdown);
        var healthy = !q.IsBadExtraction && markdown.Length >= 800;
        Record("A4", healthy, $"verdict={q.Verdict} bad={q.IsBadExtraction} chars={markdown.Length}");
        if (desired)
        {
            Assert("A4 stays healthy", healthy);
            Assert("A4 not short_quality-only reject", q.Verdict is "rich" or "noisy" or "short_quality");
        }
    }

    private static void CheckCascadeInvariant(string usableMarkdown, string errorShellMarkdown, bool desired)
    {
        var usableOk = !ExtractQualityEvaluator.LooksLikeThinExtract(usableMarkdown);
        var errorUnusable = ExtractQualityEvaluator.LooksLikeThinExtract(errorShellMarkdown);
        if (desired)
        {
            Assert("cascade usable HTTP markdown remains usable", usableOk);
            Assert("cascade error-shell markdown is unusable", errorUnusable);
        }
        else
        {
            Console.WriteLine($"NOTE cascade usableOk={usableOk} errorUnusable={errorUnusable}");
        }
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
