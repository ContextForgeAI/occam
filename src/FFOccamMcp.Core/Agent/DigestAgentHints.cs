using OccamMcp.Core.Services;

namespace OccamMcp.Core.Agent;

public sealed record DigestDecision(
    string Action,
    string Reason,
    string? Url = null);

public static class DigestAgentHints
{
    public sealed record Hints(
        string SuggestedReadOrder,
        string[] Warnings,
        DigestDecision[] Decisions);

    public static Hints ForDigest(DigestAnalysis analysis)
    {
        var warnings = new List<string>();
        var decisions = new List<DigestDecision>();
        var okItems = analysis.Items.Where(i => i.Ok).ToList();
        var focusQuery = okItems
            .Select(i => i.FocusQuery)
            .FirstOrDefault(q => !string.IsNullOrWhiteSpace(q));

        var suggestedReadOrder = "combined";
        if (!string.IsNullOrWhiteSpace(focusQuery))
        {
            var weakMatches = okItems.Where(i => i.FocusMatched == false).ToList();
            var strongMatches = okItems.Where(i => i.FocusMatched == true).ToList();

            if (weakMatches.Count > 0)
            {
                warnings.Add(
                    $"check_items_before_combined: {weakMatches.Count} URL(s) weak focus match — prefer items[].excerpt over combined.");
                suggestedReadOrder = "items_by_focusMatched";
            }

            if (strongMatches.Count > 0 && weakMatches.Count > 0)
            {
                warnings.Add("hub_in_digest: index/TOC excerpts may mislead technical focus — read focusMatched per URL.");
            }

            // Honesty: every ok item missed the focus — do not present as a successful focused digest.
            if (analysis.FocusNotFound || (weakMatches.Count == okItems.Count && okItems.Count > 0))
            {
                warnings.Add(
                    "focus_not_found: every ok item has focusMatched=false — treat this digest as unfocused; do not cite combined as a focused answer.");
                decisions.Add(new DigestDecision(
                    "focus_not_found",
                    "No focus matches — discovery/excerpts did not hit the focus_query; refine focus_query or pick leaf URLs from map."));
                decisions.Add(new DigestDecision(
                    "iterate_items",
                    "No strong focus matches — do not cite combined as the sole source."));
                suggestedReadOrder = "items_only";
            }

            foreach (var hub in weakMatches.Where(LooksLikeHubExcerpt))
            {
                warnings.Add($"hub_excerpt:{hub.Url} — TOC/navigation excerpt; use leaf transcode for definitions.");
            }
        }

        if (analysis.Failed > 0)
        {
            warnings.Add($"partial_digest: {analysis.Failed} URL(s) failed — combined excludes failed items.");
            decisions.Add(new DigestDecision(
                "skip_failed",
                "Do not invent content for failed digest items."));
        }

        return new Hints(suggestedReadOrder, warnings.ToArray(), decisions.ToArray());
    }

    private static bool LooksLikeHubExcerpt(DigestItemResult item)
    {
        var excerpt = item.Excerpt ?? string.Empty;
        if (excerpt.Length < 80)
        {
            return false;
        }

        var linkCount = excerpt.Split('[').Length - 1;
        return linkCount >= 8
            && (excerpt.Contains("Guide", StringComparison.OrdinalIgnoreCase)
                || excerpt.Contains("table of contents", StringComparison.OrdinalIgnoreCase)
                || excerpt.Contains("In this article", StringComparison.OrdinalIgnoreCase));
    }
}
