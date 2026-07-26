# Change monitoring

**Slug:** `change-monitoring` · **Product system:** PS-7 Monitoring and multi-source · **CAPs:** 19 · **Public relevance:** HIGH

**Member CAPs:** CAP-830…CAP-848  
**Product capability:** CAP-830  
**Engineering findings:** EF-019, EF-020

## What it is

Opt-in MCP tool `occam_watch` (`OCCAM_WATCH_MCP=1`) that fetches a URL through the canonical `TranscodePipeline`, compares against a durable baseline in `WatchStore`, and optionally appends a signed SI-05 history chain. There is **no scheduler/daemon** — each call is caller-driven (`CAP-841`). Consumer edge: `occam_verify(mode="history")` can verify a history payload without watch MCP enabled on the verifying host.

## Why it exists

Agents need a cheap “did this page change?” signal with optional tamper-evident history, without inventing their own hash store. Watch reuses live extract (full session support) rather than a separate crawler (`CAP-848`).

## User-visible entrypoints

| Entrypoint | Role | Evidence |
|------------|------|----------|
| `occam_watch` | Produce verdict + persist | `OccamWatchTool.cs`; `OccamMcpServerRegistration.cs:134-139` |
| `occam_verify` `mode=history` | Verify history JSON | `OccamVerifyTool.cs:42-47,83-127` |
| Manual `watch.json` edit | Only way to delete a URL | CAP-840; EF-020 |

Not registered under any `OCCAM_PROFILE` filter (`CAP-011`). Absent from BatchServer HTTP.

## Core behavior

### Forced blocks + curated params (CAP-830, CAP-831)

`WatchService` sets `JsonBlocks = true` unconditionally (`WatchService.cs:90`). Tool exposes 8 params only: `url`, `backend_policy`, `focus_query`, `session_profile`, `playbook_policy`, `include_diff`, `reset`, `include_history`. `fit_markdown` turns on only when `focus_query` is non-empty. No `max_tokens`, `json_tables`, `if_none_match`, etc.

### Evaluation path

```
TranscodePipeline.TranscodeAsync
  → fail? return failure; store untouched (CAP-842)
  → contentHash = ContentHashToken.BareHex(markdown)
  → WatchEvaluator.Evaluate(prior, hash, blocks, includeDiff)
  → AppendHistory only on first_seen | changed (CAP-836)
  → WatchStore.Upsert → whole-file Persist
```

### Append-only-on-event (CAP-836)

Unchanged calls update `LastSeenAt` and still rewrite the file, but do **not** append history (`LatestEntry` null).

### History window (CAP-838)

`MaxHistoryEntries = 64` (`WatchService.cs:78,176-177`). Silent truncate; no archive. `WatchHistoryChain.Verify` is window-aware (pruned prefix with `Seq > 0` still verifies internal links).

## Advanced behavior

| Behavior | Notes | CAP |
|----------|-------|-----|
| `include_diff` | Gates response only; block-diff always computed for `content_delta_tokens` | CAP-834 |
| `reset=true` | Destructive re-baseline; prior signed chain discarded; no archive | CAP-835 |
| Dual hash families | `BlockDiff.Hash` for detection vs `MerkleTree.Root` for signed root — uncorrelated | CAP-839 |
| Backend in response | Escalation visibility across calls | CAP-843 |
| `content_delta_tokens` | `FocusMatcher.Tokenize` — not token-budget system | CAP-847 |
| Full session support | Same pipeline as transcode (not headers-only EF-017 path) | CAP-848 |
| Dead `IWatchStore.Remove` | Implemented, zero callers | CAP-840 |

## Automatic / silent behavior

| Silent | Effect | Evidence |
|--------|--------|----------|
| Corrupt/unreadable store → empty | Bare `catch { _records.Clear(); }` — no stderr | CAP-832 |
| Key = `url.Trim()` only | Case/slash/query variants = distinct pages | CAP-833 |
| Whole-file rewrite every Upsert | O(records) I/O; uncapped URL count | CAP-846 |
| Signing follows `ReceiptsPolicy` | Unsigned chain still links when receipts off | CAP-844 |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `url` | required | Store key after Trim |
| `backend_policy` | (transcode default path) | Same policies as transcode |
| `focus_query` | unset | Enables fit_markdown + focus |
| `session_profile` | unset | Full pipeline session |
| `playbook_policy` | as parser | Genome overlay allowed |
| `include_diff` | false | Response includes diff payload |
| `reset` | false | Destroy prior chain / baseline |
| `include_history` | false | Echo history in response |

