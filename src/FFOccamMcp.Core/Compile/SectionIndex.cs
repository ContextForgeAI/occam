using System.Text;
using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

public enum FocusMatchStatus
{
    Hit,
    Weak,
    Miss,
}

public sealed record SectionEntry(
    int Ordinal,
    int Level,
    string Heading,
    string NormalizedHeading,
    IReadOnlyList<string> AnchorIds,
    int Start,
    int Length,
    int ParentOrdinal,
    double LinkDensity,
    bool IsIndexLike,
    string Text,
    string Body);

public sealed record SectionScoreTrace(
    int Ordinal,
    string Anchor,
    int Score,
    IReadOnlyList<string> Reasons);

public sealed record FocusSelection(
    FocusMatchStatus Status,
    double Confidence,
    SectionEntry? Section,
    string? MatchedAnchor,
    IReadOnlyList<SectionScoreTrace> Trace,
    bool FragmentResolved);

/// <summary>A bounded structural index over the existing Markdown surface.</summary>
public sealed class SectionIndex
{
    private static readonly Regex HeadingPattern = new(
        @"^(#{1,6})\s+(.+?)\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex ExplicitAnchorPattern = new(
        @"\s*\{#([^}]+)\}\s*$",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MarkdownLinkPattern = new(
        @"\[[^\]]+\]\([^)]+\)",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    private SectionIndex(IReadOnlyList<SectionEntry> sections) => Sections = sections;

    public IReadOnlyList<SectionEntry> Sections { get; }

    public static SectionIndex Build(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return new SectionIndex([]);
        }

        var headings = new List<(int Start, int ContentStart, int Level, string Heading, string? ExplicitAnchor)>();
        var lineStart = 0;
        while (lineStart < markdown.Length)
        {
            var newline = markdown.IndexOf('\n', lineStart);
            var lineEnd = newline < 0 ? markdown.Length : newline;
            var line = markdown.AsSpan(lineStart, lineEnd - lineStart).TrimEnd('\r').ToString();
            var match = HeadingPattern.Match(line);
            if (match.Success)
            {
                var rawHeading = match.Groups[2].Value.Trim();
                var anchorMatch = ExplicitAnchorPattern.Match(rawHeading);
                var explicitAnchor = anchorMatch.Success ? NormalizeIdentity(anchorMatch.Groups[1].Value) : null;
                var heading = anchorMatch.Success ? rawHeading[..anchorMatch.Index].TrimEnd() : rawHeading;
                headings.Add((lineStart, newline < 0 ? lineEnd : newline + 1, match.Groups[1].Length, heading, explicitAnchor));
            }

            if (newline < 0)
            {
                break;
            }
            lineStart = newline + 1;
        }

        if (headings.Count == 0)
        {
            return new SectionIndex([
                CreateEntry(0, 0, "Document", "document", ["document"], 0, markdown.Length, -1, markdown, markdown)
            ]);
        }

        var entries = new List<SectionEntry>(headings.Count + 1);
        var anchorCounts = new Dictionary<string, int>(StringComparer.Ordinal);
        if (headings[0].Start > 0)
        {
            var preamble = markdown[..headings[0].Start].Trim();
            if (preamble.Length > 0)
            {
                entries.Add(CreateEntry(0, 0, "Document", "document", ["document"], 0, headings[0].Start, -1, preamble, preamble));
            }
        }

        var parentStack = new Stack<(int Level, int Ordinal)>();
        for (var i = 0; i < headings.Count; i++)
        {
            var heading = headings[i];
            var end = i + 1 < headings.Count ? headings[i + 1].Start : markdown.Length;
            var text = markdown[heading.Start..end].Trim();
            var body = heading.ContentStart < end ? markdown[heading.ContentStart..end].Trim() : string.Empty;
            var baseAnchor = heading.ExplicitAnchor ?? Slugify(heading.Heading);
            if (string.IsNullOrEmpty(baseAnchor))
            {
                baseAnchor = $"section-{i + 1}";
            }
            anchorCounts.TryGetValue(baseAnchor, out var prior);
            anchorCounts[baseAnchor] = prior + 1;
            var synthetic = prior == 0 ? baseAnchor : $"{baseAnchor}-{prior + 1}";
            var anchors = heading.ExplicitAnchor is null || heading.ExplicitAnchor == synthetic
                ? new[] { synthetic }
                : new[] { heading.ExplicitAnchor, synthetic };

            while (parentStack.Count > 0 && parentStack.Peek().Level >= heading.Level)
            {
                parentStack.Pop();
            }
            var ordinal = entries.Count;
            var parent = parentStack.Count == 0 ? -1 : parentStack.Peek().Ordinal;
            entries.Add(CreateEntry(
                ordinal,
                heading.Level,
                heading.Heading,
                NormalizeText(heading.Heading),
                anchors,
                heading.Start,
                end - heading.Start,
                parent,
                text,
                body));
            parentStack.Push((heading.Level, ordinal));
        }

        return new SectionIndex(entries);
    }

    private static SectionEntry CreateEntry(
        int ordinal,
        int level,
        string heading,
        string normalizedHeading,
        IReadOnlyList<string> anchors,
        int start,
        int length,
        int parent,
        string text,
        string body)
    {
        var linkCharacters = MarkdownLinkPattern.Matches(body).Sum(match => match.Length);
        var density = body.Length == 0 ? 0 : Math.Min(1.0, linkCharacters / (double)body.Length);
        var normalized = normalizedHeading;
        var indexLike = normalized.Contains("table of contents", StringComparison.Ordinal)
            || normalized.Contains("contents", StringComparison.Ordinal)
            || normalized.EndsWith(" index", StringComparison.Ordinal)
            || normalized == "index"
            || density >= 0.35;
        return new SectionEntry(
            ordinal, level, heading, normalizedHeading, anchors, start, length, parent,
            Math.Round(density, 4), indexLike, text, body);
    }

    internal static string NormalizeIdentity(string value)
    {
        var trimmed = value.Trim().TrimStart('#');
        try
        {
            return Uri.UnescapeDataString(trimmed).ToLowerInvariant();
        }
        catch (UriFormatException)
        {
            return trimmed.ToLowerInvariant();
        }
    }

    internal static string NormalizeText(string value)
    {
        var builder = new StringBuilder(value.Length);
        var separating = false;
        foreach (var c in value.Normalize(NormalizationForm.FormKC).ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(c) || c is '_' or '-' or '.')
            {
                if (separating && builder.Length > 0)
                {
                    builder.Append(' ');
                }
                builder.Append(c);
                separating = false;
            }
            else
            {
                separating = true;
            }
        }
        return builder.ToString().Trim();
    }

