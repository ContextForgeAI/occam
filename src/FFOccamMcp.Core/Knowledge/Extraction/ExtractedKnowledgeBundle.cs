using OccamMcp.Core.Knowledge.Canonical;
using OccamMcp.Core.Knowledge.Legacy;
using OccamMcp.Core.Workers;

namespace OccamMcp.Core.Knowledge.Extraction;

/// <summary>
/// Internal acquisition→knowledge boundary object. Carries Canonical Knowledge, the codec-local
/// document IR, an opaque source surface, and optional worker sidecars. Not a public MCP type.
/// </summary>
public sealed record ExtractedKnowledgeBundle(
    SourceSurface SourceSurface,
    KnowledgeDocument Document,
    CanonicalExtract? Canonical,
    string? FinalUrl,
    string? Backend,
    IReadOnlyList<WorkerExtractBlockInfo>? Blocks = null,
    IReadOnlyList<WorkerExtractTableInfo>? Tables = null,
    IReadOnlyList<WorkerExtractChunkInfo>? Chunks = null,
    IReadOnlyList<MediaRefInfo>? MediaRefs = null,
    WorkerExtractFeedInfo? Feed = null,
    WorkerExtractMetaInfo? Meta = null,
    string? Screenshot = null,
    WorkerBrowserProvisionedInfo? BrowserProvisioned = null);
