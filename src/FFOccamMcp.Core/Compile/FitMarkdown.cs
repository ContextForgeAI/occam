using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

/// <summary>BM25-inspired paragraph prune (Wave 11, Crawl4AI Fit MD analogue).</summary>
public static partial class FitMarkdown
{
    private const int FocusedListLineCap = 30;

    private static readonly string[] BoilerplateTerms =
    [
        "subscribe", "newsletter", "sign up", "share on", "follow us", "all rights reserved",
        "accept all cookies", "advertisement", "sponsored", "related articles",
        "was this page helpful", "was this helpful", "edit this page", "improve this page", "report an issue",
        "skip to main content", "skip to content", "cookie preferences", "privacy policy",
        "terms of use", "terms of service", "minutes to read", "min read",
        "previous post", "next post", "read more", "continue reading",
        "carbon ads", "ads by carbon", "advertisement from carbon", "ads via carbon",
        "carbonads.net", "help improve mdn", "view this page on github",
        "learn how to contribute", "this page was last modified", "mdn contributors",
        "page was last modified", "report a problem with this content",
    ];

    public static string Apply(string markdown, string? focusQuery = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return markdown;
        }

        var blocks = SplitBlocks(markdown);
        var paragraphs = blocks.Where(b => b.Kind == BlockKind.Paragraph).ToList();
        if (paragraphs.Count <= 2)
        {
            return markdown;
        }

        var focused = !string.IsNullOrWhiteSpace(focusQuery);
        var docFreq = ComputeDocFreq(paragraphs);
        var queryTerms = ExtractQueryTerms(blocks, focusQuery);
        var focusTerms = ExtractFocusTerms(focusQuery);
        var avgDl = paragraphs.Average(p => Math.Max(1, ScoringWordCount(p)));
        var minScore = focused ? 0.06 : 0.12;
        var minHeadingScore = focused ? 0.035 : 0.08;

        var sections = BuildSections(blocks);
        var lenientIndexMode = DetectLenientIndexMode(sections);

        var sectionKeeps = sections
            .Select(section => new SectionKeepPlan(
                section,
                PruneSectionBody(
                    section,
                    queryTerms,
                    docFreq,
                    paragraphs.Count,
                    avgDl,
                    focusQuery,
                    focusTerms,
                    focused,
                    minScore,
                    minHeadingScore,
                    lenientIndexMode)))
            .ToList();

        var anyBodyKept = sectionKeeps.Any(plan => plan.KeptBody.Count > 0);
        var kept = new List<Block>();
        foreach (var plan in sectionKeeps)
        {
            var heading = plan.Section.Heading;
            var keepHeading = heading is null;
            if (heading is not null)
            {
                var tagWeight = HeadingTagWeight(heading.HeadingLevel);
                var headingTerms = focused ? focusTerms : queryTerms;
                var headingScore = Bm25Score(heading, headingTerms, docFreq, paragraphs.Count, avgDl) * tagWeight;
                keepHeading = !IsBoilerplateHeading(heading.Text)
                    && (plan.KeptBody.Count > 0
                        || (heading.HeadingLevel == 1 && anyBodyKept)
                        || (focused && (headingScore >= minHeadingScore || FocusMatcher.MatchesMarkdown(heading.Text, focusQuery))));
            }

            if (keepHeading && heading is not null)
            {
                kept.Add(heading);
            }

            kept.AddRange(plan.KeptBody);
        }

        // AF-2: inject SNIP markers for fully-dropped sections (only when result still shrinks vs original)
        if (focused && kept.Count >= 2)
        {
            var snipBlocks = new List<Block>();
            foreach (var plan in sectionKeeps)
            {
                if (!plan.SectionDropped) continue;
                var heading = plan.Section.Heading;
                var droppedTokens = plan.Section.Body.Sum(b => Math.Max(1, FocusMatcher.Tokenize(b.Text).Count));
                snipBlocks.Add(new Block(
                    $"<!-- SNIP: ({plan.Section.Body.Count} paragraphs, {droppedTokens} tokens, reason: bm25_below_threshold) -->",
                    BlockKind.Paragraph));
            }
            if (snipBlocks.Count > 0)
            {
                var withSnip = kept.Concat(snipBlocks).ToList();
                var withSnipLen = string.Join("\n\n", withSnip.Select(b => b.Text)).Length;
                if (withSnipLen < markdown.Length)
                {
                    kept.InsertRange(kept.Count, snipBlocks);
                }
            }
        }

