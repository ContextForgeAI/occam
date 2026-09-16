using OccamMcp.Core.Canary;
using OccamMcp.Core.Exam;
using OccamMcp.Core.Transport;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

public sealed class ExamHarnessTests
{
    [Fact]
    public void PerfectSubmissionGradesStrongAndRecommendsFull()
    {
        const string json = """
            {
              "modelHint": "mock-strong",
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

        Assert.True(ExamSubmissionParser.TryParse(json, out _, out var submission, out _));
        var result = ExamGrader.Grade(submission, DateTimeOffset.UtcNow);
        Assert.Equal(4, result.Score);
        Assert.Equal(AgentTier.Strong, result.Tier);
        Assert.Equal(OccamToolProfile.Full, ExamScoring.ProfileFor(result.Tier));
    }

    [Fact]
    public void WeakSubmissionWithInflatedSelfReportStillMapsToMinimal()
    {
        const string json = """
            {
              "modelHint": "mock-weak-7b",
              "selfReportTier": "strong",
              "canaryVerdict": "HALLUCINATED",
              "basicCallArguments": {"url":"bad"},
              "focusBudgetArguments": {"task":"x"}
            }
            """;

        Assert.True(ExamSubmissionParser.TryParse(json, out var doc, out var submission, out _));
        var result = ExamGrader.Grade(submission, DateTimeOffset.UtcNow);
        Assert.Equal(0, result.Score);
        Assert.Equal(AgentTier.Weak, result.Tier);
        Assert.Equal(OccamToolProfile.Minimal, ExamScoring.ProfileFor(result.Tier));
        Assert.Equal(AgentTier.Strong, ExamWire.ParseTier(doc.SelfReportTier));
        Assert.Equal(AgentTier.Weak, StaticModelMap.Map(doc.ModelHint));
    }

    [Theory]
    [InlineData("READ_VERIFIED", CanaryVerdict.ReadVerified)]
    [InlineData("read_stale", CanaryVerdict.ReadStale)]
    [InlineData("HALLUCINATED", CanaryVerdict.Hallucinated)]
    public void CanaryVerdictStringsParseWireTokens(string wire, CanaryVerdict expected)
    {
        Assert.True(CanaryVerdictStrings.TryParse(wire, out var parsed));
        Assert.Equal(expected, parsed);
    }
}
