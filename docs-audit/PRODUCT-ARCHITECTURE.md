# PRODUCT-ARCHITECTURE (Phase 5E)

**Agent:** P5-03  
**SoT:** executable code. Wave-4 corrections override prior prose (C1, C2, C6, C8).  
**Docs (`docs/`, README) are untrusted.**  
**Date:** 2026-07-26

---

## 0. Spine hypothesis — tested

Proposed shape:

```
CLIENT/AGENT → MCP/CLI/operator surface → discovery/acquisition → routing →
fetch/browser/managed providers → materialization → structured artifacts →
trust/receipts/verification → higher-level workflows
```

**Verdict: PARTIALLY TRUE as a *transcode-family* narrative; FALSE as the product-wide spine.**

| Claim | Verdict | Why |
|-------|---------|-----|
| Linear client→…→workflows for all tools | **FALSE** | Many tools never enter acquisition/materialization (probe, map, search, heal, resolve, lint, client_capabilities, atlas; verify offline/citation/prove/history). |
| `TranscodePipeline` is *the* central spine | **MISLEADING** (Wave-4 P STATEMENT_1) | Pipeline is the **transcode orchestration shell** (playbook overlay → preflight → `OccamRouter` → post-processors → `FinishMaterialize`). Escalation spine is `OccamRouter`. Flagship tools bypass the pipeline entirely. |
| Cascade = http→browser→managed always | **FALSE** (C1 / EF-056) | 404/410 + public-ref short-circuit; dual-fail uses `FailureRanking`; managed fail never wins surface. |
| Always-on content cache | **FALSE** | Default is live extract; disk cache is opt-in (`cache_ttl_s > 0`). |

**Corrected architecture statement:** Occam has **multiple parallel request spines** sharing a host process, DI, workers, and (sometimes) receipts. The **reference narrative** for “read a page” is `occam_transcode` → `TranscodePipeline` → `OccamRouter` → materialize. That path is **not** universal.

---

## 1. Layer model

| Layer | Owns | Primary code | Notes |
|-------|------|--------------|-------|
| **L0 Exposure** | How a caller reaches the host | `Program.cs`, `Transport/*`, `Cli/OccamCliVerbs.cs`, `scripts/occam.mjs`, install/connect | MCP tools ≠ product. See `ENTRYPOINT-MODEL.md`. |
| **L1 Tool / verb handlers** | Param validate, response JSON | `Tools/*`, CLI verb classes | Thin; delegate to services. |
| **L2 Orchestration spines** | End-to-end request semantics | **A:** `Routing/TranscodePipeline.cs` · **B:** `ProbeService` / `MapService` / `SearchService` · **C:** `KnowledgeExtractService` + `CssExtractWorker` · **D:** `PlaybookHealService` + `DomSkeletonWorker` · **E:** resolve/lint/save (filesystem + signature) · **F:** verify modes (crypto / optional live re-fetch) | Multiple spines; do not collapse to one. |
| **L3 Routing / escalation** | Backend selection under pipeline | `Routing/OccamRouter.cs`, `Backends/*` | Only on spine A (and consumers of A). |
| **L4 Fetch / workers** | HTML/CSS/DOM acquisition | `Workers/*`, `workers/http-extract`, `browser-extract`, `css-extract`, managed HTTP providers | Process topology §4. |
| **L5 Materialization** | Token budget, focus, codec, sidecars | `TranscodePipeline.FinishMaterialize`, `Knowledge/MaterializationPlanner`, `Compile/*`, `Codecs/*` | Mostly spine A; extract_knowledge has its own field plan (no planner/codec path). |
| **L6 Trust / provenance** | Sign, verify, Merkle, capsules | `Receipts/*`, `Claims/*`, `Attest/*`, `Dataset/*`, `PlaybookSignature` | Opt-in / partial (C6, EF-005/044). Not a universal post-layer. |
| **L7 Higher workflows** | Multi-URL / stateful / consensus | `DigestService`, `WatchService`, `ConsensusService`, `Batch/*`, `AttestService` | Compose spine A (or claim_check→A) + store. |
| **L8 Operator / host mutation** | Install, connect, refresh, session | `scripts/lib/operator/**` | Outside MCP request path. |

