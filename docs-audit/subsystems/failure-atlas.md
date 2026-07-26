# S3-04 — Failure Atlas (`occam_failure_atlas`, opt-in `OCCAM_ATLAS_MCP=1`)

**Wave:** 3 (deep dive on Wave-1 CAP-015 stub)
**CAP range (new):** CAP-870 … CAP-879 (10 minted; parent gate reuses **CAP-015**)
**SoT:** current executable code only. Docs untrusted (checked only for the persistence-claim audit).

---

## AUDIT TARGET

`occam_failure_atlas` MCP tool + its backing runtime: `Telemetry/FailureAtlasStore.cs`,
`Telemetry/FailureAtlasSink.cs`, `Telemetry/FailureAtlasClassifier` (same file as Store),
`Tools/OccamFailureAtlasTool.cs`, DI wiring in `Transport/OccamMcpServerRegistration.cs`,
gate coverage in `benchmarks/l0-gate/FailureAtlasUnitTests.cs`.

## FILES INSPECTED

- `src/FFOccamMcp.Core/Tools/OccamFailureAtlasTool.cs`
- `src/FFOccamMcp.Core/Telemetry/FailureAtlasStore.cs` (Store + `FailureAtlasClassifier` + records)
- `src/FFOccamMcp.Core/Telemetry/FailureAtlasSink.cs`
- `src/FFOccamMcp.Core/Telemetry/OccamLoggerTelemetrySink.cs`
- `src/FFOccamMcp.Core/Abstractions/IOccamTelemetrySink.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs` (lines 120-159, DI gate)
- `src/FFOccamMcp.Core/Transport/RemoteMcpTransport.cs`, `WebSocketMcpTransport.cs` (DI lifetime/session shape)
- `src/FFOccamMcp.Core/Routing/TranscodePipeline.cs` (telemetry call sites, `ctx.Url` = `fetchUrl`)
- `src/FFOccamMcp.Core/Composition/OccamServiceCollectionExtensions.cs` (base sink registration)
- Every `TranscodePipeline` consumer: `Tools/OccamTranscodeTool.cs`, `Services/DigestService.cs`,
  `Dataset/DatasetExportService.cs`, `Tools/OccamVerifyTool.cs`, `Consensus/ConsensusService.cs`,
  `Claims/ClaimCheckService.cs`, `Batch/BatchJobProcessor.cs`, `Watch/WatchService.cs`,
  `Playbooks/PlaybookSaveVerifier.cs`
- Non-consumers confirmed by absence: `Services/ProbeService.cs`, `Services/MapService.cs`,
  `Tools/OccamExtractKnowledgeTool.cs` (no `TranscodePipeline`/`IOccamTelemetrySink` reference)
- `benchmarks/l0-gate/FailureAtlasUnitTests.cs`, `benchmarks/l0-gate/Program.cs` (call sites)
- Repo-wide grep for `FailureAtlasStore` (6 hits total, all listed above — no persistence layer,
  no CLI verb, no BatchServer HTTP handler)
- `docs/tools/occam_failure_atlas.md`, `MCP_API_SPEC.md`, `docs/tools-reference.md`,
  `docs-audit/CAPABILITY-INVENTORY.md`, `docs-audit/subsystems/runtime-mcp.md` (CAP-015 stub),
  `docs-audit/PROFILE-TOOL-MATRIX.md`, `docs-audit/ENVIRONMENT-VARIABLES.md` (doc cross-check only)

## ENTRYPOINTS

Single entrypoint: `OccamFailureAtlasTool.Read(only_walled)`, registered **only** when
`OCCAM_ATLAS_MCP=1` (`OccamMcpServerRegistration.cs:149-157`). No CLI verb, no BatchServer HTTP
route, no other tool reads `FailureAtlasStore`.

## RUNTIME PATH

```
OCCAM_ATLAS_MCP=1 at host startup
  → services.AddSingleton<FailureAtlasStore>()
  → services.AddSingleton<IOccamTelemetrySink>(FailureAtlasSink wrapping a NEW OccamLoggerTelemetrySink())
  → builder.WithTools<OccamFailureAtlasTool>()

Every TranscodePipeline.TranscodeAsync call (any tool that uses the pipeline):
  compiled.Ok → telemetry.OnTranscodeCompleted(ctx, outcome) → store.RecordSuccess(ctx.Url)
  !compiled.Ok → telemetry.OnTranscodeFailed(ctx, outcome)  → store.RecordFailure(ctx.Url, outcome.FailureCode)

occam_failure_atlas(only_walled?)
  → store.Snapshot() → FailureAtlasClassifier.Summarize per host → sort by closureRate desc, failures desc
  → filter only_walled if requested → JSON response
```

