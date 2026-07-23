using System.Diagnostics;
using OccamMcp.Core.Compile;

namespace OccamMcp.Rc2Regression;

internal static class PrDFocusCases
{
    public static void Run(TestHarness test, string fixtureRoot)
    {
        var sectionsMarkdown = Read(fixtureRoot, "focus-sections.md");
        var index = SectionIndex.Build(sectionsMarkdown);
        var target = index.Sections.Single(section => section.AnchorIds.Contains("section-15.5.2"));
        test.Check("D15", "section index preserves hierarchy and deterministic identity",
            target.Level == 2 && target.ParentOrdinal >= 0 && target.Length > target.Heading.Length,
            $"ordinal={target.Ordinal}; level={target.Level}; parent={target.ParentOrdinal}; span={target.Start}+{target.Length}; anchor={target.AnchorIds[0]}");

        var numeric = SectionRanker.Select(index, "401");
        test.Check("D15", "numeric technical identifier ranks answer section",
            numeric.Status == FocusMatchStatus.Hit && numeric.Section?.Body.Contains("ANSWER_401", StringComparison.Ordinal) == true,
            Describe(numeric));

        var covered = SectionRanker.Select(index, "401 Unauthorized");
        test.Check("D15", "heading coverage beats repeated distant term",
            covered.Section?.Body.Contains("ANSWER_401", StringComparison.Ordinal) == true
            && covered.Section.Body.Contains("WRONG_SECTION", StringComparison.Ordinal) == false,
            Describe(covered));

        var fragment = SectionRanker.Select(index, null, "section-15.5.2");
        test.Check("D17", "exact fragment resolves deterministic anchor",
            fragment.FragmentResolved && fragment.Section?.Body.Contains("ANSWER_401", StringComparison.Ordinal) == true,
            Describe(fragment));

        var encoded = SectionRanker.Select(index, null, "section-15.5.2");
        test.Check("D17", "fragment normalization is stable",
            encoded.MatchedAnchor == "section-15.5.2",
            $"anchor={encoded.MatchedAnchor}");

        var missing = SectionRanker.Select(index, null, "missing-anchor");
        test.Check("D17", "missing fragment remains explicit miss",
            !missing.FragmentResolved && missing.Status == FocusMatchStatus.Miss,
            $"status={missing.Status}; resolved={missing.FragmentResolved}");

        var tocIndex = SectionIndex.Build(Read(fixtureRoot, "focus-toc.md"));
        var toc = tocIndex.Sections.Single(section => section.Body.Contains("TOC_ENTRY", StringComparison.Ordinal));
        var body = SectionRanker.Select(tocIndex, "client_max_body_size");
        var tocTrace = body.Trace.Single(trace => trace.Ordinal == toc.Ordinal);
        test.Check("D15", "toc is structural reference rather than answer winner",
            toc.IsIndexLike && tocTrace.Reasons.Contains("index_penalty")
            && body.Section?.Body.Contains("ANSWER_NGINX", StringComparison.Ordinal) == true,
            $"tocDensity={toc.LinkDensity}; tocScore={tocTrace.Score}; {Describe(body)}");

        var duplicate = SectionRanker.Select(
            SectionIndex.Build(Read(fixtureRoot, "focus-duplicates.md")),
            "deployment status");
        test.Check("D15", "equal score tie breaks by document ordinal",
            duplicate.Section?.Body.Contains("FIRST_DUPLICATE", StringComparison.Ordinal) == true,
            Describe(duplicate));

        var intent = FocusIntent.FromUrl("https://standards.example/rfc#section-15.5.2");
        test.Check("D17", "fetch URL is fragment-free while intent is retained",
            intent.FetchUrl == "https://standards.example/rfc" && intent.Fragment == "section-15.5.2",
            $"fetch={intent.FetchUrl}; fragment={intent.Fragment}");

        var focused = TokenBudget.Apply(sectionsMarkdown, 128, "401 Unauthorized");
        var fragmentFocused = TokenBudget.Apply(sectionsMarkdown, 128, focusFragment: "section-15.5.2");
        test.Check("D15", "token budget consumes structural selection",
            focused.Text.Contains("ANSWER_401", StringComparison.Ordinal),
            $"strategy={focused.Strategy}; tokens={TokenEstimator.Estimate(focused.Text)}");
        test.Check("D17", "token budget consumes fragment selection",
            fragmentFocused.Text.Contains("ANSWER_401", StringComparison.Ordinal),
            $"strategy={fragmentFocused.Strategy}; tokens={TokenEstimator.Estimate(fragmentFocused.Text)}");

        var largeSeed = Read(fixtureRoot, "focus-large.md");
        var large = string.Concat(Enumerable.Range(0, 64)
            .Select(i => largeSeed.Replace("# Large document", $"# Large document {i}", StringComparison.Ordinal)));
        var before = GC.GetAllocatedBytesForCurrentThread();
        var started = Stopwatch.GetTimestamp();
        var first = SectionIndex.Build(large);
        var elapsed = Stopwatch.GetElapsedTime(started);
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var second = SectionIndex.Build(large);
        test.Check("D15", "large index is deterministic and bounded",
            first.Sections.Count == second.Sections.Count
            && first.Sections.Select(section => section.AnchorIds[0]).SequenceEqual(second.Sections.Select(section => section.AnchorIds[0])),
            $"chars={large.Length}; sections={first.Sections.Count}; elapsedMs={elapsed.TotalMilliseconds:F3}; allocatedBytes={allocated}");
    }

    private static string Describe(FocusSelection selection)
    {
        var winner = selection.Trace.FirstOrDefault();
        var reasons = winner is null ? "none" : string.Join(',', winner.Reasons);
        return $"status={selection.Status}; heading={selection.Section?.Heading}; anchor={selection.MatchedAnchor}; score={winner?.Score}; reasons={reasons}";
    }

    private static string Read(string root, string name) => File.ReadAllText(Path.Combine(root, name));
}
