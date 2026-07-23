using System.Text.RegularExpressions;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Probe;

namespace OccamMcp.Core.Services;

/// <summary>
/// Focus-aware link ranking for map / digest discovery.
/// Entity-first: primary anchors outrank supporting-term overlap; version roots are penalized
/// when the query names a concrete API/entity. Shared by <see cref="MapService"/> and
/// <see cref="DigestService"/>.
/// </summary>
public static partial class MapLinkRanker
{
    /// <summary>Score above which a link is a strong focus hit (skips second-level expansion).</summary>
    public const double StrongHitThreshold = 4.0;

    private const double MissingPrimaryPenalty = 8.0;
    private const double VersionRootPenalty = 6.0;
    private const double ChangelogPenalty = 3.0;
    private const double SupportingCap = 2.5;

    /// <summary>Per-candidate score components for gate/diagnostics (not MCP payloads).</summary>
    public sealed record ScoreBreakdown(
        MappedLink Link,
        double Total,
        double PathSegment,
        double TitleToken,
        double AnchorPhrase,
        double PrimarySoft,
        double Supporting,
        double Bm25,
        double Semantic,
        double MissingPrimaryPenaltyApplied,
        double VersionPenaltyApplied,
        IReadOnlyList<string> PrimaryHits,
        IReadOnlyList<string> SupportingHits);

    public static IReadOnlyList<MappedLink> Rank(
        IReadOnlyList<MappedLink> links,
        string? focusQuery,
        int maxLinks)
    {
        maxLinks = Math.Max(1, maxLinks);
        if (links.Count == 0)
        {
            return links;
        }

        if (string.IsNullOrWhiteSpace(focusQuery))
        {
            return links.Take(maxLinks).ToList();
        }

        return RankScored(links, focusQuery)
            .Take(maxLinks)
            .Select(entry => entry.Link)
            .ToList();
    }

    /// <summary>Full scored ranking (tests + second-level expand decisions).</summary>
    public static IReadOnlyList<(MappedLink Link, double Score)> RankScored(
        IReadOnlyList<MappedLink> links,
        string focusQuery) =>
        RankDetailed(links, focusQuery)
            .Select(b => (b.Link, b.Total))
            .ToList();

    /// <summary>Scored ranking with component breakdown (gate / debug only).</summary>
    public static IReadOnlyList<ScoreBreakdown> RankDetailed(
        IReadOnlyList<MappedLink> links,
        string focusQuery)
    {
        var decomp = FocusQueryDecomposition.Decompose(focusQuery);
        if (decomp.AllTerms.Count == 0 && decomp.NormalizedQuery.Length < 3)
        {
            return links.Select(l => EmptyBreakdown(l)).ToList();
        }

        var fields = links.Select(link => new LinkFields(link, BuildFields(link))).ToList();
        var corpus = fields.Select(f => f.Combined).ToList();
        var docFreq = BuildDocFreq(corpus);
        var avgDl = corpus.Count == 0
            ? 1.0
            : corpus.Average(text => Math.Max(1, FocusMatcher.Tokenize(text).Count));

        return fields
            .Select(f => ScoreLink(f, decomp, docFreq, corpus.Count, avgDl))
            .OrderByDescending(b => b.Total)
            .ThenBy(b => b.Link.Url, StringComparer.Ordinal)
            .ToList();
    }

    public static bool HasStrongHit(IReadOnlyList<(MappedLink Link, double Score)> scored) =>
        scored.Any(s => s.Score >= StrongHitThreshold);

    public static bool HasStrongHit(IReadOnlyList<ScoreBreakdown> scored) =>
        scored.Any(s => s.Total >= StrongHitThreshold);

    public static double MaxScore(IReadOnlyList<(MappedLink Link, double Score)> scored) =>
        scored.Count == 0 ? 0 : scored.Max(s => s.Score);

    private static ScoreBreakdown EmptyBreakdown(MappedLink link) =>
        new(link, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, [], []);

