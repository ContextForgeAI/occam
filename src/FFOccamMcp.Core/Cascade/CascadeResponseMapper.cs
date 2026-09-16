using System.Text.Json;
using System.Text.Json.Serialization;

namespace OccamMcp.Core.Cascade;

/// <summary>Wire DTO for one cascade step (camelCase).</summary>
public sealed record CascadeStepDto(
    [property: JsonPropertyName("kind")] string Kind,
    [property: JsonPropertyName("status")] string Status,
    [property: JsonPropertyName("durationMs")] int DurationMs,
    [property: JsonPropertyName("detail")] string? Detail);

/// <summary>Public cascade JSON envelope.</summary>
public sealed record CascadeResponseDto(
    [property: JsonPropertyName("ok")] bool Ok,
    [property: JsonPropertyName("partial")] bool Partial,
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("markdown")] string? Markdown,
    [property: JsonPropertyName("failure")] CascadeFailureDto? Failure,
    [property: JsonPropertyName("playbookId")] string? PlaybookId,
    [property: JsonPropertyName("backend")] string? Backend,
    [property: JsonPropertyName("maxTokens")] int? MaxTokens,
    [property: JsonPropertyName("focusQuery")] string? FocusQuery,
    [property: JsonPropertyName("contentHash")] string? ContentHash,
    [property: JsonPropertyName("mode")] string Mode,
    [property: JsonPropertyName("steps")] CascadeStepDto[] Steps,
    [property: JsonPropertyName("omitted")] string[] Omitted,
    [property: JsonPropertyName("hint")] string? Hint = null);

public sealed record CascadeFailureDto(
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message);

[JsonSerializable(typeof(CascadeResponseDto))]
[JsonSerializable(typeof(CascadeStepDto))]
[JsonSerializable(typeof(CascadeFailureDto))]
[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
internal partial class CascadeJsonContext : JsonSerializerContext;

/// <summary>Maps <see cref="CascadeResult"/> to the public JSON wire shape.</summary>
public static class CascadeResponseMapper
{
    public static CascadeResponseDto FromResult(CascadeResult result)
    {
        CascadeFailureDto? failure = null;
        if (!result.Ok)
        {
            failure = new CascadeFailureDto(
                result.FailureCode ?? "extraction_failed",
                result.FailureMessage ?? "cascade failed.");
        }

        string? hint = null;
        if (string.Equals(result.Mode, "advanced", StringComparison.Ordinal))
        {
            hint = "mode=advanced: specialised tools (occam_transcode opt-ins, playbook_heal, …) remain on wider OCCAM_PROFILE surfaces.";
        }

        var steps = new CascadeStepDto[result.Steps.Count];
        for (var i = 0; i < result.Steps.Count; i++)
        {
            var s = result.Steps[i];
            steps[i] = new CascadeStepDto(
                CascadeStepKindStrings.Format(s.Kind),
                CascadeStepStatusStrings.Format(s.Status),
                s.DurationMs,
                s.Detail);
        }

        var omitted = new string[result.Omitted.Count];
        for (var i = 0; i < result.Omitted.Count; i++)
        {
            omitted[i] = result.Omitted[i];
        }

        return new CascadeResponseDto(
            result.Ok,
            result.Partial,
            result.Url,
            result.Markdown,
            failure,
            result.PlaybookId,
            result.BackendUsed,
            result.MaxTokensApplied,
            result.FocusQueryApplied,
            result.ContentHash,
            result.Mode,
            steps,
            omitted,
            hint);
    }

    public static string Serialize(CascadeResult result) =>
        JsonSerializer.Serialize(FromResult(result), CascadeJsonContext.Default.CascadeResponseDto);
}
