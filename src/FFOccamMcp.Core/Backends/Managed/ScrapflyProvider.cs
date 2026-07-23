using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends.Managed;

/// <summary>
/// Scrapfly (https://api.scrapfly.io/scrape). GET with the API key + target URL as query params;
/// <c>format=markdown</c> returns markdown in <c>result.content</c>, <c>render_js=true</c> drives a
/// real browser for JS-heavy pages. Default base: https://api.scrapfly.io.
/// </summary>
public sealed class ScrapflyProvider : IManagedProvider
{
    public string Name => "scrapfly";
    public bool RequiresApiKey => true;

    public ExtractRunResult Fetch(HttpClient client, string url, string? apiKey, string? baseUrl, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://api.scrapfly.io" : baseUrl.TrimEnd('/');
        try
        {
            var endpoint =
                $"{root}/scrape?key={Uri.EscapeDataString(apiKey ?? string.Empty)}"
                + $"&url={Uri.EscapeDataString(url)}&format=markdown&render_js=true";
            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            using var response = client.Send(request, cancellationToken);
            var elapsed = ManagedElapsed.Ms(started);
            if (!response.IsSuccessStatusCode)
            {
                return ManagedResults.Failure(Name, (int)response.StatusCode, elapsed);
            }

            using var stream = response.Content.ReadAsStream(cancellationToken);
            var payload = JsonSerializer.Deserialize(stream, ScrapflyJsonContext.Default.ScrapflyScrapeResponse);
            var markdown = payload?.Result?.Content;
            return ManagedResults.FromMarkdown(Name, markdown, url, elapsed);
        }
        catch (Exception ex)
        {
            return ManagedResults.Exception(Name, ex, ManagedElapsed.Ms(started));
        }
    }
}

internal sealed class ScrapflyScrapeResponse
{
    [JsonPropertyName("result")] public ScrapflyScrapeResult? Result { get; init; }
}

internal sealed class ScrapflyScrapeResult
{
    [JsonPropertyName("content")] public string? Content { get; init; }
}

[JsonSerializable(typeof(ScrapflyScrapeResponse))]
internal partial class ScrapflyJsonContext : JsonSerializerContext;
