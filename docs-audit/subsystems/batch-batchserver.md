# S3-01 — Batch MCP tools + BatchServer HTTP

**Agent:** S3-01 (Wave 3)
**CAP range:** CAP-800…829 (allocated CAP-800…818; 819–829 reserved)
**SoT:** current executable code only. Docs untrusted.
**Reused CAPs (Wave 1/2):** CAP-006, CAP-011, CAP-012 (runtime-mcp.md), CAP-024 (bind policy), CAP-069/CAP-356 (session profile), CAP-070 (playbook_policy=auto), CAP-254/CAP-372 (Receipt v1 signer + master switch), CAP-452 (whole-batch fail-closed preflight pattern, digest analogue)

---

## AUDIT TARGET

Both batch surfaces named in the task:

1. **Opt-in MCP tools** — `occam_batch_submit` / `occam_batch_status` / `occam_batch_results`, gated by `OCCAM_BATCH_MCP=1`.
2. **BatchServer HTTP** — `OccamMcp.Core --batch-server` → `BatchServerHost`, plain REST on `/v1/*`.

## FILES INSPECTED

- `src/FFOccamMcp.Core/Tools/OccamBatchTools.cs`
- `src/FFOccamMcp.Core/Batch/BatchModels.cs`
- `src/FFOccamMcp.Core/Batch/BatchJobService.cs`
- `src/FFOccamMcp.Core/Batch/BatchJobProcessor.cs`
- `src/FFOccamMcp.Core/Batch/IBatchJobStore.cs`
- `src/FFOccamMcp.Core/Batch/JsonFileBatchJobStore.cs`
- `src/FFOccamMcp.Core/Batch/BatchSettings.cs`
- `src/FFOccamMcp.Core/Batch/BatchServiceCollectionExtensions.cs`
- `src/FFOccamMcp.Core/Batch/BatchServerHost.cs`
- `src/FFOccamMcp.Core/Batch/BatchJsonContext.cs`
- `src/FFOccamMcp.Core/Program.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs` (batch wiring block, lines 120-131)
- `src/FFOccamMcp.Core/Transport/OccamMcpCli.cs` (mode parse, bind policy)
- `src/FFOccamMcp.Core/Transport/StdioMcpTransport.cs`, `WebSocketMcpTransport.cs`, `RemoteMcpTransport.cs` (host-build pattern, per-connection re-registration)
- `src/FFOccamMcp.Core/Composition/OccamServiceCollectionExtensions.cs` (`AddOccamCore`)
- `src/FFOccamMcp.Core/Routing/TranscodePipeline.cs`, `TranscodeOutcome.cs`
- `src/FFOccamMcp.Core/Tools/OccamTranscodeTool.cs` (receipt construction site, for contrast)
- Docs cross-checked (untrusted, comparison only): `docs/tools/occam_batch.md`, `docs/transports.md`, `docs/configuration.md`, `MCP_API_SPEC.md`

## EXECUTABLE ENTRYPOINTS

1. `occam_batch_submit` / `occam_batch_status` / `occam_batch_results` (MCP tool call, any transport, when `OCCAM_BATCH_MCP=1`)
2. `OccamMcp.Core --batch-server [--port N]` → `BatchServerHost.RunAsync` → `POST /v1/batch/submit`, `GET /v1/batch/{jobId}/status`, `GET /v1/batch/{jobId}/results`, `GET /v1/health`

---

## RUNTIME PATH (both surfaces)

