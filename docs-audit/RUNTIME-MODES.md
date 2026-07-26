# S3-11 — Runtime modes / transports (Wave 3)

**Agent:** S3-11
**CAP range:** CAP-1000 … CAP-1019 (used: CAP-1000 … CAP-1003; 1004–1019 reserved)
**SoT:** current executable code only. Docs untrusted.
**Reuses:** Wave 1 `subsystems/runtime-mcp.md` CAP-001…CAP-028 (still PROVEN, re-verified against current code — no drift found).

---

## AUDIT TARGET

`Program.cs` entry dispatch + all four `IMcpTransport`/host modes (stdio, WebSocket, Remote WSS+JWT,
BatchServer HTTP) — auth, bind policy, security, tool surface — plus the operator launch path
(`scripts/launch-mcp-host.mjs`) and the npm entrypoint (`packages/occam-mcp/bin/occam-mcp.js`) that
are supposed to reach those modes.

## FILES INSPECTED

- `src/FFOccamMcp.Core/Program.cs`
- `Transport/OccamMcpCli.cs`, `StdioMcpTransport.cs`, `WebSocketMcpTransport.cs`,
  `RemoteMcpTransport.cs`, `RemoteMcpAuthOptions.cs`, `OccamMcpServerRegistration.cs`,
  `OccamToolProfile.cs`, `OccamServerInstructions.cs`
- `src/FFOccamMcp.Core/Batch/BatchServerHost.cs`, `BatchSettings.cs`
- `scripts/launch-mcp-host.mjs`, `scripts/lib/resolve-host-binary.mjs`, `scripts/occam-wrapper.sh`
- `packages/occam-mcp/bin/occam-mcp.js`
- `scripts/lib/operator/occam-command-registry.mjs` (usage-string cross-check)
- Repo-wide grep of every `launch-mcp-host.mjs` invocation (33 call sites across scripts/benches/tests)
- Re-verified against `docs-audit/subsystems/runtime-mcp.md` (Wave 1 S0) — no code drift since that
  report; CAP-001…028 stand as written.

## EXECUTABLE ENTRYPOINTS

1. `Program.Main` → `OccamCliVerbs.TryRun` (offline verbs, no transport) **or**
2. `OccamMcpCli.Parse(args)` → `Mode=BatchServer` → `BatchServerHost.RunAsync` **or**
   `Mode=WebSocket|Remote|Stdio` → `IMcpTransport.StartAsync`
3. Operator path: `scripts/launch-mcp-host.mjs` (stdio-only, see CAP-1001) → resolved AOT binary,
   `[]` args
4. npm path: `packages/occam-mcp/bin/occam-mcp.js` → downloads/locates binary → forwards a
   hand-rolled arg subset (`--mcp-server`, `--port`, `--host`) — see CAP-1002 / EF-019

---

## CAPABILITIES

### Reused from Wave 1 (verified current, no changes)

| CAP | Summary |
|-----|---------|
| CAP-001 | Process entry dual-path (CLI verbs vs MCP host) |
| CAP-002 | Offline CLI verb dispatch pre-transport |
| CAP-003 | Transport mode: stdio (default) |
| CAP-004 | Transport mode: local WebSocket, port 5050, loopback-only bind enforced |
| CAP-005 | Transport mode: Remote WSS+JWT, port 8443, requires TLS cert + HTTPS JWT metadata |
| CAP-006 | Transport mode: Batch HTTP server, port 5051, loopback-only, bypasses MCP registration |
| CAP-007…010 | Core tool catalog (15), `OCCAM_PROFILE` role scoping, instructions text |
| CAP-011…015 | Opt-in MCP tools not profile-filtered (batch/watch/crosscheck/atlas) |
| CAP-016…022 | Digest URL union schema, argument-binding guard, HTTP daemon prewarm, startup banner, WS message-size cap, content-length framing, profile-aware help text |
| CAP-023 | Remote session concurrency limit (`OCCAM_REMOTE_MAX_SESSIONS`, default 4, 1–32) |
| CAP-024 | CLI bind policy asymmetry: WS/BatchServer reject any non-`127.0.0.1` `--bind`; Remote accepts any parseable IP (incl. `0.0.0.0`) |
| CAP-025…028 | Surface taxonomy, `AddOccamCore` DI-first ordering, transport unit-test coverage, stderr/stdout purity contract |

