using System.ComponentModel;
using System.Text.Json;
using ModelContextProtocol.Server;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Watch;

namespace OccamMcp.Core.Tools;

/// <summary>
/// occam_verify — the consumer half of Receipt v1. Modes:
/// <list type="bullet">
/// <item><c>offline</c> (default): signature + optional contentHash match.</item>
/// <item><c>live</c>: re-fetch finalUrl; whole-content drift + SI-02 granular "N/M blocks still present".</item>
/// <item><c>prove</c> (SI-02b): emit a compact citation package for one block (leaf + Merkle path).</item>
/// <item><c>citation</c> (SI-02b): verify someone else's block + proof against the signed root — no page needed.</item>
/// <item><c>history</c> (SI-05): verify a signed watch-history chain — links + per-entry signatures.</item>
/// </list>
/// Key trust (who owns k1:…) is out of scope for v1 (that is the registry, SI-08); pass the expected
/// <c>public_key</c> or omit for this host's local key.
/// </summary>
[McpServerToolType]
public sealed class OccamVerifyTool(TranscodePipeline pipeline, ReceiptSigner localSigner)
{
    [McpServerTool(Name = "occam_verify"), Description("Verify or cite an extraction receipt without trusting FF-Occam. Modes: offline = check the signature (+ contentHash if you pass the markdown); live = re-fetch and report how much drifted and which of your RAG chunks went stale; prove = emit a compact proof that one block was in the page; citation = verify such a block+proof against the signed root without the page; history = verify a signed occam_watch change-chain.")]
    public async Task<string> Verify(
        [Description("The receipt JSON: a transcode response's `receipt` object ({signed, blockLeaves}) or a bare signed envelope; OR a proof-carrying `occam://capsule/…` capsule (an agent-to-agent bundle that also carries its own markdown, verified offline with no re-fetch). In history mode, the watch `history` array or `{history:[...]}`.")] string receipt,
        [Description("Optional extracted markdown to check against contentHash (offline).")] string? markdown = null,
        [Description("Optional PEM public key to verify against; omit to use this host's local key.")] string? public_key = null,
        [Description("offline (default) | live | prove | citation | history.")] string mode = "offline",
        [Description("prove mode: index of the block to build a citation proof for.")] int? block_index = null,
        [Description("citation mode: the block's text.")] string? block_text = null,
        [Description("citation mode: the block's source_selector.")] string? block_selector = null,
        [Description("citation mode: the proof JSON (array of {hash, siblingIsRight}) from a prove call.")] string? proof = null,
        [Description("live mode (SI-12): JSON array of chunk leaf-hashes your RAG store holds for this URL — the response reports which of THESE went stale. Omit to check the receipt's own block leaves.")] string? chunks = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var publicKeyPem = public_key ?? localSigner.ExportPublicKeyPem();

        // SI-05: history mode verifies a chain, not a single receipt envelope — branch before the
        // receipt parse (a history array is not a valid envelope).
        if (string.Equals(mode, "history", StringComparison.OrdinalIgnoreCase))
        {
            return History(receipt, publicKeyPem);
        }

        ReceiptEnvelope? envelope;
        string[]? leaves;
        ReceiptTimeAnchor? anchor;
        // The proof-carrying capsule (HARNESS-P0) is the agent-to-agent form: an `occam://capsule/…`
        // URI that unpacks to the same {signed, blockLeaves, timeAnchor} plus the markdown it commits
        // to — so a receiving agent verifies offline without re-fetching. Its own content supplies the
        // contentHash check when the caller didn't pass `markdown` separately.
        var effectiveMarkdown = markdown;
        if (CapsuleCodec.IsCapsule(receipt))
        {
            if (!CapsuleCodec.TryParse(receipt, out var capsule) || capsule?.Signed is null)
            {
                return Fail("invalid_receipt", "Not a valid occam://capsule/… capsule.");
            }

            envelope = capsule.Signed;
            leaves = capsule.BlockLeaves;
            anchor = capsule.TimeAnchor;
            effectiveMarkdown ??= capsule.Content;
        }
        else if (!TryParseReceipt(receipt, out envelope, out leaves, out anchor) || envelope is null)
        {
            return Fail("invalid_receipt", "Receipt is not valid receipt JSON.");
        }

        return mode?.ToLowerInvariant() switch
        {
            "prove" => Prove(envelope, leaves, block_index),
            "citation" => Citation(envelope, publicKeyPem, block_text, block_selector, proof),
            "live" => await OfflineOrLiveAsync(envelope, publicKeyPem, effectiveMarkdown, leaves, anchor, ParseChunks(chunks), live: true, cancellationToken),
            _ => await OfflineOrLiveAsync(envelope, publicKeyPem, effectiveMarkdown, leaves, anchor, null, live: false, cancellationToken),
        };
    }

