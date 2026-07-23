using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Server;
using OccamMcp.Core.Client;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Autonomous client handshake: the LLM declares its context window (MCP does not expose it),
/// Occam stores an output budget and applies it to later reads that omit <c>max_tokens</c>.
/// </summary>
[McpServerToolType]
public sealed class OccamClientCapabilitiesTool(ClientCapabilityStore store)
{
    [McpServerTool(Name = "occam_client_capabilities"), Description(
        "Declare this LLM's context window so Occam sizes extracts to what you can hold. " +
        "MCP hosts do not tell servers your context size — call this once at session start with " +
        "context_tokens (you know it from your model card / host settings). " +
        "Afterwards, occam_transcode/occam_digest without max_tokens use ~20% of that window " +
        "(clamped 512–16384). Omit args to read the current budget; clear=true resets.")]
    public string ClientCapabilities(
        [Description("Your context window in tokens (e.g. 8192, 128000). Required to configure; omit to inspect.")]
        int? context_tokens = null,
        [Description("Optional model id string for the response (e.g. composer-2, gpt-5).")]
        string? model_id = null,
        [Description("If true, clear the session override and re-read OCCAM_CLIENT_CONTEXT_TOKENS env.")]
        bool clear = false,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            if (clear)
            {
                var cleared = store.Clear();
                return Serialize(Map(cleared, note: "cleared; env bootstrap re-applied if set"));
            }

            if (context_tokens is int tokens)
            {
                var applied = store.Configure(tokens, model_id, source: "tool");
                return Serialize(Map(
                    applied,
                    note:
                    "budget applied — subsequent occam_transcode/occam_digest without max_tokens use outputBudgetTokens"));
            }

            return Serialize(Map(store.Current, note: store.Current.Configured
                ? "budget already configured"
                : "not configured — pass context_tokens once (or set OCCAM_CLIENT_CONTEXT_TOKENS)"));
        }
        catch (ArgumentOutOfRangeException ex)
        {
            return Serialize(new OccamClientCapabilitiesResponse(
                Ok: false,
                Configured: false,
                ContextTokens: null,
                OutputBudgetTokens: null,
                SuggestedProfile: null,
                ModelId: null,
                Source: null,
                Note: ex.Message,
                FailureCode: "invalid_arguments"));
        }
    }

    private static OccamClientCapabilitiesResponse Map(ClientCapabilitySnapshot snap, string note) =>
        new(
            Ok: true,
            Configured: snap.Configured,
            ContextTokens: snap.ContextTokens,
            OutputBudgetTokens: snap.OutputBudgetTokens,
            SuggestedProfile: snap.SuggestedProfile,
            ModelId: snap.ModelId,
            Source: snap.Source,
            Note: note,
            FailureCode: null);

    private static string Serialize(OccamClientCapabilitiesResponse response) =>
        JsonSerializer.Serialize(response, OccamClientCapabilitiesJsonContext.Default.OccamClientCapabilitiesResponse);
}

public sealed record OccamClientCapabilitiesResponse(
    bool Ok,
    bool Configured,
    int? ContextTokens,
    int? OutputBudgetTokens,
    string? SuggestedProfile,
    string? ModelId,
    string? Source,
    string? Note,
    string? FailureCode);

[JsonSerializable(typeof(OccamClientCapabilitiesResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamClientCapabilitiesJsonContext : JsonSerializerContext;
