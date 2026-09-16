using OccamMcp.Core.Exam;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

/// <summary>
/// Rolling competence scoring — the mechanism H3 proposes. The interesting assertions are the ones
/// about restraint: no judgement below the minimum sample, and no tier change until a suggestion
/// has held. A tracker that flips the tool surface under a running agent is worse than one that is
/// stably wrong.
/// </summary>
public sealed class CompetenceTrackerTests
{
    private static readonly CallObservation Good = new("occam_transcode", ArgumentsValid: true, CallerError: false);
    private static readonly CallObservation Bad = new("occam_transcode", ArgumentsValid: false, CallerError: true);

    private static ExamSubject Subject() => ExamSubject.Create("cursor/1.0", "model-x", "session-1");

    private static CompetenceAssessment Feed(CompetenceTracker tracker, CallObservation observation, int times)
    {
        CompetenceAssessment assessment = default;
        for (var i = 0; i < times; i++)
        {
            assessment = tracker.Record(Subject(), AgentTier.Medium, observation);
        }

        return assessment;
    }

    [Fact]
    public void NoScoreIsReportedBelowTheMinimumSample()
    {
        var tracker = new CompetenceTracker();

        for (var i = 1; i < CompetenceTracker.MinObservations; i++)
        {
            var assessment = tracker.Record(Subject(), AgentTier.Medium, Bad);

            Assert.Null(assessment.Score);
            Assert.Equal(AgentTier.Medium, assessment.Tier);
            Assert.Equal(i, assessment.Observations);
        }
    }

    [Fact]
    public void TierIsUnchangedBelowTheMinimumSample()
    {
        var tracker = new CompetenceTracker();

        var assessment = Feed(tracker, Bad, CompetenceTracker.MinObservations - 1);

        Assert.Equal(AgentTier.Medium, assessment.Tier);
        Assert.Equal(0, assessment.Oscillations);
    }

    [Fact]
    public void AScoreAppearsAtTheMinimumSample()
    {
        var tracker = new CompetenceTracker();

        var assessment = Feed(tracker, Good, CompetenceTracker.MinObservations);

        Assert.Equal(1.0, assessment.Score);
    }

    [Fact]
    public void AConsistentlyFailingCallerIsDemoted()
    {
        var tracker = new CompetenceTracker();

        var assessment = Feed(
            tracker, Bad, CompetenceTracker.MinObservations + CompetenceTracker.StabilityRequirement);

        Assert.Equal(AgentTier.Weak, assessment.Tier);
        Assert.Equal(0.0, assessment.Score);
        Assert.Equal(1, assessment.Oscillations);
    }

    [Fact]
    public void ACleanCallerIsPromoted()
    {
        var tracker = new CompetenceTracker();

        var assessment = Feed(
            tracker, Good, CompetenceTracker.MinObservations + CompetenceTracker.StabilityRequirement);

        Assert.Equal(AgentTier.Strong, assessment.Tier);
        Assert.Equal(1.0, assessment.Score);
    }

    [Fact]
    public void HysteresisSuppressesASingleObservationFlip()
    {
        var tracker = new CompetenceTracker();
        Feed(tracker, Good, CompetenceTracker.MinObservations + CompetenceTracker.StabilityRequirement);
        var before = tracker.Assess(Subject(), AgentTier.Medium);

        var after = tracker.Record(Subject(), AgentTier.Medium, Bad);

        Assert.Equal(AgentTier.Strong, after.Tier);
        Assert.NotEqual(AgentTier.Strong, after.SuggestedTier);
        Assert.Equal(before.Oscillations, after.Oscillations);
    }

    [Fact]
    public void AlternatingBehaviourDoesNotThrashTheTier()
    {
        // The pathological input for a hysteresis-free implementation.
        var tracker = new CompetenceTracker();
        Feed(tracker, Good, CompetenceTracker.MinObservations);

        CompetenceAssessment assessment = default;
        for (var i = 0; i < 30; i++)
        {
            assessment = tracker.Record(Subject(), AgentTier.Medium, i % 2 == 0 ? Bad : Good);
        }

        // Alternating settles around 0.5, which suggests Weak; what matters is that the surface did
        // not change on every other call.
        Assert.InRange(assessment.Oscillations, 0, 2);
    }

