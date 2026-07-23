using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Semantics;
using OccamMcp.Core.Tools;

namespace OccamMcp.Core.Claims;

public interface IClaimCheckService
{
    ValueTask<(OccamClaimCheckSuccessResponse? Success, OccamClaimCheckFailureResponse? Failure)> CheckAsync(
        string url,
        string claim,
        OccamBackendPolicy policy,
        string? sessionProfile,
        int maxMatches,
        CancellationToken cancellationToken);
}

/// <summary>
/// SI-16: ground a claim in a page's provable source blocks. Extracts with json_blocks, ranks blocks
/// by BM25 relevance to the claim, and returns the top matches each with a Merkle citation proof plus
/// the signed extraction receipt — or an honest <c>found:false</c> when nothing clears the relevance
/// floor. Stance (support/refute) is left to the caller; this proves WHICH source text is relevant.
/// </summary>
public sealed class ClaimCheckService(TranscodePipeline pipeline, ReceiptSigner signer) : IClaimCheckService
{
    public async ValueTask<(OccamClaimCheckSuccessResponse? Success, OccamClaimCheckFailureResponse? Failure)> CheckAsync(
        string url,
        string claim,
        OccamBackendPolicy policy,
        string? sessionProfile,
        int maxMatches,
        CancellationToken cancellationToken)
    {
        var options = new OccamTranscodeOptions
        {
            JsonBlocks = true, // need blocks to rank + build citation proofs
            SessionProfile = sessionProfile,
            PlaybookPolicy = Playbooks.PlaybookPolicy.Auto,
        };

        var outcome = await pipeline.TranscodeAsync(url, policy, options, cancellationToken);
        var effectiveSigner = ReceiptsPolicy.Enabled() ? signer : null;

        if (!outcome.Ok || string.IsNullOrEmpty(outcome.Markdown))
        {
            var code = outcome.FailureCode ?? "extraction_failed";
            var negative = OccamTranscodeResponseBuilder.BuildNegativeReceipt(
                url, outcome.FinalUrl, outcome.Backend, code, outcome.StatusCode, effectiveSigner);
            return (null, new OccamClaimCheckFailureResponse(
                false, url, claim,
                new OccamClaimCheckFailureInfo(code, outcome.Message ?? "Extraction failed; cannot check the claim."),
                negative, DateTimeOffset.UtcNow.ToString("O")));
        }

        var blocks = outcome.Blocks ?? [];
        // Provable-absence gate: the block leaves are the COMPLETE extracted content only when the
        // extract wasn't token-truncated (claim_check never sets max_tokens/fit, so no compile pruning).
        // Signed into the receipt as leafSetComplete → a found:false becomes a provable "X is absent".
        var complete = blocks.Count > 0 && !outcome.Truncated;
        var receipt = OccamTranscodeResponseBuilder.BuildReceipt(
            outcome, url, effectiveSigner, leafSetComplete: complete)?.Signed;

        // No blocks → nothing provable to match against; honest found:false (absence NOT proven).
        if (blocks.Count == 0)
        {
            return (BuildResponse(url, claim, found: false, root: receipt?.BlockMerkleRoot, receipt, [], proven: false), null);
        }

        (string Text, string? SourceSelector)[] merkleBlocks =
            [.. blocks.Select(b => (b.Text ?? string.Empty, (string?)b.SourceSelector))];
        var leaves = MerkleTree.LeafHashesHex(merkleBlocks);

        var ranked = ClaimBlockRanker.Rank(blocks, claim);
        var matches = ranked
            .Where(r => r.ClearsFloor)
            .Take(Math.Clamp(maxMatches, 1, 10))
            .Select(r => new OccamClaimMatchInfo(
                r.Index,
                blocks[r.Index].Text ?? string.Empty,
                string.IsNullOrEmpty(blocks[r.Index].SourceSelector) ? null : blocks[r.Index].SourceSelector,
                Math.Round(r.Score, 4),
                leaves[r.Index],
                [.. MerkleTree.Proof(leaves, r.Index)]))
            .ToArray();

        var root = receipt?.BlockMerkleRoot ?? MerkleTree.RootFromLeafHashes(leaves);
        var found = matches.Length > 0;
        // found → proven irrelevant (null). Not found → absence is proven iff the leaf set is complete.
        return (BuildResponse(url, claim, found, root, receipt, matches, proven: found ? null : complete), null);
    }

    private OccamClaimCheckSuccessResponse BuildResponse(
        string url, string claim, bool found, string? root, ReceiptEnvelope? receipt,
        OccamClaimMatchInfo[] matches, bool? proven = null) =>
        new(
            Ok: true,
            Url: url,
            Claim: claim,
            Found: found,
            Retrieved: found,
            Verdict: SemanticVerdict.NotEvaluated,
            BlockMerkleRoot: root,
            KeyId: receipt?.KeyId,
            Matches: matches,
            Receipt: receipt,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"),
            Proven: proven);

}
