using System.Security.Cryptography;
using System.Text;
using OccamMcp.Core.Caching;
using OccamMcp.Core.Routing;

namespace OccamMcp.Core.Compile;

/// <summary>
/// Deterministic identity of a materialized representation (URL + options that change semantic
/// content). Clients store <c>materializationKey → contentHash</c>, not merely <c>URL → contentHash</c>,
/// so a focus/budget/playbook change is never confused with source-page drift.
/// </summary>
public static class MaterializationKey
{
    /// <summary>Bumped when the canonical field set or hashing codec changes.</summary>
    public const string SchemaVersion = "mat-v1";

    /// <summary>
    /// Returns <c>sha256:</c>-prefixed hex of the canonical materialization descriptor.
    /// </summary>
    public static string Compute(
        string url,
        string backendPolicy,
        OccamTranscodeOptions options,
        string? playbookId = null,
        string? playbookVersion = null,
        bool rankBlocks = false,
        bool tagTrust = false)
    {
        var canonical = new StringBuilder(512);
        canonical.Append("schema=").Append(SchemaVersion).Append('\n');
        canonical.Append(TranscodeCacheKey.NormalizeUrl(url)).Append('\n');
        canonical.Append("backend=").Append(backendPolicy ?? string.Empty).Append('\n');
        canonical.Append("max_tokens=").Append(options.MaxTokens?.ToString() ?? string.Empty).Append('\n');
        canonical.Append("fit_markdown=").Append(options.FitMarkdown ? '1' : '0').Append('\n');
        canonical.Append("focus_query=").Append(options.FocusQuery ?? string.Empty).Append('\n');
        canonical.Append("content_selectors=").Append(options.ContentSelectors.Length);
        foreach (var selector in options.ContentSelectors)
        {
            canonical.Append("\n  sel=").Append(selector);
        }

        canonical.Append('\n');
        canonical.Append("playbook_policy=").Append(options.PlaybookPolicy ?? string.Empty).Append('\n');
        canonical.Append("playbook_id=").Append(playbookId ?? string.Empty).Append('\n');
        canonical.Append("playbook_version=").Append(playbookVersion ?? string.Empty).Append('\n');
        canonical.Append("semantic_chunking=").Append(options.SemanticChunking ? '1' : '0').Append('\n');
        canonical.Append("json_blocks=").Append(options.JsonBlocks ? '1' : '0').Append('\n');
        canonical.Append("json_tables=").Append(options.JsonTables ? '1' : '0').Append('\n');
        canonical.Append("json_feed=").Append(options.JsonFeed ? '1' : '0').Append('\n');
        canonical.Append("translate_to=").Append(options.TranslateTo ?? string.Empty).Append('\n');
        canonical.Append("rank_blocks=").Append(rankBlocks ? '1' : '0').Append('\n');
        canonical.Append("tag_trust=").Append(tagTrust ? '1' : '0').Append('\n');
        // capture_screenshot affects sidecar presence, not markdown hash — still part of the envelope.
        canonical.Append("capture_screenshot=").Append(options.CaptureScreenshot ? '1' : '0');

        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString()));
        return "sha256:" + Convert.ToHexString(hash).ToLowerInvariant();
    }
}
