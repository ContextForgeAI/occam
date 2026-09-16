using System.Buffers;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Canary;
using OccamMcp.Core.Time;
using OccamMcp.Core.Transport;

namespace OccamMcp.Core.Exam;

/// <summary>
/// The <c>occam exam</c> verb family: publish the task catalogue, and prove the scoring, tiering,
/// caching and rolling-adjustment rules.
/// </summary>
/// <remarks>
/// Like the canary verbs, these need no worker tree, no browser and no network, so the engine can
/// be verified from the published binary on a bare machine.
/// </remarks>
public static class ExamCliVerbs
{
    /// <summary>Marker printed by a passing <c>exam selftest</c>.</summary>
    public const string SelftestOkMarker = "EXAM_SELFTEST_OK";

    /// <summary>
    /// Dispatches an <c>exam</c> sub-verb.
    /// </summary>
    /// <param name="args">Full process arguments, starting at the <c>exam</c> token.</param>
    /// <param name="exitCode">0 on success, 1 on a failed check, 2 on usage error.</param>
    /// <returns><c>true</c> when the verb was handled here.</returns>
    public static bool TryRun(string[] args, out int exitCode)
    {
        exitCode = 0;
        if (args.Length == 0 || !string.Equals(args[0], "exam", StringComparison.Ordinal))
        {
            return false;
        }

        switch (args.Length >= 2 ? args[1] : "help")
        {
            case "selftest":
                exitCode = Selftest();
                return true;
            case "tasks":
                exitCode = WriteTasks(args);
                return true;
            case "grade":
                exitCode = ExamGradeCli.Grade(args);
                return true;
            case "harness-selftest":
                exitCode = HarnessSelftest();
                return true;
            default:
                PrintUsage();
                exitCode = 2;
                return true;
        }
    }