        if (kept.Count < 2)
        {
            if (focused)
            {
                var fallback = kept.Count > 0
                    ? string.Join("\n\n", kept.Select(b => b.Text))
                    : ExtractLeadingIntro(blocks);
                if (string.IsNullOrWhiteSpace(fallback))
                {
                    fallback = ExtractHubFallback(blocks);
                }

                return fallback;
            }

            return markdown;
        }

        return string.Join("\n\n", kept.Select(b => b.Text));
    }

    private static string ExtractLeadingIntro(IReadOnlyList<Block> blocks)
    {
        var intro = new List<string>();
        foreach (var block in blocks)
        {
            if (block.Kind == BlockKind.Heading)
            {
                break;
            }

            if (block.Kind != BlockKind.Paragraph)
            {
                continue;
            }

            var text = FilterBoilerplateLines(block.Text);
            if (!string.IsNullOrWhiteSpace(text))
            {
                intro.Add(text);
            }
        }

        return intro.Count > 0 ? string.Join("\n\n", intro) : string.Empty;
    }

    /// <summary>When focus prune removes all BM25 hits, keep a short hub excerpt (honest focusMatched may still be false).</summary>
    private static string ExtractHubFallback(IReadOnlyList<Block> blocks)
    {
        var parts = new List<string>();
        var paraCount = 0;

        foreach (var block in blocks)
        {
            if (block.Kind == BlockKind.Heading)
            {
                if (block.HeadingLevel <= 2 && parts.Count == 0 && paraCount == 0)
                {
                    parts.Add(block.Text);
                }
                else if (block.HeadingLevel == 2 && paraCount > 0)
                {
                    break;
                }

                continue;
            }

            if (block.Kind != BlockKind.Paragraph)
            {
                continue;
            }

            var text = FilterBoilerplateLines(block.Text);
            if (string.IsNullOrWhiteSpace(text) || text.Length < 40)
            {
                continue;
            }

            parts.Add(text);
            paraCount += 1;
            if (paraCount >= 3)
            {
                break;
            }
        }

        return parts.Count > 0 ? string.Join("\n\n", parts) : string.Empty;
    }

    internal static double HeadingTagWeight(int level) => level switch
    {
        1 => 1.2,
        2 => 1.1,
        3 => 1.0,
        4 => 0.9,
        5 => 0.8,
        6 => 0.7,
        _ => 1.0,
    };

    private static List<Block> PruneSectionBody(
        MarkdownSection section,
        HashSet<string> queryTerms,
        Dictionary<string, int> docFreq,
        int paragraphCount,
        double avgDl,
        string? focusQuery,
        HashSet<string> focusTerms,
        bool focused,
        double minScore,
        double minHeadingScore,
        bool lenientIndexMode)
    {
        var body = section.Body;
        var tagWeight = section.HeadingLevel > 0 ? HeadingTagWeight(section.HeadingLevel) : 1.0;
        var sectionHasHeading = section.Heading is not null;
        var sectionMatchesFocus = focused
            && section.Heading is not null
            && (FocusMatcher.MatchesMarkdown(section.Heading.Text, focusQuery)
                || Bm25Score(section.Heading, focusTerms, docFreq, paragraphCount, avgDl) * tagWeight >= minHeadingScore);
        var sectionHasListFocus = focused && SectionHasMatchingListItem(section, focusQuery);

        var kept = new List<Block>();
        var keptIntroParagraph = false;
        var focusedListLinesKept = 0;

        foreach (var block in body)
        {
            if (block.Kind == BlockKind.Heading)
            {
                kept.Add(block);
                continue;
            }

            if (block.Kind != BlockKind.Paragraph)
            {
                kept.Add(block);
                continue;
            }

            var paragraphText = FilterBoilerplateLines(block.Text);
            var isLinkList = IsLinkListBlock(paragraphText);
            if (string.IsNullOrWhiteSpace(paragraphText)
                || (!isLinkList && FactDensityFilter.IsLowValueBlock(paragraphText)))
            {
                continue;
            }

            var paragraph = new Block(paragraphText, BlockKind.Paragraph);
            var isList = isLinkList;

            if (isList && (lenientIndexMode || sectionMatchesFocus || sectionHasListFocus))
            {
                if (focused)
                {
                    var filtered = FilterListLinesByFocus(
                        paragraph.Text,
                        focusQuery,
                        FocusedListLineCap - focusedListLinesKept);
                    if (string.IsNullOrWhiteSpace(filtered))
                    {
                        continue;
                    }

                    focusedListLinesKept += filtered.Split('\n').Count(l => !string.IsNullOrWhiteSpace(l));
                    kept.Add(new Block(filtered, BlockKind.Paragraph));
                }
                else
                {
                    kept.Add(paragraph);
                }

                continue;
            }

            if (!focused && sectionHasHeading && !keptIntroParagraph && !isList)
            {
                kept.Add(paragraph);
                keptIntroParagraph = true;
                continue;
            }

            if (!isList && IsHighLinkDensity(paragraph.Text))
            {
                continue;
            }

            if (!isList && paragraph.WordCount < 6)
            {
                continue;
            }

            var scoreBlock = isList ? ScoringBlock(paragraph) : paragraph;
            var score = Bm25Score(scoreBlock, queryTerms, docFreq, paragraphCount, avgDl) * tagWeight;
            if (score >= minScore || (focused && FocusMatcher.MatchesMarkdown(paragraph.Text, focusQuery)))
            {
                kept.Add(paragraph);
            }
        }

        return kept;
    }

    private static bool DetectLenientIndexMode(IReadOnlyList<MarkdownSection> sections)
    {
        var level2Sections = sections.Where(s => s.HeadingLevel == 2).ToList();
        if (level2Sections.Count >= 2)
        {
            var indexLikeSections = level2Sections.Count(section =>
                (section.Heading is not null && LinkRegex().IsMatch(section.Heading.Text))
                || section.Body.Any(b => b.Kind == BlockKind.Paragraph && IsIndexLinkListBlock(b.Text)));
            if (indexLikeSections >= Math.Max(2, (int)Math.Ceiling(level2Sections.Count * 0.5)))
            {
                return true;
            }
        }

        var totalParagraphs = 0;
        var indexLinkLists = 0;

        foreach (var section in sections)
        {
            if (section.HeadingLevel != 2)
            {
                continue;
            }

            foreach (var block in section.Body.Where(b => b.Kind == BlockKind.Paragraph))
            {
                totalParagraphs++;
                if (IsIndexLinkListBlock(block.Text))
                {
                    indexLinkLists++;
                }
            }
        }

        if (totalParagraphs == 0)
        {
            var allParagraphs = sections
                .SelectMany(s => s.Body)
                .Where(b => b.Kind == BlockKind.Paragraph)
                .ToList();
            totalParagraphs = allParagraphs.Count;
            indexLinkLists = allParagraphs.Count(b => IsIndexLinkListBlock(b.Text));
        }

        return totalParagraphs > 0 && indexLinkLists >= totalParagraphs * 0.4;
    }

    private static bool SectionHasMatchingListItem(MarkdownSection section, string? focusQuery)
    {
        foreach (var block in section.Body.Where(b => b.Kind == BlockKind.Paragraph))
        {
            if (!IsLinkListBlock(block.Text))
            {
                continue;
            }

            foreach (var line in NonEmptyLines(block.Text).Where(IsListLine))
            {
                if (FocusMatcher.MatchesMarkdown(line, focusQuery))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static bool IsIndexLinkListBlock(string text) =>
        IsLinkListBlock(text) && IsPredominantlyLinkListLines(text);

    private static bool IsLinkListBlock(string text) =>
        IsListBlock(text) || IsPredominantlyLinkListLines(text);

    private static bool IsPredominantlyLinkListLines(string text)
    {
        var lines = NonEmptyLines(text);
        if (lines.Count == 0)
        {
            return false;
        }

        var listLinkLines = lines.Count(line => IsListLine(line) && LinkRegex().IsMatch(line));
        return listLinkLines >= Math.Max(1, (int)Math.Ceiling(lines.Count * 0.5));
    }

    private static bool IsListBlock(string text)
    {
        var lines = NonEmptyLines(text);
        if (lines.Count == 0)
        {
            return false;
        }

        var listLines = lines.Count(IsListLine);
        return listLines >= Math.Max(1, (int)Math.Ceiling(lines.Count * 0.5));
    }

    private static bool IsListLine(string line)
    {
        var trimmed = line.TrimStart();
        return trimmed.StartsWith('-') || trimmed.StartsWith('*') || OrderedListRegex().IsMatch(trimmed);
    }

    private static string FilterListLinesByFocus(string text, string? focusQuery, int maxLines)
    {
        if (maxLines <= 0)
        {
            return string.Empty;
        }

        var kept = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line) || !IsListLine(line))
            {
                continue;
            }

            if (!FocusMatcher.MatchesMarkdown(line, focusQuery))
            {
                continue;
            }

            kept.Add(line);
            if (kept.Count >= maxLines)
            {
                break;
            }
        }

        return string.Join('\n', kept);
    }

    private static Block ScoringBlock(Block paragraph)
    {
        var anchorText = ExtractAnchorText(paragraph.Text);
        return string.IsNullOrWhiteSpace(anchorText) ? paragraph : new Block(anchorText, BlockKind.Paragraph);
    }

    private static string ExtractAnchorText(string text)
    {
        var anchors = LinkRegex().Matches(text)
            .Select(m => m.Groups[1].Value.Trim())
            .Where(a => a.Length > 0)
            .ToList();
        return anchors.Count > 0 ? string.Join(' ', anchors) : text;
    }

    private static int ScoringWordCount(Block block) => FocusMatcher.Tokenize(ScoringBlock(block).Text).Count;

    private static List<MarkdownSection> BuildSections(IReadOnlyList<Block> blocks)
    {
        var sections = new List<MarkdownSection>();
        MarkdownSection? current = null;

        foreach (var block in blocks)
        {
            if (block.Kind == BlockKind.Heading)
            {
                current = new MarkdownSection { Heading = block };
                sections.Add(current);
                continue;
            }

            if (current is null)
            {
                current = new MarkdownSection();
                sections.Add(current);
            }

            current.Body.Add(block);
        }

        return sections;
    }

    private static List<Block> SplitBlocks(string markdown)
    {
        var blocks = new List<Block>();
        var current = new List<string>();
        var inFence = false;

        foreach (var line in markdown.Split('\n'))
        {
            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                if (inFence)
                {
                    current.Add(line);
                    blocks.Add(new Block(string.Join('\n', current), BlockKind.Code));
                    current.Clear();
                    inFence = false;
                }
                else
                {
                    FlushParagraph(blocks, current);
                    inFence = true;
                    current.Add(line);
                }

                continue;
            }

            if (inFence)
            {
                current.Add(line);
                continue;
            }

            var trimmedLine = line.TrimStart();
            if (trimmedLine.StartsWith('#'))
            {
                FlushParagraph(blocks, current);
                blocks.Add(new Block(trimmedLine, BlockKind.Heading, ParseHeadingLevel(trimmedLine)));
                continue;
            }

            if (string.IsNullOrWhiteSpace(line))
            {
                FlushParagraph(blocks, current);
                continue;
            }

            current.Add(line);
        }

        FlushParagraph(blocks, current);
        if (inFence && current.Count > 0)
        {
            blocks.Add(new Block(string.Join('\n', current), BlockKind.Code));
        }

        return blocks;
    }

    private static int ParseHeadingLevel(string headingLine)
    {
        var level = 0;
        while (level < headingLine.Length && level < 6 && headingLine[level] == '#')
        {
            level++;
        }

        return level is >= 1 and <= 6 ? level : 0;
    }

    private static void FlushParagraph(List<Block> blocks, List<string> current)
    {
        if (current.Count == 0)
        {
            return;
        }

        blocks.Add(new Block(string.Join('\n', current), BlockKind.Paragraph));
        current.Clear();
    }

    private static List<string> NonEmptyLines(string text) =>
        text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line)).ToList();

    private static HashSet<string> ExtractFocusTerms(string? focusQuery)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return terms;
        }

        foreach (var term in FocusMatcher.Tokenize(focusQuery))
        {
            terms.Add(term);
        }

        return terms;
    }

    private static HashSet<string> ExtractQueryTerms(IReadOnlyList<Block> blocks, string? focusQuery)
    {
        var terms = new HashSet<string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(focusQuery))
        {
            foreach (var term in FocusMatcher.Tokenize(focusQuery))
            {
                terms.Add(term);
            }

            return terms;
        }

        foreach (var block in blocks.Where(b => b.Kind == BlockKind.Heading))
        {
            foreach (var term in FocusMatcher.Tokenize(block.Text))
            {
                terms.Add(term);
            }
        }

        if (terms.Count == 0)
        {
            foreach (var term in FocusMatcher.Tokenize(blocks[0].Text).Take(12))
            {
                terms.Add(term);
            }
        }

        return terms;
    }

    private static Dictionary<string, int> ComputeDocFreq(IReadOnlyList<Block> paragraphs)
    {
        var freq = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var paragraph in paragraphs)
        {
            var scoring = IsLinkListBlock(paragraph.Text) ? ScoringBlock(paragraph) : paragraph;
            foreach (var term in FocusMatcher.Tokenize(scoring.Text).Distinct(StringComparer.Ordinal))
            {
                freq.TryGetValue(term, out var count);
                freq[term] = count + 1;
            }
        }

        return freq;
    }

    private static double Bm25Score(
        Block block,
        HashSet<string> queryTerms,
        Dictionary<string, int> docFreq,
        int docCount,
        double avgDl)
    {
        const double k1 = 1.2;
        const double b = 0.75;
        var dl = Math.Max(1, FocusMatcher.Tokenize(block.Text).Count);
        var score = 0.0;

        foreach (var term in FocusMatcher.Tokenize(block.Text).Distinct(StringComparer.Ordinal))
        {
            if (!queryTerms.Contains(term))
            {
                continue;
            }

            docFreq.TryGetValue(term, out var df);
            var idf = Math.Log(1 + (docCount - df + 0.5) / (df + 0.5));
            var tf = block.Text.Split(term, StringSplitOptions.None).Length - 1;
            var numerator = tf * (k1 + 1);
            var denominator = tf + k1 * (1 - b + b * dl / avgDl);
            score += idf * numerator / denominator;
        }

        return score;
    }

    private static string FilterBoilerplateLines(string text)
    {
        var lines = text.Split('\n');
        var kept = lines.Where(line => !string.IsNullOrWhiteSpace(line) && !IsBoilerplate(line)).ToList();
        return string.Join('\n', kept);
    }

    private static bool IsBoilerplate(string text)
    {
        var lower = text.ToLowerInvariant();
        return BoilerplateTerms.Any(term => lower.Contains(term, StringComparison.Ordinal));
    }

    private static bool IsBoilerplateHeading(string headingText)
    {
        var stripped = headingText.TrimStart('#', ' ').Trim();
        return IsBoilerplate(stripped);
    }

    private static bool IsHighLinkDensity(string text)
    {
        var links = LinkRegex().Matches(text).Count;
        var words = Math.Max(1, FocusMatcher.Tokenize(text).Count);
        return links >= 2 && links * 5 >= words;
    }

    private enum BlockKind { Heading, Paragraph, Code }

    private sealed record Block(string Text, BlockKind Kind, int HeadingLevel = 0)
    {
        public int WordCount => FocusMatcher.Tokenize(Text).Count;
    }

    private sealed class MarkdownSection
    {
        public Block? Heading { get; init; }
        public List<Block> Body { get; } = [];
        public int HeadingLevel => Heading?.HeadingLevel ?? 0;
    }

    private sealed record SectionKeepPlan(MarkdownSection Section, List<Block> KeptBody)
    {
        public bool SectionDropped => KeptBody.Count == 0 && Section.Heading is not null;
    }

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex LinkRegex();

    [GeneratedRegex(@"^\d+\.\s", RegexOptions.CultureInvariant)]
    private static partial Regex OrderedListRegex();
}
