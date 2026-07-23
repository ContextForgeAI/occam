using System.Security.Cryptography;
using System.Text;

namespace OccamMcp.Core.Compile;

/// <summary>
/// The AF-6 conditional-response token for <c>if_none_match</c>: a bare lowercase-hex SHA-256 of the
/// markdown, used like an ETag. It is intentionally the SAME digest as a receipt's <c>contentHash</c>
/// but without the <c>sha256:</c> prefix (that codec — <see cref="Receipts.ReceiptCanonicalizer.ContentHash"/>
/// — adds it). To let a caller reuse <c>receipt.signed.contentHash</c> directly as an
/// <c>if_none_match</c> token, <see cref="Matches"/> tolerates an optional leading <c>sha256:</c>.
/// </summary>
public static class ContentHashToken
{
    private const string Prefix = "sha256:";

    /// <summary>Bare lowercase-hex SHA-256 of the markdown (the value surfaced for the client to store).</summary>
    public static string BareHex(string markdown) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(markdown))).ToLowerInvariant();

    /// <summary>
    /// True when <paramref name="token"/> matches the markdown's hash, accepting either the bare-hex
    /// form or the <c>sha256:</c>-prefixed form (so the receipt's <c>contentHash</c> works as-is).
    /// </summary>
    public static bool Matches(string markdown, string token)
    {
        var normalized = token.StartsWith(Prefix, StringComparison.OrdinalIgnoreCase)
            ? token[Prefix.Length..]
            : token;
        return BareHex(markdown).Equals(normalized, StringComparison.OrdinalIgnoreCase);
    }
}
