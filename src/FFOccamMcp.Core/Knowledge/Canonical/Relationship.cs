namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Explicit relation between two entities. Kept separate from <see cref="Fact"/> until real use
/// cases prove they should merge. Same Supported→provenance invariant as Fact.
/// </summary>
public sealed record Relationship
{
    public RelationshipId Id { get; }
    public EntityId SubjectEntityId { get; }
    public string RelationType { get; }
    public EntityId ObjectEntityId { get; }
    public ValidationState ValidationState { get; }
    public IReadOnlyList<ProvenanceId> ProvenanceRefs { get; }
    public ConfidenceLevel? Confidence { get; }
    public TemporalScope? TemporalScope { get; }

    private Relationship(
        RelationshipId id,
        EntityId subjectEntityId,
        string relationType,
        EntityId objectEntityId,
        ValidationState validationState,
        IReadOnlyList<ProvenanceId> provenanceRefs,
        ConfidenceLevel? confidence,
        TemporalScope? temporalScope)
    {
        Id = id;
        SubjectEntityId = subjectEntityId;
        RelationType = relationType;
        ObjectEntityId = objectEntityId;
        ValidationState = validationState;
        ProvenanceRefs = provenanceRefs;
        Confidence = confidence;
        TemporalScope = temporalScope;
    }

    public static Relationship Create(
        RelationshipId id,
        EntityId subjectEntityId,
        string relationType,
        EntityId objectEntityId,
        ValidationState validationState,
        IReadOnlyList<ProvenanceId> provenanceRefs,
        ConfidenceLevel? confidence = null,
        TemporalScope? temporalScope = null)
    {
        if (string.IsNullOrWhiteSpace(relationType))
        {
            throw new ArgumentException("RelationType must be non-empty.", nameof(relationType));
        }

        ArgumentNullException.ThrowIfNull(provenanceRefs);
        CanonicalValidation.RequireProvenanceWhenSupported(validationState, provenanceRefs, nameof(Relationship));

        return new Relationship(
            id,
            subjectEntityId,
            relationType.Trim(),
            objectEntityId,
            validationState,
            provenanceRefs,
            confidence,
            temporalScope);
    }
}