**Taxonomy mapping (PS-1…9):** layers L3–L5 ≈ PS-1+PS-2; L2-B ≈ PS-3; L2-C ≈ PS-4; L2-E ≈ PS-5; L6 ≈ PS-6; L7 ≈ PS-7; L0/L8 ≈ PS-8+PS-9. Hypothesis accepted with the correction that **playbooks are in-band overlays on spine A**, not a parallel “recipe system” (Wave-4 STATEMENT_7).

---

## 2. Reference narrative — `occam_transcode(url)` all defaults

Defaults from tool surface: `backend_policy=http_then_browser`, `playbook_policy=auto`, all structured/token/watch/trust knobs off (`OccamTranscodeTool.cs:45-68`).

| Step | What happens | Evidence |
|------|----------------|----------|
| 1 | MCP host already running (stdio default via `launch-mcp-host.mjs` → `[]` args → stdio). | `Program.cs:50-55`; CAP-1001 |
| 2 | `OccamTranscodeTool.Transcode` parses policy + builds `OccamTranscodeOptions` (ambient `max_tokens` from `ClientCapabilityStore` if set). | `OccamTranscodeTool.cs:72-95` |
| 3 | Opt-in cache check only if `cache_ttl_s > 0` (default omit → **no cache**). | `TranscodeCacheEligibility.cs:13-16`; tool param `:63` |
| 4 | `pipeline.TranscodeAsync(url, policy, options, ct)`. | `OccamTranscodeTool.cs:168` (typical path; llms.txt branch only if `prefer_llms_txt`) |
| 5 | Pipeline always pushes internal `json_blocks,json_tables` features for Canonical/Planner (public sidecars still opt-in). | `TranscodePipeline.cs:44-55` |
| 6 | `playbook_policy=auto` → `PlaybookSeedResolver.ResolveExtended`; soft `PlaybookVerifyScope` overlay if winning JSON present; may override backend when request policy is `HttpThenBrowser`. | `TranscodePipeline.cs:57-104` |
| 7 | `FocusIntent.FromUrl` (fragment → focus; fetch URL strip). | `TranscodePipeline.cs:112-114` |
| 8 | `FetchPreflight.Prepare` (SSRF/session headers). | `:116-127` |
| 9 | Optional robots throttle (env-gated; default no-op). | `:130-143` |
| 10 | **`OccamRouter.TranscodeAsync`** — http extract; on fail: skip browser if terminal 404/410 or public-ref; else browser; else optional managed if configured + `ShouldAttempt`; else `ChooseRawFallback` by `FailureRanking`. | `OccamRouter.cs:134-182` |
| 11 | Post-processors in order: challenge → requires_login → thin_extract. | `OccamServiceCollectionExtensions.cs:34-36`; pipeline `:153-156` |
| 12 | On success: `FinishMaterialize` → adapt bundle → `MaterializationPlanner.Plan` → default codec encode (passthrough) → budget/projection. | `TranscodePipeline.cs:176-280` |
| 13 | Tool builds response JSON; may attach Receipt v1 if `ReceiptsPolicy.Enabled()`; heal hint policy may fire on typed failures. | `OccamTranscodeTool` + `PlaybookHealPolicy` |
| 14 | Worker processes: HTTP (or http-daemon) and/or browser (or browser-daemon) under `OCCAM_HOME`. | `Workers/*` |

This is the **handbook reference path**. Do not imply every MCP tool follows it.

---

## 3. Parallel / bypass paths (tool → spine)

Legend: **uses** = enters `TranscodePipeline.TranscodeAsync`; **partial** = some modes/paths only; **bypasses** = never calls pipeline.

