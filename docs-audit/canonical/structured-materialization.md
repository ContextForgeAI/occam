# Structured materialization

**Slug:** `structured-materialization` · **Product system:** PS-2 Materialization · **CAPs:** 19 · **Public relevance:** HIGH

**Member CAPs:** CAP-075, CAP-078–CAP-081, CAP-084, CAP-086, CAP-109, CAP-111, CAP-306, CAP-314, CAP-318–CAP-320, CAP-329, CAP-331, CAP-334–CAP-336  
**Product capability:** CAP-081 (`translate_to` as named product_capabilities entry — see honesty note)  
**Artifacts (family ledger):** ART-039 (`translatedMarkdown` only) · **Also produced (ontology):** ART-001 markdown, ART-002 blocks, tables/feed/chunks sidecars, ART-006 capsule when opted, ART-024 MaterializationKey/contentHash  
**Engineering findings:** EF-004, EF-010, EF-055

## What it is

Production of **structured sidecars and codecs** after extract: blocks, tables, feeds, chunks, media refs, optional translation, capsules, plus the compile order and dead Canonical/codec paths that still burn CPU.

**Honesty:** family product_capability id CAP-081 is `translate_to` — structured outputs as a whole are CAP-316/318/319/320 etc. Translate is one opt-in codec-like path, not the whole family.

## Why it exists

Give agents machine-usable structure (RAG blocks, tables, feeds) beside markdown, and optional language transform — without requiring a separate fetch.

## User-visible entrypoints

| Param | Default | Effect | Evidence |
|-------|---------|--------|----------|
| `json_blocks` | false | Blocks sidecar | CAP-316; tool `:57` |
| `json_tables` | false | Tables + optional records | CAP-079/318 |
| `json_feed` | false | RSS/Atom/JSON Feed replace article extract | CAP-080/319 |
| `semantic_chunking` | false | `chunks[]` | CAP-075/320 — **name overstates** |
| `translate_to` | null | LibreTranslate if configured | CAP-081 |
| `prefer_llms_txt` | false | Pre-fetch URL substitute | CAP-084 — acquisition-adjacent |
| `emit_capsule` | false | Proof-carrying capsule in receipt | CAP-086 |
| (none) | — | Media refs always-on | CAP-109 |
| (none) | — | Plain-text passthrough codec path | CAP-111/329 |

## Core behavior

### Compile order (CAP-306)

`TranscodeCompiler.Apply` / `FinishMaterialize`: adapt bundle → plan → encode (passthrough) → budget/projection (`TranscodePipeline.cs:176-280`). Exact order documented in materialization.md CAP-306.

### Always-on internal collection (CAP-078 / EF-010)

Pipeline pushes internal `json_blocks,json_tables` features for Canonical/Planner even when public sidecars off — worker may collect structure always; public fields remain opt-in.

### Tables (CAP-318)

Physical rows one-per-`<tr>`; optional `records[]` semantic reconstruction with provenance. Live path via `MaterializationPlanner.Plan(request, bundle)` — **not** `TableSemanticMaterializer` (CAP-334 dead for live tools). Budget counts records **plus** physical rows (double cost).

### Feeds (CAP-319)

When URL is a feed and `json_feed=true`, parse replaces article extraction (HTTP).

### Chunks (CAP-320)

Fixed character accumulator (default 2000 via `OCCAM_CHUNK_SIZE`), early break on headings only. **Not** sentence/embedding semantic. Can split fenced code. Name “semantic_chunking” is misleading.

### Translate (CAP-081)

Requires `OCCAM_TRANSLATE_URL` (LibreTranslate). Non-fatal on failure — original markdown + warning (GAP-020). Sync `.GetResult()` noted in EF-057 adjacency.

### Media (CAP-109)

Always-on media references — not gated by a parameter.

### Capsule (CAP-086)

Opt-in; repeats markdown into `receipt.capsule`; requires receipts on; token-costly.

### Passthrough codec (CAP-329)

Only live codec. Canonical Knowledge built then discarded (**EF-004**).

## Advanced behavior

