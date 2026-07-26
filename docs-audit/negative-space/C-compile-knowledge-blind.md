# W4-C — Compile, Knowledge, Codecs, Cache, Extract (blind negative-space audit)

## Method and scope

Code was read before any prior audit artifact. Scope covered 66 C# files: all files under
`Compile/` (20), `Knowledge/` including `Canonical/`, `Extraction/`, and `Legacy/` (33),
`Codecs/` (8), `Caching/` (3), and `Extract/` (2). Runtime call sites were then traced through
`TranscodePipeline`, `OccamTranscodeTool`, `KnowledgeExtractService`, `CssExtractWorker`, and the
CSS extraction worker. The Core project compiles these files even when no live request reaches them.

## 1. Blind inventory

1. **The live transcode path always requests block and table IR, even without public structured
   opt-ins.** `TranscodePipeline.TranscodeAsync` unconditionally adds `json_blocks` and
   `json_tables`; public projection happens later (`src/FFOccamMcp.Core/Routing/TranscodePipeline.cs:44-47`,
   `:408-410`). This is automatic, invisible, non-configurable extraction/performance work.
2. **Live materialization is fixed to one codec.** The pipeline calls
   `KnowledgeCodecSelector.Select(..., requestedCodecId: null)` and therefore always selects the
   configured default; no MCP codec parameter exists (`TranscodePipeline.cs:271-278`). DI fixes that
   default to `markdown-passthrough` (`Composition/OccamServiceCollectionExtensions.cs:139-147`).
3. **Two experimental codecs ship but are not runtime-selectable by any MCP/CLI request.**
   `compact-markdown` and `knowledge-json` are DI-registered
   (`OccamServiceCollectionExtensions.cs:140-147`), and the selector can select them by explicit ID
   (`Codecs/KnowledgeCodecSelector.cs:43-95`), but production passes only `null`. They are reachable
   from tests/bench APIs, not product request surfaces.
4. **The default codec is byte passthrough.** It returns `view.Surface.Text` and ignores document IR
   and Canonical sidecars (`Codecs/MarkdownPassthroughCodec.cs:36-42`).
5. **The compact codec reconstructs lossy Markdown from IR.** It preserves heading level and tables,
   flattens list nesting, drops link hrefs, defaults missing heading level to H2, and falls back to the
   original surface for empty IR (`Codecs/CompactMarkdownCodec.cs:44-111`). This behavior is shipped
   but benchmark/test-only in the current host.
6. **The JSON codec can serialize document IR plus Canonical source/evidence/claim/provenance.** It
   emits deterministic sorted Canonical collections while preserving document order
   (`Codecs/KnowledgeJsonModels.cs:155-350`), but is not selectable from the live request path.
7. **Codec extension support is an in-process API only.** Registration is disabled by default,
   allow-listable, fail-closed, and cannot replace the default
   (`Codecs/KnowledgeCodecRegistry.cs:100-153`, `:185-193`). No assembly scan, config binding, or
   product registration call exists.
8. **Canonical Knowledge is built on every successful live transcode, then not serialized.**
   `ExtractedKnowledgeAdapter` maps blocks/tables, attaches spans, and constructs Canonical records
   (`Knowledge/Extraction/ExtractedKnowledgeAdapter.cs:27-57`); the planner retains Canonical data
   (`Knowledge/MaterializationPlanner.cs:47-72`); the only live codec ignores it
   (`MarkdownPassthroughCodec.cs:36-42`); the returned `TranscodeOutcome` has no Canonical fields
   (`TranscodePipeline.cs:412-436`). The work is computed-then-discarded on the live path.
9. **The discarded Canonical work includes per-request random IDs and per-block cryptography.**
   Every block receives new Source/Evidence/Claim/Provenance IDs and a Merkle leaf hash
   (`Knowledge/Legacy/TranscodeToCanonical.cs:83-132`); tables create evidence/provenance
   (`:136-166`). This makes internal Canonical views non-deterministic across calls and adds CPU/
   allocation cost without affecting the default response.
