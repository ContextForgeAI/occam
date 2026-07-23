using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Receipts;

/// <summary>
/// Proof-carrying capsule (v1) — the first harness primitive (HARNESS-P0-SPEC). A self-contained,
/// passable object that a RECEIVING agent verifies OFFLINE, without re-fetching the page: it bundles
/// the signed <see cref="ReceiptEnvelope"/>, the extracted markdown (whose hash the envelope commits
/// to), the ordered block leaves (so a citation/prove works without the page), an optional time
/// anchor, and a self-describing <see cref="CapsuleVerifyRecipe"/> so a stranger agent can verify
/// with no prior knowledge of Occam. Wire form: <c>occam://capsule/&lt;base64url(json)&gt;</c>.
/// The signature is always over the envelope's canonical bytes — the capsule packaging never enters
/// the signed data, so wrapping/unwrapping cannot change what was signed.
/// </summary>
public sealed record CapsulePayload(
    [property: JsonPropertyName("cap")] int Cap,
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("signed")] ReceiptEnvelope Signed,
    [property: JsonPropertyName("content"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Content,
    [property: JsonPropertyName("blockLeaves"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string[]? BlockLeaves,
    [property: JsonPropertyName("timeAnchor"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReceiptTimeAnchor? TimeAnchor,
    [property: JsonPropertyName("verifyRecipe"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] CapsuleVerifyRecipe? VerifyRecipe);

/// <summary>Self-describing verification hint carried inside a capsule (idea #9): algorithm, Merkle
/// leaf construction, the key anchor, and a runnable one-liner — so a consumer needs no out-of-band docs.</summary>
public sealed record CapsuleVerifyRecipe(
    [property: JsonPropertyName("alg")] string Alg,
    [property: JsonPropertyName("merkle")] string Merkle,
    [property: JsonPropertyName("keyAnchor")] string KeyAnchor,
    [property: JsonPropertyName("verify")] string Verify);

public static class CapsuleCodec
{
    public const string Scheme = "occam://capsule/";
    public const int CurrentVersion = 1;

    private const string MerkleDescription = "sha256, leaf=utf8(text+\\0+source_selector), odd-dup-last";
    private const string VerifyCommand = "occam verify --receipt <capsule> --pubkey <pem>";

    /// <summary>True if <paramref name="value"/> is (syntactically) a capsule URI, cheap enough to gate on.</summary>
    public static bool IsCapsule(string? value) =>
        value is not null && value.AsSpan().TrimStart().StartsWith(Scheme, StringComparison.Ordinal);

    /// <summary>Build a capsule payload from a signed receipt bundle (as produced by a transcode response).</summary>
    public static CapsulePayload FromReceipt(
        ReceiptEnvelope signed, string? content, string[]? blockLeaves, ReceiptTimeAnchor? timeAnchor = null) =>
        new(CurrentVersion,
            signed.Kind,
            signed,
            content,
            blockLeaves,
            timeAnchor,
            new CapsuleVerifyRecipe(
                signed.Alg,
                MerkleDescription,
                signed.KeyId,
                VerifyCommand));

    /// <summary>Serialize to the <c>occam://capsule/…</c> wire form.</summary>
    public static string Encode(CapsulePayload payload)
    {
        var json = JsonSerializer.SerializeToUtf8Bytes(payload, CapsuleJsonContext.Default.CapsulePayload);
        return Scheme + Base64Url.Encode(json);
    }

    /// <summary>Parse a capsule URI. Returns false (never throws) on a non-capsule, bad base64, or bad JSON.</summary>
    public static bool TryParse(string? value, out CapsulePayload? payload)
    {
        payload = null;
        if (!IsCapsule(value))
        {
            return false;
        }

        try
        {
            var body = value!.Trim()[Scheme.Length..];
            var json = Base64Url.Decode(body);
            payload = JsonSerializer.Deserialize(json, CapsuleJsonContext.Default.CapsulePayload);
            return payload?.Signed is not null;
        }
        catch (Exception ex) when (ex is FormatException or JsonException)
        {
            return false;
        }
    }
}

[JsonSerializable(typeof(CapsulePayload))]
[JsonSerializable(typeof(CapsuleVerifyRecipe))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSerializable(typeof(ReceiptTimeAnchor))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class CapsuleJsonContext : JsonSerializerContext;
