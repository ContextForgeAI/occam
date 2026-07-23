namespace OccamMcp.Core.Knowledge;

/// <summary>
/// ADR-0001 PR-3: the codec-local / materialization-pipeline document IR (blocks + tables).
/// This is <em>not</em> the Canonical Knowledge store (ADR-0003). Canonical types live under
/// <c>Knowledge.Canonical</c> (Source / Evidence / ClaimCandidate / Entity / Fact / …).
/// <see cref="KnowledgeDocument"/> is an extracted-document intermediate that the Planner and Codecs
/// already consume; heading <c>Level</c> is present when the worker emits it; list-nesting depth is
/// still deferred.
/// </summary>
public sealed record KnowledgeDocument(
    IReadOnlyList<KnowledgeBlock> Blocks,
    IReadOnlyList<KnowledgeTable> Tables)
{
    public static readonly KnowledgeDocument Empty = new([], []);

    public bool IsEmpty => Blocks.Count == 0 && Tables.Count == 0;
}

/// <summary>A typed content block. <paramref name="Type"/> is heading/paragraph/list_item/code/quote/
/// table/figure. Salience/Trust are the per-span annotations (#3/#4) when present.
/// <paramref name="Span"/> is an optional lossless pointer into the opaque <see cref="SourceSurface"/>.</summary>
public sealed record KnowledgeBlock(
    string Type,
    string Text,
    IReadOnlyList<KnowledgeLink>? Links = null,
    string? SourceSelector = null,
    double? Salience = null,
    string? Trust = null,
    int? Level = null,
    SurfaceSpan? Span = null);

public sealed record KnowledgeLink(string Text, string Href);

public sealed record KnowledgeTable(
    string? Caption,
    IReadOnlyList<string> Headers,
    IReadOnlyList<IReadOnlyList<string>> Rows,
    string? SourceSelector = null,
    IReadOnlyList<KnowledgeSemanticRow>? SemanticRows = null);

/// <summary>
/// One knowledge object reconstructed from one or more physical table rows (e.g. HN title+subtext).
/// Provenance ties the object back to the DOM / physical row indexes without altering markdown.
/// </summary>
public sealed record KnowledgeSemanticRow(
    string? Rank = null,
    string? Title = null,
    string? Url = null,
    string? Site = null,
    string? Author = null,
    int? Points = null,
    int? Comments = null,
    string? Age = null,
    string? Schema = null,
    KnowledgeRowProvenance? Provenance = null);

public sealed record KnowledgeRowProvenance(
    string SourceSelector,
    IReadOnlyList<int> RowIndexes,
    string? TableSelector = null);
