namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Normalized assertion accepted into the canonical layer with an explicit validation state.
/// Typed subject/predicate/object — not a free-form string bag, not a full RDF clone.
/// <see cref="ValidationState.Supported"/> requires at least one provenance ref (constructor invariant).
/// </summary>
public sealed record Fact
{
    public FactId Id { get; }
    public string Subject { get; }
    public string Predicate { get; }
    public string Object { get; }
    public ClaimKind ClaimKind { get; }
    public ValidationState ValidationState { get; }
    public IReadOnlyList<ProvenanceId> ProvenanceRefs { get; }
    public ConfidenceLevel? Confidence { get; }
    public SemanticType? SemanticType { get; }
    public TemporalScope? TemporalScope { get; }

    private Fact(
        FactId id,
        string subject,
        string predicate,
        string @object,
        ClaimKind claimKind,
        ValidationState validationState,
        IReadOnlyList<ProvenanceId> provenanceRefs,
        ConfidenceLevel? confidence,
        SemanticType? semanticType,
        TemporalScope? temporalScope)
    {
        Id = id;
        Subject = subject;
        Predicate = predicate;
        Object = @object;
        ClaimKind = claimKind;
        ValidationState = validationState;
        ProvenanceRefs = provenanceRefs;
        Confidence = confidence;
        SemanticType = semanticType;
        TemporalScope = temporalScope;
    }

    public static Fact Create(
        FactId id,
        string subject,
        string predicate,
        string @object,
        ClaimKind claimKind,
        ValidationState validationState,
        IReadOnlyList<ProvenanceId> provenanceRefs,
        ConfidenceLevel? confidence = null,
        SemanticType? semanticType = null,
        TemporalScope? temporalScope = null)
    {
        if (string.IsNullOrWhiteSpace(subject))
        {
            throw new ArgumentException("Fact subject must be non-empty.", nameof(subject));
        }

        if (string.IsNullOrWhiteSpace(predicate))
        {
            throw new ArgumentException("Fact predicate must be non-empty.", nameof(predicate));
        }

        if (string.IsNullOrWhiteSpace(@object))
        {
            throw new ArgumentException("Fact object/value must be non-empty.", nameof(@object));
        }

        ArgumentNullException.ThrowIfNull(provenanceRefs);
        CanonicalValidation.RequireProvenanceWhenSupported(validationState, provenanceRefs, nameof(Fact));

        return new Fact(
            id,
            subject.Trim(),
            predicate.Trim(),
            @object.Trim(),
            claimKind,
            validationState,
            provenanceRefs,
            confidence,
            semanticType,
            temporalScope);
    }
}
