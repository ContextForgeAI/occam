using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

namespace OccamMcp.Core.Canary;

/// <summary>
/// Key-free, pure helpers for the sentinel wire format: canonical MAC input framing, time-bucket
/// arithmetic, and constant-time comparison. Keeping these separate from <see cref="CanarySecret"/>
/// means the framing rules (the part a second implementation must match byte-for-byte) are testable
/// without ever touching key material. Normative definition: PROBE_PROTOCOL.md §3.
/// </summary>
public static class CanarySentinel
{
    /// <summary>
    /// Domain-separation label. Any change to the framing or tag semantics MUST bump this string,
    /// which makes old sentinels unverifiable instead of silently reinterpreted.
    /// </summary>
    public const string DomainLabel = "occam-canary-v1";

    /// <summary>HKDF <c>info</c> for the sentinel MAC subkey.</summary>
    public const string SentinelKeyInfo = "occam-canary-v1/sentinel-mac";

    /// <summary>HKDF <c>info</c> for the identifier-hashing pepper (hashed client IPs).</summary>
    public const string PepperKeyInfo = "occam-canary-v1/identifier-pepper";

    /// <summary>Longest base64url sentinel the verifier will even look at (32-byte tag, unpadded).</summary>
    public const int MaxEncodedLength = 43;

    /// <summary>Converts a wall-clock instant to its protocol time bucket.</summary>
    /// <param name="timestamp">Instant to convert; interpreted as UTC.</param>
    /// <param name="bucketSeconds">Bucket width, normally <see cref="CanaryOptions.DefaultBucketSeconds"/>.</param>
    /// <returns><c>floor(unixSeconds / bucketSeconds)</c>, using floor semantics for pre-epoch input.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="bucketSeconds"/> is not positive.</exception>
    public static long BucketFor(DateTimeOffset timestamp, int bucketSeconds)
    {
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bucketSeconds);
        var unixSeconds = timestamp.ToUnixTimeSeconds();
        // Integer division truncates toward zero; the protocol needs floor so bucket ordering stays
        // monotonic for pre-1970 clocks (a misconfigured container can report them).
        var quotient = Math.DivRem(unixSeconds, bucketSeconds, out var remainder);
        return remainder < 0 ? quotient - 1 : quotient;
    }

    /// <summary>
    /// Builds the canonical MAC input for a (bucket, session) pair:
    /// <c>label ‖ 0x00 ‖ be64(bucket) ‖ be32(len(sessionId)) ‖ utf8(sessionId)</c>.
    /// The fixed-width bucket and explicit session-id length make the encoding injective, so no two
    /// distinct inputs can collide into one tag.
    /// </summary>
    /// <param name="bucket">Time bucket from <see cref="BucketFor"/>.</param>
    /// <param name="sessionId">Per-session identifier; must be non-empty.</param>
    /// <returns>Freshly allocated canonical byte sequence.</returns>
    public static byte[] BuildMacInput(long bucket, string sessionId)
    {
        ArgumentException.ThrowIfNullOrEmpty(sessionId);

        var label = Encoding.ASCII.GetBytes(DomainLabel);
        var session = Encoding.UTF8.GetBytes(sessionId);
        var input = new byte[label.Length + 1 + sizeof(long) + sizeof(int) + session.Length];

        var offset = 0;
        label.CopyTo(input, offset);
        offset += label.Length;
        input[offset++] = 0x00;
        BinaryPrimitives.WriteInt64BigEndian(input.AsSpan(offset), bucket);
        offset += sizeof(long);
        BinaryPrimitives.WriteInt32BigEndian(input.AsSpan(offset), session.Length);
        offset += sizeof(int);
        session.CopyTo(input, offset);

        return input;
    }

    /// <summary>
    /// Whether a session id is acceptable. The id is caller-controlled and ends up in an HTML
    /// document, a URL path and structured log fields, so the charset is restricted to
    /// <c>A-Z a-z 0-9 - _ .</c> rather than relying on downstream escaping alone.
    /// </summary>
    /// <param name="sessionId">Candidate id.</param>
    /// <param name="maxLength">Inclusive upper bound on length.</param>
    /// <returns><c>true</c> when the id is safe to use as-is.</returns>
    public static bool IsValidSessionId(string? sessionId, int maxLength)
    {
        if (string.IsNullOrEmpty(sessionId) || sessionId.Length > maxLength)
        {
            return false;
        }

        foreach (var c in sessionId)
        {
            var ok = c is >= 'a' and <= 'z'
                || c is >= 'A' and <= 'Z'
                || c is >= '0' and <= '9'
                || c is '-' or '_' or '.';
            if (!ok)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// Normalises a sentinel as claimed by an agent: trims surrounding whitespace and quotes that
    /// chat models habitually add. Returns <c>null</c> when the value cannot be a sentinel at all,
    /// so the verifier can short-circuit to <see cref="CanaryVerdict.Hallucinated"/> without
    /// spending HMAC work on obviously bogus input.
    /// </summary>
    /// <param name="claimed">Raw value reported by the agent.</param>
    /// <returns>Normalised candidate, or <c>null</c> if it is empty or over-long.</returns>
    public static string? Normalize(string? claimed)
    {
        if (string.IsNullOrWhiteSpace(claimed))
        {
            return null;
        }

        var trimmed = claimed.Trim().Trim('"', '\'', '`').Trim();
        if (trimmed.Length == 0 || trimmed.Length > MaxEncodedLength)
        {
            return null;
        }

        return trimmed;
    }

    /// <summary>
    /// Compares two encoded sentinels without leaking which byte differed. Length inequality is
    /// public information (it is visible in the wire format), so it short-circuits.
    /// </summary>
    /// <param name="expected">Sentinel derived by this host.</param>
    /// <param name="claimed">Normalised value reported by the agent.</param>
    /// <returns><c>true</c> when the two encodings are identical.</returns>
    public static bool FixedTimeEquals(string expected, string claimed)
    {
        ArgumentNullException.ThrowIfNull(expected);
        ArgumentNullException.ThrowIfNull(claimed);

        if (expected.Length != claimed.Length)
        {
            return false;
        }

        // Both sides are ASCII base64url, so byte-per-char holds and the comparison stays fixed-time.
        Span<byte> left = stackalloc byte[MaxEncodedLength];
        Span<byte> right = stackalloc byte[MaxEncodedLength];
        var written = Encoding.ASCII.GetBytes(expected, left);
        Encoding.ASCII.GetBytes(claimed, right);

        return CryptographicOperations.FixedTimeEquals(left[..written], right[..written]);
    }

    /// <summary>
    /// Encodes a raw tag using the wire encoding: RFC 4648 §5 base64url, no padding. Chosen over
    /// standard base64 because <c>+</c>, <c>/</c> and <c>=</c> survive neither URL embedding nor
    /// round-tripping through chat transcripts reliably (PROBE_PROTOCOL.md §3.4).
    /// </summary>
    /// <param name="tag">Raw MAC bytes, already truncated to the configured length.</param>
    /// <returns>Unpadded base64url text.</returns>
    public static string Encode(ReadOnlySpan<byte> tag) =>
        Convert.ToBase64String(tag).TrimEnd('=').Replace('+', '-').Replace('/', '_');
}