```
MCP surface:
  Program.cs (mode=Stdio/WebSocket/Remote)
    → transport.StartAsync → Host.CreateApplicationBuilder().Services.AddOccamMcpServer()
        → AddOccamCore() (TranscodePipeline, OccamRouter, ReceiptSigner, …)
        → if OCCAM_BATCH_MCP=1: inline AddSingleton(IBatchJobStore=JsonFileBatchJobStore, IBatchJobService=BatchJobService)
                                 + AddHostedService(BatchJobProcessor) + WithTools<3 batch tools>
    → host.RunAsync() starts BatchJobProcessor as a real BackgroundService inside the MCP process

BatchServer surface:
  Program.cs (mode=BatchServer, chosen instead of any MCP transport — mutually exclusive)
    → BatchServerHost.RunAsync → WebApplication.CreateSlimBuilder()
        → builder.Services.AddOccamBatch() = AddOccamCore() + AddSingleton(IBatchJobStore/IBatchJobService) + AddHostedService(BatchJobProcessor)
        → app.MapGet/MapPost /v1/* (plain minimal-API REST, NOT MCP JSON-RPC)
    → app.RunAsync()

Shared downstream (both):
  BatchJobService.Submit → FetchPreflight.Prepare (per URL) + OccamTranscodeOptionsParser.TryBuild (once) → store.InsertJob
  BatchJobProcessor.ExecuteAsync → store.ClaimNextPendingItem (200ms poll) → SemaphoreSlim(Parallel) → pipeline.TranscodeAsync(url, policy, options)
    → TranscodeOutcome → BatchItemResult (url, ok, markdown, backend, tokens_estimated, failure) → store.MarkItemComplete
```

---

## CAPABILITIES

### CAP-800 — Batch execution core is code-shared, instance-isolated
- **Impl:** `BatchJobService`, `BatchJobProcessor`, `IBatchJobStore`/`JsonFileBatchJobStore` are the identical classes behind both surfaces.
- **But:** each surface builds its **own** DI container and therefore its **own** singleton instances of all three. There is no shared runtime object between an MCP-mode process and a BatchServer process (or between two MCP connections — see CAP-811).
- **Confidence:** PROVEN

### CAP-801 — BatchServer is a distinct execution model, not an MCP transport
- **Impl:** `BatchServerHost.RunAsync` — `WebApplication.CreateSlimBuilder()` + `app.MapGet/MapPost("/v1/…")`. No `ModelContextProtocol` server, no `tools/list`, no `initialize` handshake, no JSON-RPC envelope, no `occam_client_capabilities` budget concept — the endpoints return the batch DTOs directly as plain JSON over HTTP status codes (202/200/400/404/503).
- **Selection:** `Program.cs` chooses **exactly one** of `{Stdio, WebSocket, Remote, BatchServer}` per process invocation via `cli.Mode` — BatchServer cannot coexist with an MCP transport in the same process.
- **Answer to task question:** BatchServer is **not** "another transport for the same MCP protocol" the way WebSocket/Remote are (those still speak MCP JSON-RPC and expose `tools/list`). It is a separate, non-MCP HTTP micro-API that happens to reuse the transcode pipeline and the batch job engine. The opt-in MCP batch tools, by contrast, are ordinary MCP tools inside the normal JSON-RPC surface.
- **Confidence:** PROVEN

### CAP-802 — HTTP endpoint contract (BatchServer)
- **Impl:** `GET /v1/health` → `{ok, version, workers}` (503 when store not initialized); `POST /v1/batch/submit` → 202 + `BatchSubmitResponse`, or 400 `BatchErrorResponse`; `GET /v1/batch/{jobId}/status` → `BatchStatusResponse` or 404/400; `GET /v1/batch/{jobId}/results?cursor&limit` → `BatchResultsResponse` or 404/400.
- **Divergence from MCP tool surface:** job id travels in the URL path for status/results (not a JSON body field named `job_id` as in the MCP tools); submit is `202 Accepted` (async semantics visible at the transport level) vs. the MCP tool's synchronous string return of the same payload.
- **Confidence:** PROVEN

### CAP-803 — Batch reuses the full `TranscodePipeline`/`OccamRouter`, not a stripped-down path
- **Impl:** `BatchJobProcessor.ProcessItemAsync` calls `pipeline.TranscodeAsync(item.Url, policy, options, ct)` — the exact same `TranscodePipeline` singleton (or, for BatchServer, its own instance of the same class) used by `occam_transcode`.
- **Consequences (all apply to batch items):** `playbook_policy=auto` genome/seed resolution + overlay (`PlaybookSeedResolver.ResolveExtended`, `PlaybookVerifyScope`), `session_profile` cookie/header injection (`FetchPreflight.Prepare(url, options.SessionProfile)`), `backend_policy` escalation (`http`/`browser`/`http_then_browser`), `FocusIntent.FromUrl` fragment handling, robots/throttle service, all `ITranscodePostProcessor`s (challenge/login/thin-extract).
- **Confidence:** PROVEN