    /// <summary>
    /// Exercises the grader on every task, both boundaries of both tier thresholds, the tier →
    /// surface mapping, cache expiry and capacity, and the rolling tracker's minimum sample and
    /// hysteresis.
    /// </summary>
    /// <returns>0 when every assertion holds, otherwise 1.</returns>
    public static int Selftest()
    {
        var failures = new List<string>();
        var now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

        // --- grading: each task passes on a correct submission and fails on a wrong one ---
        var perfect = new ExamSubmission
        {
            CanaryVerdict = CanaryVerdict.ReadVerified,
            BasicCallArguments = """{"url":"https://example.com"}""",
            FocusBudgetArguments = """{"url":"https://example.com","focus_query":"closures","max_tokens":800}""",
            Chain = new ExamChainAttempt(
                [
                    """{"url":"https://example.com"}""",
                    """{"url":"https://example.com","if_none_match":"sha256:abc"}""",
                ],
                ProducedValue: "sha256:abc"),
        };

        var graded = ExamGrader.Grade(perfect, now);
        Check(failures, "a complete submission scores 4", graded.Score == 4);
        Check(failures, "a score of 4 is strong", graded.Tier == AgentTier.Strong);
        Check(failures, "grading reports one outcome per task", graded.Outcomes.Count == ExamResult.MaxScore);

        var empty = ExamGrader.Grade(new ExamSubmission(), now);
        Check(failures, "an empty submission scores 0", empty.Score == 0);
        Check(failures, "a score of 0 is weak", empty.Tier == AgentTier.Weak);
        Check(failures, "a null submission grades without throwing", ExamGrader.Grade(null, now).Score == 0);

        // --- grading: the canary task is the one that cannot be gamed ---
        Check(failures, "canary passes only on READ_VERIFIED",
            ExamGrader.GradeCanary(CanaryVerdict.ReadVerified).Passed
            && !ExamGrader.GradeCanary(CanaryVerdict.ReadStale).Passed
            && !ExamGrader.GradeCanary(CanaryVerdict.ReplaySuspect).Passed
            && !ExamGrader.GradeCanary(CanaryVerdict.Hallucinated).Passed
            && !ExamGrader.GradeCanary(null).Passed);

        // --- grading: schema binding ---
        Check(failures, "basic call rejects malformed JSON",
            !ExamGrader.GradeBasicCall("{not json").Passed);
        Check(failures, "basic call rejects a JSON array",
            !ExamGrader.GradeBasicCall("""["https://example.com"]""").Passed);
        Check(failures, "basic call rejects a relative URL",
            !ExamGrader.GradeBasicCall("""{"url":"/docs"}""").Passed);
        Check(failures, "basic call rejects a non-http scheme",
            !ExamGrader.GradeBasicCall("""{"url":"file:///etc/passwd"}""").Passed);
        Check(failures, "basic call rejects a numeric url",
            !ExamGrader.GradeBasicCall("""{"url":42}""").Passed);

        // --- grading: budget plausibility ---
        Check(failures, "focus+budget rejects a missing budget",
            !ExamGrader.GradeFocusBudget("""{"focus_query":"x"}""").Passed);
        Check(failures, "focus+budget rejects an empty focus query",
            !ExamGrader.GradeFocusBudget("""{"focus_query":"   ","max_tokens":800}""").Passed);
        Check(failures, "focus+budget rejects an implausible budget",
            !ExamGrader.GradeFocusBudget("""{"focus_query":"x","max_tokens":999999999}""").Passed);
        Check(failures, "focus+budget rejects a non-integer budget",
            !ExamGrader.GradeFocusBudget("""{"focus_query":"x","max_tokens":"800"}""").Passed);

        // --- grading: chaining needs the exact produced value ---
        Check(failures, "chain rejects a single call",
            !ExamGrader.GradeChain(new ExamChainAttempt(["""{"url":"https://example.com"}"""], "sha256:abc")).Passed);
        Check(failures, "chain rejects a value that was never consumed",
            !ExamGrader.GradeChain(new ExamChainAttempt(
                ["""{"url":"https://example.com"}""", """{"url":"https://example.com"}"""],
                "sha256:abc")).Passed);
        Check(failures, "chain rejects a mismatched value",
            !ExamGrader.GradeChain(new ExamChainAttempt(
                ["""{"url":"https://example.com"}""", """{"if_none_match":"sha256:zzz"}"""],
                "sha256:abc")).Passed);
        Check(failures, "chain accepts diff_against as a consuming parameter",
            ExamGrader.GradeChain(new ExamChainAttempt(
                ["""{"url":"https://example.com"}""", """{"diff_against":"sha256:abc"}"""],
                "sha256:abc")).Passed);
        Check(failures, "chain rejects consumption by the first call only",
            !ExamGrader.GradeChain(new ExamChainAttempt(
                ["""{"if_none_match":"sha256:abc"}""", """{"url":"https://example.com"}"""],
                "sha256:abc")).Passed);

        // --- tiering: both boundaries of both thresholds ---
        Check(failures, "score 0 and 1 map to weak",
            ExamScoring.TierFor(0) == AgentTier.Weak && ExamScoring.TierFor(1) == AgentTier.Weak);
        Check(failures, "score 2 and 3 map to medium",
            ExamScoring.TierFor(2) == AgentTier.Medium && ExamScoring.TierFor(3) == AgentTier.Medium);
        Check(failures, "score 4 maps to strong", ExamScoring.TierFor(4) == AgentTier.Strong);
        Check(failures, "out-of-range scores clamp rather than throw",
            ExamScoring.TierFor(-5) == AgentTier.Weak && ExamScoring.TierFor(99) == AgentTier.Strong);

        // --- tiering: the surface a tier actually sees ---
        Check(failures, "weak sees exactly one tool", ExamScoring.SurfaceSizeFor(AgentTier.Weak) == 1);
        Check(failures, "medium sees exactly three tools", ExamScoring.SurfaceSizeFor(AgentTier.Medium) == 3);
        Check(failures, "strong sees the whole catalogue",
            ExamScoring.SurfaceSizeFor(AgentTier.Strong) == OccamMcpServerRegistration.OccamToolNames.Length);
        Check(failures, "weak surface is the cascade facade",
            OccamToolProfile.IsExposed("occam", OccamToolProfile.Minimal));
        Check(failures, "weak surface hides specialised transcode",
            !OccamToolProfile.IsExposed("occam_transcode", OccamToolProfile.Minimal));
        Check(failures, "a weak agent cannot reach playbook authoring",
            !OccamToolProfile.IsExposed("occam_playbook_heal", OccamToolProfile.Minimal)
            && !OccamToolProfile.IsExposed("occam_playbook_save", OccamToolProfile.Basic));
        Check(failures, "narrow profiles get their own instructions, not the full menu",
            !ReferenceEquals(
                OccamServerInstructions.TextFor(OccamToolProfile.Minimal),
                OccamServerInstructions.TextFor(OccamToolProfile.Full))
            && !OccamServerInstructions.TextFor(OccamToolProfile.Minimal).Contains("occam_playbook_heal", StringComparison.Ordinal));

        // --- default: a client that never sat the exam is trusted, not starved ---
        Check(failures, "the default tier is medium", ExamResult.Default(now).Tier == AgentTier.Medium);
        Check(failures, "the default result is marked as such", ExamResult.Default(now).Source == "default");

        // --- cache: expiry, replacement, capacity ---
        Check(failures, "cached results expire and capacity is bounded", CacheBehaves(graded));

        // --- rolling competence: minimum sample, hysteresis, oscillation counting ---
        Check(failures, "rolling scoring withholds judgement below the minimum sample", RollingWithholdsEarly());
        Check(failures, "rolling scoring demotes a consistently failing caller", RollingDemotes());
        Check(failures, "hysteresis suppresses a single-observation flip", HysteresisHolds());

        if (failures.Count > 0)
        {
            foreach (var failure in failures)
            {
                Console.Error.WriteLine($"[exam.selftest] FAIL {failure}");
            }

            Console.Out.WriteLine($"EXAM_SELFTEST_FAILED checks_failed={failures.Count}");
            return 1;
        }

        Console.Out.WriteLine(SelftestOkMarker);
        return 0;
    }