| Tool / surface | Path taken | Uses pipeline? | Skips (vs transcode spine) | Evidence |
|----------------|------------|----------------|----------------------------|----------|
| `occam_transcode` | Tool → `TranscodePipeline` → Router → PP → materialize | **uses** | — (reference) | `OccamTranscodeTool.cs:19,168`; `TranscodePipeline.cs:28-85` |
| `occam_digest` | `DigestService` → per-URL `pipeline.TranscodeAsync`; optional `MapService` for `source_url` | **uses** (+ map bypass for discovery) | Per-URL combine/focus defaults differ; map discovery skips router | `DigestService.cs:54-59,309`; map via `:83-87` |
| `occam_probe` | `ProbeService` → `HttpProbeFetcher` | **bypasses** | Router, backends workers, post-processors, materialize, receipts | `OccamProbeTool.cs:10`; `ProbeService.cs:7` |
| `occam_map` | `MapService` → `HttpProbeFetcher` (sitemap/homepage crawl) | **bypasses** | Same as probe | `OccamMapTool.cs:10`; `MapService.cs:8` |
| `occam_search` | `SearchService` → provider HTTP; optional probe scoring | **bypasses** | Extract/materialize/receipts | `OccamSearchTool.cs:9`; `SearchService.cs:22` |
| `occam_extract_knowledge` | Resolve playbook schema → `CssExtractWorker` (http then optional browser flag inside css worker) | **bypasses** | TranscodePipeline, Router backends, post-processors, FitMarkdown/token budget, Receipt v1 (telemetry “receipt” only — EF-006) | `KnowledgeExtractService.cs:10-114`; `OccamExtractKnowledgeTool.cs:13` |
| `occam_playbook_heal` | `PlaybookHealService` → `DomSkeletonWorker` → `dom-skeleton-capture.mjs` / daemon `/skeleton` | **bypasses** | Full extract spine, materialize, receipts | `PlaybookHealService.cs:7,46`; `DomSkeletonWorker.cs:10,79` |
| `occam_playbook_resolve` | `PlaybookSeedResolver` (+ optional genome fetch) | **bypasses** | All fetch extract | `OccamPlaybookResolveTool.cs:10` |
| `occam_playbook_lint` | Static lint of playbook JSON | **bypasses** | All network/extract | `OccamPlaybookLintTool.cs:16` |
| `occam_playbook_save` | Write + **always** `PlaybookSignature.BuildSignedJson`; optional verify via `PlaybookSaveVerifier` → pipeline | **partial** | Receipts master switch ignored on sign (EF-005); verify path uses spine | `PlaybookSaveService.cs:86-91`; `PlaybookSaveVerifier.cs:5,24` |
| `occam_verify` | Modes: offline/prove/citation/history = crypto only; **live** re-fetches via pipeline | **partial** | Offline etc. skip acquisition/materialize | `OccamVerifyTool.cs:23,74-79,171` |
| `occam_claim_check` | Forces `JsonBlocks` + `pipeline.TranscodeAsync` + BM25 + Merkle | **uses** | Stance not judged | `ClaimCheckService.cs:25-42` |
| `occam_attest` | Fan-out → `IClaimCheckService` (hence pipeline) | **uses** (indirect) | Aggregate unsigned (GAP-028) | `AttestService.cs:20-35` |
| `occam_dataset_export` | Per-URL `pipeline.TranscodeAsync` + manifest | **uses** | — | `DatasetExportService.cs:25,82` |
| `occam_client_capabilities` | Writes `ClientCapabilityStore` | **bypasses** | All extract | `OccamClientCapabilitiesTool.cs:14` |
| `occam_watch` (opt-in) | `WatchService` → pipeline + `WatchStore` | **uses** | Adds durable watch state | `WatchService.cs:74-91` |
| `occam_crosscheck` (opt-in) | `ConsensusService` → pipeline per backend | **uses** | Multi-backend compare | `ConsensusService.cs:23,81` |
| `occam_batch_*` (opt-in) | Queue → `BatchJobProcessor` → pipeline; **no Receipt v1** (EF-037) | **uses** | Receipts; sync MCP response | `BatchJobProcessor.cs:10,61` |
| `occam_failure_atlas` (opt-in) | Read `FailureAtlasStore` | **bypasses** | Extract | `OccamFailureAtlasTool.cs:16` |
| BatchServer (`--batch-server`) | HTTP API → batch store/processor (no MCP registration) | **uses** (processor) | MCP tools/list; auth | `Program.cs:37-42`; CAP-006 |
| Offline CLI `verify` / `keys` | Host verbs, no transport | **bypasses** | Workers | `Program.cs:12-15` |