10. **Surface spans are also computed then discarded in live responses.** The adapter performs
    ordered exact substring searches with retry-from-zero (`Knowledge/Extraction/SurfaceSpanAttacher.cs:17-43`);
    spans exist only in document IR, which passthrough ignores.
11. **Canonical retention separately budgets claims at one quarter of the surface budget.** Default
    policy keeps a minimum 8-token claim pool, focus-ranks claims, restores source order, and closes
    evidence/provenance over retained claims; `evidence-preserving` bypasses pruning
    (`Knowledge/CanonicalRetention.cs:18-47`, `:56-142`). This live computation currently has no
    externally observable response effect.
12. **Canonical `Fact`, `Entity`, and `Relationship` are dead-at-runtime domain types.** No product
    call site creates them. `Fact`/`Relationship` enforce Supported→provenance
    (`Knowledge/Canonical/Fact.cs:45-85`, `Relationship.cs:38-64`), but live adaptation creates only
    `ClaimCandidate` and explicitly never promotes to Fact (`Knowledge/Legacy/TranscodeToCanonical.cs:110-120`).
13. **The provenance resolver and planner/codec benches are shipped libraries, not live surfaces.**
    `MaterializedProvenanceResolver` can resolve claim→evidence→source and verify a Merkle membership
    proof with explicit missing/root-mismatch statuses (`Knowledge/MaterializedProvenanceResolver.cs:21-120`);
    all production references are absent. `PlannerBench` self-identifies as not live
    (`Knowledge/PlannerBench.cs:108-112`).
14. **Document IR is budgeted independently but does not drive the default Markdown output.**
    `MaterializationPlanner` greedily retains salience-prioritized blocks and then tables
    (`Knowledge/MaterializationPlanner.cs:164-236`); passthrough returns the separately compiled
    surface. Public blocks are instead reconciled and trimmed from raw worker blocks
    (`TranscodePipeline.cs:322-410`).
15. **Whole-response budgeting is heuristic and bucketed, not a serialized-JSON hard limit.**
    It reserves 48 estimated tokens for a receipt, allocates at least 50%/128 tokens to Markdown,
    then greedily keeps screenshot, blocks, tables, chunks, media, feed in that order
    (`Compile/ResponseBudgetPlanner.cs:77-112`, `:115-232`). JSON syntax, envelope metadata, escaping,
    warnings, signatures, and several receipt fields are not measured.
16. **The receipt reserve is allowed to under-fit silently.** `receiptFit` is clamped to leftover
    budget but does not trim the actual receipt object (`ResponseBudgetPlanner.cs:205-221`).
    `ResponseBudgetAllocation.Total` is therefore an estimate of selected payload buckets, not proof
    that the serialized response respects `max_tokens`.
17. **Response budget diagnostics are computed but not mapped to the public response.**
    `TranscodePipeline` creates `ResponseBudgetDiagnostics` (`TranscodePipeline.cs:355-383`), while
    public compile mapping uses only `TokensEstimated`, `Omitted`, and `Budget`
    (`Tools/OccamTranscodeModels.cs:300-332`). `ActualSerializedProjectionTokens` is never assigned
    (`Compile/ResponseBudgetDiagnostics.cs:6-15`).
18. **Token counts use a script-weighted character heuristic.** ASCII costs 0.25, CJK/Kana/Hangul
    1.0, other UTF-16 code units 0.5; astral characters therefore cost roughly 1.0 via two surrogate
    halves (`Compile/TokenEstimator.cs:14-57`). Byte pre-sizing still uses `ceil(bytes/4)`
    (`:60-64`). It is not a model tokenizer.
19. **The heuristic has known semantic unit gaps.** Combining marks, emoji sequences, code-heavy
    text, whitespace, and tokenizer vocabulary are all charged per UTF-16 character; CJK runs are
    one token per character. The implementation is internally char-cost consistent for prefix/suffix
    cutting (`TokenEstimator.cs:66-105`) but only approximate relative to actual model tokens.