    private static string Slugify(string heading)
    {
        var normalized = NormalizeText(heading);
        return Regex.Replace(normalized, @"\s+", "-", RegexOptions.CultureInvariant);
    }
}

/// <summary>Deterministic local ranker over <see cref="SectionIndex"/>.</summary>
public static partial class SectionRanker
{
    public static FocusSelection Select(SectionIndex index, string? focusQuery, string? focusFragment = null)
    {
        var fragment = string.IsNullOrWhiteSpace(focusFragment)
            ? null
            : SectionIndex.NormalizeIdentity(focusFragment);
        var query = string.IsNullOrWhiteSpace(focusQuery) ? fragment : focusQuery.Trim();
        var normalizedQuery = SectionIndex.NormalizeText(query ?? string.Empty);
        var terms = TechnicalTokenPattern().Matches(query ?? string.Empty)
            .Select(match => match.Value.ToLowerInvariant())
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var traces = new List<SectionScoreTrace>(index.Sections.Count);

        foreach (var section in index.Sections)
        {
            var reasons = new List<string>(8);
            var score = 0;
            var exactFragment = fragment is not null && section.AnchorIds.Contains(fragment, StringComparer.Ordinal);
            if (exactFragment)
            {
                score += 10_000;
                reasons.Add("exact_fragment");
            }
            if (normalizedQuery.Length > 0 && section.AnchorIds.Contains(SectionIndex.NormalizeIdentity(normalizedQuery), StringComparer.Ordinal))
            {
                score += 4_000;
                reasons.Add("exact_anchor");
            }
            if (normalizedQuery.Length > 0 && section.NormalizedHeading == normalizedQuery)
            {
                score += 2_500;
                reasons.Add("exact_heading");
            }

            var headingHits = terms.Count(term => ContainsTerm(section.NormalizedHeading, term));
            if (headingHits > 0)
            {
                score += headingHits * 350;
                reasons.Add($"heading_terms:{headingHits}/{terms.Length}");
                if (headingHits == terms.Length)
                {
                    score += 700;
                    reasons.Add("heading_coverage");
                }
            }

            var bodyProbe = section.Body.Length <= 2_048 ? section.Body : section.Body[..2_048];
            var normalizedBody = SectionIndex.NormalizeText(bodyProbe);
            var bodyHits = terms.Count(term => ContainsTerm(normalizedBody, term));
            if (bodyHits > 0)
            {
                score += bodyHits * 45;
                reasons.Add($"nearby_body_terms:{bodyHits}/{terms.Length}");
            }
            if (normalizedQuery.Length > 0 && normalizedBody.Contains(normalizedQuery, StringComparison.Ordinal))
            {
                score += 160;
                reasons.Add("nearby_phrase");
            }
            if (HasDefinitionalAnswer(bodyProbe, terms))
            {
                // Prefer a specific definitional leaf without allowing repeated-term prose to
                // overtake a heading that covers the complete query.
                score += 900 + Math.Min(300, section.Level * 50);
                reasons.Add("definitional_answer");
            }
            if (score > 0 && section.Body.Length >= 32 && section.LinkDensity < 0.35)
            {
                score += 80;
                reasons.Add("answer_body");
            }
            if (section.IsIndexLike)
            {
                score -= 3_000;
                reasons.Add("index_penalty");
            }

            traces.Add(new SectionScoreTrace(
                section.Ordinal,
                section.AnchorIds.FirstOrDefault() ?? string.Empty,
                score,
                reasons));
        }

        var ranked = traces.OrderByDescending(trace => trace.Score).ThenBy(trace => trace.Ordinal).ToArray();
        var winnerTrace = ranked.FirstOrDefault();
        if (winnerTrace is null || winnerTrace.Score <= 0)
        {
            return new FocusSelection(FocusMatchStatus.Miss, 0, null, null, ranked, false);
        }
        var winner = index.Sections.First(section => section.Ordinal == winnerTrace.Ordinal);
        var resolved = fragment is not null && winner.AnchorIds.Contains(fragment, StringComparer.Ordinal);
        var strong = resolved
            || winnerTrace.Reasons.Contains("exact_anchor", StringComparer.Ordinal)
            || winnerTrace.Reasons.Contains("exact_heading", StringComparer.Ordinal)
            || winnerTrace.Reasons.Contains("heading_coverage", StringComparer.Ordinal);
        return new FocusSelection(
            strong ? FocusMatchStatus.Hit : FocusMatchStatus.Weak,
            strong ? 0.95 : 0.55,
            winner,
            resolved ? fragment : winner.AnchorIds.FirstOrDefault(),
            ranked,
            resolved);
    }

    private static bool ContainsTerm(string normalizedText, string term)
    {
        if (normalizedText.Contains(term, StringComparison.Ordinal))
        {
            return true;
        }

        // Keep deterministic English plural tolerance aligned with the legacy focus matcher.
        return term.Length > 4 && term.EndsWith('s')
            && normalizedText.Contains(term[..^1], StringComparison.Ordinal);
    }

    private static bool HasDefinitionalAnswer(string body, IReadOnlyList<string> terms)
    {
        var lower = body.ToLowerInvariant();
        string[] connectors = [" is any ", " is a ", " is an ", " refers to ", " is defined as "];
        foreach (var connector in connectors)
        {
            var at = lower.IndexOf(connector, StringComparison.Ordinal);
            while (at >= 0)
            {
                var subjectStart = Math.Max(0, at - 48);
                var subject = SectionIndex.NormalizeText(lower[subjectStart..at]);
                if (terms.Any(term => ContainsTerm(subject, term)))
                {
                    return true;
                }

                at = lower.IndexOf(connector, at + connector.Length, StringComparison.Ordinal);
            }
        }

        return false;
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[._-][\p{L}\p{N}]+)*", RegexOptions.CultureInvariant)]
    private static partial Regex TechnicalTokenPattern();
}