## Configuration

| Env | Default | Effect |
|-----|---------|--------|
| `OCCAM_WATCH_MCP` | off | Tool registration only — no timer |
| `OCCAM_WATCH_DB_PATH` | `~/.occam/watch/watch.json` | Store path (`WatchStore.cs:32`) |
| `OCCAM_RECEIPTS` | (policy) | Whether history entries carry sig |

No `OCCAM_WATCH_MAX_URLS`, TTL, or eviction env (`CAP-841`, `CAP-846`).

## Backends

Via `TranscodePipeline` → `OccamRouter` (HTTP / browser / cascade). Watch does not invent a backend.

## Sessions / state

| State | Class | Notes |
|-------|-------|-------|
| `watch.json` records + history | PERSISTENT (ST-13) | URLs uncapped; history 64/URL |
| In-process lock | PROCESS | Not OS file lock (`CAP-845`) |
| Session profiles | Same as transcode | CAP-848 |

Align with `STATE-MODEL.md` ST-13 and `AUTOMATION-MODEL.md` (no auto-poll).

## Network behavior

Live extract per call. Failures never persist (`CAP-842`). No background network.

## Artifacts produced

| Artifact | Notes |
|----------|-------|
| `WatchRecord` in store | ContentHash, BlockHashes, timestamps, history[] |
| Signed history entries | SI-05; pin sig into next link hash (`CAP-837`) |
| Response diff / backend / delta tokens | Optional fields |

Related ART: ART-025 / ART-028 (`ARTIFACT-ONTOLOGY.md` / `ARTIFACT-MAP.md`).

## Trust / provenance properties

- History signing is **EF-005-compliant** (`EffectiveSigner` via `ReceiptsPolicy`) — unlike playbook_save (`CAP-844`).
- Tamper of body **or** signature breaks next link (`CAP-837`).
- Outer record fields (`LastSeenAt`, baseline hashes) are **not** independently signed — only `history[]` entries.
- Verify proves retained-window integrity, not pruned prefix (`CAP-838`).
- `OCCAM_RECEIPTS=off` → chained but unsigned entries still verify as chain-only.

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Pipeline failure | Typed failure returned; store untouched (`CAP-842`) |
| Corrupt store | Silent empty reset (`CAP-832`) |
| Multi-process same path | Last-write-wins; torn write → next load wipe (`CAP-845`; **EF-019**) |
| No un-watch | Manual file edit only (**EF-020**) |

Failure codes = full transcode taxonomy; watch adds none.

## Platform differences

Home-path defaults OS-dependent. File locking absence is cross-platform (`CAP-845`). No watch-specific platform fork.

## Composition with other capabilities

- **Uses** PS-1/PS-2 pipeline; **produces** PS-6-adjacent history consumable by `occam_verify`.
- Orthogonal to batch/crosscheck/atlas opt-ins.
- Can feed atlas when atlas enabled (pipeline traffic) (`CAP-873`).
- Peer trust: siblings with claim_check/attest, not callers (`PRODUCT-ARCHITECTURE.md` L7).

## Known limitations

- Not a monitor daemon — caller must poll.
- Cannot stop watching via any product surface.
- History is a sliding window, not a full audit log.
- Failed attempts leave no signed trace.
- Store growth unbounded by URL count.
- Multi-host-process sharing of `OCCAM_WATCH_DB_PATH` is unsafe.

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-019** | In-process lock + plain `WriteAllText` — multi-process race; combined with CAP-832 can wipe entire store |
| **EF-020** | No un-watch / eviction API |

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamWatchTool.cs`
- `src/FFOccamMcp.Core/Watch/WatchService.cs:78-177`, `WatchStore.cs`, `WatchHistory.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs:134-139`
- Gate: `benchmarks/l0-gate/ReceiptUnitTests.cs:247-277` (chain only — no WatchStore live gate)
- Deep: `docs-audit/subsystems/watch.md`

## Public-doc relevance

**HIGH** for change-detection recipes. Must disclose: opt-in; no daemon; 64-entry window; `reset` destroys history; no un-watch; verify history mode.

## Handbook relevance

**Workflows:** “re-check a URL” + “prove history”. Pair with receipts chapter. Operator note: do not share one `watch.json` across concurrent hosts.