20. **FitMarkdown is a hard-coded BM25-inspired filter, not standard BM25 over paragraphs.** It uses
    `k1=1.2`, `b=0.75`, thresholds 0.12/0.08 (unfocused) and 0.06/0.035 (focused), heading weights
    1.2→0.7, link-density and short-paragraph filters, a fixed boilerplate phrase list, and a 30-line
    focused-list cap (`Compile/FitMarkdown.cs:8-44`, `:205-316`, `:644-698`).
21. **FitMarkdown term frequency is substring-counted on original case-sensitive text.**
    Tokens are lowercased but `block.Text.Split(term)` is case-sensitive and can count substrings
    rather than token occurrences (`FitMarkdown.cs:644-668`). Capitalized occurrences can score as
    zero despite being present, while embedded substrings can inflate TF.
22. **Focus behavior is more than a keyword filter.** URL fragments are stripped before fetch,
    decoded and truncated to 512 UTF-16 chars, then used as internal focus intent
    (`Compile/FocusIntent.cs:6-30`; `TranscodePipeline.cs:112-116`). Section ranking applies exact
    fragment/anchor/heading bonuses, definitional boosts, a 2,048-character body probe, and a
    -3,000 index penalty (`Compile/SectionIndex.cs:220-322`).
23. **Token truncation is structure-aware but can reorder content.** Focus mode selects one ranked
    section, preserves a definitional anchor, and may append an unchosen-section marker
    (`Compile/TokenBudget.cs:39-84`). Within an oversized section it selects a minimum answer unit and
    adds remaining blocks by score, not source order (`:302-426`). Non-focus uses head-safe truncation;
    focus fallback can use a 55%/40% head/tail sandwich (`:483-550`).
24. **OmittedManifest is an estimate with limited honesty.** It reports token-difference, one region
    inferred solely from strategy, and the net count of `##`/`###` headings
    (`Compile/OmittedManifest.cs:32-63`, `:66-86`). It cannot identify which sections were omitted,
    reordered, partially cut, or replaced; structured-only trim reports `TokensDropped: 0`
    (`TranscodePipeline.cs:458-473`).
25. **Materialization identity omits fragment focus.** `MaterializationKey` normalizes the URL by
    dropping its fragment and hashes no `FocusFragment` field (`Compile/MaterializationKey.cs:21-55`;
    `Caching/TranscodeCacheKey.cs:54-71`), although fragments change fit/truncation and focus/
    completeness output (`TranscodeCompiler.cs:28-44`; `MaterializationPlanner.cs:58-63`).
26. **The response cache has a direct fragment collision.** Cache lookup/keying happens before the
    pipeline derives `FocusFragment` (`Tools/OccamTranscodeTool.cs:117-130`; `TranscodePipeline.cs:112-114`);
    `NormalizeUrl` drops fragments (`Caching/TranscodeCacheKey.cs:54-71`). Two fragment-specific reads
    can therefore return the wrong cached materialization.
27. **The response cache key omits three response-affecting tool inputs.** It hashes transcode options
    through `translate_to` (`TranscodeCacheKey.cs:19-47`) but not `emit_capsule`, `rank_blocks`, or
    `tag_trust`, which are applied after extraction and alter the serialized success
    (`OccamTranscodeTool.cs:258-281`, `:286-316`). A hit can return a capsule/tags/salience state
    requested by a previous caller rather than the current one.
28. **Cache privacy eligibility is conservative for explicit private/session/differential modes.**
    It is off unless TTL>0, excludes session profiles, `if_none_match`, private/invalid hosts
    (`Caching/TranscodeCacheEligibility.cs:13-40`); the tool additionally excludes `diff_against` and
    `prefer_llms_txt` (`OccamTranscodeTool.cs:117-125`).
29. **Cache expiry is read-driven and best-effort.** Entries are one JSON file per key; there is no
    proactive sweep, size bound, quota, or eviction. Expired entries are deleted only when the same
    key is read; all IO/JSON failures silently become misses (`Caching/TranscodeResponseCache.cs:45-93`,
    `:96-134`). Default location is `%TEMP%/occam-cache`, overridable by `OCCAM_CACHE_DIR`
    (`:37-43`).
