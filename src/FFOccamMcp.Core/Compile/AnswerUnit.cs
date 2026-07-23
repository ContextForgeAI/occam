namespace OccamMcp.Core.Compile;

public sealed record MinimumAnswerUnit(string Text, int Tokens, string EvidenceKind);

/// <summary>Selects a bounded heading/body unit that remains useful when wider context is trimmed.</summary>
public static class AnswerUnitSelector
{
    public static MinimumAnswerUnit? Select(SectionEntry section, string? focusQuery)
    {
        ArgumentNullException.ThrowIfNull(section);
        var blocks = SplitBlocks(section.Body);
        if (blocks.Count == 0)
        {
            return null;
        }

        var heading = $"{new string('#', Math.Max(1, section.Level))} {section.Heading}";
        var prose = blocks.FirstOrDefault(block => !IsStructured(block));
        var structuredIndex = blocks.FindIndex(IsStructured);
        var parts = new List<string> { heading };
        if (!string.IsNullOrWhiteSpace(prose))
        {
            parts.Add(prose);
        }

        var evidenceKind = "prose";
        if (structuredIndex >= 0)
        {
            if (structuredIndex > 0)
            {
                var label = blocks[structuredIndex - 1];
                if (label.Length <= 160 && !parts.Contains(label, StringComparer.Ordinal))
                {
                    parts.Add(label);
                }
            }
            parts.Add(blocks[structuredIndex]);
            evidenceKind = StructuredKind(blocks[structuredIndex]);
        }

        var text = string.Join("\n\n", parts.Distinct(StringComparer.Ordinal));
        return new MinimumAnswerUnit(text, TokenEstimator.Estimate(text), evidenceKind);
    }

    internal static List<string> SplitBlocks(string markdown) =>
        markdown.Replace("\r\n", "\n", StringComparison.Ordinal)
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(block => block.Length > 0)
            .ToList();

    private static bool IsStructured(string block)
    {
        var first = block.Split('\n', 2)[0].TrimStart();
        return first.StartsWith("- ", StringComparison.Ordinal)
            || first.StartsWith("* ", StringComparison.Ordinal)
            || first.StartsWith("```", StringComparison.Ordinal)
            || first.StartsWith('|');
    }

    private static string StructuredKind(string block)
    {
        var first = block.TrimStart();
        return first.StartsWith("```", StringComparison.Ordinal) ? "code"
            : first.StartsWith('|') ? "table"
            : "list";
    }
}
