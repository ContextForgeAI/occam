# NONCORE-SURFACE-MAP (Wave 3 Phase 3A)

**Generated:** 2026-07-26  
**SoT:** executable registration/entrypoints only. Docs untrusted.  
**Core MCP (15):** already audited Wave 1–2 — listed for boundary only.

---

## Classification legend

| Class | Meaning |
|-------|---------|
| CORE_MCP | In `OccamToolNames`, always-on subject to profile |
| OPT_IN_MCP | MCP tools gated by env, not in OccamToolNames |
| ALTERNATE_MCP | Same MCP tools over non-stdio transport |
| CLI | Host binary or operator CLI verbs |
| OPERATOR | Operator scripts (doctor/connect/session/…) |
| INSTALLER | Install/bootstrap |
| CONNECTOR | Host auto-connect adapters |
| DAEMON | Long-lived worker processes |
| WORKER | One-shot extract workers |
| PACKAGE_ENTRYPOINT | npm/bin published entry |
| INTERNAL_ONLY | Maintainer/CI/gate |
| DEAD_OR_UNREACHABLE | Registered but unused (prior waves) |
| UNKNOWN | Needs deep audit |

---

## A. Core MCP (boundary — Wave 2 done)

15 tools in `OccamMcpServerRegistration.OccamToolNames` — **CORE_MCP**.

---

## B. Opt-in MCP (code-confirmed)

| Surface | Gate | Class | Deep agent |
|---------|------|-------|------------|
| `occam_batch_submit` / `status` / `results` | `OCCAM_BATCH_MCP=1` | OPT_IN_MCP | S3-01 |
| `occam_watch` | `OCCAM_WATCH_MCP=1` | OPT_IN_MCP | S3-02 |
| `occam_crosscheck` | `OCCAM_CONSENSUS_MCP=1` | OPT_IN_MCP | S3-03 |
| `occam_failure_atlas` | `OCCAM_ATLAS_MCP=1` | OPT_IN_MCP | S3-04 |

**Profile interaction (proven W1):** opt-ins registered **without** `OccamToolProfile.IsExposed` — orthogonal to profile.

**Why opt-in (code comments only — intent):**
- Batch: “fire-and-forget… Off by default: no background processor”
- Watch: “stateful page-change… Off by default”
- Crosscheck: “consensus / cloaking… SI-14”
- Atlas: “failure atlas… SI-10… In-memory… not persisted” (tool description)

Formal product intent beyond comments → UNKNOWN until agent report.

---

## C. Alternate MCP / runtime modes

| Surface | Entrypoint | Class | Agent |
|---------|------------|-------|-------|
| stdio MCP (default) | `Program.cs` → `StdioMcpTransport` | ALTERNATE_MCP / default | S3-11 |
| WebSocket local MCP | `--mcp-server` → `WebSocketMcpTransport` (127.0.0.1:5050) | ALTERNATE_MCP | S3-11 |
| Remote WSS+JWT | `--remote` → `RemoteMcpTransport` | ALTERNATE_MCP | S3-11 |
| **BatchServer HTTP API** | `--batch-server` → `BatchServerHost` (`/v1/health`, `/v1/batch/submit|status|results`) | ALTERNATE_MCP + distinct execution | S3-01 + S3-11 |

Note: BatchServer is **not** the same as MCP batch tools, though both use `Batch.*` services when MCP batch is on; BatchServer bypasses MCP registration in `Program.cs`.

---

## D. Host binary CLI verbs (`OccamCliVerbs`)

| Verb | Class | Agent |
|------|-------|-------|
| `keys export` | CLI | S3-06 |
| `verify` | CLI | S3-06 |
| `install-browser` | CLI / OPERATOR | S3-06 (+ browser touch) |
| `version-surface` | CLI | S3-07 |
| `lifecycle` | CLI | S3-07 |

---

## E. Operator CLI (`scripts/occam.mjs` → `CLI_SUBCOMMANDS`)

| Subcommand | Delegate | Class | Agent |
|------------|----------|-------|-------|
| `doctor` | shell occam-doctor | OPERATOR | S3-08 |
| `onboard` / `settings` | occam-onboard.mjs | OPERATOR / INSTALLER | S3-09 |
| `connect` | occam-connect.mjs | CONNECTOR | S3-10 |
| `help` | occam-help.mjs | OPERATOR | S3-07 |
| `refresh` / `restart` | occam-refresh-host.mjs | OPERATOR | S3-07 |
| `smoke` | hermes-smoke.mjs | OPERATOR | S3-08 |
| `update` | internal | OPERATOR | S3-07 |
| `session` | occam-session.mjs | OPERATOR | S3-05 |
| `snippet` | print-mcp-snippet.mjs | OPERATOR | S3-09 |
| `skill` | occam-skill-install.mjs | PACKAGE_ENTRYPOINT / OPERATOR | S3-12 |
| `control` | internal soft TUI | OPERATOR | S3-07 |
| `status` | internal | OPERATOR | S3-07 |
| `contract` / `version-surface` | check-public-mcp-contract.mjs | OPERATOR | S3-07 |


