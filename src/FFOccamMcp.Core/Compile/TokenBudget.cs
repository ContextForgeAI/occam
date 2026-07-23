namespace OccamMcp.Core.Compile;

public static class TokenBudget
{
    private const string SandwichMarker = "\n\n…\n\n";

    public static (string Text, bool Truncated, string? Strategy) Apply(
        string text,
        int? maxTokens,
        string? focusQuery = null,
        string? focusFragment = null)
    {
        if (maxTokens is null)
        {
            return (text, false, null);
        }

        if (TokenEstimator.Estimate(text) <= maxTokens.Value)
        {
            return (text, false, null);
        }

        if (!string.IsNullOrWhiteSpace(focusQuery) || !string.IsNullOrWhiteSpace(focusFragment))
        {
            var focused = TruncateFocusCentered(text, maxTokens.Value, focusQuery ?? focusFragment!, focusFragment);
            if (!string.IsNullOrWhiteSpace(focused))
            {
                return (focused, true, "focus_window");
            }

            var sandwich = TruncateSandwichSafe(text, maxTokens.Value, SandwichMarker, 0.55, 0.40);
            return (sandwich, true, "sandwich");
        }

        var head = TruncateHeadSafe(text, maxTokens.Value);
        return (head, true, "head_safe");
    }

    internal static string TruncateFocusCentered(
        string text,
        int maxTokens,
        string focusQuery,
        string? focusFragment = null)
    {
        if (string.IsNullOrWhiteSpace(text) || string.IsNullOrWhiteSpace(focusQuery))
        {
            return string.Empty;
        }

        var anchor = TryExtractDefinitionalAnchorParagraph(text, focusQuery);

        var selection = SectionRanker.Select(SectionIndex.Build(text), focusQuery, focusFragment);
        if (selection.Section is null)
        {
            return anchor is null ? string.Empty : FitWithDefinitionalAnchor(anchor, string.Empty, maxTokens);
        }

        var selected = selection.Section.Text;
        anchor = anchor is not null && selected.Contains(anchor, StringComparison.Ordinal)
            ? anchor
            : null;
        var bodyBudget = anchor is null
            ? maxTokens
            : Math.Max(96, maxTokens - TokenEstimator.Estimate(anchor) - 6);
        var body = TokenEstimator.Estimate(selected) <= bodyBudget
            ? selected
            : TruncateSectionFocusAware(selected, bodyBudget, focusQuery);
        var result = FitWithDefinitionalAnchor(anchor, body, maxTokens);
        var unchosenCount = selection.Trace.Count(trace =>
            trace.Score > 0 && trace.Ordinal != selection.Section.Ordinal);
        if (unchosenCount == 0)
        {
            return result;
        }

        var marker = $"\n\n<!-- SNIP: {unchosenCount} unchosen (reason: budget_exceeded) -->";
        if (TokenEstimator.Estimate(result + marker) <= maxTokens)
        {
            return result + marker;
        }

        var contentBudget = Math.Max(1, maxTokens - TokenEstimator.Estimate(marker));
        return TruncateHeadSafe(result, contentBudget) + marker;
    }

    private static int ScoreSection(string section, string focusQuery)
    {
        var heading = section.Split('\n', 2)[0];
        var headingLower = heading.ToLowerInvariant();
        var terms = FocusMatcher.Tokenize(focusQuery);
        var score = 0;

        if (FocusMatcher.MatchesMarkdown(section, focusQuery))
        {
            score = 10;
            // A section whose heading names the queried concept is more on-topic than one that only
            // mentions it in passing.
            score += terms.Count(term => headingLower.Contains(term, StringComparison.Ordinal));

            // Leaf headings (###) are usually the most specific match for a concept.
            if (heading.StartsWith("### ", StringComparison.Ordinal))
            {
                score += 3;
            }
        }
        else if (FocusMatcher.MatchesMarkdown(heading, focusQuery))
        {
            score = 2;
        }

        // A section that both matches the query AND reads definitionally ("X is a…", "X refers to…")
        // is the best answer to a "what is <concept>" focus. Reward it generically — no per-topic
        // keyword — enough to lift a real definition above sibling sections that merely mention the
        // term. The stronger boost applies when the definition's subject is itself the queried term.
        if (score > 0 && WantsDefinitionalFocus(focusQuery) && ContainsDefinitionalProse(section))
        {
            score += DefinitionSubjectMatchesQuery(section.ToLowerInvariant(), terms) ? 12 : 4;
        }

        return score;
    }

