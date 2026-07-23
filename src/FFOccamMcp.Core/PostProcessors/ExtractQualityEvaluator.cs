using System.Text.RegularExpressions;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.PostProcessors;

/// <summary>
/// Extract Quality Model (ADR-0004): multi-signal gate separating short quality documents (SQD)
/// from bad extraction (BE). Length is an input to density/richness — never a sole reject trigger.
/// </summary>
public static partial class ExtractQualityEvaluator
{
    /// <summary>
    /// Component scores and gate verdict for one markdown extract (optional blocks boost richness).
    /// </summary>
    public sealed record ExtractQualityReport(
        double Score,
        double Noise,
        double ContentDensity,
        double SemanticRichness,
        double LengthPrior,
        string Verdict,
        bool IsBadExtraction,
        int VisibleProseChars,
        int TotalChars);

    private const int PromoBannerMaxChars = 280;
    private const int ThinLengthChars = 400;

    /// <summary>
    /// Pure EQM evaluation. Deterministic / AOT-safe — no remote ML.
    /// </summary>
    public static ExtractQualityReport Evaluate(
        string? markdown,
        IReadOnlyList<WorkerExtractBlockInfo>? blocks = null)
    {
        if (string.IsNullOrWhiteSpace(markdown))
        {
            return new ExtractQualityReport(
                Score: 0,
                Noise: 1,
                ContentDensity: 0,
                SemanticRichness: 0,
                LengthPrior: 0,
                Verdict: "thin",
                IsBadExtraction: true,
                VisibleProseChars: 0,
                TotalChars: 0);
        }

        var md = markdown.Trim();
        var totalChars = md.Length;
        var visibleProse = VisibleContentChars(md);
        var headingCount = HeadingLine().Matches(md).Count;
        var listItems = ListItemLine().Matches(md).Count;
        var linkCount = MarkdownLink().Matches(md).Count;
        var headingShell = IsHeadingShell(md, headingCount, listItems, linkCount);

        var contentDensity = Clamp01(visibleProse / (double)Math.Max(totalChars, 1));
        var semanticRichness = ComputeSemanticRichness(md, visibleProse, blocks);
        var noiseScore = ComputeNoiseScore(md, totalChars, headingCount, listItems, linkCount, headingShell);
        var lengthPrior = Clamp01(visibleProse / (double)(visibleProse + 120));

        var qualityScore = Clamp01(
            0.35 * (1.0 - noiseScore)
            + 0.25 * contentDensity
            + 0.25 * semanticRichness
            + 0.15 * lengthPrior);

        var isBad = DecideBadExtraction(
            visibleProse,
            qualityScore,
            noiseScore,
            contentDensity,
            semanticRichness,
            headingShell);

        var verdict = ResolveVerdict(isBad, qualityScore, noiseScore, totalChars, visibleProse);

        return new ExtractQualityReport(
            Score: Round4(qualityScore),
            Noise: Round4(noiseScore),
            ContentDensity: Round4(contentDensity),
            SemanticRichness: Round4(semanticRichness),
            LengthPrior: Round4(lengthPrior),
            Verdict: verdict,
            IsBadExtraction: isBad,
            VisibleProseChars: visibleProse,
            TotalChars: totalChars);
    }

    /// <summary>
    /// Compatibility gate used by post-processors and router escalation. Delegates to <see cref="Evaluate"/>.
    /// </summary>
    public static bool LooksLikeThinExtract(string? markdown) =>
        Evaluate(markdown).IsBadExtraction;

    /// <summary>
    /// AF-1 confidence: EQM qualityScore after a successful extract, with a small backend factor
    /// (±0.05) documented as non-authoritative. Failures → 0.
    /// </summary>
    public static double ComputeConfidence(Routing.TranscodeOutcome outcome)
    {
        if (outcome is null || !outcome.Ok || string.IsNullOrWhiteSpace(outcome.Markdown))
        {
            return 0.0;
        }

        var report = Evaluate(outcome.Markdown, outcome.Blocks);
        // Direct calls (unit tests) may evaluate BE markdown with Ok=true; keep confidence honest.
        if (report.IsBadExtraction)
        {
            return Clamp01(Round4(Math.Min(report.Score, 0.25)));
        }

        var backendDelta = string.Equals(outcome.Backend, "browser", StringComparison.OrdinalIgnoreCase)
            ? 0.05
            : 0.0;
        var truncationPenalty = outcome.Truncated ? 0.05 : 0.0;
        return Clamp01(Round4(report.Score + backendDelta - truncationPenalty));
    }

