namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Minimal identifiable knowledge subject. No entity-resolution engine, no mandatory global name
/// uniqueness. Aliases are local hints only.
/// </summary>
public sealed record Entity
{
    public EntityId Id { get; }
    public SemanticType SemanticType { get; }
    public string? CanonicalName { get; }
    public IReadOnlyList<string> Aliases { get; }
    public IReadOnlyDictionary<string, string> Attributes { get; }

    private Entity(
        EntityId id,
        SemanticType semanticType,
        string? canonicalName,
        IReadOnlyList<string> aliases,
        IReadOnlyDictionary<string, string> attributes)
    {
        Id = id;
        SemanticType = semanticType;
        CanonicalName = canonicalName;
        Aliases = aliases;
        Attributes = attributes;
    }

    public static Entity Create(
        EntityId id,
        SemanticType semanticType,
        string? canonicalName = null,
        IReadOnlyList<string>? aliases = null,
        IReadOnlyDictionary<string, string>? attributes = null)
    {
        return new Entity(
            id,
            semanticType,
            string.IsNullOrWhiteSpace(canonicalName) ? null : canonicalName.Trim(),
            aliases ?? CanonicalEmpty.Strings,
            attributes ?? CanonicalEmpty.Metadata);
    }
}