    // Generic definitional connectors — the shapes an English definition takes ("X is a…",
    // "X refers to…"). Replaces the old per-concept keyword list that named "closure" literally.
    private static readonly string[] DefinitionalConnectors =
        [" is any ", " is a ", " is an ", " is the ", " are the ", " refers to ", " is defined as "];

    // Any real topical focus query may have a definition worth preserving; whether a given block is
    // actually a definition is decided by ContainsDefinitionalProse / the anchor finder, not by a
    // hardcoded concept list. Gate only on the query carrying a usable content term.
    private static bool WantsDefinitionalFocus(string focusQuery) =>
        FocusMatcher.Tokenize(focusQuery).Any(term => term.Length >= 4);

    private static bool ContainsDefinitionalProse(string section) =>
        DefinitionalConnectors.Any(c => section.Contains(c, StringComparison.OrdinalIgnoreCase));

    // True when a definitional sentence's subject is (a stem of) a queried term — i.e. the text
    // defines the thing the user asked about, not merely some other term in a matching section.
    private static bool DefinitionSubjectMatchesQuery(string lowerText, List<string> terms)
    {
        foreach (var connector in DefinitionalConnectors)
        {
            var at = lowerText.IndexOf(connector, StringComparison.Ordinal);
            while (at >= 0)
            {
                var windowStart = Math.Max(0, at - 40);
                var window = lowerText[windowStart..at];
                if (terms.Any(term => term.Length >= 4 && LooseContains(window, term)))
                {
                    return true;
                }

                at = lowerText.IndexOf(connector, at + connector.Length, StringComparison.Ordinal);
            }
        }

        return false;
    }

    // Substring match tolerant of a single trailing English plural 's' on the query term, so a query
    // for "closures"/"functions" still matches prose that says "closure"/"function". Language-neutral
    // scripts fall back to the plain substring test.
    private static bool LooseContains(string lowerText, string term)
    {
        if (lowerText.Contains(term, StringComparison.Ordinal))
        {
            return true;
        }

        return term.Length > 4 && term.EndsWith('s')
            && lowerText.Contains(term[..^1], StringComparison.Ordinal);
    }

    internal static string PreserveDefinitionalAnchor(
        string sourceMarkdown,
        string compiledMarkdown,
        int? maxTokens,
        string? focusQuery)
    {
        if (maxTokens is null || string.IsNullOrWhiteSpace(focusQuery))
        {
            return compiledMarkdown;
        }

        var anchor = TryExtractDefinitionalAnchorParagraph(sourceMarkdown, focusQuery);
        if (anchor is null)
        {
            // No anchor found — but still enforce token budget
            if (TokenEstimator.Estimate(compiledMarkdown) > maxTokens.Value)
            {
                return TruncateHeadSafe(compiledMarkdown, maxTokens.Value);
            }
            return compiledMarkdown;
        }
        return FitWithDefinitionalAnchor(anchor, compiledMarkdown, maxTokens.Value);
    }

    private static string? TryExtractDefinitionalAnchorParagraph(string text, string focusQuery)
    {
        var terms = FocusMatcher.Tokenize(focusQuery).Where(t => t.Length >= 4).ToList();
        if (terms.Count == 0)
        {
            return null;
        }

        // The best anchor is the paragraph that both mentions a queried term and reads as a
        // definition. Score by how many query terms it carries, with a bonus when the definition's
        // subject is the queried term itself; keep the strongest. Replaces the old literal search
        // for "a closure is any piece of source code" (one MDN golden case).
        string? best = null;
        var bestScore = 0;
        foreach (var para in SplitParagraphs(text))
        {
            if (!ContainsDefinitionalProse(para))
            {
                continue;
            }

            var lower = para.ToLowerInvariant();
            var termHits = terms.Count(term => LooseContains(lower, term));
            if (termHits == 0)
            {
                continue;
            }

            var score = (termHits * 2) + (DefinitionSubjectMatchesQuery(lower, terms) ? 5 : 0);
            if (score > bestScore)
            {
                bestScore = score;
                best = para.Trim();
            }
        }

        return best;
    }

    private static IEnumerable<string> SplitParagraphs(string text) =>
        text.Split("\n\n", StringSplitOptions.RemoveEmptyEntries)
            .Select(p => p.Trim())
            .Where(p => p.Length > 0);

