# Host connectors

**Slug:** `host-connectors` · **Product system:** PS-9 Operator surface · **CAPs:** 22 · **Public relevance:** HIGH

**Member CAPs:** CAP-980…999, CAP-1040, CAP-1041  
**Product capability:** CAP-980  
**Engineering findings:** EF-021

**ID note (C9):** CAP-995/CAP-999 historically collided; reminted adapters are **CAP-1040 `roo`**, **CAP-1041 `junie`**. Do not resurrect duplicate IDs (`CANONICAL-AUDIT-INDEX.md` C9).

## What it is

`occam connect` (`scripts/occam-connect.mjs`) — multi-adapter platform that detects MCP/IDE hosts, plans add/update, applies managed entries, verifies, and optionally rolls back. Fifteen adapters under one orchestrator; adapters are **mechanisms**, not separate product entrypoints (`ENTRYPOINT-MODEL.md` §2.1).

## Why it exists

One-liner installs should wire Cursor/Hermes/Claude/… without hand-editing JSON. Ownership guards prevent clobbering unrelated MCP entries (`CAP-982`).

## User-visible entrypoints

| Entrypoint | Notes | Evidence |
|------------|-------|----------|
| `occam connect` | Primary | CAP-980; CLI-SURFACE |
| `node scripts/occam-connect.mjs` | Direct | flags below |
| `get-ff-occam.*` post-install | Auto-invokes once | install CAP-971 |

Flags: `--json`, `--detect-only`, `--force`, `--only IDS`, `--skip-occam-verify`.  
Env: `OCCAM_HOME`, `OCCAM_CONNECT=auto|off|detect`, `OCCAM_CONNECT_FORCE=1`.

## Core behavior

### Pipeline per host (CAP-980)

```
assertLaunchable → detect runtimes (classify-only) → detect hosts
 → selectAutoConnectAdapters (Tier A; Tier B only with --only)
 → plan → apply → verifyHost → evaluateReadyState
 → decidePostVerifyCleanup / rollback
 → verifyOccamMcp (stdio tools/list) → aggregateConnectionReady
 → write ~/.occam/connect-last.json
```

Independent per-host transactions — **not** global all-or-nothing.

### Stable launch spec (CAP-981)

Every managed entry: `node <OCCAM_HOME>/scripts/launch-mcp-host.mjs` with `OCCAM_HOME`, `OCCAM_BANNER=0`, `WT_OCCAM_BANNER=0`, `OCCAM_CONNECT_MANAGED=occam-managed:v1`. Optional `occam-wrapper.sh` on POSIX only.

### Ownership (CAP-982)

Mutate only if managed marker or command points at wrapper/launcher. Else `skip-unmanaged` unless `--force`.

### Config merge engine (CAP-983)

`config-engine.mjs`: backup `*.occam-bak` → atomic write → restore. JSONC with comments → **refuse** (`action: jsonc`), never strip comments.

### Verification ladder (CAP-984)

Levels 0–6. Caps: NATIVE_CLI ≤5; CONFIG_FILE ≤1 (`CONFIG_VALID`). `EXPECTED_MIN_TOOLS=15` on Occam self-verify.

### Policy (CAP-986)

CI/server: no mutate unless `OCCAM_CONNECT_FORCE=1`. Desktop default `auto` with `mutateHosts:true` even non-TTY (curl|bash North Star) — suppress with `OCCAM_CONNECT=off|detect`.

## Advanced behavior

### NATIVE_CLI (Tier A, maxVerify 5)

| CAP | id | Notes |
|-----|-----|-------|
| CAP-987 | `hermes` | YAML via `hermes mcp *` |
| CAP-988 | `openclaw` | JSON + CLI; npx fallback |
| CAP-989 | `claude-code` | `claude mcp add -s user` |
| CAP-990 | `codex` | `codex mcp add/get/remove` |
| CAP-991 | `gemini` | Disabled folder → action required, not hard fail |

### CONFIG_FILE