30. **Cache TTL is caller-relative, not stored policy.** The entry stores only creation time and full
    response JSON; each read supplies its own TTL (`TranscodeResponseCache.cs:13-16`, `:138-142`).
    A later caller can reuse an entry for longer than the creator requested.
31. **Cache hits replay the full prior envelope.** A successful response is persisted after receipts,
    translation, warnings, semantic hints, and optional capsule/tags are built
    (`OccamTranscodeTool.cs:319-367`); hits only set `cached:true` and age (`:373-390`). Runtime changes
    to receipt policy/key, translation backend, browser availability, or playbook content are not
    represented in the key during the caller-selected TTL.
32. **`extract_knowledge` field syntax is richer than ordinary CSS text fields.** Each schema field
    accepts `selector`, optional `attr`, `multiple`, and integer `divide`
    (`Extract/FieldSpecParser.cs:55-74`). Worker attributes include `text`, `html`, arbitrary DOM
    attributes (including `href`/`src`), plus special `nuxt`, `regex`, and `const`; `divide>1`
    numerically scales `nuxt`/`regex` results
    (`workers/css-extract/lib/css-schema-extract.mjs:84-113`, `:167-182`).
33. **Row mode exists in worker/wire types but cannot be produced by the shipped parser.**
    `FieldExtractionPlan.BaseSelector` enables row extraction
    (`Extract/FieldExtractionPlan.cs:11-16`; worker `css-schema-extract.mjs:15-75`), but both parser
    entry points construct the plan with only `Fields` (`FieldSpecParser.cs:20-31`, `:41-52`).
    No runtime assignment to `BaseSelector` exists.
34. **Field parsing is weakly validated and some malformed JSON escapes the service's catch.**
    `ParseSpec` calls `TryGetProperty`/`GetString`/`GetBoolean` directly and validates neither node
    object kind, attr vocabulary, selector syntax, `divide` sign, nor field names
    (`FieldSpecParser.cs:55-74`). `KnowledgeExtractService` catches only `ArgumentException`
    (`Services/KnowledgeExtractService.cs:67-79`), while `JsonException`/`InvalidOperationException`
    from wrong JSON kinds can escape as tool-level exceptions depending on the playbook payload.
35. **Knowledge extraction returns partial facts on worker failure.** Failed worker data is mapped
    through the same field plan and attached as `partialFacts`
    (`KnowledgeExtractService.cs:104-132`), while success flattens arrays with `"; "` and preserves
    objects as raw JSON (`:183-218`). This is a distinct degraded artifact/failure semantic.
36. **No platform-specific branches exist inside the assigned C# scope.** The observable platform
    difference is path/temp behavior inherited from `Path.GetTempPath()` for cache and temporary
    field files (`TranscodeResponseCache.cs:37-43`; `Workers/CssExtractWorker.cs:23-24`).

## 2. Gap classification

Comparison set: `CAPABILITY-INVENTORY.md`, `capabilities.json`, `CAPABILITY-GRAPH.md`,
`capability-graph.json`, `ARTIFACT-MAP.md`, `CODE-DERIVED-WORKFLOWS.md`,
`NONCORE-SURFACE-MAP.md`, `subsystems/materialization.md`, `tools/occam_transcode.md`, and
`tools/occam_extract_knowledge.md`.

### Covered exactly (19)

- **COVERED_EXACTLY — two-layer budget and greedy structured trim.** CAP-300/301/302 accurately
  capture the allocation layers, fixed bucket order, and repeated estimates.
- **COVERED_EXACTLY — compile order, selector semantics, truncation, assessment.** CAP-306…313
  accurately cover selector→fit→budget→anchor, three truncation strategies, section ranking,
  answer-unit protection, and the coarse omitted manifest.