    private static ScoreBreakdown ScoreLink(
        LinkFields fields,
        FocusQueryDecomposition.Result decomp,
        Dictionary<string, int> docFreq,
        int docCount,
        double avgDl)
    {
        var pathNorm = NormalizePathBlob(fields.Link.Path);
        var pathSegments = PathSegments(fields.Link.Path);
        var titleNorm = (fields.Link.Title ?? string.Empty).ToLowerInvariant();
        var contextNorm = (fields.Link.Context ?? fields.Link.Description ?? string.Empty).ToLowerInvariant();
        var queryLower = decomp.NormalizedQuery;

        var pathSegment = 0.0;
        var titleToken = 0.0;
        var anchorPhrase = 0.0;
        var primarySoft = 0.0;
        var supporting = 0.0;
        var primaryHits = new List<string>();
        var supportingHits = new List<string>();

        // Quoted phrases + contiguous query (when short enough to be an entity phrase).
        var phrases = decomp.QuotedPhrases.Count > 0
            ? decomp.QuotedPhrases
            : (queryLower.Length is >= 3 and <= 48 ? [queryLower] : Array.Empty<string>());
        foreach (var phrase in phrases)
        {
            if (pathNorm.Contains(phrase, StringComparison.Ordinal)
                || pathSegments.Any(s => s.Equals(phrase, StringComparison.Ordinal)
                                         || s.Contains(phrase, StringComparison.Ordinal)))
            {
                anchorPhrase = Math.Max(anchorPhrase, 10.0);
            }
            else if (titleNorm.Contains(phrase, StringComparison.Ordinal))
            {
                anchorPhrase = Math.Max(anchorPhrase, 6.0);
            }
            else if (contextNorm.Contains(phrase, StringComparison.Ordinal))
            {
                anchorPhrase = Math.Max(anchorPhrase, 2.5);
            }
        }

        // Primary anchors — entity-first hierarchy.
        if (decomp.HasPrimaryAnchors)
        {
            foreach (var anchor in decomp.PrimaryAnchors)
            {
                var hit = false;
                if (pathSegments.Any(s => SegmentEquals(s, anchor)))
                {
                    pathSegment += 12.0;
                    hit = true;
                }
                else if (pathNorm.Contains(anchor, StringComparison.Ordinal)
                         || pathSegments.Any(s => s.Contains(anchor, StringComparison.Ordinal)))
                {
                    pathSegment += 9.0;
                    hit = true;
                }

                var titleTokens = FocusMatcher.Tokenize(titleNorm);
                if (titleTokens.Any(t => t.Equals(anchor, StringComparison.Ordinal))
                    || titleNorm.Contains(anchor, StringComparison.Ordinal))
                {
                    titleToken += 8.0;
                    hit = true;
                }
                else if (titleTokens.Any(t => FocusMatcher.SoftTermEquals(t, anchor)))
                {
                    // Soft-stem only as a weak primary signal (avoids asynchronous ≈ asyncio leaps).
                    primarySoft += 2.0;
                    hit = true;
                }

                if (!hit
                    && (pathNorm.Contains(anchor, StringComparison.Ordinal)
                        || titleNorm.Contains(anchor, StringComparison.Ordinal)
                        || contextNorm.Contains(anchor, StringComparison.Ordinal)))
                {
                    primarySoft += 3.0;
                    hit = true;
                }

                if (hit && !primaryHits.Contains(anchor, StringComparer.Ordinal))
                {
                    primaryHits.Add(anchor);
                }
            }
        }

        // Supporting intent — capped so it cannot drown a primary path/title hit.
        var supportTerms = decomp.SupportingTerms.Count > 0
            ? decomp.SupportingTerms
            : (decomp.HasPrimaryAnchors ? decomp.SupportingTerms : decomp.AllTerms);
        // When no primaries (negative case), score all terms as supporting/BM25 symmetrically.
        if (!decomp.HasPrimaryAnchors)
        {
            supportTerms = decomp.AllTerms;
        }

        if (supportTerms.Count > 0)
        {
            var pathHits = CountTermHits(supportTerms, pathNorm);
            var titleHits = CountTermHits(supportTerms, titleNorm);
            var contextHits = CountTermHits(supportTerms, contextNorm);
            var raw = (2.0 * pathHits + 1.5 * titleHits + 0.75 * contextHits) / supportTerms.Count;
            supporting = Math.Min(SupportingCap, raw);
            foreach (var term in supportTerms)
            {
                if (TermHits(term, pathNorm, FocusMatcher.Tokenize(pathNorm))
                    || TermHits(term, titleNorm, FocusMatcher.Tokenize(titleNorm))
                    || TermHits(term, contextNorm, FocusMatcher.Tokenize(contextNorm)))
                {
                    supportingHits.Add(term);
                }
            }
        }

        var queryForBm25 = decomp.HasPrimaryAnchors
            ? decomp.PrimaryAnchors.Concat(decomp.SupportingTerms).Distinct(StringComparer.Ordinal).ToList()
            : decomp.AllTerms;
        var bm25 = Bm25Score(fields.Combined, queryForBm25, docFreq, docCount, avgDl);
        // When primaries exist, down-weight BM25 so supporting-heavy pages cannot leapfrog.
        if (decomp.HasPrimaryAnchors)
        {
            bm25 *= 0.35;
        }

        var semantic = 2.0 * SemanticOverlap(
            decomp.HasPrimaryAnchors ? decomp.PrimaryAnchors : decomp.AllTerms,
            fields.Combined);
        if (decomp.HasPrimaryAnchors)
        {
            semantic = Math.Min(2.0, semantic);
        }

        var missingPrimaryPenalty = 0.0;
        if (decomp.HasPrimaryAnchors && primaryHits.Count == 0)
        {
            missingPrimaryPenalty = MissingPrimaryPenalty;
        }

        var versionPenalty = 0.0;
        var isVersionNoise = LooksLikeVersionNoise(fields.Link.Path, fields.Link.Title)
                             || LooksLikeVersionRoot(fields.Link.Path);
        if (isVersionNoise && !QueryLooksLikeVersion(queryLower))
        {
            var primaryInPath = decomp.PrimaryAnchors.Any(a =>
                pathNorm.Contains(a, StringComparison.Ordinal)
                || titleNorm.Contains(a, StringComparison.Ordinal));
            if (!primaryInPath)
            {
                versionPenalty = LooksLikeVersionRoot(fields.Link.Path)
                    ? VersionRootPenalty
                    : ChangelogPenalty;
                // Extra when the query has concrete entity anchors.
                if (decomp.HasPrimaryAnchors)
                {
                    versionPenalty += 2.0;
                }
            }
        }

        var total = pathSegment + titleToken + anchorPhrase + primarySoft + supporting
                    + bm25 + semantic - missingPrimaryPenalty - versionPenalty;

        return new ScoreBreakdown(
            fields.Link,
            total,
            pathSegment,
            titleToken,
            anchorPhrase,
            primarySoft,
            supporting,
            bm25,
            semantic,
            missingPrimaryPenalty,
            versionPenalty,
            primaryHits,
            supportingHits);
    }