    /// <summary>
    /// Writes the task catalogue to <c>--out</c>, or to stdout when no path is given.
    /// </summary>
    /// <remarks>
    /// <c>--out</c> exists for the same reason it does on <c>canary vectors</c>: a shell redirect is
    /// not byte-faithful on every platform — PowerShell reassembles stdout line by line and rewrites
    /// the newlines — so an artefact that has to be byte-identical across platforms must be written
    /// by the process that produced it.
    /// </remarks>
    /// <param name="args">Verb arguments; accepts <c>--out &lt;path&gt;</c>.</param>
    /// <returns>0 on success.</returns>
    public static int WriteTasks(string[] args)
    {
        var json = EmitTasks();
        var outIndex = Array.IndexOf(args, "--out");
        if (outIndex >= 0 && outIndex + 1 < args.Length)
        {
            var path = args[outIndex + 1];
            var directory = Path.GetDirectoryName(Path.GetFullPath(path));
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllText(path, json, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
            Console.Error.WriteLine($"[exam.tasks] wrote {path}");
            return 0;
        }

        // Write, not WriteLine: the payload already ends with LF, and WriteLine would append
        // Environment.NewLine on top.
        Console.Out.Write(json);
        return 0;
    }

    /// <summary>
    /// Emits the task catalogue as JSON, so a harness administering the exam and the grader cannot
    /// drift apart.
    /// </summary>
    /// <returns>UTF-8 JSON text, newline terminated, with LF newlines on every platform.</returns>
    public static string EmitTasks()
    {
        var buffer = new ArrayBufferWriter<byte>(2_048);
        using (var writer = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true, NewLine = "\n" }))
        {
            writer.WriteStartObject();
            writer.WriteNumber("maxScore", ExamResult.MaxScore);
            writer.WriteStartObject("tiers");
            foreach (var tier in Enum.GetValues<AgentTier>())
            {
                writer.WriteStartObject(ExamWire.ToWire(tier));
                writer.WriteString("profile", ExamScoring.ProfileFor(tier));
                writer.WriteNumber("toolCount", ExamScoring.SurfaceSizeFor(tier));
                writer.WriteEndObject();
            }

            writer.WriteEndObject();
            writer.WriteStartArray("tasks");
            foreach (var task in ExamTasks.All)
            {
                writer.WriteStartObject();
                writer.WriteString("id", ExamWire.ToWire(task.Id));
                writer.WriteString("prompt", task.Prompt);
                writer.WriteString("passCondition", task.PassCondition);
                writer.WriteString("measures", task.Measures);
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan) + "\n";
    }

