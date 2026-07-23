using System.Globalization;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Watch;

public sealed record WatchVerdict(
    bool FirstSeen,
    bool Changed,
    OccamTranscodeDiffInfo? Diff,
    string[] BlockHashes,
    int? ContentDeltaTokens = null);

/// <summary>Pure change-detection verdict from a prior record + the current extraction. Testable.</summary>
public static class WatchEvaluator
{
    public static WatchVerdict Evaluate(
        WatchRecord? prior,
        string contentHash,
        IReadOnlyList<WorkerExtractBlockInfo> blocks,
        bool includeDiff)
    {
        var currentHashes = new string[blocks.Count];
        for (var i = 0; i < blocks.Count; i++)
        {
            currentHashes[i] = BlockDiff.Hash(blocks[i]);
        }

        if (prior is null)
        {
            return new WatchVerdict(FirstSeen: true, Changed: false, Diff: null, BlockHashes: currentHashes);
        }

        var changed = !string.Equals(contentHash, prior.ContentHash, StringComparison.OrdinalIgnoreCase);
        if (!changed)
        {
            return new WatchVerdict(FirstSeen: false, Changed: false, Diff: null, BlockHashes: currentHashes, ContentDeltaTokens: 0);
        }

        // Freshness magnitude: how much NEW content appeared. Compute the block delta whenever the page
        // changed (cheap, hash-based) so content_delta_tokens is always available; expose the full diff
        // only when the caller asked for it.
        var fullDiff = BlockDiff.Compute(blocks, prior.BlockHashes);
        var deltaTokens = fullDiff.AddedBlocks.Sum(b => FocusMatcher.Tokenize(b.Text).Count);
        return new WatchVerdict(
            FirstSeen: false,
            Changed: true,
            Diff: includeDiff ? fullDiff : null,
            BlockHashes: currentHashes,
            ContentDeltaTokens: deltaTokens);
    }
}

public interface IWatchService
{
    ValueTask<(OccamWatchSuccessResponse? Success, OccamWatchFailureInfo? Failure)> WatchAsync(
        string url,
        OccamBackendPolicy policy,
        OccamTranscodeOptions options,
        bool reset,
        bool includeDiff,
        bool includeHistory,
        CancellationToken cancellationToken);
}

/// <summary>
/// Stateful page-change watch (opt-in). Extracts the page (with block hashing), compares against the
/// last-seen record, persists the new state, and reports <c>changed</c> + an optional block-level
/// diff. The agent calls it on its own cadence (no daemon in Core); state lives in a small JSON store.
/// </summary>
public sealed class WatchService(TranscodePipeline pipeline, IWatchStore store, ReceiptSigner signer) : IWatchService
{
    // SI-05: cap the retained chain so watch.json stays bounded. The chain stays verifiable over the
    // retained window (verification is window-relative; a pruned prefix only means the head is older).
    private const int MaxHistoryEntries = 64;

    public async ValueTask<(OccamWatchSuccessResponse? Success, OccamWatchFailureInfo? Failure)> WatchAsync(
        string url,
        OccamBackendPolicy policy,
        OccamTranscodeOptions options,
        bool reset,
        bool includeDiff,
        bool includeHistory,
        CancellationToken cancellationToken)
    {
        // Block hashes drive the diff, so force the worker to emit blocks.
        var effective = options with { JsonBlocks = true };
        var outcome = await pipeline.TranscodeAsync(url, policy, effective, cancellationToken);
        if (!outcome.Ok || string.IsNullOrEmpty(outcome.Markdown))
        {
            return (null, new OccamWatchFailureInfo(
                outcome.FailureCode ?? "extraction_failed",
                outcome.Message ?? "Watch extraction failed; last-seen state unchanged."));
        }

        var contentHash = ContentHashToken.BareHex(outcome.Markdown);
        var blocks = outcome.Blocks ?? [];
        var prior = reset ? null : store.Get(url);
        var verdict = WatchEvaluator.Evaluate(prior, contentHash, blocks, includeDiff);

        var now = DateTimeOffset.UtcNow;
        var firstSeenAt = prior?.FirstSeenAt ?? now;
        var lastChangedAt = verdict.FirstSeen
            ? null
            : verdict.Changed ? now : prior?.LastChangedAt;

        // SI-05: append a signed entry on a real event (first sighting or a change); an unchanged call
        // adds nothing. Reset restarts the chain from a fresh genesis.
        var priorHistory = reset ? [] : prior?.History ?? [];
        var (history, latestEntry) = AppendHistory(priorHistory, verdict, contentHash, blocks, now);

        store.Upsert(new WatchRecord
        {
            Url = url,
            ContentHash = contentHash,
            BlockHashes = verdict.BlockHashes,
            FirstSeenAt = firstSeenAt,
            LastSeenAt = now,
            LastChangedAt = lastChangedAt,
            History = history,
        });

        var response = new OccamWatchSuccessResponse(
            Ok: true,
            Url: url,
            FirstSeen: verdict.FirstSeen,
            Changed: verdict.Changed,
            ContentHash: contentHash,
            BlockCount: blocks.Count,
            FirstSeenAt: firstSeenAt,
            LastSeenAt: now,
            LastChangedAt: lastChangedAt,
            Diff: verdict.Changed ? verdict.Diff : null,
            Backend: outcome.Backend,
            ContentDeltaTokens: verdict.ContentDeltaTokens,
            HistoryLength: history.Length,
            LatestEntry: latestEntry,
            History: includeHistory ? history : null);

        return (response, null);
    }

    private (WatchHistoryEntry[] History, WatchHistoryEntry? Latest) AppendHistory(
        WatchHistoryEntry[] priorHistory,
        WatchVerdict verdict,
        string contentHash,
        IReadOnlyList<WorkerExtractBlockInfo> blocks,
        DateTimeOffset now)
    {
        if (!verdict.FirstSeen && !verdict.Changed)
        {
            return (priorHistory, null); // unchanged — no new event
        }

        var merkleBlocks = blocks.Count == 0
            ? Array.Empty<(string, string?)>()
            : [.. blocks.Select(b => (b.Text, (string?)b.SourceSelector))];

        var entry = WatchHistoryChain.Append(
            priorHistory,
            verdict.FirstSeen ? WatchHistoryEntry.EventFirstSeen : WatchHistoryEntry.EventChanged,
            "sha256:" + contentHash,
            MerkleTree.Root(merkleBlocks),
            verdict.ContentDeltaTokens,
            now.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture),
            EffectiveSigner());

        var appended = new WatchHistoryEntry[priorHistory.Length + 1];
        Array.Copy(priorHistory, appended, priorHistory.Length);
        appended[^1] = entry;

        // Cap: keep the most recent window (chain stays valid over it).
        var capped = appended.Length > MaxHistoryEntries
            ? appended[^MaxHistoryEntries..]
            : appended;
        return (capped, entry);
    }

    private ReceiptSigner? EffectiveSigner() =>
        ReceiptsPolicy.Enabled() ? signer : null;
}