    private static bool SegmentEquals(string segment, string anchor) =>
        segment.Equals(anchor, StringComparison.Ordinal)
        || segment.Equals(anchor + ".html", StringComparison.Ordinal)
        || segment.Equals(anchor + ".htm", StringComparison.Ordinal)
        || (segment.StartsWith(anchor + ".", StringComparison.Ordinal)
            && segment.Length <= anchor.Length + 6);

    private static List<string> PathSegments(string path) =>
        path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(s => s.ToLowerInvariant())
            .ToList();

    private static int CountTermHits(IReadOnlyList<string> queryTerms, string blob)
    {
        if (blob.Length == 0)
        {
            return 0;
        }

        var tokens = FocusMatcher.Tokenize(blob);
        var hits = 0;
        foreach (var term in queryTerms)
        {
            if (TermHits(term, blob, tokens))
            {
                hits++;
            }
        }

        return hits;
    }

    private static bool TermHits(string term, string blob, List<string> tokens)
    {
        if (blob.Contains(term, StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var token in tokens)
        {
            if (FocusMatcher.SoftTermEquals(token, term))
            {
                return true;
            }
        }

        return false;
    }

    private static double SemanticOverlap(IReadOnlyList<string> queryTerms, string docText)
    {
        if (queryTerms.Count == 0 || string.IsNullOrWhiteSpace(docText))
        {
            return 0;
        }

        var docTokens = FocusMatcher.Tokenize(docText);
        if (docTokens.Count == 0)
        {
            return 0;
        }

        var hits = 0;
        foreach (var term in queryTerms)
        {
            if (TermHits(term, docText.ToLowerInvariant(), docTokens))
            {
                hits++;
            }
        }

        return (double)hits / queryTerms.Count;
    }

    private static double Bm25Score(
        string docText,
        IReadOnlyList<string> queryTerms,
        Dictionary<string, int> docFreq,
        int docCount,
        double avgDl)
    {
        if (queryTerms.Count == 0 || string.IsNullOrWhiteSpace(docText))
        {
            return 0;
        }

        const double k1 = 1.2;
        const double b = 0.75;
        var tokens = FocusMatcher.Tokenize(docText);
        var dl = Math.Max(1, tokens.Count);
        var score = 0.0;
        var lower = docText.ToLowerInvariant();

        foreach (var term in queryTerms)
        {
            docFreq.TryGetValue(term, out var df);
            var idf = Math.Log(1 + (docCount - df + 0.5) / (df + 0.5));
            var tf = 0;
            foreach (var token in tokens)
            {
                if (FocusMatcher.SoftTermEquals(token, term))
                {
                    tf++;
                }
            }

            if (tf == 0 && lower.Contains(term, StringComparison.Ordinal))
            {
                tf = 1;
            }

            if (tf == 0)
            {
                continue;
            }

            var numerator = tf * (k1 + 1);
            var denominator = tf + k1 * (1 - b + b * dl / avgDl);
            score += idf * numerator / denominator;
        }

        return score;
    }

    private static Dictionary<string, int> BuildDocFreq(IReadOnlyList<string> corpus)
    {
        var docFreq = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var doc in corpus)
        {
            foreach (var term in FocusMatcher.Tokenize(doc).Distinct(StringComparer.Ordinal))
            {
                docFreq.TryGetValue(term, out var count);
                docFreq[term] = count + 1;
            }
        }

        return docFreq;
    }

