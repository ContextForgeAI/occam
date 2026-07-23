using System.Text.Json.Serialization;
using OccamMcp.Core.Tools;

namespace OccamMcp.Core.Watch;

/// <summary>Persisted last-seen state for one watched URL.</summary>
public sealed record WatchRecord
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("content_hash")]
    public string ContentHash { get; init; } = "";

    [JsonPropertyName("block_hashes")]
    public string[] BlockHashes { get; init; } = [];

    [JsonPropertyName("first_seen_at")]
    public DateTimeOffset FirstSeenAt { get; init; }

    [JsonPropertyName("last_seen_at")]
    public DateTimeOffset LastSeenAt { get; init; }

    [JsonPropertyName("last_changed_at")]
    public DateTimeOffset? LastChangedAt { get; init; }

    // SI-05: append-only signed chain of change events (capped window). Absent on legacy stores → [].
    [JsonPropertyName("history")]
    public WatchHistoryEntry[] History { get; init; } = [];
}

/// <summary>On-disk container for the watch store.</summary>
public sealed record WatchStoreFile
{
    [JsonPropertyName("records")]
    public WatchRecord[] Records { get; init; } = [];
}

public sealed record OccamWatchSuccessResponse(
    bool Ok,
    string Url,
    bool FirstSeen,
    bool Changed,
    string ContentHash,
    int BlockCount,
    DateTimeOffset FirstSeenAt,
    DateTimeOffset LastSeenAt,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    DateTimeOffset? LastChangedAt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamTranscodeDiffInfo? Diff = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Backend = null,
    // Freshness magnitude: approximate token count of newly-added blocks since last seen
    // (0 when unchanged, omitted on first-seen). Lets an agent gauge how much actually changed.
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? ContentDeltaTokens = null,
    // SI-05: length of the retained signed change-chain (always present).
    int HistoryLength = 0,
    // SI-05: the entry just appended (only when an event occurred this call).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WatchHistoryEntry? LatestEntry = null,
    // SI-05: full chain — only when include_history=true (keeps the common response lean).
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    WatchHistoryEntry[]? History = null);

public sealed record OccamWatchFailureInfo(string Code, string Message);

public sealed record OccamWatchFailureResponse(
    bool Ok,
    string Url,
    OccamWatchFailureInfo Failure);

[JsonSerializable(typeof(WatchStoreFile))]
[JsonSerializable(typeof(WatchRecord))]
[JsonSerializable(typeof(WatchHistoryEntry))]
[JsonSerializable(typeof(WatchHistoryEntry[]))]
[JsonSerializable(typeof(OccamWatchSuccessResponse))]
[JsonSerializable(typeof(OccamWatchFailureResponse))]
[JsonSerializable(typeof(OccamTranscodeDiffInfo))]
[JsonSerializable(typeof(OccamTranscodeDiffBlockInfo))]
[JsonSerializable(typeof(OccamTranscodeDiffBlockInfo[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamWatchJsonContext : JsonSerializerContext;
