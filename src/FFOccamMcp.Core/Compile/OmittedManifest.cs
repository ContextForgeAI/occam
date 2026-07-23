namespace OccamMcp.Core.Compile;

/// <summary>
/// #7 omitted-manifest: a structured, machine-readable record of what max_tokens budgeting dropped
/// from the returned markdown. The token budgeter leaves in-band SNIP comments
/// (<c>&lt;!-- SNIP: … --&gt;</c>); this promotes the same information to a first-class field so a
/// consuming agent KNOWS there are holes — and their size and shape — instead of inferring
/// completeness from a silently-truncated body. Present only when truncation actually occurred.
/// </summary>
/// <param name="Reason">Truncation strategy that produced the holes: head_safe / sandwich / focus_window.</param>
/// <param name="TokensDropped">Estimated tokens removed (pre-budget estimate minus the returned estimate).</param>
/// <param name="Regions">Where content was cut: tail / middle / unchosen.</param>
/// <param name="SectionsOmitted">Count of top-level markdown sections that vanished from the returned body (null when none / not applicable).</param>
/// <param name="Structured">Counts of structured sidecar items dropped by the unified response budget planner (null when none).</param>
public sealed record OmittedManifest(
    string Reason,
    int TokensDropped,
    IReadOnlyList<string> Regions,
    int? SectionsOmitted = null,
    ResponseBudgetDropped? Structured = null);

public static class OmittedManifestBuilder
{
    /// <summary>
    /// Builds the manifest from first-class facts (the strategy and a before/after diff), NOT from the
    /// in-band SNIP markers — a later re-truncation (definitional-anchor fit) can rewrite those markers,
    /// so scraping them misreports the hole. Returns null when nothing was truncated (the honest
    /// "no holes" answer is the absence of the field, not an empty manifest).
    /// </summary>
    /// <param name="preBudgetText">The markdown fed into token budgeting (post-selectors/fit).</param>
    /// <param name="finalText">The returned markdown after budgeting.</param>
    public static OmittedManifest? Build(
        string preBudgetText,
        string finalText,
        bool truncated,
        string? strategy,
        int tokensBefore,
        int tokensAfter)
    {
        if (!truncated)
        {
            return null;
        }

        // The strategy is the authoritative record of which cut ran; derive the region from it rather
        // than parsing markers a downstream stage may have clobbered.
        var region = strategy switch
        {
            "sandwich" => "middle",
            "focus_window" => "unchosen",
            _ => "tail",
        };

        var sectionsBefore = CountSections(preBudgetText);
        var sectionsAfter = CountSections(finalText);
        var sectionsOmitted = sectionsBefore - sectionsAfter;

        var dropped = Math.Max(0, tokensBefore - tokensAfter);
        return new OmittedManifest(
            strategy ?? "head_safe",
            dropped,
            [region],
            sectionsOmitted > 0 ? sectionsOmitted : null);
    }

    // Counts top-level markdown section headings (## / ###), matching how the budgeter splits
    // sections. A cheap, marker-independent proxy for "how many sections disappeared".
    private static int CountSections(string text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return 0;
        }

        var count = 0;
        foreach (var line in text.Split('\n'))
        {
            if (line.StartsWith("## ", StringComparison.Ordinal)
                || line.StartsWith("### ", StringComparison.Ordinal))
            {
                count++;
            }
        }

        return count;
    }
}
