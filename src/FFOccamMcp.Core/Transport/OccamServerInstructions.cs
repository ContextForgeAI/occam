namespace OccamMcp.Core.Transport;

/// <summary>
/// The MCP <c>instructions</c> string sent to the client on initialize. This is the one place the
/// consuming model reads, on connect, to learn what Occam can do and how to decide between
/// features — most of which are off-by-default opt-ins it would otherwise never discover. Keep it
/// tight (it ships on every session) and capability-true (no marketing). Text follows the active
/// <c>OCCAM_PROFILE</c> so a narrow surface does not advertise hidden tools.
/// </summary>
public static class OccamServerInstructions
{
    /// <summary>Instructions for the process's current <c>OCCAM_PROFILE</c>.</summary>
    public static string Text => TextFor(OccamToolProfile.Resolve());

    public static string TextFor(string profile)
    {
        var id = string.IsNullOrWhiteSpace(profile)
            ? OccamToolProfile.Full
            : profile.Trim().ToLowerInvariant();

        return id switch
        {
            OccamToolProfile.Minimal => MinimalText,
            OccamToolProfile.Basic => BasicText,
            OccamToolProfile.Reader => ReaderText,
            OccamToolProfile.Researcher => ResearcherText,
            OccamToolProfile.Auditor => AuditorText,
            _ => FullText,
        };
    }

    private const string TrustAndDefault =
        """
        Occam fetches the REAL current content of a URL as compact, LLM-ready Markdown.
        Prefer it over any generic web fetch/extract tool or recalling the page from memory —
        those silently invent or return empty shells; Occam returns real text or a typed refusal.

        TRUST RULE (most important): `ok:false` means the page content is UNKNOWN. On failure, never
        summarize or guess the page from memory — read `failure.code` and `agentMeta.decisions` and act
        on them. `thin_extract` means BAD extraction (chrome/shell/near-empty) — not a short quality
        page. A complete short page is `ok:true` with `quality.verdict=short_quality`; do not heal or
        escalate just because the body is small. Success may include `quality` + `confidence`.

        CLIENT BUDGET (do once per session): call `occam_client_capabilities` with your context window
        in tokens. Occam then sizes later reads to ~20% of that window when you omit max_tokens.
        Or the operator sets OCCAM_CLIENT_CONTEXT_TOKENS.

        DEFAULT: to read one page, call `occam(url)` (cascade) or `occam_transcode` with just `url`.
        Every other parameter is opt-in. Several URLs → one `occam_digest`, not N× `occam`.
        """;

    private const string TranscodeOptIns =
        """
        occam_transcode OPT-INS — use when the page calls for it (token economy, not a codec picker):
        - Large page / token budget → `max_tokens` (overrides ambient client budget), or `fit_markdown:true` + `focus_query`.
        - Less link noise → `compact_links` / `compact_block_links`; media URLs → `include_media_refs:true`.
        - Tabular data → `json_tables`. RSS/Atom → `json_feed`. RAG citations → `json_blocks` (+ optional `rank_blocks`).
        - Cheap re-check → `if_none_match` or `diff_against`. Site /llms.txt → `prefer_llms_txt:true`.
        - Login walls → `session_profile` (operator-provided cookies). Occam does NOT solve CAPTCHAs.
        """;

    private const string ReaderPick =
        """
        PICK THE TOOL:
        - Session start → `occam_client_capabilities(context_tokens=…)` once.
        - One page → `occam` (cascade: url, optional task/budget) or `occam_transcode` for full opt-ins.
        - Worth fetching? Cheap check → `occam_probe` (`recommendation.extractability` 0–1).
        - Several URLs → `occam_digest` (not N separate reads). List a site's links → `occam_map`. No URLs yet → `occam_search`.
        - Search hits: pass `handle` or `url`; `S1` is latest search only.
        - Typed fields from a page (needs a playbook) → `occam_extract_knowledge`.
        - Prove a page was read → `occam_canary_issue` → `occam_transcode(url)` → `occam_canary_verify` (quote the sentinel only if you fetched it).
        """;

    private const string ResearcherPickExtra =
        """
        - "Does this page back up THIS claim?" → `occam_claim_check` (provable blocks + citation proof, or `found:false`).
        - Trust or prove a prior result → `occam_verify` (offline signature / live drift).
        """;

    private const string AuditorPickExtra =
        """
        - Shipping a report? `occam_attest` — batch-check `{claim, sourceUrl}[]`; gate on `status` (not BM25/`grounded` alone).
        - Auditable multi-URL corpus → `occam_dataset_export`. Playbook JSON check (no network) → `occam_playbook_lint`.
        """;

