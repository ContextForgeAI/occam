using OccamMcp.Core.Canary;
using OccamMcp.Core.Exam;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

/// <summary>
/// Grading rules. The grader is the part a tier is built on, so it has to be total: malformed input
/// scores zero for that task rather than throwing, and no task can escape being graded.
/// </summary>
public sealed class ExamGraderTests
{
    private static readonly DateTimeOffset Now = DateTimeOffset.FromUnixTimeSeconds(1_800_000_000);

    private static ExamSubmission PerfectSubmission() => new()
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

    [Fact]
    public void CompleteSubmission_ScoresFourAndIsStrong()
    {
        var result = ExamGrader.Grade(PerfectSubmission(), Now);

        Assert.Equal(4, result.Score);
        Assert.Equal(AgentTier.Strong, result.Tier);
        Assert.Equal("strong", result.TierWire);
        Assert.Equal("exam", result.Source);
        Assert.All(result.Outcomes, outcome => Assert.True(outcome.Passed));
    }

    [Fact]
    public void EmptySubmission_ScoresZeroAndIsWeak()
    {
        var result = ExamGrader.Grade(new ExamSubmission(), Now);

        Assert.Equal(0, result.Score);
        Assert.Equal(AgentTier.Weak, result.Tier);
        Assert.All(result.Outcomes, outcome => Assert.False(outcome.Passed));
    }

    [Fact]
    public void NullSubmission_GradesWithoutThrowing()
    {
        var result = ExamGrader.Grade(null, Now);

        Assert.Equal(0, result.Score);
        Assert.Equal(ExamResult.MaxScore, result.Outcomes.Count);
    }

    [Fact]
    public void EveryTaskIsGradedExactlyOnce()
    {
        var result = ExamGrader.Grade(PerfectSubmission(), Now);

        var graded = result.Outcomes.Select(o => o.Task).ToArray();
        Assert.Equal(Enum.GetValues<ExamTaskId>().Length, graded.Length);
        Assert.Equal(graded.Length, graded.Distinct().Count());
    }

    [Fact]
    public void EveryOutcomeCarriesAnExplanation()
    {
        // A tier an agent cannot act on is a tier it cannot fix.
        var result = ExamGrader.Grade(new ExamSubmission(), Now);

        Assert.All(result.Outcomes, outcome => Assert.False(string.IsNullOrWhiteSpace(outcome.Detail)));
    }

    // ---- canary task ----

    [Fact]
    public void Canary_PassesOnlyOnReadVerified()
    {
        Assert.True(ExamGrader.GradeCanary(CanaryVerdict.ReadVerified).Passed);
        Assert.False(ExamGrader.GradeCanary(CanaryVerdict.ReadStale).Passed);
        Assert.False(ExamGrader.GradeCanary(CanaryVerdict.ReplaySuspect).Passed);
        Assert.False(ExamGrader.GradeCanary(CanaryVerdict.Hallucinated).Passed);
        Assert.False(ExamGrader.GradeCanary(null).Passed);
    }

    [Fact]
    public void Canary_DistinguishesStaleFromInvented()
    {
        // Both fail, but for materially different reasons, and the agent needs to know which.
        var stale = ExamGrader.GradeCanary(CanaryVerdict.ReadStale);
        var invented = ExamGrader.GradeCanary(CanaryVerdict.Hallucinated);

        Assert.NotEqual(stale.Detail, invented.Detail);
        Assert.Contains("current read", stale.Detail, StringComparison.Ordinal);
    }

    // ---- basic call task ----

