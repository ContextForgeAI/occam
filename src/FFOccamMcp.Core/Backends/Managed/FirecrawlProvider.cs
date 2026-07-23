using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends.Managed;

/// <summary>
/// Firecrawl (https://api.firecrawl.dev/v1/scrape). POST + Bearer API key, returns
/// { success, data: { markdown } }. Default base: https://api.firecrawl.dev.
/// </summary>
public sealed class FirecrawlProvider : IManagedProvider
{
    public string Name => "firecrawl";
    public bool RequiresApiKey => true;

    public ExtractRunResult Fetch(HttpClient client, string url, string? apiKey, string? baseUrl, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.firecrawl.dev" : baseUrl.TrimEnd('/');
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, $"{root}/v1/scrape")
            {
                Content = JsonContent.Create(
                    new FirecrawlScrapeRequest { Url = url, Formats = ["markdown"] },
                    FirecrawlJsonContext.Default.FirecrawlScrapeRequest),
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
            var payload = JsonSerializer.Deserialize(stream, FirecrawlJsonContext.Default.FirecrawlScrapeResponse);
            var markdown = payload?.Data?.Markdown;
            return ManagedResults.FromMarkdown(Name, markdown, url, elapsed);
        }
        catch (Exception ex)
        {
            return ManagedResults.Exception(Name, ex, ManagedElapsed.Ms(started));
        }
    }
}

internal sealed class FirecrawlScrapeRequest
{
    [JsonPropertyName("url")] public string Url { get; init; } = "";
    [JsonPropertyName("formats")] public string[] Formats { get; init; } = [];
}

internal sealed class FirecrawlScrapeResponse
{
    [JsonPropertyName("success")] public bool Success { get; init; }
    [JsonPropertyName("data")] public FirecrawlScrapeData? Data { get; init; }
}

internal sealed class FirecrawlScrapeData
{
    [JsonPropertyName("markdown")] public string? Markdown { get; init; }
}

[JsonSerializable(typeof(FirecrawlScrapeRequest))]
[JsonSerializable(typeof(FirecrawlScrapeResponse))]
internal partial class FirecrawlJsonContext : JsonSerializerContext;
