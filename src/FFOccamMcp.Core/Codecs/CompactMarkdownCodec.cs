using System.Text;
using OccamMcp.Core.Knowledge;

namespace OccamMcp.Core.Codecs;

/// <summary>
/// ADR-0001 PR-2: the first codec that renders a surface <em>from the block/table IR in C#</em> rather
/// than passing the worker markdown through. It emits a lean markdown: tables are rebuilt compactly from
/// <see cref="WorkerExtractTableInfo"/> (lossless — caption/headers/rows are faithfully represented),
/// and content blocks are rendered by type.
///
/// <para><b>Still lossy (residual IR limitations).</b> The IR now carries heading level (rebuilt as
/// <c>#</c>×level), but not list-nesting depth, and this minimal renderer drops per-block link hrefs — so
/// some structure is still flattened. Hence <see cref="KnowledgeCodecMode.Lossy"/>. It remains an opt-in/
/// experimental codec, deliberately NOT wired into the receipt-bearing transcode path (an alternate
/// surface would need its own contentHash / block reconciliation — ADR-0001 §9).</para>
///
/// Deterministic: identical view → byte-identical surface. Falls back to <c>view.Surface.Text</c> when no
/// structured input is present, so it is never emptier than passthrough.
/// </summary>
public sealed class CompactMarkdownCodec : IKnowledgeCodec
{
    public const string Id = "compact-markdown";

    public KnowledgeCodecDescriptor Descriptor { get; } = new(
        CodecId: Id,
        Version: "0.1",
        SupportedIrVersion: "0.1",
        CanEncode: true,
        CanDecode: false,
        Mode: KnowledgeCodecMode.Lossy,
        Deterministic: true,
        Streaming: false,
        QueryConditioned: false,
        SupportedMediaTypes: ["text/markdown"],
        BenchmarkMetadata: new Dictionary<string, string>
        {
            ["family"] = "markdown",
            ["role"] = "experimental",
            ["renders_from"] = "block-ir",
        },
        Trust: KnowledgeCodecTrust.BuiltinExperimental);

    public KnowledgeCodecResult Encode(MaterializedKnowledgeView view, KnowledgeCodecEncodeOptions options)
    {
        var doc = view.Knowledge;
        if (doc is null || doc.IsEmpty)
        {
            // Nothing structured to render — never emptier than passthrough.
            return new KnowledgeCodecResult(view.Surface.Text, Id, Descriptor.Version);
        }

        var parts = new List<string>();
        foreach (var b in doc.Blocks)
        {
            var rendered = RenderBlock(b);
            if (!string.IsNullOrEmpty(rendered))
            {
                parts.Add(rendered);
            }
        }

        foreach (var t in doc.Tables)
        {
            parts.Add(RenderTable(t));
        }

        var surface = parts.Count > 0 ? string.Join("\n\n", parts) + "\n" : view.Surface.Text;
        return new KnowledgeCodecResult(surface, Id, Descriptor.Version);
    }

    private static string RenderBlock(KnowledgeBlock b)
    {
        var text = b.Text?.Trim() ?? string.Empty;
        if (text.Length == 0)
        {
            return string.Empty;
        }

        return b.Type switch
        {
            "heading" => $"{new string('#', Math.Clamp(b.Level ?? 2, 1, 6))} {text}", // rebuild the level (defaults to h2 when absent)
            "list_item" => $"- {text}",
            "quote" => $"> {text}",
            "code" => $"```\n{text}\n```",
            _ => text,                            // paragraph / figure / table-as-text / unknown
        };
    }

    private static string RenderTable(KnowledgeTable t)
    {
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(t.Caption))
        {
            sb.Append("**").Append(t.Caption.Trim()).Append("**\n");
        }

        IReadOnlyList<string> headers = t.Headers.Count > 0 ? t.Headers : (t.Rows.Count > 0 ? t.Rows[0] : []);
        if (headers.Count == 0)
        {
            return sb.ToString().TrimEnd('\n');
        }

        sb.Append("| ").Append(string.Join(" | ", headers)).Append(" |\n");
        sb.Append("| ").Append(string.Join(" | ", headers.Select(_ => "---"))).Append(" |");
        foreach (var row in t.Rows)
        {
            sb.Append("\n| ").Append(string.Join(" | ", row)).Append(" |");
        }

        return sb.ToString();
    }
}