    [Theory]
    [InlineData("""{"url":"https://example.com"}""")]
    [InlineData("""{"url":"http://example.com/docs?a=1"}""")]
    [InlineData("""{"url":"  https://example.com  "}""")]
    [InlineData("""{"url":"https://example.com","max_tokens":800}""")]
    public void BasicCall_AcceptsAUsableAbsoluteUrl(string arguments)
    {
        Assert.True(ExamGrader.GradeBasicCall(arguments).Passed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("{not json")]
    [InlineData("""["https://example.com"]""")]
    [InlineData(""""a string"""")]
    [InlineData("""{"URL":"https://example.com"}""")]
    [InlineData("""{"url":"/docs"}""")]
    [InlineData("""{"url":"example.com"}""")]
    [InlineData("""{"url":"file:///etc/passwd"}""")]
    [InlineData("""{"url":"ftp://example.com"}""")]
    [InlineData("""{"url":42}""")]
    [InlineData("""{"url":null}""")]
    [InlineData("""{"url":""}""")]
    [InlineData("""{"max_tokens":800}""")]
    public void BasicCall_RejectsAnythingElse(string? arguments)
    {
        Assert.False(ExamGrader.GradeBasicCall(arguments).Passed);
    }

    // ---- focus and budget task ----

    [Theory]
    [InlineData("""{"focus_query":"closures","max_tokens":800}""")]
    [InlineData("""{"focus_query":"x","max_tokens":64}""")]
    [InlineData("""{"focus_query":"x","max_tokens":200000}""")]
    [InlineData("""{"task":"closures","budget":800}""")]
    public void FocusBudget_AcceptsAFocusedRequestWithAPlausibleBudget(string arguments)
    {
        Assert.True(ExamGrader.GradeFocusBudget(arguments).Passed);
    }

    [Theory]
    [InlineData("""{"focus_query":"x"}""")]
    [InlineData("""{"max_tokens":800}""")]
    [InlineData("""{"focus_query":"","max_tokens":800}""")]
    [InlineData("""{"focus_query":"   ","max_tokens":800}""")]
    [InlineData("""{"focus_query":"x","max_tokens":63}""")]
    [InlineData("""{"focus_query":"x","max_tokens":200001}""")]
    [InlineData("""{"focus_query":"x","max_tokens":"800"}""")]
    [InlineData("""{"focus_query":"x","max_tokens":800.5}""")]
    [InlineData("""{"focus_query":42,"max_tokens":800}""")]
    public void FocusBudget_RejectsAnythingElse(string arguments)
    {
        Assert.False(ExamGrader.GradeFocusBudget(arguments).Passed);
    }

    [Fact]
    public void FocusBudget_TreatsAnAbsurdBudgetAsOptingOut()
    {
        var outcome = ExamGrader.GradeFocusBudget("""{"focus_query":"x","max_tokens":999999999}""");

        Assert.False(outcome.Passed);
        Assert.Contains("not a budget", outcome.Detail, StringComparison.Ordinal);
    }

    // ---- chain task ----

    [Fact]
    public void Chain_AcceptsAValueCarriedIntoALaterCall()
    {
        var outcome = ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com"}""", """{"url":"https://example.com","if_none_match":"sha256:abc"}"""],
            "sha256:abc"));

        Assert.True(outcome.Passed);
        Assert.Contains("if_none_match", outcome.Detail, StringComparison.Ordinal);
    }

    [Fact]
    public void Chain_AcceptsDiffAgainstAsAConsumingParameter()
    {
        Assert.True(ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com"}""", """{"diff_against":"sha256:abc"}"""],
            "sha256:abc")).Passed);
    }

    [Fact]
    public void Chain_ToleratesLeadingAndTrailingWhitespaceOnTheCarriedValue()
    {
        Assert.True(ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com"}""", """{"if_none_match":"  sha256:abc  "}"""],
            "  sha256:abc  ")).Passed);
    }

    [Fact]
    public void Chain_RejectsASingleCall()
    {
        Assert.False(ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com","if_none_match":"sha256:abc"}"""],
            "sha256:abc")).Passed);
    }

    [Fact]
    public void Chain_RejectsConsumptionByTheFirstCall()
    {
        // A value cannot be carried backwards; consuming it in call 1 proves nothing.
        Assert.False(ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"if_none_match":"sha256:abc"}""", """{"url":"https://example.com"}"""],
            "sha256:abc")).Passed);
    }

    [Fact]
    public void Chain_RequiresTheExactProducedValue()
    {
        Assert.False(ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com"}""", """{"if_none_match":"sha256:different"}"""],
            "sha256:abc")).Passed);
    }

    [Fact]
    public void Chain_RejectsAValueInANonConsumingParameter()
    {
        Assert.False(ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com"}""", """{"focus_query":"sha256:abc"}"""],
            "sha256:abc")).Passed);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Chain_RejectsAMissingProducedValue(string? produced)
    {
        Assert.False(ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com"}""", """{"if_none_match":"sha256:abc"}"""],
            produced)).Passed);
    }

    [Fact]
    public void Chain_SkipsMalformedCallsInsteadOfFailingHard()
    {
        var outcome = ExamGrader.GradeChain(new ExamChainAttempt(
            ["""{"url":"https://example.com"}""", "{broken", """{"if_none_match":"sha256:abc"}"""],
            "sha256:abc"));

        Assert.True(outcome.Passed);
    }

    [Fact]
    public void Chain_RejectsANullAttempt()
    {
        Assert.False(ExamGrader.GradeChain(null).Passed);
    }

    [Fact]
    public void Chain_RejectsAnEmptyCallList()
    {
        Assert.False(ExamGrader.GradeChain(new ExamChainAttempt([], "sha256:abc")).Passed);
    }

    // ---- catalogue ----

    [Fact]
    public void CatalogueCoversEveryTaskExactlyOnce()
    {
        var ids = ExamTasks.All.Select(t => t.Id).ToArray();

        Assert.Equal(Enum.GetValues<ExamTaskId>().Length, ids.Length);
        Assert.Equal(ids.Length, ids.Distinct().Count());
        Assert.Equal(ExamResult.MaxScore, ExamTasks.All.Count);
    }

    [Fact]
    public void EveryCatalogueEntryStatesPromptPassConditionAndWhatItMeasures()
    {
        Assert.All(ExamTasks.All, task =>
        {
            Assert.False(string.IsNullOrWhiteSpace(task.Prompt));
            Assert.False(string.IsNullOrWhiteSpace(task.PassCondition));
            Assert.False(string.IsNullOrWhiteSpace(task.Measures));
        });
    }

    [Fact]
    public void GetRejectsAnUnknownTask()
    {
        Assert.Throws<ArgumentOutOfRangeException>(() => ExamTasks.Get((ExamTaskId)99));
    }
}
