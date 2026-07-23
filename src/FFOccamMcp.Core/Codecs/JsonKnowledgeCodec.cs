using System.Text;
using System.Text.Json;
using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Codecs;

/// <summary>
/// Experimental Builtin codec: serializes a <see cref="MaterializedKnowledgeView"/> to JSON.
/// Representation-only — does not plan, extract, rank, budget, or call network/workers.
/// Selectable by explicit id (<see cref="Id"/>); never the live MCP default.
/// </summary>
public sealed class JsonKnowledgeCodec : IKnowledgeCodec
{
    public const string Id = "knowledge-json";

    public KnowledgeCodecDescriptor Descriptor { get; } = new(
        CodecId: Id,
        Version: "0.1",
        SupportedIrVersion: "0.1",
        CanEncode: true,
        CanDecode: false,
        Mode: KnowledgeCodecMode.Lossless,
        Deterministic: true,
        Streaming: false,
        QueryConditioned: false,
        SupportedMediaTypes: ["application/json"],
        BenchmarkMetadata: new Dictionary<string, string>
        {
            ["family"] = "json",
            ["role"] = "experimental",
            ["renders_from"] = "materialized-view",
        },
        Trust: KnowledgeCodecTrust.BuiltinExperimental);

    public KnowledgeCodecResult Encode(MaterializedKnowledgeView view, KnowledgeCodecEncodeOptions options)
    {
        ArgumentNullException.ThrowIfNull(view);
        _ = options; // surface hints unused — JSON projection is not query-conditioned

        var envelope = KnowledgeJsonProjection.FromView(view);
        var json = JsonSerializer.Serialize(envelope, KnowledgeJsonContext.Default.KnowledgeJsonEnvelope);
        return new KnowledgeCodecResult(json, Id, Descriptor.Version);
    }

    /// <summary>UTF-8 byte length of an encoded surface (bench helper).</summary>
    public static int Utf8ByteCount(string surface) => Encoding.UTF8.GetByteCount(surface);
}
