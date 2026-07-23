using System.Text.Json.Serialization;

namespace OccamMcp.Core.Workers;

public sealed class WorkerExtractResponse
{
    [JsonPropertyName("ok")]
    public bool Ok { get; init; }

    [JsonPropertyName("backend")]
    public string? Backend { get; init; }

    [JsonPropertyName("markdown")]
    public string? Markdown { get; init; }

    [JsonPropertyName("latency_ms")]
    public int LatencyMs { get; init; }

    [JsonPropertyName("network_ms")]
    public int NetworkMs { get; init; }

    [JsonPropertyName("parse_ms")]
    public int ParseMs { get; init; }

    [JsonPropertyName("failure")]
    public string? Failure { get; init; }

    // Human-readable "why this failed" + an actionable fix, emitted by the browser worker for a
    // browser-availability failure (playwright_missing). Additive: null for every other failure.
    [JsonPropertyName("reason")]
    public string? Reason { get; init; }

    [JsonPropertyName("fix")]
    public WorkerExtractFixInfo? Fix { get; init; }

    // Branch-2 telemetry: present (on success) when this call auto-provisioned the browser binary.
    [JsonPropertyName("browser_provisioned")]
    public WorkerBrowserProvisionedInfo? BrowserProvisioned { get; init; }

    [JsonPropertyName("status_code")]
    public int StatusCode { get; init; }

    [JsonPropertyName("url")]
    public WorkerExtractUrlInfo? Url { get; init; }

    [JsonPropertyName("media_refs")]
    public WorkerMediaRefInfo[]? MediaRefs { get; init; }

    [JsonPropertyName("chunks")]
    public WorkerExtractChunkInfo[]? Chunks { get; init; }

    [JsonPropertyName("blocks")]
    public WorkerExtractBlockInfo[]? Blocks { get; init; }

    [JsonPropertyName("tables")]
    public WorkerExtractTableInfo[]? Tables { get; init; }

    [JsonPropertyName("feed")]
    public WorkerExtractFeedInfo? Feed { get; init; }

    [JsonPropertyName("meta")]
    public WorkerExtractMetaInfo? Meta { get; init; }

    [JsonPropertyName("access")]
    public WorkerAccessEvidenceInfo? Access { get; init; }

    [JsonPropertyName("screenshot")]
    public string? Screenshot { get; init; }

    // A3: true when a playbook overlay actually matched this host and shaped the extract (not merely
    // pushed). The receipt stamps PlaybookId/PlaybookVersion only when this is true — honest provenance.
    [JsonPropertyName("overlay_applied")]
    public bool OverlayApplied { get; init; }
}

public sealed class WorkerAccessEvidenceInfo
{
    [JsonPropertyName("has_authentication_challenge")]
    public bool HasAuthenticationChallenge { get; init; }

    [JsonPropertyName("redirected_to_login")]
    public bool RedirectedToLogin { get; init; }

    [JsonPropertyName("password_field")]
    public bool PasswordField { get; init; }

    [JsonPropertyName("identity_field")]
    public bool IdentityField { get; init; }

    [JsonPropertyName("login_form_action")]
    public bool LoginFormAction { get; init; }

    [JsonPropertyName("login_heading")]
    public bool LoginHeading { get; init; }

    [JsonPropertyName("blocking_overlay")]
    public bool BlockingOverlay { get; init; }

    [JsonPropertyName("has_usable_content")]
    public bool HasUsableContent { get; init; }

    [JsonPropertyName("authentication_terminology")]
    public bool AuthenticationTerminology { get; init; }
}

public sealed class WorkerExtractMetaInfo
{
    [JsonPropertyName("publishedAt")]
    public string? PublishedAt { get; init; }

    [JsonPropertyName("author")]
    public string? Author { get; init; }

    [JsonPropertyName("lang")]
    public string? Lang { get; init; }

    [JsonPropertyName("canonical")]
    public string? Canonical { get; init; }
}

