using OccamMcp.Core.Exam;
using OccamMcp.Core.Transport;
using Xunit;

namespace OccamMcp.Core.Tests.Exam;

/// <summary>
/// The two mappings that decide what an agent can see: score → tier, and tier → tool surface. Both
/// thresholds are asserted at their boundaries, because an off-by-one here silently widens or
/// starves a surface.
/// </summary>
public sealed class ExamTieringTests
{
    [Theory]
    [InlineData(0, AgentTier.Weak)]
    [InlineData(1, AgentTier.Weak)]
    [InlineData(2, AgentTier.Medium)]
    [InlineData(3, AgentTier.Medium)]
    [InlineData(4, AgentTier.Strong)]
    public void ScoreMapsToTierAtEveryBoundary(int score, AgentTier expected)
    {
        Assert.Equal(expected, ExamScoring.TierFor(score));
    }

    [Theory]
    [InlineData(-100)]
    [InlineData(-1)]
    public void NegativeScoresClampToWeak(int score)
    {
        Assert.Equal(AgentTier.Weak, ExamScoring.TierFor(score));
    }

    [Theory]
    [InlineData(5)]
    [InlineData(1_000)]
    public void ScoresAboveTheMaximumClampToStrong(int score)
    {
        Assert.Equal(AgentTier.Strong, ExamScoring.TierFor(score));
    }

    [Fact]
    public void OnlyAPerfectScoreReachesTheWidestSurface()
    {
        // Widening is the risky direction: a client that mis-selects among fifteen tools burns the
        // user's turn, so the bar for the full surface is the highest one available.
        Assert.NotEqual(AgentTier.Strong, ExamScoring.TierFor(ExamResult.MaxScore - 1));
        Assert.Equal(AgentTier.Strong, ExamScoring.TierFor(ExamResult.MaxScore));
    }

    [Theory]
    [InlineData(AgentTier.Weak, 1)]
    [InlineData(AgentTier.Medium, 3)]
    public void NarrowTiersSeeExactlyTheDocumentedNumberOfTools(AgentTier tier, int expected)
    {
        Assert.Equal(expected, ExamScoring.SurfaceSizeFor(tier));
    }

    [Fact]
    public void StrongSeesTheWholeCatalogue()
    {
        Assert.Equal(
            OccamMcpServerRegistration.OccamToolNames.Length,
            ExamScoring.SurfaceSizeFor(AgentTier.Strong));
    }

    [Fact]
    public void SurfacesAreNested()
    {
        // A promotion must never remove a tool the agent already had.
        var weak = OccamToolProfile.GetExposedToolNames(ExamScoring.ProfileFor(AgentTier.Weak));
        var medium = OccamToolProfile.GetExposedToolNames(ExamScoring.ProfileFor(AgentTier.Medium));
        var strong = OccamToolProfile.GetExposedToolNames(ExamScoring.ProfileFor(AgentTier.Strong));

        Assert.All(weak, tool => Assert.Contains(tool, medium));
        Assert.All(medium, tool => Assert.Contains(tool, strong));
    }

    [Fact]
    public void TheSingleToolAWeakAgentGetsIsThePageReader()
    {
        var weak = OccamToolProfile.GetExposedToolNames(OccamToolProfile.Minimal);

        Assert.Equal(["occam"], weak);
    }

    [Fact]
    public void NarrowSurfacesCannotReachPlaybookAuthoring()
    {
        foreach (var profile in new[] { OccamToolProfile.Minimal, OccamToolProfile.Basic })
        {
            Assert.False(OccamToolProfile.IsExposed("occam_playbook_heal", profile));
            Assert.False(OccamToolProfile.IsExposed("occam_playbook_save", profile));
        }
    }

    [Fact]
    public void EveryProfileExposesOnlyRealTools()
    {
        foreach (var profile in new[]
        {
            OccamToolProfile.Minimal, OccamToolProfile.Basic, OccamToolProfile.Reader,
            OccamToolProfile.Researcher, OccamToolProfile.Auditor, OccamToolProfile.Full,
        })
        {
            foreach (var tool in OccamToolProfile.GetExposedToolNames(profile))
            {
                Assert.Contains(tool, OccamMcpServerRegistration.OccamToolNames);
            }
        }
    }

