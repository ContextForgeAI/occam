using OccamMcp.Core.Compile;

namespace OccamMcp.Core.Routing;

public sealed record TranscodeCompileResult(
    string Markdown,
    bool SelectorsMatched,
    bool Truncated,
    int TokensEstimated,
    string? TruncationStrategy = null,
    OmittedManifest? Omitted = null);

public static class TranscodeCompiler
{
    public static TranscodeCompileResult Apply(string markdown, OccamTranscodeOptions options)
    {
        var text = markdown;
        var selectorsMatched = true;

        if (options.ContentSelectors.Length > 0)
        {
            var filtered = MarkdownContentFilter.ApplyWithMeta(text, options.ContentSelectors);
            text = filtered.Text;
            selectorsMatched = filtered.SelectorsMatched;
        }

        var source = text;
        if (options.FitMarkdown)
        {
            text = FitMarkdown.Apply(text, options.FocusQuery ?? options.FocusFragment);
        }

        var preBudgetText = text;
        var tokensBeforeBudget = TokenEstimator.Estimate(text);
        var (truncatedText, truncated, truncationStrategy) = TokenBudget.Apply(
            text,
            options.MaxTokens,
            options.FocusQuery,
            options.FocusFragment);
        text = TokenBudget.PreserveDefinitionalAnchor(
            source,
            truncatedText,
            options.MaxTokens,
            options.FocusQuery);
        var tokens = TokenEstimator.Estimate(text);

        // #7: surface a machine-readable manifest of what budgeting dropped, so the consumer never
        // mistakes a truncated body for the whole page.
        var omitted = OmittedManifestBuilder.Build(
            preBudgetText, text, truncated, truncationStrategy, tokensBeforeBudget, tokens);

        return new TranscodeCompileResult(
            text,
            selectorsMatched,
            truncated,
            tokens,
            truncationStrategy,
            omitted);
    }
}