    private static bool CacheBehaves(ExamResult result)
    {
        var clock = ManualClock.AtUnixSeconds(1_800_000_000);
        var cache = new ExamResultCache(ttlHours: 1, capacity: 2, timeProvider: clock);
        var subject = ExamSubject.Create("cursor/1.0", "model-x", "session-1");

        cache.Store(subject, result);
        if (!cache.TryGet(subject, out _))
        {
            return false;
        }

        // A subject that never sat the exam falls back to the default tier rather than missing.
        var unknown = ExamSubject.Create("cursor/1.0", "model-x", "session-unknown");
        if (cache.GetOrDefault(unknown).Tier != AgentTier.Medium)
        {
            return false;
        }

        // Re-storing must extend the entry, and the extension must actually be honoured.
        clock.Advance(TimeSpan.FromMinutes(50));
        cache.Store(subject, result);
        clock.Advance(TimeSpan.FromMinutes(20));
        if (!cache.TryGet(subject, out _))
        {
            return false;
        }

        clock.Advance(TimeSpan.FromMinutes(61));
        if (cache.TryGet(subject, out _))
        {
            return false;
        }

        cache.Store(ExamSubject.Create("c", "m", "s1"), result);
        cache.Store(ExamSubject.Create("c", "m", "s2"), result);
        cache.Store(ExamSubject.Create("c", "m", "s3"), result);
        return cache.Count <= 2;
    }

    private static bool RollingWithholdsEarly()
    {
        var tracker = new CompetenceTracker();
        var subject = ExamSubject.Create("c", "m", "s");
        var bad = new CallObservation("occam_transcode", ArgumentsValid: false, CallerError: true);

        for (var i = 0; i < CompetenceTracker.MinObservations - 1; i++)
        {
            var assessment = tracker.Record(subject, AgentTier.Medium, bad);
            if (assessment.Score is not null || assessment.Tier != AgentTier.Medium)
            {
                return false;
            }
        }

        return true;
    }

    private static bool RollingDemotes()
    {
        var tracker = new CompetenceTracker();
        var subject = ExamSubject.Create("c", "m", "s");
        var bad = new CallObservation("occam_transcode", ArgumentsValid: false, CallerError: true);

        CompetenceAssessment assessment = default;
        for (var i = 0; i < CompetenceTracker.MinObservations + CompetenceTracker.StabilityRequirement; i++)
        {
            assessment = tracker.Record(subject, AgentTier.Medium, bad);
        }

        return assessment.Tier == AgentTier.Weak
            && assessment.Score == 0.0
            && assessment.Oscillations == 1;
    }

    private static bool HysteresisHolds()
    {
        var tracker = new CompetenceTracker();
        var subject = ExamSubject.Create("c", "m", "s");
        var good = new CallObservation("occam_transcode", ArgumentsValid: true, CallerError: false);
        var bad = new CallObservation("occam_transcode", ArgumentsValid: false, CallerError: true);

        // A clean run establishes strong.
        for (var i = 0; i < CompetenceTracker.MinObservations + CompetenceTracker.StabilityRequirement; i++)
        {
            tracker.Record(subject, AgentTier.Medium, good);
        }

        var before = tracker.Assess(subject, AgentTier.Medium);
        if (before.Tier != AgentTier.Strong)
        {
            return false;
        }

        // One bad call drops the score below the promote threshold, but must not move the tier yet.
        var after = tracker.Record(subject, AgentTier.Medium, bad);
        return after.Tier == AgentTier.Strong
            && after.SuggestedTier != AgentTier.Strong
            && after.Oscillations == before.Oscillations;
    }

    private static void Check(List<string> failures, string description, bool condition)
    {
        Console.Error.WriteLine($"[exam] {(condition ? "ok  " : "FAIL")} {description}");
        if (!condition)
        {
            failures.Add(description);
        }
    }

