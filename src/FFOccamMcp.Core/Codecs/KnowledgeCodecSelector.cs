namespace OccamMcp.Core.Codecs;

/// <summary>
/// Deterministic, fail-closed codec selection (master PR-E).
///
/// <para>Rules (in order):</para>
/// <list type="number">
/// <item>Unknown capability profile → <see cref="KnowledgeCodecFailureCodes.UnknownCapabilityProfile"/>.</item>
/// <item>Null/whitespace requested id → configured registry default (<see cref="KnowledgeCodecRegistry.DefaultCodecId"/>).</item>
/// <item>Unknown id → <see cref="KnowledgeCodecFailureCodes.UnsupportedCodec"/> (no silent fallback).</item>
/// <item>Registered but <c>CanEncode=false</c> → <see cref="KnowledgeCodecFailureCodes.CodecCannotEncode"/>.</item>
/// <item><see cref="KnowledgeCodecTrust.OptInExtension"/> without allow →
/// <see cref="KnowledgeCodecFailureCodes.CodecExtensionNotAllowed"/>.</item>
/// <item>Otherwise select the requested codec. BuiltinExperimental is allowed by explicit id only —
/// never as an implicit default.</item>
/// </list>
/// </summary>
public static class KnowledgeCodecSelector
{
    /// <summary>Known capability profile tokens for v1. Empty/null/"default" are accepted.</summary>
    public static readonly HashSet<string> KnownCapabilityProfiles = new(StringComparer.OrdinalIgnoreCase)
    {
        "default",
    };

    public static KnowledgeCodecSelection Select(
        KnowledgeCodecRegistry registry,
        string? requestedCodecId,
        KnowledgeCodecSelectOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(registry);
        options ??= KnowledgeCodecSelectOptions.Default;

        if (!IsKnownCapabilityProfile(options.CapabilityProfile))
        {
            return Fail(
                KnowledgeCodecSelectStatus.UnknownCapabilityProfile,
                KnowledgeCodecFailureCodes.UnknownCapabilityProfile,
                requestedCodecId,
                registry.Default.Descriptor.CodecId);
        }

        if (string.IsNullOrWhiteSpace(requestedCodecId))
        {
            var def = registry.Default;
            return new KnowledgeCodecSelection(
                KnowledgeCodecSelectStatus.Selected,
                def,
                RequestedId: null,
                SelectedId: def.Descriptor.CodecId,
                FailureCode: null);
        }

        var id = requestedCodecId.Trim();
        if (!registry.TryGet(id, out var codec) || codec is null)
        {
            return Fail(
                KnowledgeCodecSelectStatus.UnsupportedCodec,
                KnowledgeCodecFailureCodes.UnsupportedCodec,
                id,
                registry.Default.Descriptor.CodecId);
        }

        if (!codec.Descriptor.CanEncode)
        {
            return Fail(
                KnowledgeCodecSelectStatus.CodecCannotEncode,
                KnowledgeCodecFailureCodes.CodecCannotEncode,
                id,
                registry.Default.Descriptor.CodecId);
        }

        if (codec.Descriptor.Trust == KnowledgeCodecTrust.OptInExtension)
        {
            // Registration is the primary opt-in gate. Selection still refuses extensions when the
            // registry policy is disabled (e.g. policy flipped) or when an allow-list excludes the id.
            if (!registry.ExtensionOptions.AllowOptInExtensions
                || (registry.ExtensionOptions.AllowedExtensionIds is { Count: > 0 }
                    && !registry.ExtensionOptions.AllowedExtensionIds.Contains(id)))
            {
                return Fail(
                    KnowledgeCodecSelectStatus.ExtensionNotAllowed,
                    KnowledgeCodecFailureCodes.CodecExtensionNotAllowed,
                    id,
                    registry.Default.Descriptor.CodecId);
            }
        }

        return new KnowledgeCodecSelection(
            KnowledgeCodecSelectStatus.Selected,
            codec,
            RequestedId: id,
            SelectedId: codec.Descriptor.CodecId,
            FailureCode: null);
    }

    private static bool IsKnownCapabilityProfile(string? profile)
    {
        if (string.IsNullOrWhiteSpace(profile))
        {
            return true;
        }

        return KnownCapabilityProfiles.Contains(profile.Trim());
    }

    private static KnowledgeCodecSelection Fail(
        KnowledgeCodecSelectStatus status,
        string failureCode,
        string? requestedId,
        string defaultId) =>
        new(status, Codec: null, RequestedId: requestedId, SelectedId: defaultId, FailureCode: failureCode);
}

public enum KnowledgeCodecSelectStatus
{
    Selected = 0,
    UnsupportedCodec = 1,
    ExtensionNotAllowed = 2,
    UnknownCapabilityProfile = 3,
    CodecCannotEncode = 4,
}

/// <summary>
/// Result of <see cref="KnowledgeCodecSelector.Select"/>. On failure <see cref="Codec"/> is null —
/// callers must not silently substitute the default (except when the request itself asked for default).
/// <see cref="SelectedId"/> still names the would-be default for diagnostics.
/// </summary>
public sealed record KnowledgeCodecSelection(
    KnowledgeCodecSelectStatus Status,
    IKnowledgeCodec? Codec,
    string? RequestedId,
    string SelectedId,
    string? FailureCode)
{
    public bool Ok => Status == KnowledgeCodecSelectStatus.Selected && Codec is not null;
}

/// <summary>Per-call selection options. Capability profiles are a seed for the future passport.</summary>
public sealed record KnowledgeCodecSelectOptions(string? CapabilityProfile = null)
{
    public static readonly KnowledgeCodecSelectOptions Default = new();
}
