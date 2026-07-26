# Differential materialization

**Slug:** `differential-materialization` · **Product system:** PS-2 Materialization · **CAPs:** 9 · **Public relevance:** HIGH

**Member CAPs:** CAP-074, CAP-082, CAP-083, CAP-089, CAP-308, CAP-323–CAP-326  
**Product capability:** CAP-074  
**Engineering findings:** None on family ledger (EF-010 related: diff forces blocks).

## What it is

**Conditional and block-level change** responses: whole-body `if_none_match` (unchanged envelope), `diff_against` / `delta_only` block deltas, plus FitMarkdown prune used when shaping content before compare. `occam_watch` reuses the same options/diff plumbing under an env gate.

## Why it exists

Cheap re-reads: detect “same page” without shipping full markdown; or ship only block deltas when the agent already holds prior content.

## User-visible entrypoints

| Param / tool | Default | Effect | Evidence |
|--------------|---------|--------|----------|
| `if_none_match` | null | SHA-256 of compiled markdown; match → `unchanged:true` minimal envelope | CAP-074/323; tool `:54` |
| `diff_against` | null | Prior block hashes → `diff:{addedBlocks,removedHashes,blockHashes}` | CAP-082/325 |
| `delta_only` | false | Empty markdown + delta when preconditions met | CAP-089 |
| `fit_markdown` | false (transcode) | BM25 prune (CAP-308) — shapes hash inputs | |
| `occam_watch` | env `OCCAM_WATCH_MCP=1` | Stateful wrapper | CAP-326 |

## Core behavior

### if_none_match (CAP-323)

- Hash space = bare hex SHA-256 of **final compiled** markdown; accepts optional `sha256:` prefix from receipt `contentHash`.
- On match: `omitHeavySidecars` — empty markdown, no blocks/tables/chunks/feed/media; minimal conditional receipt variant.
- Ineligible for response cache (`TranscodeCacheEligibility` + tool).

### diff_against / delta_only (CAP-325 / CAP-082 / CAP-089)

- Block hash = first 16 hex of SHA-256 over `type+text` concatenation (**no separator** — closed type set assumption).
- Returns added blocks, removed hashes, full current `blockHashes` for next round-trip.
- `delta_only` requires `diff_against` **and** non-empty blocks (`json_blocks`); else soft warning (`delta_only_ignored_no_base` / `_no_blocks`) and full markdown returned.

### Hidden force-blocks (CAP-083 / EF-010)

`diff_against` silently forces full `blocks[]` into the response path even if caller omitted `json_blocks` — needed for hashing; payload-size/privacy implication.

### FitMarkdown (CAP-308)

Membership includes fit prune because it changes compiled markdown (hence contentHash) and surviving blocks. Defaults: transcode false, digest true.

### Dead budget modes (CAP-324)

`ResponseBudgetMode.Unchanged` / `DeltaOnly` exist and are unit-tested but **never** passed from live `TranscodePipeline` — production uses ad-hoc field nulling in the tool. Maintenance trap, not user-facing.

### occam_watch (CAP-326)

Opt-in MCP tool; builds same `OccamTranscodeOptions`; auto-enables `fit_markdown` when `focus_query` set; persists history via WatchService (PS-7).

## Advanced behavior

| Topic | Notes |
|-------|-------|
| Pairing | `if_none_match` = cheap boolean; `diff_against` = what changed |
| Reconstruction | current = prior − removed + added in blockHashes order; verify with contentHash |
| Cache | diff_against / if_none_match ineligible |

## Automatic / silent behavior

| Behavior | Notes |
|----------|-------|
| diff forces blocks | CAP-083 |
| Fit defaults on digest/watch | Changes hash surface |
| Fragment focus changes compile | May change hash without being in cache key (EF-045) |

## Parameters

| Name | Default | Effect when flipped |
|------|---------|---------------------|
| `if_none_match` | unset | Conditional whole-response |
| `diff_against` | unset | Block delta codec; forces blocks |
| `delta_only` | false | Suppress full body when diff ok |
| `json_blocks` | false | Required for honest delta_only |
| `fit_markdown` / `focus_query` | tool defaults | Change compiled body/hash |

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_WATCH_MCP` | off | Exposes watch tool |
| Watch store paths | WatchService | History durability (PS-7) |

No env for hash algorithm (fixed SHA-256 / 16-hex prefix).

## Backends

Post-acquisition host logic. Watch may re-enter TranscodePipeline (acquisition).

## Sessions / state

- Stateless per transcode call for if_none_match/diff (caller holds prior hashes).
- Watch: durable history chain (corrupt store → empty history per FAILURE map).
- Cache disjoint from these features.

## Network behavior

Still performs live fetch unless other gates skip (cache is separate and ineligible here). Unchanged saves **response tokens**, not necessarily network — fetch already happened before hash compare in the success path.

## Artifacts produced

| Artifact | When |
|----------|------|
| `unchanged:true` minimal envelope | if_none_match hit |
| `diff` object | diff_against |
| `deltaOnly:true` + empty markdown | delta_only success |
| Watch history entries | occam_watch |

## Trust / provenance properties

contentHash / blockHashes are integrity aids for **same materialization options**, not proof against hostile site changes mid-flight. delta_only reconstruction must verify returned contentHash. Forced blocks may leak structure the caller did not request (EF-010).

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| Hash mismatch | Full materialization |
| delta_only missing base/blocks | Warning + full markdown |
| Watch store corrupt | Empty history (silent reset) |

## Platform differences

None.

## Composition with other capabilities

- Needs structured blocks (`structured-materialization` / focus).
- Excluded from `response-cache` eligibility.
- Fit/focus (`token-budget` / `focus-selection`) alter hashes.
- Receipts contentHash round-trips into if_none_match.

## Known limitations

- Does not skip network on unchanged (fetch-then-compare).
- Block hash separator assumption.
- Budget Unchanged mode dead (CAP-324).
- CAP-308 fit membership is shared with token-budget — do not double-count as unique feature.

## Engineering findings

| ID | Notes |
|----|-------|
| EF-010 | diff_against forces blocks[] |
| CAP-324 | Dead ResponseBudgetMode.Unchanged/DeltaOnly live wiring |

## Code evidence

- `Tools/OccamTranscodeTool.cs:54,61,67,190,229-244,285-343`
- `Tools/BlockDiff.cs:14-50`
- `Compile/ContentHashToken.cs:13-32`
- `Compile/FitMarkdown.cs:24-134`
- `docs-audit/subsystems/materialization.md` §5

## Public-doc relevance

**HIGH** for agent re-read economy. Document pairing of if_none_match vs diff_against and delta_only preconditions.

## Handbook relevance

Change-detection recipes; watch as opt-in monitoring (point to PS-7 for store semantics).
