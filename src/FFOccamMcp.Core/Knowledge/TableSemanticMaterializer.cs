using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Turns worker table payloads (physical rows + optional semantic <c>records</c>) into the
/// materialization-pipeline knowledge view. Markdown is never rewritten here — callers keep the
/// existing extract markdown; semantic rows ride alongside as knowledge objects with provenance.
/// </summary>
public static class TableSemanticMaterializer
{
    public static MaterializedKnowledgeView Materialize(
        string markdown,
        IReadOnlyList<WorkerExtractBlockInfo>? blocks,
        IReadOnlyList<WorkerExtractTableInfo>? tables,
        MaterializationPolicy? policy = null)
    {
        var doc = WorkerKnowledgeMapper.FromExtract(blocks, tables);
        var planner = new MaterializationPlanner();
        return planner.Plan(markdown, doc, policy ?? MaterializationPolicy.None);
    }

    /// <summary>Convenience: count semantic knowledge objects across all tables.</summary>
    public static int CountSemanticRows(KnowledgeDocument? doc) =>
        doc?.Tables.Sum(t => t.SemanticRows?.Count ?? 0) ?? 0;
}
