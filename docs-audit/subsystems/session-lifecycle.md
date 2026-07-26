# Subsystem audit: Session lifecycle (Wave 3, S3-05)

**Agent:** Wave 3 subagent S3-05
**Scope:** CAP-880..CAP-899 (new mints only — reuse Wave 1 CAP-150..194 and Wave 2 CAP-423/424,
CAP-527, CAP-543, CAP-594, CAP-651..653 aggressively for anything already proven there)
**Method:** Direct inspection of shipped runtime code only (C# `src/FFOccamMcp.Core/Session/**`,
`Workers/**`, `Backends/**`, `Services/**`, `Playbooks/**`, `Batch/**`, `Claims/**`, `Attest/**`,
`Consensus/**`, `Watch/**`; Node `workers/browser-extract/lib/browser-pool.mjs` +
`browser-session.mjs`; operator scripts `scripts/occam-session.mjs`,
`scripts/lib/occam-sessions-lib.mjs`, `scripts/lib/occam-session-export-state.mjs`; gate
`benchmarks/l0-gate/L2SessionRunner.cs` + `corpora/l2-session.jsonl`). Docs were not used as source
of truth.

---

## Relationship to prior waves

Wave 1 S17 (`subsystems/network-fetch-proxy.md`, CAP-150..194) already proved the mechanics of a
**single** `session_profile` fetch (headers resolution, storageState path containment, temp-file
handoff, Playwright cookie injection, secret hygiene, the CLI's `init`/`list`/`import`/
`export-state` verbs). Wave 2 per-tool agents already proved four **hidden narrowing** findings
(CAP-423/424 probe, CAP-527 map, CAP-543 heal, CAP-594 extract_knowledge). **Nothing in this file
re-derives those — it cites them.**

This file's new contribution is the **cross-tool state model** (which of the 12 tools carrying
`session_profile` actually reach the full headers+storageState path vs. a narrower one) and a
**deep-dive on the browser-pool context lifecycle** underneath EF-002/EF-017, which no prior wave
inspected below `BrowserExtractBackend.cs`.

---

## New capabilities (CAP-880..885)

### CAP-880 — Session support is bifurcated into three tiers, not one uniform contract

Grep-confirmed `session_profile` plumbing (Wave 1 CAP-191) reaches 12 surfaces. Tracing each to its
terminal fetch call site shows three distinct tiers:

**Tier 1 — full pipeline delegation (headers + Playwright storageState):** every one of these
constructs an `OccamTranscodeOptions { SessionProfile = ... }` and calls
`TranscodePipeline.TranscodeAsync`, which runs `FetchPreflight.Prepare` → `FetchHeadersScope` →
`BrowserExtractBackend.ExtractAsync` reading `FetchHeadersScope.ActiveStorageStatePath`
(`Backends/BrowserExtractBackend.cs:22-23`):
- `occam_transcode` (`Routing/TranscodePipeline.cs:116,145-149`)
- `occam_digest` (`Services/DigestService.cs:112,226-307` — per-URL, reuses the pipeline)
- `occam_claim_check` (`Claims/ClaimCheckService.cs:38-42`)
- `occam_attest` (`Attest/AttestService.cs:27-69` → delegates into claim_check → pipeline)
- `occam_dataset_export` (`Dataset/DatasetExportService.cs:71-82`)
- `occam_batch_submit` (`Batch/BatchJobProcessor.cs:51-68`)
- `occam_watch` (`Watch/WatchService.cs:74-91`)
- `occam_crosscheck` (`Consensus/ConsensusService.cs:67-81`, also explicitly runs a **second**,
  session-tagged vantage — `+session` observation label at line 45 — i.e. crosscheck is the one
  surface that treats session vs. anonymous as a deliberate comparison axis)

**Tier 2 — HTTP-only by construction (headers apply, storageState is a non-concept):**
- `occam_probe` (`Services/ProbeService.cs:16` — `HttpProbeFetcher` only, no `IExtractBackend`,
  browser is never invoked) — Wave 2 CAP-423/424.
- `occam_map` (`Services/MapService.cs:33` — same `HttpProbeFetcher`-only shape) — Wave 2 CAP-527.

**Tier 3 — reaches a browser fallback but forwards headers only, silently drops storageState:**
- `occam_playbook_heal` (`Workers/DomSkeletonWorker.cs:19-26,35-39,80-83` — passes `headersFile` to
  both the daemon `/skeleton` route and the one-shot `--headers-file=` arg; `preflight.Session`'s
  resolved `StorageStatePath` is never read anywhere in this file) — Wave 2 CAP-543.
- `occam_extract_knowledge` (`Services/KnowledgeExtractService.cs:92-114` → `CssExtractWorker.Extract`,
  whose signature (`Workers/CssExtractWorker.cs:10-15`) has a `headersFile` parameter and **no**
  storageState parameter at all — architecturally impossible to wire, not just unwired) — Wave 2
  CAP-594.

**Status: PROVEN.** This tiering is the answer to "state model" — a caller cannot infer from the
tool surface alone which tier applies; `session_profile`'s param description is textually near-
identical across all 12 tools (e.g. `Tools/OccamPlaybookHealTool.cs:17`: *"Optional session profile
id (same as occam_transcode)"* — this specific wording actively implies Tier-1 parity while the
implementation is Tier 3).

