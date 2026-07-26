# Focus and selection

**Slug:** `focus-selection` · **Product system:** PS-2 Materialization · **CAPs:** 11 · **Public relevance:** HIGH

**Member CAPs:** CAP-064, CAP-065, CAP-077, CAP-087, CAP-307, CAP-311–CAP-313, CAP-316, CAP-317, CAP-327  
**Product capability:** CAP-064  
**Engineering findings:** None on family ledger.

## What it is

Mechanisms that **select which parts of an extract matter** for a caller intent: `focus_query`, heading `content_selectors`, section ranking, answer-unit protection, block salience (`rank_blocks`), and related assessment fields (`focus` / `completeness`).

## Why it exists

Large pages waste tokens; focus keeps the answering section and reports honestly when the answer unit was truncated or missing.

## User-visible entrypoints

| Param / field | Role | Evidence |
|---------------|------|----------|
| `focus_query` | Keywords / intent | CAP-064; `OccamTranscodeTool.cs:50` |
| URL `#fragment` | Implicit focus via `FocusIntent` | FLOW-020; AUTOMATION #17 |
| `content_selectors` | Heading-anchor keep list | CAP-065/307 — **not** CSS |
| `json_blocks` + `rank_blocks` | Per-block salience | CAP-077/087/316/317 |
| Response `focus` / `completeness` | Semantic verdicts | CAP-311 |

## Core behavior

### Focus query (CAP-064)

Drives FitMarkdown scoring when `fit_markdown=true`, `TokenBudget` focus_window strategy, and `MaterializationAssessment`. On digest, fit often auto-on with focus.

### content_selectors (CAP-065 / CAP-307)

JSON array or comma-separated **heading anchors** (e.g. `# API Reference`). Scopes compile to those sections. Name suggests CSS — **incorrect**; real CSS paths live on `json_blocks[].source_selector` (CAP-316).

### SectionIndex / SectionRanker (CAP-312)

Deterministic local resolver: exact-fragment 10000 → exact-anchor 4000 → exact-heading 2500 → term hits → definitional bonus → index-page penalty. Single SoT for both truncation and assessment (cannot disagree by construction).

### AnswerUnitSelector (CAP-313)

Minimum `{heading, first prose, nearest structured block}` protected first under budget; assessment checks survival → Incomplete if lost.

### Assessment (CAP-311)

Completeness: `Complete` / `Partial` / `Incomplete`. Incomplete specifically when section resolved but answer unit cut (`focus_body_truncated` vs `source_missing`).

### Blocks + rank/trust (CAP-077 / CAP-087 / CAP-316 / CAP-317)

- `json_blocks`: DOM blocks with real CSS `source_selector`.
- `rank_blocks`: BM25 salience 0–1 vs focus; requires `json_blocks` + `focus_query`.
- `tag_trust`: suspicious/boilerplate tags; requires `json_blocks` (trust heuristic, not guarantee).

### Codec registry (CAP-327)

Extension point exists; production always `MarkdownPassthroughCodec` — selection does not change focus behavior today.

## Advanced behavior

| Topic | Notes |
|-------|-------|
| Fragment vs cache | Fragment dropped from cache key — **EF-045** collision risk |
| Block reconciler 50% line keep | Tolerates FitMarkdown line filters (CAP-316) |
| Digest vs transcode fit defaults | Digest more aggressive |

## Automatic / silent behavior

| Behavior | Impact |
|----------|--------|
| URL fragment → FocusIntent; fetch URL strips fragment | AUTOMATION #17 |
| Internal always-on block/table features for planner | Public sidecars still opt-in |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `focus_query` | null | Focus targeting; needed for rank_blocks |
| `fit_markdown` | false (transcode) | Enables BM25 prune with focus |
| `content_selectors` | null | Heading-anchor scope |
| `json_blocks` | false | Enables blocks (+ rank/trust) |
| `rank_blocks` | false | Salience annotation |
| `tag_trust` | false | Trust channel tags |

## Configuration

No env knobs for section rank weights or answer-unit shape. Hardcoded score tables in `SectionRanker`.

## Backends

Host compile. Worker supplies blocks when features enabled. Fragment handling precedes fetch URL.

## Sessions / state

Stateless per call aside from cache identity issues (fragment omitted — EF-045).

## Network behavior

None for selection itself. Wrong fragment focus can still fetch full page then prune.

## Artifacts produced

Focused markdown; `focus`/`completeness` fields; optional `blocks[].salience` / `trust`; section-derived omitted reasons.

## Trust / provenance properties

Focus/completeness are **honesty signals about materialization**, not page truth. `tag_trust` is heuristic. `source_selector` on blocks is stronger provenance for RAG than heading selectors.

## Failure / fallback behavior

| Condition | Signal |
|-----------|--------|
| Selectors miss content | May yield `content_selectors_miss` (ranking 40) |
| Focus not in doc | `source_missing`-class focus |
| Unit truncated | Incomplete / `focus_body_truncated` |
| rank_blocks without preconditions | No salience (needs blocks+focus) |

## Platform differences

None.

## Composition with other capabilities

- Feeds `token-budget` truncation choice.
- Overlaps `structured-materialization` for blocks.
- `differential-materialization` uses block hashes from focused/compiled blocks.
- Acquisition fragment strip couples to fetch URL.

## Known limitations

- `content_selectors` name overstates (not CSS).
- Rank thresholds not tunable.
- Codec selection unused for focus.
- Fragment/cache key gap (EF-045).
- English-centric FitMarkdown boilerplate denylist (token-budget CAP-063) biases focused prune on non-English chrome.

### SectionRanker score table (evidence)

| Signal | Score | Evidence |
|--------|-------|----------|
| Exact fragment | 10000 | `SectionIndex.cs` / `SectionRanker.Select` |
| Exact anchor | 4000 | same |
| Exact heading | 2500 | same |
| Heading term hits | ×350 | same |
| Body term hits | ×45 | same |
| Definitional-answer bonus | +900 | same |
| Index-page penalty | −3000 | same |

### FitMarkdown thresholds (when `fit_markdown=true`)

| Mode | `minScore` | `minHeadingScore` | Evidence |
|------|------------|-------------------|----------|
| Focused | 0.06 | 0.035 | `FitMarkdown.cs:43-44` |
| Unfocused | 0.12 | 0.08 | same |

## Engineering findings

| ID | Notes |
|----|-------|
| EF-045 | Fragment omitted from cache/materialization keys |

## Code evidence

- `Compile/SectionIndex.cs:42-323`, `AnswerUnit.cs:8-66`
- `Compile/FitMarkdown.cs:24-134,43-44`
- `Knowledge/MaterializationAssessment.cs:21-69`
- `OccamTranscodeTool.cs:50-51,65-66,260-267`
- `Routing/TranscodePipeline.cs:112-114` (FocusIntent)
- `docs-audit/subsystems/materialization.md` CAP-307–317

## Public-doc relevance

**HIGH.** Clarify heading vs CSS selectors; teach focus+completeness reading; fragment behavior.

## Handbook relevance

“Ask with focus_query” recipe; RAG with json_blocks+rank_blocks.
