using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

public static partial class FactDensityFilter
{
    private static readonly string[] SeoPhrases =
    [
        "in this article", "in this guide", "we'll explore", "we will explore",
        "let's dive", "lets dive", "whether you're", "whether you are",
        "click here", "learn more about", "read also", "related posts",
        "table of contents", "disclaimer", "advertiser disclosure",
        "sign up for free", "get started free", "subscribe to our",
        "best practices for", "top 10", "top 5", "ultimate guide",
        "everything you need to know", "frequently asked questions",
        "was this page helpful", "share this article", "follow us on",
    ];

    public static bool IsLowValueBlock(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        var trimmed = text.Trim();
        if (trimmed.Length < 24)
        {
            return false;
        }

        var lower = trimmed.ToLowerInvariant();
        if (SeoPhrases.Any(phrase => lower.Contains(phrase, StringComparison.Ordinal)))
        {
            return true;
        }

        if (IsQuestionOnlyCta(lower))
        {
            return true;
        }

        var words = Tokenize(trimmed);
        if (words.Count < 10)
        {
            return false;
        }

        var unique = words.Distinct(StringComparer.Ordinal).Count();
        var density = (double)unique / words.Count;
        var linkWords = LinkWordRegex().Matches(trimmed).Count;
        if (linkWords >= 3 && linkWords * 4 >= words.Count)
        {
            return true;
        }

        return density < 0.38 && words.Count >= 28;
    }

    private static bool IsQuestionOnlyCta(string lower)
    {
        if (!lower.Contains('?'))
        {
            return false;
        }

        var words = Tokenize(lower);
        return words.Count <= 14
            && (lower.Contains("sign up", StringComparison.Ordinal)
                || lower.Contains("subscribe", StringComparison.Ordinal)
                || lower.Contains("cookie", StringComparison.Ordinal));
    }

    private static List<string> Tokenize(string text) =>
        WordRegex().Matches(text.ToLowerInvariant())
            .Select(m => m.Value)
            .Where(w => w.Length >= 3)
            .ToList();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex LinkWordRegex();

    // Unicode letter/number runs, not ASCII [a-z0-9]: input is lower-cased first, so an ASCII
    // class scored every non-Latin block as zero words → density heuristics never fired on
    // Cyrillic/Greek/Arabic content. \p{L}\p{N} covers all alphabetic scripts. (Space-less
    // scripts like CJK/Thai still need a segmenter — tracked separately.)
    [GeneratedRegex(@"[\p{L}\p{N}]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex WordRegex();
}