### CAP-881 — HIDDEN: per-call GUID header temp-file defeats browser-pool warm reuse for any headered/session call

`Session/FetchHeadersScope.cs:36` creates a **new** `Path.GetTempPath()/occam-headers-{Guid.NewGuid():N}.json`
on every `FetchPreflight.Prepare` invocation, even when the header **content** is byte-identical to
the previous call (same `session_profile`, or just an ambient `OCCAM_REQUEST_HEADERS_FILE` with no
session at all). `workers/browser-extract/lib/browser-pool.mjs:31-38` (`ensureSession`) detects a
"session changed" condition purely by **path string inequality**
(`options.headersFile !== this.#headersFile`), not content hash. Consequence: the daemon/pool
(`recycle()` at line 60-66 does a full `session.close()` = `context.close()+browser.close()`,
`browser-session.mjs:193-195`) is forced to fully relaunch Chromium **before every single
browser-backend extract that carries any headers at all** — including every per-URL sub-call inside
one `occam_digest` request against the same site with the same `session_profile`. The pool's own
"amortizes chromium.launch() across requests" design comment (`browser-pool.mjs:12`) does not hold
for the headered/session path; only fully anonymous traffic (no session, no ambient headers file)
gets the intended warm-reuse. **Status: PROVEN, not previously recorded** — propose as **EF-019**.

### CAP-882 — Refined EF-002: the real bleed vector is anonymous-to-anonymous context sharing, not `session_profile`-to-`session_profile`

CAP-881's forced-recycle is an accidental mitigation: any transition **into or out of** a headered
call (any two different `session_profile`s, or session→anonymous, or anonymous→session) always
recycles the context first, so Playwright-injected session cookies (`context.addCookies`, Wave 1
CAP-171, invoked from inside `renderAndExtract` per `browser-pool.mjs:167-173`, `session.sessionHeaders`)
never actually survive to a second call. The genuine bleed surface documented in Wave 1's CAP-249
stub ("Session/state isolation caveat", empty implementation/tests/entrypoints) is narrower and
different: **fully anonymous** consecutive calls (`headersFile` and `storageStateFile` both `null`
on every call, so the mismatch check at `browser-pool.mjs:32-38` never fires) share **one**
`BrowserContext` for up to `RECYCLE_AFTER_RUNS = 10` runs or until `process.memoryUsage().heapUsed`
exceeds `RECYCLE_MEMORY_THRESHOLD_BYTES = 400 MB` (`browser-pool.mjs:9-10,194-198`). Any cookie a
**visited site itself** sets (`Set-Cookie` / `document.cookie`) — or a recipe-injected cookie under
`WT_COOKIE_INJECT=1` (Wave 1 CAP-177, same `context.addCookies` call path, off by default) — persists
in that shared jar and is replayed on whichever unrelated URL the pool happens to serve next within
that 10-run/400MB window, regardless of which logical MCP call or caller triggered the original
fetch. A failed run (`!result.ok`) does force an immediate recycle (`browser-pool.mjs:187-190`), so
the exposure window is bounded by *successful anonymous runs only*. **Status: PROVEN** — this
supersedes the vague framing of Wave 1 CAP-249 with a mechanism; CAP-249 should be considered
resolved by this entry (kept as a distinct id since it originated in a different wave/file).

### CAP-883 — `L2_SESSION_OK` gate covers 4 of 12 session-carrying surfaces; zero coverage of pool-recycle/cookie-persistence behavior

`benchmarks/l0-gate/L2SessionRunner.cs:37-54` dispatches only on `tool ∈ {transcode, probe, digest,
map}`; `corpora/l2-session.jsonl` cases are limited to invalid-id / not-found / valid-header
assertions (`Ok`, `FailureCode`, `MinMarkdownChars`/`MinLinks`) for those four tools. No gate case
exercises `occam_playbook_heal`, `occam_extract_knowledge`, `occam_claim_check`, `occam_attest`,
`occam_dataset_export`, `occam_batch_submit`, `occam_watch`, or `occam_crosscheck` with a
`session_profile`, and no case in this or any other gate file drives two sequential browser-backend
calls to assert context-recycle or cookie-isolation behavior (CAP-881/882) — that behavior is only
established by static code reading in this report. **Status: PROVEN ABSENT** — direct answer to the
"verify session drop" instruction: the drop is real in code, has no regression coverage, and is
recorded here rather than fixed or gated.

### CAP-884 — Operator CLI and MCP host resolve the identical sessions-root default (no split-brain)

