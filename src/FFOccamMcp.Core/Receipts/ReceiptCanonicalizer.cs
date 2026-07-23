using System.Buffers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace OccamMcp.Core.Receipts;

/// <summary>
/// Normative canonical serialization of a receipt for signing/verifying. The ENTIRE scheme depends
/// on this being byte-stable, so it is written by hand with a fixed field order (not reflection,
/// not sorted-keys) — AOT-safe and immune to serializer/property-order drift. <see cref="ReceiptEnvelope.Sig"/>
/// is always excluded; null optional fields are omitted. A golden vector in the gate freezes the output.
/// </summary>
public static class ReceiptCanonicalizer
{
    /// <summary>Content hash codec — SHA256 lowercase hex, matching WatchService/DigestService.</summary>
    public static string ContentHash(string content) =>
        MerkleTree.HashPrefix + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(content))).ToLowerInvariant();

    /// <summary>Deterministic bytes over which the signature is computed (excludes <c>sig</c>).</summary>
    public static byte[] CanonicalBytes(ReceiptEnvelope e)
    {
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = false, SkipValidation = true }))
        {
            w.WriteStartObject();
            w.WriteNumber("v", e.V);
            w.WriteString("kind", e.Kind);
            w.WriteString("url", e.Url);
            w.WriteString("finalUrl", e.FinalUrl);
            w.WriteString("backend", e.Backend);
            w.WriteString("ts", e.Ts);
            w.WriteString("toolchain", e.Toolchain);
            if (e.Playbook is not null)
            {
                w.WriteStartObject("playbook");
                w.WriteString("id", e.Playbook.Id);
                w.WriteString("version", e.Playbook.Version);
                w.WriteEndObject();
            }

            if (e.ContentHash is not null) w.WriteString("contentHash", e.ContentHash);
            if (e.BlockMerkleRoot is not null) w.WriteString("blockMerkleRoot", e.BlockMerkleRoot);
            if (e.LeafSetComplete is not null) w.WriteBoolean("leafSetComplete", e.LeafSetComplete.Value);
            if (e.Tokens is not null) w.WriteNumber("tokens", e.Tokens.Value);
            if (e.FailureCode is not null) w.WriteString("failureCode", e.FailureCode);
            if (e.StatusCode is not null) w.WriteNumber("statusCode", e.StatusCode.Value);
            if (e.Confidence is not null) w.WriteNumber("confidence", e.Confidence.Value);
            w.WriteString("keyId", e.KeyId);
            w.WriteString("alg", e.Alg);
            // sig intentionally excluded
            w.WriteEndObject();
        }

        return buffer.WrittenSpan.ToArray();
    }
}
