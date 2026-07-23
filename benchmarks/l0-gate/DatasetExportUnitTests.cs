using OccamMcp.Core.Dataset;
using OccamMcp.Core.Receipts;

namespace OccamMcp.L0Gate;

/// <summary>
/// L_DATASET — SI-17 verifiable dataset export. Pure, deterministic manifest core: row-leaf determinism,
/// Merkle-root reconstruction from the rows, tamper detection (any row edit / reorder / drop changes the
/// root), and the detached manifest signature round-trip + wrong-key rejection. Live multi-URL export
/// stays a smoke concern.
/// </summary>
public static class DatasetExportUnitTests
{
    public static void Run(Action<string, bool> assert)
    {
        var rows = new List<DatasetRow>
        {
            new("https://a.example/1", "https://a.example/1", true, "sha256:aaa", "sha256:ra", null),
            new("https://a.example/2", "https://a.example/2", true, "sha256:bbb", "sha256:rb", null),
            new("https://a.example/3", "https://a.example/3", false, null, null, "requires_login"),
        };

        // Row leaf is deterministic and content-bound.
        var leaf0 = DatasetManifestBuilder.RowLeafHex(rows[0]);
        assert("dataset row leaf is deterministic", leaf0 == DatasetManifestBuilder.RowLeafHex(rows[0]));
        assert("dataset row leaf changes with content",
            leaf0 != DatasetManifestBuilder.RowLeafHex(rows[0] with { ContentHash = "sha256:zzz" }));

        var root = DatasetManifestBuilder.ManifestRoot(rows);
        assert("dataset manifest root present", !string.IsNullOrEmpty(root));

        // Sign the manifest with an ephemeral key and verify end-to-end.
        var signer = ReceiptSigner.CreateEphemeral();
        var createdAt = DatasetManifestBuilder.NowUtc();
        var bytes = DatasetManifestBuilder.CanonicalBytes(
            DatasetManifestBuilder.Version, createdAt, rows.Count, root, signer.KeyId, DatasetManifestBuilder.Alg);
        var sig = signer.SignDetached(bytes);
        var pub = signer.ExportPublicKeyPem();

        assert("dataset manifest verifies with the signing key",
            DatasetManifestBuilder.Verify(rows, DatasetManifestBuilder.Version, createdAt, root, signer.KeyId, DatasetManifestBuilder.Alg, sig, pub));

        // Tamper: edit a row → root no longer reconstructs → verify fails.
        var tampered = new List<DatasetRow>(rows) { [1] = rows[1] with { ContentHash = "sha256:forged" } };
        assert("dataset verify rejects an edited row",
            !DatasetManifestBuilder.Verify(tampered, DatasetManifestBuilder.Version, createdAt, root, signer.KeyId, DatasetManifestBuilder.Alg, sig, pub));

        // Reorder: swapping rows changes the ordered Merkle root.
        var reordered = new List<DatasetRow> { rows[1], rows[0], rows[2] };
        assert("dataset root is order-sensitive",
            DatasetManifestBuilder.ManifestRoot(reordered) != root);

        // Drop a row → different root → verify fails.
        var dropped = new List<DatasetRow> { rows[0], rows[1] };
        assert("dataset verify rejects a dropped row",
            !DatasetManifestBuilder.Verify(dropped, DatasetManifestBuilder.Version, createdAt, root, signer.KeyId, DatasetManifestBuilder.Alg, sig, pub));

        // Wrong key rejects a valid manifest.
        var wrongPub = ReceiptSigner.CreateEphemeral().ExportPublicKeyPem();
        assert("dataset verify rejects the wrong key",
            !DatasetManifestBuilder.Verify(rows, DatasetManifestBuilder.Version, createdAt, root, signer.KeyId, DatasetManifestBuilder.Alg, sig, wrongPub));

        Console.WriteLine("L_DATASET_OK");
    }
}
