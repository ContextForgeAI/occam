namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Domain-level provenance for a semantic object (Fact / Relationship / ClaimCandidate linkage).
/// Named <c>KnowledgeProvenance</c> deliberately — do NOT confuse with:
/// <list type="bullet">
/// <item><see cref="Playbooks.PlaybookProvenance"/> — playbook resolver tier labels (local/user/community/seed).</item>
/// <item><c>ReceiptEnvelope</c> — signed extraction infrastructure (crypto). Optional hash fields here are
/// opaque bridges to that infra; this type does not depend on the Receipts assembly graph.</item>
/// </list>
/// Does not embed large excerpts — point at <see cref="Evidence"/> instead.
/// </summary>
public sealed record KnowledgeProvenance
{
    public ProvenanceId Id { get; }
    public SourceId SourceId { get; }
    public IReadOnlyList<EvidenceId> EvidenceIds { get; }
    public DateTimeOffset? ObservedAt { get; }
    public string? ExtractionMethod { get; }
    public string? ExtractionVersion { get; }
    public ValidationState? ValidationHint { get; }
    /// <summary>Optional opaque bridge to Receipt v1 <c>contentHash</c> — not a second receipt model.</summary>
    public string? ReceiptContentHash { get; }
    /// <summary>Optional opaque bridge to a receipt block leaf hash.</summary>
    public string? BlockLeafHash { get; }

    private KnowledgeProvenance(
        ProvenanceId id,
        SourceId sourceId,
        IReadOnlyList<EvidenceId> evidenceIds,
        DateTimeOffset? observedAt,
        string? extractionMethod,
        string? extractionVersion,
        ValidationState? validationHint,
        string? receiptContentHash,
        string? blockLeafHash)
    {
        Id = id;
        SourceId = sourceId;
        EvidenceIds = evidenceIds;
        ObservedAt = observedAt;
        ExtractionMethod = extractionMethod;
        ExtractionVersion = extractionVersion;
        ValidationHint = validationHint;
        ReceiptContentHash = receiptContentHash;
        BlockLeafHash = blockLeafHash;
    }

    public static KnowledgeProvenance Create(
        ProvenanceId id,
        SourceId sourceId,
        IReadOnlyList<EvidenceId> evidenceIds,
        DateTimeOffset? observedAt = null,
        string? extractionMethod = null,
        string? extractionVersion = null,
        ValidationState? validationHint = null,
        string? receiptContentHash = null,
        string? blockLeafHash = null)
    {
        ArgumentNullException.ThrowIfNull(evidenceIds);
        if (evidenceIds.Count == 0)
        {
            throw new ArgumentException("KnowledgeProvenance requires at least one EvidenceId.", nameof(evidenceIds));
        }

        return new KnowledgeProvenance(
            id,
            sourceId,
            evidenceIds,
            observedAt,
            string.IsNullOrWhiteSpace(extractionMethod) ? null : extractionMethod.Trim(),
            string.IsNullOrWhiteSpace(extractionVersion) ? null : extractionVersion.Trim(),
            validationHint,
            string.IsNullOrWhiteSpace(receiptContentHash) ? null : receiptContentHash.Trim(),
            string.IsNullOrWhiteSpace(blockLeafHash) ? null : blockLeafHash.Trim());
    }
}
