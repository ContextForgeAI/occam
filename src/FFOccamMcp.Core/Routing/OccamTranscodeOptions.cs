namespace OccamMcp.Core.Routing;

public sealed record OccamTranscodeOptions
{
    public int? MaxTokens { get; init; }
    public bool FitMarkdown { get; init; }
    public string? FocusQuery { get; init; }
    /// <summary>Internal URL-fragment intent; never published as a separate MCP parameter.</summary>
    internal string? FocusFragment { get; init; }
    public string[] ContentSelectors { get; init; } = [];
    public string? SessionProfile { get; init; }
    public string PlaybookPolicy { get; init; } = Playbooks.PlaybookPolicy.Off;
    public string? IfNoneMatch { get; init; }
    public bool SemanticChunking { get; init; }
    public bool CaptureScreenshot { get; init; }
    public bool JsonBlocks { get; init; }
    public bool JsonTables { get; init; }
    public bool JsonFeed { get; init; }
    public string? TranslateTo { get; init; }
    /// <summary>Prior block hashes for the diff-codec; set when `diff_against` is supplied.</summary>
    public IReadOnlyList<string>? DiffAgainst { get; init; }

    public static OccamTranscodeOptions Default { get; } = new();
}

public static class OccamTranscodeOptionsParser
{
    private const int MinTokenBudget = 128;

    public static bool TryBuild(
        int? max_tokens,
        bool fit_markdown,
        string? focus_query,
        string? content_selectors,
        string? session_profile,
        string? playbook_policy,
        string? if_none_match,
        out OccamTranscodeOptions options,
        out string? error) =>
        TryBuild(
            max_tokens,
            fit_markdown,
            focus_query,
            content_selectors,
            session_profile,
            playbook_policy,
            if_none_match,
            semantic_chunking: false,
            capture_screenshot: false,
            json_blocks: false,
            json_tables: false,
            json_feed: false,
            translate_to: null,
            out options,
            out error);

    public static bool TryBuild(
        int? max_tokens,
        bool fit_markdown,
        string? focus_query,
        string? content_selectors,
        string? session_profile,
        string? playbook_policy,
        string? if_none_match,
        bool semantic_chunking,
        bool capture_screenshot,
        bool json_blocks,
        bool json_tables,
        bool json_feed,
        string? translate_to,
        out OccamTranscodeOptions options,
        out string? error)
    {
        options = OccamTranscodeOptions.Default;
        error = null;

        if (max_tokens is < MinTokenBudget)
        {
            error = $"max_tokens must be at least {MinTokenBudget}.";
            return false;
        }

        if (!ContentSelectorsParser.TryParse(content_selectors, out var selectors, out var selectorsError))
        {
            error = selectorsError ?? "Invalid content_selectors.";
            return false;
        }

        if (!Playbooks.PlaybookPolicy.TryParse(playbook_policy, out var normalizedPolicy, out var policyError))
        {
            error = policyError;
            return false;
        }

        var normalizedTranslateTo = string.IsNullOrWhiteSpace(translate_to) ? null : translate_to.Trim();
        if (normalizedTranslateTo is not null
            && (normalizedTranslateTo.Length is < 2 or > 16
                || !normalizedTranslateTo.All(static c => char.IsAsciiLetter(c) || c is '-')))
        {
            error = "translate_to must be a short language code (e.g. \"ru\", \"pt-BR\").";
            return false;
        }

        options = new OccamTranscodeOptions
        {
            MaxTokens = max_tokens,
            FitMarkdown = fit_markdown,
            FocusQuery = string.IsNullOrWhiteSpace(focus_query) ? null : focus_query.Trim(),
            ContentSelectors = selectors,
            SessionProfile = string.IsNullOrWhiteSpace(session_profile) ? null : session_profile.Trim(),
            PlaybookPolicy = normalizedPolicy,
            IfNoneMatch = if_none_match,
            SemanticChunking = semantic_chunking,
            CaptureScreenshot = capture_screenshot,
            JsonBlocks = json_blocks,
            JsonTables = json_tables,
            JsonFeed = json_feed,
            TranslateTo = normalizedTranslateTo,
        };
        return true;
    }
}