---

## CAPABILITIES

### CAP-015 (reused, Wave 1) — Opt-in gate: `OCCAM_ATLAS_MCP=1` → tool + sink swap
Confirmed exactly as Wave-1 recorded it: `OccamMcpServerRegistration.cs:148-157`. Default off.

### CAP-870 — `occam_failure_atlas` tool contract
- **Impl:** `Tools/OccamFailureAtlasTool.cs:16-36`
- **Param:** `only_walled` (bool, default `false`) — client-side filter applied after full snapshot
- **Response:** `{ ok:true, hostCount, walledCount, hosts:[...], timestamp }`; per-host
  `{ host, attempts, successes, failures, closureRate, walled, dominantFailure, byCode:[{code,count}], lastFailureAt }`
- **Ordering:** worst-first (`closureRate` desc, then `failures` desc) — set in `Store.Snapshot()`, not in the tool
- **Status:** PRODUCT (opt-in MCP surface)
- **Confidence:** PROVEN

### CAP-871 — `FailureAtlasStore` bounded in-memory aggregation
- **Impl:** `Telemetry/FailureAtlasStore.cs:13-108`
- **Bound:** `MaxHosts = 500`; once full, **new** hosts are silently dropped (`TryGetOrAdd` returns false) — existing tracked hosts keep updating
- **Host key:** `Uri.Host` lowercased, `www.` prefix stripped, case-insensitive dictionary — `http://WWW.Foo.com/a` and `https://foo.com/b` are the same atlas entry
- **Concurrency:** single `lock (_lock)` around all mutation + snapshot construction — thread-safe under concurrent transcodes
- **Malformed URL handling:** `TryHost` returns false for unparseable/relative URLs → silently not recorded (no exception, no metric)
- **Status:** PRODUCT (internal engine, not directly reachable except via CAP-870)
- **Confidence:** PROVEN

### CAP-872 — `FailureAtlasClassifier`: closure taxonomy + walled/closureRate math
- **Impl:** `Telemetry/FailureAtlasStore.cs:134-169`
- **Closure codes (honest wall, not worth retrying):** `captcha_or_challenge`, `requires_login`,
  `http_401`, `http_403`, `http_404`, `http_410`
- **Transient codes excluded from closure rate:** everything else — explicitly `timeout`, `http_429`,
  `http_503`/5xx, `network_error`, `dns_error` (per gate + code comments)
- **`walled = true` iff:** `successes == 0 AND failures > 0 AND dominantFailure is a closure code`
  — a host with **any** success is never walled even if most attempts hit a 403
- **`closureRate`:** `closures / attempts`, rounded to 4 decimals; **0.0 when attempts == 0**
- **`dominantFailure`:** highest-count code, ties broken alphabetically (`Ordinal`) — not by recency
- **Status:** PRODUCT (pure function, gate-testable)
- **Confidence:** PROVEN

### CAP-873 — Atlas silently aggregates across every `TranscodePipeline`-routed tool, not just `occam_transcode`
- **Impl:** the sink is wired once at `IOccamTelemetrySink` (DI, process-wide) and `TranscodePipeline`
  is the single call site for `OnTranscodeCompleted`/`OnTranscodeFailed` (`Routing/TranscodePipeline.cs:167,196,200`)
- **Tools that feed the atlas (all route through `TranscodePipeline`):** `occam_transcode`,
  `occam_digest`, `occam_dataset_export`, `occam_verify` (live re-fetch mode), `occam_crosscheck`
  (opt-in), `occam_claim_check`, `occam_batch_*` (opt-in, via `BatchJobProcessor`), `occam_watch`
  (opt-in), and the internal `PlaybookSaveVerifier` pre-save check
