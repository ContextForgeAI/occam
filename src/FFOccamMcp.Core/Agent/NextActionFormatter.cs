namespace OccamMcp.Core.Agent;

/// <summary>
/// Derives a single Donsetch-style <c>next_action</c> string from existing
/// <see cref="ProbeDecision"/> rows — no invented page content.
/// </summary>
public static class NextActionFormatter
{
    /// <summary>
    /// Primary operational hint: <c>action</c>, optional <c>(parameter)</c> / <c>tool=…</c>, then reason.
    /// Empty when there is nothing actionable.
    /// </summary>
    public static string? FromDecisions(IReadOnlyList<ProbeDecision>? decisions)
    {
        if (decisions is null || decisions.Count == 0)
        {
            return null;
        }

        var d = decisions[0];
        if (string.IsNullOrWhiteSpace(d.Action))
        {
            return null;
        }

        var action = d.Action.Trim();
        if (!string.IsNullOrWhiteSpace(d.Parameter))
        {
            action = $"{action}({d.Parameter.Trim()})";
        }

        if (string.IsNullOrWhiteSpace(d.Reason))
        {
            return string.IsNullOrWhiteSpace(d.Tool) ? action : $"{action} [{d.Tool.Trim()}]";
        }

        return string.IsNullOrWhiteSpace(d.Tool)
            ? $"{action}: {d.Reason.Trim()}"
            : $"{action}: {d.Reason.Trim()} [{d.Tool.Trim()}]";
    }

    /// <summary>
    /// Prefer rich decisions; otherwise map a non-<c>none</c> suggestedNext tool name.
    /// </summary>
    public static string? FromHints(
        IReadOnlyList<ProbeDecision>? decisions,
        string? suggestedNext)
    {
        var fromDecisions = FromDecisions(decisions);
        if (!string.IsNullOrWhiteSpace(fromDecisions))
        {
            return fromDecisions;
        }

        if (string.IsNullOrWhiteSpace(suggestedNext)
            || string.Equals(suggestedNext, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        return $"continue: tool={suggestedNext.Trim()}";
    }
}
