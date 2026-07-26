# Runtime transports

**Slug:** `runtime-transports` · **Product system:** PS-8 Runtime and exposure · **CAPs:** 10 · **Public relevance:** HIGH

**Member CAPs:** CAP-002, CAP-003, CAP-004, CAP-005, CAP-006, CAP-020, CAP-021, CAP-023, CAP-024, CAP-027  
**Product capability:** CAP-003  
**Engineering findings:** (family list empty; related EF-041, EF-050, CAP-1000…1003 live in `mcp-exposure` / packaging — cite where behavior overlaps)

## What it is

Process-level modes that decide **how** a caller speaks to the Occam host after `Program.Main` / `OccamMcpCli.Parse`:

| Mode | Flag / default | Protocol |
|------|----------------|----------|
| Stdio MCP | default (no flag) | JSON-RPC on stdin/stdout (`CAP-003`) |
| Local WebSocket MCP | `--mcp-server` | MCP over WS, loopback (`CAP-004`) |
| Remote WSS + JWT | `--remote` | Authenticated MCP (`CAP-005`) |
| BatchServer HTTP | `--batch-server` | Non-MCP REST (`CAP-006`) |

Offline CLI verbs run **before** transport (`CAP-002`; `OccamCliVerbs.TryRun`). Exactly one mode per process (`RUNTIME-MODES.md`; `ENTRYPOINT-MODEL.md`).

## Why it exists

Default agent embedding is stdio via `launch-mcp-host.mjs`. Alternate modes support local multi-client WS, authenticated remote MCP, and non-MCP batch automation — without conflating those with “another way to list the same 15 tools over HTTP” (`CAP-801` for BatchServer).

## User-visible entrypoints

| Entrypoint | Reaches | Evidence |
|------------|---------|----------|
| `OccamMcp.Core` / AOT binary with args | All four modes | `Program.cs`, `OccamMcpCli.cs` |
| `scripts/launch-mcp-host.mjs` | **Stdio only** (`[]` args) | CAP-1001; `RUNTIME-MODES.md` |
| `packages/occam-mcp/bin/occam-mcp.js` | May forward WS subset | CAP-1002 |
| Connect / doctor banners | Point at canonical launcher → stdio | 33 call sites |

**Binding contradiction:** `ENTRYPOINT-MODEL.md` counts 51 entrypoints; transports beyond stdio require **direct binary flags**, not the canonical launcher.

## Core behavior

### Stdio (CAP-003)

Default when no mode flag. Stderr = diagnostics/banner; stdout = MCP JSON only (`CAP-028` peer in mcp-exposure). Used by Cursor/Hermes/connect adapters via launcher.

### Local WebSocket (CAP-004)

Port default **5050**. Bind policy: **only `127.0.0.1`** (`CAP-024`). MCP JSON-RPC over WebSocket. Per-connection DI (`CAP-1000`). Message size cap (`CAP-020`).

### Remote WSS + JWT (CAP-005)

Port default **8443**. Requires TLS cert + HTTPS JWT metadata. `Authorization: Bearer` required; query `?token=` / `?access_token=` → 400 `query_token_forbidden`. Full `TokenValidationParameters` (issuer/audience/lifetime/signature; 30s skew). Unauthenticated `/health` by design. Session concurrency: `OCCAM_REMOTE_MAX_SESSIONS` default **4**, clamp 1–32 (`CAP-023`). Bind may be non-loopback (`CAP-024` asymmetry).

### BatchServer (CAP-006)

Port default **5051**, loopback-only, **no auth** (`CAP-817` in batch-jobs). Not MCP. See family `batch-jobs`.

### Offline verbs first (CAP-002)

`keys export`, `verify`, `install-browser`, `version-surface`, `lifecycle` — no transport start.

## Advanced behavior

| Behavior | Notes | Evidence |
|----------|-------|----------|
| Content-Length framing helper | Used on **WS** path — CAP-021 older “stdio framing” prose corrected by **C5**/GAP-011 | `CANONICAL-AUDIT-INDEX.md` C5 |
| Per-connection DI (WS/Remote) | Fresh singletons per socket; atlas resets; file stores reload | CAP-1000 |
| Outer `AddOccamMcpServer` on WS host | Protocol-dead for tools; session builder is live | CAP-1003 |
| Unit-test catalog length | Transport tests assert tool counts | CAP-027 |

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| Launcher drops all CLI args | Operators cannot enable WS/Remote via connect path |
| npm WS arg forward | Diverges from canonical launcher (`CAP-1002`) |
| Each WS session `AddOccamCore` | Invokes `BrowserPoolManager.InstallShared` → **`StopAll()`** kills process-wide pool (**EF-041**) |