- **COVERED_EXACTLY — cache flags already known.** CAP-315 / EF-001 exactly cover omitted
  `rank_blocks`, `tag_trust`, and `emit_capsule` (`TranscodeCacheKey.cs:19-47`).
- **COVERED_EXACTLY — read-time-only cache expiry.** CAP-321/322 cover file storage, best-effort
  misses, no proactive sweep, and no size bound (`TranscodeResponseCache.cs:45-134`).
- **COVERED_EXACTLY — codec reachability.** CAP-327…329 correctly distinguish real registry
  machinery from the two dead experimental codecs and the sole live passthrough codec.
- **COVERED_EXACTLY — Canonical computed/discarded.** CAP-330/333 and EF-004 correctly identify
  the live Canonical construction/retention whose output never reaches MCP.
- **COVERED_EXACTLY — dead Canonical/runtime helpers.** CAP-331/332/334 correctly identify the
  provenance resolver, Fact/Entity/Relationship tier, and table materializer as non-live.
- **COVERED_EXACTLY — live but unexposed spans/diagnostics.** CAP-303/335 cover both.
- **COVERED_EXACTLY — richer extraction modes and partial facts.** CAP-599 and CAP-602 cover
  `regex`/`const`/`divide` and failure `partialFacts`.

### Partial coverage (6)

1. **COVERED_PARTIALLY — CAP-308 describes thresholds but misses a scoring correctness flaw.**
   BM25 TF tokenization lowercases terms, then counts with case-sensitive substring splitting over
   original text (`FitMarkdown.cs:644-668`). Capitalized terms undercount and embedded substrings
   overcount. Existing coverage discusses language/boilerplate limitations, not this.
2. **COVERED_PARTIALLY — token estimator is used everywhere but has no first-class capability.**
   Existing CAP-309 mentions script-aware cuts and CAP-337 covers chunk chars-vs-tokens, but no CAP
   describes the externally reported `heuristic-unicode-v1` estimator, its UTF-16 cost model, or the
   distinction between `Estimate` and byte pre-sizing (`TokenEstimator.cs:14-64`).
3. **COVERED_PARTIALLY — CAP-310 calls the omitted manifest honest without bounding its claim.**
   It reports only strategy-derived region, token delta, and net `##`/`###` count; it does not identify
   partial sections, actual omitted identities, or score-based reordering (`OmittedManifest.cs:32-86`).
4. **COVERED_PARTIALLY — CAP-321/322 omit caller-relative TTL semantics.** TTL is not persisted;
   each reader supplies a new TTL for the same creation timestamp
   (`TranscodeResponseCache.cs:13-16`, `:79-93`, `:138-142`).
5. **COVERED_PARTIALLY — CAP-330/EF-004 understate discarded work.** Beyond "Canonical records",
   every live success creates random IDs, computes a Merkle leaf per block, maps full block/table IR,
   performs span searches, and runs Canonical claim retention
   (`TranscodeToCanonical.cs:83-166`; `SurfaceSpanAttacher.cs:17-43`).
6. **COVERED_PARTIALLY — field syntax coverage omits validation behavior.** CAP-590/599 enumerate
   syntax, but the model misses that attr vocabulary, selector validity, node kinds, field names, and
   divide bounds are not validated (`FieldSpecParser.cs:55-74`).

### Wrong or contradictory model claims (4)

1. **COVERED_WRONG — CAP-314 overclaims materialization identity completeness.**
   `MaterializationKey` drops URL fragments via `NormalizeUrl` and has no `FocusFragment` descriptor
   field (`MaterializationKey.cs:21-55`; `TranscodeCacheKey.cs:54-71`), although a fragment changes
   focus selection and completeness. It is an input-descriptor hash, not content-addressing.
2. **COVERED_WRONG — `tools/occam_transcode.md` CAP-085 says the cache key contains every
   output-affecting option, including playbook ID and annotation flags.** The code contains none of
   those fields (`TranscodeCacheKey.cs:19-47`). The consolidated CAP-315 corrects only the annotation
   flags; the source model remains internally contradictory.
