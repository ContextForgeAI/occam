# Batch jobs

**Slug:** `batch-jobs` · **Product system:** PS-7 Monitoring and multi-source · **CAPs:** 19 · **Public relevance:** HIGH

**Member CAPs:** CAP-800…CAP-818  
**Product capability:** CAP-800  
**Engineering findings:** EF-037, EF-038

## What it is

Asynchronous multi-URL extraction via a shared job engine (`BatchJobService` + `BatchJobProcessor` + `JsonFileBatchJobStore`) exposed on **two mutually exclusive process surfaces**:

1. Opt-in MCP tools `occam_batch_submit` / `occam_batch_status` / `occam_batch_results` when `OCCAM_BATCH_MCP=1` (`OccamMcpServerRegistration.cs:122-131`).
2. Dedicated HTTP REST process `--batch-server` → `BatchServerHost` on loopback (`CAP-801`, `CAP-816`).

Both reuse the full `TranscodePipeline` / `OccamRouter` per item (`CAP-803`). Batch is **not** an MCP transport (`CAP-801`; `ENTRYPOINT-MODEL.md` BATCH class).

## Why it exists

Operators and agents need to submit many URLs without blocking a single MCP turn, then poll status/results. BatchServer provides the same engine outside MCP JSON-RPC for local automation (`BatchServerHost.cs`; `RUNTIME-MODES.md`).

## User-visible entrypoints

| Entrypoint | Mode | Evidence |
|------------|------|----------|
| `occam_batch_submit` / `status` / `results` | MCP (any transport) when env set | `OccamBatchTools.cs`; CAP-012 |
| `OccamMcp.Core --batch-server [--port N]` | Separate process; plain HTTP | `Program.cs` mode BatchServer; CAP-006 |
| `POST /v1/batch/submit`, `GET /v1/batch/{id}/status`, `GET /v1/batch/{id}/results`, `GET /v1/health` | BatchServer only | CAP-802 |

Canonical launcher `launch-mcp-host.mjs` never starts BatchServer (`CAP-1001`).

## Core behavior

### Shared engine, instance-isolated DI (CAP-800)

Identical classes behind both surfaces; each surface builds its **own** DI container and singleton store/processor. MCP stdio and `--batch-server` never share live objects.

### Submit → process → persist

```
Submit → FetchPreflight per URL + OccamTranscodeOptionsParser.TryBuild (once)
      → store.InsertJob
BatchJobProcessor (BackgroundService) polls ClaimNextPendingItem (200ms idle)
      → SemaphoreSlim(OCCAM_BATCH_PARALLEL) → pipeline.TranscodeAsync
      → BatchItemResult {url, ok, markdown, backend, tokens_estimated, failure}
```

Evidence: `BatchJobService.cs`, `BatchJobProcessor.cs`; subsystem `batch-batchserver.md`.

### Whole-batch fail-closed validation (CAP-813)

First invalid URL or options aborts the entire submit (`invalid_request`). Same shape as digest preflight (CAP-452 analogue).

### Caps (CAP-818, CAP-806)

| Setting | Default | Clamp | Scope |
|---------|---------|-------|-------|
| `OCCAM_BATCH_MAX_URLS` | 64 | 1–256 | Per submit, both surfaces |
| `OCCAM_BATCH_PARALLEL` | 4 | 1–16 | Per processor **instance** |

## Advanced behavior

| Behavior | Notes | CAP |
|----------|-------|-----|
| Idempotency key | 24h window **per store instance** only | CAP-809 |
| `on_oversize=partial` | Same ambient scopes as one-shot transcode | CAP-814 |
| Playbook / session / cascade | Full pipeline semantics apply per item | CAP-803 |
| Dual registration sites | MCP wires inline; BatchServer uses `AddOccamBatch()` | CAP-810 |
| WS/Remote multiplication | Each socket connection → new store + processor → same default path | CAP-811 |

## Automatic / silent behavior

| Silent | Effect | Evidence |
|--------|--------|----------|
| Background processor starts with host | Jobs drain without further MCP calls | `AddHostedService(BatchJobProcessor)` |
| Entire snapshot rewrite on every mutation | Disk I/O scales with all jobs | CAP-807 |
| No eviction | Completed markdown retained forever | CAP-808; ST-17 |
| Shutdown cancel leaves item `running` | Comment admits future reclaim not implemented | CAP-815 |

## Parameters

### MCP / HTTP submit (representative)

| Name | Notes |
|------|-------|
| `urls` | Required array; capped by `OCCAM_BATCH_MAX_URLS` |
| Transcode option subset | Parsed via `OccamTranscodeOptionsParser` (same as `occam_transcode`) |
| `session_profile` | Preflight per URL |
| `idempotency_key` | Optional; instance-local |
| `on_oversize` | Including `partial` | 