Confirmed unchanged: `RemoteMcpTransport` still rejects `?token=`/`?access_token=` query params
(400 `query_token_forbidden`), requires `Authorization: Bearer` JWT (401 `unauthorized` otherwise),
enforces `RequireHttpsMetadata=true` + full `TokenValidationParameters` (issuer/audience/lifetime/
signature all validated, 30s clock skew), and exposes an **unauthenticated** `/health` endpoint by
design. `BatchServerHost` confirmed to ship **zero** authentication of any kind (no token, no
origin check) — its only trust boundary is the hardcoded `127.0.0.1` bind in `BatchSettings.cs:8`.

### New this wave

#### CAP-1000 — Per-connection DI container isolation on WS/Remote transports
- **Impl:** `WebSocketMcpTransport.RunSingleSessionAsync` / `RemoteMcpTransport.RunSingleSessionAsync`
  each call `Host.CreateApplicationBuilder().Services.AddOccamMcpServer()` — a **brand-new**
  `IServiceCollection`, independent of the outer `WebApplication`'s own `AddOccamMcpServer()` call
  made once at `StartAsync`.
- **Effect:** every accepted WebSocket connection gets fresh singleton instances of whatever opt-in
  services are DI-registered inside `AddOccamMcpServer` (`FailureAtlasStore`, `ConsensusService`,
  in addition to the always-fresh `WatchService`/`BatchJobService` wrappers). **In-memory-only**
  state (`FailureAtlasStore` — Wave 1/2 already noted it as "not persisted", CAP-015) is reset to
  empty on every reconnect over WS/Remote; it only accumulates across calls within one continuous
  session. File-backed stores (`JsonFileBatchJobStore`, `WatchStore` — both read/write a path under
  `OCCAM_SESSIONS_ROOT`/similar) are unaffected because they reload from disk each time.
- **Practically:** `occam_failure_atlas` telemetry is far less useful behind WS/Remote (client
  reconnect = telemetry wipe) than behind stdio (one process = one long session = one store).
- **Confidence:** PROVEN (direct code read, both transports).

#### CAP-1001 — Canonical launcher is stdio-only; never forwards CLI args
- **Impl:** `scripts/launch-mcp-host.mjs` calls `runChild(hostBinary, [])` — a **hardcoded empty
  array**, never `process.argv.slice(2)`. No flag (`--mcp-server`, `--remote`, `--batch-server`,
  `--port`, `--bind`, `--tls-cert`, `--jwt-*`) can ever reach the child process through this script.
- **Reach:** this is the launcher wired by **every** operator entry point that spawns the host in
  this repo — `occam-wrapper.sh` (Hermes), all 15 host-connect adapters
  (`scripts/lib/operator/connect/*`), `occam-doctor.ps1`/`.sh` ("Canonical launcher:" banner text),
  every bench/gate script (`bench/sweep.mjs`, `hermes-smoke.mjs`, `run-agent-*.mjs`,
  `check-public-mcp-contract.mjs`, etc. — 33 call sites total, none pass args).
- **Implication:** a user who wires Cursor/Claude Desktop/Hermes/etc. via the documented
  "auto-connect" or "canonical launcher" path can **only ever get stdio mode**. WebSocket/Remote/
  BatchServer are reachable **only** by invoking the published AOT binary directly with flags (the
  path documented in `OccamMcpCli.WriteUsage`) — there is no operator subcommand or connect-adapter
  flow that starts those modes today except the npm package's own WS-only re-implementation
  (CAP-1002).
- **Confidence:** PROVEN (code read + exhaustive repo grep of `launch-mcp-host.mjs` call sites).

#### CAP-1002 — npm entrypoint independently re-implements WS-mode arg forwarding
- **Impl:** `packages/occam-mcp/bin/occam-mcp.js` (`main()`) parses `--mcp-server`/`--port`/`--host`
  itself and constructs `dotnetArgs` to forward to the resolved binary — a second, independent CLI
  parser that duplicates (a subset of) `OccamMcpCli.Parse`'s job.
