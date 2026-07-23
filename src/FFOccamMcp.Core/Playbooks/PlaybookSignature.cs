using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Playbooks;

/// <summary>
/// SI-08 (local foundation): sign a playbook at save time so a recipe is self-authenticating — it
/// carries the author's key id, a signature, and the verify-gate proof (score/passesGate). The
/// signature covers a canonical hash of the playbook with its own <c>provenance</c> block excluded,
/// so re-signing / re-verifying is stable. This is the building block a future signed registry
/// (SI-08 distribution) and reputation counter build on; no hosting is required to sign locally.
/// </summary>
/// <summary>
/// Result of <see cref="PlaybookSignature.Inspect"/>. <c>Status</c> ∈ { <c>unsigned</c>, <c>verified</c>,
/// <c>invalid</c>, <c>unknown_key</c> }. Not a resolve failure — a trust signal a consumer weighs before
/// applying the recipe. <c>Score</c>/<c>PassesGate</c> echo the recipe's own verify-gate claim (only
/// trustworthy when <c>Status == verified</c>).
/// </summary>
public sealed record PlaybookSignatureStatus(bool Present, string Status, string? KeyId, int? Score, bool? PassesGate);

public static class PlaybookSignature
{
    public const string Alg = "ecdsa-p256-sha256";

    /// <summary>Canonical SHA-256 (sha256:hex) over the playbook with any top-level provenance removed.</summary>
    public static string ContentHash(string playbookJson)
    {
        using var doc = JsonDocument.Parse(playbookJson);
        var buffer = new ArrayBufferWriter<byte>(256);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { SkipValidation = true }))
        {
            WriteCanonical(doc.RootElement, w, excludeTopKey: "provenance");
        }

        return "sha256:" + Convert.ToHexString(SHA256.HashData(buffer.WrittenSpan)).ToLowerInvariant();
    }

    /// <summary>Return the playbook JSON with a fresh signed <c>provenance</c> block injected.</summary>
    public static string BuildSignedJson(string playbookJson, int? score, bool passesGate, double? noise, ReceiptSigner signer)
    {
        var contentHash = ContentHash(playbookJson);
        var signature = signer.SignDetached(Encoding.UTF8.GetBytes(contentHash));

        using var doc = JsonDocument.Parse(playbookJson);
        var buffer = new ArrayBufferWriter<byte>(512);
        using (var w = new Utf8JsonWriter(buffer, new JsonWriterOptions { Indented = true }))
        {
            w.WriteStartObject();
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                if (prop.Name == "provenance")
                {
                    continue; // replace any existing block
                }

                prop.WriteTo(w);
            }

            w.WritePropertyName("provenance");
            w.WriteStartObject();
            w.WriteString("keyId", signer.KeyId);
            w.WriteString("alg", Alg);
            w.WriteString("contentHash", contentHash);
            w.WriteString("signature", signature);
            w.WriteString("signedAt", DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture));
            w.WriteStartObject("verify");
            if (score.HasValue)
            {
                w.WriteNumber("score", score.Value);
            }

            w.WriteBoolean("passesGate", passesGate);
            if (noise.HasValue)
            {
                w.WriteNumber("noiseLeakage", noise.Value);
            }

            w.WriteEndObject();
            w.WriteEndObject();
            w.WriteEndObject();
        }

        return Encoding.UTF8.GetString(buffer.WrittenSpan);
    }

    /// <summary>
    /// Resolve-side inspection (SI-08 consumer loop): classify a resolved playbook's provenance
    /// against the local key WITHOUT trusting the recipe's own claim. Distinguishes tampering
    /// (<c>invalid</c> — signed by our key but hash/sig no longer check out) from a foreign author
    /// (<c>unknown_key</c> — a real signature we cannot verify with the only key we hold). Never
    /// throws; a malformed recipe reads as <c>unsigned</c>.
    /// </summary>
    public static PlaybookSignatureStatus Inspect(string playbookJson, string localKeyId, string localPublicKeyPem)
    {
        try
        {
            using var doc = JsonDocument.Parse(playbookJson);
            if (!doc.RootElement.TryGetProperty("provenance", out var prov)
                || prov.ValueKind != JsonValueKind.Object
                || !prov.TryGetProperty("signature", out _))
            {
                return new PlaybookSignatureStatus(false, "unsigned", null, null, null);
            }

            var claimedKeyId = prov.TryGetProperty("keyId", out var k) ? k.GetString() : null;
            int? score = null;
            bool? passesGate = null;
            if (prov.TryGetProperty("verify", out var v) && v.ValueKind == JsonValueKind.Object)
            {
                if (v.TryGetProperty("score", out var s) && s.ValueKind == JsonValueKind.Number)
                {
                    score = s.GetInt32();
                }

                if (v.TryGetProperty("passesGate", out var pg)
                    && (pg.ValueKind == JsonValueKind.True || pg.ValueKind == JsonValueKind.False))
                {
                    passesGate = pg.GetBoolean();
                }
            }

            // A recipe signed by a key we do not hold cannot be verified locally — report the claim,
            // do not pretend it verified. Only a claim to OUR key is checkable, so mismatch there is tamper.
            if (!string.Equals(claimedKeyId, localKeyId, StringComparison.Ordinal))
            {
                return new PlaybookSignatureStatus(true, "unknown_key", claimedKeyId, score, passesGate);
            }

            var valid = Verify(playbookJson, localPublicKeyPem);
            return new PlaybookSignatureStatus(true, valid ? "verified" : "invalid", claimedKeyId, score, passesGate);
        }
        catch (JsonException)
        {
            return new PlaybookSignatureStatus(false, "unsigned", null, null, null);
        }
    }

    /// <summary>Verify a signed playbook against a public key: content hash matches + signature valid.</summary>
    public static bool Verify(string signedPlaybookJson, string publicKeyPem)
    {
        try
        {
            using var doc = JsonDocument.Parse(signedPlaybookJson);
            if (!doc.RootElement.TryGetProperty("provenance", out var prov)
                || !prov.TryGetProperty("signature", out var sigEl)
                || !prov.TryGetProperty("contentHash", out var hashEl))
            {
                return false;
            }

            var recomputed = ContentHash(signedPlaybookJson);
            if (!string.Equals(recomputed, hashEl.GetString(), StringComparison.Ordinal))
            {
                return false;
            }

            return ReceiptVerifier.VerifyDetached(Encoding.UTF8.GetBytes(recomputed), sigEl.GetString() ?? string.Empty, publicKeyPem);
        }
        catch (JsonException)
        {
            return false;
        }
    }

    private static void WriteCanonical(JsonElement el, Utf8JsonWriter w, string? excludeTopKey)
    {
        switch (el.ValueKind)
        {
            case JsonValueKind.Object:
                w.WriteStartObject();
                foreach (var prop in el.EnumerateObject()
                             .Where(p => p.Name != excludeTopKey)
                             .OrderBy(p => p.Name, StringComparer.Ordinal))
                {
                    w.WritePropertyName(prop.Name);
                    WriteCanonical(prop.Value, w, null); // exclusion only applies at the top level
                }

                w.WriteEndObject();
                break;
            case JsonValueKind.Array:
                w.WriteStartArray();
                foreach (var item in el.EnumerateArray())
                {
                    WriteCanonical(item, w, null);
                }

                w.WriteEndArray();
                break;
            default:
                el.WriteTo(w);
                break;
        }
    }
}
