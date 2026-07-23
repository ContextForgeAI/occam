using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Backends.Managed;

/// <summary>
/// Jina Reader (https://r.jina.ai/&lt;url&gt;). GET-based, returns markdown directly. API key is
/// optional (raises rate limits). Default base: https://r.jina.ai.
/// </summary>
public sealed class JinaProvider : IManagedProvider
{
    public string Name => "jina";
    public bool RequiresApiKey => false;

    public ExtractRunResult Fetch(HttpClient client, string url, string? apiKey, string? baseUrl, CancellationToken cancellationToken)
    {
        var started = System.Diagnostics.Stopwatch.GetTimestamp();
        var root = string.IsNullOrWhiteSpace(baseUrl) ? "https://r.jina.ai" : baseUrl.TrimEnd('/');
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{root}/{url}");
            request.Headers.TryAddWithoutValidation("Accept", "text/markdown, text/plain");
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
            using var reader = new StreamReader(stream);
            var markdown = reader.ReadToEnd();
            return ManagedResults.FromMarkdown(Name, markdown, url, elapsed);
        }
        catch (Exception ex)
        {
            return ManagedResults.Exception(Name, ex, ManagedElapsed.Ms(started));
        }
    }
}