- **Tools that do NOT feed the atlas (proven by absence of `TranscodePipeline`/`IOccamTelemetrySink`
  references):** `occam_probe`, `occam_map`, `occam_extract_knowledge`, `occam_playbook_resolve`,
  `occam_playbook_heal` — these use separate backend paths, not `TranscodePipeline`
- **Implication:** an agent calling only `occam_probe`/`occam_map` on a host will never populate the
  atlas for that host, even if the probe correctly detects a wall; the atlas is a **transcode-family
  outcome ledger**, not a general "any tool touched this host" ledger — tool description does not
  state this scope boundary
- **Status:** advanced / non-obvious
- **Confidence:** PROVEN

### CAP-874 — Non-persistence is real and total (persistence claim verified TRUE)
- **Evidence:** repo-wide grep for `FailureAtlasStore` returns exactly 6 files (Store, Sink, Tool,
  registration, unit tests, CHANGELOG prose) — no file/DB/JSON writer, no `System.IO` in the Store,
  no reference from `Watch/WatchStore.cs`-style persistence patterns used elsewhere in the codebase
  (contrast: `occam_watch` explicitly persists to `OCCAM_WATCH_DB_PATH`; the atlas has no equivalent env var)
- **Effect:** the atlas is empty at every process start and is lost on restart, redeploy, or crash;
  it is also **not** shared across separate host processes (e.g. two Cursor windows each spawning
  their own MCP host get independent atlases)
