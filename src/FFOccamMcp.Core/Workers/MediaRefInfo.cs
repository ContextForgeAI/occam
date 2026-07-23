namespace OccamMcp.Core.Workers;

public sealed record MediaRefInfo(
    string Url,
    string Kind,
    string? Alt,
    string? ContextHeading,
    string? SelectorHint);
