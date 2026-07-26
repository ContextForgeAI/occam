# S0 — Runtime / MCP surface

**Agent:** Orchestrator takeover (S0 subagent `f066cb9e` failed: `resource_exhausted` mid-Write)  
**CAP range:** CAP-001 … CAP-049 (allocated CAP-001 … CAP-028; 029–049 reserved)  
**SoT:** current executable code only. Docs untrusted.

---

## AUDIT TARGET
Runtime entry, transport modes, MCP tool registration, `OCCAM_PROFILE`, opt-in tool gates, server instructions, binding guard.

## FILES INSPECTED
- `src/FFOccamMcp.Core/Program.cs`
- `Transport/OccamMcpServerRegistration.cs`
- `Transport/OccamToolProfile.cs`
- `Transport/OccamServerInstructions.cs`
- `Transport/OccamMcpCli.cs`
- `Transport/StdioMcpTransport.cs`
- `Transport/WebSocketMcpTransport.cs`
- `Transport/RemoteMcpTransport.cs`
- `Transport/RemoteMcpAuthOptions.cs`
- `Transport/WebSocketMcpStreams.cs`
- `Transport/McpArgumentBindingGuard.cs`
- `Transport/McpContentLengthFraming.cs`
- `Transport/IMcpTransport.cs`
- `Cli/OccamCliVerbs.cs` (dispatch only)
- `benchmarks/l0-gate/L2TransportUnitTests.cs` (evidence)

## ENTRYPOINTS
1. `Program.Main` → `OccamCliVerbs.TryRun` (offline verbs; no MCP) **or**
2. `OccamMcpCli.Parse` → `BatchServerHost` **or** `IMcpTransport.StartAsync`

## RUNTIME PATH
```
args → CliVerbs? → exit
     → Parse CLI → help/invalid?
     → Mode=BatchServer → BatchServerHost
     → Mode=WebSocket|Remote|Stdio → transport.StartAsync
         → AddOccamMcpServer (profile + opt-in flags) → tools/list surface
```

---

## CAPABILITIES

### CAP-001 — Process entry dual-path (CLI verbs vs MCP host)
- **Impl:** `Program.cs:12-69`
- **Reach:** any binary invocation
- **Status:** public
- **Confidence:** PROVEN

### CAP-002 — Offline CLI verb dispatch (pre-transport)
- **Impl:** `Cli/OccamCliVerbs.TryRun` — `keys export`, `verify`, `install-browser`, `version-surface`, `lifecycle`
- **Status:** public (CLI)
- **Note:** Deep trust/browser detail owned by S19/S18; S0 only proves verbs short-circuit before MCP.
- **Confidence:** PROVEN

### CAP-003 — Transport mode: stdio (default)
- **Impl:** `OccamMcpCli` default `Stdio`; `StdioMcpTransport`
- **Defaults:** Mode=Stdio when no `--mcp-server`/`--remote`/`--batch-server`
- **Status:** public
- **Confidence:** PROVEN

### CAP-004 — Transport mode: local WebSocket MCP
- **Impl:** `--mcp-server` → `WebSocketMcpTransport`; default port **5050**, bind **127.0.0.1 only** (public bind rejected)
- **Security:** loopback-only
- **Tests:** `L2TransportUnitTests`
- **Status:** advanced
- **Confidence:** PROVEN

### CAP-005 — Transport mode: Remote WSS + JWT
- **Impl:** `--remote` → `RemoteMcpTransport`; default port **8443**; requires TLS cert (+ optional password); JWT issuer/audience/metadata (CLI overrides env `OCCAM_TLS_*`, `OCCAM_JWT_*`); session cap `OCCAM_REMOTE_MAX_SESSIONS`
- **Failures:** `remote_requires_tls_cert`, `tls_cert_not_found`, `remote_requires_jwt_metadata`, `invalid_jwt_metadata_uri`
- **Security:** HTTPS metadata required; no query-string token hook; forbids query tokens
- **Status:** advanced
- **Confidence:** PROVEN

### CAP-006 — Transport mode: Batch HTTP server
- **Impl:** `--batch-server` → `BatchServerHost` (bypasses MCP tool registration path in `Program.cs`); default port **5051**, bind 127.0.0.1 only
- **Status:** advanced / experimental surface
- **Note:** Distinct from MCP opt-in `OCCAM_BATCH_MCP` tools (CAP-015). Wave 3 must reconcile BatchServer vs batch MCP tools.
- **Confidence:** PROVEN

