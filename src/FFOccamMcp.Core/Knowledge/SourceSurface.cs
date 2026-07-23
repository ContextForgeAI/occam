namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Opaque, representation-tagged extracted surface. Codecs that understand the media type may
/// serialize it; Canonical Knowledge records never embed this type.
/// </summary>
public sealed record SourceSurface(string MediaType, string Text)
{
    public const string MarkdownMediaType = "text/markdown";

    public static SourceSurface Markdown(string text) =>
        new(MarkdownMediaType, text ?? string.Empty);

    public bool IsMarkdown =>
        string.Equals(MediaType, MarkdownMediaType, StringComparison.OrdinalIgnoreCase);
}
