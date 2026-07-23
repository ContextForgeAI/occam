using System.Security.Cryptography;

namespace OccamMcp.Core.Receipts;

/// <summary>Offline verification result for a receipt (SI-06 consumer loop, offline mode).</summary>
public sealed record ReceiptVerification(
    bool SignatureValid,
    bool? ContentHashMatch,
    string Verdict)
{
    public const string Verified = "verified";
    public const string SignatureInvalid = "signature_invalid";
    public const string ContentMismatch = "content_mismatch";
    public const string InvalidReceipt = "invalid_receipt";
}

/// <summary>
/// Verifies a receipt's signature (integrity + provenance) against a supplied public key, and
/// optionally that supplied markdown still hashes to <c>contentHash</c>. Trust of the KEY itself
/// (who owns k1:…) is out of scope for v1 — that is the registry PKI (SI-08). This is the offline
/// half of <c>occam_verify</c>; the live half (re-fetch + Merkle membership) lands in Phase 3.
/// </summary>
public static class ReceiptVerifier
{
    /// <summary>Verify a detached base64url signature over arbitrary bytes (SI-08 playbook signing).</summary>
    public static bool VerifyDetached(ReadOnlySpan<byte> data, string signatureBase64Url, string publicKeyPem)
    {
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            return key.VerifyData(data, Base64Url.Decode(signatureBase64Url), HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return false;
        }
    }

    public static ReceiptVerification VerifyOffline(ReceiptEnvelope receipt, string publicKeyPem, string? markdown = null)
    {
        if (receipt.Sig is null || receipt.V != ReceiptEnvelope.CurrentVersion)
        {
            return new ReceiptVerification(false, null, ReceiptVerification.InvalidReceipt);
        }

        bool signatureValid;
        try
        {
            using var key = ECDsa.Create();
            key.ImportFromPem(publicKeyPem);
            var bytes = ReceiptCanonicalizer.CanonicalBytes(receipt);
            var sig = Base64Url.Decode(receipt.Sig);
            signatureValid = key.VerifyData(bytes, sig, HashAlgorithmName.SHA256);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return new ReceiptVerification(false, null, ReceiptVerification.InvalidReceipt);
        }

        if (!signatureValid)
        {
            return new ReceiptVerification(false, null, ReceiptVerification.SignatureInvalid);
        }

        bool? hashMatch = null;
        if (markdown is not null && receipt.ContentHash is not null)
        {
            hashMatch = string.Equals(
                ReceiptCanonicalizer.ContentHash(markdown),
                receipt.ContentHash,
                StringComparison.Ordinal);
            if (hashMatch == false)
            {
                return new ReceiptVerification(true, false, ReceiptVerification.ContentMismatch);
            }
        }

        return new ReceiptVerification(true, hashMatch, ReceiptVerification.Verified);
    }
}