### Spine dependency diagram

```
                    ┌─ probe / map ──────────── HttpProbeFetcher
                    ├─ search ───────────────── ISearchProvider
CLIENT ─ exposure ──┼─ extract_knowledge ────── CssExtractWorker
                    ├─ playbook_heal ────────── DomSkeletonWorker
                    ├─ resolve / lint / caps / atlas ── local only
                    └─ transcode family ─── TranscodePipeline ── OccamRouter ── http|browser|managed
                           │                      │
                           │                      └─ post-processors → FinishMaterialize
                           ├ digest / claim_check / attest / dataset / watch / crosscheck / batch
                           └ verify (live only)
```

---

## 4. Process / deployment topology

| Process | Role | How started | Evidence |
|---------|------|-------------|----------|
| **Host** `OccamMcp.Core` (AOT or `dotnet run`) | MCP / CLI / BatchServer | `Program.Main`; operator `launch-mcp-host.mjs` (stdio-only, empty argv — CAP-1001) | `Program.cs`; `SHIPPED-CODE-MAP.md` |
| **stdio session** | One DI container for process lifetime | `StdioMcpTransport` → `AddOccamMcpServer` | CAP-003 |
| **WS / Remote session** | **Per-connection** new `Host` + `AddOccamMcpServer` | `WebSocketMcpTransport.RunSingleSessionAsync` / Remote twin | CAP-1000; C2 |
| **Browser pool (process-wide static)** | Shared slots across sessions | `BrowserPoolManager.InstallShared` in DI factory — **StopAll then replace** on every new DI (EF-041) | `BrowserPoolManager.cs:45-48`; `OccamServiceCollectionExtensions.cs:39-46` |
| **http-daemon** | Persistent Node HTTP extract | Lazy / prewarm `HttpDaemonHost.TryEnsureRunning` | `OccamMcpServerRegistration.cs:40-52` |
| **browser-daemon** | Persistent Playwright pool | Spawned via pool manager / workers | `Workers/BrowserDaemonHost.cs` |
| **One-shot workers** | `extract.mjs`, `browser-extract.mjs`, `css-extract.mjs`, `dom-skeleton-capture.mjs` | Per call when daemon off / path requires | `WorkerPaths.cs` |
| **BatchServer** | Loopback HTTP job API | `--batch-server` | `Program.cs:37-42` |
| **BatchJobProcessor** | HostedService when `OCCAM_BATCH_MCP=1` | DI in registration | `OccamMcpServerRegistration.cs:122-126` |

**EF-041 relevance:** WS/Remote multi-session is not “isolated happy path” for browsers — each session DI reinstalls the shared pool and kills prior slots.

---

## 5. Where state and side effects enter

| Kind | When | Where | Persist? |
|------|------|-------|----------|
| Receipt signing key | Every `AddOccamCore` | `ReceiptSigner.LoadOrCreate()` | Disk key material (EF-044 — minted even if receipts off) |
| Client token budget | `occam_client_capabilities` or env | `ClientCapabilityStore` | In-memory (per DI) |
| Session cookies/headers | `session_profile` / operator session CLI | `OCCAM_SESSIONS_ROOT` | Files |
| Playbook save | `occam_playbook_save` | Seeds / local store + **always signed** | Files (EF-005) |
| Watch history | `occam_watch` | `WatchStore` | `watch.json` (EF-019 multi-process race) |
| Batch jobs | batch MCP or BatchServer | `JsonFileBatchJobStore` | Files (EF-037/038) |
| Failure atlas | atlas opt-in | `FailureAtlasStore` | **In-memory per DI session** (C2) |
| Opt-in response cache | `cache_ttl_s > 0` | `FileTranscodeResponseCache` | Temp dir files |
| Connect mutations | `occam connect` | Host MCP configs ≤15 adapters | Host files (EF-021 rollback gap) |
| Onboard env inject | Every launch | `~/.occam/onboard.json` → process env | EF-050 |
| Process kill | `occam refresh` | Name-wide stop | EF-049 |
| Telemetry / banner | Startup | stderr | — |

