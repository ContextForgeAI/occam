namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Optional valid-time window for a Fact/Relationship. PR-A deliberately does NOT model full
/// bitemporal (valid vs ingest) history — only leaves room for it. All fields optional.
/// </summary>
public sealed record TemporalScope(
    DateTimeOffset? ValidFrom = null,
    DateTimeOffset? ValidTo = null)
{
    public static readonly TemporalScope Unknown = new();
}
