using System.ComponentModel;
using System.Text.Json;
using System.Text.Json.Serialization;
using ModelContextProtocol.Protocol;
using ModelContextProtocol.Server;
using OccamMcp.Core.Client;
using OccamMcp.Core.Exam;
using OccamMcp.Core.Transport;

namespace OccamMcp.Core.Tools;

/// <summary>
/// Autonomous client handshake: context budget + runtime <c>OCCAM_PROFILE</c> surface switch
/// (sends <c>notifications/tools/list_changed</c> — no MCP host restart).
/// </summary>
[McpServerToolType]
public sealed class OccamClientCapabilitiesTool(ClientCapabilityStore store, SessionToolSurface surface)
{
    [McpServerTool(Name = "occam_client_capabilities"), Description(
        "Declare context window and/or switch live tool surface. Pass context_tokens once; " +
        "profile=reader|researcher|auditor|full widens tools without MCP restart (list_changed — re-list). " +
        "Locked if OCCAM_PROFILE_LOCK=1. Omit args to inspect; clear=true resets budget.")]
    public async Task<string> ClientCapabilities(
        [Description("Context window tokens (e.g. 128000). Omit to inspect.")]
        int? context_tokens = null,
        [Description("Optional model id label.")]
        string? model_id = null,
        [Description("Clear session budget; re-read OCCAM_CLIENT_CONTEXT_TOKENS.")]
        bool clear = false,
        [Description("Live surface: reader|researcher|auditor|full (list_changed).")]
        string? profile = null,
        McpServer? server = null,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            string? note = null;
            var listChangedSent = false;
            var profileApplied = false;
            var profileRequested = !string.IsNullOrWhiteSpace(profile);

            if (clear)
            {
                _ = store.Clear();
                note = "cleared; env bootstrap re-applied if set";
            }

            if (context_tokens is int tokens)
            {
                _ = store.Configure(tokens, model_id, source: "tool");
                note = note is null
                    ? "budget applied — subsequent occam_transcode/occam_digest without max_tokens use outputBudgetTokens"
                    : note + "; budget applied";
            }

            if (profileRequested)
            {
                if (!OccamToolProfile.TryNormalize(profile, out var normalized))
                {
                    return Serialize(new OccamClientCapabilitiesResponse(
                        Ok: false,
                        Configured: store.Current.Configured,
                        ContextTokens: store.Current.ContextTokens,
                        OutputBudgetTokens: store.Current.OutputBudgetTokens,
                        SuggestedProfile: store.Current.SuggestedProfile,
                        ModelId: store.Current.ModelId,
                        Source: store.Current.Source,
                        Note: "profile must be minimal|basic|reader|researcher|auditor|full",
                        FailureCode: "invalid_arguments",
                        ActiveProfile: surface.ProfileId,
                        ProfileApplied: false,
                        ProfileLocked: surface.IsPinned,
                        ListChangedSent: false));
                }

                if (surface.IsPinned)
                {
                    note = AppendNote(note,
                        $"profile locked (OCCAM_PROFILE_LOCK=1); active stays '{surface.ProfileId}'");
                }
                else if (string.Equals(surface.ProfileId, normalized, StringComparison.Ordinal))
                {
                    note = AppendNote(note, $"profile already '{normalized}'");
                }
                else if (surface.TryApplyProfile(normalized))
                {
                    profileApplied = true;
                    note = AppendNote(note,
                        $"profile → '{normalized}' ({OccamToolProfile.GetExposedToolNames(normalized).Length} tools); re-list after list_changed");
                    if (server is not null)
                    {
                        try
                        {
                            await server.SendNotificationAsync(
                                    NotificationMethods.ToolListChangedNotification,
                                    cancellationToken)
                                .ConfigureAwait(false);
                            listChangedSent = true;
                        }
                        catch (Exception ex) when (ex is not OperationCanceledException)
                        {
                            Console.Error.WriteLine($"[occam.profile] list_changed notify failed: {ex.Message}");
                        }
                    }
                }
            }

            if (note is null)
            {
                note = store.Current.Configured
                    ? "budget already configured"
                    : "not configured — pass context_tokens once (or set OCCAM_CLIENT_CONTEXT_TOKENS); optional profile=… switches tools live";
            }

            return Serialize(Map(store.Current, note, profileApplied, listChangedSent));
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
                FailureCode: "invalid_arguments",
                ActiveProfile: surface.ProfileId,
                ProfileApplied: false,
                ProfileLocked: surface.IsPinned,
                ListChangedSent: false));
        }
    }

    private OccamClientCapabilitiesResponse Map(
        ClientCapabilitySnapshot snap,
        string note,
        bool profileApplied,
        bool listChangedSent) =>
        new(
            Ok: true,
            Configured: snap.Configured,
            ContextTokens: snap.ContextTokens,
            OutputBudgetTokens: snap.OutputBudgetTokens,
            SuggestedProfile: snap.SuggestedProfile,
            ModelId: snap.ModelId,
            Source: snap.Source,
            Note: note,
            FailureCode: null,
            ActiveProfile: surface.ProfileId,
            ProfileApplied: profileApplied,
            ProfileLocked: surface.IsPinned,
            ListChangedSent: listChangedSent);

    private static string AppendNote(string? prior, string next) =>
        string.IsNullOrEmpty(prior) ? next : prior + "; " + next;

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
    string? FailureCode,
    string? ActiveProfile = null,
    bool ProfileApplied = false,
    bool ProfileLocked = false,
    bool ListChangedSent = false);

[JsonSerializable(typeof(OccamClientCapabilitiesResponse))]
[JsonSourceGenerationOptions(PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase)]
internal partial class OccamClientCapabilitiesJsonContext : JsonSerializerContext;