    // True when the chosen body already contains the anchor's opening sentence, so prepending the
    // anchor would duplicate it. Generic replacement for the old literal "any piece of source code"
    // check.
    private static bool AnchorAlreadyPresent(string body, string? anchor)
    {
        if (string.IsNullOrWhiteSpace(body) || string.IsNullOrWhiteSpace(anchor))
        {
            return false;
        }

        var probe = FirstSentence(anchor);
        return probe.Length >= 16 && body.Contains(probe, StringComparison.OrdinalIgnoreCase);
    }

    private static string FirstSentence(string text)
    {
        var trimmed = text.Trim();
        var dot = trimmed.IndexOf(". ", StringComparison.Ordinal);
        var end = dot >= 16 ? dot : Math.Min(trimmed.Length, 80);
        return trimmed[..end];
    }

    private static string FitWithDefinitionalAnchor(string? anchor, string body, int maxTokens)
    {
        if (string.IsNullOrWhiteSpace(anchor))
        {
            return string.IsNullOrWhiteSpace(body)
                ? string.Empty
                : TokenEstimator.Estimate(body) <= maxTokens
                    ? body
                    : TruncateHeadSafe(body, maxTokens);
        }

        if (AnchorAlreadyPresent(body, anchor))
        {
            return TokenEstimator.Estimate(body) <= maxTokens
                ? body
                : TruncateHeadSafe(body, maxTokens);
        }

        var merged = string.IsNullOrWhiteSpace(body) ? anchor : $"{anchor}\n\n{body}";
        if (TokenEstimator.Estimate(merged) <= maxTokens)
        {
            return merged;
        }

        var anchorTokens = TokenEstimator.Estimate(anchor);
        var bodyBudget = Math.Max(48, maxTokens - anchorTokens - 4);
        if (string.IsNullOrWhiteSpace(body))
        {
            return TruncateHeadSafe(anchor, maxTokens);
        }

        var trimmedBody = TruncateHeadSafe(body, bodyBudget);
        merged = $"{anchor}\n\n{trimmedBody}";
        return TokenEstimator.Estimate(merged) <= maxTokens
            ? merged
            : TruncateHeadSafe(merged, maxTokens);
    }

    internal static string TruncateSectionFocusAware(string section, int maxTokens, string focusQuery)
    {
        if (string.IsNullOrWhiteSpace(section) || maxTokens <= 0)
        {
            return string.Empty;
        }

        if (TokenEstimator.Estimate(section) <= maxTokens)
        {
            return section;
        }

        var localIndex = SectionIndex.Build(section);
        var localSection = localIndex.Sections.FirstOrDefault();
        var minimum = localSection is null ? null : AnswerUnitSelector.Select(localSection, focusQuery);
        if (minimum is not null && minimum.Tokens <= maxTokens)
        {
            var protectedBlocks = AnswerUnitSelector.SplitBlocks(minimum.Text);
            var extras = AnswerUnitSelector.SplitBlocks(section)
                .Where(block => !protectedBlocks.Contains(block, StringComparer.Ordinal))
                .Select((block, ordinal) => new
                {
                    Text = block,
                    Ordinal = ordinal,
                    Score = ScoreBodyBlock(block, focusQuery, WantsDefinitionalFocus(focusQuery),
                        block.StartsWith("```", StringComparison.Ordinal)),
                })
                .OrderByDescending(item => item.Score)
                .ThenBy(item => item.Ordinal);
            var retained = new List<string>(protectedBlocks);
            foreach (var extra in extras)
            {
                var trial = string.Join("\n\n", retained.Append(extra.Text));
                if (TokenEstimator.Estimate(trial) <= maxTokens)
                {
                    retained.Add(extra.Text);
                }
            }
            return string.Join("\n\n", retained);
        }

        var definitional = WantsDefinitionalFocus(focusQuery);
        var lines = section.Split('\n');
        var headingLines = new List<string>();
        var bodyBlocks = new List<(string Text, int Score)>();

        var block = new List<string>();
        void FlushBlock()
        {
            if (block.Count == 0)
            {
                return;
            }

            var text = string.Join('\n', block).Trim();
            block.Clear();
            if (text.Length == 0)
            {
                return;
            }

            if (text.StartsWith('#'))
            {
                headingLines.Add(text);
                return;
            }

            var isCode = text.StartsWith("```", StringComparison.Ordinal);
            var score = ScoreBodyBlock(text, focusQuery, definitional, isCode);
            bodyBlocks.Add((text, score));
        }

        foreach (var line in lines)
        {
            if (line.StartsWith("```", StringComparison.Ordinal) && block.Count > 0
                && block[0].StartsWith("```", StringComparison.Ordinal))
            {
                block.Add(line);
                FlushBlock();
                continue;
            }

            if (line.StartsWith("```", StringComparison.Ordinal))
            {
                FlushBlock();
            }

            block.Add(line);
            if (line.StartsWith("```", StringComparison.Ordinal) && block.Count > 1)
            {
                FlushBlock();
            }
        }

        FlushBlock();

        var ordered = bodyBlocks.OrderByDescending(b => b.Score).ToList();
        var result = new List<string>(headingLines);
        foreach (var (text, _) in ordered)
        {
            var trial = string.Join("\n\n", result.Append(text));
            if (TokenEstimator.Estimate(trial) <= maxTokens)
            {
                result.Add(text);
            }
        }

        if (result.Count > headingLines.Count)
        {
            return string.Join("\n\n", result);
        }

        var definitionalBlock = ordered.FirstOrDefault(b => b.Score >= 8).Text;
        if (!string.IsNullOrEmpty(definitionalBlock))
        {
            var withHeading = string.Join("\n\n", headingLines.Append(definitionalBlock));
            if (TokenEstimator.Estimate(withHeading) <= maxTokens)
            {
                return withHeading;
            }

            return TruncateHeadSafe(withHeading, maxTokens);
        }

        return TruncateHeadSafe(section, maxTokens);
    }