    private static string BuildFields(MappedLink link)
    {
        var path = NormalizePathBlob(link.Path);
        var title = link.Title ?? string.Empty;
        var description = link.Description ?? string.Empty;
        var context = link.Context ?? string.Empty;
        return $"{title} {title} {path} {path} {description} {context}".Trim();
    }

    private static string NormalizePathBlob(string path) =>
        path.Replace('/', ' ').Replace('-', ' ').Replace('_', ' ').Replace('.', ' ').ToLowerInvariant();

    private static bool LooksLikeVersionNoise(string path, string? title)
    {
        var blob = $"{path} {title}".ToLowerInvariant();
        if (VersionPathRegex().IsMatch(path))
        {
            return true;
        }

        return blob.Contains("what's new", StringComparison.Ordinal)
            || blob.Contains("whats new", StringComparison.Ordinal)
            || blob.Contains("changelog", StringComparison.Ordinal)
            || blob.Contains("release notes", StringComparison.Ordinal)
            || blob.Contains("release history", StringComparison.Ordinal);
    }

    /// <summary>
    /// Documentation version roots: /3/, /3.12/, /docs/3.10/, leaving little else in the path.
    /// </summary>
    internal static bool LooksLikeVersionRoot(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || path is "/" or ".")
        {
            return false;
        }

        var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (segments.Length == 0)
        {
            return false;
        }

        // Bare major or major.minor leaf: /3/, /3.12/, /docs/3.11/
        if (segments.Length <= 2
            && segments.All(s => VersionSegmentRegex().IsMatch(s)
                                 || s.Equals("docs", StringComparison.OrdinalIgnoreCase)
                                 || s.Equals("doc", StringComparison.OrdinalIgnoreCase)
                                 || s.Equals("en", StringComparison.OrdinalIgnoreCase)
                                 || s.Equals("en-us", StringComparison.OrdinalIgnoreCase)
                                 || s.Equals("index.html", StringComparison.OrdinalIgnoreCase)
                                 || s.Equals("index.htm", StringComparison.OrdinalIgnoreCase)))
        {
            return segments.Any(s => VersionSegmentRegex().IsMatch(s));
        }

        // Trailing version directory with no further leaf: .../3.12/ or .../v3.12/
        var last = segments[^1];
        if (VersionSegmentRegex().IsMatch(last)
            || (last.StartsWith('v') && VersionSegmentRegex().IsMatch(last[1..])))
        {
            return segments.Length <= 3;
        }

        return false;
    }

    private static bool QueryLooksLikeVersion(string queryLower) =>
        VersionTokenRegex().IsMatch(queryLower)
        || queryLower.Contains("version history", StringComparison.Ordinal)
        || queryLower.Contains("whats new", StringComparison.Ordinal)
        || queryLower.Contains("what's new", StringComparison.Ordinal)
        || queryLower.Contains("changelog", StringComparison.Ordinal)
        || queryLower.Contains("release notes", StringComparison.Ordinal)
        || (queryLower.Contains("changed", StringComparison.Ordinal)
            && VersionTokenRegex().IsMatch(queryLower.Replace(" ", ".")));

    private sealed record LinkFields(MappedLink Link, string Combined);

    [GeneratedRegex(@"/\d+\.\d+(\.\d+)?(/|$)", RegexOptions.CultureInvariant)]
    private static partial Regex VersionPathRegex();

    [GeneratedRegex(@"^\d+(\.\d+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex VersionSegmentRegex();

    [GeneratedRegex(@"\b\d+(\.\d+)+\b", RegexOptions.CultureInvariant)]
    private static partial Regex VersionTokenRegex();
}