- **Scope:** this is currently the **only** code path in the repo that can start WebSocket mode
  without the operator manually typing binary flags. It only covers WS — it has no `--remote` or
  `--batch-server` equivalent, and on a git clone it *refuses* to run (`rejectInRepoNpmEntry` /
  `failCloneWithoutBinary`) and tells the user to use `launch-mcp-host.mjs` instead, which per
  CAP-1001 cannot reach WS mode either. Net effect: **on a git clone, no scripted path starts
  WebSocket/Remote/BatchServer mode** — only direct binary invocation does.
- **Confidence:** PROVEN.

#### CAP-1003 — Outer `AddOccamMcpServer()` call in WS transport is protocol-dead
- **Impl:** `WebSocketMcpTransport.StartAsync` line `builder.Services.AddOccamMcpServer()` runs once
  against the `WebApplication`'s own DI container, but the `app.Map("/", ...)` WebSocket handler
  never reads from `context.RequestServices` — it always delegates to `RunSingleSessionAsync`, which
  builds its *own* independent `Host` (CAP-1000). The outer registration's `IMcpServerBuilder`
  return value is discarded and its `WithTools<...>()` chain never executes.
- **Effect:** the outer call still pays real costs once per process start — `AddOccamCore()` DI
  registration, the (idempotent, so harmless) startup-banner write, and one `HttpDaemonHost`
  pre-warm `Task.Run` — for a service collection that never answers a single `tools/call`. Low
  severity (idempotent/best-effort work, not a correctness bug), but it is dead registration code
  worth knowing about before someone "fixes" a WS-mode issue by editing the outer call.
- **Confidence:** PROVEN.

---

## GRAPH EDGES (capability graph)

```
CLI|USES|CAP-001            (Program.Main entry dispatch)
CLI|USES|CAP-002            (offline verbs short-circuit)
TRANSPORT:stdio|USES|CAP-003
TRANSPORT:websocket|USES|CAP-004
TRANSPORT:websocket|USES|CAP-1000   (per-session DI isolation)
TRANSPORT:websocket|USES|CAP-1003   (dead outer registration)
TRANSPORT:remote|USES|CAP-005
TRANSPORT:remote|USES|CAP-1000      (per-session DI isolation, shared code path)
TRANSPORT:remote|USES|CAP-023       (session concurrency semaphore)
TRANSPORT:batchserver|USES|CAP-006
TOOL:occam_failure_atlas|AFFECTED_BY|CAP-1000   (in-memory store reset per WS/Remote reconnect)
OPERATOR:launch-mcp-host.mjs|USES|CAP-1001      (stdio-only, no arg forwarding)
PACKAGE:occam-mcp(npm)|USES|CAP-1002            (independent WS arg forwarding)
PACKAGE:occam-mcp(npm)|HAS_BUG|EF-020           (--host flag silently dropped)
CONNECTOR:*(all 15 adapters)|USES|CAP-1001      (every adapter's launch spec points at launch-mcp-host.mjs)
TOOL:occam_failure_atlas|CONTRADICTS|EF-019     (see CROSS-AGENT DISCREPANCY below)
```

## ARTIFACTS CREATED / CONSUMED

- **Created:** none (read-only audit; this report is the only artifact).
- **Consumed:** none at runtime — pure static code inspection, no live MCP calls needed for this
  area (transport startup/bind logic is deterministic from source).

## "INVISIBLE PRODUCT" ANSWER

What an MCP-only user (Cursor/Claude/Hermes via the documented auto-connect flow) **never sees**:

1. **Three of the four runtime modes are unreachable from the product's own installer/connector
   code.** The "canonical launcher" that every connect adapter, wrapper script, and doctor message
   points at (`launch-mcp-host.mjs`) is hardwired to stdio only (CAP-1001). WebSocket exists only
   via a separate, narrower npm re-implementation (CAP-1002); Remote (WSS+JWT) and BatchServer have
   **no** scripted launch path at all anywhere in the repo — an operator must hand-invoke the
   published binary with `--remote --tls-cert ... --jwt-metadata-uri ...` or `--batch-server`.
