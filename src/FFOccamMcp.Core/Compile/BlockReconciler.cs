using System.Text;
using System.Text.RegularExpressions;
using OccamMcp.Core.Claims;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Compile;

/// <summary>
/// SI-02 trust integrity. The workers extract a block list from the FULL page, but the compiler then
/// prunes the markdown (max_tokens / fit_markdown / content_selectors / truncation). If the blocks are
/// left untouched, the receipt's <c>blockMerkleRoot</c> + <c>blockLeaves</c> — and the returned
/// <c>blocks[]</c> — would describe content that is no longer in the returned markdown: a citation proof
/// could "prove" a block the reader can't see, and <c>contentHash</c> (post-prune) would disagree with
/// the block root (pre-prune). This reconciles the block list to the compiled markdown: a block survives
/// only if its (normalized) text is still present — or, for list/paragraph rewrites, a derived subset
/// whose every kept line appears in the markdown. Erring toward dropping is the safe direction.
/// </summary>
public static partial class BlockReconciler
{
    /// <summary>
    /// Blocks still present in <paramref name="compiledMarkdown"/>. Returns the input unchanged when
    /// there are no blocks or the compiler did not change the markdown (the common no-prune path).
    /// </summary>
    public static IReadOnlyList<WorkerExtractBlockInfo>? SurvivingBlocks(
        IReadOnlyList<WorkerExtractBlockInfo>? blocks, string compiledMarkdown, string originalMarkdown)
    {
        if (blocks is null || blocks.Count == 0)
        {
            return blocks;
        }

        // No prune happened → every block still corresponds to the returned markdown.
        if (string.Equals(compiledMarkdown, originalMarkdown, StringComparison.Ordinal))
        {
            return blocks;
        }

        var haystack = NormalizeForMatch(compiledMarkdown);
        var survivors = new List<WorkerExtractBlockInfo>(blocks.Count);
        foreach (var b in blocks)
        {
            if (string.IsNullOrWhiteSpace(b.Text))
            {
                continue;
            }

            var needle = NormalizeForMatch(b.Text);
            if (needle.Length > 0 && haystack.Contains(needle, StringComparison.Ordinal))
            {
                survivors.Add(b);
                continue;
            }

            // Focus pruning may filter list lines or strip emphasis — keep a derived block whose
            // surviving lines are literally present (fail-closed: never invent text).
            if (TryDeriveSurvivingText(b.Text, haystack, out var derived) && derived.Length > 0)
            {
                survivors.Add(CloneWithText(b, derived));
            }
        }

        return survivors;
    }

    /// <summary>
    /// Among reconciled survivors, order focus-relevant blocks first (by BM25 salience). Does not
    /// invent or drop blocks here — budget trim + <see cref="DropFocusIrrelevantKeepers"/> handle
    /// filler-only leftovers.
    /// </summary>
    public static IReadOnlyList<WorkerExtractBlockInfo>? PrioritizeForFocus(
        IReadOnlyList<WorkerExtractBlockInfo>? blocks, string? focusQuery)
    {
        if (blocks is null || blocks.Count == 0 || string.IsNullOrWhiteSpace(focusQuery))
        {
            return blocks;
        }

        var ranks = ClaimBlockRanker.Rank(blocks, focusQuery);
        var scoreByIndex = ranks.ToDictionary(r => r.Index, r => r);
        bool IsRelevant(int index)
        {
            if (!scoreByIndex.TryGetValue(index, out var r))
            {
                return FocusMatcher.MatchesMarkdown(blocks[index].Text, focusQuery);
            }

            return r.ClearsFloor || FocusMatcher.MatchesMarkdown(blocks[index].Text, focusQuery);
        }

        return blocks
            .Select((b, i) => (Block: b, Index: i, Relevant: IsRelevant(i), Score: scoreByIndex.TryGetValue(i, out var r) ? r.Score : 0.0))
            .OrderByDescending(x => x.Relevant)
            .ThenByDescending(x => x.Score)
            .ThenBy(x => x.Index)
            .Select(x => x.Block)
            .ToList();
    }

    /// <summary>How many blocks clear the focus relevance floor (or match via <see cref="FocusMatcher"/>).</summary>
    public static int CountFocusRelevant(IReadOnlyList<WorkerExtractBlockInfo> blocks, string focusQuery) =>
        CountRelevant(blocks, focusQuery);