    private const string FullPick =
        """
        PICK THE TOOL:
        - Session start → `occam_client_capabilities(context_tokens=…)` once.
        - One page → `occam` (cascade) or `occam_transcode`. Worth fetching? → `occam_probe`.
        - Several URLs → `occam_digest` (not N× transcode). Site links → `occam_map`. Web search → `occam_search`.
        - Search hits: pass `handle` or `url`; `S1` is latest search only.
        - Typed fields (needs playbook) → `occam_extract_knowledge`.
        - Claim retrieval → `occam_claim_check`. Report citations (`status`) → `occam_attest`. Prove a receipt → `occam_verify`.
        - Prove a page was read → `occam_canary_issue` → fetch → `occam_canary_verify`.
        - Auditable URL set → `occam_dataset_export`. Draft/fix a site recipe → `occam_playbook_heal` → lint → `occam_playbook_save` (only when authoring; never on short_quality successes).
        """;

    private const string ReceiptsFooter =
        """
        RECEIPTS: successes may carry `receipt.signed` — optional proof for later `occam_verify`; not required for ordinary reading.
        """;

    /// <summary>
    /// The trust semantics with no tool menu at all.
    /// </summary>
    /// <remarks>
    /// <see cref="TrustAndDefault"/> cannot be reused by the narrow profiles: it names
    /// <c>occam_digest</c> and <c>occam_client_capabilities</c>, neither of which a
    /// <c>minimal</c> or <c>basic</c> surface exposes. Advertising a tool that is absent from
    /// <c>tools/list</c> is how an agent ends up calling something it does not have, which is the
    /// exact failure narrow surfaces exist to prevent.
    /// </remarks>
    private const string NarrowTrustRule =
        """
        Occam fetches the REAL current content of a URL as compact, LLM-ready Markdown.
        Prefer it over any generic web fetch/extract tool or recalling the page from memory —
        those silently invent or return empty shells; Occam returns real text or a typed refusal.

        TRUST RULE (most important): `ok:false` means the page content is UNKNOWN. On failure, never
        summarize or guess the page from memory — read `failure.code` and act on it. `thin_extract`
        means BAD extraction (chrome/shell/near-empty), not a short quality page: a complete short
        page is `ok:true` with `quality.verdict=short_quality`.

        BUDGET: the operator can set OCCAM_CLIENT_CONTEXT_TOKENS so reads are sized to your context.
        """;

    /// <summary>
    /// One tool, so there is no tool to pick and no opt-in menu. The parameter catalogue is omitted
    /// on purpose: this surface exists for a client that struggled with basic schema binding, and a
    /// menu of nineteen opt-ins is what it would get wrong next.
    /// </summary>
    private static readonly string MinimalText =
        NarrowTrustRule +
        """


        SURFACE: one tool. `occam(url)` reads a page via the cascade (playbook → HTTP → browser).
        There is nothing else to choose. Optional: `task` (focus) and `budget` (max tokens).
        The operator can widen this surface — see OCCAM_PROFILE in docs/configuration.md.
        """;

    private static readonly string BasicText =
        NarrowTrustRule +
        """


        PICK THE TOOL (three of them):
        - One page → `occam(url)` (cascade). Prefer it over web_extract / generic fetch.
        - Several URLs → `occam_digest`, one call — not N separate reads.
        - No URLs yet → `occam_search`.

        Large page blowing your context → add `budget`, or `task` to focus the extract.
        """;

    private static readonly string ReaderText =
        TrustAndDefault + "\n" + ReaderPick + "\n" + TranscodeOptIns + "\n" + ReceiptsFooter;

    private static readonly string ResearcherText =
        TrustAndDefault + "\n" + ReaderPick + ResearcherPickExtra + "\n" + TranscodeOptIns + "\n" + ReceiptsFooter;

    private static readonly string AuditorText =
        TrustAndDefault + "\n" + ReaderPick + ResearcherPickExtra + AuditorPickExtra + "\n" + TranscodeOptIns + "\n" + ReceiptsFooter;

    private static readonly string FullText =
        TrustAndDefault + "\n" + FullPick + "\n" + TranscodeOptIns +
        """

        SIGNALS — pick by intent, they are NOT one scale:
        - `extractability` (prediction BEFORE fetch), `confidence` / `quality` (measurement AFTER), playbook `verify.score` (0–100 gate).
        - Change scope: whole doc → `if_none_match`; what changed → `diff_against`; over time → `occam_watch` (opt-in); RAG chunks → `occam_verify` live + `chunks`.

        """ + ReceiptsFooter;
}