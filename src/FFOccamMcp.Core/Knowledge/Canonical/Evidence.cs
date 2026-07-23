namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// A concrete fragment / region of a <see cref="Source"/> that can support a claim (ADR-0003:
/// Evidence ≠ conclusion). Blocks from the 0.9 extract become Evidence carriers via the future
/// Legacy Adapter — never Facts by default.
/// </summary>
public sealed record Evidence
{
    public EvidenceId Id { get; }
    public SourceId SourceId { get; }
    public EvidenceLocator Locator { get; }
    public EvidenceKind Kind { get; }
    public DateTimeOffset CreatedAt { get; }
    public string? ContentHash { get; }
    public string? Excerpt { get; }

    private Evidence(
        EvidenceId id,
        SourceId sourceId,
        EvidenceLocator locator,
        EvidenceKind kind,
        DateTimeOffset createdAt,
        string? contentHash,
        string? excerpt)
    {
        Id = id;
        SourceId = sourceId;
        Locator = locator;
        Kind = kind;
        CreatedAt = createdAt;
        ContentHash = contentHash;
        Excerpt = excerpt;
    }

    public static Evidence Create(
        EvidenceId id,
        SourceId sourceId,
        EvidenceLocator locator,
        EvidenceKind kind,
        DateTimeOffset createdAt,
        string? contentHash = null,
        string? excerpt = null)
    {
        ArgumentNullException.ThrowIfNull(locator);

        return new Evidence(
            id,
            sourceId,
            locator,
            kind,
            createdAt,
            string.IsNullOrWhiteSpace(contentHash) ? null : contentHash.Trim(),
            string.IsNullOrWhiteSpace(excerpt) ? null : excerpt);
    }
}
