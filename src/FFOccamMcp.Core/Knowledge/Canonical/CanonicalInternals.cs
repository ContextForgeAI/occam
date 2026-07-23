namespace OccamMcp.Core.Knowledge.Canonical;

internal static class CanonicalEmpty
{
    public static readonly IReadOnlyDictionary<string, string> Metadata =
        new Dictionary<string, string>(0);

    public static readonly IReadOnlyList<string> Strings = Array.Empty<string>();
}

internal static class CanonicalValidation
{
    public static void RequireProvenanceWhenSupported(
        ValidationState state,
        IReadOnlyList<ProvenanceId> provenanceRefs,
        string typeName)
    {
        if (state == ValidationState.Supported && provenanceRefs.Count == 0)
        {
            throw new ArgumentException(
                $"{typeName} with ValidationState.Supported requires at least one ProvenanceRef.",
                nameof(provenanceRefs));
        }
    }
}
