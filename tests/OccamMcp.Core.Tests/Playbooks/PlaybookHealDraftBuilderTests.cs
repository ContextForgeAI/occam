using OccamMcp.Core.Playbooks;
using Xunit;

namespace OccamMcp.Core.Tests.Playbooks;

/// <summary>
/// Golden-output tests for the mechanical heal draft.
/// </summary>
/// <remarks>
/// These exist to make a specific refactor safe. The builder serialised through reflection-based
/// <c>JsonSerializer.Serialize</c>, which raised IL2026/IL3050 on an AOT-compiled path — a real
/// trimming hazard on shipped code. Switching to a source-generated context is only safe if the
/// emitted bytes do not move, and this file pins them. See ADR-0013 for why the warnings-as-errors
/// flag follows the tests rather than leading them.
/// </remarks>
public sealed class PlaybookHealDraftBuilderTests
{
    private static PlaybookHealAnchors Anchors(params (string Selector, double Score)[] candidates) =>
        new(
            Landmarks: [],
            DataTestIds: [],
            MainCandidates: [.. candidates.Select(c => new MainCandidateAnchor(c.Selector, TextAnchor: null, c.Score))]);

    [Fact]
    public void DraftJsonIsByteStable()
    {
        var json = PlaybookHealDraftBuilder.TryBuildJson(
            "https://www.Example.com/docs/page",
            Anchors(("main.content", 0.91), ("article", 0.62)));

        // The em-dash is emitted as \u2014: the default encoder escapes non-ASCII. Pinned verbatim
        // rather than normalised, because a serializer change that stopped escaping it would be a
        // wire change to a shipped artefact.
        Assert.Equal(
            """
            {"schema_version":"1.0","id":"example.com","hosts":["example.com"],"meta":{"title":"heal draft for example.com","tags":["heal_draft"]},"routing":{"preferred_backend":"browser"},"extract":{"contentSelectors":["main.content","article"]},"agent_notes":"Mechanical stub from occam_playbook_heal mainCandidates \u2014 review selectors before occam_playbook_save."}
            """,
            json);
    }

    [Fact]
    public void SelectorsAreOrderedByScoreAndCappedAtThree()
    {
        var json = PlaybookHealDraftBuilder.TryBuildJson(
            "https://example.com",
            Anchors(("d", 0.50), ("a", 0.99), ("c", 0.70), ("b", 0.80)));

        Assert.NotNull(json);
        Assert.Contains("""["a","b","c"]""", json, StringComparison.Ordinal);
        Assert.DoesNotContain("\"d\"", json, StringComparison.Ordinal);
    }

    [Fact]
    public void LowScoringCandidatesAreDropped()
    {
        // 0.45 is the documented inclusion floor.
        Assert.Null(PlaybookHealDraftBuilder.TryBuildJson("https://example.com", Anchors(("weak", 0.44))));
        Assert.NotNull(PlaybookHealDraftBuilder.TryBuildJson("https://example.com", Anchors(("edge", 0.45))));
    }

    [Fact]
    public void DuplicateSelectorsCollapse()
    {
        var json = PlaybookHealDraftBuilder.TryBuildJson(
            "https://example.com",
            Anchors(("main", 0.9), ("main", 0.8), (" main ", 0.7)));

        Assert.Contains("""["main"]""", json, StringComparison.Ordinal);
    }

    [Fact]
    public void HostIsNormalised()
    {
        var json = PlaybookHealDraftBuilder.TryBuildJson(
            "https://WWW.Example.COM./docs",
            Anchors(("main", 0.9)));

        // www. prefix, trailing dot and case are all stripped.
        Assert.Contains("""{"schema_version":"1.0","id":"example.com","hosts":["example.com"]""", json, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("not a url")]
    [InlineData("/relative")]
    [InlineData("ftp://example.com")]
    [InlineData("file:///etc/passwd")]
    public void NonHttpUrlsProduceNoDraft(string url)
    {
        Assert.Null(PlaybookHealDraftBuilder.TryBuildJson(url, Anchors(("main", 0.9))));
    }

    [Fact]
    public void MissingAnchorsProduceNoDraft()
    {
        Assert.Null(PlaybookHealDraftBuilder.TryBuildJson("https://example.com", anchors: null));
        Assert.Null(PlaybookHealDraftBuilder.TryBuildJson("https://example.com", Anchors()));
    }

    [Fact]
    public void BlankSelectorsAreIgnored()
    {
        Assert.Null(PlaybookHealDraftBuilder.TryBuildJson(
            "https://example.com",
            Anchors(("   ", 0.9))));
    }
}
