using System.Text;
using System.Text.Json;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Playbooks;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Watch;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_RECEIPT — Receipt v1 spine (SPEC-receipt-v1.md Phase 1): sign→verify roundtrip, tamper
/// detection (content + signature), canonical-form golden (guards serializer drift), Merkle root,
/// negative receipts, and wrong-key rejection. Deterministic, offline.
/// </summary>
public static class ReceiptUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var signer = ReceiptSigner.CreateEphemeral();
        var pub = signer.ExportPublicKeyPem();

        // --- positive roundtrip ---
        var markdown = "# Title\n\nBody paragraph.";
        var positive = new ReceiptEnvelope(
            ReceiptEnvelope.CurrentVersion, ReceiptEnvelope.KindExtraction,
            "https://a.com/", "https://a.com/", "http",
            "2026-01-01T00:00:00Z", "ff-occam/test",
            new ReceiptPlaybook("a.com", "1.0.0"),
            ContentHash: ReceiptCanonicalizer.ContentHash(markdown),
            BlockMerkleRoot: null, Tokens: 100,
            FailureCode: null, StatusCode: null, Confidence: 0.85,
            KeyId: "", Alg: "", Sig: null);
        var signed = signer.Sign(positive);

        assert("receipt sign attaches sig/keyId/alg",
            signed.Sig is not null && signed.KeyId.StartsWith("k1:", StringComparison.Ordinal)
            && signed.Alg == ReceiptEnvelope.AlgEcdsaP256);

        var ok = ReceiptVerifier.VerifyOffline(signed, pub, markdown);
        assert("receipt roundtrip verified", ok.Verdict == ReceiptVerification.Verified);
        assert("receipt roundtrip signature valid", ok.SignatureValid);
        assert("receipt roundtrip content hash match", ok.ContentHashMatch == true);

        // --- tamper: markdown changed ---
        var tamperedContent = ReceiptVerifier.VerifyOffline(signed, pub, markdown + " EXTRA");
        assert("receipt tampered markdown -> content_mismatch",
            tamperedContent.Verdict == ReceiptVerification.ContentMismatch && tamperedContent.SignatureValid);

        // --- tamper: signature flipped ---
        var sigBytes = Base64Url.Decode(signed.Sig!);
        sigBytes[0] ^= 0xFF;
        var tamperedSig = signed with { Sig = Base64Url.Encode(sigBytes) };
        var badSig = ReceiptVerifier.VerifyOffline(tamperedSig, pub, markdown);
        assert("receipt flipped signature -> signature_invalid",
            badSig.Verdict == ReceiptVerification.SignatureInvalid && !badSig.SignatureValid);

        // --- tamper: a signed field changed (url) invalidates the signature ---
        var tamperedField = signed with { Url = "https://evil.com/" };
        var badField = ReceiptVerifier.VerifyOffline(tamperedField, pub, markdown);
        assert("receipt tampered field -> signature_invalid",
            badField.Verdict == ReceiptVerification.SignatureInvalid);

        // --- wrong key ---
        var otherPub = ReceiptSigner.CreateEphemeral().ExportPublicKeyPem();
        var wrongKey = ReceiptVerifier.VerifyOffline(signed, otherPub, markdown);
        assert("receipt wrong key -> signature_invalid",
            wrongKey.Verdict == ReceiptVerification.SignatureInvalid);

        // --- canonical golden (freezes the signable byte form) ---
        var golden = new ReceiptEnvelope(
            1, ReceiptEnvelope.KindExtraction, "https://a.com/", "https://a.com/", "http",
            "2026-01-01T00:00:00Z", "ff-occam/test", new ReceiptPlaybook("a.com", "1.0.0"),
            ContentHash: "sha256:abc", BlockMerkleRoot: null, Tokens: 100,
            FailureCode: null, StatusCode: null, Confidence: 0.85,
            KeyId: "k1:test", Alg: ReceiptEnvelope.AlgEcdsaP256, Sig: "IGNORED");
        var canonical = Encoding.UTF8.GetString(ReceiptCanonicalizer.CanonicalBytes(golden));
        const string expected =
            "{\"v\":1,\"kind\":\"extraction\",\"url\":\"https://a.com/\",\"finalUrl\":\"https://a.com/\","
            + "\"backend\":\"http\",\"ts\":\"2026-01-01T00:00:00Z\",\"toolchain\":\"ff-occam/test\","
            + "\"playbook\":{\"id\":\"a.com\",\"version\":\"1.0.0\"},\"contentHash\":\"sha256:abc\","
            + "\"tokens\":100,\"confidence\":0.85,\"keyId\":\"k1:test\",\"alg\":\"ecdsa-p256-sha256\"}";
        assert("receipt canonical golden (sig excluded, fixed order)", canonical == expected);

        // --- P0-2c: leafSetComplete — signed completeness flag enabling provable absence ---
        var completeReceipt = signer.Sign(positive with { BlockMerkleRoot = "sha256:deadbeef", LeafSetComplete = true });
        assert("leafSetComplete receipt verifies (signed field)",
            ReceiptVerifier.VerifyOffline(completeReceipt, pub, markdown).Verdict == ReceiptVerification.Verified
            && completeReceipt.LeafSetComplete == true);
        var completeCanonical = Encoding.UTF8.GetString(
            ReceiptCanonicalizer.CanonicalBytes(golden with { BlockMerkleRoot = "sha256:r", LeafSetComplete = true }));
        assert("leafSetComplete canonical position (after blockMerkleRoot, before tokens)",
            completeCanonical.Contains("\"blockMerkleRoot\":\"sha256:r\",\"leafSetComplete\":true,\"tokens\"", StringComparison.Ordinal));
        assert("null leafSetComplete omitted (back-compat, bytes unchanged)",
            !canonical.Contains("leafSetComplete", StringComparison.Ordinal));

        // --- Merkle root ---
        var root1 = MerkleTree.Root(new[] { ("alpha", (string?)"#a"), ("beta", "#b") });
        var root2 = MerkleTree.Root(new[] { ("alpha", (string?)"#a"), ("GAMMA", "#c") });
        assert("merkle root deterministic + prefixed",
            root1 is not null && root1.StartsWith("sha256:", StringComparison.Ordinal));
        assert("merkle root changes when a block changes", root1 != root2);
        assert("merkle empty -> null", MerkleTree.Root(Array.Empty<(string, string?)>()) is null);
        assert("merkle single leaf is root",
            MerkleTree.Root(new[] { ("solo", (string?)"#s") }) is not null);

        // --- negative receipt (SI-03) ---
        var negative = signer.Sign(new ReceiptEnvelope(
            ReceiptEnvelope.CurrentVersion, ReceiptEnvelope.KindNegative,
            "https://locked.com/", "https://locked.com/", "http",
            "2026-01-01T00:00:00Z", "ff-occam/test", Playbook: null,
            ContentHash: null, BlockMerkleRoot: null, Tokens: null,
            FailureCode: "captcha_or_challenge", StatusCode: 403, Confidence: null,
            KeyId: "", Alg: "", Sig: null));
        var negVerify = ReceiptVerifier.VerifyOffline(negative, pub);
        assert("negative receipt verifies (signed ok:false)",
            negVerify.Verdict == ReceiptVerification.Verified && negVerify.ContentHashMatch is null);

        // --- Phase 2: BuildReceipt wires a signed envelope into the transcode receipt ---
        var outcome = new TranscodeOutcome(
            Ok: true, Markdown: markdown, FinalUrl: "https://a.com/x", Backend: "http",
            FailureCode: null, Message: null,
            Blocks: new[] { new WorkerExtractBlockInfo { Text = "alpha", SourceSelector = "#a" } },
            Confidence: 0.9, PlaybookId: "a.com", PlaybookVersion: "1.2.0");
        var wired = OccamTranscodeResponseBuilder.BuildReceipt(outcome, "https://a.com/x", signer);
        assert("buildReceipt attaches signed envelope", wired?.Signed is not null);
        assert("buildReceipt telemetry preserved", wired!.TokensUsed == outcome.TokensEstimated);
        var env = wired.Signed!;
        assert("buildReceipt provenance playbook",
            env.Playbook?.Id == "a.com" && env.Playbook.Version == "1.2.0");
        assert("buildReceipt content hash set",
            env.ContentHash == ReceiptCanonicalizer.ContentHash(markdown));
        assert("buildReceipt merkle root set", env.BlockMerkleRoot is not null);
        assert("buildReceipt sidecar block leaves (SI-02)", wired.BlockLeaves is { Length: 1 });
        assert("buildReceipt leaves reconstruct signed root",
            MerkleTree.RootFromLeafHashes(wired.BlockLeaves!) == env.BlockMerkleRoot);
        assert("buildReceipt url provenance", env.Url == "https://a.com/x" && env.FinalUrl == "https://a.com/x");
        var wiredVerify = ReceiptVerifier.VerifyOffline(env, pub, markdown);
        assert("buildReceipt envelope verifies", wiredVerify.Verdict == ReceiptVerification.Verified);
        var noSign = OccamTranscodeResponseBuilder.BuildReceipt(outcome, "https://a.com/x", null);
        assert("buildReceipt without signer -> telemetry only", noSign?.Signed is null);

        // --- Phase 3: envelope survives the JSON round-trip occam_verify relies on ---
        var envJson = JsonSerializer.Serialize(signed, ReceiptJsonContext.Default.ReceiptEnvelope);
        var reparsed = JsonSerializer.Deserialize(envJson, ReceiptJsonContext.Default.ReceiptEnvelope);
        assert("receipt json roundtrip preserves fields",
            reparsed is not null && reparsed.Sig == signed.Sig
            && reparsed.ContentHash == signed.ContentHash && reparsed.KeyId == signed.KeyId);
        assert("receipt survives json roundtrip and still verifies",
            ReceiptVerifier.VerifyOffline(reparsed!, pub, markdown).Verdict == ReceiptVerification.Verified);

        // --- occam_verify tool, offline path (pipeline unused offline) ---
        var verifyTool = new OccamVerifyTool(null!, signer);
        assert("occam_verify offline -> verified",
            verifyTool.Verify(envJson, markdown, pub, "offline").Sync().Contains("\"verdict\":\"verified\"", StringComparison.Ordinal));
        assert("occam_verify offline detects content mismatch",
            verifyTool.Verify(envJson, markdown + "X", pub, "offline").Sync().Contains("\"verdict\":\"content_mismatch\"", StringComparison.Ordinal));
        assert("occam_verify rejects junk receipt",
            verifyTool.Verify("{not a receipt}", null, pub, "offline").Sync().Contains("\"failureCode\":\"invalid_receipt\"", StringComparison.Ordinal));
        assert("occam_verify wrong key -> signature_invalid",
            verifyTool.Verify(envJson, markdown, otherPub, "offline").Sync().Contains("\"verdict\":\"signature_invalid\"", StringComparison.Ordinal));

        // --- Phase 4: negative receipts (SI-03) signed only for provable unavailability ---
        var negCaptcha = OccamTranscodeResponseBuilder.BuildNegativeReceipt(
            "https://locked.com/", "https://locked.com/", "http", "captcha_or_challenge", 403, signer);
        assert("negative receipt on captcha",
            negCaptcha is not null && negCaptcha.Kind == ReceiptEnvelope.KindNegative
            && negCaptcha.FailureCode == "captcha_or_challenge" && negCaptcha.StatusCode == 403);
        assert("negative receipt verifies",
            ReceiptVerifier.VerifyOffline(negCaptcha!, pub).Verdict == ReceiptVerification.Verified);
        assert("negative receipt on 4xx status",
            OccamTranscodeResponseBuilder.BuildNegativeReceipt("https://x.com/", null, "http", "extraction_failed", 404, signer) is not null);
        assert("no negative receipt on transient timeout",
            OccamTranscodeResponseBuilder.BuildNegativeReceipt("https://x.com/", null, "http", "timeout", 0, signer) is null);
        assert("no negative receipt without signer",
            OccamTranscodeResponseBuilder.BuildNegativeReceipt("https://x.com/", null, "http", "captcha_or_challenge", 403, null) is null);

        // --- SI-02: block leaves reconstruct the root + granular membership ---
        var mblocks = new[] { ("alpha", (string?)"#a"), ("beta", "#b"), ("gamma", "#c") };
        var mleaves = MerkleTree.LeafHashesHex(mblocks);
        var mroot = MerkleTree.Root(mblocks)!;
        assert("merkle leaf hashes count", mleaves.Length == 3);
        assert("merkle root from leaves matches tree root", MerkleTree.RootFromLeafHashes(mleaves) == mroot);
        var changedLeaves = new HashSet<string>(
            MerkleTree.LeafHashesHex(new[] { ("alpha", (string?)"#a"), ("beta-CHANGED", "#b"), ("delta", "#d") }),
            StringComparer.Ordinal);
        assert("granular membership counts surviving blocks", mleaves.Count(changedLeaves.Contains) == 1);

        // --- SI-02b: Merkle proof — prove a block without the page ---
        for (var i = 0; i < mleaves.Length; i++)
        {
            assert($"merkle proof verifies for leaf {i}", MerkleTree.VerifyProof(mleaves[i], MerkleTree.Proof(mleaves, i), mroot));
        }
        var proof0 = MerkleTree.Proof(mleaves, 0);
        assert("merkle proof rejects wrong leaf", !MerkleTree.VerifyProof(mleaves[1], proof0, mroot));
        assert("merkle proof rejects tampered root", !MerkleTree.VerifyProof(mleaves[0], proof0, "sha256:deadbeef"));

        // --- SI-02b: occam_verify prove + citation through the tool (no page) ---
        var citeEnv = signer.Sign(new ReceiptEnvelope(
            ReceiptEnvelope.CurrentVersion, ReceiptEnvelope.KindExtraction,
            "https://a.com/", "https://a.com/", "http", "2026-01-01T00:00:00Z", "ff-occam/test",
            null, ContentHash: "sha256:abc", BlockMerkleRoot: mroot, Tokens: null,
            FailureCode: null, StatusCode: null, Confidence: null, KeyId: "", Alg: "", Sig: null));
        var bareEnvJson = JsonSerializer.Serialize(citeEnv, ReceiptJsonContext.Default.ReceiptEnvelope);
        var receiptObjJson = $"{{\"signed\":{bareEnvJson},\"blockLeaves\":[{string.Join(",", mleaves.Select(l => $"\"{l}\""))}]}}";
        var proofJson = "[" + string.Join(",", proof0.Select(s => $"{{\"hash\":\"{s.Hash}\",\"siblingIsRight\":{(s.SiblingIsRight ? "true" : "false")}}}")) + "]";

        var proveOut = verifyTool.Verify(receiptObjJson, mode: "prove", block_index: 0).Sync();
        assert("occam_verify prove emits leaf", proveOut.Contains($"\"leaf\":\"{mleaves[0]}\"", StringComparison.Ordinal));
        assert("occam_verify citation -> citation_verified",
            verifyTool.Verify(bareEnvJson, mode: "citation", block_text: "alpha", block_selector: "#a", proof: proofJson).Sync()
                .Contains("\"verdict\":\"citation_verified\"", StringComparison.Ordinal));
        assert("occam_verify citation rejects wrong block",
            verifyTool.Verify(bareEnvJson, mode: "citation", block_text: "WRONG", block_selector: "#a", proof: proofJson).Sync()
                .Contains("\"verdict\":\"citation_invalid\"", StringComparison.Ordinal));

        // --- SI-08: signed playbooks (local foundation for the registry) ---
        const string pbJson = "{\"schema_version\":\"1.0\",\"id\":\"a.com\",\"routing\":{\"preferred_backend\":\"http\"},\"extract\":{\"contentSelectors\":[\"article\"]}}";
        var signedPb = PlaybookSignature.BuildSignedJson(pbJson, 85, true, 0.05, signer);
        assert("signed playbook verifies", PlaybookSignature.Verify(signedPb, pub));
        assert("signed playbook carries keyId + signature",
            signedPb.Contains(signer.KeyId, StringComparison.Ordinal) && signedPb.Contains("\"signature\"", StringComparison.Ordinal));
        assert("content hash ignores provenance block",
            PlaybookSignature.ContentHash(pbJson) == PlaybookSignature.ContentHash(signedPb));
        assert("tampered playbook body fails verify",
            !PlaybookSignature.Verify(signedPb.Replace("article", "TAMPERED", StringComparison.Ordinal), pub));
        assert("signed playbook wrong key fails", !PlaybookSignature.Verify(signedPb, otherPub));

        // --- SI-08 consumer loop: resolve-side Inspect classifies trust vs the local key ---
        var inspectVerified = PlaybookSignature.Inspect(signedPb, signer.KeyId, pub);
        assert("inspect verified for our signed recipe",
            inspectVerified is { Present: true, Status: "verified", Score: 85, PassesGate: true }
            && inspectVerified.KeyId == signer.KeyId);
        assert("inspect unsigned for bare recipe",
            PlaybookSignature.Inspect(pbJson, signer.KeyId, pub) is { Present: false, Status: "unsigned" });
        assert("inspect invalid for tampered body",
            PlaybookSignature.Inspect(signedPb.Replace("article", "TAMPERED", StringComparison.Ordinal), signer.KeyId, pub)
                is { Status: "invalid" });
        var otherSigner = ReceiptSigner.CreateEphemeral();
        var foreignPb = PlaybookSignature.BuildSignedJson(pbJson, 70, true, 0.1, otherSigner);
        assert("inspect unknown_key for foreign author",
            PlaybookSignature.Inspect(foreignPb, signer.KeyId, pub)
                is { Present: true, Status: "unknown_key" } fk && fk.KeyId == otherSigner.KeyId);

        // --- SI-05: signed watch-history chain ---
        var h0 = WatchHistoryChain.Append([], WatchHistoryEntry.EventFirstSeen, "sha256:aa", null, null, "2026-07-03T00:00:00Z", signer);
        assert("history genesis is seq0 + null prev + signed",
            h0.Seq == 0 && h0.PrevEntryHash is null && h0.Sig is not null && h0.Event == "first_seen");
        var h1 = WatchHistoryChain.Append([h0], WatchHistoryEntry.EventChanged, "sha256:bb", "sha256:root", 12, "2026-07-03T01:00:00Z", signer);
        assert("history second entry links to first",
            h1.Seq == 1 && h1.PrevEntryHash == WatchHistoryChain.EntryHash(h0));
        WatchHistoryEntry[] chain = [h0, h1];
        assert("history chain verifies", WatchHistoryChain.Verify(chain, pub));
        assert("history chain wrong key fails", !WatchHistoryChain.Verify(chain, otherPub));
        assert("history tampered body fails", !WatchHistoryChain.Verify([h0, h1 with { ContentHash = "sha256:zz" }], pub));
        assert("history tampered sig fails", !WatchHistoryChain.Verify([h0, h1 with { Sig = h0.Sig }], pub));
        assert("history reordered fails", !WatchHistoryChain.Verify([h1, h0], pub));
        assert("history broken link fails", !WatchHistoryChain.Verify([h0, h1 with { PrevEntryHash = "sha256:00" }], pub));
        // Windowed chain (genesis pruned by the cap): first retained entry has seq>0 + a non-null prev → still verifies.
        assert("windowed chain (pruned prefix) verifies", WatchHistoryChain.Verify([h1], pub));
        // Unsigned chain (OCCAM_RECEIPTS=off): no signatures but the hash chain still links.
        var u0 = WatchHistoryChain.Append([], WatchHistoryEntry.EventFirstSeen, "sha256:aa", null, null, "t0", null);
        var u1 = WatchHistoryChain.Append([u0], WatchHistoryEntry.EventChanged, "sha256:bb", null, 3, "t1", null);
        assert("unsigned history chains + verifies", u0.Sig is null && WatchHistoryChain.Verify([u0, u1], pub));
        assert("unsigned history broken link fails", !WatchHistoryChain.Verify([u0, u1 with { PrevEntryHash = "sha256:00" }], pub));

        // occam_verify history mode (SI-05 consumer): verify the chain through the tool.
        var chainJson = JsonSerializer.Serialize(chain, OccamWatchJsonContext.Default.WatchHistoryEntryArray);
        assert("occam_verify history -> history_verified",
            verifyTool.Verify(chainJson, mode: "history").Sync().Contains("\"verdict\":\"history_verified\"", StringComparison.Ordinal));
        assert("occam_verify history wrong key -> history_invalid",
            verifyTool.Verify(chainJson, public_key: otherPub, mode: "history").Sync().Contains("\"verdict\":\"history_invalid\"", StringComparison.Ordinal));
        var wrappedJson = "{\"history\":" + chainJson + "}";
        assert("occam_verify history accepts {history:[...]} wrapper",
            verifyTool.Verify(wrappedJson, mode: "history").Sync().Contains("\"verdict\":\"history_verified\"", StringComparison.Ordinal));

        // --- SI-15: time-anchored receipts (RFC3161). Deterministic vector: a real freeTSA token over
        // SHA256("occam-si15-test-vector"), captured once (only the hash left the machine). ---
        const string tsaToken = "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";
        var taPreimage = Encoding.UTF8.GetBytes("occam-si15-test-vector");
        var taImprint = System.Security.Cryptography.SHA256.HashData(taPreimage);
        var (taValid, taGen, taSubj) = TimeAnchorVerifier.VerifyToken(tsaToken, taImprint);
        assert("time anchor token verifies for its imprint", taValid && taGen is not null && taSubj is not null);
        assert("time anchor genTime is 2026", taGen!.StartsWith("2026", StringComparison.Ordinal));
        assert("time anchor rejects a wrong imprint",
            !TimeAnchorVerifier.VerifyToken(tsaToken, System.Security.Cryptography.SHA256.HashData("WRONG"u8.ToArray())).Valid);

        // High-level Verify: a "signature" whose bytes hash to the vector imprint proves the binding path.
        var taSig = Base64Url.Encode(taPreimage);
        var timeAnchor = new ReceiptTimeAnchor(ReceiptTimeAnchor.TypeRfc3161, "https://freetsa.org/tsr", tsaToken, taGen!);
        assert("time anchor high-level verify accepts a matching signature",
            TimeAnchorVerifier.Verify(timeAnchor, taSig) is { Present: true, Valid: true });
        assert("time anchor absent -> not present", !TimeAnchorVerifier.Verify(null, taSig).Present);
        assert("time anchor mismatched signature -> invalid",
            !TimeAnchorVerifier.Verify(timeAnchor, Base64Url.Encode("different"u8.ToArray())).Valid);

        // occam_verify surfaces the anchor (offline): feed {signed, timeAnchor} with sig == taSig.
        var anchoredReceiptJson = "{\"signed\":"
            + JsonSerializer.Serialize(signed with { Sig = taSig }, ReceiptJsonContext.Default.ReceiptEnvelope)
            + ",\"timeAnchor\":" + JsonSerializer.Serialize(timeAnchor, OccamVerifyJsonContext.Default.ReceiptTimeAnchor) + "}";
        var anchoredOut = verifyTool.Verify(anchoredReceiptJson, mode: "offline", public_key: pub).Sync();
        assert("occam_verify surfaces a valid time anchor",
            anchoredOut.Contains("\"timeAnchor\"", StringComparison.Ordinal)
            && anchoredOut.Contains("\"valid\":true", StringComparison.Ordinal));

        // --- SI-12: chunk-level RAG expiry (pure staleness verdict) ---
        var liveLeaves = new HashSet<string>(["a", "b", "c"], StringComparer.Ordinal);
        var st = ChunkStalenessEvaluator.Compute(["a", "b", "x"], liveLeaves);
        assert("chunk staleness pinpoints the stale chunk",
            st is { Total: 3, Present: 2, Stale: 1 } && st.StaleChunks is ["x"]);
        assert("chunk staleness all-present -> none stale",
            ChunkStalenessEvaluator.Compute(["a", "b"], liveLeaves) is { Stale: 0, StaleChunks.Length: 0 });
        assert("chunk staleness empty source -> zero",
            ChunkStalenessEvaluator.Compute([], liveLeaves) is { Total: 0, Stale: 0 });

        // --- SI-02 integrity: blocks reconcile to the compiled (pruned) markdown ---
        // A block whose text was pruned out must NOT appear in the receipt's block root/leaves, so a
        // citation can never prove a block the reader can't see, and contentHash agrees with the root.
        var allBlocks = new[]
        {
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Alpha block kept in output.", SourceSelector = "#a" },
            new WorkerExtractBlockInfo { Type = "paragraph", Text = "Beta block pruned away by fit_markdown.", SourceSelector = "#b" },
            new WorkerExtractBlockInfo { Type = "heading", Text = "Gamma heading kept.", SourceSelector = "#c" },
        };
        const string prunedMd = "# Doc\n\nAlpha block kept in output.\n\n## Gamma heading kept.";
        var survivors = BlockReconciler.SurvivingBlocks(allBlocks, prunedMd, prunedMd + "\n\nBeta block pruned away by fit_markdown.");
        assert("reconcile drops the pruned block",
            survivors is { Count: 2 }
                && survivors.Any(b => b.SourceSelector == "#a")
                && survivors.Any(b => b.SourceSelector == "#c")
                && !survivors.Any(b => b.SourceSelector == "#b"));
        assert("reconcile is a no-op when markdown is unchanged",
            ReferenceEquals(BlockReconciler.SurvivingBlocks(allBlocks, prunedMd, prunedMd), allBlocks));
        // Post-reconcile, the block root reconstructs from exactly the surviving leaves.
        (string Text, string? SourceSelector)[] survPairs = [.. survivors!.Select(b => (b.Text, (string?)b.SourceSelector))];
        var survLeaves = MerkleTree.LeafHashesHex(survPairs);
        assert("reconciled block root matches its leaves",
            MerkleTree.Root(survPairs) == MerkleTree.RootFromLeafHashes(survLeaves));
        assert("every surviving leaf's block text is present in the returned markdown",
            survivors!.All(b => BlockReconciler.NormalizeWhitespace(prunedMd)
                .Contains(BlockReconciler.NormalizeWhitespace(b.Text), StringComparison.Ordinal)));

        // --- audit C: the if_none_match token accepts BOTH the bare hex and the receipt contentHash ---
        const string etagMd = "# Page\n\nStable body.";
        var bareToken = ContentHashToken.BareHex(etagMd);
        assert("content-hash token matches its bare hex", ContentHashToken.Matches(etagMd, bareToken));
        assert("content-hash token accepts the sha256:-prefixed receipt contentHash",
            ContentHashToken.Matches(etagMd, "sha256:" + bareToken)
                && ContentHashToken.Matches(etagMd, ReceiptCanonicalizer.ContentHash(etagMd)));
        assert("content-hash token rejects a different hash",
            !ContentHashToken.Matches(etagMd, ContentHashToken.BareHex(etagMd + " changed")));

        Console.WriteLine("L_RECEIPT_OK");
    }
}
