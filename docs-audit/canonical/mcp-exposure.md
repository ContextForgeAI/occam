# MCP exposure

**Slug:** `mcp-exposure` · **Product system:** PS-8 Runtime and exposure · **CAPs:** 22 · **Public relevance:** HIGH

**Member CAPs:** CAP-001, CAP-007…CAP-019, CAP-022, CAP-025, CAP-026, CAP-028, CAP-1000…CAP-1003  
**Product capability:** CAP-007  
**Engineering findings:** EF-033 (family); EF-041 / EF-050 related via launch/DI

## What it is

The registration and advertisement layer that decides **which MCP tools exist on `tools/list`**, what `instructions` text clients receive, and how DI/host startup wraps the core. Canonical 15-name catalog: `OccamMcpServerRegistration.OccamToolNames` (`OccamMcpServerRegistration.cs:15-32`). Profiles narrow core tools; four env flags add opt-ins **without** profile filtering (`CAP-011`).

**Product capability ≠ 15 tools** (`ENTRYPOINT-MODEL.md`): max MCP surface is 15+6=21 names; many product entrypoints are CLI/install/connect.

## Why it exists

Match agent role (reader vs auditor) and keep experimental surfaces (batch/watch/consensus/atlas) off by default while still shipping in the same binary.

## User-visible entrypoints

| Surface | What caller sees | Evidence |
|---------|------------------|----------|
| `tools/list` after initialize | Profile ∩ core ∪ opt-ins | registration `:78-157` |
| MCP `instructions` | Profile-aware guide | `OccamServerInstructions.cs`; CAP-010 |
| Host help / banner | Live tool count | CAP-019, CAP-022 |
| Dual process entry | Offline CLI verbs **or** MCP host | CAP-001 |

## Core behavior

### Catalog (CAP-007)

Fifteen always-on **names** under `OCCAM_PROFILE=full`. Exact list is code SoT — do not hand-count in prose.

### Profiles (CAP-008, CAP-009)

| Profile | Count | Evidence |
|---------|------:|----------|
| `full` (default) | 15 | `OccamToolProfile.cs`; `PROFILE-TOOL-MATRIX.md` |
| `reader` | 7 | same |
| `researcher` | 9 | same |
| `auditor` | 12 | same |
| invalid | → full + stderr warn | CAP-008 |

Profile gates **registration only**, not handler semantics.

### Opt-ins (CAP-012…015)

| Flag | Tools |
|------|-------|
| `OCCAM_BATCH_MCP=1` | batch_submit/status/results + hosted processor |
| `OCCAM_WATCH_MCP=1` | `occam_watch` |
| `OCCAM_CONSENSUS_MCP=1` | `occam_crosscheck` |
| `OCCAM_ATLAS_MCP=1` | `occam_failure_atlas` + telemetry sink swap |

### DI-first (CAP-026)

`AddOccamCore()` always runs before tool registration. WS/Remote build a **new** container per connection (`CAP-1000`).

### Stderr/stdout purity (CAP-028)

Diagnostics → stderr; MCP JSON → stdout.

## Advanced behavior

| Behavior | CAP |
|----------|-----|
| Digest `urls` schema union (compat) | CAP-016 |
| Argument-binding → typed `invalid_arguments` | CAP-017 |
| HTTP daemon pre-warm on server start | CAP-018 |
| Startup banner + worker path resolve | CAP-019 |
| Surface taxonomy code-derived | CAP-025 |
| Launcher stdio-only | CAP-1001 |
| npm independent WS arg forward | CAP-1002 |
| Outer WS `AddOccamMcpServer` protocol-dead | CAP-1003 |
| Crosscheck absent from instructions | CAP-861 (consensus family); EF-031 |

## Automatic / silent behavior

| Silent | Effect | Evidence |
|--------|--------|----------|
| Invalid profile → full | Wider surface than operator intended | CAP-008 |
| Opt-ins ignore profile | `reader` + `OCCAM_CONSENSUS_MCP` still exposes crosscheck | CAP-011 |
| Atlas replaces telemetry sink | CAP-875 | 
| `hermes-smoke` expects 15 tools | Fails under non-full profile | **EF-033** |
| Launcher merges onboard.json env | Every launch | **EF-050** |
| Per-session `InstallShared` | Pool kill | **EF-041** |

## Parameters

Not a tool family — exposure is env/CLI at process start. Tool-level params belong to other families.

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_PROFILE` | full | Core registration set |
| `OCCAM_*_MCP` flags | off | Opt-in tools |
| `OCCAM_BANNER` / `OCCAM_LOG` | on / level | Banner & diagnostics |
| `OCCAM_HOME` | discovery | Worker path at registration |

## Backends

None. Exposure precedes routing.

## Sessions / state

Registration is process/session start. Per-connection DI on WS/Remote resets in-memory opt-in singletons (`CAP-1000`). File-backed stores (watch/batch) reload from disk paths.

## Network behavior

HTTP daemon pre-warm may open local worker sockets (`CAP-018`). Transport listen behavior → `runtime-transports`.

## Artifacts produced

None durable. Instructions string is session advertisement. Banner text on stderr.

## Trust / provenance properties

Exposure does not sign anything. Narrow profiles can hide trust tools (`verify`/`attest`/…) from `tools/list` while opt-in consensus can still appear if flagged — asymmetric discoverability.

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Workers missing at resolve | Banner/diagnostics; tools may fail `workers_unavailable` later |
| Bad profile string | Fallback full + warn |
| Smoke vs profile mismatch | **EF-033** false failure |

## Platform differences

Banner/worker path discovery OS-dependent (`OCCAM_HOME` walk). Launcher binary RID selection (`CAP-354` in operator-cli bucket).

## Composition with other capabilities

- **Gates** which PS-1…7 tool families are callable.
- Opt-ins unlock entire PS-7 families.
- Align with `ENTRYPOINT-MODEL.md` fractions (~29% of named entrypoints are core MCP tools).

## Known limitations

- “15 tools” is one slice under full+opt-ins-off+stdio.
- Opt-ins not profile-filtered.
- Crosscheck not in instructions even when enabled.
- Smoke gate profile-blind (**EF-033**).
- Canonical launcher cannot expose non-stdio transports.

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-033** | `hermes-smoke` asserts `EXPECTED_TOOLS=15` ignoring profile/opt-ins |
| **EF-041** | Session DI pool kill (transport/DI interaction) |
| **EF-050** | Unconditional onboard.json env merge on launch |

## Code evidence

- `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs:15-157`
- `src/FFOccamMcp.Core/Transport/OccamToolProfile.cs`, `OccamServerInstructions.cs`
- `docs-audit/PROFILE-TOOL-MATRIX.md`, `NONCORE-SURFACE-MAP.md`, `RUNTIME-MODES.md`
- Deep: `docs-audit/subsystems/runtime-mcp.md`
- Peer: `ENTRYPOINT-MODEL.md`

## Public-doc relevance

**HIGH.** Must teach profiles, opt-in flags, and that tool count is dynamic. Forbidden: equating product to 15 tools only.

## Handbook relevance

**First agent page:** call `occam_client_capabilities`, then use exposed tools; operators set profile/opt-ins before launch.
