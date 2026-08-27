using OccamMcp.Core.Compile;

namespace OccamMcp.L0Gate;

internal static class MustContainAndTocUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var md = "# Install\n\nRun doctor then smoke.\n\n## Verify\n\nGate must pass.\n";
        var toc = TocBuilder.Build(md);
        assert("toc: at least one heading", toc.Count >= 1);
        assert("toc: first heading Install", toc[0].Heading.Contains("Install", StringComparison.Ordinal));

        var hit = MustContainMatcher.Evaluate(md, "doctor");
        assert("must_contain: MATCH", hit.Verdict == "MATCH" && hit.HitCount >= 1);
        assert("must_contain: excerpt present", hit.Excerpts.Count >= 1);

        var miss = MustContainMatcher.Evaluate(md, "definitely-not-present-xyz");
        assert("must_contain: NO_MATCH", miss.Verdict == "NO_MATCH" && miss.HitCount == 0);

        Console.WriteLine("L_TOC_MUST_CONTAIN_OK");
    }
}