    /// <summary>Evaluate + attach confidence semantics for a success-path outcome.</summary>
    public static ExtractQualityReport EvaluateOutcome(Routing.TranscodeOutcome outcome)
    {
        if (outcome is null || string.IsNullOrWhiteSpace(outcome.Markdown))
        {
            return Evaluate(null);
        }

        return Evaluate(outcome.Markdown, outcome.Blocks);
    }

    private static bool DecideBadExtraction(
        int visibleProse,
        double qualityScore,
        double noiseScore,
        double contentDensity,
        double semanticRichness,
        bool headingShell)
    {
        // BE = bad extraction → thin_extract. SQD / rich → pass.
        if (visibleProse < 40)
        {
            return true;
        }

        if (noiseScore >= 0.70)
        {
            return true;
        }

        // Headings-only chrome (anti-bot / SPA shell) is never a quality document.
        if (headingShell)
        {
            return true;
        }

        if (qualityScore >= 0.55)
        {
            return false;
        }

        if (qualityScore >= 0.40 && contentDensity >= 0.45 && noiseScore < 0.35)
        {
            return false;
        }

        return qualityScore < 0.40;
    }

    private static string ResolveVerdict(
        bool isBad,
        double qualityScore,
        double noiseScore,
        int totalChars,
        int visibleProse)
    {
        if (isBad)
        {
            return noiseScore >= 0.55 ? "noisy" : "thin";
        }

        if (totalChars < 500 && visibleProse < 280)
        {
            return "short_quality";
        }

        if (qualityScore >= 0.70)
        {
            return "rich";
        }

        return noiseScore >= 0.35 ? "noisy" : "rich";
    }

    private static double ComputeNoiseScore(
        string md,
        int totalChars,
        int headingCount,
        int listItems,
        int linkCount,
        bool headingShell)
    {
        var hits = 0.0;

        if (ContainsAny(md,
                "continue shopping",
                "click the button below",
                "accept cookies",
                "accept cookie",
                "cookie consent",
                "consent to continue",
                "enable javascript",
                "enable js",
                "please enable",
                "are you a robot",
                "verify you are human"))
        {
            hits += 0.55;
        }

        // Promo / CTA banner shape (nginx-class marketing stub). Require CTA lexicon —
        // a single benign outbound link (example.com "Learn more") is not promo chrome.
        if (totalChars <= PromoBannerMaxChars
            && listItems == 0
            && headingCount <= 1
            && linkCount <= 1
            && ContainsAny(md, "start here", "migration?", "sign up", "subscribe", "buy now",
                "planning your", "click here", "learn more and buy"))
        {
            hits += 0.45;
        }
        else if (totalChars <= PromoBannerMaxChars
                 && listItems == 0
                 && headingCount <= 1
                 && linkCount <= 1
                 && !LooksLikeDefinitionalShortDoc(md))
        {
            // Short, sparse structure without definitional prose — suspicious chrome.
            hits += 0.35;
        }

        if (totalChars < ThinLengthChars
            && listItems < 3
            && linkCount < 3
            && headingCount < 2
            && VisibleContentChars(md) < 160
            && !LooksLikeDefinitionalShortDoc(md))
        {
            hits += 0.25;
        }

        if (headingShell)
        {
            hits += 0.55;
        }

        // Link-farm density: many links relative to length.
        var linkRatio = totalChars > 0 ? linkCount * 200.0 / totalChars : 0;
        if (linkRatio > 0.4 && VisibleContentChars(md) < 200)
        {
            hits += 0.30;
        }

        return Clamp01(hits);
    }

