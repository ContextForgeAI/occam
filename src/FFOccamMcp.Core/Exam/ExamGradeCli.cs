using System.Buffers;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Transport;

namespace OccamMcp.Core.Exam;

/// <summary>
/// Grades a harness submission JSON and emits the tier → profile recommendation operators need
/// before the next MCP process start (ADR-0016: surface is fixed at DI registration).
/// </summary>
public static class ExamGradeCli
{
    public const string GradeOkMarker = "EXAM_GRADE_OK";

    public static int Grade(string[] args)
    {
        string? submissionPath = null;
        string? outPath = null;
        string? envPath = null;
        for (var i = 0; i < args.Length; i++)
        {
            if (args[i] is "--submission" or "-s" && i + 1 < args.Length)
            {
                submissionPath = args[++i];
            }
            else if (args[i] is "--out" or "-o" && i + 1 < args.Length)
            {
                outPath = args[++i];
            }
            else if (args[i] == "--write-env" && i + 1 < args.Length)
            {
                envPath = args[++i];
            }
            else if (args[i].StartsWith("--submission=", StringComparison.Ordinal))
            {
                submissionPath = args[i]["--submission=".Length..];
            }
            else if (args[i].StartsWith("--out=", StringComparison.Ordinal))
            {
                outPath = args[i]["--out=".Length..];
            }
        }

        if (string.IsNullOrWhiteSpace(submissionPath))
        {
            Console.Error.WriteLine("usage: occam exam grade --submission path.json [--out result.json] [--write-env profile.env]");
            return 2;
        }

        if (!File.Exists(submissionPath))
        {
            Console.Error.WriteLine($"exam grade: submission not found: {submissionPath}");
            return 1;
        }

        var json = File.ReadAllText(submissionPath);
        if (!ExamSubmissionParser.TryParse(json, out var document, out var submission, out var error))
        {
            Console.Error.WriteLine($"exam grade: {error}");
            return 1;
        }

        var now = DateTimeOffset.UtcNow;
        var result = ExamGrader.Grade(submission, now);
        var subject = ExamSubject.Create(document.ClientInfo, document.ModelHint, document.SessionId);
        var profile = ExamScoring.ProfileFor(result.Tier);
        var toolCount = ExamScoring.SurfaceSizeFor(result.Tier);
        AgentTier? selfReport = string.IsNullOrWhiteSpace(document.SelfReportTier)
            ? null
            : ExamWire.ParseTier(document.SelfReportTier);

        var payload = EmitGradeResult(subject, result, profile, toolCount, selfReport, document.ModelHint);
        WriteText(outPath, payload, toStdoutIfNull: true);

        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var env = $"OCCAM_PROFILE={profile}\n";
            WriteText(envPath, env, toStdoutIfNull: false);
            Console.Error.WriteLine($"[exam.grade] wrote env {envPath}");
        }

        Console.Error.WriteLine(
            $"[exam.grade] score={result.Score} tier={result.TierWire} profile={profile} tools={toolCount}");
        // Keep stdout parseable when the grade envelope is streamed there.
        if (string.IsNullOrWhiteSpace(outPath))
        {
            Console.Error.WriteLine(GradeOkMarker);
        }
        else
        {
            Console.Out.WriteLine(GradeOkMarker);
        }

        return 0;
    }

    public static string EmitGradeResult(
        ExamSubject subject,
        ExamResult result,
        string profile,
        int toolCount,
        AgentTier? selfReportTier,
        string? modelHint)
    {
        var buffer = new ArrayBufferWriter<byte>(2_048);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteString("protocol", "occam-exam-grade-v1");
            writer.WriteStartObject("subject");
            writer.WriteString("clientInfo", subject.ClientInfo);
            writer.WriteString("modelHint", subject.ModelHint);
            writer.WriteString("sessionId", subject.SessionId);
            writer.WriteEndObject();
            writer.WriteNumber("score", result.Score);
            writer.WriteNumber("maxScore", ExamResult.MaxScore);
            writer.WriteString("tier", result.TierWire);
            writer.WriteString("profile", profile);
            writer.WriteNumber("toolCount", toolCount);
            writer.WriteString("source", result.Source);
            writer.WriteString("gradedAt", result.GradedAt.ToString("O"));
            writer.WriteString("recommendedEnv", $"OCCAM_PROFILE={profile}");

            if (selfReportTier is { } reported)
            {
                writer.WriteString("selfReportTier", ExamWire.ToWire(reported));
                writer.WriteBoolean("selfReportMatchesExam", reported == result.Tier);
            }

            var staticTier = StaticModelMap.Map(modelHint);
            writer.WriteString("staticMapTier", ExamWire.ToWire(staticTier));
            writer.WriteBoolean("staticMapMatchesExam", staticTier == result.Tier);

            writer.WriteStartArray("outcomes");
            foreach (var outcome in result.Outcomes)
            {
                writer.WriteStartObject();
                writer.WriteString("task", ExamWire.ToWire(outcome.Task));
                writer.WriteBoolean("passed", outcome.Passed);
                writer.WriteString("detail", outcome.Detail);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static void WriteText(string? path, string content, bool toStdoutIfNull)
    {
        if (string.IsNullOrWhiteSpace(path))
        {
            if (toStdoutIfNull)
            {
                Console.Out.Write(content);
            }

            return;
        }

        var directory = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, content, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        Console.Error.WriteLine($"[exam.grade] wrote {path}");
    }
}

/// <summary>
/// H3 baseline: static map from model-name hint → tier. Deliberately coarse and documented so the
/// experiment can refute behavioural scoring against something cheap and stable.
/// </summary>
public static class StaticModelMap
{
    public static AgentTier Map(string? modelHint)
    {
        if (string.IsNullOrWhiteSpace(modelHint))
        {
            return AgentTier.Medium;
        }

        var m = modelHint.Trim().ToLowerInvariant();

        // Weak: small / deliberately limited models.
        if (m.Contains("7b", StringComparison.Ordinal)
            || m.Contains("8b", StringComparison.Ordinal)
            || m.Contains("mini", StringComparison.Ordinal)
            || m.Contains("tiny", StringComparison.Ordinal)
            || m.Contains("mock-weak", StringComparison.Ordinal))
        {
            return AgentTier.Weak;
        }

        // Strong: frontier-ish names. This table is the baseline H3 must beat — not a product claim.
        if (m.Contains("opus", StringComparison.Ordinal)
            || m.Contains("gpt-5", StringComparison.Ordinal)
            || m.Contains("gpt-4", StringComparison.Ordinal)
            || m.Contains("sonnet", StringComparison.Ordinal)
            || m.Contains("mock-strong", StringComparison.Ordinal))
        {
            return AgentTier.Strong;
        }

        if (m.Contains("mock-medium", StringComparison.Ordinal))
        {
            return AgentTier.Medium;
        }

        return AgentTier.Medium;
    }
}