3. **COVERED_WRONG — ART-024 says MaterializationKey is consumed by cache and `if_none_match`.**
   The response cache consumes the separate `TranscodeCacheKey`; `if_none_match` compares
   `ContentHashToken` (`OccamTranscodeTool.cs:117-130`, `:185-191`). `MaterializationKey` is returned
   as client identity metadata, not used by either mechanism server-side.
4. **COVERED_WRONG — ART-006/007 say capsule is not cacheable and receipts are not cached.**
   The full serialized success envelope, including receipt and optional capsule, is written to the
   response cache (`OccamTranscodeTool.cs:286-367`) and replayed wholesale (`:373-390`).

### Missing capabilities / edges / artifacts / workflows

1. **MISSING_CAPABILITY — CAP-NEW-C-1: script-aware heuristic token accounting.**
   `TokenEstimator.EstimatorId = "heuristic-unicode-v1"` is response-visible provenance; its
   ASCII/CJK/other-script costs drive compile truncation, budget allocation, receipts, and codec
   benches (`TokenEstimator.cs:14-64`). This is a distinct product behavior, not merely an internal
   helper.
2. **MISSING_CAPABILITY — CAP-NEW-C-2: URL fragment as implicit local focus intent.**
   A fragment is decoded, capped at 512 chars, stripped from the fetch URL, and silently drives
   fit/truncation and semantic focus without a separate MCP parameter (`FocusIntent.cs:3-30`;
   `TranscodePipeline.cs:112-116`).
3. **MISSING_EDGE — fragment → cache-key collision.** Cache lookup occurs before `FocusFragment` is
   derived, while URL normalization drops the fragment. `url#section-a` can populate an entry later
   replayed for `url#section-b` (`OccamTranscodeTool.cs:117-130`; `TranscodeCacheKey.cs:54-71`).
4. **MISSING_EDGE — fragment → MaterializationKey collision.** Both fragment-specific
   materializations return the same key when all explicit options match
   (`MaterializationKey.cs:21-55`).
5. **MISSING_EDGE — cached response → receipt/capsule/annotations replay.** Artifact-map relations do
   not show that cache storage captures the post-sign/post-annotation full envelope.
6. **MISSING_EDGE — public max_tokens → heuristic projection, not serialized hard bound.**
   The planner excludes JSON syntax/envelope/warnings and lets actual receipt cost exceed its clamped
   `receiptFit` estimate (`ResponseBudgetPlanner.cs:205-221`; `OccamTranscodeModels.cs:300-332`).
7. **MISSING_EDGE — schema field → special worker interpretation.** The graph links playbook schema
   to facts but not `attr=nuxt|regex|const`, arbitrary DOM attributes, `multiple`, and `divide` to
   worker behavior (`FieldSpecParser.cs:55-74`; worker `css-schema-extract.mjs:84-113`).
8. **MISSING_ARTIFACT — file-backed transcode cache entry.** `OccamCacheEntry` is a persisted boundary
   artifact containing schema version, creation time, and a complete prior response JSON
   (`TranscodeResponseCache.cs:138-145`); ARTIFACT-MAP lists only response artifacts, not this store.
9. **MISSING_ARTIFACT — temporary CSS field-spec file.** `CssExtractWorker` serializes the plan to
   `%TEMP%/occam-fields-<guid>.json` and passes its path to Node
   (`Workers/CssExtractWorker.cs:23-47`). It is a host→worker artifact with best-effort cleanup.
10. **MISSING_WORKFLOW — opt-in cache replay.** The workflow map lacks
    request→eligibility→key→read/TTL/delete-on-expiry→live fallback→full-envelope write/replay.
11. **MISSING_WORKFLOW — implicit fragment focus.** The workflow map does not show
    URL fragment→local intent→fragmentless fetch→section rank→budget/assessment.

### Missing failure/security semantics and dead-code correction

