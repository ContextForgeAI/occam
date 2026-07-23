using System.Text.Json.Serialization;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Watch;

namespace OccamMcp.Core.Tools;

/// <summary>Result of <c>occam_verify</c> — the consumer-side half of Receipt v1 (SI-06 / SI-02).</summary>
public sealed record OccamVerifySuccessResponse(
    bool Ok,
    bool SignatureValid,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? ContentHashMatch,
    string KeyId,
    string Mode,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamVerifyLiveInfo? Live,
    string Verdict,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamVerifyHistoryInfo? History = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamVerifyTimeAnchorInfo? TimeAnchor = null);

/// <summary>SI-15 time-anchor check: does the receipt carry a valid independent timestamp, and for when.</summary>
public sealed record OccamVerifyTimeAnchorInfo(
    bool Present,
    bool Valid,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? GenTime,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Tsa,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? TsaSubject);

/// <summary>SI-05 history-mode summary: how much of the signed change-chain checked out.</summary>
public sealed record OccamVerifyHistoryInfo(
    int EntriesTotal,
    int SignedCount,
    int HeadSeq,
    bool ChainValid);

/// <summary>Flexible history input: a bare entries array or an object <c>{ history: [...] }</c> (the watch response).</summary>
public sealed record OccamVerifyHistoryInput(WatchHistoryEntry[]? History);

/// <summary>Live-mode re-fetch comparison (mode = "live"). SI-02 adds the granular block counts.</summary>
public sealed record OccamVerifyLiveInfo(
    bool Refetched,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? ContentHashMatch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? BlockRootMatch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? BlocksTotal = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? BlocksStillPresent = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Drift = null,
    // SI-12: which specific chunks went stale (against the caller's `chunks` set if given, else the
    // receipt's block leaves) — so a RAG store invalidates individual fragments, not whole documents.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamVerifyChunkStalenessInfo? ChunkStaleness = null);

/// <summary>SI-12 chunk-level RAG expiry: per-chunk staleness against the live page.</summary>
public sealed record OccamVerifyChunkStalenessInfo(
    int Total,
    int Present,
    int Stale,
    string[] StaleChunks);

/// <summary>SI-02b prove-mode output: a compact citation package for one block (verifiable without the page).</summary>
public sealed record OccamVerifyProveResponse(
    bool Ok,
    string KeyId,
    string Root,
    int LeafIndex,
    string Leaf,
    MerkleProofStep[] Proof);

public sealed record OccamVerifyFailureResponse(
    bool Ok,
    string FailureCode,
    string Message);

/// <summary>Flexible input: a full transcode <c>receipt</c> object ({signed, blockLeaves, timeAnchor}) or a bare envelope.</summary>
public sealed record OccamVerifyReceiptInput(
    ReceiptEnvelope? Signed,
    string[]? BlockLeaves,
    ReceiptTimeAnchor? TimeAnchor);

[JsonSerializable(typeof(OccamVerifySuccessResponse))]
[JsonSerializable(typeof(OccamVerifyFailureResponse))]
[JsonSerializable(typeof(OccamVerifyProveResponse))]
[JsonSerializable(typeof(OccamVerifyLiveInfo))]
[JsonSerializable(typeof(OccamVerifyChunkStalenessInfo))]
[JsonSerializable(typeof(OccamVerifyHistoryInfo))]
[JsonSerializable(typeof(OccamVerifyHistoryInput))]
[JsonSerializable(typeof(OccamVerifyTimeAnchorInfo))]
[JsonSerializable(typeof(OccamVerifyReceiptInput))]
[JsonSerializable(typeof(ReceiptEnvelope))]
[JsonSerializable(typeof(ReceiptPlaybook))]
[JsonSerializable(typeof(ReceiptTimeAnchor))]
[JsonSerializable(typeof(WatchHistoryEntry))]
[JsonSerializable(typeof(WatchHistoryEntry[]))]
[JsonSerializable(typeof(MerkleProofStep))]
[JsonSerializable(typeof(MerkleProofStep[]))]
[JsonSerializable(typeof(string[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamVerifyJsonContext : System.Text.Json.Serialization.JsonSerializerContext;
