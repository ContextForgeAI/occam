using FsCheck;
using FsCheck.Fluent;
using FsCheck.Xunit;
using OccamMcp.Core.Exam;

namespace OccamMcp.Core.Tests.Exam;

/// <summary>
/// Property-based checks on the grader and the tiering maps. The grader is fed arbitrary strings on
/// purpose: its input is an agent's raw arguments, so "never throws, always grades" has to hold for
/// input nobody anticipated — a grader that crashes on a malformed submission denies a tier instead
/// of assigning a low one.
/// </summary>
public sealed class ExamPropertyTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    [Property(MaxTest = 500)]
    public bool GradingArbitraryArgumentsNeverThrows(string? basicCall, string? focusBudget)
    {
        var result = ExamGrader.Grade(
            new ExamSubmission { BasicCallArguments = basicCall, FocusBudgetArguments = focusBudget },
            Now);

        return result.Outcomes.Count == ExamResult.MaxScore;
    }

    [Property(MaxTest = 500)]
    public bool GradingArbitraryChainsNeverThrows(string[] calls, string? produced)
    {
        var outcome = ExamGrader.GradeChain(new ExamChainAttempt(calls ?? [], produced));

        return !string.IsNullOrWhiteSpace(outcome.Detail);
    }

    [Property(MaxTest = 500)]
    public bool ScoreIsAlwaysWithinBounds(string? basicCall, string? focusBudget, string? chainCall)
    {
        var result = ExamGrader.Grade(
            new ExamSubmission
            {
                BasicCallArguments = basicCall,
                FocusBudgetArguments = focusBudget,
                Chain = new ExamChainAttempt([chainCall ?? string.Empty], ProducedValue: chainCall),
            },
            Now);

        return result.Score >= 0 && result.Score <= ExamResult.MaxScore;
    }

    [Property(MaxTest = 500)]
    public bool ScoreAlwaysEqualsThePassedOutcomeCount(string? basicCall, string? focusBudget)
    {
        var result = ExamGrader.Grade(
            new ExamSubmission { BasicCallArguments = basicCall, FocusBudgetArguments = focusBudget },
            Now);

        return result.Score == result.Outcomes.Count(o => o.Passed);
    }

    [Property(MaxTest = 500)]
    public bool TierIsMonotonicInScore(int first, int second)
    {
        var lower = Math.Min(first, second);
        var higher = Math.Max(first, second);

        // A higher score can never buy a narrower surface.
        return ExamScoring.SurfaceSizeFor(ExamScoring.TierFor(lower))
            <= ExamScoring.SurfaceSizeFor(ExamScoring.TierFor(higher));
    }

    [Property(MaxTest = 500)]
    public bool AnyScoreMapsToAKnownTier(int score)
    {
        return Enum.IsDefined(ExamScoring.TierFor(score));
    }

    [Property(MaxTest = 500)]
    public Property RollingSuggestionIsMonotonicInCompetence(double first, double second)
    {
        var lower = Math.Clamp(Math.Min(first, second), 0, 1);
        var higher = Math.Clamp(Math.Max(first, second), 0, 1);

        return (CompetenceTracker.SuggestTier(lower) <= CompetenceTracker.SuggestTier(higher))
            .When(!double.IsNaN(first) && !double.IsNaN(second));
    }

    [Property(MaxTest = 200)]
    public bool SubjectNormalisationIsIdempotent(string? clientInfo, string? modelHint, string? sessionId)
    {
        var once = ExamSubject.Create(clientInfo, modelHint, sessionId);
        var twice = ExamSubject.Create(once.ClientInfo, once.ModelHint, once.SessionId);

        return once == twice;
    }

    [Property(MaxTest = 200)]
    public bool SubjectKeyComponentsAreNeverBlank(string? clientInfo, string? modelHint, string? sessionId)
    {
        var subject = ExamSubject.Create(clientInfo, modelHint, sessionId);

        return !string.IsNullOrWhiteSpace(subject.ClientInfo)
            && !string.IsNullOrWhiteSpace(subject.ModelHint)
            && !string.IsNullOrWhiteSpace(subject.SessionId);
    }

    [Property(MaxTest = 200)]
    public bool TrackerNeverAppliesATierOutsideTheKnownSet(bool[] outcomes)
    {
        var tracker = new CompetenceTracker();
        var subject = ExamSubject.Create("c", "m", "s");

        CompetenceAssessment assessment = default;
        foreach (var valid in outcomes ?? [])
        {
            assessment = tracker.Record(
                subject,
                AgentTier.Medium,
                new CallObservation("occam_transcode", valid, CallerError: !valid));
        }

        return (outcomes ?? []).Length == 0
            || (Enum.IsDefined(assessment.Tier) && assessment.Oscillations >= 0);
    }
}