| CAP | id | Tier | Ceiling | Notes |
|-----|-----|------|---------|-------|
| CAP-992 | `cursor` | A | 1 | `~/.cursor/mcp.json`; `requiresRestart:true` |
| CAP-993 | `claude-desktop` | A | 1 | Classic vs MSIX ambiguous → never guesses |
| CAP-994 | `vscode` | B | 1 | root `servers`; profile ambiguity refuse |
| CAP-995 | `cline` | B | 1 | globalStorage settings |
| CAP-1040 | `roo` | B | 1 | reminted |
| CAP-996 | `windsurf` | B | 1 | |
| CAP-997 | `zed` | B | 1 | `context_servers`; JSONC usually refuse |
| CAP-998 | `opencode` | B | 1 | root `mcp`; command-array codec |

### ASSISTED (no auto write)

| CAP | id | Notes |
|-----|-----|-------|
| CAP-999 | `goose` | Hint `goose configure` |
| CAP-1041 | `junie` | Tier C; NEEDS LIVE VALIDATION |

Model runtimes (Ollama, etc.) are detect-only Tier D — never MCP targets.

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| get-ff-occam auto-connect | May mutate desktop configs |
| Skip reasons for non-targets | Always explained, not silent |
| Post-verify rollback | **Dead** for CONFIG_FILE + `requiresRestart:true` (**EF-021**) |

## Parameters

See flags/env above. `--only` expands Tier B eligibility. `--force` overrides unmanaged skip.

## Configuration

| Artifact | Role |
|----------|------|
| Host MCP JSON / CLI registries | Mutated targets (ST-24) |
| `*.occam-bak` | Backups |
| `~/.occam/connect-last.json` | Last-run report (ART-030; ST-23) |

## Backends

Not applicable. Registers stdio launcher only (CAP-1001) — never WS/Remote.

## Sessions / state

HOST CONFIGURATION mutations. Rollback intended via bak/restore or host CLI remove — **EF-021** blocks effective rollback when restart required and verify cannot observe IDE load.

## Network behavior

May spawn host CLIs and one Occam stdio self-verify. No page fetch.

## Artifacts produced

Managed MCP entries, backups, `connect-last.json`, TTY/JSON transcript.

## Trust / provenance properties

- Ownership marker prevents casual overwrite — not cryptographic trust.
- Verification levels must not be over-claimed: CONFIG_FILE never proves IDE loaded entry.
- Self-verify expects 15 tools — profile/opt-in mismatch can fail similarly to EF-033.

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Missing launcher | Abort before host mutation |
| JSONC | Refuse write + user hint |
| Ambiguous paths | No guess (Claude Desktop, VS Code profiles) |
| Verify fail after apply | Attempt rollback — **EF-021** may no-op for restart hosts |
| CI without FORCE | Detect-only |

## Platform differences

| Area | Difference |
|------|------------|
| Wrapper script | POSIX only |
| Claude Desktop paths | Classic APPDATA vs MSIX |
| VS Code / Cline / Roo paths | OS-specific globalStorage |
| Process quoting | Windows `cmd.exe` hardening in `process.mjs` |

See `HOST-CAPABILITY-MATRIX.md`, `PLATFORM-DIFFERENCES.md`.

## Composition with other capabilities

- Depends on **install-onboarding** tree + launch-mcp-host.
- Feeds operator **CLI** (`occam connect`).
- Level B tarball historically **omits** `occam-connect.mjs` while help may advertise it (**EF-035** packaging).

## Known limitations

- CONFIG_FILE verify ceiling = file bytes, not live IDE.
- Assisted hosts never auto-write.
- EF-021 rollback gap for Cursor-like restart hosts.
- 15-tool expect may disagree with `OCCAM_PROFILE`.
- Reminted CAP IDs must be used for roo/junie.

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-021** | Post-verify rollback dead for CONFIG_FILE hosts with `requiresRestart:true` |

## Code evidence

- `scripts/occam-connect.mjs`
- `scripts/lib/operator/connect/orchestrator.mjs`, `policy.mjs`, `ownership.mjs`, `config-engine.mjs`, `verification.mjs`, `launch-spec.mjs`, `adapters/*`
- `docs-audit/CONNECT-PLATFORM.md`, `HOST-CAPABILITY-MATRIX.md`
- Peer: `ENTRYPOINT-MODEL.md`, `STATE-MODEL.md` ST-23/24

## Public-doc relevance

**HIGH.** Document ownership, JSONC refusal, tier/auto rules, restart hosts, and that connect ≠ proving the IDE loaded Occam.

## Handbook relevance

**Connect chapter** after install. Table of 15 hosts with method/tier/verify ceiling; call out EF-021 and Level B missing script (EF-035).
