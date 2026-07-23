namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Intermediate assertion extracted from a source but NOT yet a canonical <see cref="Fact"/>
/// (ADR-0003: extracted text ≠ Fact). May exist with zero validation and without Confidence.
/// Legacy Adapter (PR-B) may emit these from blocks; it must not promote them to Fact by default.
/// </summary>
public sealed record ClaimCandidate
{
    public ClaimCandidateId Id { get; }
    public string Statement { get; }
    public ClaimKind ClaimKind { get; }
    public IReadOnlyList<EvidenceId> EvidenceRefs { get; }
    public DateTimeOffset ExtractedAt { get; }
    public string? ExtractorId { get; }
    public string? ExtractorVersion { get; }
    /// <summary>Null means not computed — never invent from extractor salience.</summary>
    public ConfidenceLevel? Confidence { get; }

    private ClaimCandidate(
        ClaimCandidateId id,
        string statement,
        ClaimKind claimKind,
        IReadOnlyList<EvidenceId> evidenceRefs,
        DateTimeOffset extractedAt,
        string? extractorId,
        string? extractorVersion,
        ConfidenceLevel? confidence)
    {
        Id = id;
        Statement = statement;
        ClaimKind = claimKind;
        EvidenceRefs = evidenceRefs;
        ExtractedAt = extractedAt;
        ExtractorId = extractorId;
        ExtractorVersion = extractorVersion;
        Confidence = confidence;
    }

    public static ClaimCandidate Create(
        ClaimCandidateId id,
        string statement,
        ClaimKind claimKind,
        IReadOnlyList<EvidenceId> evidenceRefs,
        DateTimeOffset extractedAt,
        string? extractorId = null,
        string? extractorVersion = null,
        ConfidenceLevel? confidence = null)
    {
        if (string.IsNullOrWhiteSpace(statement))
        {
            throw new ArgumentException("ClaimCandidate statement must be non-empty.", nameof(statement));
        }

        ArgumentNullException.ThrowIfNull(evidenceRefs);

        return new ClaimCandidate(
            id,
            statement.Trim(),
            claimKind,
            evidenceRefs,
            extractedAt,
            string.IsNullOrWhiteSpace(extractorId) ? null : extractorId.Trim(),
            string.IsNullOrWhiteSpace(extractorVersion) ? null : extractorVersion.Trim(),
            confidence);
    }
}
