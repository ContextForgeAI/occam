using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// The single adapter that bridges the worker extract payload into the codec-local Knowledge IR
/// (ADR-0002: this is the ONE place that knows both sides, so codecs stay free of worker types). Pure
/// mapping — no selection or dropping (that is the planner's job).
/// </summary>
public static class WorkerKnowledgeMapper
{
    public static KnowledgeDocument FromExtract(
        IReadOnlyList<WorkerExtractBlockInfo>? blocks,
        IReadOnlyList<WorkerExtractTableInfo>? tables)
    {
        var kb = blocks is null
            ? (IReadOnlyList<KnowledgeBlock>)[]
            : [.. blocks.Select(MapBlock)];
        var kt = tables is null
            ? (IReadOnlyList<KnowledgeTable>)[]
            : [.. tables.Select(MapTable)];
        return new KnowledgeDocument(kb, kt);
    }

    private static KnowledgeBlock MapBlock(WorkerExtractBlockInfo b) => new(
        b.Type,
        b.Text,
        b.Links is { Length: > 0 }
            ? [.. b.Links.Select(l => new KnowledgeLink(l.Text, l.Href))]
            : null,
        string.IsNullOrEmpty(b.SourceSelector) ? null : b.SourceSelector,
        b.Salience,
        b.Trust,
        b.Level);

    private static KnowledgeTable MapTable(WorkerExtractTableInfo t) => new(
        string.IsNullOrWhiteSpace(t.Caption) ? null : t.Caption,
        t.Headers,
        [.. t.Rows.Select(r => (IReadOnlyList<string>)r)],
        string.IsNullOrEmpty(t.SourceSelector) ? null : t.SourceSelector,
        t.Records is { Length: > 0 }
            ? [.. t.Records.Select(MapRecord)]
            : null);

    private static KnowledgeSemanticRow MapRecord(WorkerExtractTableRecordInfo r) => new(
        r.Rank,
        r.Title,
        r.Url,
        r.Site,
        r.Author,
        r.Points,
        r.Comments,
        r.Age,
        r.Schema,
        r.Provenance is null
            ? null
            : new KnowledgeRowProvenance(
                r.Provenance.SourceSelector,
                r.Provenance.RowIndexes,
                r.Provenance.TableSelector));
}
