using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Codecs;

/// <summary>
/// The behaviour-preserving built-in codec. It returns the already-materialized Markdown surface
/// exactly as produced by the Materialization Planner. It is the guaranteed default codec and the
/// live compatibility baseline.
///
/// <para>Pure and deterministic: no extraction, network, planner, receipt or transport dependency.
/// It performs no semantic selection and ignores structured/canonical sidecars; provenance hashes
/// therefore remain byte-compatible with the pre-codec Markdown path.</para>
/// </summary>
public sealed class MarkdownPassthroughCodec : IKnowledgeCodec
{
    public const string Id = "markdown-passthrough";

    public KnowledgeCodecDescriptor Descriptor { get; } = new(
        CodecId: Id,
        Version: "1.0",
        SupportedIrVersion: "0.1",
        CanEncode: true,
        CanDecode: false,
        Mode: KnowledgeCodecMode.Lossless,
        Deterministic: true,
        Streaming: false,
        QueryConditioned: false,
        SupportedMediaTypes: ["text/markdown"],
        BenchmarkMetadata: new Dictionary<string, string>
        {
            ["family"] = "markdown",
            ["role"] = "builtin-default",
        },
        Trust: KnowledgeCodecTrust.Builtin);

    public KnowledgeCodecResult Encode(MaterializedKnowledgeView view, KnowledgeCodecEncodeOptions options)
    {
        // Prefer the opaque surface; Markdown accessor is the compatibility bridge.
        var text = view.Surface.IsMarkdown || string.IsNullOrEmpty(view.Surface.MediaType)
            ? view.Surface.Text
            : view.Markdown;
        return new(text, Id, Descriptor.Version);
    }
}
