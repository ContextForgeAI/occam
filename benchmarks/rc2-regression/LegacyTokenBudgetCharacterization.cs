using System.Text.RegularExpressions;
using OccamMcp.Core.Compile;

namespace OccamMcp.Rc2Regression;

/// <summary>Frozen RC.1 focus behavior used only to prove the characterization baseline remains intact.</summary>
internal static class LegacyTokenBudgetCharacterization
{
    public static (string Text, bool Truncated, string? Strategy) Apply(
        string text,
        int? maxTokens,
        string? focusQuery = null)
    {
        if (maxTokens is null || TokenEstimator.Estimate(text) <= maxTokens.Value)
        {
            return (text, false, null);
        }

        if (!string.IsNullOrWhiteSpace(focusQuery))
        {
            var focused = Focus(text, maxTokens.Value, focusQuery);
            if (!string.IsNullOrWhiteSpace(focused))
            {
                return (focused, true, "focus_window");
            }

            return (Sandwich(text, maxTokens.Value), true, "sandwich");
        }

        return (Head(text, maxTokens.Value), true, "head_safe");
    }

    private static string Focus(string text, int maxTokens, string focusQuery)
    {
        var ranked = SplitSections(text)
            .Select((section, ordinal) => (Section: section, Ordinal: ordinal, Score: Score(section, focusQuery)))
            .OrderByDescending(item => item.Score)
            .ThenBy(item => item.Ordinal)
            .ToArray();
        if (ranked.Length == 0 || ranked[0].Score <= 0)
        {
            return string.Empty;
        }

        var winner = ranked[0].Section;
        return TokenEstimator.Estimate(winner) <= maxTokens
            ? winner
            : Head(winner, maxTokens);
    }

    private static int Score(string section, string focusQuery)
    {
        var heading = section.Split('\n', 2)[0];
        var terms = Regex.Matches(focusQuery.ToLowerInvariant(), @"[\p{L}\p{N}]{3,}").Select(match => match.Value).ToArray();
        if (!FocusMatcher.MatchesMarkdown(section, focusQuery))
        {
            return FocusMatcher.MatchesMarkdown(heading, focusQuery) ? 2 : 0;
        }

        var score = 10 + terms.Count(term => heading.Contains(term, StringComparison.OrdinalIgnoreCase));
        if (heading.StartsWith("### ", StringComparison.Ordinal))
        {
            score += 3;
        }
        if (section.Contains(" is a ", StringComparison.OrdinalIgnoreCase)
            || section.Contains(" is an ", StringComparison.OrdinalIgnoreCase)
            || section.Contains(" refers to ", StringComparison.OrdinalIgnoreCase))
        {
            score += 4;
        }
        return score;
    }

    private static string Head(string text, int maxTokens)
    {
        var maxCharacters = Math.Min(text.Length, Math.Max(1, maxTokens * 4));
        return text[..maxCharacters].TrimEnd();
    }

    private static string Sandwich(string text, int maxTokens)
    {
        var maxCharacters = Math.Min(text.Length, Math.Max(1, maxTokens * 4));
        if (maxCharacters >= text.Length)
        {
            return text;
        }

        const string marker = "\n\n…\n\n";
        var available = Math.Max(2, maxCharacters - marker.Length);
        var headCharacters = Math.Max(1, (int)(available * 0.55));
        var tailCharacters = Math.Max(1, available - headCharacters);
        return text[..Math.Min(headCharacters, text.Length)].TrimEnd()
            + marker
            + text[Math.Max(headCharacters, text.Length - tailCharacters)..].TrimStart();
    }

    private static IReadOnlyList<string> SplitSections(string text)
    {
        var sections = new List<string>();
        var current = new List<string>();
        foreach (var line in text.Split('\n'))
        {
            if ((line.StartsWith("## ", StringComparison.Ordinal) || line.StartsWith("### ", StringComparison.Ordinal))
                && current.Count > 0)
            {
                sections.Add(string.Join('\n', current).Trim());
                current = [line];
            }
            else
            {
                current.Add(line);
            }
        }
        if (current.Count > 0)
        {
            sections.Add(string.Join('\n', current).Trim());
        }
        return sections.Where(section => section.Length > 0).ToArray();
    }
}
