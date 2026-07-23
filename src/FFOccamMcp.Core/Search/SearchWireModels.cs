using System.Text.Json.Serialization;

namespace OccamMcp.Core.Search;

// --- SearXNG: GET {base}/search?q=..&format=json -> { results: [{ url, title, content }] }
internal sealed class SearxngResponse
{
    [JsonPropertyName("results")] public SearxngResult[]? Results { get; init; }
}

internal sealed class SearxngResult
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("content")] public string? Content { get; init; }
}

// --- Brave: GET /res/v1/web/search?q=.. -> { web: { results: [{ title, url, description }] } }
internal sealed class BraveResponse
{
    [JsonPropertyName("web")] public BraveWeb? Web { get; init; }
}

internal sealed class BraveWeb
{
    [JsonPropertyName("results")] public BraveResult[]? Results { get; init; }
}

internal sealed class BraveResult
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("description")] public string? Description { get; init; }
}

// --- Tavily: POST /search { query, max_results } -> { results: [{ title, url, content }] }
internal sealed class TavilyRequest
{
    [JsonPropertyName("query")] public string Query { get; init; } = "";
    [JsonPropertyName("max_results")] public int MaxResults { get; init; }
}

internal sealed class TavilyResponse
{
    [JsonPropertyName("results")] public TavilyResult[]? Results { get; init; }
}

internal sealed class TavilyResult
{
    [JsonPropertyName("title")] public string? Title { get; init; }
    [JsonPropertyName("url")] public string? Url { get; init; }
    [JsonPropertyName("content")] public string? Content { get; init; }
}

[JsonSerializable(typeof(SearxngResponse))]
[JsonSerializable(typeof(BraveResponse))]
[JsonSerializable(typeof(TavilyRequest))]
[JsonSerializable(typeof(TavilyResponse))]
internal partial class SearchJsonContext : JsonSerializerContext;