1. **MISSING_FAILURE_SEMANTIC — malformed playbook field specs can escape typed tool failure.**
   `KnowledgeExtractService` catches only `ArgumentException` (`KnowledgeExtractService.cs:67-79`);
   wrong JSON kinds can make `JsonElement.GetString`/`GetBoolean` throw
   `InvalidOperationException`, and malformed JSON can throw `JsonException`
   (`FieldSpecParser.cs:55-74`). The model lists `invalid_arguments` but not this unhandled boundary.
2. **DEAD_CODE_MISTAKEN_AS_PRODUCT — CAP-600's row-mode trigger is unreachable before the worker.**
   The prior report says a `knowledge_schema` can set `base_selector` and the host then fails to map
   returned rows. In current C#, neither parser reads a top-level `base_selector`; both construct
   `FieldExtractionPlan { Fields = fields }`, and no runtime assignment to `BaseSelector` exists
   (`FieldSpecParser.cs:20-31`, `:41-52`). Therefore worker row mode is dead earlier than modeled:
   the host never sends a non-null `baseSelector` at all.
3. **MISSING_SECURITY_SEMANTIC — none new in assigned C# scope.** The page-controlled Nuxt `eval`
   path is already accurately covered by CAP-598 / EF-013.
4. **MISSING_CONFIG — none.** `OCCAM_CACHE_DIR` is correctly catalogued in
   `ENVIRONMENT-VARIABLES.md`; no platform branch exists in assigned C# files.
5. **PRODUCT_MISTAKEN_AS_INTERNAL — none.**

## 3. Engineering finding candidates

- **EFC-C-1 — BUG / HIGH confidence:** URL-fragment focus is omitted from both cache and
  materialization keys, causing deterministic cross-fragment stale-response collisions
  (`FocusIntent.cs:8-30`; `TranscodeCacheKey.cs:19-47,54-71`; `MaterializationKey.cs:21-55`).
- **EFC-C-2 — BUG / HIGH confidence:** whole-response `max_tokens` is not a bound on the serialized
  response; envelope/JSON/signature costs are unmeasured and receipt estimate is clamped without
  trimming the actual receipt (`ResponseBudgetPlanner.cs:205-221`).
- **EFC-C-3 — BUG / HIGH confidence:** malformed `knowledge_schema` field node types can escape the
  `ArgumentException` catch and bypass the tool's typed `invalid_arguments` response
  (`FieldSpecParser.cs:55-74`; `KnowledgeExtractService.cs:67-79`).
- **EFC-C-4 — BUG / HIGH confidence:** FitMarkdown's BM25 TF count is case-sensitive substring
  counting against lowercased query terms (`FitMarkdown.cs:656-668`).
- **EFC-C-5 — PERFORMANCE / HIGH confidence (extends EF-004):** discarded live Canonical work includes
  GUID generation, one Merkle leaf per block, span attachment, document-IR retention, and Canonical
  closure pruning, not just DTO construction (`TranscodeToCanonical.cs:83-166`;
  `SurfaceSpanAttacher.cs:17-43`; `MaterializationPlanner.cs:40-56`).
- **EFC-C-6 — DESIGN / HIGH confidence:** cache TTL is reader-selected and entries retain no creator
  TTL; a later caller may intentionally or accidentally extend reuse of an old full envelope
  (`TranscodeResponseCache.cs:13-16`, `:79-93`, `:138-142`).

## 4. Convergence and uncertainties

Independent discovery converged for the assigned C# scope: after tracing every scoped type to
production call sites, additional searches found only tests/bench consumers for dead codecs,
Canonical helpers, and bench machinery. Major existing Wave 1 findings were independently reproduced.

Bounded uncertainties:

- No runtime repro was performed; fragment/cache collisions and malformed-schema throws are proven by
  call order and key/parser construction, not an executed MCP call.
- Exact model-token error bounds for `heuristic-unicode-v1` are tokenizer/model dependent and were not
  benchmark-measured.
- Cache replay across receipt-key/config transitions is structurally possible during TTL, but whether
  operators consider replaying the originally valid signed envelope undesirable is a policy question.
