using System.Text.RegularExpressions;

namespace OccamMcp.Core.Compile;

/// <summary>
/// Opt-in markdown link compaction: keep visible link text, drop destination URLs.
/// Changes content bytes — callers must fold the flag into materialization / cache keys.
/// </summary>
public static partial class MarkdownLinkCompactor
{
    /// <summary>Replace <c>[text](url)</c> with <c>text</c> (or bare text when empty label).</summary>
    public static string Compact(string markdown)
    {
        if (string.IsNullOrEmpty(markdown))
        {
            return markdown;
        }

        return InlineLinkRegex().Replace(markdown, static m =>
        {
            var label = m.Groups[1].Value;
            return string.IsNullOrEmpty(label) ? m.Groups[2].Value : label;
        });
    }

    // Standard markdown inline links; does not handle reference-style or images (![alt](url)).
    [GeneratedRegex(@"\[([^\]]*)\]\(([^)\s]+)(?:\s+""[^""]*"")?\)", RegexOptions.CultureInvariant)]
    private static partial Regex InlineLinkRegex();
}