---

## 6. Concurrency / lifecycle

| Concern | Behavior | Evidence |
|---------|----------|----------|
| Stdio | Single long-lived host; one DI | `StdioMcpTransport` |
| Local WS | **No** session semaphore; concurrent upgrades each build DI | CAP-1000; A-blind B20 |
| Remote | Semaphore `OCCAM_REMOTE_MAX_SESSIONS` default 4 | CAP-023 |
| Browser concurrency | Pool size + `OCCAM_BROWSER_MAX_PARALLEL` limiter | `BrowserPoolManager` |
| Batch processor | BackgroundService drains store | `BatchJobProcessor` |
| Cancel | Ctrl+C → CTS; exit 0 on cancel | `Program.cs:30-35,62-64` |
| Worker timeouts | HTTP ~35s / browser ~120s (backend constants) | Backends (subsystem runtime-mcp / CODE-MAP) |

---

## 7. Architecture-level engineering findings (reference only — do not fix)

| EF | Architecture impact |
|----|---------------------|
| EF-041 | Per-session DI + `InstallShared` → process-wide browser pool kill |
| EF-005 / EF-044 | Trust layer not gated by single `OCCAM_RECEIPTS` switch (C6) |
| EF-006 | extract_knowledge “receipt” ≠ Receipt v1 |
| EF-037 | Batch path uses pipeline but skips receipts |
| EF-045 | Fragment focus vs cache/materialization key mismatch |
| EF-049 / EF-050 | Operator lifecycle can kill wrong installs / inject global env |
| EF-051 | Docker HEALTHCHECK topology broken (`--version` → stdio hang) |
| EF-056 | Router cascade model corrections (not a code bug) |
| EF-024 | WITHDRAWN — atlas is per-session, not process-wide leak |

---

## 8. What the architecture does NOT do (verified)

| Non-claim | Proof |
|-----------|-------|
| **No persistent content cache by default** | Cache requires `cache_ttl_s > 0`; else eligibility false (`TranscodeCacheEligibility.cs:13-16`). Product design: live extract. |
| **Not every tool goes through `TranscodePipeline`** | §3 table. |
| **Not universal http→browser→managed** | `OccamRouter.cs:145-182`; EF-056. |
| **Managed is not always “last rung safety valve”** | Off by default; with provider set and domains unset, any host eligible (Wave-4 STATEMENT_6 / ManagedExtractBackend). |
| **Receipts are not the sole proof layer for all success paths** | Batch no receipts; extract_knowledge telemetry receipt; save always signs; capsule packaging unsigned (Wave-4 STATEMENT_3). |
| **Canonical IR is not always the live user-visible representation** | Live path often plans then encodes via passthrough; IR work may be discarded (AUTOMATIC #18 / W4-C). |
| **`launch-mcp-host` does not expose WS/Remote/Batch** | Hardcoded `[]` args (CAP-1001). |
| **PlaybookCommunitySanitizer is not on the local save path** | Core-dead (EF-047 / C3). |
| **No CAPTCHA solving** | Failure codes / agent hints only (product trust rule). |

---

## 9. Corrections to prior model

1. Replace “TranscodePipeline = product spine” with “transcode-family orchestration shell; router owns escalation; N parallel spines.”
2. Replace linear discovery→acquisition→… diagram as *universal* with the §3 table + §2 as reference-only.
3. Honor C1/EF-056 cascade facts; C2 atlas DI; C6 receipts incompleteness; C8 dead-but-shipped types still in AOT binary.

---

## 10. Uncertainty

| Item | Status | Resolve by |
|------|--------|------------|
| Exact live EF-041 repro (2 WS sessions killing pool) | Source-proven; runtime not executed in Wave 4 | Optional dual-WS repro |
| Whether any digest path can skip pipeline entirely | Unlikely; `source_url` uses MapService then pipeline per URL | Already code-read |
| Public handbook wording for “spine” | Out of scope (docs frozen) | Later handbook agent |
