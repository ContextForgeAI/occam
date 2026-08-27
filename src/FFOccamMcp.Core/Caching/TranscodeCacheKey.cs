using System.Security.Cryptography;
using System.Text;
using OccamMcp.Core.Compile;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Caching;

/// <summary>
/// Computes the on-disk cache key for a transcode request. The key folds in every input that
/// affects the produced markdown so that different options never collide onto the same entry.
/// </summary>
public static class TranscodeCacheKey
{
    /// <summary>
    /// Builds a deterministic sha256-hex key from the request URL, backend selection, and all
    /// output-affecting <see cref="OccamTranscodeOptions"/>. The URL is normalized (lowercased
    /// scheme+host, default port stripped) so trivial spelling differences hit the same entry;
    /// path and query are preserved verbatim. The URL fragment is keyed separately via
    /// <see cref="FocusIntent"/> because it drives local focus (EF-045) even though it is
    /// stripped from the fetch URL.
    /// </summary>
    public static string Compute(
        string url,
        string backendPolicy,
        OccamTranscodeOptions options,
        bool rankBlocks = false,
        bool tagTrust = false,
        bool emitCapsule = false)
    {
        var focusFragment = FocusIntent.FromUrl(url).Fragment
            ?? options.FocusFragment;
        var canonical = new StringBuilder();
        canonical.Append(NormalizeUrl(url)).Append('\n');
        canonical.Append("backend=").Append(backendPolicy ?? string.Empty).Append('\n');
        canonical.Append("max_tokens=").Append(options.MaxTokens?.ToString() ?? string.Empty).Append('\n');
        canonical.Append("fit_markdown=").Append(options.FitMarkdown ? '1' : '0').Append('\n');
        canonical.Append("focus_query=").Append(options.FocusQuery ?? string.Empty).Append('\n');
        canonical.Append("focus_fragment=").Append(focusFragment ?? string.Empty).Append('\n');
        // Length-prefixed + newline-framed so selector text can never forge a key collision.
        canonical.Append("content_selectors=").Append(options.ContentSelectors.Length);
        foreach (var selector in options.ContentSelectors)
        {
            canonical.Append("\n  sel=").Append(selector);
        }
        canonical.Append('\n');
        canonical.Append("playbook_policy=").Append(options.PlaybookPolicy ?? string.Empty).Append('\n');
        canonical.Append("semantic_chunking=").Append(options.SemanticChunking ? '1' : '0').Append('\n');
        canonical.Append("capture_screenshot=").Append(options.CaptureScreenshot ? '1' : '0').Append('\n');
        canonical.Append("json_blocks=").Append(options.JsonBlocks ? '1' : '0').Append('\n');
        canonical.Append("json_tables=").Append(options.JsonTables ? '1' : '0').Append('\n');
        canonical.Append("json_feed=").Append(options.JsonFeed ? '1' : '0').Append('\n');
        canonical.Append("translate_to=").Append(options.TranslateTo ?? string.Empty).Append('\n');
        // Output-affecting post-options must split identity (EF-001 / cache collision).
        canonical.Append("rank_blocks=").Append(rankBlocks ? '1' : '0').Append('\n');
        canonical.Append("tag_trust=").Append(tagTrust ? '1' : '0').Append('\n');
        canonical.Append("emit_capsule=").Append(emitCapsule ? '1' : '0').Append('\n');
        canonical.Append("toc=").Append(options.EmitToc ? '1' : '0').Append('\n');
        canonical.Append("section=").Append(options.Section ?? string.Empty).Append('\n');
        canonical.Append("must_contain=").Append(options.MustContain ?? string.Empty);

        var bytes = Encoding.UTF8.GetBytes(canonical.ToString());
        var hash = SHA256.HashData(bytes);
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    /// <summary>
    /// Normalizes a URL for cache keying (scheme+host lowercased, default port stripped,
    /// fragment omitted — fragment is folded separately as focus_fragment). Falls back to the
    /// trimmed raw string if the URL does not parse (defensive; unparseable URLs fail preflight).
    /// </summary>
    internal static string NormalizeUrl(string url)
    {
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
        {
            return (url ?? string.Empty).Trim();
        }

        var scheme = uri.Scheme.ToLowerInvariant();
        var host = uri.Host.ToLowerInvariant();
        var builder = new StringBuilder();
        builder.Append(scheme).Append("://").Append(host);
        if (!uri.IsDefaultPort)
        {
            builder.Append(':').Append(uri.Port);
        }

        builder.Append(uri.PathAndQuery);
        return builder.ToString();
    }
}
