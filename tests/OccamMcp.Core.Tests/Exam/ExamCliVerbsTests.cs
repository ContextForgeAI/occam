using System.Text.Json;
using OccamMcp.Core.Exam;
using OccamMcp.Core.Transport;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

/// <summary>
/// The shipped <c>occam exam</c> verbs. Running them in-process keeps the checks that a bare machine
/// executes and the checks CI executes as the same code, so the cross-platform evidence in
/// <c>docs/testing/</c> cannot diverge from the test suite.
/// </summary>
public sealed class ExamCliVerbsTests
{
    [Fact]
    public void SelftestPassesInProcess()
    {
        Assert.Equal(0, ExamCliVerbs.Selftest());
    }

    [Fact]
    public void TryRunDispatchesSelftest()
    {
        Assert.True(ExamCliVerbs.TryRun(["exam", "selftest"], out var exitCode));
        Assert.Equal(0, exitCode);
    }

    [Fact]
    public void TryRunDispatchesTasks()
    {
        Assert.True(ExamCliVerbs.TryRun(["exam", "tasks"], out var exitCode));
        Assert.Equal(0, exitCode);
    }

    [Theory]
    [InlineData("nonsense")]
    [InlineData("help")]
    public void UnknownSubverbIsAUsageError(string subverb)
    {
        Assert.True(ExamCliVerbs.TryRun(["exam", subverb], out var exitCode));
        Assert.Equal(2, exitCode);
    }

    [Fact]
    public void BareExamVerbPrintsUsage()
    {
        Assert.True(ExamCliVerbs.TryRun(["exam"], out var exitCode));
        Assert.Equal(2, exitCode);
    }

    [Theory]
    [InlineData("canary")]
    [InlineData("verify")]
    public void UnrelatedVerbsAreNotClaimed(string verb)
    {
        Assert.False(ExamCliVerbs.TryRun([verb], out _));
    }

    [Fact]
    public void NoArgumentsAreNotClaimed()
    {
        Assert.False(ExamCliVerbs.TryRun([], out _));
    }

    // ---- task catalogue as JSON ----

    [Fact]
    public void EmittedCatalogueDescribesEveryTask()
    {
        using var document = JsonDocument.Parse(ExamCliVerbs.EmitTasks());
        var root = document.RootElement;

        Assert.Equal(ExamResult.MaxScore, root.GetProperty("maxScore").GetInt32());

        var tasks = root.GetProperty("tasks").EnumerateArray().ToArray();
        Assert.Equal(ExamTasks.All.Count, tasks.Length);
        Assert.All(tasks, task =>
        {
            Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("id").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("prompt").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("passCondition").GetString()));
            Assert.False(string.IsNullOrWhiteSpace(task.GetProperty("measures").GetString()));
        });
    }

    [Fact]
    public void EmittedCatalogueAgreesWithTheTieringCode()
    {
        // A harness reads this JSON to administer the exam; if it disagreed with ExamScoring, the
        // surface an agent was promised and the surface it got would differ.
        using var document = JsonDocument.Parse(ExamCliVerbs.EmitTasks());
        var tiers = document.RootElement.GetProperty("tiers");

        foreach (var tier in Enum.GetValues<AgentTier>())
        {
            var entry = tiers.GetProperty(ExamWire.ToWire(tier));
            Assert.Equal(ExamScoring.ProfileFor(tier), entry.GetProperty("profile").GetString());
            Assert.Equal(ExamScoring.SurfaceSizeFor(tier), entry.GetProperty("toolCount").GetInt32());
        }
    }

    [Fact]
    public void EmittedCatalogueUsesLfNewlinesOnEveryPlatform()
    {
        // Same defect class as the canary vector file: Utf8JsonWriter defaults its newline to
        // Environment.NewLine, which makes an emitted artefact non-portable.
        var json = ExamCliVerbs.EmitTasks();

        Assert.DoesNotContain('\r', json);
        Assert.EndsWith("\n", json, StringComparison.Ordinal);
    }

    [Fact]
    public void EmittedCatalogueNeverNamesAToolTheTierCannotSee()
    {
        using var document = JsonDocument.Parse(ExamCliVerbs.EmitTasks());
        var prompts = string.Join(
            '\n',
            document.RootElement.GetProperty("tasks").EnumerateArray()
                .Select(t => t.GetProperty("prompt").GetString()));

        // Prompts are given to a client at exam time, when it holds the default medium surface.
        var exposed = OccamToolProfile.GetExposedToolNames(ExamScoring.ProfileFor(AgentTier.Medium));
        foreach (var tool in OccamMcpServerRegistration.OccamToolNames)
        {
            if (!exposed.Contains(tool))
            {
                Assert.DoesNotContain(tool, prompts, StringComparison.Ordinal);
            }
        }
    }

    // ---- wire spellings ----

    [Fact]
    public void EveryTaskHasADistinctWireSpelling()
    {
        var spellings = Enum.GetValues<ExamTaskId>().Select(ExamWire.ToWire).ToArray();

        Assert.Equal(spellings.Length, spellings.Distinct(StringComparer.Ordinal).Count());
        Assert.All(spellings, s => Assert.False(string.IsNullOrWhiteSpace(s)));
    }

    [Theory]
    [InlineData(ExamTaskId.Canary, "canary")]
    [InlineData(ExamTaskId.BasicCall, "basic_call")]
    [InlineData(ExamTaskId.FocusBudget, "focus_budget")]
    [InlineData(ExamTaskId.Chain, "chain")]
    public void TaskWireSpellingsAreStable(ExamTaskId task, string expected)
    {
        Assert.Equal(expected, ExamWire.ToWire(task));
    }

    [Fact]
    public void UnknownTaskFallsBackRatherThanThrowing()
    {
        Assert.False(string.IsNullOrWhiteSpace(ExamWire.ToWire((ExamTaskId)99)));
    }
}