    /// <summary>
    /// After structured budget trim: if the kept set has no focus-relevant blocks while the pre-trim
    /// set did, clear the kept set (do not retain unrelated fillers just to avoid an empty array).
    /// </summary>
    public static IReadOnlyList<WorkerExtractBlockInfo>? DropFocusIrrelevantKeepers(
        IReadOnlyList<WorkerExtractBlockInfo>? kept,
        IReadOnlyList<WorkerExtractBlockInfo>? availableBeforeTrim,
        string? focusQuery,
        out int droppedAsIrrelevant)
    {
        droppedAsIrrelevant = 0;
        if (kept is null || kept.Count == 0 || string.IsNullOrWhiteSpace(focusQuery))
        {
            return kept;
        }

        var keptRelevant = CountRelevant(kept, focusQuery);
        if (keptRelevant > 0)
        {
            return kept;
        }

        var availableRelevant = availableBeforeTrim is null ? 0 : CountRelevant(availableBeforeTrim, focusQuery);
        if (availableRelevant == 0)
        {
            return kept;
        }

        droppedAsIrrelevant = kept.Count;
        return Array.Empty<WorkerExtractBlockInfo>();
    }

    /// <summary>
    /// Collapse whitespace, strip common Markdown emphasis/link wrappers so prune reflow does not
    /// false-drop a block whose meaning survived. Does not fuzzy-match across rewritten wording.
    /// </summary>
    public static string NormalizeForMatch(string s)
    {
        if (string.IsNullOrEmpty(s))
        {
            return string.Empty;
        }

        var stripped = LinkMarkdownRegex().Replace(s, "$1");
        stripped = EmphasisRegex().Replace(stripped, "$2");
        return NormalizeWhitespace(stripped);
    }

    /// <summary>
    /// Collapse every run of whitespace to a single space and trim, so markdown reflow / re-indentation
    /// does not cause a false drop — while a truncated or removed block still fails the containment test.
    /// </summary>
    public static string NormalizeWhitespace(string s)
    {
        var sb = new StringBuilder(s.Length);
        var pendingSpace = false;
        foreach (var ch in s)
        {
            if (char.IsWhiteSpace(ch))
            {
                pendingSpace = sb.Length > 0;
                continue;
            }

            if (pendingSpace)
            {
                sb.Append(' ');
                pendingSpace = false;
            }

            sb.Append(ch);
        }

        return sb.ToString();
    }

    private static int CountRelevant(IReadOnlyList<WorkerExtractBlockInfo> blocks, string focusQuery)
    {
        var ranks = ClaimBlockRanker.Rank(blocks, focusQuery);
        var n = 0;
        foreach (var r in ranks)
        {
            if (r.ClearsFloor || FocusMatcher.MatchesMarkdown(blocks[r.Index].Text, focusQuery))
            {
                n++;
            }
        }

        return n;
    }

    private static bool TryDeriveSurvivingText(string blockText, string haystack, out string derived)
    {
        derived = string.Empty;
        var lines = blockText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        if (lines.Length < 2)
        {
            return false;
        }

        var kept = new List<string>(lines.Length);
        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.Length == 0)
            {
                continue;
            }

            var needle = NormalizeForMatch(trimmed);
            if (needle.Length >= 8 && haystack.Contains(needle, StringComparison.Ordinal))
            {
                kept.Add(trimmed);
            }
        }

        // Require at least half of the non-empty lines (and ≥1) so a single accidental echo cannot
        // resurrect a mostly-pruned block.
        var nonEmpty = lines.Count(static l => !string.IsNullOrWhiteSpace(l));
        if (kept.Count == 0 || kept.Count * 2 < nonEmpty)
        {
            return false;
        }

        derived = string.Join("\n", kept);
        return true;
    }

    private static WorkerExtractBlockInfo CloneWithText(WorkerExtractBlockInfo source, string text) =>
        new()
        {
            Type = source.Type,
            Text = text,
            SourceSelector = source.SourceSelector,
            Level = source.Level,
            Links = source.Links,
            Salience = source.Salience,
            Trust = source.Trust,
        };

    [GeneratedRegex(@"\[([^\]]+)\]\([^)]*\)", RegexOptions.CultureInvariant)]
    private static partial Regex LinkMarkdownRegex();

    [GeneratedRegex(@"(\*{1,3}|_{1,3}|`+)([^*_`]+)\1", RegexOptions.CultureInvariant)]
    private static partial Regex EmphasisRegex();
}
