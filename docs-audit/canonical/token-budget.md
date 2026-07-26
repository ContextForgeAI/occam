# Token budgeting

**Slug:** `token-budget` · **Product system:** PS-2 Materialization · **CAPs:** 15 · **Public relevance:** HIGH

**Member CAPs:** CAP-060–CAP-063, CAP-066, CAP-067, CAP-300–CAP-305, CAP-309, CAP-310, CAP-337  
**Product capability:** CAP-061  
**Engineering findings:** None on family ledger (EF-055 notes whole-response max_tokens is not a serialized hard bound — adjacent).

## What it is

Allocation and enforcement of **whole-response token budgets** across markdown body and structured sidecars (blocks/tables/chunks/media/feed/receipt), plus honesty markers (`<!-- SNIP -->`) and `compile.omitted` manifests for what was cut.

## Why it exists

Keep extracts inside the client’s context window without silently dropping content. Pair with ambient client capabilities (~20% of context) so agents need not pass `max_tokens` every call.

## User-visible entrypoints

| Surface | Role | Evidence |
|---------|------|----------|
| `max_tokens` on transcode/digest | Explicit budget | `OccamTranscodeTool.cs:48` |
| `occam_client_capabilities` / `OCCAM_CLIENT_CONTEXT_TOKENS` | Ambient default | CAP-304; `ClientCapabilityStore.cs:70-85` |
| `fit_markdown` | Pre-budget BM25 prune | CAP-063; default **false** on transcode, **true** on digest |
| `focus_query` | Selects truncation strategy | CAP-309 |
| Response `compile.omitted` / SNIP markers | Honesty | CAP-067/310 |

## Core behavior

### Ambient resolution (CAP-060 / CAP-304)

- Explicit `max_tokens` wins.
- Else if client capabilities configured: `clamp(round(context * 0.20), 512, 16384)` (`OutputFractionOfContext=0.20`, `MinOutputBudget=512`, `MaxOutputBudget` in store).
- Else: full payload (no ambient trim).
- Process-scoped singleton — concurrent WS sessions share ambient default (materialization.md CAP-304 gap).

### Parameter floor (CAP-305)

Minimum **128** tokens at parameter layer (`OccamTranscodeTool` description).

### Two-layer ownership (CAP-061 / CAP-300)

`BudgetOwnership` / `ResponseBudgetPlanner` / `MaterializationPlanner`: markdown floor + structured sidecar pool. Trim order for structured is fixed greedy prefix (`TrimStructured` — CAP-301).

### Truncation strategies (CAP-066 / CAP-309)

`TokenBudget.Apply`:

| Strategy | When |
|----------|------|
| `focus_window` | Focus query/fragment + section found |
| `sandwich` (head+tail + SNIP) | Focus path fallback when no section |
| `head_safe` | No focus |

Script-aware char budgets via `TokenEstimator` (not flat tokens×4).

### Omitted manifest (CAP-067 / CAP-310)

Structured `compile.omitted` + per-bucket drop counts from planner. In-band SNIP comments remain even without reading the manifest.

### Fit markdown (CAP-063)

BM25 paragraph prune; thresholds hardcoded (`minScore` focused 0.06 / unfocused 0.12). English boilerplate denylist — non-English chrome not list-filtered.

## Advanced behavior

| CAP | Notes |
|-----|-------|
| CAP-302 | Per-item estimators for block/table/chunk/media/feed |
| CAP-303 | `ResponseBudgetDiagnostics` computed but **never surfaced** (SHIPPED_DEAD exposure) |
| CAP-337 | `OCCAM_CHUNK_SIZE` is **chars**, while host budgets are **tokens** — unit mismatch |

## Automatic / silent behavior

| Behavior | Notes |
|----------|-------|
| Ambient client budget applies when max_tokens omitted | AUTOMATION #15 |
| Internal Canonical retention ~25% surface tokens | Computed then discarded by passthrough codec (EF-004 / structured family) — **not** the public budget |
| Digest defaults `fit_markdown=true` | Differs from transcode |

## Parameters

| Name | Default (transcode) | Effect |
|------|---------------------|--------|
| `max_tokens` | null → ambient or full | Whole-response budget; min 128 |
| `fit_markdown` | `false` | BM25 prune before/with budget |
| `focus_query` | null | Focus-centered truncation + fit targeting |
| `content_selectors` | null | Heading-anchor keep set (focus family) |

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_CLIENT_CONTEXT_TOKENS` | unset | Bootstraps ambient output budget |
| `OCCAM_CLIENT_MODEL_ID` | unset | Advisory metadata on capabilities |
| `OCCAM_CHUNK_SIZE` | 2000 **chars** | Chunk plugin only (CAP-337) |

No env to change BM25 thresholds or OutputFraction (hardcoded 0.20).

## Backends

Host-side compile after acquisition. Independent of http/browser once markdown exists. Worker chunking size is separate (char-based).

## Sessions / state

`ClientCapabilityStore` is process-wide mutable. Budget itself is per-call. Cache keys include `max_tokens` / fit / focus (`TranscodeCacheKey.cs:27-29`).

## Network behavior

None (post-fetch). Translate codec may call network — structured family.

## Artifacts produced

Truncated markdown; `compile.budget` / `compile.omitted`; SNIP HTML comments; focus/completeness semantics (assessment family overlap).

## Trust / provenance properties

Truncation is self-describing (SNIP + omitted). Does **not** guarantee a serialized hard byte/token ceiling for every sidecar combination (EF-055 adjacency). Dropped Canonical IR is invisible — do not claim “knowledge retained within budget” for Canonical claims.

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| Answer unit truncated | `completeness=Incomplete` / `focus_body_truncated` (CAP-311) |
| No focus match | `source_missing`-class focus signals |
| Diagnostics unused | Agents cannot see PlannerRetries (CAP-303) |

## Platform differences

TokenEstimator script heuristics are Unicode-general; no OS-specific budget math. Tokenizer error bounds for `heuristic-unicode-v1` unmeasured (CANONICAL-AUDIT-INDEX incompleteness).

## Composition with other capabilities

- Consumes output of acquisition + post-processors via `FinishMaterialize`.
- `focus-selection` chooses what survives under budget.
- `structured-materialization` sidecars compete for the same pool (CAP-301).
- `differential-materialization` may omit body entirely (`unchanged` / `delta_only`) outside planner Unchanged mode (dead enum — CAP-324).

## Known limitations

- Ambient store not per-session isolated under multi-client WS.
- Fit thresholds not tunable.
- CAP-303 diagnostics hidden.
- Chunk size vs token budget unit mismatch (CAP-337).
- Name “token” for chunks is misleading (chars).

## Engineering findings

| ID | Notes |
|----|-------|
| EF-055 | max_tokens not a serialized hard bound (adjacent) |
| CAP-303 | Dead exposure of diagnostics |

## Code evidence

- `Client/ClientCapabilityStore.cs:15-85`
- `Compile/TokenBudget.cs:7-84,316-341,485-551`
- `Compile/FitMarkdown.cs:24-134`
- `Knowledge/ResponseBudgetPlanner.cs`, `BudgetOwnership.cs`
- `docs-audit/subsystems/materialization.md` §§1–2

## Public-doc relevance

**HIGH.** Document ambient 20% rule, min 128, fit defaults differ by tool, and omitted/SNIP honesty.

## Handbook relevance

Token economy chapter; agent recipe “call client_capabilities once.”
