using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

[Flags]
public enum MarkdownSectionKind
{
    None = 0,
    Headings = 1,
    Paragraphs = 2,
    Code = 4,
    Tables = 8,
    Lists = 16,
}

public static partial class MarkdownContentFilter
{
    public static ContentFilterResult ApplyWithMeta(string markdown, IReadOnlyList<string>? contentSelectors)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new ContentFilterResult(markdown, true);
        }

        var blocks = SplitBlocks(markdown);
        if (blocks.Count == 0)
        {
            return new ContentFilterResult(markdown, true);
        }

        var (scoped, selectorsMatched) = ScopeBySelectors(blocks, contentSelectors);
        if (contentSelectors is { Count: > 0 } && !selectorsMatched)
        {
            return new ContentFilterResult(string.Empty, false);
        }

        return new ContentFilterResult(JoinBlocks(scoped), selectorsMatched);
    }

    public sealed record ContentFilterResult(string Text, bool SelectorsMatched);

    private static (List<MarkdownBlock> Blocks, bool SelectorsMatched) ScopeBySelectors(
        List<MarkdownBlock> blocks,
        IReadOnlyList<string>? selectors)
    {
        if (selectors is null || selectors.Count == 0)
        {
            return (blocks, true);
        }

        var sections = BuildSections(blocks);
        var kept = new HashSet<int>();
        foreach (var selector in selectors)
        {
            if (string.IsNullOrWhiteSpace(selector))
            {
                continue;
            }

            var index = FindSectionIndex(sections, selector.Trim());
            if (index >= 0)
            {
                kept.Add(index);
            }
        }

        if (kept.Count == 0)
        {
            return ([], false);
        }

        var result = new List<MarkdownBlock>();
        foreach (var sectionIndex in kept.Order())
        {
            result.AddRange(sections[sectionIndex].Blocks);
        }

        return (result, true);
    }

    private static int FindSectionIndex(IReadOnlyList<MarkdownSection> sections, string selector)
    {
        var headingNeedle = selector.StartsWith('#')
            ? selector.TrimStart('#').Trim()
            : selector;

        for (var i = 0; i < sections.Count; i++)
        {
            var heading = sections[i].Heading;
            if (heading is null)
            {
                continue;
            }

            if (selector.StartsWith('#'))
            {
                if (HeadingMatchesPrefix(heading.Text, selector))
                {
                    return i;
                }

                continue;
            }

            if (heading.Text.Contains(headingNeedle, StringComparison.OrdinalIgnoreCase))
            {
                return i;
            }
        }

        return -1;
    }

    private static bool HeadingMatchesPrefix(string headingLine, string selectorPrefix)
    {
        var normalizedHeading = headingLine.Trim();
        var normalizedSelector = selectorPrefix.Trim();
        return normalizedHeading.StartsWith(normalizedSelector, StringComparison.OrdinalIgnoreCase)
            || normalizedHeading.TrimStart('#').TrimStart()
                .StartsWith(normalizedSelector.TrimStart('#').TrimStart(), StringComparison.OrdinalIgnoreCase);
    }

    private static List<MarkdownSection> BuildSections(List<MarkdownBlock> blocks)
    {
        var sections = new List<MarkdownSection>();
        var current = new List<MarkdownBlock>();
        MarkdownBlock? currentHeading = null;

        foreach (var block in blocks)
        {
            if (block.Kind == MarkdownSectionKind.Headings)
            {
                if (current.Count > 0 || currentHeading is not null)
                {
                    sections.Add(new MarkdownSection(currentHeading, current));
                    current = [];
                }

                currentHeading = block;
                current.Add(block);
                continue;
            }

            current.Add(block);
        }

        if (current.Count > 0 || currentHeading is not null)
        {
            sections.Add(new MarkdownSection(currentHeading, current));
        }

        return sections;
    }

    private static List<MarkdownBlock> SplitBlocks(string markdown)
    {
        var blocks = new List<MarkdownBlock>();
        var lines = markdown.Split('\n');
        var index = 0;
        while (index < lines.Length)
        {
            var line = lines[index];
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                var fence = new List<string> { line };
                index++;
                while (index < lines.Length)
                {
                    fence.Add(lines[index]);
                    if (lines[index].StartsWith("```", StringComparison.Ordinal))
                    {
                        index++;
                        break;
                    }

                    index++;
                }

                blocks.Add(new MarkdownBlock(string.Join('\n', fence), MarkdownSectionKind.Code));
                continue;
            }

            var trimmed = line.TrimStart();
            if (trimmed.StartsWith('#'))
            {
                blocks.Add(new MarkdownBlock(trimmed, MarkdownSectionKind.Headings));
                index++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                index++;
                continue;
            }

            var paragraph = new List<string>();
            while (index < lines.Length)
            {
                var peek = lines[index];
                if (string.IsNullOrWhiteSpace(peek)
                    || peek.TrimStart().StartsWith('#')
                    || peek.StartsWith("```", StringComparison.Ordinal))
                {
                    break;
                }

                paragraph.Add(peek);
                index++;
            }

            if (paragraph.Count > 0)
            {
                blocks.Add(new MarkdownBlock(string.Join('\n', paragraph), MarkdownSectionKind.Paragraphs));
            }
        }

        return blocks;
    }

    private static string JoinBlocks(IReadOnlyList<MarkdownBlock> blocks) =>
        string.Join("\n\n", blocks.Select(b => b.Text).Where(t => !string.IsNullOrWhiteSpace(t)));

    private sealed record MarkdownBlock(string Text, MarkdownSectionKind Kind);

    private sealed record MarkdownSection(MarkdownBlock? Heading, List<MarkdownBlock> Blocks);
}
