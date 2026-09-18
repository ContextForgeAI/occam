using System.Text.Json;
using OccamMcp.Core.Exam;
using OccamMcp.Core.Transport;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

public sealed class ExamMcpRuntimeTests
{
    private const string PerfectSubmission = """
        {
          "clientInfo": "cursor/1.0",
          "modelHint": "mock-strong",
          "sessionId": "s-perfect",
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

    private const string WeakSubmission = """
        {
          "clientInfo": "cursor/1.0",
          "modelHint": "mock-weak-7b",
          "sessionId": "s-weak",
          "canaryVerdict": "HALLUCINATED",
          "basicCallArguments": {"url":"not-a-url"},
          "focusBudgetArguments": {"task":"x"},
          "chain": { "calls": [], "producedValue": null }
        }
        """;

    private static ExamMcpRuntime CreateRuntime(bool pinned, string initial = OccamToolProfile.Reader)
    {
        var surface = new SessionToolSurface(initial, pinned);
        return new ExamMcpRuntime(new ExamResultCache(), surface);
    }

    [Fact]
    public void Submit_Perfect_AppliesFull_WhenNotPinned()
    {
        var runtime = CreateRuntime(pinned: false);

        var outcome = runtime.Submit(PerfectSubmission);

        Assert.True(outcome.Ok);
        Assert.Equal(4, outcome.Result.Score);
        Assert.Equal(AgentTier.Strong, outcome.Result.Tier);
        Assert.Equal(OccamToolProfile.Full, outcome.RecommendedProfile);
        Assert.True(outcome.Applied);
        Assert.False(outcome.Pinned);
        Assert.Equal(OccamToolProfile.Full, outcome.ActiveProfile);
        Assert.Equal(OccamToolProfile.Full, runtime.Surface.ProfileId);
    }

    [Fact]
    public void Submit_Perfect_DoesNotApply_WhenPinned()
    {
        var runtime = CreateRuntime(pinned: true, initial: OccamToolProfile.Reader);

        var outcome = runtime.Submit(PerfectSubmission);

        Assert.True(outcome.Ok);
        Assert.Equal(OccamToolProfile.Full, outcome.RecommendedProfile);
        Assert.False(outcome.Applied);
        Assert.True(outcome.Pinned);
        Assert.Equal(OccamToolProfile.Reader, outcome.ActiveProfile);
        Assert.Equal(OccamToolProfile.Reader, runtime.Surface.ProfileId);
    }

    [Fact]
    public void Submit_Weak_NarrowsToMinimal_WhenNotPinned()
    {
        var runtime = CreateRuntime(pinned: false);

        var outcome = runtime.Submit(WeakSubmission);

        Assert.True(outcome.Ok);
        Assert.Equal(0, outcome.Result.Score);
        Assert.True(outcome.Applied);
        Assert.Equal(OccamToolProfile.Minimal, runtime.Surface.ProfileId);
    }

    [Fact]
    public void Submit_CachesResult_AndTryApplyCached_ReusesIt()
    {
        var runtime = CreateRuntime(pinned: false);
        var first = runtime.Submit(PerfectSubmission);
        Assert.True(first.Applied);

        // Reset surface to reader; cache should still hold the strong result.
        var surface2 = new SessionToolSurface(OccamToolProfile.Reader, isPinned: false);
        var runtime2 = new ExamMcpRuntime(runtime.Cache, surface2);
        var subject = ExamSubject.Create("cursor/1.0", "mock-strong", "s-perfect");

        Assert.True(runtime2.TryApplyCached(subject, out var cached));
        Assert.Equal(AgentTier.Strong, cached.Tier);
        Assert.Equal(OccamToolProfile.Full, surface2.ProfileId);
    }

    [Fact]
    public void TryApplyCached_DoesNotApplyDefault_OnMiss()
    {
        var runtime = CreateRuntime(pinned: false);
        var subject = ExamSubject.Create("cursor/1.0", "unknown", "never");

        Assert.False(runtime.TryApplyCached(subject, out _));
        Assert.Equal(OccamToolProfile.Reader, runtime.Surface.ProfileId);

        // GetOrDefault still reports medium for API callers — without mutating the surface.
        var fallback = runtime.Cache.GetOrDefault(subject);
        Assert.Equal(AgentTier.Medium, fallback.Tier);
        Assert.Equal("default", fallback.Source);
        Assert.Equal(OccamToolProfile.Reader, runtime.Surface.ProfileId);
    }

    [Fact]
    public void Submit_InvalidJson_ReturnsInvalidArguments()
    {
        var runtime = CreateRuntime(pinned: false);

        var outcome = runtime.Submit("{not-json");

        Assert.False(outcome.Ok);
        Assert.Equal("invalid_arguments", outcome.FailureCode);
        Assert.Equal(OccamToolProfile.Reader, runtime.Surface.ProfileId);
    }

    [Fact]
    public void ExamResultDefault_MapsToBasic_ButIsNotAutoApplied()
    {
        // Contract: Default is a valid exam API value; MCP host must not demote reader→basic on miss.
        var at = DateTimeOffset.UtcNow;
        var def = ExamResult.Default(at);
        Assert.Equal(AgentTier.Medium, def.Tier);
        Assert.Equal(OccamToolProfile.Basic, ExamScoring.ProfileFor(def.Tier));

        var runtime = CreateRuntime(pinned: false);
        Assert.False(runtime.TryApplyCached(ExamSubject.Create("a", "b", "c"), out _));
        Assert.Equal(OccamToolProfile.Reader, runtime.Surface.ProfileId);
    }
}
