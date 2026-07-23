using System.Globalization;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace OccamMcp.Core.Receipts;

/// <summary>
/// SI-15: an independent time attestation over a signed receipt — an RFC3161 timestamp token from a
/// Time-Stamping Authority (TSA) over <c>SHA-256(signature bytes)</c>. It proves the signed receipt
/// existed <b>no later than</b> <see cref="GenTime"/>, attested by a third party, so the time no longer
/// rests on the node's own clock. Rides as an unsigned sidecar (applied after signing); authentic on
/// its own because the token cryptographically binds the signature hash. <see cref="Type"/> leaves room
/// for OpenTimestamps later without a format break.
/// </summary>
public sealed record ReceiptTimeAnchor(string Type, string Tsa, string Token, string GenTime)
{
    public const string TypeRfc3161 = "rfc3161";
}

/// <summary>Result of verifying a time anchor against a receipt's signature.</summary>
public sealed record TimeAnchorVerification(
    bool Present,
    bool Valid,
    string? GenTime,
    string? Tsa,
    string? TsaSubject);

/// <summary>Offline verification of an RFC3161 time anchor (the consumer half of SI-15).</summary>
public static class TimeAnchorVerifier
{
    /// <summary>
    /// Verify a base64 RFC3161 token binds to <paramref name="expectedImprint"/> and is internally
    /// valid (the TSA's own signature over the imprint + genTime). TSA trust (chain-to-root) is out of
    /// scope for v1 — we return the signer subject and let the consumer decide. Never throws.
    /// </summary>
    public static (bool Valid, string? GenTime, string? Subject) VerifyToken(string tokenBase64, ReadOnlySpan<byte> expectedImprint)
    {
        try
        {
            var der = Convert.FromBase64String(tokenBase64);
            if (!Rfc3161TimestampToken.TryDecode(der, out var token, out _))
            {
                return (false, null, null);
            }

            var valid = token.VerifySignatureForHash(expectedImprint, HashAlgorithmName.SHA256, out X509Certificate2? signer);
            var genTime = token.TokenInfo.Timestamp.ToUniversalTime()
                .ToString("yyyy-MM-ddTHH:mm:ssZ", CultureInfo.InvariantCulture);
            return (valid, genTime, signer?.Subject);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return (false, null, null);
        }
    }

    /// <summary>Verify the anchor over the receipt's signature: imprint = SHA-256 of the raw signature bytes.</summary>
    public static TimeAnchorVerification Verify(ReceiptTimeAnchor? anchor, string? signatureBase64Url)
    {
        if (anchor is null)
        {
            return new TimeAnchorVerification(false, false, null, null, null);
        }

        if (anchor.Type != ReceiptTimeAnchor.TypeRfc3161 || string.IsNullOrEmpty(signatureBase64Url))
        {
            return new TimeAnchorVerification(true, false, anchor.GenTime, anchor.Tsa, null);
        }

        byte[] imprint;
        try
        {
            imprint = SHA256.HashData(Base64Url.Decode(signatureBase64Url));
        }
        catch (FormatException)
        {
            return new TimeAnchorVerification(true, false, anchor.GenTime, anchor.Tsa, null);
        }

        var (valid, genTime, subject) = VerifyToken(anchor.Token, imprint);
        return new TimeAnchorVerification(true, valid, valid ? genTime : anchor.GenTime, anchor.Tsa, subject);
    }
}
