using System.Buffers;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Playbooks;

/// <summary>
/// SI-08 (local foundation): sign a playbook at save time so a recipe is self-authenticating — it
/// carries a claimed key id, a signature, and unsigned verify-gate metadata (score/passesGate). The
/// signature covers a canonical hash of the playbook with its own <c>provenance</c> block excluded,
/// so re-signing / re-verifying is stable. This is the building block a future signed registry
/// (SI-08 distribution) and reputation counter build on; no hosting is required to sign locally.
/// </summary>
/// <summary>
/// Result of <see cref="PlaybookSignature.Inspect"/>. <c>Status</c> ∈ { <c>unsigned</c>, <c>verified</c>,
/// <c>invalid</c>, <c>wrong_key</c>, <c>key_mismatch</c>, <c>unsupported_version</c> }. Not a resolve
/// failure — a trust signal a consumer weighs before applying the recipe. <c>SigVersion</c> is 1, 2, or
/// null (unsigned). Under v1, <c>Score</c>/<c>PassesGate</c> echo UNSIGNED provenance claims; under v2
/// they are covered by the signature (tamper-evident, still not a proof of quality).
/// </summary>
public sealed record PlaybookSignatureStatus(
    bool Present,
    string Status,
    string? KeyId,
    int? Score,
    bool? PassesGate,
    int? SigVersion = null);

public static class PlaybookSignature
{
    public const string Alg = "ecdsa-p256-sha256";

    // OD-4 / PLAYBOOK-SIGNATURE-V2-CONTRACT.md.
    public const string SchemeV1 = "playbook-sig-v1";
    public const string SchemeV2 = "playbook-sig-v2";

    // Domain-separation prefix for the v2 preimage: "occam-playbook-sig-v2" + 0x0A.
    private static readonly byte[] DomainV2Prefix = Encoding.ASCII.GetBytes(SchemeV2 + "\n");

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

    /// <summary>
    /// Return the playbook JSON with a fresh signed <c>provenance</c> block injected. As of OD-4 the
    /// default scheme is <see cref="SchemeV2"/>, whose signature covers keyId/alg/contentHash/signedAt
    /// and the verify-gate snapshot (see the v2 contract). Pass <paramref name="scheme"/>=<see cref="SchemeV1"/>
    /// only to reproduce a legacy artifact (fixtures/compat tests).
    /// </summary>
    public static string BuildSignedJson(
        string playbookJson,
        int? score,
        bool passesGate,
        double? noise,
        ReceiptSigner signer,
        string scheme = SchemeV2)
    {
        var contentHash = ContentHash(playbookJson);
        var signedAt = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);

        string signature;
        if (scheme == SchemeV1)
        {
            // Legacy: sign only utf8(contentHash); provenance fields remain unsigned.
            signature = signer.SignDetached(Encoding.UTF8.GetBytes(contentHash));
        }
        else if (scheme == SchemeV2)
        {
            signature = signer.SignDetached(
                BuildV2Preimage(signer.KeyId, contentHash, signedAt, score, passesGate, noise));
        }
        else
        {
            throw new ArgumentException($"Unknown signature scheme '{scheme}'.", nameof(scheme));
        }

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
            w.WriteString("sigScheme", scheme);
            w.WriteString("keyId", signer.KeyId);
            w.WriteString("alg", Alg);
            w.WriteString("contentHash", contentHash);
            w.WriteString("signature", signature);
            w.WriteString("signedAt", signedAt);
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
    /// Build the deterministic v2 preimage bytes: domain prefix ‖ canonical(assertion). The assertion
    /// object binds v/alg/keyId/contentHash/signedAt/verify under the signature (see the v2 contract).
    /// Optional verify sub-fields are omitted (never null) so presence is part of the signed shape.
    /// </summary>
    private static byte[] BuildV2Preimage(
        string keyId,
        string contentHash,
        string signedAt,
        int? score,
        bool passesGate,
        double? noise)
    {
        var assertion = new ArrayBufferWriter<byte>(256);
        using (var w = new Utf8JsonWriter(assertion, new JsonWriterOptions { SkipValidation = true }))
        {
            // Written in a fixed order for readability; WriteCanonical below re-sorts deterministically.
            w.WriteStartObject();
            w.WriteNumber("v", 2);
            w.WriteString("alg", Alg);
            w.WriteString("keyId", keyId);
            w.WriteString("contentHash", contentHash);
            w.WriteString("signedAt", signedAt);
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
        }

        // Re-canonicalize (sorted keys) so byte output is independent of the writer order above.
        using var assertionDoc = JsonDocument.Parse(assertion.WrittenMemory);
        var canonical = new ArrayBufferWriter<byte>(256);
        using (var cw = new Utf8JsonWriter(canonical, new JsonWriterOptions { SkipValidation = true }))
        {
            WriteCanonical(assertionDoc.RootElement, cw, null);
        }

        var preimage = new byte[DomainV2Prefix.Length + canonical.WrittenCount];
        DomainV2Prefix.CopyTo(preimage, 0);
        canonical.WrittenSpan.CopyTo(preimage.AsSpan(DomainV2Prefix.Length));
        return preimage;
    }