    private static double ComputeSemanticRichness(
        string md,
        int visibleProse,
        IReadOnlyList<WorkerExtractBlockInfo>? blocks)
    {
        var visible = ExtractVisibleProse(md);
        if (string.IsNullOrWhiteSpace(visible))
        {
            return 0;
        }

        var sentenceLike = 0;
        foreach (Match m in SentenceEnd().Matches(visible))
        {
            sentenceLike++;
        }

        foreach (var line in visible.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (line.Length >= 40 && !line.StartsWith('#'))
            {
                sentenceLike++;
            }
        }

        // Cap double-counting: each long line that also ends with .!? counted once via regex is enough;
        // the length≥40 bonus is additive but clamp later.
        sentenceLike = Math.Min(sentenceLike, 12);

        var tokens = Tokenize(visible);
        var tokenCount = tokens.Count;
        var unique = tokens.Count == 0 ? 0 : tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var tokenEntropyProxy = tokenCount == 0 ? 0 : unique / (double)tokenCount;

        var richness = Clamp01(
            0.45 * Clamp01(sentenceLike / 3.0)
            + 0.35 * tokenEntropyProxy
            + 0.20 * Clamp01(unique / 40.0));

        if (blocks is { Count: > 0 })
        {
            var substance = 0;
            var chrome = 0;
            foreach (var b in blocks)
            {
                var t = b.Type ?? "";
                if (t is "paragraph" or "code" or "quote" or "list_item" or "table")
                {
                    substance++;
                }
                else if (t is "heading" or "nav" or "banner")
                {
                    chrome++;
                }
            }

            if (substance > chrome)
            {
                richness = Clamp01(richness + 0.1);
            }
        }

        // Definitional short docs (example.com-class): two+ sentence-like runs with decent unique tokens.
        if (visibleProse is >= 80 and < 280 && unique >= 12 && sentenceLike >= 2)
        {
            richness = Math.Max(richness, 0.55);
        }

        return richness;
    }

    private static bool LooksLikeDefinitionalShortDoc(string md)
    {
        var visible = ExtractVisibleProse(md);
        var tokens = Tokenize(visible);
        var unique = tokens.Distinct(StringComparer.OrdinalIgnoreCase).Count();
        var sentenceLike = SentenceEnd().Matches(visible).Count;
        return unique >= 12 && sentenceLike >= 1 && VisibleContentChars(md) >= 80;
    }

    private static bool IsHeadingShell(string md, int headingCount, int listItems, int linkCount) =>
        md.Length < ThinLengthChars
        && headingCount >= 2
        && listItems == 0
        && linkCount == 0
        && NonHeadingProseChars(md) < 40;

    /// <summary>Approx visible content chars: drop images, keep link anchor text but drop URLs,
    /// strip heading/list markers, collapse whitespace.</summary>
    internal static int VisibleContentChars(string md)
    {
        var s = MarkdownImage().Replace(md, " ");
        s = MarkdownLinkText().Replace(s, "$1");
        s = HeadingLine().Replace(s, " ");
        s = ListItemLine().Replace(s, " ");
        s = WhitespaceRun().Replace(s, " ");
        return s.Trim().Length;
    }

    private static string ExtractVisibleProse(string md)
    {
        var s = MarkdownImage().Replace(md, " ");
        s = MarkdownLinkText().Replace(s, "$1");
        s = HeadingLine().Replace(s, " ");
        s = ListItemLine().Replace(s, " ");
        s = WhitespaceRun().Replace(s, " ");
        return s.Trim();
    }

    private static int NonHeadingProseChars(string md)
    {
        var total = 0;
        foreach (var line in md.Split('\n'))
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0 || trimmed.StartsWith('#'))
            {
                continue;
            }

            total += trimmed.Length;
        }

        return total;
    }

    private static List<string> Tokenize(string prose)
    {
        var list = new List<string>();
        foreach (Match m in WordToken().Matches(prose))
        {
            if (m.Length >= 3)
            {
                list.Add(m.Value);
            }
        }

        return list;
    }

    private static bool ContainsAny(string md, params string[] phrases)
    {
        foreach (var p in phrases)
        {
            if (md.Contains(p, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static double Clamp01(double v) => Math.Clamp(v, 0.0, 1.0);

    private static double Round4(double v) => Math.Round(v, 4, MidpointRounding.AwayFromZero);

    [GeneratedRegex(@"^#{1,6}\s", RegexOptions.Multiline)]
    private static partial Regex HeadingLine();

    [GeneratedRegex(@"^\s*[-*+]\s", RegexOptions.Multiline)]
    private static partial Regex ListItemLine();

    [GeneratedRegex(@"\[[^\]]+\]\([^)]+\)")]
    private static partial Regex MarkdownLink();

    [GeneratedRegex(@"!\[[^\]]*\]\([^)]+\)")]
    private static partial Regex MarkdownImage();

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]+\)")]
    private static partial Regex MarkdownLinkText();

    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRun();

    [GeneratedRegex(@"[.!?](\s|$)")]
    private static partial Regex SentenceEnd();

    [GeneratedRegex(@"[\p{L}\p{N}]{3,}", RegexOptions.CultureInvariant)]
    private static partial Regex WordToken();
}
