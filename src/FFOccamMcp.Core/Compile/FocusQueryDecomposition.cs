using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

/// <summary>
/// Deterministic focus-query split: rare identifiers / product-like tokens (primary anchors)
/// vs generic topical ops/concepts (supporting intent). No site-specific hardcodes.
/// </summary>
public static partial class FocusQueryDecomposition
{
    /// <summary>Primary anchors and supporting terms extracted from a focus query.</summary>
    public sealed record Result(
        IReadOnlyList<string> PrimaryAnchors,
        IReadOnlyList<string> SupportingTerms,
        IReadOnlyList<string> QuotedPhrases,
        IReadOnlyList<string> AllTerms,
        string NormalizedQuery)
    {
        public bool HasPrimaryAnchors => PrimaryAnchors.Count > 0;
    }

    public static Result Decompose(string? focusQuery)
    {
        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return new Result([], [], [], [], string.Empty);
        }

        var normalized = focusQuery.Trim().ToLowerInvariant();
        var quoted = ExtractQuotedPhrases(focusQuery);
        var allTerms = FocusMatcher.Tokenize(focusQuery)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var primary = new List<string>();
        var supporting = new List<string>();

        foreach (var phrase in quoted)
        {
            if (phrase.Length >= 3 && !primary.Contains(phrase, StringComparer.Ordinal))
            {
                primary.Add(phrase);
            }
        }

        // Path-like / dotted identifiers from the raw query (before word tokenization splits them).
        foreach (Match m in PathLikeRegex().Matches(focusQuery))
        {
            var token = m.Value.Trim().ToLowerInvariant();
            if (token.Length >= 3 && !primary.Contains(token, StringComparer.Ordinal))
            {
                primary.Add(token);
            }
        }

        foreach (Match m in IdentifierRegex().Matches(focusQuery))
        {
            var token = m.Value.ToLowerInvariant();
            if (token.Length >= 3 && !primary.Contains(token, StringComparer.Ordinal))
            {
                primary.Add(token);
            }
        }

        foreach (var term in allTerms)
        {
            if (primary.Any(p => p.Equals(term, StringComparison.Ordinal)
                                 || p.Contains(term, StringComparison.Ordinal)))
            {
                continue;
            }

            if (IsSupportingLexicon(term))
            {
                if (!supporting.Contains(term, StringComparer.Ordinal))
                {
                    supporting.Add(term);
                }
            }
            else if (!primary.Contains(term, StringComparer.Ordinal))
            {
                primary.Add(term);
            }
        }

        // If everything landed in supporting (generic topical query), leave primaries empty
        // so ranking falls back to symmetric semantic/BM25 (negative case).
        // If we somehow have neither, promote the longest term as a weak primary only when
        // the query clearly has an identifier-shaped leftover — otherwise stay empty.
        if (primary.Count == 0 && supporting.Count == 0 && allTerms.Count > 0)
        {
            primary.Add(allTerms.OrderByDescending(t => t.Length).First());
        }

        return new Result(primary, supporting, quoted, allTerms, normalized);
    }

    private static List<string> ExtractQuotedPhrases(string focusQuery)
    {
        var phrases = new List<string>();
        foreach (Match m in QuotedPhraseRegex().Matches(focusQuery))
        {
            var trimmed = m.Groups[1].Value.Trim().ToLowerInvariant();
            if (trimmed.Length >= 3)
            {
                phrases.Add(trimmed);
            }
        }

        return phrases;
    }

    private static bool IsSupportingLexicon(string term) =>
        SupportingLexicon.Contains(term);

    /// <summary>
    /// Generic ops / concepts / actions. Intentionally excludes library and product names.
    /// </summary>
    private static readonly HashSet<string> SupportingLexicon = new(StringComparer.Ordinal)
    {
        "event", "events", "loop", "loops", "task", "tasks", "cancel", "cancellation",
        "request", "requests", "fetch", "operation", "operations", "synchronization",
        "synchronize", "sync", "async", "asynchronous", "asynchronously",
        "stable", "network", "identity", "version", "versions", "history",
        "change", "changes", "changed", "howto", "how", "safe", "safely",
        "create", "creating", "use", "using", "get", "set", "run", "running",
        "start", "stop", "read", "write", "call", "calling", "handle", "handling",
        "manage", "management", "config", "configuration", "configure",
        "guide", "tutorial", "overview", "introduction", "reference", "docs",
        "documentation", "api", "module", "library", "page", "section",
        "what", "when", "where", "which", "with", "from", "into", "about",
        "the", "and", "for", "new", "old",
    };

    // "phrase" or 'phrase'
    [GeneratedRegex("""["']([^"']{3,})["']""", RegexOptions.CultureInvariant)]
    private static partial Regex QuotedPhraseRegex();

    // path-like or dotted module tokens: foo/bar, pkg.mod, a.b.c
    [GeneratedRegex(@"[\p{L}\p{N}]+(?:[./][\p{L}\p{N}]+){1,}", RegexOptions.CultureInvariant)]
    private static partial Regex PathLikeRegex();

    // snake_case or CamelCase / PascalCase identifiers
    [GeneratedRegex(
        @"\b[\p{L}][\p{L}\p{N}]*_[\p{L}\p{N}_]+\b|\b[\p{Lu}][\p{L}\p{N}]*[\p{Lu}][\p{L}\p{N}]*\b",
        RegexOptions.CultureInvariant)]
    private static partial Regex IdentifierRegex();
}
