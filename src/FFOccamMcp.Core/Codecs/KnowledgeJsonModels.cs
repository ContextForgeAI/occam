using System.Text.Json.Serialization;
using OccamMcp.Core.Knowledge;
using OccamMcp.Core.Knowledge.Canonical;

namespace OccamMcp.Core.Codecs;

/// <summary>
/// Stable, AOT-friendly DTO projection of a <see cref="MaterializedKnowledgeView"/> for
/// <see cref="JsonKnowledgeCodec"/>. Representation-only — no inference, no reordering beyond
/// deterministic sorts documented on each collection.
/// </summary>
public sealed class KnowledgeJsonEnvelope
{
    public string Codec { get; init; } = JsonKnowledgeCodec.Id;
    public string Version { get; init; } = "0.1";
    public KnowledgeJsonSurface Surface { get; init; } = new();
    public KnowledgeJsonDocument? Knowledge { get; init; }
    public IReadOnlyList<KnowledgeJsonSource>? Sources { get; init; }
    public IReadOnlyList<KnowledgeJsonEvidence>? Evidence { get; init; }
    public IReadOnlyList<KnowledgeJsonClaim>? Claims { get; init; }
    public IReadOnlyList<KnowledgeJsonProvenance>? Provenance { get; init; }
}

public sealed class KnowledgeJsonSurface
{
    public string MediaType { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
}

public sealed class KnowledgeJsonDocument
{
    public IReadOnlyList<KnowledgeJsonBlock> Blocks { get; init; } = [];
    public IReadOnlyList<KnowledgeJsonTable> Tables { get; init; } = [];
}

public sealed class KnowledgeJsonBlock
{
    public string Type { get; init; } = string.Empty;
    public string Text { get; init; } = string.Empty;
    public IReadOnlyList<KnowledgeJsonLink>? Links { get; init; }
    public string? SourceSelector { get; init; }
    public double? Salience { get; init; }
    public string? Trust { get; init; }
    public int? Level { get; init; }
    public KnowledgeJsonSpan? Span { get; init; }
}

public sealed class KnowledgeJsonLink
{
    public string Text { get; init; } = string.Empty;
    public string Href { get; init; } = string.Empty;
}

public sealed class KnowledgeJsonSpan
{
    public int Start { get; init; }
    public int Length { get; init; }
}

public sealed class KnowledgeJsonTable
{
    public string? Caption { get; init; }
    public IReadOnlyList<string> Headers { get; init; } = [];
    public IReadOnlyList<IReadOnlyList<string>> Rows { get; init; } = [];
    public string? SourceSelector { get; init; }
    public IReadOnlyList<KnowledgeJsonSemanticRow>? SemanticRows { get; init; }
}

public sealed class KnowledgeJsonSemanticRow
{
    public string? Rank { get; init; }
    public string? Title { get; init; }
    public string? Url { get; init; }
    public string? Site { get; init; }
    public string? Author { get; init; }
    public int? Points { get; init; }
    public int? Comments { get; init; }
    public string? Age { get; init; }
    public string? Schema { get; init; }
    public KnowledgeJsonRowProvenance? Provenance { get; init; }
}

public sealed class KnowledgeJsonRowProvenance
{
    public string SourceSelector { get; init; } = string.Empty;
    public IReadOnlyList<int> RowIndexes { get; init; } = [];
    public string? TableSelector { get; init; }
}

public sealed class KnowledgeJsonSource
{
    public string Id { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public string Locator { get; init; } = string.Empty;
    public string RetrievedAt { get; init; } = string.Empty;
    public string? PublishedAt { get; init; }
    public string? ContentHash { get; init; }
    public string? Title { get; init; }
    public IReadOnlyList<KnowledgeJsonKv>? Metadata { get; init; }
}

public sealed class KnowledgeJsonEvidence
{
    public string Id { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public string Kind { get; init; } = string.Empty;
    public KnowledgeJsonLocator Locator { get; init; } = new();
    public string CreatedAt { get; init; } = string.Empty;
    public string? ContentHash { get; init; }
    public string? Excerpt { get; init; }
}

public sealed class KnowledgeJsonLocator
{
    public string Kind { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
    public IReadOnlyList<KnowledgeJsonKv>? Attributes { get; init; }
}

public sealed class KnowledgeJsonClaim
{
    public string Id { get; init; } = string.Empty;
    public string Statement { get; init; } = string.Empty;
    public string ClaimKind { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceRefs { get; init; } = [];
    public string ExtractedAt { get; init; } = string.Empty;
    public string? ExtractorId { get; init; }
    public string? ExtractorVersion { get; init; }
    public string? Confidence { get; init; }
}

public sealed class KnowledgeJsonProvenance
{
    public string Id { get; init; } = string.Empty;
    public string SourceId { get; init; } = string.Empty;
    public IReadOnlyList<string> EvidenceIds { get; init; } = [];
    public string? ObservedAt { get; init; }
    public string? ExtractionMethod { get; init; }
    public string? ExtractionVersion { get; init; }
    public string? ValidationHint { get; init; }
    public string? ReceiptContentHash { get; init; }
    public string? BlockLeafHash { get; init; }
}

/// <summary>Key/value pair used instead of dictionaries so JSON property order is stable.</summary>
public sealed class KnowledgeJsonKv
{
    public string Key { get; init; } = string.Empty;
    public string Value { get; init; } = string.Empty;
}

/// <summary>Projects a materialized view into JSON DTOs with deterministic collection order.</summary>
public static class KnowledgeJsonProjection
{
    public static KnowledgeJsonEnvelope FromView(MaterializedKnowledgeView view)
    {
        ArgumentNullException.ThrowIfNull(view);

        return new KnowledgeJsonEnvelope
        {
            Codec = JsonKnowledgeCodec.Id,
            Version = "0.1",
            Surface = new KnowledgeJsonSurface
            {
                MediaType = view.Surface.MediaType ?? string.Empty,
                Text = view.Surface.Text ?? string.Empty,
            },
            Knowledge = ProjectDocument(view.Knowledge),
            Sources = ProjectSources(view.SourceRefs),
            Evidence = ProjectEvidence(view.EvidenceRefs),
            Claims = ProjectClaims(view.Claims),
            Provenance = ProjectProvenance(view.Provenance),
        };
    }

