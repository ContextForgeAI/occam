namespace OccamMcp.Core.Knowledge;

/// <summary>
/// Semantic inputs for the Materialization Planner. Serialization / media type is the codec's job —
/// this request never names a codec syntax.
/// <para>
/// <see cref="MaxTokens"/> is the <b>surface</b> budget (markdown + document IR + Canonical claim
/// share) — not the public whole-response <c>max_tokens</c>. Live path maps via
/// <c>Compile.BudgetOwnership</c> before <c>Plan</c>.
/// </para>
/// </summary>
public sealed record MaterializationRequest(
    int? MaxTokens = null,
    string? FocusQuery = null,
    bool FitMarkdown = false,
    IReadOnlyList<string>? ContentSelectors = null,
    bool ExposePublicBlocks = false,
    bool ExposePublicTables = false,
    bool ExposePublicChunks = false,
    bool ExposePublicMedia = true,
    bool ExposePublicFeed = false,
    bool ExposePublicScreenshot = false,
    string? CapabilityProfile = null,
    string? ProvenancePolicy = null,
    string? DisclosurePolicy = null,
    string? FocusFragment = null)
{
    public static readonly MaterializationRequest None = new();

    public static MaterializationRequest FromTranscodeOptions(Routing.OccamTranscodeOptions options) =>
        new(
            MaxTokens: options.MaxTokens,
            FocusQuery: options.FocusQuery,
            FocusFragment: options.FocusFragment,
            FitMarkdown: options.FitMarkdown,
            ContentSelectors: options.ContentSelectors.Length > 0 ? options.ContentSelectors : null,
            ExposePublicBlocks: options.JsonBlocks || options.DiffAgainst is not null,
            ExposePublicTables: options.JsonTables,
            ExposePublicChunks: options.SemanticChunking,
            ExposePublicMedia: true,
            ExposePublicFeed: options.JsonFeed,
            ExposePublicScreenshot: options.CaptureScreenshot,
            CapabilityProfile: "default",
            ProvenancePolicy: "default",
            DisclosurePolicy: "default");

    public MaterializationPolicy ToPolicy() => new(MaxTokens, FocusQuery, ProvenancePolicy);

    public Routing.OccamTranscodeOptions ToCompileOptions() => new()
    {
        MaxTokens = MaxTokens,
        FocusQuery = FocusQuery,
        FocusFragment = FocusFragment,
        FitMarkdown = FitMarkdown,
        ContentSelectors = ContentSelectors is { Count: > 0 } ? [.. ContentSelectors] : [],
    };
}