Status/results take `job_id` (MCP body) or path id (HTTP). Results support `cursor`/`limit` pagination on HTTP (`CAP-802`).

## Configuration

| Env | Default | Effect |
|-----|---------|--------|
| `OCCAM_BATCH_MCP` | off | Registers three MCP tools + hosted processor |
| `OCCAM_BATCH_PORT` | 5051 | BatchServer listen port |
| `OCCAM_BATCH_DB_PATH` | `~/.occam/jobs/jobs.db` text | Store forces `.json` extension → actual `jobs.json` (`JsonFileBatchJobStore`; CAP-807) |
| `OCCAM_BATCH_MAX_URLS` | 64 | Submit cap |
| `OCCAM_BATCH_PARALLEL` | 4 | Per-instance concurrency |

Bind: BatchServer rejects non-`127.0.0.1` (`CAP-816` / CAP-024 reuse).

## Backends

Per-item: same as `occam_transcode` — HTTP / browser / `http_then_browser` cascade via `OccamRouter` (`CAP-803`). Not a separate extract path.

## Sessions / state

| State | Class | Notes |
|-------|-------|-------|
| Job snapshot JSON | PERSISTENT (ST-17) | Full markdown retained; no delete API |
| Processor / store singletons | PROCESS / SESSION | Per DI container; WS = per connection |
| Session profiles | Via pipeline | Same as transcode |

**Contradiction risk with “no file cache” headline:** batch is the durable extract-result store (`STATE-MODEL.md` ST-17; EF-037).

## Network behavior

Each item performs live fetch through workers. BatchServer itself is **unauthenticated plain HTTP** on loopback only (`CAP-817`) — trust boundary is bind, not auth (contrast Remote TLS+JWT).

## Artifacts produced

| Artifact | Content | Evidence |
|----------|---------|----------|
| `~/.occam/jobs/jobs.json` (or override) | Jobs + full item markdown | ART-027; CAP-807/808 |
| MCP/HTTP status & results DTOs | No receipt, no sidecars | CAP-804, CAP-805 |

## Trust / provenance properties

- **Structurally never produces Receipt v1** — receipt builders live in tool response paths; batch never calls them (`CAP-804`; EF-037).
- Sidecars (`blocks`/`tables`/`quality`/…) stripped (`CAP-805`).
- Results are **not** verifiable via `occam_verify`.
- Store file has no HMAC — OS permissions only.

## Failure / fallback behavior

| Case | Behavior | CAP |
|------|----------|-----|
| Invalid submit | Whole batch rejected | CAP-813 |
| Per-item pipeline failure | Stored on item; job continues | processor |
| Unhandled exception | `failureCode: "transcode_failed"` (**off taxonomy**) | CAP-815 |
| Host shutdown mid-item | Item stuck `running` | CAP-815 |
| Multi-instance same file | Last-writer-wins clobber | CAP-812; EF-038 |

## Platform differences

Path defaults under user home (`PLATFORM-DIFFERENCES.md`). Bind/`127.0.0.1` enforcement is CLI-parse, not OS-specific. No platform-unique batch semantics found.

## Composition with other capabilities

- **Feeds** failure atlas when `OCCAM_ATLAS_MCP=1` in same process (`CAP-877`).
- **Composes** PS-1/PS-2 via pipeline; **does not** compose PS-6 receipts.
- Orthogonal to watch/crosscheck (separate opt-ins).
- Profile-independent registration (`CAP-011`).

## Known limitations

- No receipt / structured sidecars on results.
- No retention, prune, or delete-job API.
- Idempotency and store are not safe across processes (`CAP-809`, `CAP-812`).
- MCP-only agents never see BatchServer exists.
- Docs claiming “shared job store” with BatchServer mean **same path**, not shared live store (`CAP-812`).

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-037** | No Receipt v1; indefinite markdown retention in JSON snapshot |
| **EF-038** | Load-once + whole-snapshot persist → cross-process last-writer-wins |

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamBatchTools.cs`
- `src/FFOccamMcp.Core/Batch/BatchJobService.cs`, `BatchJobProcessor.cs`, `JsonFileBatchJobStore.cs`, `BatchSettings.cs`, `BatchServerHost.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs:122-131`
- Deep: `docs-audit/subsystems/batch-batchserver.md`
- Peers: `STATE-MODEL.md` ST-17, `ENTRYPOINT-MODEL.md`, `RUNTIME-MODES.md`

## Public-doc relevance

**HIGH** for opt-in batch MCP and experimental BatchServer. Must state: no receipts; durable results file; loopback-only unauthenticated HTTP; not reachable via canonical launcher.

## Handbook relevance

**Operator / automation** chapter. Contrast with `occam_digest` (sync, receipt-capable) and “live extract default” messaging. Document multi-process store hazard.