    /// <summary>
    /// Resolve-side inspection (SI-08 consumer loop): classify a resolved playbook's provenance
    /// against the local key WITHOUT trusting the recipe's claimed key id. Cryptographic verification
    /// is attempted before classification. Never throws; a malformed recipe reads as <c>unsigned</c>.
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
                return new PlaybookSignatureStatus(false, "unsigned", null, null, null, null);
            }

            var scheme = prov.TryGetProperty("sigScheme", out var sc) ? sc.GetString() : null;
            // Absent marker → legacy v1. Unknown marker → unsupported.
            int? sigVersion = scheme switch
            {
                null or SchemeV1 => 1,
                SchemeV2 => 2,
                _ => null,
            };

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

            if (sigVersion is null)
            {
                return new PlaybookSignatureStatus(true, "unsupported_version", claimedKeyId, score, passesGate, null);
            }

            var valid = Verify(playbookJson, localPublicKeyPem);
            var keyMatches = string.Equals(claimedKeyId, localKeyId, StringComparison.Ordinal);
            var status = valid
                ? keyMatches ? "verified" : "key_mismatch"
                : keyMatches ? "invalid" : "wrong_key";
            return new PlaybookSignatureStatus(true, status, claimedKeyId, score, passesGate, sigVersion);
        }
        catch (JsonException)
        {
            return new PlaybookSignatureStatus(false, "unsigned", null, null, null, null);
        }
    }

    /// <summary>
    /// Verify a signed playbook against a public key. Dispatches by <c>provenance.sigScheme</c>: v1
    /// (or absent) checks the detached signature over <c>utf8(contentHash)</c>; v2 checks the signature
    /// over the domain-separated assertion preimage. A v1 artifact is never verified under v2 rules or
    /// vice versa. An unknown scheme returns false (see <see cref="Inspect"/> for the typed verdict).
    /// </summary>
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

            var scheme = prov.TryGetProperty("sigScheme", out var sc) ? sc.GetString() : null;
            var signature = sigEl.GetString() ?? string.Empty;

            if (scheme is null || scheme == SchemeV1)
            {
                return ReceiptVerifier.VerifyDetached(Encoding.UTF8.GetBytes(recomputed), signature, publicKeyPem);
            }

            if (scheme == SchemeV2)
            {
                var claimedKeyId = prov.TryGetProperty("keyId", out var k) ? k.GetString() : null;
                var signedAt = prov.TryGetProperty("signedAt", out var sa) ? sa.GetString() : null;
                if (claimedKeyId is null || signedAt is null)
                {
                    return false;
                }

                int? score = null;
                bool passesGate = false;
                double? noise = null;
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

                    if (v.TryGetProperty("noiseLeakage", out var nl) && nl.ValueKind == JsonValueKind.Number)
                    {
                        noise = nl.GetDouble();
                    }
                }

                var preimage = BuildV2Preimage(claimedKeyId, recomputed, signedAt, score, passesGate, noise);
                return ReceiptVerifier.VerifyDetached(preimage, signature, publicKeyPem);
            }

            return false; // unsupported scheme
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
