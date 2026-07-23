using System.Text.Json.Serialization;

namespace OccamMcp.Core.Receipts;

/// <summary>
/// Receipt v1 (SPEC-receipt-v1.md). A signed extraction envelope — the flagship "verifiable
/// extraction" primitive. Two variants share one schema: a positive receipt (<c>Kind = "extraction"</c>,
/// carries <see cref="ContentHash"/>) and a negative receipt (<c>Kind = "negative"</c>, a signed
/// honest <c>ok:false</c>, SI-03). <see cref="Sig"/> is attached after signing and is EXCLUDED from
/// the canonical bytes — see <see cref="ReceiptCanonicalizer"/>.
/// </summary>
public sealed record ReceiptEnvelope(
    int V,
    string Kind,
    string Url,
    string FinalUrl,
    string Backend,
    string Ts,
    string Toolchain,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptPlaybook? Playbook,
    // positive-only
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContentHash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BlockMerkleRoot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Tokens,
    // negative-only
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailureCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? StatusCode,
    // signed advisory (D3 = yes)
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Confidence,
    // signature block
    string KeyId,
    string Alg,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Sig,
    // positive-only, signed: attests that blockMerkleRoot covers the COMPLETE extracted content (no
    // token/fit pruning dropped blocks) — so "claim X is absent" is provable (X is not among the signed
    // leaves), not merely "not found". Null (omitted) unless the extract was complete; a null value
    // writes identical canonical bytes to a pre-field receipt, so it is backward-compatible.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? LeafSetComplete = null)
{
    public const int CurrentVersion = 1;
    public const string KindExtraction = "extraction";
    public const string KindNegative = "negative";
    public const string AlgEcdsaP256 = "ecdsa-p256-sha256";
}

public sealed record ReceiptPlaybook(string Id, string Version);

[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class ReceiptJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