    [Fact]
    public void ASustainedChangeIsEventuallyApplied()
    {
        var tracker = new CompetenceTracker();
        Feed(tracker, Good, CompetenceTracker.MinObservations + CompetenceTracker.StabilityRequirement);

        // A full window of failures must overcome hysteresis.
        var assessment = Feed(tracker, Bad, CompetenceTracker.DefaultWindow);

        Assert.Equal(AgentTier.Weak, assessment.Tier);
        Assert.True(assessment.Oscillations >= 1);
    }

    [Fact]
    public void OnlyTheLastWindowOfObservationsCounts()
    {
        var tracker = new CompetenceTracker(window: CompetenceTracker.MinObservations);
        Feed(tracker, Bad, CompetenceTracker.MinObservations);

        var assessment = Feed(tracker, Good, CompetenceTracker.MinObservations);

        Assert.Equal(1.0, assessment.Score);
        Assert.Equal(CompetenceTracker.MinObservations, assessment.Observations);
    }

    [Fact]
    public void APageFailureIsNotTheAgentsFault()
    {
        // http_404 on a valid call must not count against the caller: otherwise the tracker scores
        // the web and calls the result agent competence.
        var tracker = new CompetenceTracker();
        var pageFailure = new CallObservation("occam_transcode", ArgumentsValid: true, CallerError: false);

        var assessment = Feed(tracker, pageFailure, CompetenceTracker.MinObservations);

        Assert.Equal(1.0, assessment.Score);
    }

    [Theory]
    [InlineData(1.0, AgentTier.Strong)]
    [InlineData(0.9, AgentTier.Strong)]
    [InlineData(0.89, AgentTier.Medium)]
    [InlineData(0.51, AgentTier.Medium)]
    [InlineData(0.5, AgentTier.Weak)]
    [InlineData(0.0, AgentTier.Weak)]
    public void SuggestionThresholdsHoldAtTheirBoundaries(double score, AgentTier expected)
    {
        Assert.Equal(expected, CompetenceTracker.SuggestTier(score));
    }

    [Fact]
    public void AssessingAnUnknownSubjectReportsTheBaseline()
    {
        var tracker = new CompetenceTracker();

        var assessment = tracker.Assess(ExamSubject.Create("c", "m", "never-seen"), AgentTier.Strong);

        Assert.Equal(AgentTier.Strong, assessment.Tier);
        Assert.Null(assessment.Score);
        Assert.Equal(0, assessment.Observations);
    }

    [Fact]
    public void ForgettingASubjectResetsItToTheBaseline()
    {
        var tracker = new CompetenceTracker();
        Feed(tracker, Bad, CompetenceTracker.MinObservations + CompetenceTracker.StabilityRequirement);

        Assert.True(tracker.Forget(Subject()));
        Assert.False(tracker.Forget(Subject()));
        Assert.Equal(AgentTier.Medium, tracker.Assess(Subject(), AgentTier.Medium).Tier);
    }

    [Fact]
    public void TheExamBaselineIsTheStartingPoint()
    {
        var tracker = new CompetenceTracker();

        // A client that the exam rated Weak starts there, even before any call is observed.
        var assessment = tracker.Record(Subject(), AgentTier.Weak, Good);

        Assert.Equal(AgentTier.Weak, assessment.Tier);
    }

    [Fact]
    public void TrackedSubjectsStayBounded()
    {
        var tracker = new CompetenceTracker(maxSubjects: 8);

        for (var i = 0; i < 500; i++)
        {
            tracker.Record(ExamSubject.Create("c", "m", $"s{i}"), AgentTier.Medium, Good);
        }

        Assert.InRange(tracker.TrackedSubjects, 1, 8);
    }

    [Fact]
    public void ConstructorArgumentsAreValidated()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => new CompetenceTracker(window: CompetenceTracker.MinObservations - 1));
        Assert.Throws<ArgumentOutOfRangeException>(() => new CompetenceTracker(maxSubjects: 0));
    }

    [Fact]
    public void ConcurrentRecordingIsSafe()
    {
        var tracker = new CompetenceTracker();

        Parallel.For(0, 200, i =>
            tracker.Record(Subject(), AgentTier.Medium, i % 3 == 0 ? Bad : Good));

        var assessment = tracker.Assess(Subject(), AgentTier.Medium);

        Assert.NotNull(assessment.Score);
        Assert.InRange(assessment.Observations, 1, CompetenceTracker.DefaultWindow);
    }
}
