using System.Reflection;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace OccamMcp.Core.Receipts;

/// <summary>
/// Signs receipts with a local ECDsa P-256 key (D4: BCL-native + AOT-safe; <c>alg</c> in the
/// envelope lets Ed25519 be added later without a format break). v1 is self-managed local keys —
/// key distribution / third-party trust is out of scope (SI-08). The private key lives under
/// <c>OCCAM_KEYS_ROOT</c> (default <c>~/.occam/keys/</c>), generated on first use, never logged.
/// </summary>
public sealed class ReceiptSigner
{
    private readonly ECDsa _key;
    public string KeyId { get; }
    public static string Toolchain { get; } = ResolveToolchain();

    private ReceiptSigner(ECDsa key)
    {
        _key = key;
        KeyId = ComputeKeyId(key);
    }

    /// <summary>Load the key at <paramref name="keysRoot"/> or generate + persist a new one.</summary>
    public static ReceiptSigner LoadOrCreate(string? keysRoot = null)
    {
        var root = keysRoot ?? DefaultKeysRoot();
        Directory.CreateDirectory(root);
        var keyPath = Path.Combine(root, "signing-key.pem");

        var key = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        if (File.Exists(keyPath))
        {
            key.ImportFromPem(File.ReadAllText(keyPath));
        }
        else
        {
            var pem = PemEncoding.WriteString("PRIVATE KEY", key.ExportPkcs8PrivateKey());
            File.WriteAllText(keyPath, pem);
            TryHardenPermissions(keyPath);
        }

        return new ReceiptSigner(key);
    }

    /// <summary>In-memory signer for tests — no disk, ephemeral key.</summary>
    public static ReceiptSigner CreateEphemeral() => new(ECDsa.Create(ECCurve.NamedCurves.nistP256));

    /// <summary>Return a copy of <paramref name="unsigned"/> with keyId/alg/sig attached.</summary>
    public ReceiptEnvelope Sign(ReceiptEnvelope unsigned)
    {
        var stamped = unsigned with
        {
            KeyId = KeyId,
            Alg = ReceiptEnvelope.AlgEcdsaP256,
            Sig = null,
        };
        var bytes = ReceiptCanonicalizer.CanonicalBytes(stamped);
        // Default ECDsa.SignData returns the IEEE P1363 fixed-size r‖s encoding — stable across
        // platforms, unlike the DER/ASN.1 variable-length form.
        var sig = _key.SignData(bytes, HashAlgorithmName.SHA256);
        return stamped with { Sig = Base64Url.Encode(sig) };
    }

    /// <summary>SPKI public key (PEM) for a consumer to pin — see <c>occam keys export</c>.</summary>
    public string ExportPublicKeyPem() =>
        PemEncoding.WriteString("PUBLIC KEY", _key.ExportSubjectPublicKeyInfo());

    /// <summary>Detached base64url signature over arbitrary bytes (SI-08 playbook signing).</summary>
    public string SignDetached(ReadOnlySpan<byte> data) =>
        Base64Url.Encode(_key.SignData(data, HashAlgorithmName.SHA256));

    private static string ComputeKeyId(ECDsa key)
    {
        var fingerprint = SHA256.HashData(key.ExportSubjectPublicKeyInfo());
        return "k1:" + Convert.ToHexString(fingerprint).ToLowerInvariant()[..16];
    }

    private static string DefaultKeysRoot() =>
        Environment.GetEnvironmentVariable("OCCAM_KEYS_ROOT")
        ?? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".occam", "keys");

    private static void TryHardenPermissions(string path)
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return; // NTFS ACLs inherit; POSIX 0600 not applicable
        }

        try
        {
            File.SetUnixFileMode(path, UnixFileMode.UserRead | UnixFileMode.UserWrite);
        }
        catch
        {
            // best-effort hardening; key is still usable
        }
    }

    private static string ResolveToolchain()
    {
        var version = Assembly.GetExecutingAssembly()
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion
            ?? Assembly.GetExecutingAssembly().GetName().Version?.ToString()
            ?? "dev";
        // strip build metadata (+sha) for a stable, human string
        var plus = version.IndexOf('+');
        if (plus >= 0)
        {
            version = version[..plus];
        }

        return "ff-occam/" + version;
    }
}