`scripts/lib/occam-sessions-lib.mjs:9-15` (`resolveSessionsRoot`) and
`Session/SessionProfileHeaders.cs:115-129` (`GetSessionsRoot`) both default to
`homedir()/.occam/sessions` (`OccamUserPaths.ResolveUserDataRoot()` + `"sessions"` on the C# side)
and both honor `OCCAM_SESSIONS_ROOT` as an override with the same semantics (trim + resolve).
`writeSessionProfile` (`occam-sessions-lib.mjs:134-150`) and `SessionProfileHeaders.Resolve`
(`SessionProfileHeaders.cs:54-113`) agree on the on-disk shape: a flat `<id>.json` with header keys
as top-level string properties, `_occam` metadata, and an optional `storageState` string. **Status:
PROVEN** — a positive state-model finding: the CLI writes exactly what the host later reads, with no
observed divergence in path resolution or file shape.

### CAP-885 — HIDDEN: `export-state`'s browser-only hint is the sole place the storageState/backend_policy dependency is surfaced

`scripts/lib/occam-session-export-state.mjs:99,114` is the only shipped text (a profile-JSON `notes`
field and the CLI's own JSON `hint` field) stating storageState-based profiles need
`backend_policy=browser` (or `http_then_browser`) to take effect. No MCP tool description
cross-references this: `occam_playbook_heal`'s param doc claims parity with `occam_transcode`
(CAP-880) and `occam_extract_knowledge`'s param doc says "loads headers" (accurate but silent on the
storageState gap it implies by omission — `Tools/OccamExtractKnowledgeTool.cs:19`). An MCP-only
agent that exports a Cloudflare-passed storageState profile via the CLI and later reuses that same
`session_profile` id against `occam_playbook_heal` or `occam_extract_knowledge` gets silent
Cookie-header-only behavior (CAP-543/594) with **no runtime warning or failure code** — the call
still returns `ok:true` on an anonymous fetch if the site tolerates it, or a generic
`captcha_or_challenge`/`requires_login` if it doesn't, never a session-specific diagnostic.
**Status: PROVEN.**

---

## Capability graph edges

```
TOOL(occam_transcode)          |USES| CAP-880 (tier1) |USES| CAP-170 |USES| CAP-171
TOOL(occam_digest)              |USES| CAP-880 (tier1)
TOOL(occam_claim_check)         |USES| CAP-880 (tier1)
TOOL(occam_attest)              |USES| CAP-880 (tier1) |USES| TOOL(occam_claim_check)
TOOL(occam_dataset_export)      |USES| CAP-880 (tier1)
TOOL(occam_batch_submit)        |USES| CAP-880 (tier1)
TOOL(occam_watch)               |USES| CAP-880 (tier1)
TOOL(occam_crosscheck)          |USES| CAP-880 (tier1) |USES| CAP-880-vantage-comparison
TOOL(occam_probe)               |USES| CAP-880 (tier2) |USES| CAP-423 |USES| CAP-424
TOOL(occam_map)                 |USES| CAP-880 (tier2) |USES| CAP-527
TOOL(occam_playbook_heal)       |USES| CAP-880 (tier3) |USES| CAP-543
TOOL(occam_extract_knowledge)   |USES| CAP-880 (tier3) |USES| CAP-594
CLI(occam session)              |USES| CAP-174 |USES| CAP-175 |USES| CAP-176 |USES| CAP-884
CLI(occam session export-state) |USES| CAP-885 |PRODUCES| ARTIFACT(states/<id>.json)
BrowserPool.ensureSession       |USES| CAP-881 |CAUSES| CAP-249/CAP-882 (context reuse window)
GATE(L2_SESSION_OK)             |PARTIALLY_COVERS| CAP-880 |DOES_NOT_COVER| CAP-881/882/883
```

---

## Artifacts created/consumed

| Artifact | Producer | Consumer | Lifetime |
|---|---|---|---|
| `~/.occam/sessions/<id>.json` (or `OCCAM_SESSIONS_ROOT`) | `occam session init/import/export-state` | `SessionProfileHeaders.Resolve` (all 12 tools) | Persistent, operator-owned, gitignored by default (CAP-192) |
| `~/.occam/sessions/states/<name>.json` (Playwright storageState) | `occam session export-state` | `SessionProfileHeaders.ResolveStorageStatePath` → `BrowserExtractBackend` (Tier 1 only) | Persistent |
| `~/.occam/sessions/_imports/<file>` | `occam session import` (unless `--no-keep-import`) | none (audit trail only) | Persistent |
| `%TEMP%/occam-headers-{guid}.json` | `FetchHeadersScope.Create` | `HttpExtractBackend`/`BrowserExtractBackend`/`DomSkeletonWorker`/`CssExtractWorker` (via `--headers-file`) | Per-call, deleted on `Dispose()` (best-effort, background retry on lock) |
| In-memory Playwright `BrowserContext` (daemon/pool only) | `BrowserPool.ensureSession` | `renderAndExtract` | Up to 10 successful runs or 400 MB heap, or first mismatch/failure |

---

## Completeness verdict

**Complete for the assigned scope.** All executable entrypoints in `scripts/occam-session.mjs`,
`Session/*.cs`, and every `session_profile`-consuming service were traced to their terminal fetch
call site. Not independently re-verified: the exact byte-for-byte parity of `browser-session.mjs`'s
`renderAndExtract` cookie-add call beyond what Wave 1 CAP-171 already cited (re-read only the
call-site line range, not the full cookie-parsing logic — already proven elsewhere). No product code
was changed; no gate was run (static-audit task only, per instructions).
