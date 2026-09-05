using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

/// <summary>Local Markdown dependencies used by selection and materialization, never by codecs.</summary>
internal static partial class InstructionDependencies
{
    internal static bool IsStructured(string text)
    {
        var first = text.TrimStart();
        return first.StartsWith("```", StringComparison.Ordinal)
            || first.StartsWith("~~~", StringComparison.Ordinal)
            || first.StartsWith('|')
            || (first.StartsWith('`') && first.EndsWith('`'))
            || ListStart().IsMatch(first);
    }

    // Navigation link lists are independent destinations, not procedural steps.
    internal static bool IsInstruction(string text) => IsStructured(text)
        && !text.Split('\n').Where(line => !string.IsNullOrWhiteSpace(line))
            .All(line => NavigationItem().IsMatch(line.Trim()));

    internal static IReadOnlyList<(int Start, int End)> Groups(IReadOnlyList<string> blocks)
    {
        var groups = new List<(int Start, int End)>();
        for (var i = 0; i < blocks.Count; i++)
        {
            if (!IsInstruction(blocks[i])) continue;
            var start = i > 0 && IsContext(blocks[i - 1]) ? i - 1 : i;
            var end = i + 1 < blocks.Count && IsContext(blocks[i + 1]) ? i + 1 : i;
            if (groups.Count > 0 && groups[^1].End >= start)
                groups[^1] = (groups[^1].Start, end);
            else
                groups.Add((start, end));
        }
        return groups;
    }

    private static bool IsContext(string text) => !text.StartsWith('#')
        && !text.StartsWith("<!--", StringComparison.Ordinal)
        && !StandaloneLink().IsMatch(text.Trim());

    [GeneratedRegex(@"^\[[^\]]*\]\([^)]+\)$", RegexOptions.CultureInvariant)]
    private static partial Regex StandaloneLink();

    [GeneratedRegex(@"^(?:[-*+]\s|\d+[.)]\s)", RegexOptions.CultureInvariant)]
    private static partial Regex ListStart();

    [GeneratedRegex(@"^(?:[-*+]\s+|\d+[.)]\s+)\[[^\]]+\]\([^)]+\)\s*$", RegexOptions.CultureInvariant)]
    private static partial Regex NavigationItem();
}
