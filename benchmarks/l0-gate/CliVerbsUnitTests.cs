using System.Text.Json;
using OccamMcp.Core.Cli;
using OccamMcp.Core.Receipts;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_CLI_VERIFY — the public offline verifier verbs (`keys export`, `verify`). End-to-end through
/// OccamCliVerbs.TryRun with a real ephemeral key and temp files: a genuine receipt verifies (exit 0),
/// a wrong key / tampered markdown fails (exit 1), keys export succeeds, and bad usage is exit 2. This
/// is the consumer-side surface — a third party checks a receipt without running the MCP host.
/// </summary>
public static class CliVerbsUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var dir = Path.Combine(Path.GetTempPath(), "occam-cli-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var signer = ReceiptSigner.CreateEphemeral();
            const string markdown = "# Hello\n\nThis is the extracted page body, long enough to hash.";
            var envelope = signer.Sign(new ReceiptEnvelope(
                ReceiptEnvelope.CurrentVersion, ReceiptEnvelope.KindExtraction,
                "https://ex.example/p", "https://ex.example/p", "http",
                "2026-07-03T00:00:00Z", "ff-occam/test", null,
                ContentHash: ReceiptCanonicalizer.ContentHash(markdown),
                BlockMerkleRoot: null, Tokens: 12, FailureCode: null, StatusCode: null, Confidence: 0.9,
                KeyId: string.Empty, Alg: string.Empty, Sig: null));

            var receiptPath = Path.Combine(dir, "receipt.json");
            var pubPath = Path.Combine(dir, "key.pem");
            var mdPath = Path.Combine(dir, "page.md");
            var wrongPubPath = Path.Combine(dir, "wrong.pem");
            File.WriteAllText(receiptPath, JsonSerializer.Serialize(envelope, ReceiptJsonContext.Default.ReceiptEnvelope));
            File.WriteAllText(pubPath, signer.ExportPublicKeyPem());
            File.WriteAllText(mdPath, markdown);
            File.WriteAllText(wrongPubPath, ReceiptSigner.CreateEphemeral().ExportPublicKeyPem());

            // A genuine receipt verifies (exit 0) and the emitted verdict says so.
            var (verifiedExit, verifiedOut) = Capture(["verify", "--receipt", receiptPath, "--pubkey", pubPath]);
            assert("cli verify genuine receipt exits 0", verifiedExit == 0);
            assert("cli verify verdict is verified", verifiedOut.Contains("\"verdict\":\"verified\""));

            // With the matching markdown, contentHash also checks out.
            var (mdExit, mdOut) = Capture(["verify", "--receipt", receiptPath, "--pubkey", pubPath, "--markdown", mdPath]);
            assert("cli verify with markdown exits 0", mdExit == 0 && mdOut.Contains("\"contentHashMatch\":true"));

            // Wrong key → not verified (exit 1).
            var (wrongExit, wrongOut) = Capture(["verify", "--receipt", receiptPath, "--pubkey", wrongPubPath]);
            assert("cli verify wrong key exits 1", wrongExit == 1 && wrongOut.Contains("signature_invalid"));

            // Tampered markdown → content hash mismatch (exit 1).
            var badMdPath = Path.Combine(dir, "bad.md");
            File.WriteAllText(badMdPath, markdown + " TAMPERED");
            var (badMdExit, badMdOut) = Capture(["verify", "--receipt", receiptPath, "--pubkey", pubPath, "--markdown", badMdPath]);
            assert("cli verify tampered markdown exits 1", badMdExit == 1 && badMdOut.Contains("content_hash_mismatch"));

            // keys export prints a PEM (exit 0).
            var (keysExit, keysOut) = Capture(["keys", "export", "--keys-root", dir]);
            assert("cli keys export exits 0", keysExit == 0 && keysOut.Contains("BEGIN PUBLIC KEY"));

            // Missing --pubkey → usage error (exit 2).
            var (usageExit, _) = Capture(["verify", "--receipt", receiptPath]);
            assert("cli verify without pubkey exits 2", usageExit == 2);

            // A non-verb falls through (TryRun returns false).
            assert("cli non-verb falls through", !OccamCliVerbs.TryRun(["--mcp-server"], out _));

            // version-surface prints hostVersion + assemblyPath (exit 0).
            var (vsExit, vsOut) = Capture(["version-surface"]);
            assert("cli version-surface exits 0", vsExit == 0);
            assert(
                "cli version-surface has hostVersion",
                vsOut.Contains("\"hostVersion\"", StringComparison.Ordinal)
                && vsOut.Contains("\"assemblyPath\"", StringComparison.Ordinal));

            Console.WriteLine("L_CLI_VERIFY_OK");
        }
        finally
        {
            try { Directory.Delete(dir, recursive: true); } catch (IOException) { /* best-effort */ }
        }
    }

    /// <summary>Run a verb, capturing stdout + exit code (redirects Console.Out so gate markers stay clean).</summary>
    private static (int Exit, string Out) Capture(string[] args)
    {
        var original = Console.Out;
        var sw = new StringWriter();
        Console.SetOut(sw);
        try
        {
            OccamCliVerbs.TryRun(args, out var exit);
            return (exit, sw.ToString());
        }
        finally
        {
            Console.SetOut(original);
        }
    }
}
