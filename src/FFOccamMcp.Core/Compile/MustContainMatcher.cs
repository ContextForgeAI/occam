using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

/// <summary>Post-extract must_contain probe: MATCH / NO_MATCH with up to three excerpts.</summary>
public static partial class MustContainMatcher
{
    public const int MaxExcerpts = 3;
    public const int ExcerptRadius = 80;

    public sealed record Result(string Verdict, IReadOnlyList<string> Excerpts, int HitCount);

    public static Result Evaluate(string markdown, string? needle)
    {
        if (string.IsNullOrWhiteSpace(needle))
        {
            return new Result("MATCH", [], 0);
        }

        var text = markdown ?? string.Empty;
        var query = needle.Trim();
        if (query.Length == 0)
        {
            return new Result("MATCH", [], 0);
        }

        var excerpts = new List<string>(MaxExcerpts);
        var comparison = StringComparison.OrdinalIgnoreCase;
        var start = 0;
        var hits = 0;
        while (hits < MaxExcerpts)
        {
            var idx = text.IndexOf(query, start, comparison);
            if (idx < 0)
            {
                break;
            }

            hits++;
            var from = Math.Max(0, idx - ExcerptRadius);
            var to = Math.Min(text.Length, idx + query.Length + ExcerptRadius);
            var slice = text[from..to].Replace("\r\n", "\n", StringComparison.Ordinal);
            slice = WhitespaceCollapse().Replace(slice, " ").Trim();
            excerpts.Add(slice);
            start = idx + query.Length;
        }

        return hits > 0
            ? new Result("MATCH", excerpts, hits)
            : new Result("NO_MATCH", [], 0);
    }

    [GeneratedRegex(@"\s+", RegexOptions.CultureInvariant)]
    private static partial Regex WhitespaceCollapse();
}