    // SI-05: verify a signed watch-history chain — consecutive links + each entry's signature.
    private string History(string historyJson, string publicKeyPem)
    {
        var entries = ParseHistory(historyJson);
        if (entries is null)
        {
            return Fail("invalid_arguments", "history mode needs a JSON array of entries, or an object with a `history` array.");
        }

        var chainValid = WatchHistoryChain.Verify(entries, publicKeyPem);
        var signedCount = entries.Count(e => e.Sig is not null);
        var headSeq = entries.Length == 0 ? -1 : entries[^1].Seq;
        var keyId = entries.FirstOrDefault(e => e.Sig is not null)?.KeyId ?? string.Empty;

        return JsonSerializer.Serialize(
            new OccamVerifySuccessResponse(
                Ok: true,
                SignatureValid: chainValid,
                ContentHashMatch: null,
                KeyId: keyId,
                Mode: "history",
                Live: null,
                Verdict: chainValid ? "history_verified" : "history_invalid",
                History: new OccamVerifyHistoryInfo(entries.Length, signedCount, headSeq, chainValid)),
            OccamVerifyJsonContext.Default.OccamVerifySuccessResponse);
    }

    /// <summary>SI-12: parse the caller's chunk leaf-hash array (JSON string array); null on absence/malformed.</summary>
    private static string[]? ParseChunks(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize(json, OccamVerifyJsonContext.Default.StringArray);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    /// <summary>Accept a bare entries array or an object <c>{ history: [...] }</c> (the watch response shape).</summary>
    private static WatchHistoryEntry[]? ParseHistory(string json)
    {
        try
        {
            var trimmed = json.TrimStart();
            if (trimmed.StartsWith('['))
            {
                return JsonSerializer.Deserialize(json, OccamVerifyJsonContext.Default.WatchHistoryEntryArray);
            }

            var wrapper = JsonSerializer.Deserialize(json, OccamVerifyJsonContext.Default.OccamVerifyHistoryInput);
            return wrapper?.History;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private async Task<string> OfflineOrLiveAsync(
        ReceiptEnvelope envelope, string publicKeyPem, string? markdown, string[]? leaves,
        ReceiptTimeAnchor? anchor, string[]? callerChunks, bool live, CancellationToken ct)
    {
        var offline = ReceiptVerifier.VerifyOffline(envelope, publicKeyPem, markdown);
        if (offline.Verdict == ReceiptVerification.InvalidReceipt)
        {
            return Fail("invalid_receipt", "Receipt is missing a signature or has an unsupported version.");
        }

        // SI-15: independently verify the time anchor (if any) over the receipt's signature.
        OccamVerifyTimeAnchorInfo? anchorInfo = null;
        if (anchor is not null)
        {
            var ta = TimeAnchorVerifier.Verify(anchor, envelope.Sig);
            anchorInfo = new OccamVerifyTimeAnchorInfo(ta.Present, ta.Valid, ta.GenTime, ta.Tsa, ta.TsaSubject);
        }

        OccamVerifyLiveInfo? liveInfo = null;
        var verdict = offline.Verdict;

        if (live && offline.SignatureValid)
        {
            var outcome = await pipeline.TranscodeAsync(
                envelope.FinalUrl,
                OccamBackendPolicy.HttpThenBrowser,
                new OccamTranscodeOptions { JsonBlocks = envelope.BlockMerkleRoot is not null },
                ct);

            if (!outcome.Ok || string.IsNullOrEmpty(outcome.Markdown))
            {
                liveInfo = new OccamVerifyLiveInfo(false, null, null);
                verdict = "refetch_failed";
            }
            else
            {
                var contentMatch = ReceiptCanonicalizer.ContentHash(outcome.Markdown) == envelope.ContentHash;
                (string Text, string? SourceSelector)[]? newBlocks = outcome.Blocks is null
                    ? null
                    : [.. outcome.Blocks.Select(b => (b.Text, (string?)b.SourceSelector))];

                bool? rootMatch = envelope.BlockMerkleRoot is not null && newBlocks is not null
                    ? MerkleTree.Root(newBlocks) == envelope.BlockMerkleRoot
                    : null;

                int? blocksTotal = null, blocksStillPresent = null;
                double? drift = null;
                OccamVerifyChunkStalenessInfo? chunkStaleness = null;
                if (newBlocks is not null)
                {
                    var newLeaves = new HashSet<string>(MerkleTree.LeafHashesHex(newBlocks), StringComparer.Ordinal);
                    if (leaves is { Length: > 0 })
                    {
                        blocksTotal = leaves.Length;
                        blocksStillPresent = leaves.Count(newLeaves.Contains);
                        drift = Math.Round(1.0 - ((double)blocksStillPresent.Value / blocksTotal.Value), 3);
                    }

                    // SI-12: report WHICH specific chunks went stale — the caller's `chunks` set if
                    // supplied, else the receipt's own block leaves. Lets a RAG store invalidate
                    // individual fragments instead of the whole document.
                    var sourceChunks = callerChunks is { Length: > 0 } ? callerChunks : leaves;
                    if (sourceChunks is { Length: > 0 })
                    {
                        var st = ChunkStalenessEvaluator.Compute(sourceChunks, newLeaves);
                        chunkStaleness = new OccamVerifyChunkStalenessInfo(st.Total, st.Present, st.Stale, st.StaleChunks);
                    }
                }

                liveInfo = new OccamVerifyLiveInfo(true, contentMatch, rootMatch, blocksTotal, blocksStillPresent, drift, chunkStaleness);
                verdict = contentMatch ? ReceiptVerification.Verified : "drifted";
            }
        }

        return JsonSerializer.Serialize(
            new OccamVerifySuccessResponse(
                Ok: true,
                SignatureValid: offline.SignatureValid,
                ContentHashMatch: offline.ContentHashMatch,
                KeyId: envelope.KeyId,
                Mode: live ? "live" : "offline",
                Live: liveInfo,
                Verdict: verdict,
                TimeAnchor: anchorInfo),
            OccamVerifyJsonContext.Default.OccamVerifySuccessResponse);
    }

    // SI-02b: build a compact citation package (leaf + Merkle path) for one block.
    private string Prove(ReceiptEnvelope envelope, string[]? leaves, int? blockIndex)
    {
        if (leaves is not { Length: > 0 })
        {
            return Fail("invalid_arguments", "prove mode needs a receipt with blockLeaves (from a json_blocks transcode).");
        }

        if (blockIndex is null || blockIndex < 0 || blockIndex >= leaves.Length)
        {
            return Fail("invalid_arguments", $"block_index must be in [0, {leaves.Length - 1}].");
        }

        if (envelope.BlockMerkleRoot is null || MerkleTree.RootFromLeafHashes(leaves) != envelope.BlockMerkleRoot)
        {
            return Fail("invalid_receipt", "blockLeaves do not reconstruct the signed blockMerkleRoot.");
        }

        var proofSteps = MerkleTree.Proof(leaves, blockIndex.Value);
        return JsonSerializer.Serialize(
            new OccamVerifyProveResponse(true, envelope.KeyId, envelope.BlockMerkleRoot, blockIndex.Value, leaves[blockIndex.Value], [.. proofSteps]),
            OccamVerifyJsonContext.Default.OccamVerifyProveResponse);
    }

    // SI-02b: verify a block + proof against the signed root — without the page or the other blocks.
    private string Citation(ReceiptEnvelope envelope, string publicKeyPem, string? blockText, string? blockSelector, string? proofJson)
    {
        if (blockText is null || proofJson is null)
        {
            return Fail("invalid_arguments", "citation mode needs block_text and proof.");
        }

        MerkleProofStep[]? proofSteps;
        try
        {
            proofSteps = JsonSerializer.Deserialize(proofJson, OccamVerifyJsonContext.Default.MerkleProofStepArray);
        }
        catch (JsonException)
        {
            proofSteps = null;
        }

        if (proofSteps is null || envelope.BlockMerkleRoot is null)
        {
            return Fail("invalid_arguments", "proof is not valid, or the receipt has no block root.");
        }

        var offline = ReceiptVerifier.VerifyOffline(envelope, publicKeyPem);
        var leaf = Convert.ToHexString(MerkleTree.LeafHash(blockText, blockSelector)).ToLowerInvariant();
        var membershipOk = MerkleTree.VerifyProof(leaf, proofSteps, envelope.BlockMerkleRoot);

        var verdict = !offline.SignatureValid
            ? ReceiptVerification.SignatureInvalid
            : membershipOk ? "citation_verified" : "citation_invalid";

        return JsonSerializer.Serialize(
            new OccamVerifySuccessResponse(true, offline.SignatureValid, null, envelope.KeyId, "citation", null, verdict),
            OccamVerifyJsonContext.Default.OccamVerifySuccessResponse);
    }

    /// <summary>Accept a full receipt object ({signed, blockLeaves, timeAnchor}) or a bare envelope.</summary>
    private static bool TryParseReceipt(string json, out ReceiptEnvelope? envelope, out string[]? leaves, out ReceiptTimeAnchor? anchor)
    {
        envelope = null;
        leaves = null;
        anchor = null;
        try
        {
            var wrapper = JsonSerializer.Deserialize(json, OccamVerifyJsonContext.Default.OccamVerifyReceiptInput);
            if (wrapper?.Signed is not null)
            {
                envelope = wrapper.Signed;
                leaves = wrapper.BlockLeaves;
                anchor = wrapper.TimeAnchor;
                return true;
            }

            envelope = JsonSerializer.Deserialize(json, OccamVerifyJsonContext.Default.ReceiptEnvelope);
            return envelope is not null;
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static string Fail(string code, string message) =>
        JsonSerializer.Serialize(
            new OccamVerifyFailureResponse(false, code, message),
            OccamVerifyJsonContext.Default.OccamVerifyFailureResponse);
}
