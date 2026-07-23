namespace OccamMcp.Core.Knowledge.Canonical;

/// <summary>
/// Extensible pointer into a <see cref="Source"/>. PR-A carries a kind + primary value + optional
/// attributes; concrete shapes (text span offsets, table cell coords, JSON paths, …) land as
/// attribute conventions later without changing this record.
/// </summary>
public sealed record EvidenceLocator(
    EvidenceLocatorKind Kind,
    string Value,
    IReadOnlyDictionary<string, string>? Attributes = null)
{
    public static EvidenceLocator SourceSelector(string selector) =>
        new(EvidenceLocatorKind.SourceSelector, RequireValue(selector));

    public static EvidenceLocator Unspecified(string? value = null) =>
        new(EvidenceLocatorKind.Unspecified, string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim());

    public static EvidenceLocator Custom(string value, IReadOnlyDictionary<string, string>? attributes = null) =>
        new(EvidenceLocatorKind.Custom, RequireValue(value), attributes);

    private static string RequireValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Evidence locator value must be non-empty.", nameof(value));
        }

        return value.Trim();
    }
}
