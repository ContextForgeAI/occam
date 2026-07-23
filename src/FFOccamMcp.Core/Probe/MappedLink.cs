namespace OccamMcp.Core.Probe;

/// <summary>
/// Same-domain link discovered by map / digest auto-discovery.
/// <paramref name="Title"/> is anchor text; <paramref name="Description"/> is optional meta;
/// <paramref name="Context"/> is neighboring plain text around the anchor (homepage HTML).
/// </summary>
public sealed record MappedLink(
    string Url,
    string? Title,
    string Path,
    string? Description = null,
    string? Context = null);