2. **`occam_failure_atlas`'s memory resets silently under WS/Remote.** A user who enables
   `OCCAM_ATLAS_MCP=1` behind WebSocket/Remote and reconnects their client gets a stateless failure
   atlas — a completely new, empty `Dictionary` — with no indication in any tool response that the
   history was wiped (CAP-1000). Same risk exists in principle for any future in-memory opt-in
   singleton added under `AddOccamMcpServer`.
3. **BatchServer HTTP API has literally no authentication** (not even a shared secret): its entire
   security model is "nothing else on this machine listens on 127.0.0.1:5051." This is consistent
   with WebSocket's model (also loopback-only, no auth) but is easy to miss because Remote mode
   *does* require TLS+JWT, creating an inconsistent mental model of "the network modes are secured."
4. **`--host` on the npm WS launcher does nothing.** A user following the package's own `--help`
   text (`npx @ff-occam/mcp --mcp-server --host <addr>`) gets silent no-op behavior — the process
   still binds `127.0.0.1` regardless, and there is no warning (EF-019).

## HIDDEN / ADVANCED

- Opt-in tools ignore `OCCAM_PROFILE` (CAP-011) — reconfirmed, unaffected by transport mode.
- WS/Remote session state does not survive reconnect for in-memory-only stores (CAP-1000) — genuinely
  hidden; nothing in tool responses or docs flags this.
- Direct-binary-only reachability of Remote/BatchServer modes (CAP-1001) is de facto "advanced/
  maintainer" despite `--remote`/`--batch-server` being documented CLI flags in `--help`.
- `BatchServerHost` and `OCCAM_BATCH_MCP` MCP tools are fully independent surfaces sharing only
  `Batch.*` service classes (confirmed, matches Wave 3 plan note) — BatchServer never appears in
  `tools/list` under any circumstance since `Program.cs` special-cases it before transport dispatch.

## CROSS-AGENT DISCREPANCY (flag for orchestrator)

`docs-audit/ENGINEERING-FINDINGS.md` already contains **EF-019** (written by S3-04, failure atlas):
> "`--remote` multi-session mode registers `FailureAtlasStore` as one process-wide singleton
> (`AddOccamMcpServer()` called once); ... `occam_failure_atlas` returns the union of every
> connected session's crawl outcomes — one tenant can see which hosts another tenant probed..."

**This appears to conflict with CAP-1000 as verified in this report.** `RemoteMcpTransport` (like
`WebSocketMcpTransport`) calls `AddOccamMcpServer()` **twice**, not once: once on the outer
`WebApplication`'s `IServiceCollection` (`StartAsync`, used only to host JWT auth middleware — see
CAP-1003, this outer registration's `IMcpServerBuilder`/tool chain is never invoked), and once more
**per accepted WebSocket connection** inside `RunSingleSessionAsync`, which builds a brand-new,
independent `Host.CreateApplicationBuilder()` → its own `IServiceCollection` → its own
`FailureAtlasStore` instance (a plain instance field `Dictionary`, not `static`, per
`Telemetry/FailureAtlasStore.cs`). By ordinary .NET DI semantics, a type registered with
`AddSingleton<T>()` on one `IServiceCollection` is **not** shared with a singleton of the same type
registered on a *different* `IServiceCollection`/`ServiceProvider` — each `RunSingleSessionAsync`
call gets its own store, scoped to that one WebSocket connection's lifetime. Actual `tools/call`
dispatch happens inside `RunSingleSessionAsync`'s host (`WithStreamServerTransport` +
`host.RunAsync`), not the outer app's services.

If this reading is correct, the described cross-tenant leak cannot occur through the mechanism EF-019
describes: two separate JWT sessions are two separate WebSocket connections, each running its own
`RunSingleSessionAsync` with its own fresh, empty `FailureAtlasStore` — there is no shared instance
for them to leak through. (A **narrower**, still-true risk survives: within **one** long-lived
session/connection making many tool calls, or in **stdio** mode's single long process, the store is
legitimately a shared, accumulating singleton for that one client for that run — but that is
single-tenant by construction, not a cross-tenant leak.)