### CAP-007 — Core MCP catalog: 15 always-registered names (profile=full)
- **Impl:** `OccamMcpServerRegistration.OccamToolNames` (lines 15-32)
- **List:** client_capabilities, transcode, probe, digest, playbook_resolve, map, playbook_heal, playbook_save, extract_knowledge, search, verify, claim_check, attest, playbook_lint, dataset_export
- **Status:** public (default surface)
- **Confidence:** PROVEN

### CAP-008 — `OCCAM_PROFILE` role-scoped registration
- **Impl:** `OccamToolProfile.Resolve` / `IsExposed` / `GetExposedToolNames`
- **Values:** `full` | `reader` | `researcher` | `auditor` (unknown → warn stderr → **full**)
- **Default:** `full`
- **Effect:** **registration-time** — tools not in profile are never `WithTools`'d → absent from `tools/list`
- **Status:** advanced (operator/env)
- **Confidence:** PROVEN

### CAP-009 — Profile tool sets (exact)

| Profile | Tools (code) |
|---------|----------------|
| `full` | all 15 from CAP-007 |
| `reader` | client_capabilities, transcode, probe, digest, map, search, extract_knowledge (**7**) |
| `researcher` | reader + claim_check, verify (**9**) |
| `auditor` | researcher + attest, dataset_export, playbook_lint (**12**) |

**Not in reader/researcher/auditor:** `occam_playbook_resolve`, `occam_playbook_heal`, `occam_playbook_save` (heal/save/resolve only on `full`).

- **Confidence:** PROVEN

### CAP-010 — Profile-aware MCP `instructions` on initialize
- **Impl:** `OccamServerInstructions.TextFor(profile)` → `AddMcpServer(options => ServerInstructions = …)`
- **Effect:** consuming model sees pick-list matching exposed tools; FullText adds playbook heal/save path
- **Status:** public (protocol)
- **Confidence:** PROVEN

### CAP-011 — Opt-in MCP tools are **not** profile-filtered
- **Impl:** `OccamMcpServerRegistration` lines 120-157 — batch/watch/crosscheck/atlas gated **only** by env flags, after profile core registration
- **Implication:** `OCCAM_PROFILE=reader` + `OCCAM_BATCH_MCP=1` → reader tools **plus** batch tools on `tools/list`
- **Status:** advanced / easy-to-miss
- **Confidence:** PROVEN

### CAP-012 — Opt-in: `OCCAM_BATCH_MCP` → batch_submit/status/results + hosted processor
- **Default:** off
- **Impl:** JsonFileBatchJobStore + BatchJobService + BatchJobProcessor + 3 tools
- **Confidence:** PROVEN

### CAP-013 — Opt-in: `OCCAM_WATCH_MCP` → `occam_watch`
- **Default:** off
- **Confidence:** PROVEN

### CAP-014 — Opt-in: `OCCAM_CONSENSUS_MCP` → `occam_crosscheck`
- **Default:** off
- **Confidence:** PROVEN

### CAP-015 — Opt-in: `OCCAM_ATLAS_MCP` → `occam_failure_atlas` + FailureAtlasSink DI wrap
- **Default:** off
- **Confidence:** PROVEN

### CAP-016 — Digest `urls` input schema union (compat)
- **Impl:** `WithDigestUrlsUnion` replaces schema for `urls` with oneOf array|string (deprecated string forms)
- **Status:** compatibility
- **Confidence:** PROVEN

### CAP-017 — Argument-binding guard → typed `invalid_arguments`
- **Impl:** `McpArgumentBindingGuard` CallTool filter; maps missing-required / JSON conversion failures to `ok:false` JSON result (**not** `IsError=true`)
- **Status:** public contract
- **Confidence:** PROVEN

### CAP-018 — HTTP daemon pre-warm on MCP server start
- **Impl:** registration Task.Run → `HttpDaemonHost.TryEnsureRunning` when daemon enabled and `OCCAM_HTTP_DAEMON_PREWARM` default true
- **Status:** internal/performance
- **Confidence:** PROVEN

### CAP-019 — Startup banner + worker path resolve at registration
- **Impl:** `WorkerPaths.Resolve()` + `OccamLogger.TryWriteStartupBanner`
- **Status:** operator-visible (stderr)
- **Confidence:** PROVEN

### CAP-020 — WebSocket message size cap
- **Impl:** `McpWebSocketLimits.ReadMaxMessageBytes` (tests assert 65536); binary frames rejected
- **Status:** advanced
- **Confidence:** PROVEN

### CAP-021 — Content-length framing helper (stdio protocol)
- **Impl:** `McpContentLengthFraming`
- **Tests:** L2TransportUnitTests
- **Status:** internal/protocol
- **Confidence:** PROVEN

