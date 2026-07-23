using System.Text.Json.Serialization;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Claims;

/// <summary>
/// A provable source block relevant to the claim. <see cref="Leaf"/> + <see cref="Proof"/> reconstruct
/// the receipt's <c>blockMerkleRoot</c>; <see cref="Text"/> + <see cref="SourceSelector"/> recompute the
/// leaf — so a third party can prove this block was in the signed extraction via <c>occam_verify citation</c>
/// without the page. Stance (support vs refute) is the caller's judgment.
/// </summary>
public sealed record OccamClaimMatchInfo(
    int BlockIndex,
    string Text,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SourceSelector,
    double Score,
    string Leaf,
    MerkleProofStep[] Proof);

public sealed record OccamClaimCheckSuccessResponse(
    bool Ok,
    string Url,
    string Claim,
    bool Found,
    /// <summary>PR-F alias clarifying that <see cref="Found"/> is retrieval relevance, not support.</summary>
    bool Retrieved,
    /// <summary>Semantic judgment. Retrieval-only tools emit <c>not_evaluated</c>.</summary>
    string Verdict,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BlockMerkleRoot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? KeyId,
    OccamClaimMatchInfo[] Matches,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptEnvelope? Receipt,
    string Timestamp,
    // Provable absence: when found=false AND the receipt attests a complete leaf set
    // (leafSetComplete), proven=true means "the extracted content provably does NOT contain matching
    // text" — a grounded 'no', not a silent miss. Omitted when found=true or completeness is unknown.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? Proven = null);

public sealed record OccamClaimCheckFailureInfo(string Code, string Message);

public sealed record OccamClaimCheckFailureResponse(
    bool Ok,
    string Url,
    string Claim,
    OccamClaimCheckFailureInfo Failure,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptEnvelope? Receipt,
    string Timestamp);

[JsonSerializable(typeof(OccamClaimCheckSuccessResponse))]
[JsonSerializable(typeof(OccamClaimCheckFailureResponse))]
[JsonSerializable(typeof(OccamClaimMatchInfo))]
[JsonSerializable(typeof(OccamClaimMatchInfo[]))]
[JsonSerializable(typeof(MerkleProofStep))]
[JsonSerializable(typeof(MerkleProofStep[]))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamClaimCheckJsonContext : JsonSerializerContext;