- **Status:** confirms tool-description + `docs/tools/occam_failure_atlas.md` claim ("In-memory over
  the current run; not persisted") — **doc/code match, no drift found**
- **Confidence:** PROVEN

### CAP-875 — Enabling the atlas replaces the host's entire telemetry sink (DI last-registration-wins), not an addition
- **Impl:** `AddOccamCore()` registers `IOccamTelemetrySink → OccamLoggerTelemetrySink` first
  (`Composition/OccamServiceCollectionExtensions.cs:37`); when `OCCAM_ATLAS_MCP=1`,
  `OccamMcpServerRegistration.cs:152-155` adds a **second** `AddSingleton<IOccamTelemetrySink>`
  registration wrapping a **brand-new** `OccamLoggerTelemetrySink()` instance (not the one already
  registered). `TranscodePipeline` and `IBrowserPoolManager` both take a single (non-`IEnumerable`)
  `IOccamTelemetrySink` constructor parameter, so Microsoft.Extensions.DependencyInjection resolves
  the **last** registration — the atlas-wrapped one — everywhere in the process
- **Effect:** turning on the atlas is not additive instrumentation; it swaps the sink used for
  browser-pool acquire/release logging too (`OnBrowserPoolAcquired`/`Released` still delegate to the
  fresh logger, so behavior is unchanged in practice, but the indirection is a latent trap if a
  second telemetry consumer is ever added)
- **Status:** internal / correctness-by-coincidence (harmless today because `OccamLoggerTelemetrySink`
  is stateless; would silently drop any *stateful* base sink registered by `AddOccamCore` in the future)
- **Confidence:** PROVEN

### CAP-876 — No alternate read surface for atlas state
- **Impl:** confirmed by grep — no `OccamCliVerbs` verb, no `BatchServerHost` HTTP route
  (`/v1/health`, `/v1/batch/*`), and no other MCP tool references `FailureAtlasStore`
- **Effect:** the only way to observe the atlas is `occam_failure_atlas` itself, and only in a host
  process where `OCCAM_ATLAS_MCP=1` was set at startup (cannot be toggled at runtime)
- **Status:** PRODUCT (single, deliberate surface)
- **Confidence:** PROVEN

### CAP-877 — Cross-feature interaction: Batch mode feeds the same singleton atlas
- **Impl:** `Batch/BatchJobProcessor.cs` is a `BackgroundService` holding the same `TranscodePipeline`
  singleton; when both `OCCAM_BATCH_MCP=1` and `OCCAM_ATLAS_MCP=1` are set, fire-and-forget batch jobs
  submitted via `occam_batch_submit` record successes/failures into the identical `FailureAtlasStore`
  instance as interactive `occam_transcode` calls in the same process
- **Implication:** an agent can submit a large batch crawl, then poll `occam_failure_atlas` mid-run to
  see which hosts in the batch are already provably walled and stop waiting on them — an emergent,
  undocumented combo of two opt-in features
- **Status:** advanced / undocumented interaction
- **Confidence:** PROVEN (by shared singleton wiring; not runtime-observed)

### CAP-878 — Gate coverage is unit/pure-function only; no live proof of the registration path or wire shape
- **Impl:** `FailureAtlasUnitTests.Run` (`benchmarks/l0-gate/FailureAtlasUnitTests.cs`) is invoked
  unconditionally in both fast and full gate runs (`Program.cs:132,252`) and exercises
  `FailureAtlasClassifier.IsClosure`/`Summarize` and a raw `new FailureAtlasStore()` round-trip directly
  — **no DI container, no `OCCAM_ATLAS_MCP=1` env, no MCP tool invocation, no JSON serialization check**
- **Gap:** nothing in the gate proves that `OccamMcpServerRegistration` actually exposes
  `occam_failure_atlas` on `tools/list` when the flag is set, that `OccamFailureAtlasJsonContext`
  serializes the camelCase shape the docs promise, or that the DI swap in CAP-875 resolves as analyzed
  (static trace only)
- **Status:** test-coverage gap (unit-proven, integration-unproven)
- **Confidence:** PROVEN (absence confirmed by reading the full test file + both gate call graphs)

### CAP-879 — Tool description accuracy audit (persistence claim + full contract)
Cross-checked `[Description]` string in `OccamFailureAtlasTool.cs:18` and
`docs/tools/occam_failure_atlas.md` against code:

| Claim in tool description / docs | Code reality | Verdict |
|---|---|---|
| "opt-in, OCCAM_ATLAS_MCP=1" | `OccamMcpServerRegistration.cs:149` | MATCH |
| Response shape `{ok, hostCount, walledCount, hosts:[...]}` worst-first | `OccamFailureAtlasResponse` record + `Store.Snapshot()` ordering | MATCH |
| Per-host fields `{host, attempts, successes, failures, closureRate, walled, dominantFailure, byCode, lastFailureAt}` | `FailureAtlasHostSummary` record | MATCH (exact field set, camelCase via `JsonSourceGenerationOptions`) |
| "'walled' = never succeeded and the dominant failure is an honest closure (captcha/login/4xx)" | `FailureAtlasClassifier.Summarize` walled formula | MATCH |
| "In-memory over the current run; not persisted" | CAP-874 | MATCH |
| `only_walled` default `false` | method signature default | MATCH |
| *Not stated:* atlas scope is transcode-pipeline-only (CAP-873) | — | GAP (silent scope limit, not a false claim) |
| *Not stated:* process-wide singleton shared across ALL sessions in `--remote` multi-session mode | — | GAP → see EF-019 below |

- **Status:** description is **factually accurate** for everything it claims; the two gaps are
  omissions (undocumented scope/sharing), not false claims
- **Confidence:** PROVEN

---

## ADVANCED / HIDDEN

- Atlas only sees `TranscodePipeline` traffic — probe/map/extract_knowledge/resolve/heal calls are
  invisible to it even though they hit the same hosts (CAP-873)
- Enabling `OCCAM_ATLAS_MCP=1` silently reroutes browser-pool telemetry through a new sink instance
  too, not just transcode outcomes (CAP-875)
- Batch + Atlas combo lets an agent watch a background crawl's closure map mid-flight (CAP-877)
- Malformed/relative URLs and the 501st distinct host are silently dropped, with no signal to the
  caller that tracking stopped (CAP-871)

## INVISIBLE PRODUCT — what an MCP-only user never sees

An MCP client that never sets `OCCAM_ATLAS_MCP=1` (i.e. essentially everyone, since it is off by
default and set only by whoever launches the host process — the agent itself cannot turn it on)
never sees `occam_failure_atlas` in `tools/list` at all, and every `occam_transcode`/`occam_digest`/etc.
call it makes is still silently discarded by the always-registered `OccamLoggerTelemetrySink` (stderr
log lines only). The entire closure-map concept — "this host is a proven dead end, stop retrying it"
— is invisible unless an operator opts in at launch. Even when opted in, the atlas resets on every
host restart and is invisible across parallel host processes (e.g. two IDE windows), so a user
working in two sessions against the same target site gets two independent, incomplete pictures with
no way to merge them.

## ENGINEERING FINDINGS (candidates)

### EF-019 — SECURITY/PRIVACY-CANDIDATE — `--remote` multi-session mode shares one process-wide failure atlas across all authenticated clients
**Related CAPs:** CAP-871, CAP-875, plus CAP-005/CAP-023 (S0, remote transport multi-session).
`RemoteMcpTransport.cs:73` calls `builder.Services.AddOccamMcpServer()` exactly once when the remote
host process starts, registering `FailureAtlasStore` as a single process-wide singleton — but Remote
mode is explicitly designed for **multiple concurrent JWT-authenticated sessions**
(`OCCAM_REMOTE_MAX_SESSIONS`, CAP-023). Because there is one DI container for the whole process (not
one per session/connection), every connected client's transcode outcomes land in the same
`FailureAtlasStore`, and `occam_failure_atlas` (if also enabled) returns the **union of every
session's crawl history** to whichever client calls it — one tenant can observe which hosts another
tenant has been probing and whether those attempts failed with `requires_login`/`captcha`/4xx. Local
stdio and loopback-WS modes are single-client by design (`WebSocketMcpTransport` doc comment: "single
client") so this is specific to `--remote`.
**Confidence:** PROVEN by DI wiring trace (single `AddOccamMcpServer()` call site, singleton
lifetime, no per-session scoping anywhere in `RemoteMcpTransport.cs`). Not runtime-reproduced in this
pass (would need two authenticated remote sessions).
**Needs repro?** Yes — two JWT sessions against one `--remote` host, session A fails a host, session B
calls `occam_failure_atlas`, confirm session A's host appears.
**Security review?** Yes — flag for security-review subagent; this is a real cross-tenant information
leak specifically when an operator combines `--remote` (multi-session) with `OCCAM_ATLAS_MCP=1`.
**Status:** OPEN

*(Not appended to `docs-audit/ENGINEERING-FINDINGS.md` per instructions — "append only if proven" for
the ledger; this is proven by static trace but unconfirmed by live repro. Orchestrator should decide
whether static-DI-trace confidence is sufficient to append as EF-019, matching the bar set by
EF-002/EF-003 in the existing ledger which are also PROVEN-in-code without live repro.)*

## CONFIG / ENV (this subsystem)

`OCCAM_ATLAS_MCP` (flag, default off) — the only env var. No `OCCAM_ATLAS_*` sub-config exists (no
max-hosts override, no TTL, no persistence path) — `MaxHosts = 500` (CAP-871) is a hardcoded constant.

## FALLBACKS

None. The tool has no backend policy, no retry, no partial-failure mode — it is a pure in-memory read.

## FAILURES

The tool itself cannot fail in a typed sense (always returns `ok:true`, even with zero hosts tracked).
Cancellation is honored via `cancellationToken.ThrowIfCancellationRequested()`.

## SECURITY / TRUST

- No signed receipt, no auth check beyond whatever transport-level auth already gates the MCP session
- See EF-019 for the one real security-relevant finding (remote multi-session sharing)
- Bounded (`MaxHosts=500`) — cannot be used to exhaust host memory via a URL-flooding attack

## TEST EVIDENCE

`benchmarks/l0-gate/FailureAtlasUnitTests.cs` — unit-only, see CAP-878 for the integration gap.

## DOC GAPS (deferred)

- `docs/tools/occam_failure_atlas.md` and the tool description do not state the transcode-pipeline-only
  scope (CAP-873) or the remote multi-session sharing behavior (EF-019) — both are omissions, not
  inaccuracies, so no "claims discipline" violation, but worth a follow-up doc note once EF-019 is
  triaged.

## UNCERTAINTIES

- Whether `--remote` mode is used in practice with `OCCAM_ATLAS_MCP=1` simultaneously (both opt-in;
  no evidence either way in code) — EF-019 is a latent risk, not a confirmed incident
- Whether a future stateful base `IOccamTelemetrySink` would actually be silently dropped by CAP-875
  (today's only implementation is stateless, so no observable bug yet)

## COMPLETENESS VERDICT

**COMPLETE** for the failure-atlas subsystem as scoped (tool, store, classifier, sink, DI gate, gate
coverage, doc-claim audit, cross-tool reachability). One security-relevant cross-session finding
(EF-019) flagged for orchestrator triage; no other product code touches this subsystem.