public sealed class WorkerExtractBlockInfo
{
    [JsonPropertyName("type")]
    public string Type { get; init; } = "";

    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("links")]
    public WorkerExtractBlockLink[] Links { get; init; } = [];

    [JsonPropertyName("source_selector")]
    public string SourceSelector { get; init; } = "";

    // PR-3 part 2: heading level (h1..h6 → 1..6) so a codec can rebuild the heading hierarchy instead of
    // flattening. Emitted by the worker only for headings; null for every other block type.
    [JsonPropertyName("level")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Level { get; init; }

    // #3 span-substrate (rank_blocks): 0–1 relevance of this block to focus_query (BM25, normalized to
    // the top block). Set host-side after extraction; omitted (null) unless ranking was requested — an
    // explicit per-span attention signal so the consuming LLM knows which blocks to weight and cite.
    [JsonPropertyName("salience")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public double? Salience { get; set; }

    // #4 trust-channels (tag_trust): machine-checkable provenance channel — "suspicious" (the text
    // reads like an instruction to the reader/model → possible prompt-injection) or "boilerplate"
    // (a non-content region). Normal main content is null (omitted). Lets a harness hard-isolate
    // untrusted spans instead of trusting all extracted text equally.
    [JsonPropertyName("trust")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Trust { get; set; }
}

public sealed class WorkerExtractBlockLink
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("href")]
    public string Href { get; init; } = "";
}

public sealed class WorkerExtractTableInfo
{
    [JsonPropertyName("caption")]
    public string Caption { get; init; } = "";

    [JsonPropertyName("headers")]
    public string[] Headers { get; init; } = [];

    [JsonPropertyName("rows")]
    public string[][] Rows { get; init; } = [];

    [JsonPropertyName("source_selector")]
    public string SourceSelector { get; init; } = "";

