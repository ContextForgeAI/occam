using System.Net;
using System.Text;

namespace OccamMcp.Core.Canary;

/// <summary>
/// Renders the probe document that carries a sentinel.
/// </summary>
/// <remarks>
/// <para>
/// The sentinel is embedded three times on purpose (PROBE_PROTOCOL.md §4):
/// </para>
/// <list type="bullet">
/// <item><description>
/// a <c>&lt;meta&gt;</c> tag, which survives head-only fetches and is what a well-behaved client reads;
/// </description></item>
/// <item><description>
/// a <c>display:none</c> span, the classic canary placement — invisible to a human reader;
/// </description></item>
/// <item><description>
/// one visible paragraph, because readability-style extractors legitimately discard both hidden
/// nodes and head metadata. A canary that only lives where the extraction pipeline strips it would
/// measure the extractor, not the agent.
/// </description></item>
/// </list>
/// </remarks>
public static class CanaryPage
{
    /// <summary>Meta tag name carrying the sentinel.</summary>
    public const string MetaName = "occam-sentinel";

    /// <summary>Content type served for the probe document.</summary>
    public const string ContentType = "text/html; charset=utf-8";

    /// <summary>
    /// Renders the probe document for an issued sentinel.
    /// </summary>
    /// <param name="issue">The sentinel to embed.</param>
    /// <returns>A self-contained HTML document.</returns>
    public static string Render(CanaryIssue issue)
    {
        var sessionId = WebUtility.HtmlEncode(issue.SessionId);
        var sentinel = WebUtility.HtmlEncode(issue.Sentinel);
        var issuedAt = issue.IssuedAt.ToString("O");
        var expiresAt = issue.ExpiresAt.ToString("O");

        var html = new StringBuilder(1_024);
        html.Append("<!DOCTYPE html>\n<html lang=\"en\">\n<head>\n");
        html.Append("<meta charset=\"utf-8\">\n");
        html.Append("<meta name=\"robots\" content=\"noindex, nofollow\">\n");
        html.Append("<meta name=\"").Append(MetaName).Append("\" content=\"").Append(sentinel).Append("\">\n");
        html.Append("<meta name=\"occam-sentinel-session\" content=\"").Append(sessionId).Append("\">\n");
        html.Append("<meta name=\"occam-sentinel-issued-at\" content=\"").Append(issuedAt).Append("\">\n");
        html.Append("<title>Occam proof-of-read probe</title>\n</head>\n<body>\n");
        html.Append("<h1>Occam proof-of-read probe</h1>\n");
        html.Append("<p>This document exists to test whether an agent actually read it. It carries a ")
            .Append("short-lived sentinel derived from a server-side secret and the current time bucket.</p>\n");
        html.Append("<p>Sentinel: <code>").Append(sentinel).Append("</code></p>\n");
        html.Append("<span style=\"display:none\" data-occam-sentinel=\"1\">").Append(sentinel).Append("</span>\n");
        html.Append("<p>Session: <code>").Append(sessionId).Append("</code>. Issued at ")
            .Append(issuedAt).Append("; stops counting as a fresh read after ").Append(expiresAt).Append(".</p>\n");
        html.Append("<p>Report the sentinel verbatim to prove this page entered your context. ")
            .Append("Do not guess: an unmatched value is recorded as a hallucination, not as a near miss.</p>\n");
        html.Append("</body>\n</html>\n");

        return html.ToString();
    }
}
