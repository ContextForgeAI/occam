using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

/// <summary>Shared focus-query matching for fit_markdown and digest honesty signals.</summary>
public static partial class FocusMatcher
{
    /// <summary>
    /// Digest focus evaluation tiers:
    /// <list type="bullet">
    /// <item><description><c>phrase</c> — contiguous query substring (ideal).</description></item>
    /// <item><description><c>ideal</c> — every query term hits (exact / stem / synonym).</description></item>
    /// <item><description><c>partial</c> — enough terms hit for multi-term queries (see <see cref="PartialHitThreshold"/>).</description></item>
    /// <item><description><c>none</c> — below threshold (including single-term-only overlap on 2-term queries).</description></item>
    /// </list>
    /// </summary>
    public sealed record FocusMatchEvaluation(bool Matched, double Score, string Tier, int Terms, int Hits);

    public static bool Matches(string text, string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var lower = text.ToLowerInvariant();
        var queryLower = focusQuery.Trim().ToLowerInvariant();
        if (queryLower.Length >= 4 && lower.Contains(queryLower, StringComparison.Ordinal))
        {
            return true;
        }

        return Tokenize(focusQuery).Any(term => term.Length >= 4 && lower.Contains(term, StringComparison.Ordinal));
    }

    /// <summary>Match focus against visible anchor/label text — strips markdown links and heading marks.</summary>
    public static bool MatchesMarkdown(string text, string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery) || string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var stripped = MarkdownLinkRegex().Replace(text, "$1");
        stripped = HeadingMarkRegex().Replace(stripped, string.Empty);
        return Matches(stripped.Trim(), focusQuery);
    }

    /// <summary>
    /// Digest <c>focusMatched</c>: scored match with stem + synonym tolerance.
    /// Boolean is <see cref="FocusMatchEvaluation.Matched"/> — true for phrase / ideal / partial tiers.
    /// </summary>
    public static bool MatchesForDigest(string text, string? focusQuery) =>
        EvaluateForDigest(text, focusQuery).Matched;

    /// <summary>Full digest focus evaluation (score + tier) for tests and diagnostics.</summary>
    public static FocusMatchEvaluation EvaluateForDigest(string text, string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery) || string.IsNullOrWhiteSpace(text))
        {
            return new FocusMatchEvaluation(false, 0, "none", 0, 0);
        }

        var visible = StripMarkdownForDigest(text);
        if (visible.Length == 0)
        {
            return new FocusMatchEvaluation(false, 0, "none", 0, 0);
        }

        var searchBlob = BuildDigestSearchBlob(text).ToLowerInvariant();
        var queryLower = focusQuery.Trim().ToLowerInvariant();

        // Ideal path A: contiguous phrase (len ≥ 4) appears as written.
        if (queryLower.Length >= 4 && searchBlob.Contains(queryLower, StringComparison.Ordinal))
        {
            var phraseTerms = Tokenize(focusQuery);
            return new FocusMatchEvaluation(true, 1.0, "phrase", phraseTerms.Count, phraseTerms.Count);
        }

        var terms = Tokenize(focusQuery)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        if (terms.Count == 0)
        {
            var weak = Matches(visible, focusQuery);
            return new FocusMatchEvaluation(weak, weak ? 1.0 : 0, weak ? "ideal" : "none", 0, 0);
        }

        var corpusTokens = Tokenize(searchBlob);
        var hits = 0;
        foreach (var term in terms)
        {
            if (TermHitsCorpus(term, searchBlob, corpusTokens))
            {
                hits++;
            }
        }

        var score = (double)hits / terms.Count;
        if (hits == terms.Count)
        {
            return new FocusMatchEvaluation(true, 1.0, "ideal", terms.Count, hits);
        }

        // Two-term queries stay strict: a single shared word on a hub/TOC is not enough
        // (historical honesty: MDN "syntax" alone must not match "configuration syntax").
        if (terms.Count == 1)
        {
            return new FocusMatchEvaluation(false, score, "none", terms.Count, hits);
        }

        if (terms.Count == 2)
        {
            return new FocusMatchEvaluation(false, score, "none", terms.Count, hits);
        }

        // Partial: enough of a longer query (synonym/stem tolerant) clearly overlaps the excerpt.
        var need = PartialHitThreshold(terms.Count);
        if (hits >= need)
        {
            return new FocusMatchEvaluation(true, score, "partial", terms.Count, hits);
        }

        return new FocusMatchEvaluation(false, score, "none", terms.Count, hits);
    }

    /// <summary>
    /// Minimum term hits for a partial match when the query has ≥3 terms:
    /// <c>max(2, ceil(2n/3))</c> — majority-ish, never a single accidental word.
    /// </summary>
    public static int PartialHitThreshold(int termCount)
    {
        if (termCount <= 2)
        {
            return termCount; // 0/1/2 → must hit all (caller special-cases)
        }

        return Math.Max(2, (int)Math.Ceiling(termCount * 2.0 / 3.0));
    }

    private static bool TermHitsCorpus(string term, string searchBlob, List<string> corpusTokens)
    {
        _ = searchBlob; // blob is already tokenized into corpusTokens (visible + URL segments)
        foreach (var candidate in ExpandTerm(term))
        {
            foreach (var token in corpusTokens)
            {
                if (SoftTermEquals(token, candidate))
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// Exact, prefix, or long shared-stem equality. "configuration" ≈ "configure" (LCP "configur").
    /// </summary>
    internal static bool SoftTermEquals(string a, string b)
    {
        if (a.Length == 0 || b.Length == 0)
        {
            return false;
        }

        if (a.Equals(b, StringComparison.Ordinal))
        {
            return true;
        }

        // Prefix: shorter must be ≥4 so "con" does not match "configuration".
        var shorter = a.Length <= b.Length ? a : b;
        var longer = a.Length <= b.Length ? b : a;
        if (shorter.Length >= 4 && longer.StartsWith(shorter, StringComparison.Ordinal))
        {
            return true;
        }

        var lcp = LongestCommonPrefixLength(a, b);
        var minLen = Math.Min(a.Length, b.Length);
        return lcp >= 5 && lcp >= (int)Math.Ceiling(minLen * 0.7);
    }

    private static int LongestCommonPrefixLength(string a, string b)
    {
        var n = Math.Min(a.Length, b.Length);
        var i = 0;
        while (i < n && a[i] == b[i])
        {
            i++;
        }

        return i;
    }

    /// <summary>Small closed synonym sets for common research focus wording (deterministic, no NLP pack).</summary>
    private static IEnumerable<string> ExpandTerm(string term)
    {
        yield return term;
        if (SynonymGroups.TryGetValue(term, out var group))
        {
            foreach (var g in group)
            {
                if (!g.Equals(term, StringComparison.Ordinal))
                {
                    yield return g;
                }
            }
        }
    }

    // Each key maps to the full group (including itself). Generated once from group lists.
    private static readonly Dictionary<string, string[]> SynonymGroups = BuildSynonymGroups(
    [
        ["config", "configuration", "configure", "configured", "configuring"],
        ["auth", "authentication", "authenticate", "authenticated", "authorization", "authorize"],
        ["login", "signin", "sign-in", "logon"],
        ["database", "databases", "db"],
        ["function", "functions", "fn", "func"],
        ["select", "selection", "selector"],
        ["query", "queries", "queried", "querying"],
        ["syntax", "syntactic"],
        ["token", "tokens", "tokenize", "tokenizer"],
        ["async", "asynchronous", "asynchronously"],
    ]);

    private static Dictionary<string, string[]> BuildSynonymGroups(string[][] groups)
    {
        var map = new Dictionary<string, string[]>(StringComparer.Ordinal);
        foreach (var group in groups)
        {
            var normalized = group
                .Select(g => g.ToLowerInvariant())
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            foreach (var key in normalized)
            {
                map[key] = normalized;
            }
        }

        return map;
    }

    private static string BuildDigestSearchBlob(string text)
    {
        var visible = StripMarkdownForDigest(text);
        var urls = MarkdownLinkTargetRegex()
            .Matches(text)
            .Select(m => m.Groups[1].Value)
            .Where(u => u.Length > 0);
        return string.Join(' ', new[] { visible }.Concat(urls));
    }

    private static string StripMarkdownForDigest(string text)
    {
        var stripped = MarkdownLinkRegex().Replace(text, "$1");
        stripped = HeadingMarkRegex().Replace(stripped, string.Empty);
        return stripped.Trim();
    }

    internal static List<string> Tokenize(string text) =>
        WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => w.Length >= 3)
            .ToList();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex MarkdownLinkRegex();

    [GeneratedRegex(@"\[[^\]]*\]\(([^)]+)\)")]
    private static partial Regex MarkdownLinkTargetRegex();

    [GeneratedRegex(@"^#+\s*", RegexOptions.CultureInvariant)]
    private static partial Regex HeadingMarkRegex();

    // Unicode letter/number runs, not ASCII [a-z0-9]: the input is already lower-cased, so an
    // ASCII class tokenized Cyrillic/Greek/Arabic (and other non-Latin scripts) to nothing,
    // leaving focus matching blind on non-English pages. \p{L}\p{N} covers every alphabetic
    // script. (CJK/Thai and other space-less scripts still need a word segmenter — tracked
    // separately; this class at least captures multi-char runs instead of dropping them.)
    [GeneratedRegex(@"[\p{L}\p{N}]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}
