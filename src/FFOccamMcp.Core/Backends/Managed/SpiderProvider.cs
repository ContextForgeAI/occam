using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends.Managed;

/// <summary>
/// Spider (https://api.spider.cloud/crawl). POST + Bearer API key, body
/// { url, limit: 1, return_format: "markdown" }; returns an array of page objects whose
/// <c>content</c> is the markdown. Default base: https://api.spider.cloud.
/// </summary>
public sealed class SpiderProvider : IManagedProvider
{
    public string Name => "spider";
    public bool RequiresApiKey => true;

    public ExtractRunResult Fetch(HttpClient client, string url, string? apiKey, string? baseUrl, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.spider.cloud" : baseUrl.TrimEnd('/');
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{root}/crawl")
            {
                Content = JsonContent.Create(
                    new SpiderScrapeRequest { Url = url, Limit = 1, ReturnFormat = "markdown" },
                    SpiderJsonContext.Default.SpiderScrapeRequest),
            };
            if (!string.IsNullOrWhiteSpace(apiKey))
            {
                request.Headers.TryAddWithoutValidation("Authorization", $"Bearer {apiKey}");
            }

            using var response = client.Send(request, cancellationToken);
            var elapsed = ManagedElapsed.Ms(started);
            if (!response.IsSuccessStatusCode)
            {
                return ManagedResults.Failure(Name, (int)response.StatusCode, elapsed);
            }

            using var stream = response.Content.ReadAsStream(cancellationToken);
            var pages = JsonSerializer.Deserialize(stream, SpiderJsonContext.Default.SpiderScrapePageArray);
            var markdown = pages is { Length: > 0 } ? pages[0].Content : null;
            return ManagedResults.FromMarkdown(Name, markdown, url, elapsed);
        }
        catch (Exception ex)
        {
            return ManagedResults.Exception(Name, ex, ManagedElapsed.Ms(started));
        }
    }
}

internal sealed class SpiderScrapeRequest
{
    [JsonPropertyName("url")] public string Url { get; init; } = "";
    [JsonPropertyName("limit")] public int Limit { get; init; } = 1;
    [JsonPropertyName("return_format")] public string ReturnFormat { get; init; } = "markdown";
}

internal sealed class SpiderScrapePage
{
    [JsonPropertyName("content")] public string? Content { get; init; }
}

[JsonSerializable(typeof(SpiderScrapeRequest))]
[JsonSerializable(typeof(SpiderScrapePage[]))]
internal partial class SpiderJsonContext : JsonSerializerContext;
