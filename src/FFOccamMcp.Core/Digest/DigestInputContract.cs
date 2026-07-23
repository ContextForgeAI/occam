using System.Text.Json;

namespace OccamMcp.Core.Digest;

/// <summary>
/// MCP input contract for <c>occam_digest</c>: callers must supply <c>urls</c> and/or
/// <c>source_url</c>. When <c>source_url</c> is set, <c>urls</c> is ignored (AF-5). Neither →
/// typed <c>invalid_arguments</c>. Pure / network-free — gate-testable.
/// </summary>
public static class DigestInputContract
{
    public const string NeitherMessage =
        "Provide urls and/or source_url (at least one). When source_url is set, urls is ignored.";

    public const string EmptyDiscoveryMessage =
        "source_url did not yield any discoverable links.";

    /// <summary>
    /// Validates the urls / source_url pair before fetch. <paramref name="useSourceUrl"/> is true
    /// when discovery must run (and urls must be ignored).
    /// </summary>
    public static bool TryValidate(
        JsonElement? urls,
        string? sourceUrl,
        out bool useSourceUrl,
        out string? failureCode,
        out string? failureMessage)
    {
        useSourceUrl = false;
        failureCode = null;
        failureMessage = null;

        var hasUrls = urls is { ValueKind: not JsonValueKind.Null and not JsonValueKind.Undefined };
        var hasSource = !string.IsNullOrWhiteSpace(sourceUrl);

        if (!hasUrls && !hasSource)
        {
            failureCode = "invalid_arguments";
            failureMessage = NeitherMessage;
            return false;
        }

        useSourceUrl = hasSource;
        return true;
    }
}
