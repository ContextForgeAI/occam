using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Dataset;

/// <summary>
/// SI-17: the identity of one extraction in an exported dataset — enough to bind it into the manifest
/// Merkle tree without carrying the (large) content. The row leaf hashes exactly these fields, so a
/// consumer re-derives it from the row and confirms membership under the signed manifest root.
/// </summary>
public sealed record DatasetRow(
    string Url,
    string FinalUrl,
    bool Ok,
    string? ContentHash,
    string? BlockMerkleRoot,
    string? FailureCode);

/// <summary>
/// SI-17: a signed, auditable dataset manifest. Each dataset row carries its own signed extraction
/// receipt (independently verifiable via <c>occam_verify</c>); this manifest binds the whole SET with a
/// single signature over the Merkle root of the per-row leaves — so "these N extractions, exactly, were
/// produced together" is provable and tamper-evident (add/drop/edit any row → the root changes). Pure
/// and network-free; reuses <see cref="MerkleTree"/> and the Receipt v1 detached-signature machinery.
/// </summary>
public static class DatasetManifestBuilder
{
    public const int Version = 1;
    public const string Alg = "ecdsa-p256-sha256";

    /// <summary>Deterministic hex leaf for a row — SHA-256 over a canonical, field-ordered preimage.</summary>
    public static string RowLeafHex(DatasetRow row)
    {
        // Newline-joined canonical form; every field present (empty when null) so the layout is fixed.
        var preimage = string.Join('\n',
            row.Url,
            row.FinalUrl,
            row.Ok ? "1" : "0",
            row.ContentHash ?? string.Empty,
            row.BlockMerkleRoot ?? string.Empty,
            row.FailureCode ?? string.Empty);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(preimage))).ToLowerInvariant();
    }

    /// <summary>Ordered hex leaves for the dataset rows (row order is significant and part of the proof).</summary>
    public static string[] LeafHashes(IReadOnlyList<DatasetRow> rows)
    {
        var leaves = new string[rows.Count];
        for (var i = 0; i < rows.Count; i++)
        {
            leaves[i] = RowLeafHex(rows[i]);
        }

        return leaves;
    }

    /// <summary>Manifest Merkle root over the row leaves (null for an empty dataset).</summary>
    public static string? ManifestRoot(IReadOnlyList<DatasetRow> rows) =>
        MerkleTree.RootFromLeafHashes(LeafHashes(rows));

    /// <summary>
    /// Deterministic bytes the manifest signature covers (excludes <c>sig</c>). Hand-written fixed order,
    /// AOT-safe, mirroring <see cref="ReceiptCanonicalizer"/>.
    /// </summary>
    public static byte[] CanonicalBytes(int version, string createdAt, int rowCount, string? manifestRoot, string keyId, string alg)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("v", version);
            w.WriteString("createdAt", createdAt);
            w.WriteNumber("rowCount", rowCount);
            if (manifestRoot is not null) w.WriteString("manifestRoot", manifestRoot);
            w.WriteString("keyId", keyId);
            w.WriteString("alg", alg);
            // sig intentionally excluded
            w.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }

    public static string NowUtc() =>
        DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

    /// <summary>
    /// Verify a signed manifest end-to-end: the manifest root reconstructs from the supplied rows AND
    /// the detached signature over the canonical bytes checks out under <paramref name="publicKeyPem"/>.
    /// The consumer-side proof that "this exact set of rows was signed together".
    /// </summary>
    public static bool Verify(
        IReadOnlyList<DatasetRow> rows,
        int version,
        string createdAt,
        string? manifestRoot,
        string keyId,
        string alg,
        string signatureBase64Url,
        string publicKeyPem)
    {
        var recomputedRoot = ManifestRoot(rows);
        if (!string.Equals(recomputedRoot, manifestRoot, StringComparison.Ordinal))
        {
            return false;
        }

        var bytes = CanonicalBytes(version, createdAt, rows.Count, manifestRoot, keyId, alg);
        return ReceiptVerifier.VerifyDetached(bytes, signatureBase64Url, publicKeyPem);
    }
}