    private static KnowledgeJsonDocument? ProjectDocument(KnowledgeDocument? doc)
    {
        if (doc is null)
        {
            return null;
        }

        return new KnowledgeJsonDocument
        {
            // Document order is already the planner's order — preserve it.
            Blocks = doc.Blocks.Select(ProjectBlock).ToArray(),
            Tables = doc.Tables.Select(ProjectTable).ToArray(),
        };
    }

    private static KnowledgeJsonBlock ProjectBlock(KnowledgeBlock b) => new()
    {
        Type = b.Type ?? string.Empty,
        Text = b.Text ?? string.Empty,
        Links = b.Links is { Count: > 0 }
            ? b.Links.Select(l => new KnowledgeJsonLink { Text = l.Text ?? string.Empty, Href = l.Href ?? string.Empty }).ToArray()
            : null,
        SourceSelector = b.SourceSelector,
        Salience = b.Salience,
        Trust = b.Trust,
        Level = b.Level,
        Span = b.Span is null ? null : new KnowledgeJsonSpan { Start = b.Span.Start, Length = b.Span.Length },
    };

    private static KnowledgeJsonTable ProjectTable(KnowledgeTable t) => new()
    {
        Caption = t.Caption,
        Headers = t.Headers.ToArray(),
        Rows = t.Rows.Select(r => (IReadOnlyList<string>)r.ToArray()).ToArray(),
        SourceSelector = t.SourceSelector,
        SemanticRows = t.SemanticRows is { Count: > 0 }
            ? t.SemanticRows.Select(ProjectSemanticRow).ToArray()
            : null,
    };

    private static KnowledgeJsonSemanticRow ProjectSemanticRow(KnowledgeSemanticRow r) => new()
    {
        Rank = r.Rank,
        Title = r.Title,
        Url = r.Url,
        Site = r.Site,
        Author = r.Author,
        Points = r.Points,
        Comments = r.Comments,
        Age = r.Age,
        Schema = r.Schema,
        Provenance = r.Provenance is null
            ? null
            : new KnowledgeJsonRowProvenance
            {
                SourceSelector = r.Provenance.SourceSelector ?? string.Empty,
                RowIndexes = r.Provenance.RowIndexes.ToArray(),
                TableSelector = r.Provenance.TableSelector,
            },
    };

    private static IReadOnlyList<KnowledgeJsonSource>? ProjectSources(IReadOnlyList<Source>? sources)
    {
        if (sources is null)
        {
            return null;
        }

        return sources
            .OrderBy(s => s.Id.Value, StringComparer.Ordinal)
            .Select(s => new KnowledgeJsonSource
            {
                Id = s.Id.Value,
                Kind = s.Kind.ToString(),
                Locator = s.Locator,
                RetrievedAt = s.RetrievedAt.ToUniversalTime().ToString("O"),
                PublishedAt = s.PublishedAt?.ToUniversalTime().ToString("O"),
                ContentHash = s.ContentHash,
                Title = s.Title,
                Metadata = ProjectMetadata(s.Metadata),
            })
            .ToArray();
    }

