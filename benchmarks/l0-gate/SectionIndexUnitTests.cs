using OccamMcp.Core.Compile;

namespace OccamMcp.L0Gate;

internal static class SectionIndexUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        const string markdown = """
            # HTTP Semantics

            ## Unauthorized deployment notes

            Unauthorized audit log prose.

            ## 15.5.2 401 Unauthorized {#section-15.5.2}

            The 401 status code indicates missing valid authentication credentials.
            """;
        var index = SectionIndex.Build(markdown);
        var numeric = SectionRanker.Select(index, "401 Unauthorized");
        assert("section index numeric heading coverage", numeric.Section?.Heading.StartsWith("15.5.2 401", StringComparison.Ordinal) == true);
        assert("section index trace observable", numeric.Trace.First().Reasons.Contains("heading_coverage"));

        var fragment = SectionRanker.Select(index, null, "section%2D15.5.2");
        assert("section index encoded fragment exact", fragment.FragmentResolved && fragment.MatchedAnchor == "section-15.5.2");
        var malformed = SectionRanker.Select(index, null, "%not-encoded");
        assert("section index malformed fragment bounded miss", malformed.Status == FocusMatchStatus.Miss && !malformed.FragmentResolved);

        const string tocMarkdown = """
            # Module

            ## client_max_body_size index

            - [client_max_body_size](#client_max_body_size)

            ## Request body limit {#client_max_body_size}

            Sets the maximum allowed size of the client request body.
            """;
        var tocIndex = SectionIndex.Build(tocMarkdown);
        var selected = SectionRanker.Select(tocIndex, "client_max_body_size");
        assert("section index exact anchor beats toc", selected.Section?.Heading == "Request body limit");
        assert("section index toc penalty traced", selected.Trace.Any(trace => trace.Reasons.Contains("index_penalty")));

        var intent = FocusIntent.FromUrl("https://example.com/rfc#section%2D15.5.2");
        assert("focus intent strips fetch fragment", intent.FetchUrl == "https://example.com/rfc");
        assert("focus intent decodes local fragment", intent.Fragment == "section-15.5.2");
    }
}
