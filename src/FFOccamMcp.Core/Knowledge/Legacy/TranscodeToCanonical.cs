using System.Globalization;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Knowledge.Canonical;
using OccamMcp.Core.Receipts;
using OccamMcp.Core.Routing;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Knowledge.Legacy;

/// <summary>
/// Legacy Adapter: map a successful extract into Canonical Knowledge records.
/// Fail-closed on <c>Ok=false</c> (returns null — never invents knowledge from unknown content).
/// Pure / deterministic structure / no network / does not promote blocks to <see cref="Fact"/>.
/// </summary>
public static class TranscodeToCanonical
{
    public const string AdapterId = "transcode-to-canonical";
    public const string AdapterVersion = "1";

    /// <summary>
    /// Adapt a successful extract outcome. Prefer <see cref="TryAdaptAcquisition"/> for the live
    /// acquisition path (no compiled-outcome dependency). This overload remains for unit tests and
    /// callers that already hold a <see cref="TranscodeOutcome"/>.
    /// </summary>
    public static CanonicalExtract? TryAdapt(
        string requestUrl,
        TranscodeOutcome outcome,
        string? contentHash = null,
        DateTimeOffset? retrievedAt = null,
        string? extractionVersion = null)
    {
        ArgumentNullException.ThrowIfNull(outcome);

        if (!outcome.Ok)
        {
            return null;
        }

        return TryAdaptAcquisition(
            requestUrl,
            outcome.FinalUrl,
            outcome.Backend,
            outcome.Markdown,
            outcome.Blocks,
            outcome.Tables,
            outcome.Meta,
            contentHash,
            outcome.PlaybookId,
            outcome.PlaybookVersion,
            retrievedAt,
            extractionVersion);
    }

    /// <summary>
    /// Acquisition-time canonical mapping — does not require a compiled <see cref="TranscodeOutcome"/>.
    /// When <paramref name="contentHash"/> is null, a hash is derived from the source surface only as a
    /// provisional bridge; the live pipeline rebinds the final receipt hash after codec output.
    /// </summary>
    public static CanonicalExtract? TryAdaptAcquisition(
        string requestUrl,
        string? finalUrl,
        string? backend,
        string? sourceSurfaceText,
        IReadOnlyList<WorkerExtractBlockInfo>? blocks,
        IReadOnlyList<WorkerExtractTableInfo>? tables,
        WorkerExtractMetaInfo? meta,
        string? contentHash = null,
        string? playbookId = null,
        string? playbookVersion = null,
        DateTimeOffset? retrievedAt = null,
        string? extractionVersion = null)
    {
        if (string.IsNullOrWhiteSpace(requestUrl) && string.IsNullOrWhiteSpace(finalUrl))
        {
            return null;
        }

        var observedAt = retrievedAt ?? DateTimeOffset.UtcNow;
        var locator = string.IsNullOrWhiteSpace(finalUrl) ? requestUrl.Trim() : finalUrl.Trim();
        var hash = NormalizeContentHash(contentHash)
            ?? (string.IsNullOrEmpty(sourceSurfaceText) ? null : ContentHashToken.BareHex(sourceSurfaceText));

        var source = Source.Create(
            SourceId.New(),
            InferSourceKind(locator),
            locator,
            observedAt,
            publishedAt: TryParsePublishedAt(meta),
            contentHash: hash,
            title: null,
            metadata: BuildSourceMetadata(backend, playbookId, playbookVersion, meta));

        var evidence = new List<Evidence>();
        var claims = new List<ClaimCandidate>();
        var provenance = new List<KnowledgeProvenance>();

        var extractionMethod = string.IsNullOrWhiteSpace(backend) ? AdapterId : backend.Trim();
        var version = string.IsNullOrWhiteSpace(extractionVersion) ? null : extractionVersion.Trim();

        if (blocks is { Count: > 0 })
        {
            for (var i = 0; i < blocks.Count; i++)
            {
                var block = blocks[i];
                var leafHex = Convert.ToHexString(MerkleTree.LeafHash(block.Text, block.SourceSelector))
                    .ToLowerInvariant();
                var ev = MapBlockEvidence(source.Id, block, leafHex, observedAt);
                evidence.Add(ev);

                if (!string.IsNullOrWhiteSpace(block.Text))
                {
                    claims.Add(ClaimCandidate.Create(
                        ClaimCandidateId.New(),
                        block.Text,
                        ClaimKind.ExtractedClaim,
                        [ev.Id],
                        observedAt,
                        extractorId: extractionMethod,
                        extractorVersion: version,
                        confidence: null));
                }

                provenance.Add(KnowledgeProvenance.Create(
                    ProvenanceId.New(),
                    source.Id,
                    [ev.Id],
                    observedAt: observedAt,
                    extractionMethod: extractionMethod,
                    extractionVersion: version,
                    validationHint: ValidationState.Unvalidated,
                    receiptContentHash: hash,
                    blockLeafHash: leafHex));
            }
        }

        if (tables is { Count: > 0 })
        {
            foreach (var table in tables)
            {
                var ev = MapTableEvidence(source.Id, table, observedAt);
                evidence.Add(ev);

                if (!string.IsNullOrWhiteSpace(table.Caption))
                {
                    claims.Add(ClaimCandidate.Create(
                        ClaimCandidateId.New(),
                        table.Caption,
                        ClaimKind.ExtractedClaim,
                        [ev.Id],
                        observedAt,
                        extractorId: extractionMethod,
                        extractorVersion: version,
                        confidence: null));
                }

                provenance.Add(KnowledgeProvenance.Create(
                    ProvenanceId.New(),
                    source.Id,
                    [ev.Id],
                    observedAt: observedAt,
                    extractionMethod: extractionMethod,
                    extractionVersion: version,
                    validationHint: ValidationState.Unvalidated,
                    receiptContentHash: hash,
                    blockLeafHash: null));
            }
        }

        // Surface-only success (no structured blocks/tables): one excerpt Evidence, no ClaimCandidate
        // (avoid inventing an assertion from the whole page body).
        if (evidence.Count == 0 && !string.IsNullOrWhiteSpace(sourceSurfaceText))
        {
            var ev = Evidence.Create(
                EvidenceId.New(),
                source.Id,
                EvidenceLocator.Unspecified("surface"),
                EvidenceKind.Excerpt,
                observedAt,
                contentHash: hash,
                excerpt: null);
            evidence.Add(ev);
            provenance.Add(KnowledgeProvenance.Create(
                ProvenanceId.New(),
                source.Id,
                [ev.Id],
                observedAt: observedAt,
                extractionMethod: extractionMethod,
                extractionVersion: version,
                validationHint: ValidationState.Unvalidated,
                receiptContentHash: hash));
        }

        return new CanonicalExtract(source, evidence, claims, provenance);
    }