| CAP | Status | Notes |
|-----|--------|-------|
| CAP-314 | LIVE | MaterializationKey content-addressed identity |
| CAP-331 | DEAD | ProvenanceTrace resolver unused |
| CAP-334 | DEAD live | TableSemanticMaterializer bench-only |
| CAP-335 | INTERNAL | SurfaceSpanAttacher computed, not exposed |
| CAP-336 | LIVE | Env: CACHE_DIR, CHUNK_SIZE, CLIENT_CONTEXT_* |

`prefer_llms_txt` (CAP-084) is acquisition gate — listed here for membership; see acquisition-routing.

## Automatic / silent behavior

| Behavior | Notes |
|----------|-------|
| Canonical IR build+discard every success | EF-004; AUTOMATION #18 |
| Internal feature flags for blocks/tables | EF-010 |
| Media refs without opt-in | CAP-109 |
| `diff_against` forces blocks into response | EF-010 / differential family |

## Parameters

See table above. `emit_capsule` / translate / structured flags are independent opt-ins.

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_TRANSLATE_URL` | unset | Enables translate_to |
| `OCCAM_CHUNK_SIZE` | 2000 chars | Chunk length |
| `OCCAM_CACHE_DIR` | `%TEMP%/occam-cache` | Cache family |
| `OCCAM_CLIENT_CONTEXT_TOKENS` | unset | Ambient budget |
| Receipts env | partial master switch | Capsule requires receipts on (C6 caveats) |

## Backends

Worker produces blocks/tables/feed/chunks when features set. Host maps via `WorkerKnowledgeMapper`. Translate is host HTTP to LibreTranslate.

## Sessions / state

MaterializationKey / cache key identity — see response-cache. Stateless otherwise.

## Network behavior

- `prefer_llms_txt`: extra HTTP fetch.
- `translate_to`: outbound to translate service.
- Feed/article still subject to acquisition SSRF rules.

## Artifacts produced

| Artifact | Gate |
|----------|------|
| `blocks[]` | json_blocks |
| `tables[]` / records | json_tables |
| `feed` | json_feed |
| `chunks[]` | semantic_chunking |
| `media` refs | always |
| `translatedMarkdown` | translate_to success |
| `receipt.capsule` | emit_capsule + receipts |
| Canonical claims/evidence | **computed, not serialized** |

## Trust / provenance properties

Block `source_selector` and table record provenance are the strong structured trust signals. Capsule is proof-carrying only with receipts. Canonical ProvenanceTrace is dead (CAP-331) — do not document as shipped citation proof. Translate is lossy (tool warns).

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| Translate fail | Warning; original MD |
| Non-feed + json_feed | Unaffected article path |
| Chunk mid-fence | Broken markdown possible |
| Malformed knowledge paths | EF-055 (extract_knowledge) — adjacent PS-4 |

## Platform differences

None specific.

## Composition with other capabilities

- Budget trim competes (`token-budget`).
- Focus ranks blocks (`focus-selection`).
- Diff uses block hashes (`differential-materialization`).
- Cache key omits some structured flags (`response-cache` / EF-001).
- Trust/receipts own capsule verification (PS-6).

## Known limitations

- “Semantic” chunking is not semantic.
- Canonical IR / alternate codecs registered but unreachable (EF-004, CAP-328).
- TableSemanticMaterializer unused live.
- prefer_llms_txt membership is acquisition-adjacent.
- CAP-081 as sole product_capability understates family breadth.

## Engineering findings

| ID | Finding |
|----|---------|
| **EF-004** | Canonical extract every transcode then discarded |
| **EF-010** | Always-on block collection; diff forces blocks |
| **EF-055** | Schema/max_tokens bound issues (partially adjacent) |

## Code evidence

- `Routing/TranscodePipeline.cs:44-55,176-280`
- `Codecs/MarkdownPassthroughCodec.cs:36-43`
- `Knowledge/MaterializationPlanner.cs`, `WorkerKnowledgeMapper.cs`
- `workers/shared/plugins/chunking.mjs:10-66`
- `docs-audit/subsystems/materialization.md` §§3,6–10

## Public-doc relevance

**HIGH** for structured params; **must** demystify semantic_chunking and say Canonical IR is not a client feature.

## Handbook relevance

Structured outputs cookbook; RAG blocks; feed mode; translate optional ops note.