    /// <summary>
    /// Semantic row reconstructions (e.g. HN title+subtext → one object). Omitted when the table
    /// is a plain grid with no paired-row pattern. Physical <see cref="Rows"/> stay unchanged.
    /// </summary>
    [JsonPropertyName("records")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkerExtractTableRecordInfo[]? Records { get; init; }
}

/// <summary>One semantic knowledge object reconstructed from one or more physical table rows.</summary>
public sealed class WorkerExtractTableRecordInfo
{
    [JsonPropertyName("rank")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Rank { get; init; }

    [JsonPropertyName("title")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Title { get; init; }

    [JsonPropertyName("url")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Url { get; init; }

    [JsonPropertyName("site")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Site { get; init; }

    [JsonPropertyName("author")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Author { get; init; }

    [JsonPropertyName("points")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Points { get; init; }

    [JsonPropertyName("comments")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public int? Comments { get; init; }

    [JsonPropertyName("age")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Age { get; init; }

    [JsonPropertyName("schema")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? Schema { get; init; }

    [JsonPropertyName("provenance")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public WorkerExtractTableRowProvenanceInfo? Provenance { get; init; }
}

public sealed class WorkerExtractTableRowProvenanceInfo
{
    [JsonPropertyName("source_selector")]
    public string SourceSelector { get; init; } = "";

    [JsonPropertyName("row_indexes")]
    public int[] RowIndexes { get; init; } = [];

    [JsonPropertyName("table_selector")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? TableSelector { get; init; }
}

public sealed class WorkerExtractFeedInfo
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("items")]
    public WorkerExtractFeedItemInfo[] Items { get; init; } = [];
}

public sealed class WorkerExtractFeedItemInfo
{
    [JsonPropertyName("title")]
    public string Title { get; init; } = "";

    [JsonPropertyName("link")]
    public string Link { get; init; } = "";

    [JsonPropertyName("publishedAt")]
    public string PublishedAt { get; init; } = "";

    /// <summary>Compat alias of <see cref="SummaryText"/> (plain text, no HTML).</summary>
    [JsonPropertyName("summary")]
    public string Summary { get; init; } = "";

    /// <summary>Source HTML when the feed entry carried markup; empty for plain-text summaries.</summary>
    [JsonPropertyName("summaryHtml")]
    public string SummaryHtml { get; init; } = "";

    /// <summary>Plain text summary — tags stripped, entities decoded.</summary>
    [JsonPropertyName("summaryText")]
    public string SummaryText { get; init; } = "";

    /// <summary>Clean markdown summary — no raw HTML tags.</summary>
    [JsonPropertyName("summaryMarkdown")]
    public string SummaryMarkdown { get; init; } = "";
}

public sealed class WorkerExtractChunkInfo
{
    [JsonPropertyName("text")]
    public string Text { get; init; } = "";

    [JsonPropertyName("headers")]
    public string[] Headers { get; init; } = [];
}

public sealed class WorkerMediaRefInfo
{
    [JsonPropertyName("url")]
    public string Url { get; init; } = "";

    [JsonPropertyName("kind")]
    public string Kind { get; init; } = "";

    [JsonPropertyName("alt")]
    public string? Alt { get; init; }

    [JsonPropertyName("context_heading")]
    public string? ContextHeading { get; init; }

    [JsonPropertyName("selector_hint")]
    public string? SelectorHint { get; init; }
}

public sealed class WorkerExtractUrlInfo
{
    [JsonPropertyName("requested")]
    public string? Requested { get; init; }

    [JsonPropertyName("final")]
    public string? Final { get; init; }
}

/// <summary>Branch-2 auto-provision telemetry: occam downloaded the user-level browser on this call.</summary>
public sealed class WorkerBrowserProvisionedInfo
{
    [JsonPropertyName("installed")]
    public bool Installed { get; init; }

    [JsonPropertyName("channel")]
    public string? Channel { get; init; }

    [JsonPropertyName("path")]
    public string? Path { get; init; }

    [JsonPropertyName("tookMs")]
    public int TookMs { get; init; }
}

/// <summary>B6: the browser worker's own answer to "would I auto-provision chromium?", printed by
/// workers/browser-extract/lib/provision-gate.mjs. The gate rule lives only there; C# asks instead of
/// mirroring it (see FeatureDiscoveryService.WillAutoProvisionBrowser).</summary>
public sealed class ProvisionGateProbeResponse
{
    [JsonPropertyName("will_provision")]
    public bool WillProvision { get; init; }
}

/// <summary>An actionable remedy attached to a browser-availability failure: which command fixes it and
/// whether that command needs root (the boundary occam can/can't cross on the user's behalf).</summary>
public sealed class WorkerExtractFixInfo
{
    [JsonPropertyName("kind")]
    public string? Kind { get; init; }

    [JsonPropertyName("command")]
    public string? Command { get; init; }

    [JsonPropertyName("root_required")]
    public bool RootRequired { get; init; }
}

[JsonSerializable(typeof(WorkerExtractResponse))]
[JsonSerializable(typeof(ProvisionGateProbeResponse))]
[JsonSerializable(typeof(WorkerBrowserProvisionedInfo))]
[JsonSerializable(typeof(WorkerExtractFixInfo))]
[JsonSerializable(typeof(WorkerExtractChunkInfo))]
[JsonSerializable(typeof(WorkerExtractChunkInfo[]))]
[JsonSerializable(typeof(WorkerExtractBlockInfo))]
[JsonSerializable(typeof(WorkerExtractBlockInfo[]))]
[JsonSerializable(typeof(WorkerExtractBlockLink))]
[JsonSerializable(typeof(WorkerExtractBlockLink[]))]
[JsonSerializable(typeof(WorkerExtractTableInfo))]
[JsonSerializable(typeof(WorkerExtractTableInfo[]))]
[JsonSerializable(typeof(WorkerExtractTableRecordInfo))]
[JsonSerializable(typeof(WorkerExtractTableRecordInfo[]))]
[JsonSerializable(typeof(WorkerExtractTableRowProvenanceInfo))]
[JsonSerializable(typeof(WorkerExtractFeedInfo))]
[JsonSerializable(typeof(WorkerExtractFeedItemInfo))]
[JsonSerializable(typeof(WorkerExtractFeedItemInfo[]))]
[JsonSerializable(typeof(WorkerAccessEvidenceInfo))]
[JsonSerializable(typeof(WorkerExtractMetaInfo))]
internal partial class WorkerExtractJsonContext : JsonSerializerContext;