    private static Evidence MapBlockEvidence(
        SourceId sourceId,
        WorkerExtractBlockInfo block,
        string leafHex,
        DateTimeOffset createdAt)
    {
        var locator = string.IsNullOrWhiteSpace(block.SourceSelector)
            ? EvidenceLocator.Unspecified(string.IsNullOrWhiteSpace(block.Type) ? "block" : block.Type)
            : EvidenceLocator.SourceSelector(block.SourceSelector);

        return Evidence.Create(
            EvidenceId.New(),
            sourceId,
            locator,
            EvidenceKind.ContentBlock,
            createdAt,
            contentHash: leafHex,
            excerpt: string.IsNullOrWhiteSpace(block.Text) ? null : TruncateExcerpt(block.Text));
    }

    private static Evidence MapTableEvidence(
        SourceId sourceId,
        WorkerExtractTableInfo table,
        DateTimeOffset createdAt)
    {
        var locator = string.IsNullOrWhiteSpace(table.SourceSelector)
            ? EvidenceLocator.Unspecified("table")
            : EvidenceLocator.SourceSelector(table.SourceSelector);

        return Evidence.Create(
            EvidenceId.New(),
            sourceId,
            locator,
            EvidenceKind.Table,
            createdAt,
            contentHash: null,
            excerpt: string.IsNullOrWhiteSpace(table.Caption) ? null : TruncateExcerpt(table.Caption));
    }

    private static SourceKind InferSourceKind(string locator)
    {
        if (locator.StartsWith("http://", StringComparison.OrdinalIgnoreCase)
            || locator.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            return SourceKind.WebPage;
        }

        return SourceKind.Other;
    }

    private static DateTimeOffset? TryParsePublishedAt(WorkerExtractMetaInfo? meta)
    {
        if (meta is null || string.IsNullOrWhiteSpace(meta.PublishedAt))
        {
            return null;
        }

        return DateTimeOffset.TryParse(
            meta.PublishedAt,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
            out var dto)
            ? dto
            : null;
    }

    private static IReadOnlyDictionary<string, string> BuildSourceMetadata(
        string? backend,
        string? playbookId,
        string? playbookVersion,
        WorkerExtractMetaInfo? meta)
    {
        var map = new Dictionary<string, string>(StringComparer.Ordinal);
        if (!string.IsNullOrWhiteSpace(backend))
        {
            map["backend"] = backend.Trim();
        }

        if (!string.IsNullOrWhiteSpace(playbookId))
        {
            map["playbookId"] = playbookId.Trim();
        }

        if (!string.IsNullOrWhiteSpace(playbookVersion))
        {
            map["playbookVersion"] = playbookVersion.Trim();
        }

        if (meta?.Lang is { Length: > 0 } lang)
        {
            map["lang"] = lang.Trim();
        }

        return map.Count == 0 ? CanonicalEmpty.Metadata : map;
    }

    private static string? NormalizeContentHash(string? contentHash)
    {
        if (string.IsNullOrWhiteSpace(contentHash))
        {
            return null;
        }

        var t = contentHash.Trim();
        const string prefix = "sha256:";
        if (t.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            t = t[prefix.Length..];
        }

        return string.IsNullOrWhiteSpace(t) ? null : t.ToLowerInvariant();
    }

    private static string TruncateExcerpt(string text)
    {
        const int max = 512;
        var trimmed = text.Trim();
        return trimmed.Length <= max ? trimmed : trimmed[..max];
    }
}