    private static int ScoreBodyBlock(string text, string focusQuery, bool definitionalFocus, bool isCode)
    {
        if (isCode)
        {
            return definitionalFocus ? 0 : 3;
        }

        var score = FocusMatcher.MatchesMarkdown(text, focusQuery) ? 6 : 0;

        // Generic definitional-shape boost (no per-topic literal). A block that states what the
        // concept *is* outranks sibling prose that only mentions it.
        if (text.Contains(" is any ", StringComparison.OrdinalIgnoreCase))
        {
            score += 10;
        }
        else if (text.Contains(" is a ", StringComparison.OrdinalIgnoreCase)
                 || text.Contains(" is an ", StringComparison.OrdinalIgnoreCase))
        {
            score += 6;
        }
        else if (text.Contains(" refers to ", StringComparison.OrdinalIgnoreCase)
                 || text.Contains(" is defined as ", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        return score;
    }

    private static List<string> SplitMarkdownSections(string text)
    {
        var sections = new List<string>();
        var current = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if ((line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal))
                && current.Count > 0)
            {
                sections.Add(string.Join('\n', current).Trim());
                current = [line];
                continue;
            }

            current.Add(line);
        }

        if (current.Count > 0)
        {
            sections.Add(string.Join('\n', current).Trim());
        }

        return sections.Where(section => section.Length > 0).ToList();
    }

    private const string HeadSafeSnipMarker = "\n\n<!-- SNIP: tail (reason: head_safe) -->";

    internal static string TruncateHeadSafe(string text, int maxTokens)
    {
        if (string.IsNullOrEmpty(text) || maxTokens <= 0)
        {
            return string.Empty;
        }

        var snipMarkerTokens = TokenEstimator.Estimate(HeadSafeSnipMarker);
        // Script-aware char budget (not maxTokens*4): a CJK/Cyrillic body is far denser than ASCII,
        // so the flat inverse over-kept several times the token budget on non-Latin pages.
        var maxChars = TokenEstimator.CharBudgetForTokens(text, Math.Max(1, maxTokens - snipMarkerTokens));
        if (text.Length <= maxChars)
        {
            return text;
        }

        var cut = FindSafeCutIndex(text, maxChars, fromStart: true);
        return text[..cut].TrimEnd() + HeadSafeSnipMarker;
    }

