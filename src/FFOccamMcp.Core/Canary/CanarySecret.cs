using System.Security.Cryptography;
using System.Text;

namespace OccamMcp.Core.Canary;

/// <summary>
/// Process-lifetime key material for the proof-of-read canary.
/// </summary>
/// <remarks>
/// <para>
/// A 32-byte CSPRNG root key is generated at construction, split into two independent subkeys with
/// HKDF-SHA256, and then zeroed. Nothing here is persisted, serialised or logged: there is no
/// property, no <c>ToString</c> and no JSON surface that can reach key bytes, and restarting the
/// host deliberately invalidates every outstanding sentinel.
/// </para>
/// <para>
/// Two subkeys instead of one so that the value used to pepper hashed client IPs can never be used
/// to forge a sentinel, and vice versa — separate purposes get separate keys even when the root
/// entropy is shared.
/// </para>
/// </remarks>
public sealed class CanarySecret : IDisposable
{
    /// <summary>Root key length in bytes (256 bits of CSPRNG entropy).</summary>
    public const int RootKeyBytes = 32;

    private const int SubkeyBytes = 32;
    private const int HashedIdentifierBytes = 8;

    private readonly byte[] _sentinelKey;
    private readonly byte[] _pepperKey;
    private bool _disposed;

    private CanarySecret(ReadOnlySpan<byte> rootKey)
    {
        _sentinelKey = new byte[SubkeyBytes];
        _pepperKey = new byte[SubkeyBytes];
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: rootKey,
            output: _sentinelKey,
            salt: default,
            info: Encoding.ASCII.GetBytes(CanarySentinel.SentinelKeyInfo));
        HKDF.DeriveKey(
            HashAlgorithmName.SHA256,
            ikm: rootKey,
            output: _pepperKey,
            salt: default,
            info: Encoding.ASCII.GetBytes(CanarySentinel.PepperKeyInfo));
    }

    /// <summary>
    /// Generates a fresh secret from the platform CSPRNG. This is the only constructor a production
    /// host uses; the root key is zeroed before the call returns.
    /// </summary>
    /// <returns>A new secret bound to this process.</returns>
    public static CanarySecret Create()
    {
        var root = RandomNumberGenerator.GetBytes(RootKeyBytes);
        try
        {
            return new CanarySecret(root);
        }
        finally
        {
            CryptographicOperations.ZeroMemory(root);
        }
    }

    /// <summary>
    /// Reconstructs a secret from a caller-supplied root key. Exists so the protocol can publish
    /// reproducible test vectors (PROBE_PROTOCOL.md §9) and so cross-platform determinism can be
    /// asserted against a fixed key. Never call this with a hard-coded or shared key in production.
    /// </summary>
    /// <param name="rootKey">Exactly <see cref="RootKeyBytes"/> bytes of key material.</param>
    /// <returns>A secret deriving the same sentinels on every platform and runtime.</returns>
    /// <exception cref="ArgumentException"><paramref name="rootKey"/> has the wrong length.</exception>
    public static CanarySecret FromRootKey(ReadOnlySpan<byte> rootKey)
    {
        if (rootKey.Length != RootKeyBytes)
        {
            throw new ArgumentException(
                $"Canary root key must be exactly {RootKeyBytes} bytes; got {rootKey.Length}.", nameof(rootKey));
        }

        return new CanarySecret(rootKey);
    }

    /// <summary>
    /// Derives the sentinel for a (bucket, session) pair. Deterministic: the same inputs against the
    /// same secret always yield the same string, which is what makes verification possible without
    /// storing any sentinel.
    /// </summary>
    /// <param name="bucket">Time bucket from <see cref="CanarySentinel.BucketFor"/>.</param>
    /// <param name="sessionId">Per-session identifier.</param>
    /// <param name="sentinelBytes">Tag length in bytes, in [16..32].</param>
    /// <returns>Unpadded base64url sentinel.</returns>
    /// <exception cref="ObjectDisposedException">The secret has been disposed.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="sentinelBytes"/> is out of range.</exception>
    public string DeriveSentinel(long bucket, string sessionId, int sentinelBytes = CanaryOptionsValidator.MaxSentinelBytes)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        ArgumentException.ThrowIfNullOrEmpty(sessionId);
        ArgumentOutOfRangeException.ThrowIfLessThan(sentinelBytes, CanaryOptionsValidator.MinSentinelBytes);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(sentinelBytes, CanaryOptionsValidator.MaxSentinelBytes);

        var input = CanarySentinel.BuildMacInput(bucket, sessionId);
        Span<byte> tag = stackalloc byte[SHA256.HashSizeInBytes];
        HMACSHA256.HashData(_sentinelKey, input, tag);
        return CanarySentinel.Encode(tag[..sentinelBytes]);
    }

    /// <summary>
    /// Peppered, truncated hash of a client identifier (typically a remote IP), for audit records
    /// that must be correlatable within one process lifetime but must not retain the address
    /// itself. The pepper never leaves the process, so the digest is not reversible by a log reader.
    /// </summary>
    /// <param name="value">Identifier to hash; <c>null</c> or blank maps to <c>"none"</c>.</param>
    /// <returns>Lowercase hex digest prefixed with <c>h1:</c>, or <c>"none"</c>.</returns>
    /// <exception cref="ObjectDisposedException">The secret has been disposed.</exception>
    public string HashIdentifier(string? value)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (string.IsNullOrWhiteSpace(value))
        {
            return "none";
        }

        Span<byte> digest = stackalloc byte[SHA256.HashSizeInBytes];
        HMACSHA256.HashData(_pepperKey, Encoding.UTF8.GetBytes(value), digest);
        return "h1:" + Convert.ToHexString(digest[..HashedIdentifierBytes]).ToLowerInvariant();
    }

    /// <summary>Zeroes both subkeys. Subsequent derivation attempts throw.</summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        CryptographicOperations.ZeroMemory(_sentinelKey);
        CryptographicOperations.ZeroMemory(_pepperKey);
        _disposed = true;
    }

    /// <summary>Redacted by design — the secret must never reach a log line or an error message.</summary>
    /// <returns>A constant placeholder.</returns>
    public override string ToString() => "CanarySecret(redacted)";
}