Registry also lists (not all are `occam <sub>`): install.*, get-ff-occam, launch-mcp-host, wrapper, playbook-publish, build-release, gates — classify below.

---

## F. Session operator surface

| Surface | Path | Class | Agent |
|---------|------|-------|-------|
| `occam session init\|list\|import\|export-state` | `scripts/occam-session.mjs` | OPERATOR | S3-05 |
| Profile files under `OCCAM_SESSIONS_ROOT` | Session/* + scripts/lib | OPERATOR | S3-05 |

---

## G. Install / onboard / doctor

| Surface | Path | Class | Agent |
|---------|------|-------|-------|
| `scripts/install.ps1` / `install.sh` | INSTALLER | S3-09 |
| `scripts/get-ff-occam.ps1` / `.sh` | INSTALLER | S3-09 |
| `scripts/occam-doctor.ps1` / `.sh` | OPERATOR | S3-08 |
| `scripts/occam-onboard.mjs` | OPERATOR | S3-09 |
| `scripts/launch-mcp-host.mjs` | OPERATOR | S3-11 / S3-09 |
| `scripts/verify-install.*` | INSTALLER | S3-09 |

---

## H. Connect platform

| Surface | Path | Class | Agent |
|---------|------|-------|-------|
| `scripts/occam-connect.mjs` + `scripts/lib/operator/connect/*` | CONNECTOR | S3-10 |
| Host adapters (code filenames) | adapters/*.mjs | CONNECTOR | S3-10 |

**Adapters on disk:** claude-code, claude-desktop, cline, codex, cursor, gemini, goose, hermes, junie, openclaw, opencode, roo, vscode, windsurf, zed (**15** adapter modules).

---

## I. Daemons / workers

| Surface | Class | Agent note |
|---------|-------|------------|
| `workers/http-extract/http-daemon.mjs` | DAEMON | Covered W1 S18; Wave 3 only if batch/watch changes lifecycle |
| `workers/browser-extract/browser-daemon.mjs` | DAEMON | Same |
| http/browser/css extract.mjs | WORKER | W1 done |

---

## J. Packages / distribution

| Surface | Class | Agent |
|---------|-------|-------|
| `packages/occam-mcp` bin `occam-mcp` | PACKAGE_ENTRYPOINT | S3-12 |
| `packages/occam-agent-sdk` | PACKAGE_ENTRYPOINT | S3-12 |
| `packages/occam-skill` | PACKAGE_ENTRYPOINT | S3-12 |
| `scripts/build-release.*` | INTERNAL_ONLY / release | S3-12 |
| Dockerfile / docker-compose.yml | PACKAGE_ENTRYPOINT / UNKNOWN | S3-12 |
| `.github/workflows/*` | INTERNAL_ONLY | S3-12 (shipping boundary) |

---

## K. Maintainer / CI (not end-user product)

| Surface | Class |
|---------|-------|
| `run-l0-fast`, `ci-agent-mvp-gate`, `run-agent-*`, `run-wide-cursor-desk`, hermes battery, etc. | INTERNAL_ONLY |
| `occam-playbook-publish` | OPERATOR / ADVANCED (community publish) — include in S3-07 briefly |

---

## L. Prior DEAD (do not re-audit as product)

IWorkerProcessSpawner unused, BrowserConcurrencyGate.Run unused, probe.autoRedirect client, etc. — see `DEAD-OR-UNREACHABLE.md`.

---

## Phase 3B agent plan (after this file)

| ID | Area | Report path | CAP range if new |
|----|------|-------------|------------------|
| S3-01 | Batch MCP + BatchServer | `subsystems/batch-batchserver.md` | CAP-800…829 |
| S3-02 | Watch | `subsystems/watch.md` | CAP-830…849 |
| S3-03 | Consensus/crosscheck | `subsystems/consensus-crosscheck.md` | CAP-850…869 |
| S3-04 | Failure atlas | `subsystems/failure-atlas.md` | CAP-870…879 |
| S3-05 | Session lifecycle | `SESSION-LIFECYCLE.md` + `subsystems/session-lifecycle.md` | CAP-880…899 |
| S3-06 | Verify CLI / offline trust | `subsystems/verify-cli.md` | CAP-900…919 |
| S3-07 | Main CLI surface | `CLI-SURFACE.md` | CAP-920…939 |
| S3-08 | Doctor + smoke | `subsystems/doctor.md` | CAP-940…959 |
| S3-09 | Install / onboard | `subsystems/install-onboard.md` | CAP-960…979 |
| S3-10 | Connect platform | `CONNECT-PLATFORM.md` + `HOST-CAPABILITY-MATRIX.md` | CAP-980…999 |
| S3-11 | Runtime modes / transports | `RUNTIME-MODES.md` | CAP-1000…1019 |
| S3-12 | Packaging / shipping | `subsystems/packaging-distribution.md` | CAP-1020…1039 |

**Reuse Wave 1–2 CAP IDs aggressively.** Prefer edges over new CAPs.

---

## Phase 3A completeness

Enumeration of registration/entrypoints for opt-in MCP, transports, CLI verbs, operator subcommands, session, install/doctor/connect, packages, Docker present. Ready for Phase 3B.