### CAP-804 — Batch structurally never produces a Receipt v1
- **Impl:** `TranscodeOutcome` (the pipeline's return type) carries **no** receipt field at all. Receipt construction (`OccamTranscodeResponseBuilder.BuildReceipt`, `ReceiptsPolicy.Enabled() ? receiptSigner : null`) happens exclusively inside `OccamTranscodeTool`/`OccamDigestTool` response-building code, which `BatchJobProcessor` never calls. `BatchItemResult` (the batch result DTO) has no receipt property in its schema (`url, ok, markdown, backend, tokens_estimated, failure`).
- **Implication:** unlike `occam_transcode`/`occam_digest`/`occam_dataset_export`, batch results (either surface) are **not** verifiable via `occam_verify` and carry no signed provenance — this is a structural gap, not a policy toggle (`OCCAM_RECEIPTS` is irrelevant here because the call site that would consult it doesn't exist in the batch path).
- **Confidence:** PROVEN

### CAP-805 — Batch strips all structured sidecars
- **Impl:** `TranscodeOutcome` carries `Chunks`, `Blocks`, `Tables`, `Feed`, `MediaRefs`, `Quality`, `Confidence`, `Omitted`, `Budget`, `Session` — none of these survive into `BatchItemResult` (`BatchJobProcessor.ProcessItemAsync` only copies `Markdown`, `Backend`, `TokensEstimated`, `FailureCode`/`Message`).
- **Confidence:** PROVEN

### CAP-806 — Concurrency model (shared code, per-instance semaphore)
- **Impl:** `BatchJobProcessor` holds one `SemaphoreSlim(BatchSettings.Parallel, BatchSettings.Parallel)` per **processor instance**. `ExecuteAsync` polls `store.ClaimNextPendingItem()` every 200ms when idle; on a hit it awaits the semaphore then fires `Task.Run(ProcessItemAsync)` (fire-and-forget, no join, no per-item timeout beyond whatever the pipeline itself enforces).
- **Env:** `OCCAM_BATCH_PARALLEL` (default 4, clamp 1-16) — **per processor instance**, so N concurrent processor instances (CAP-811) each get their own independent semaphore of that size; there is no global cap across instances.
- **Confidence:** PROVEN

### CAP-807 — Persistence model: whole-snapshot JSON file, load-once
- **Impl:** `JsonFileBatchJobStore` keeps the entire job set as one in-memory `Dictionary`/`BatchStoreSnapshot`, loaded from disk **exactly once** in `Initialize()` (lazily via `EnsureInit()`), and re-serializes the **entire snapshot** to a temp file + atomic rename on every single mutation (`InsertJob`, `ClaimNextPendingItem`, `MarkItemRunning`, `MarkItemComplete`, `MarkJobFailed`).
- **Path:** `OCCAM_BATCH_DB_PATH` (default `~/.occam/jobs/jobs.db`, but the store does `Path.ChangeExtension(…, ".json")` — actual on-disk default is `~/.occam/jobs/jobs.json`, not `.db`; see DOC GAPS).
- **Note (code comment):** deliberately replaces a prior SQLite-backed store to drop a native SQLite CVE and native-interop dependency; concurrency guarantee is stated as "identical to the original store" (single in-process lock) — this claim holds **within one process** but not **across** processes/instances (CAP-812).
- **Confidence:** PROVEN

### CAP-808 — No retention/eviction — batch results persist indefinitely
- **Impl:** grep of `Batch/*` for prune/expire/TTL/retention/delete-job finds nothing. Every completed item's `ResultJson` (which **includes the full extracted markdown**, per CAP-805's surviving fields) is written into the on-disk snapshot and never removed by any code path on either surface.
- **Product-claim tension:** AGENTS.md/CLAUDE.md headline "No file cache by design — every call is live extraction." Batch (both surfaces, whenever `OCCAM_BATCH_MCP=1` or `--batch-server` is used) is the one code path in the product that durably writes extracted page markdown to a persistent, un-evicted disk file, keyed by job/URL, readable later via `occam_batch_results`/`GET /v1/batch/{jobId}/results` without a re-fetch. See ENGINEERING-FINDINGS EF-019 candidate.
- **Confidence:** PROVEN

### CAP-809 — Idempotency window is per-store-instance, not global
- **Impl:** `BatchJobService.Submit` — when `idempotency_key` is set, `store.FindJobByIdempotencyKey(key, DateTimeOffset.UtcNow - 24h)` scans **that store instance's** in-memory `_snapshot.Jobs`. Because BatchServer and MCP-mode (and each WS/Remote connection, CAP-811) each hold their own instance, the same idempotency key submitted to two different instances (even against the same file path) will **not** be recognized as a duplicate by the second instance until/unless it happens to have loaded a snapshot that already contains it.
- **Confidence:** PROVEN

### CAP-810 — Two independent registration call sites for the same trio of services
- **Impl:** MCP-mode wires `IBatchJobStore`/`IBatchJobService`/`BatchJobProcessor` **inline** in `OccamMcpServerRegistration` (lines 124-126), directly on the MCP server's `IServiceCollection` — it does **not** call `BatchServiceCollectionExtensions.AddOccamBatch()`. BatchServer calls `AddOccamBatch()` (`Batch/BatchServerHost.cs:22`), which itself calls `AddOccamCore()` again (a second, separate full core wiring, since BatchServer's `WebApplication.CreateSlimBuilder()` has no relation to the MCP host's DI).
- **Risk:** the same three registrations are hand-duplicated at two call sites; a future edit to one (e.g. adding a 4th batch service, or changing store DI lifetime) is not guaranteed to be mirrored in the other.
- **Confidence:** PROVEN

### CAP-811 — Per-connection multiplication under WebSocket/Remote transports
- **Impl:** `WebSocketMcpTransport`/`RemoteMcpTransport` build a **brand-new** `Host.CreateApplicationBuilder().Services.AddOccamMcpServer()` for **every accepted socket connection** (`WithStreamServerTransport(input, output)` per connection). With `OCCAM_BATCH_MCP=1`, every concurrent WS/Remote client therefore gets its **own** `BatchJobProcessor` BackgroundService + its own `JsonFileBatchJobStore` instance, all defaulting to the same on-disk path unless the operator sets a per-connection-unique `OCCAM_BATCH_DB_PATH` (which the env-var model does not support — it is process-wide, not per-connection).
- **Confidence:** PROVEN

### CAP-812 — [HIDDEN / RISK] Cross-instance last-writer-wins clobber on the batch job file
- **Impl:** Because `JsonFileBatchJobStore` reads the snapshot file exactly once (CAP-807) and never re-reads it, and because every mutation persists the **entire** in-memory snapshot (not a diff/merge), running more than one store instance against the same file concurrently — e.g. a `--batch-server` process **and** a stdio/WS MCP process with `OCCAM_BATCH_MCP=1`, or two WS connections (CAP-811) — is a genuine last-writer-wins hazard: a job inserted/updated by instance A can be silently reverted or erased the next time instance B (holding a stale in-memory copy) persists.
- **Confidence:** PROVEN (static trace of `Initialize`/`Persist`/`EnsureInit`; no cross-process lock, no file-mtime check, no re-read anywhere in `JsonFileBatchJobStore`)
- **Doc contradiction:** `docs/transports.md` states BatchServer "Shares job store with `OCCAM_BATCH_MCP=1` MCP tools when configured" — true only in the narrow sense of "same file path if `OCCAM_BATCH_DB_PATH` matches"; it is **not** a shared live store and the two instances can corrupt each other's data as described above. See ENGINEERING-FINDINGS EF-020 candidate.

### CAP-813 — Submit validation is whole-batch fail-closed
- **Impl:** `BatchJobService.TryValidateSubmit` loops every URL through `FetchPreflight.Prepare(url, session_profile)` and builds transcode options **once** via `OccamTranscodeOptionsParser.TryBuild`; the **first** invalid URL or invalid option aborts the **entire** submit with `invalid_request` — no partial acceptance, no per-URL rejection list.
- **Pattern reuse:** structurally identical to `occam_digest`'s documented whole-batch preflight gate (CAP-452) — same shape, different tool (`occam_batch_submit` / `POST /v1/batch/submit`).
- **Confidence:** PROVEN

### CAP-814 — Per-item `on_oversize=partial` reuses the exact one-shot ambient scopes
- **Impl:** `BatchJobProcessor.ProcessItemAsync` pushes `HttpExtractOversizeScope.PushPartial()` (when `item.Params.OnOversize == "partial"`) and always pushes `HttpExtractRoutingScope.PushOneShot()` around the pipeline call — the identical ambient-scope mechanism used by a single synchronous `occam_transcode` call with `on_oversize=partial`. Confirms batch items are not a specialized "batch fast path" — they are ordinary one-shot transcodes driven in a loop.
- **Confidence:** PROVEN

### CAP-815 — Failure code hygiene gap: `transcode_failed` is off-taxonomy
- **Impl:** unhandled exceptions in `ProcessItemAsync`'s catch block are stored with `failureCode: "transcode_failed"` — a code that does not appear in the canonical failure taxonomy (`docs/failure-codes.md` lists `invalid_arguments`, `workers_unavailable`, `timeout`, `extraction_failed`, `thin_extract`, `captcha_or_challenge`, `requires_login`, `http_403`, `http_404`, `response_too_large`, `private_url_blocked`, `dns_error`, `tls_error`, `network_error`, etc. — no `transcode_failed`).
- **Also:** `OperationCanceledException` on host shutdown is caught and the item is left in `running` state permanently — the code comment explicitly acknowledges "next start may reclaim stale rows in a future slice" (not implemented).
- **Confidence:** PROVEN

### CAP-816 — BatchServer bind/port policy
- **Impl:** `--batch-server` defaults to port `5051` (`OCCAM_BATCH_PORT`, clamp 1-65535); `OccamMcpCli` parse-time check rejects any bind address other than `127.0.0.1` for `BatchServer` mode (same rule as local WebSocket mode; contrasts with `Remote` mode which allows arbitrary bind for TLS+JWT use).
- **Confidence:** PROVEN (reuses CAP-024 bind-policy finding, batch-specific instance)

### CAP-817 — BatchServer is unauthenticated plain HTTP
- **Impl:** no TLS, no JWT, no API key/token check anywhere in `BatchServerHost`. The sole boundary control is the loopback-only bind (CAP-816). Contrasts with `RemoteMcpTransport` (TLS cert + JWT metadata required). `docs/transports.md` labels it "(experimental)".
- **Confidence:** PROVEN

### CAP-818 — `OCCAM_BATCH_MAX_URLS` enforced once, shared by both surfaces
- **Impl:** `BatchSettings.MaxUrls` (default 64, clamp 1-256) is checked inside `BatchJobService.TryValidateSubmit`, so both `occam_batch_submit` and `POST /v1/batch/submit` inherit the identical cap and identical error message shape (`invalid_request`, "urls exceeds OCCAM_BATCH_MAX_URLS (N)").
- **Confidence:** PROVEN

---

## CAPABILITY GRAPH EDGES

```
occam_batch_submit        USES        CAP-803 (TranscodePipeline reuse)
occam_batch_submit        USES        CAP-813 (whole-batch fail-closed preflight)
occam_batch_submit        SIMILAR_TO  CAP-452 (occam_digest whole-batch preflight)
occam_batch_submit        USES        CAP-069 / CAP-356 (session_profile)
occam_batch_submit        USES        CAP-070 (playbook_policy=auto genome merge)
occam_batch_status        USES        CAP-807 (JsonFileBatchJobStore read path)
occam_batch_results       USES        CAP-805 (sidecar-stripped BatchItemResult)
occam_batch_results       LACKS       CAP-254 / CAP-372 (Receipt v1 signer / master switch — never invoked, CAP-804)
BatchServerHost            DISTINCT_FROM  CAP-003/004/005 (stdio/WS/Remote — not an MCP transport, CAP-801)
BatchServerHost            USES        CAP-807, CAP-806, CAP-803 (same engine as MCP batch tools)
BatchServerHost            SHARES_FILE_WITH_RACE  OCCAM_BATCH_MCP=1 MCP mode (CAP-812)
OCCAM_BATCH_MCP=1 (CAP-012) MULTIPLIED_BY  CAP-811 (per-WS/Remote-connection re-registration)
CAP-812                    CONTRADICTS  docs/transports.md "shares job store" claim
CAP-808                    CONTRADICTS  AGENTS.md/CLAUDE.md "No file cache by design" headline claim
```

---

## ARTIFACTS CREATED / CONSUMED

- **Created (disk):** one JSON snapshot file per store instance's configured path — default `~/.occam/jobs/jobs.json` (despite `OCCAM_BATCH_DB_PATH` default text and doc naming it `jobs.db`), or `Path.ChangeExtension(OCCAM_BATCH_DB_PATH, ".json")` when overridden. Contains full job metadata **and** every completed item's extracted markdown, indefinitely (CAP-808).
- **Consumed:** none beyond the standard `TranscodePipeline` inputs (worker processes, browser pool, playbook seeds, session profile files) — batch does not read any batch-specific config file besides the env vars in `BatchSettings`.

## INVISIBLE PRODUCT — what an MCP-only user never sees

An agent that only ever calls `occam_batch_submit`/`status`/`results` over MCP:

- **Never sees BatchServer at all.** There is no MCP-visible signal that a second, unauthenticated, loopback HTTP REST surface exists that can drive the identical job engine from outside the MCP session (`--batch-server`, operator-only, invoked as a completely separate process/binary run).
- **Never sees that its "batch job" has no receipt.** Every other extraction tool (`occam_transcode`, `occam_digest`, `occam_dataset_export`) mentions or carries `receipt`; batch results silently omit the concept — an agent relying on "everything Occam returns is receipt-backed" (a reasonable inference from the other tools) will be wrong specifically for batch.
- **Never sees that results are being written to a permanent, un-evicted file on the host's disk** (`~/.occam/jobs/jobs.json`), containing the full markdown of every page ever batch-fetched by that host, readable indefinitely by anyone with local file access to that path — a materially different data-retention posture than every other tool's "live extract, nothing kept" model.
- **Never sees the multi-instance race.** If the operator also runs `--batch-server`, or the client reconnects over WebSocket multiple times, the agent's own job can be silently lost/overwritten with no error surfaced anywhere in the MCP response (CAP-812) — from the agent's point of view a previously-`done` job could regress to missing or `queued` for no visible reason.
- **Never sees that `occam_batch_submit`'s options are validated exactly like `occam_transcode`'s** (same `OccamTranscodeOptionsParser`), so batch's actual capability ceiling (playbook policy, session profile, backend escalation) matches the synchronous tool's, even though the batch tool's parameter list looks like a small, separate subset.

## HIDDEN / ADVANCED

- BatchServer vs. MCP `OCCAM_BATCH_MCP` are **two independently-reachable front doors to the same underlying job engine**, not variants of one feature (CAP-800/801).
- Per-WS/Remote-connection re-registration silently multiplies `BatchJobProcessor` instances (CAP-811) — an operator running several agent sessions over WebSocket with batch enabled ends up with N independent poll loops against the same file.
- `on_oversize=partial` batch items funnel through the same ambient one-shot scopes as `occam_transcode` (CAP-814) — no batch-specific fast path exists despite the "batch" branding.
- Default DB filename in code (`jobs.json`) diverges from both the env var name (`OCCAM_BATCH_DB_PATH` default text `.db`) and `docs/configuration.md`'s documented default (`~/.occam/jobs/jobs.db`) — see DOC GAPS.

## CONFIG / ENV (this subsystem)

`OCCAM_BATCH_MCP` (MCP tool registration gate), `OCCAM_BATCH_PORT` (BatchServer port, default 5051), `OCCAM_BATCH_MAX_URLS` (default 64, clamp 1-256), `OCCAM_BATCH_PARALLEL` (default 4, clamp 1-16, per-instance), `OCCAM_BATCH_DB_PATH` (override path; actual on-disk extension forced to `.json`).

## FAILURES

`invalid_request` (bad urls/options/on_oversize, both surfaces), `job_not_found` (mapped to HTTP 404 on BatchServer; plain error object on MCP), per-item failures use the standard transcode taxonomy **plus** the off-taxonomy `transcode_failed` (CAP-815) for unhandled processor exceptions.

## SECURITY / TRUST

- BatchServer: loopback-only bind (CAP-816), **no auth** (CAP-817) — anyone with local access to port 5051 can submit/read jobs, including whatever markdown a previous job captured.
- Both surfaces inherit the same SSRF/session preflight as `occam_transcode` (`FetchPreflight.Prepare`, whole-batch fail-closed, CAP-813) — no SSRF gap specific to batch was found.
- No receipt/signature ever attached to batch output (CAP-804) — batch results are not part of the Receipt v1 trust chain and cannot be fed to `occam_verify`.
- Persistent on-disk markdown with no eviction (CAP-808) is a data-retention/privacy consideration not covered by any existing security review in this repo's audit trail.

## TEST EVIDENCE

No batch-specific gate level found in `benchmarks/l0-gate` naming (L0-L9 per CLAUDE.md do not list a batch level; `L5_BATCH_OK` is referenced in AGENTS.md's gate marker list but this agent did not locate/execute it — out of scope for a read-only audit). Not independently verified this wave; flag as UNCERTAIN.

## DOC GAPS

- `MCP_API_SPEC.md` has **zero** mentions of `occam_batch_submit`/`status`/`results` or BatchServer (confirmed via grep) — violates the repo's own doc-sync rule ("Any MCP tool signature/defaults changed → `docs/tools-reference.md`, `MCP_API_SPEC.md`").
- `docs/transports.md` "Shares job store with `OCCAM_BATCH_MCP=1` MCP tools when configured" overstates the reality — see CAP-812.
- `docs/configuration.md` documents `OCCAM_BATCH_DB_PATH` default as `~/.occam/jobs/jobs.db`; code's actual default write path is `~/.occam/jobs/jobs.json` (leftover from a SQLite→JSON store migration recorded in a code comment).
- No public doc states that batch results carry no receipt, or that job data is retained indefinitely on disk — both are material trust/privacy facts missing from `docs/tools/occam_batch.md`.

## UNCERTAINTIES

- Whether `L5_BATCH_OK` gate (named in AGENTS.md) actually exercises the two-surface interaction (concurrent BatchServer + MCP-mode) described in CAP-812, or only one surface in isolation — not verified this wave (no test execution performed, read-only code audit).
- Whether any operator documentation (outside `docs/`) warns against running both surfaces against the same `OCCAM_BATCH_DB_PATH` — not found in this repo's committed docs.
- Exact behavior when the on-disk JSON snapshot is read back after a very large accumulation (memory footprint of `_jobs`/`_snapshot` growing unbounded) — plausible but not benchmarked.

## COMPLETENESS VERDICT

**COMPLETE** for the specific question asked (BatchServer vs. batch MCP tools relationship, and their relationship to TranscodePipeline/profile/sessions/receipts/concurrency/persistence/shipping). Both code paths were read in full; the answer is code-proven, not inferred. Gate/test execution and the exact `L5_BATCH_OK` gate content were **not** run this wave (read-only audit) — flagged under UNCERTAINTIES rather than left implicit.

**Direct answer to the task's question:** BatchServer is a **distinct execution model** (separate process mode, separate non-MCP HTTP REST contract, no MCP protocol framing) that happens to be built by wiring the *same* `TranscodePipeline`/`OccamRouter`/session/playbook stack used by every other Occam tool, through the *same* batch job engine (`BatchJobService`/`BatchJobProcessor`/`JsonFileBatchJobStore`) that the opt-in MCP batch tools also use. It is not "MCP over a different transport" — it is a second, independent front door into the batch engine, mutually exclusive with any MCP transport at the process level, sharing only the on-disk file path (by convention, not by synchronization) with an `OCCAM_BATCH_MCP=1` MCP process — which is exactly the mechanism behind the cross-instance clobber risk in CAP-812. Batch (either surface) inherits full session/playbook/backend-policy behavior from the pipeline but structurally forfeits Receipt v1 and all structured sidecars, and is the one place in the product that persists extracted markdown to disk indefinitely, in tension with the "no file cache by design" product claim.