## Parameters

CLI (representative; see `OccamMcpCli.WriteUsage`): `--mcp-server`, `--remote`, `--batch-server`, `--port`, `--bind`, `--tls-cert`, `--jwt-*` (remote). Offline verbs have their own flags (`keys`, `verify --mode`, etc.).

## Configuration

| Env | Role |
|-----|------|
| `OCCAM_REMOTE_MAX_SESSIONS` | Remote concurrency (`CAP-023`) |
| `OCCAM_BATCH_PORT` | BatchServer port |
| TLS/JWT envs | Remote only (`CAP-388`/`CAP-389` live under operator-cli config bucket) |
| WS message size | CAP-020 (see `ENVIRONMENT-VARIABLES.md`) |

## Backends

Transports do not select extract backends. They expose whatever tools the registration layer adds.

## Sessions / state

| Mode | Session shape |
|------|---------------|
| Stdio | One process ≈ one long session |
| WS/Remote | Per-connection DI (`CAP-1000`); reconnect clears in-memory singletons |
| BatchServer | Own DI; job file persistence |

EF-041: new session DI can kill shared browser pool — availability finding, not a transport feature.

## Network behavior

| Mode | Listen | Auth |
|------|--------|------|
| Stdio | none | parent process pipes |
| WS | loopback | none beyond local |
| Remote | any bind | TLS + JWT |
| BatchServer | loopback | none |

## Artifacts produced

None intrinsic. Stderr banner/logs ephemeral (`STATE-MODEL.md` ST-28).

## Trust / provenance properties

Remote JWT validates caller identity to the MCP session — does **not** sign page content. BatchServer has no caller auth. Stdio trusts the parent host process.

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Non-loopback bind on WS/BatchServer | Parse reject (`CAP-024`) |
| Remote missing TLS/JWT config | Start failure |
| Remote bad/missing Bearer | 401 |
| Unknown CLI args on Docker HEALTHCHECK | See EF-051 under packaging — `--version` not a verb |

## Platform differences

Process spawning and path separators for cert files OS-dependent (`PLATFORM-DIFFERENCES.md`). Bind/`127.0.0.1` semantics are CLI-enforced. AOT `Assembly.Location` empty — `version-surface` prefers `ProcessPath` (Surface A).

## Composition with other capabilities

- **Hosts** all MCP families (PS-1…7) and opt-ins.
- **Mutually exclusive** with offline verbs for a given invocation path.
- Align with `ENTRYPOINT-MODEL.md`: 15 tools ≠ product; transports are L0 exposure.

## Known limitations

- Canonical operator path cannot start non-stdio modes.
- WS multi-session pool kill (**EF-041**).
- CAP-021 naming historically confused stdio vs WS framing (C5).
- BatchServer is easy to mislabel as “MCP over HTTP.”

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-041** | `InstallShared`/`StopAll` per WS/Remote session DI kills process-wide browser pool |
| **EF-050** | `launch-mcp-host` merges `~/.occam/onboard.json` env every launch (config surface) |
| C5 / GAP-011 | Content-Length framing is WS, not stdio |

## Code evidence

- `src/FFOccamMcp.Core/Program.cs`
- `src/FFOccamMcp.Core/Transport/OccamMcpCli.cs`, `StdioMcpTransport.cs`, `WebSocketMcpTransport.cs`, `RemoteMcpTransport.cs`
- `src/FFOccamMcp.Core/Batch/BatchServerHost.cs`
- `scripts/launch-mcp-host.mjs`
- Deep: `docs-audit/subsystems/runtime-mcp.md`, `RUNTIME-MODES.md`
- Peer: `ENTRYPOINT-MODEL.md`

## Public-doc relevance

**HIGH.** Default = stdio. Document Remote auth requirements and BatchServer non-MCP nature. Do not imply connect launches WS/Remote.

## Handbook relevance

**Runtime chapter** before tools. Explicit mode matrix + launcher limitation + EF-041 availability note.
