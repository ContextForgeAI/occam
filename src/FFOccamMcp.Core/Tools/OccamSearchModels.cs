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
    string? RecommendedBackend = null);

public sealed record OccamSearchSuccessResponse(
    bool Ok,
    string Query,
    string Provider,
    int Count,
    OccamSearchResultInfo[] Results,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamSearchAgentHintsInfo? AgentHints = null);

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
