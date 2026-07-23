using System.Text.Json;
using System.Text.Json.Serialization;
using OccamMcp.Core.Agent;
using OccamMcp.Core.Playbooks;

namespace OccamMcp.Core.Tools;

public sealed record OccamPlaybookResolveSuccessResponse(
    bool Ok,
    string Url,
    string MatchedHost,
    string PlaybookId,
    string SchemaVersion,
    string Provenance,
    string SourcePath,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string[]? ContentSelectors,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PreferredBackend,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? AgentNotes,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Genome,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? KnowledgeSchema,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? PageClass,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamGenomeFetchInfo? GenomeFetch,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    JsonElement? Lessons,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? SchemaVersionWarning,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamPlaybookSignatureInfo? Signature,
    string Timestamp);

// SI-08 consumer loop: trust signal for the resolved recipe. status ∈ unsigned|verified|invalid|unknown_key.
public sealed record OccamPlaybookSignatureInfo(
    bool Present,
    string Status,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? KeyId,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    int? Score,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    bool? PassesGate);

public sealed record OccamGenomeFetchInfo(
    bool Ok,
    string WellKnownUrl,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    string? FailureCode,
    bool CacheHit = false);

public sealed record OccamPlaybookResolveFailureResponse(
    bool Ok,
    string Url,
    string FailureCode,
    string Message,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    OccamResolveFailureAgentHintsInfo? AgentHints,
    string Timestamp);

public sealed record OccamResolveFailureAgentHintsInfo(ProbeDecision[] Decisions);

internal static class OccamPlaybookResolveResponseMapper
{
    public static OccamPlaybookResolveSuccessResponse MapSuccess(
        PlaybookSeedResolveResult result,
        OccamPlaybookSignatureInfo? signature = null) =>
        new(
            Ok: true,
            Url: result.Requested,
            MatchedHost: result.MatchedHost!,
            PlaybookId: result.PlaybookId!,
            SchemaVersion: result.SchemaVersion!,
            Provenance: result.Provenance!,
            SourcePath: result.SourcePath!,
            ContentSelectors: result.ContentSelectors,
            PreferredBackend: result.PreferredBackend,
            AgentNotes: result.AgentNotes,
            Genome: result.Genome,
            KnowledgeSchema: result.KnowledgeSchema,
            PageClass: result.PageClass,
            GenomeFetch: result.GenomeFetch is null
                ? null
                : new OccamGenomeFetchInfo(
                    result.GenomeFetch.Ok,
                    result.GenomeFetch.WellKnownUrl,
                    result.GenomeFetch.FailureCode,
                    result.GenomeFetch.CacheHit),
            Lessons: result.Lessons,
            SchemaVersionWarning: result.SchemaVersionWarning,
            Signature: signature,
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));

    public static OccamPlaybookResolveFailureResponse MapFailure(PlaybookSeedResolveResult result)
    {
        var code = result.FailureCode!;
        var hints = FailureAgentHints.ForCode(code);
        return new(
            Ok: false,
            Url: result.Requested,
            FailureCode: code,
            Message: result.Message!,
            AgentHints: hints is null ? null : new OccamResolveFailureAgentHintsInfo(hints.Decisions),
            Timestamp: DateTimeOffset.UtcNow.ToString("O"));
    }
}

[JsonSerializable(typeof(OccamPlaybookResolveSuccessResponse))]
[JsonSerializable(typeof(OccamPlaybookSignatureInfo))]
[JsonSerializable(typeof(OccamPlaybookResolveFailureResponse))]
[JsonSerializable(typeof(OccamResolveFailureAgentHintsInfo))]
[JsonSerializable(typeof(ProbeDecision))]
[JsonSerializable(typeof(ProbeDecision[]))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamPlaybookResolveJsonContext : JsonSerializerContext;