    /// <summary>
    /// Proves the harness path: parse fixture submissions → grade → recommended profile, plus the
    /// H3 static-map baseline, without a model in the loop.
    /// </summary>
    public static int HarnessSelftest()
    {
        var failures = new List<string>();
        const string perfectJson = """
            {
              "clientInfo": "harness/selftest",
              "modelHint": "mock-strong",
              "sessionId": "s-perfect",
              "selfReportTier": "strong",
              "canaryVerdict": "READ_VERIFIED",
              "basicCallArguments": {"url":"https://example.com"},
              "focusBudgetArguments": {"task":"closures","budget":800},
              "chain": {
                "calls": [
                  {"url":"https://example.com"},
                  {"url":"https://example.com","if_none_match":"sha256:abc"}
                ],
                "producedValue": "sha256:abc"
              }
            }
            """;

        const string weakJson = """
            {
              "clientInfo": "harness/selftest",
              "modelHint": "mock-weak-7b",
              "sessionId": "s-weak",
              "selfReportTier": "strong",
              "canaryVerdict": "HALLUCINATED",
              "basicCallArguments": {"url":"not-a-url"},
              "focusBudgetArguments": {"task":"x"},
              "chain": { "calls": [], "producedValue": null }
            }
            """;

        Check(failures, "perfect submission parses",
            ExamSubmissionParser.TryParse(perfectJson, out _, out var perfectSub, out _));
        var perfect = ExamGrader.Grade(perfectSub, DateTimeOffset.UtcNow);
        Check(failures, "perfect scores 4", perfect.Score == 4);
        Check(failures, "perfect is strong → full", ExamScoring.ProfileFor(perfect.Tier) == OccamToolProfile.Full);

        Check(failures, "weak submission parses",
            ExamSubmissionParser.TryParse(weakJson, out var weakDoc, out var weakSub, out _));
        var weak = ExamGrader.Grade(weakSub, DateTimeOffset.UtcNow);
        Check(failures, "weak scores 0", weak.Score == 0);
        Check(failures, "weak is weak → minimal", ExamScoring.ProfileFor(weak.Tier) == OccamToolProfile.Minimal);
        Check(failures, "self-report can disagree with exam",
            ExamWire.ParseTier(weakDoc.SelfReportTier) == AgentTier.Strong && weak.Tier == AgentTier.Weak);
        Check(failures, "static map marks mock-weak as weak",
            StaticModelMap.Map(weakDoc.ModelHint) == AgentTier.Weak);
        Check(failures, "static map marks mock-strong as strong",
            StaticModelMap.Map("mock-strong") == AgentTier.Strong);
        Check(failures, "static map defaults unknown to medium",
            StaticModelMap.Map("some-unknown-model") == AgentTier.Medium);

        var emitted = ExamGradeCli.EmitGradeResult(
            ExamSubject.Create("harness/selftest", "mock-strong", "s"),
            perfect,
            ExamScoring.ProfileFor(perfect.Tier),
            ExamScoring.SurfaceSizeFor(perfect.Tier),
            AgentTier.Strong,
            "mock-strong");
        Check(failures, "grade envelope is JSON", emitted.StartsWith('{'));
        Check(failures, "grade envelope names protocol", emitted.Contains("occam-exam-grade-v1", StringComparison.Ordinal));
        Check(failures, "grade envelope has LF newline", emitted.EndsWith('\n') && !emitted.Contains('\r'));

        if (failures.Count == 0)
        {
            Console.Out.WriteLine("EXAM_HARNESS_SELFTEST_OK");
            return 0;
        }

        foreach (var f in failures)
        {
            Console.Error.WriteLine($"[exam.harness-selftest] FAIL {f}");
        }

        return 1;
    }

    private static void PrintUsage()
    {
        Console.Error.WriteLine("""
            occam exam <subverb>

              selftest                 prove grading, tiering, tier -> tool surface, cache expiry and hysteresis
              tasks [--out P]          write the task catalogue as JSON (for a harness administering the exam)
              grade --submission P     grade a harness submission JSON; optional --out result.json --write-env profile.env
              harness-selftest         prove parse → grade → profile + H3 static-map baseline without a model

            Markers: EXAM_SELFTEST_OK, EXAM_GRADE_OK, EXAM_HARNESS_SELFTEST_OK.

            The host does not administer the exam on the MCP request path — MCP has no general
            server-initiated task mechanism. The harness grades a behaviour record offline, then an
            operator sets OCCAM_PROFILE for the next process start. See docs/adr/0016-capability-exam.md.
            """);
    }
}
