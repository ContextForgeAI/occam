using System.Text.Json.Serialization;
using OccamMcp.Core.Claims;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Attest;

/// <summary>
/// Semantic support status for one claim against retrieved source blocks.
/// Independent of BM25 retrieval and of Merkle existence proofs.
/// </summary>
public static class AttestStatus
{
    public const string Supported = "supported";
    public const string Contradicted = "contradicted";
    public const string Related = "related";
    public const string Unsupported = "unsupported";
    public const string Unknown = "unknown";

    public static bool IsGroundedAlias(string status) =>
        string.Equals(status, Supported, StringComparison.Ordinal);
}

/// <summary>
/// SI-11 attest input row: one claim from an LLM report plus the source URL it cited.
/// </summary>
public sealed record OccamAttestClaimInput(string Claim, string SourceUrl);

/// <summary>
/// Per-claim attestation result. <see cref="Status"/> is the semantic verdict (fail-closed).
/// <see cref="Grounded"/> is a compat alias: true only when <see cref="Status"/> is
/// <c>supported</c> — never from BM25/lexical retrieval alone. When a block is attached,
/// <see cref="Leaf"/> + <see cref="Proof"/> reconstruct <see cref="BlockMerkleRoot"/> and prove
/// only that the block existed in the signed extract — never that the claim is true.
/// </summary>
public sealed record OccamAttestClaimResult(
    string Claim,
    string SourceUrl,
    string Status,
    bool Grounded,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? BlockIndex,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Score,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Leaf,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    MerkleProofStep[]? Proof,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BlockMerkleRoot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptEnvelope? Receipt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Reason);

/// <summary>
/// Report-level attestation. Named status counts are canonical. <see cref="Grounded"/> equals
/// <see cref="Supported"/> (compat). <see cref="UnsupportedTotal"/> is the sum of all non-supported
/// statuses (related + contradicted + unsupported + unknown) for legacy agents that gated on the
/// old binary partition.
/// </summary>
public sealed record OccamAttestResponse(
    bool Ok,
    int ClaimsTotal,
    int Supported,
    int Contradicted,
    int Related,
    int Unsupported,
    int Unknown,
    int Grounded,
    int UnsupportedTotal,
    OccamAttestClaimResult[] PerClaim,
    string Timestamp);

public sealed record OccamAttestFailureResponse(
    bool Ok,
    OccamClaimCheckFailureInfo Failure,
    string Timestamp);

[JsonSerializable(typeof(OccamAttestClaimInput))]
[JsonSerializable(typeof(OccamAttestClaimInput[]))]
[JsonSerializable(typeof(OccamAttestResponse))]
[JsonSerializable(typeof(OccamAttestFailureResponse))]
[JsonSerializable(typeof(OccamAttestClaimResult))]
[JsonSerializable(typeof(OccamAttestClaimResult[]))]
[JsonSerializable(typeof(MerkleProofStep))]
[JsonSerializable(typeof(MerkleProofStep[]))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamAttestJsonContext : JsonSerializerContext;