### CAP-022 — Help text reports live profile tool count
- **Impl:** `OccamMcpCli.WriteUsage` prints `GetExposedToolNames().Length` and profile id
- **Status:** operator
- **Confidence:** PROVEN

### CAP-023 — Remote session concurrency limit
- **Impl:** `RemoteMcpAuthOptions.ReadMaxSessions` / reject when limit reached
- **Env:** `OCCAM_REMOTE_MAX_SESSIONS`
- **Status:** advanced
- **Confidence:** PROVEN

### CAP-024 — CLI bind policy asymmetry
- **Impl:** WS + BatchServer reject non-127.0.0.1 bind; Remote allows any parseable IP (incl. `0.0.0.0`)
- **Security:** intentional for remote TLS mode; local WS stays loopback
- **Confidence:** PROVEN

### CAP-025 — Surface taxonomy (code-derived, not docs)
Distinguish at least:
1. **Default core MCP surface** — 15 tools, profile=`full`, opt-ins off
2. **Profile-reduced MCP surface** — CAP-009
3. **Env opt-in MCP tools** — CAP-012…015 (orthogonal to profile)
4. **Operator/CLI capabilities** — CAP-002 (no tools/list)
5. **Non-MCP host modes** — BatchServer (CAP-006), transports CAP-003…005

Saying only “Occam has 15 tools” is **incomplete** relative to runtime truth.

### CAP-026 — `AddOccamCore` DI always runs before tool registration
- **Impl:** `services.AddOccamCore()` first in `AddOccamMcpServer`
- **Implication:** managed backends, proxy rotation, etc. may be registered in DI even when unused (see S18)
- **Confidence:** PROVEN (call site); DI contents deferred to composition audit

### CAP-027 — Transport unit-test evidence for tool catalog length
- **Impl:** `L2TransportUnitTests` asserts `GetExposedToolNames` length == `OccamToolNames.Length` and presence of search/verify/claim_check/attest/playbook_lint/dataset_export
- **Gap:** tests do **not** appear to assert profile narrowing (Wave 4 / second pass)
- **Confidence:** PROVEN for full profile; UNCERTAIN whether profile suites exist elsewhere

### CAP-028 — Stderr diagnostics / stdout MCP purity (host contract)
- **Impl:** UTF-8 console encoding set in `Program.cs`; CLI usage → stderr; MCP JSON on transport streams
- **Confidence:** STRONGLY INFERRED from Program + Cli patterns (full stdout audit not exhaustive here)

---

## ADVANCED / HIDDEN
- Opt-in tools ignore `OCCAM_PROFILE` (CAP-011)
- Playbook authoring tools only on `full` (CAP-009)
- BatchServer vs `OCCAM_BATCH_MCP` dual surfaces (CAP-006 vs CAP-012)
- Remote public bind allowed only with TLS+JWT

## CONFIG / ENV (this subsystem)
`OCCAM_PROFILE`, `OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP`, `OCCAM_HTTP_DAEMON` / `OCCAM_HTTP_DAEMON_PREWARM`, remote TLS/JWT/session envs (see S24 catalog)

## FALLBACKS
- Invalid `OCCAM_PROFILE` → `full` + stderr warning
- Binding failures → typed `invalid_arguments` result

## FAILURES
CLI: `invalid_arguments`, `remote_requires_tls_cert`, `tls_cert_not_found`, `remote_requires_jwt_metadata`, `invalid_jwt_metadata_uri`

## SECURITY / TRUST
Loopback lock for WS/BatchServer; Remote requires TLS cert + JWT metadata HTTPS; binding guard prevents opaque SDK errors

## TEST EVIDENCE
`benchmarks/l0-gate/L2TransportUnitTests.cs`

## DOC GAPS (deferred Phase 8)
Profile matrix, opt-in orthogonality, dual batch surfaces, remote mode — likely STALE/MISSING in public docs (not verified this wave)

## UNCERTAINTIES
- Exact BatchServerHost HTTP API surface (Wave 3)
- Whether profile narrowing is gate-tested
- Full text of all four ServerInstructions variants not line-copied here (Full/Reader/Researcher/Auditor exist)

## RECOMMENDED DOC CHANGES (later)
Document four-layer surface taxonomy (CAP-025); never claim a single fixed tool count without profile+opt-in qualifiers.

## COMPLETENESS VERDICT
**COMPLETE** for registration/transport/profile baseline. BatchServer HTTP details and composition DI dump deferred.
