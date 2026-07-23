using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Codecs;

/// <summary>
/// ADR-0001 / ADR-0002 (open knowledge-representation layer; planner↔codec separation). A codec
/// transforms a <see cref="MaterializedKnowledgeView"/> into a model-specific surface representation.
/// Encode is required; decode is optional (encode-only codecs are permitted — see
/// <see cref="KnowledgeCodecDescriptor.CanDecode"/>).
///
/// <para><b>Responsibility boundary (ADR-0002).</b> A codec owns ONLY surface encoding: syntax,
/// delimiters, serialization order, escaping, key format, how relations are spelled, media type, and
/// (optionally) the inverse decode. It MUST NOT decide which knowledge matters or what to drop under a
/// token budget — those are the Materialization Planner's semantic decisions, already baked into the
/// view it receives. If a codec did its own selection, every codec would grow a private planner and
/// codec-vs-codec benchmarks would be dishonest.</para>
///
/// <para><b>Extension safety (master PR-E).</b> A codec MUST NOT depend on HTML parser, crawler, MCP
/// transport, playbook storage, browser automation, receipt signing, outbound HTTP, or any acquisition/
/// extraction service. The plugin surface is <see cref="MaterializedKnowledgeView"/> only — never
/// worker payloads or router internals. Third-party codecs are opt-in
/// (<see cref="KnowledgeCodecTrust.OptInExtension"/>); they cannot silently become the default or
/// bypass provenance/trust policy.</para>
/// </summary>
public interface IKnowledgeCodec
{
    KnowledgeCodecDescriptor Descriptor { get; }

    /// <summary>Serializes the already-materialized view to a surface string. No knowledge selection.</summary>
    KnowledgeCodecResult Encode(MaterializedKnowledgeView view, KnowledgeCodecEncodeOptions options);
}

public enum KnowledgeCodecMode
{
    /// <summary>The surface preserves all knowledge in the view (round-trippable in principle).</summary>
    Lossless,

    /// <summary>The surface loses some knowledge in the view during encoding (e.g. a structurally lossy
    /// flattening). NOTE: budget/selection loss is the planner's, not the codec's — this flag is about
    /// the encoding step only.</summary>
    Lossy,
}

/// <summary>
/// Trust / rollout tier for a registered codec (master PR-E). Selection rules refuse to silently
/// promote an extension into the default path.
/// </summary>
public enum KnowledgeCodecTrust
{
    /// <summary>Always-on built-in; <c>markdown-passthrough</c> is the only default.</summary>
    Builtin = 0,

    /// <summary>Shipped with the host but experimental; selectable by explicit id, never auto-default.</summary>
    BuiltinExperimental = 1,

    /// <summary>Third-party / local plugin. Requires <c>AllowOptInExtensions</c>; never auto-default.</summary>
    OptInExtension = 2,
}

/// <summary>
/// Minimal, self-describing codec contract (ADR-0001 §4). Additive: fields default to the most
/// conservative value so a new built-in codec declares only what it supports.
/// </summary>
public sealed record KnowledgeCodecDescriptor(
    string CodecId,
    string Version,
    string SupportedIrVersion,
    bool CanEncode,
    bool CanDecode,
    KnowledgeCodecMode Mode,
    bool Deterministic,
    bool Streaming = false,
    bool QueryConditioned = false,
    IReadOnlyList<string>? SupportedMediaTypes = null,
    IReadOnlyDictionary<string, string>? BenchmarkMetadata = null,
    KnowledgeCodecTrust Trust = KnowledgeCodecTrust.Builtin);

/// <summary>
/// Per-call SURFACE hints for a codec. Intentionally carries no budget/selection knob — retention is
/// the planner's job and is already resolved in the <see cref="MaterializedKnowledgeView"/>.
/// <paramref name="FocusQuery"/> is an optional hint ONLY for a codec that declares
/// <see cref="KnowledgeCodecDescriptor.QueryConditioned"/>: it may affect surface emphasis/ordering
/// (e.g. foregrounding higher-salience spans), never which assertions are present.
/// </summary>
public sealed record KnowledgeCodecEncodeOptions(string? FocusQuery = null)
{
    public static readonly KnowledgeCodecEncodeOptions None = new();
}

public sealed record KnowledgeCodecResult(string Surface, string CodecId, string Version);

/// <summary>Typed failure codes for codec selection / extension registration (master PR-E).</summary>
public static class KnowledgeCodecFailureCodes
{
    public const string UnsupportedCodec = "unsupported_codec";
    public const string CodecExtensionNotAllowed = "codec_extension_not_allowed";
    public const string UnknownCapabilityProfile = "unknown_capability_profile";
    public const string CodecCannotEncode = "codec_cannot_encode";
    public const string CodecAlreadyRegistered = "codec_already_registered";
    public const string InvalidCodecDescriptor = "invalid_codec_descriptor";
}
