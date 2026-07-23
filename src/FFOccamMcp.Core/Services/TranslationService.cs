using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Services;

/// <summary>
/// Optional markdown translation via a self-hosted/managed LibreTranslate endpoint.
/// Off unless <c>OCCAM_TRANSLATE_URL</c> is configured. Failures are non-fatal: the caller keeps
/// the original markdown and surfaces a warning (translation is a convenience codec, not the
/// extraction contract).
/// </summary>
public interface ITranslationService
{
    /// <summary>Configured (endpoint present) — cheap check before attempting a call.</summary>
    bool IsConfigured { get; }

    /// <summary>
    /// Translates <paramref name="text"/> to <paramref name="targetLang"/>. Returns the translated
    /// text, or null with a <paramref name="warning"/> code when unavailable or on failure.
    /// </summary>
    string? Translate(string text, string targetLang, out string? warning);
}

public sealed class TranslationService(IHttpClientFactory httpClientFactory) : ITranslationService
{
    public const string HttpClientName = "occam.translate";

    private static string? EndpointBase =>
        Environment.GetEnvironmentVariable("OCCAM_TRANSLATE_URL")?.Trim().TrimEnd('/') is { Length: > 0 } url
            ? url
            : null;

    public bool IsConfigured => EndpointBase is not null;

    public string? Translate(string text, string targetLang, out string? warning)
    {
        warning = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var endpoint = EndpointBase;
        if (endpoint is null)
        {
            warning = "translate_endpoint_unconfigured";
            return null;
        }

        try
        {
            var request = new LibreTranslateRequest
            {
                Q = text,
                Source = "auto",
                Target = targetLang,
                Format = "text",
                ApiKey = Environment.GetEnvironmentVariable("OCCAM_TRANSLATE_API_KEY"),
            };

            using var client = httpClientFactory.CreateClient(HttpClientName);
            using var content = JsonContent.Create(request, TranslationJsonContext.Default.LibreTranslateRequest);
            using var response = client.PostAsync($"{endpoint}/translate", content).GetAwaiter().GetResult();
            if (!response.IsSuccessStatusCode)
            {
                warning = $"translate_http_{(int)response.StatusCode}";
                return null;
            }

            using var stream = response.Content.ReadAsStream();
            var payload = JsonSerializer.Deserialize(stream, TranslationJsonContext.Default.LibreTranslateResponse);
            if (string.IsNullOrWhiteSpace(payload?.TranslatedText))
            {
                warning = "translate_empty_response";
                return null;
            }

            return payload.TranslatedText;
        }
        catch (Exception ex)
        {
            warning = ex is TaskCanceledException ? "translate_timeout" : "translate_error";
            return null;
        }
    }
}

internal sealed class LibreTranslateRequest
{
    [JsonPropertyName("q")] public string Q { get; init; } = "";
    [JsonPropertyName("source")] public string Source { get; init; } = "auto";
    [JsonPropertyName("target")] public string Target { get; init; } = "";
    [JsonPropertyName("format")] public string Format { get; init; } = "text";
    [JsonPropertyName("api_key")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? ApiKey { get; init; }
}

internal sealed class LibreTranslateResponse
{
    [JsonPropertyName("translatedText")] public string? TranslatedText { get; init; }
}

[JsonSerializable(typeof(LibreTranslateRequest))]
[JsonSerializable(typeof(LibreTranslateResponse))]
internal partial class TranslationJsonContext : JsonSerializerContext;