    private static IReadOnlyList<KnowledgeJsonEvidence>? ProjectEvidence(IReadOnlyList<Evidence>? evidence)
    {
        if (evidence is null)
        {
            return null;
        }

        return evidence
            .OrderBy(e => e.Id.Value, StringComparer.Ordinal)
            .Select(e => new KnowledgeJsonEvidence
            {
                Id = e.Id.Value,
                SourceId = e.SourceId.Value,
                Kind = e.Kind.ToString(),
                Locator = new KnowledgeJsonLocator
                {
                    Kind = e.Locator.Kind.ToString(),
                    Value = e.Locator.Value ?? string.Empty,
                    Attributes = ProjectMetadata(e.Locator.Attributes),
                },
                CreatedAt = e.CreatedAt.ToUniversalTime().ToString("O"),
                ContentHash = e.ContentHash,
                Excerpt = e.Excerpt,
            })
            .ToArray();
    }

    private static IReadOnlyList<KnowledgeJsonClaim>? ProjectClaims(IReadOnlyList<ClaimCandidate>? claims)
    {
        if (claims is null)
        {
            return null;
        }

        return claims
            .OrderBy(c => c.Id.Value, StringComparer.Ordinal)
            .Select(c => new KnowledgeJsonClaim
            {
                Id = c.Id.Value,
                Statement = c.Statement,
                ClaimKind = c.ClaimKind.ToString(),
                EvidenceRefs = c.EvidenceRefs
                    .Select(id => id.Value)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToArray(),
                ExtractedAt = c.ExtractedAt.ToUniversalTime().ToString("O"),
                ExtractorId = c.ExtractorId,
                ExtractorVersion = c.ExtractorVersion,
                Confidence = c.Confidence?.ToString(),
            })
            .ToArray();
    }

    private static IReadOnlyList<KnowledgeJsonProvenance>? ProjectProvenance(IReadOnlyList<KnowledgeProvenance>? provenance)
    {
        if (provenance is null)
        {
            return null;
        }

        return provenance
            .OrderBy(p => p.Id.Value, StringComparer.Ordinal)
            .Select(p => new KnowledgeJsonProvenance
            {
                Id = p.Id.Value,
                SourceId = p.SourceId.Value,
                EvidenceIds = p.EvidenceIds
                    .Select(id => id.Value)
                    .OrderBy(v => v, StringComparer.Ordinal)
                    .ToArray(),
                ObservedAt = p.ObservedAt?.ToUniversalTime().ToString("O"),
                ExtractionMethod = p.ExtractionMethod,
                ExtractionVersion = p.ExtractionVersion,
                ValidationHint = p.ValidationHint?.ToString(),
                ReceiptContentHash = p.ReceiptContentHash,
                BlockLeafHash = p.BlockLeafHash,
            })
            .ToArray();
    }

    private static IReadOnlyList<KnowledgeJsonKv>? ProjectMetadata(IReadOnlyDictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        return metadata
            .OrderBy(kv => kv.Key, StringComparer.Ordinal)
            .Select(kv => new KnowledgeJsonKv { Key = kv.Key, Value = kv.Value })
            .ToArray();
    }
}

[JsonSourceGenerationOptions(
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false)]
[JsonSerializable(typeof(KnowledgeJsonEnvelope))]
[JsonSerializable(typeof(KnowledgeJsonSurface))]
[JsonSerializable(typeof(KnowledgeJsonDocument))]
[JsonSerializable(typeof(KnowledgeJsonBlock))]
[JsonSerializable(typeof(KnowledgeJsonLink))]
[JsonSerializable(typeof(KnowledgeJsonSpan))]
[JsonSerializable(typeof(KnowledgeJsonTable))]
[JsonSerializable(typeof(KnowledgeJsonSemanticRow))]
[JsonSerializable(typeof(KnowledgeJsonRowProvenance))]
[JsonSerializable(typeof(KnowledgeJsonSource))]
[JsonSerializable(typeof(KnowledgeJsonEvidence))]
[JsonSerializable(typeof(KnowledgeJsonLocator))]
[JsonSerializable(typeof(KnowledgeJsonClaim))]
[JsonSerializable(typeof(KnowledgeJsonProvenance))]
[JsonSerializable(typeof(KnowledgeJsonKv))]
internal partial class KnowledgeJsonContext : JsonSerializerContext;
