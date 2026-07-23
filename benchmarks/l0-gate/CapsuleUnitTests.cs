using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Tools;
using OccamMcp.Core.Workers;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_CAPSULE — proof-carrying capsule (HARNESS-P0-SPEC, verified hand-off). A capsule is the
/// agent-to-agent form of a receipt: encode → pass → the receiving agent verifies OFFLINE via
/// occam_verify, with no page and no re-fetch. Deterministic, offline.
/// </summary>
public static class CapsuleUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var signer = ReceiptSigner.CreateEphemeral();
        var pub = signer.ExportPublicKeyPem();
        var otherPub = ReceiptSigner.CreateEphemeral().ExportPublicKeyPem();

        const string markdown = "# Capsule\n\nProof-carrying body one.\n\nProof-carrying body two.";
        var blocks = new[] { ("Proof-carrying body one.", (string?)"#b1"), ("Proof-carrying body two.", (string?)"#b2") };
        var leaves = MerkleTree.LeafHashesHex(blocks);
        var root = MerkleTree.Root(blocks);

        var signed = signer.Sign(new ReceiptEnvelope(
            ReceiptEnvelope.CurrentVersion, ReceiptEnvelope.KindExtraction,
            "https://a.com/", "https://a.com/", "http",
            "2026-01-01T00:00:00Z", "ff-occam/test", null,
            ContentHash: ReceiptCanonicalizer.ContentHash(markdown),
            BlockMerkleRoot: root, Tokens: 42,
            FailureCode: null, StatusCode: null, Confidence: 0.9,
            KeyId: "", Alg: "", Sig: null));

        // --- encode / decode round-trip ---
        var capsule = CapsuleCodec.Encode(CapsuleCodec.FromReceipt(signed, markdown, leaves));
        assert("capsule uses occam scheme", capsule.StartsWith(CapsuleCodec.Scheme, StringComparison.Ordinal));
        assert("capsule parses back", CapsuleCodec.TryParse(capsule, out var parsed) && parsed!.Signed.Sig == signed.Sig);
        assert("capsule preserves block leaves", parsed!.BlockLeaves is { Length: 2 } && parsed.BlockLeaves[0] == leaves[0]);
        assert("capsule preserves content", parsed!.Content == markdown);
        assert("capsule leaves reconstruct signed root", MerkleTree.RootFromLeafHashes(parsed!.BlockLeaves!) == signed.BlockMerkleRoot);
        assert("capsule carries self-describing verify recipe",
            parsed!.VerifyRecipe is not null
            && parsed.VerifyRecipe!.Alg == ReceiptEnvelope.AlgEcdsaP256
            && parsed.VerifyRecipe.KeyAnchor == signed.KeyId);

        // --- verified hand-off: occam_verify offline, content taken FROM the capsule (no page) ---
        var verifyTool = new OccamVerifyTool(null!, signer);
        var verified = verifyTool.Verify(capsule, markdown: null, public_key: pub, mode: "offline").Sync();
        assert("capsule verifies offline via its own content",
            verified.Contains("\"verdict\":\"verified\"", StringComparison.Ordinal)
            && verified.Contains("\"contentHashMatch\":true", StringComparison.Ordinal));

        // --- tampered content inside the capsule -> content_mismatch (signature still valid) ---
        var tampered = CapsuleCodec.Encode(CapsuleCodec.FromReceipt(signed, markdown + " EVIL", leaves));
        assert("capsule tampered content -> content_mismatch",
            verifyTool.Verify(tampered, null, pub, "offline").Sync()
                .Contains("\"verdict\":\"content_mismatch\"", StringComparison.Ordinal));

        // --- wrong key -> signature_invalid ---
        assert("capsule wrong key -> signature_invalid",
            verifyTool.Verify(capsule, null, otherPub, "offline").Sync()
                .Contains("\"verdict\":\"signature_invalid\"", StringComparison.Ordinal));

        // --- malformed capsule -> rejected (never throws) ---
        assert("malformed capsule -> invalid_receipt",
            verifyTool.Verify("occam://capsule/!!!not-base64!!!", null, pub, "offline").Sync()
                .Contains("\"failureCode\":\"invalid_receipt\"", StringComparison.Ordinal));
        assert("codec rejects a non-capsule string", !CapsuleCodec.TryParse("{\"signed\":{}}", out _));
        assert("codec IsCapsule guards the scheme",
            CapsuleCodec.IsCapsule(capsule) && !CapsuleCodec.IsCapsule("{\"signed\":{}}"));

        // --- proof-of-actually-read: prove mode works THROUGH a capsule, no page ---
        var proveOut = verifyTool.Verify(capsule, mode: "prove", block_index: 0).Sync();
        assert("capsule prove emits leaf 0 (proof-of-read path)",
            proveOut.Contains($"\"leaf\":\"{leaves[0]}\"", StringComparison.Ordinal));

        // --- OKP producer → consumer end-to-end: transcode EMITS a capsule that a peer verifies
        // offline with no page. Proves the full hand-off path through the real response builder. ---
        var outcome = new TranscodeOutcome(
            Ok: true, Markdown: markdown, FinalUrl: "https://a.com/x", Backend: "http",
            FailureCode: null, Message: null,
            Blocks: new[] { new WorkerExtractBlockInfo { Text = "Proof-carrying body one.", SourceSelector = "#b1" } },
            Confidence: 0.9, PlaybookId: null, PlaybookVersion: null);
        var produced = OccamTranscodeResponseBuilder.BuildReceipt(outcome, "https://a.com/x", signer, emitCapsule: true);
        assert("transcode emits capsule when emit_capsule",
            produced?.Capsule is not null && produced.Capsule!.StartsWith(CapsuleCodec.Scheme, StringComparison.Ordinal));
        assert("transcode omits capsule by default",
            OccamTranscodeResponseBuilder.BuildReceipt(outcome, "https://a.com/x", signer)?.Capsule is null);
        var handoff = verifyTool.Verify(produced!.Capsule!, markdown: null, public_key: pub, mode: "offline").Sync();
        assert("produced capsule verifies offline end-to-end (no page)",
            handoff.Contains("\"verdict\":\"verified\"", StringComparison.Ordinal)
            && handoff.Contains("\"contentHashMatch\":true", StringComparison.Ordinal));

        Console.WriteLine("L_CAPSULE_OK");
    }
}