    internal static string TruncateSandwichSafe(
        string text,
        int maxTokens,
        string marker,
        double headRatio,
        double tailRatio)
    {
        if (string.IsNullOrEmpty(text) || maxTokens <= 0)
        {
            return string.Empty;
        }

        if (TokenEstimator.Estimate(text) <= maxTokens)
        {
            return text;
        }

        // Split the token budget (minus the marker we ACTUALLY insert) between head and tail by ratio,
        // then convert each share to a character count with the script-aware walker — a flat
        // maxTokens*4 char budget over-kept non-Latin text several-fold. The SandwichMarker ("\n\n…\n\n")
        // is swapped for a much longer HTML SNIP comment, so its own tokens are reserved here.
        var snipMarker = marker == SandwichMarker ? "\n\n<!-- SNIP: middle (reason: budget_exceeded) -->\n\n" : marker;
        var markerTokens = TokenEstimator.Estimate(snipMarker);
        var contentTokens = Math.Max(1, maxTokens - markerTokens);
        var headTokens = Math.Max(1, (int)Math.Floor(contentTokens * headRatio));
        var tailTokens = Math.Max(0, (int)Math.Floor(contentTokens * tailRatio));
        if (headTokens + tailTokens > contentTokens)
        {
            tailTokens = Math.Max(0, contentTokens - headTokens);
        }

        var headChars = TokenEstimator.CharBudgetForTokens(text, headTokens);
        var tailChars = tailTokens > 0 ? TokenEstimator.CharBudgetForTokensFromEnd(text, tailTokens) : 0;

        var headCut = FindSafeCutIndex(text, Math.Min(headChars, text.Length), fromStart: true);
        var headText = text[..headCut];

        var tailStartPreferred = Math.Max(headCut, text.Length - tailChars);
        var tailStart = FindSafeCutIndex(text, tailStartPreferred, fromStart: false);
        if (tailStart <= headCut)
        {
            return TruncateHeadSafe(text, maxTokens);
        }

        var tailText = text[tailStart..].TrimStart('\n');
        return headText.TrimEnd() + snipMarker + tailText;
    }

    private static int FindSafeCutIndex(string text, int preferredIndex, bool fromStart)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var cut = Math.Clamp(preferredIndex, 1, text.Length);
        if (fromStart)
        {
            var lastNewline = text.LastIndexOf('\n', cut - 1);
            if (lastNewline > cut / 2)
            {
                cut = lastNewline;
            }
        }
        else
        {
            var nextNewline = text.IndexOf('\n', cut);
            if (nextNewline >= 0 && nextNewline < text.Length - 1 && nextNewline <= cut + 64)
            {
                cut = nextNewline + 1;
            }
        }

        cut = AdjustForWordBoundary(text, cut);
        cut = AdjustForMarkdownLink(text, cut);
        cut = AdjustForUrlParenthesis(text, cut);
        return Math.Clamp(cut, 1, text.Length);
    }

    private static int AdjustForUrlParenthesis(string text, int cut)
    {
        if (cut <= 0 || cut >= text.Length)
        {
            return cut;
        }

        var scanStart = Math.Max(0, cut - 240);
        for (var open = text.LastIndexOf('(', cut - 1); open >= scanStart; open = text.LastIndexOf('(', open - 1))
        {
            if (open + 1 >= text.Length)
            {
                continue;
            }

            var slice = text.AsSpan(open + 1, Math.Min(8, text.Length - open - 1));
            if (!slice.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                && !slice.StartsWith("/en", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var close = text.IndexOf(')', open + 1);
            if (close < 0 || cut <= open || cut >= close)
            {
                continue;
            }

            return open;
        }

        return cut;
    }

    private static int AdjustForWordBoundary(string text, int cut)
    {
        if (cut <= 0 || cut >= text.Length)
        {
            return cut;
        }

        if (char.IsLetterOrDigit(text[cut - 1]) && char.IsLetterOrDigit(text[cut]))
        {
            var lastSpace = text.LastIndexOf(' ', cut - 1);
            if (lastSpace > cut / 2)
            {
                return lastSpace;
            }
        }

        return cut;
    }

    private static int AdjustForMarkdownLink(string text, int cut)
    {
        for (var scan = Math.Min(cut, text.Length - 1); scan >= Math.Max(0, cut - 240); scan--)
        {
            if (text[scan] != '[')
            {
                continue;
            }

            var linkEnd = FindMarkdownLinkEnd(text, scan);
            if (linkEnd > cut)
            {
                return scan;
            }

            break;
        }

        return cut;
    }

    private static int FindMarkdownLinkEnd(string text, int openBracket)
    {
        var closeBracket = text.IndexOf(']', openBracket + 1);
        if (closeBracket < 0 || closeBracket + 1 >= text.Length || text[closeBracket + 1] != '(')
        {
            return openBracket;
        }

        var closeParen = text.IndexOf(')', closeBracket + 2);
        return closeParen >= 0 ? closeParen + 1 : openBracket;
    }
}
