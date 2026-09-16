using System.Text.Json.Serialization;

namespace OccamMcp.Core.Tools;

public sealed record OccamSearchResultInfo(
    string Title,
    string Url,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Snippet,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    double? Extractability = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? RecommendedBackend = null,
    /// <summary>Latest-search shorthand <c>S1</c>…<c>Sn</c> after ranking. Pass <see cref="Handle"/> or <see cref="Url"/> to fetch tools.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    [property: JsonPropertyOrder(-1)]
    string? Id = null,
    /// <summary>Process-local durable handle <c>Hxxxxxxxx</c>; survives later searches until TTL/LRU.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? Handle = null);

public sealed record OccamSearchSuccessResponse(
    bool Ok,
    string Query,
    string Provider,
    int Count,
    OccamSearchResultInfo[] Results,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamSearchAgentHintsInfo? AgentHints = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? HandleTtlS = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? HandleScope = null,
    /// <summary>When <see cref="Provider"/> is <c>fanout</c>, backends that returned ok hits.</summary>
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? ProvidersUsed = null);

public sealed record OccamSearchAgentHintsInfo(string SuggestedNext);

public sealed record OccamSearchFailureInfo(string Code, string Message);

public sealed record OccamSearchFailureResponse(
    bool Ok,
    string Query,
    OccamSearchFailureInfo Failure);

[JsonSerializable(typeof(OccamSearchSuccessResponse))]
[JsonSerializable(typeof(OccamSearchFailureResponse))]
[JsonSerializable(typeof(OccamSearchResultInfo))]
[JsonSerializable(typeof(OccamSearchResultInfo[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamSearchJsonContext : JsonSerializerContext;
