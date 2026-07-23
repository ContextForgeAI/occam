using System.Text.Json.Serialization;
using OccamMcp.Core.Receipts;

namespace OccamMcp.Core.Consensus;

/// <summary>
/// SI-14: one witness's observation of a URL (a vantage point). The pure comparison input for
/// <see cref="ConsensusEvaluator"/>. <see cref="IsAccessWall"/> marks a provable wall (captcha /
/// login / paywall / 4xx) — a witness that was denied content, which is itself a cloaking signal.
/// </summary>
public sealed record VantageObservation(
    string Label,
    string Backend,
    bool Ok,
    string? FailureCode,
    bool IsAccessWall,
    string? ContentHash,
    string? BlockMerkleRoot,
    string[]? LeafHashes);

/// <summary>Pairwise comparison of two usable vantages: do the roots match, and how much overlaps.</summary>
public sealed record DivergencePair(
    string A,
    string B,
    bool RootsMatch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? BlocksCommon,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? BlocksTotal);

/// <summary>Pure verdict over a set of vantages (SI-14 consensus core).</summary>
public sealed record ConsensusVerdict(string Verdict, IReadOnlyList<DivergencePair> Pairs);

// --- occam_crosscheck response shapes ---

public sealed record OccamCrosscheckVantageInfo(
    string Label,
    string Backend,
    bool Ok,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailureCode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? ContentHash,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? BlockMerkleRoot,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    ReceiptEnvelope? Receipt);

public sealed record OccamCrosscheckSuccessResponse(
    bool Ok,
    string Url,
    string Verdict,
    OccamCrosscheckVantageInfo[] Vantages,
    DivergencePair[] Divergence,
    string Timestamp);

public sealed record OccamCrosscheckFailureResponse(
    bool Ok,
    string Url,
    string FailureCode,
    string Message,
    string Timestamp);

[JsonSerializable(typeof(OccamCrosscheckSuccessResponse))]
[JsonSerializable(typeof(OccamCrosscheckFailureResponse))]
[JsonSerializable(typeof(OccamCrosscheckVantageInfo))]
[JsonSerializable(typeof(DivergencePair))]
[JsonSerializable(typeof(DivergencePair[]))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamCrosscheckJsonContext : JsonSerializerContext;
