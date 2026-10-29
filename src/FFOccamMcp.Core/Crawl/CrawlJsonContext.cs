using System.Text.Json.Serialization;

namespace OccamMcp.Core.Crawl;

[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase, DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
[JsonSerializable(typeof(CrawlOutcome))]
[JsonSerializable(typeof(CrawlPageResult))]
[JsonSerializable(typeof(CrawlResumeState))]
[JsonSerializable(typeof(CrawlErrorResponse))]
internal partial class CrawlJsonContext : JsonSerializerContext;

public sealed record CrawlErrorResponse(CrawlFailureInfo Failure);

public sealed record CrawlFailureInfo(string Code, string Message);
