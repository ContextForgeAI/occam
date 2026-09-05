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
        var groups = InstructionDependencies.Groups(blocks);
        var group = groups.FirstOrDefault(g => blocks.Skip(g.Start).Take(g.End - g.Start + 1)
            .Any(block => FocusMatcher.MatchesMarkdown(block, focusQuery)));
        if (groups.Count > 0)
        {
            if (group == default) group = groups[0];
            var instruction = string.Join("\n\n", new[] { heading }
                .Concat(blocks.Skip(group.Start).Take(group.End - group.Start + 1)));
            return new MinimumAnswerUnit(instruction, TokenEstimator.Estimate(instruction), "instruction");
        }
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
        return InstructionDependencies.IsStructured(block);
    }

    private static string StructuredKind(string block)
    {
        var first = block.TrimStart();
        return first.StartsWith("```", StringComparison.Ordinal) ? "code"
            : first.StartsWith('|') ? "table"
            : "list";
    }
}
