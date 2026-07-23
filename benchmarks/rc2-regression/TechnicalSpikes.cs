using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using OccamMcp.Core.Compile;

namespace OccamMcp.Rc2Regression;

internal static partial class TechnicalSpikes
{
    public static int Run(string fixtureRoot)
    {
        var test = new TestHarness("rc2-pr-a-spikes");
        SectionIndexSpike(test, File.ReadAllText(Path.Combine(fixtureRoot, "focus-large.md")));
        AccessEvidenceSpike(test);
        SerializedBudgetSpike(test);
        return test.Finish();
    }

    private static void SectionIndexSpike(TestHarness test, string markdown)
    {
        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        var index = BuildIndex(markdown);
        clock.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        var deterministic = index.Select(item => item.Anchor).SequenceEqual(BuildIndex(markdown).Select(item => item.Anchor));
        test.Check("SPIKE-SECTION", "lightweight SectionIndex prototype",
            index.Count > 20 && deterministic,
            $"sections={index.Count}; elapsedUs={clock.Elapsed.TotalMicroseconds:F0}; allocatedBytes={allocated}; deterministic={deterministic}; aotShape=records+regex; embeddings=false");
    }

    private static void AccessEvidenceSpike(TestHarness test)
    {
        var corpus = new[]
        {
            new AccessEvidence(200, false, false, false, true),
            new AccessEvidence(200, false, true, true, false),
            new AccessEvidence(401, false, false, false, false),
            new AccessEvidence(200, true, false, false, false),
        };
        var before = GC.GetAllocatedBytesForCurrentThread();
        var clock = Stopwatch.StartNew();
        var decisions = Enumerable.Range(0, 10_000).Select(i => Assess(corpus[i % corpus.Length])).ToArray();
        clock.Stop();
        var allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        test.Check("SPIKE-ACCESS", "AccessEvidence prototype ignores prose-only topical evidence",
            Assess(corpus[0]) == "unknown"
            && Assess(corpus[1]) == "login_likely"
            && Assess(corpus[2]) == "login_likely",
            $"iterations={decisions.Length}; elapsedMs={clock.Elapsed.TotalMilliseconds:F2}; allocatedBytes={allocated}; evidenceCodes=status,redirect,password_control,blocking_form; hostAllowlist=false");
    }

    private static void SerializedBudgetSpike(TestHarness test)
    {
        var projections = new[]
        {
            new Projection(false, false, false, false),
            new Projection(true, false, false, false),
            new Projection(true, true, true, true),
        };
        var raw = new Inventory(480, 320, 180, 48);
        var results = projections.Select(projection => Project(raw, projection)).ToArray();
        test.Check("SPIKE-BUDGET", "serialized projection charges only emitted fields",
            results[0].Blocks == 0 && results[0].Tables == 0 && results[0].Media == 0
            && results[1].Blocks == raw.Blocks && results[1].Tables == 0,
            $"markdownOnly={results[0].Total}; blocks={results[1].Total}; all={results[2].Total}; estimator=integer-bucket; hiddenCharge=0");
        test.Check("SPIKE-BUDGET", "projection prototype preserves a 128-token answer floor",
            AllocateMarkdown(700, results[2]) >= ResponseBudgetPlanner.MinMarkdownTokens,
            $"budget=700; markdownCap={AllocateMarkdown(700, results[2])}; floor={ResponseBudgetPlanner.MinMarkdownTokens}");
    }

    private static List<SectionEntry> BuildIndex(string markdown)
    {
        var result = new List<SectionEntry>();
        var used = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (Match match in HeadingRegex().Matches(markdown))
        {
            var heading = match.Groups[2].Value.Trim();
            var explicitAnchor = AnchorRegex().Match(heading);
            var label = explicitAnchor.Success ? heading[..explicitAnchor.Index].Trim() : heading;
            var baseAnchor = explicitAnchor.Success ? explicitAnchor.Groups[1].Value : Slug(label);
            used.TryGetValue(baseAnchor, out var occurrence);
            used[baseAnchor] = occurrence + 1;
            var anchor = occurrence == 0 ? baseAnchor : $"{baseAnchor}-{occurrence + 1}";
            result.Add(new SectionEntry(label, anchor, match.Index, match.Groups[1].Length));
        }
        return result;
    }

    private static string Slug(string value) =>
        Regex.Replace(value.ToLowerInvariant(), "[^\\p{L}\\p{N}]+", "-").Trim('-');

    private static string Assess(AccessEvidence evidence)
    {
        if (evidence.StatusCode is 401 or 403 || evidence.RedirectedToLogin) return "login_likely";
        if (evidence.PasswordControl && evidence.BlockingForm) return "login_likely";
        return "unknown";
    }

    private static Inventory Project(Inventory raw, Projection projection) => new(
        projection.Blocks ? raw.Blocks : 0,
        projection.Tables ? raw.Tables : 0,
        projection.Media ? raw.Media : 0,
        projection.Receipt ? raw.Receipt : 0);

    private static int AllocateMarkdown(int budget, Inventory projected) =>
        Math.Max(ResponseBudgetPlanner.MinMarkdownTokens, budget - projected.Total);

    private sealed record SectionEntry(string Heading, string Anchor, int Offset, int Level);
    private sealed record AccessEvidence(int StatusCode, bool RedirectedToLogin, bool PasswordControl, bool BlockingForm, bool AuthenticationProse);
    private sealed record Projection(bool Blocks, bool Tables, bool Media, bool Receipt);
    private sealed record Inventory(int Blocks, int Tables, int Media, int Receipt)
    {
        public int Total => Blocks + Tables + Media + Receipt;
    }

    [GeneratedRegex("^(#{2,6})\\s+(.+)$", RegexOptions.Multiline)]
    private static partial Regex HeadingRegex();

    [GeneratedRegex("\\{#([^}]+)\\}\\s*$")]
    private static partial Regex AnchorRegex();
}