**Recommendation:** orchestrator/S3-04 should re-verify EF-019 against `RunSingleSessionAsync` in
`Transport/RemoteMcpTransport.cs` before treating it as PROVEN — either confirm a mechanism this
report missed (e.g., reuse of the outer app's `IServiceProvider` that this report did not find), or
downgrade/correct EF-019. Not resolved unilaterally here to avoid stepping on S3-04's ownership of
`ENGINEERING-FINDINGS.md` row EF-019.

## ENGINEERING FINDINGS (candidates)

### EF-020 (candidate — next free ID as of this write; verify before appending) | BUG-CANDIDATE | CAP-1002
npm package `packages/occam-mcp/bin/occam-mcp.js` (`main()`, lines ~356-369) accepts a `--host
<address>` flag for WebSocket mode (documented in its own `--help` text and header comment) and
forwards it verbatim as `--host wsHost` in `dotnetArgs`. The C# CLI parser
`OccamMcp.Core.Transport.OccamMcpCli.Parse` (`Transport/OccamMcpCli.cs`) has **no** `--host` case —
it only recognizes `--bind`/`-bind` for the WebSocket bind address. Unmatched CLI tokens are simply
skipped by the parse loop (no `invalid_arguments` failure), so `--host <addr>` and its value are
silently swallowed: the host process always binds `127.0.0.1` regardless of what the npm wrapper's
`--host` flag says, with no error surfaced to the user. Impact is currently low (127.0.0.1 also
happens to be the enforced/only-legal bind for WS mode per CAP-024, so no config is actually lost
today) but the flag is a documented no-op that will mislead anyone reading `--help`.
- **Confidence:** PROVEN in code (two files, direct string comparison of flag names).
- **Repro:** `npx @ff-occam/mcp --mcp-server --host 0.0.0.0` — process starts, binds
  `127.0.0.1:5050` anyway, exit code 0, no diagnostic.
- **Security-relevant:** No (the swallowed override cannot be used to *widen* exposure — the
  underlying `OccamMcpCli` would reject a non-127.0.0.1 `--bind` anyway per CAP-024 — but it is a
  UX/correctness bug: the flag's only possible effect is create a false sense that a custom bind
  address was honored).

*(Not appended to `docs-audit/ENGINEERING-FINDINGS.md` by this agent — the ledger already advanced
to EF-019 (S3-04) while this report was being written; orchestrator should assign the final EF
number for this finding to avoid collision with parallel Wave 3 agents writing the same file.)*

## UNCERTAINTIES

- Whether `ConsensusService`/`FailureAtlasSink` hold any additional state beyond `FailureAtlasStore`
  that would compound the CAP-1000 reset effect — not traced beyond the DI registration itself
  (deep dive owned by S3-03/S3-04).
- Whether any *production* connect adapter (outside this repo's own scripts) independently invokes
  the binary with `--mcp-server`/`--remote` flags directly (cannot prove a negative for third-party
  MCP hosts not in this repo).
- Docker/Compose packaging (if it wires `--batch-server` or `--remote` with its own arg-forwarding)
  is explicitly out of scope here — owned by S3-12.

## COMPLETENESS VERDICT

**COMPLETE** for the assigned area: all four transport modes, `Program.cs` dispatch, CLI parsing/
validation/failure codes, auth/bind/security posture, and the operator + npm launch paths that are
supposed to reach them. Wave 1 CAP-001…028 re-verified against current code with zero drift. Four
new CAPs minted (CAP-1000…1003) for genuinely new findings not covered in Wave 1: per-session DI
isolation, the stdio-only canonical launcher, the npm package's parallel arg-forwarding path, and
one dead-registration observation. One bug candidate identified (EF-020 candidate, `--host` flag
no-op — not yet appended pending orchestrator EF-number assignment). One **cross-agent discrepancy**
flagged against S3-04's EF-019 for orchestrator reconciliation (see above) — this report's own
CAP-1000 finding (per-session DI isolation) appears to directly contradict EF-019's "process-wide
singleton" premise.
