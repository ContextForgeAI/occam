namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Opaque stable identifiers for the canonical knowledge domain (PR-A / ADR-0003).
/// Values are random hex strings (<c>Guid.N</c>) — not content-addressed, not display-name derived,
/// not array-position derived. Typed wrappers prevent mixing Source/Evidence/Fact ids at compile time.
/// </summary>
public static class CanonicalId
{
    public static string NewOpaque() => Guid.NewGuid().ToString("N");

    internal static string Require(string value, string paramName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Canonical id must be a non-empty opaque string.", paramName);
        }

        return value.Trim();
    }
}

public readonly record struct SourceId
{
    public string Value { get; }

    private SourceId(string value) => Value = value;

    public static SourceId New() => new(CanonicalId.NewOpaque());

    public static SourceId From(string value) => new(CanonicalId.Require(value, nameof(value)));

    public override string ToString() => Value;
}

public readonly record struct EvidenceId
{
    public string Value { get; }

    private EvidenceId(string value) => Value = value;

    public static EvidenceId New() => new(CanonicalId.NewOpaque());

    public static EvidenceId From(string value) => new(CanonicalId.Require(value, nameof(value)));

    public override string ToString() => Value;
}

public readonly record struct ProvenanceId
{
    public string Value { get; }

    private ProvenanceId(string value) => Value = value;

    public static ProvenanceId New() => new(CanonicalId.NewOpaque());

    public static ProvenanceId From(string value) => new(CanonicalId.Require(value, nameof(value)));

    public override string ToString() => Value;
}

public readonly record struct ClaimCandidateId
{
    public string Value { get; }

    private ClaimCandidateId(string value) => Value = value;

    public static ClaimCandidateId New() => new(CanonicalId.NewOpaque());

    public static ClaimCandidateId From(string value) => new(CanonicalId.Require(value, nameof(value)));

    public override string ToString() => Value;
}

public readonly record struct EntityId
{
    public string Value { get; }

    private EntityId(string value) => Value = value;

    public static EntityId New() => new(CanonicalId.NewOpaque());

    public static EntityId From(string value) => new(CanonicalId.Require(value, nameof(value)));

    public override string ToString() => Value;
}

public readonly record struct FactId
{
    public string Value { get; }

    private FactId(string value) => Value = value;

    public static FactId New() => new(CanonicalId.NewOpaque());

    public static FactId From(string value) => new(CanonicalId.Require(value, nameof(value)));

    public override string ToString() => Value;
}

public readonly record struct RelationshipId
{
    public string Value { get; }

    private RelationshipId(string value) => Value = value;

    public static RelationshipId New() => new(CanonicalId.NewOpaque());

    public static RelationshipId From(string value) => new(CanonicalId.Require(value, nameof(value)));

    public override string ToString() => Value;
}