    [Fact]
    public void NewProfilesAreAccepted()
    {
        Assert.Single(OccamToolProfile.GetExposedToolNames(OccamToolProfile.Minimal));
        Assert.Equal(3, OccamToolProfile.GetExposedToolNames(OccamToolProfile.Basic).Length);
    }

    [Fact]
    public void NarrowProfilesGetTheirOwnInstructions()
    {
        var minimal = OccamServerInstructions.TextFor(OccamToolProfile.Minimal);
        var basic = OccamServerInstructions.TextFor(OccamToolProfile.Basic);
        var full = OccamServerInstructions.TextFor(OccamToolProfile.Full);

        Assert.NotEqual(full, minimal);
        Assert.NotEqual(full, basic);
        Assert.NotEqual(minimal, basic);
    }

    [Fact]
    public void NoProfileAdvertisesAToolItDoesNotExpose()
    {
        // The general invariant, rather than a hand-written deny list: instructions are the one
        // thing the model reads on connect, so naming a tool that is absent from tools/list is how
        // an agent ends up calling something it does not have.
        var violations = new List<string>();

        foreach (var profile in new[]
        {
            OccamToolProfile.Minimal, OccamToolProfile.Basic, OccamToolProfile.Reader,
            OccamToolProfile.Researcher, OccamToolProfile.Auditor, OccamToolProfile.Full,
        })
        {
            var instructions = OccamServerInstructions.TextFor(profile);
            var exposed = OccamToolProfile.GetExposedToolNames(profile);

            foreach (var tool in OccamMcpServerRegistration.OccamToolNames)
            {
                if (!exposed.Contains(tool) && instructions.Contains(tool, StringComparison.Ordinal))
                {
                    violations.Add($"{profile} instructions mention hidden tool {tool}");
                }
            }
        }

        Assert.Empty(violations);
    }

    [Fact]
    public void EveryProfileKeepsTheTrustRule()
    {
        // Narrowing the surface must not narrow the honesty contract: `ok:false` still means the
        // page is unknown, whatever the tier.
        foreach (var profile in new[]
        {
            OccamToolProfile.Minimal, OccamToolProfile.Basic, OccamToolProfile.Reader,
            OccamToolProfile.Researcher, OccamToolProfile.Auditor, OccamToolProfile.Full,
        })
        {
            var instructions = OccamServerInstructions.TextFor(profile);

            Assert.Contains("TRUST RULE", instructions, StringComparison.Ordinal);
            Assert.Contains("ok:false", instructions, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void BasicInstructionsMentionExactlyItsThreeTools()
    {
        var basic = OccamServerInstructions.TextFor(OccamToolProfile.Basic);

        foreach (var exposed in OccamToolProfile.GetExposedToolNames(OccamToolProfile.Basic))
        {
            Assert.Contains(exposed, basic, StringComparison.Ordinal);
        }

        Assert.DoesNotContain("occam_playbook_heal", basic, StringComparison.Ordinal);
        Assert.DoesNotContain("occam_map", basic, StringComparison.Ordinal);
    }

    [Fact]
    public void AnUnknownProfileFallsBackToTheDefaultSurface()
    {
        // Resolve() falls back to reader with a stderr note; GetExposedToolNames must not throw.
        var tools = OccamToolProfile.GetExposedToolNames("nonsense");

        Assert.NotEmpty(tools);
    }

    [Fact]
    public void TierWireSpellingsRoundTrip()
    {
        foreach (var tier in Enum.GetValues<AgentTier>())
        {
            Assert.Equal(tier, ExamWire.ParseTier(ExamWire.ToWire(tier)));
        }
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("nonsense")]
    [InlineData("STRONG")]
    public void UnknownTierSpellingsParseSafely(string? value)
    {
        // "STRONG" is deliberately included: parsing is case-insensitive, so it must round-trip to
        // Strong rather than silently degrading.
        var parsed = ExamWire.ParseTier(value);

        Assert.Equal(
            string.Equals(value, "STRONG", StringComparison.OrdinalIgnoreCase) ? AgentTier.Strong : AgentTier.Medium,
            parsed);
    }
}
