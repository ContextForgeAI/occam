namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// A source or a concrete acquisition of a source (PR-A). <see cref="Locator"/> is the
/// CanonicalUri / SourceLocator (URL, file path, API endpoint, …). <see cref="RetrievedAt"/> is the
/// acquisition/observed time; <see cref="PublishedAt"/> is optional source-declared publish time.
/// Does not embed document body — body lives behind Evidence.
/// </summary>
public sealed record Source
{
    public SourceId Id { get; }
    public SourceKind Kind { get; }
    public string Locator { get; }
    public DateTimeOffset RetrievedAt { get; }
    public DateTimeOffset? PublishedAt { get; }
    public string? ContentHash { get; }
    public string? Title { get; }
    public IReadOnlyDictionary<string, string> Metadata { get; }

    private Source(
        SourceId id,
        SourceKind kind,
        string locator,
        DateTimeOffset retrievedAt,
        DateTimeOffset? publishedAt,
        string? contentHash,
        string? title,
        IReadOnlyDictionary<string, string> metadata)
    {
        Id = id;
        Kind = kind;
        Locator = locator;
        RetrievedAt = retrievedAt;
        PublishedAt = publishedAt;
        ContentHash = contentHash;
        Title = title;
        Metadata = metadata;
    }

    public static Source Create(
        SourceId id,
        SourceKind kind,
        string locator,
        DateTimeOffset retrievedAt,
        DateTimeOffset? publishedAt = null,
        string? contentHash = null,
        string? title = null,
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        if (string.IsNullOrWhiteSpace(locator))
        {
            throw new ArgumentException("Source locator (CanonicalUri / SourceLocator) must be non-empty.", nameof(locator));
        }

        return new Source(
            id,
            kind,
            locator.Trim(),
            retrievedAt,
            publishedAt,
            string.IsNullOrWhiteSpace(contentHash) ? null : contentHash.Trim(),
            string.IsNullOrWhiteSpace(title) ? null : title.Trim(),
            metadata ?? CanonicalEmpty.Metadata);
    }
}
