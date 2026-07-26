# WAVE 3 REPORT

**WAVE:** 3  
**STATUS:** COMPLETE  
**SoT:** current executable code (docs UNTRUSTED)  
**Scope:** second surface outside the 15 core MCP tools — opt-ins, BatchServer, CLI, doctor, install, connect, packaging, runtime modes, session lifecycle  
**STOP:** no Wave 4 / no public doc rewrite in this wave

## Agents (12/12)

| ID | Area | Agent | Report |
|----|------|-------|--------|
| S3-01 | Batch / BatchServer | [S3-01 Batch](16993bfe-4e4b-4a22-b82d-b49d25a846a9) | `subsystems/batch-batchserver.md` |
| S3-02 | Watch | [S3-02 Watch](dbb68b64-c223-4f5b-a0c7-5d92094a8f64) | `subsystems/watch.md` |
| S3-03 | Consensus | [S3-03 Consensus](caee025b-7fc5-4cb4-9ef3-3b4532e6b61c) | `subsystems/consensus-crosscheck.md` |
| S3-04 | Failure atlas | [S3-04 Atlas](6982eb2a-cd31-403f-9d01-b391375d3e3a) | `subsystems/failure-atlas.md` |
| S3-05 | Session | [S3-05 Session](9ecf69af-92db-406f-8022-8ad6ebb19c11) | `SESSION-LIFECYCLE.md` + `subsystems/session-lifecycle.md` |
| S3-06 | Verify CLI | [S3-06 Verify CLI](f09d23de-00ce-43dc-b713-97b622ec5992) | `subsystems/verify-cli.md` |
| S3-07 | CLI surface | [S3-07 CLI](185ea7d1-d555-4445-84c9-4c114cf08d4b) | `CLI-SURFACE.md` |
| S3-08 | Doctor | [S3-08 Doctor](c13eb4ab-2de8-4ce5-a456-e1a80abab5c3) | `subsystems/doctor.md` |
| S3-09 | Install/onboard | [S3-09 Install](00b04b90-c43e-4785-a9d6-471c9171c54a) | `subsystems/install-onboard.md` |
| S3-10 | Connect | [S3-10 Connect](48e2f387-956d-4faf-a6d9-a7e2efcc74a2) | `CONNECT-PLATFORM.md` + `HOST-CAPABILITY-MATRIX.md` |
| S3-11 | Runtime modes | [S3-11 Runtime](e5a7d6d4-f3a3-45dc-b43e-56187ed2c2f8) | `RUNTIME-MODES.md` |
| S3-12 | Packaging | [S3-12 Packaging](f8d5e7ef-5a4e-48fc-9c6b-2456cced6dec) | `subsystems/packaging-distribution.md` |

Phase 3A map: `NONCORE-SURFACE-MAP.md`.

## Capabilities

| | |
|--|--|
| before (end Wave 2) | 490 |
| after | **674** |
| wave-3 rows | **184** (includes remints CAP-1040/1041) |
| machine inventory | `capabilities.json` (`wave: 3`) |

### CAP remints (orchestrator)

| Collision | Remint | Reason |
|-----------|--------|--------|
| CAP-995 `roo` (dup with `cline`) | CAP-1040 | HOST matrix duplicate ID |
| CAP-999 `junie` (dup with `goose`) | CAP-1041 | HOST matrix duplicate ID |

### EF ledger

Canonical renumber EF-019…040 in `ENGINEERING-FINDINGS.md` (agents collided on EF-019/020/021).

**EF-024 WITHDRAWN:** S3-04 multi-tenant atlas leak — contradicted by per-WebSocket-session `Host.CreateApplicationBuilder()` + `AddOccamMcpServer()` (CAP-1000 / `RemoteMcpTransport.RunSingleSessionAsync`).

## Product surface (compressed)

Occam’s **second surface** is larger than “opt-in MCP tools”:

1. **Opt-in MCP** — batch trio, watch, crosscheck, failure_atlas — env-gated, **not** profile-filtered.
2. **BatchServer** — distinct HTTP job API sharing batch engine; no Receipt v1; persistent markdown in `jobs.json` (EF-037/038).
3. **Operator CLI** — `occam <sub>` dispatcher + host binary verbs; wrapper does **not** expose `verify`/`keys`/`install-browser` (EF-025).
4. **Doctor / smoke** — publish + chromium + selftests; hermes-smoke hard-codes 15 tools (EF-033).
5. **Install / onboard** — Level A/B + get-ff-occam; destructive extract; onboard writes before verify (EF-028/029).
6. **Connect** — 15 host adapters; CONFIG_FILE post-verify rollback dead when `requiresRestart:true` (EF-021).
7. **Packaging** — real ship = GitHub Release Level B tarballs (+ local Docker); npm `@ff-occam/*` **unpublished** (404); DOA bin if published (EF-034); tarball missing connect/contract entry scripts (EF-035).
8. **Runtime** — `launch-mcp-host.mjs` is stdio-only for connect; per-session DI under WS/remote.
9. **Session** — three tiers of `session_profile` fidelity; pool warm-reuse broken for headered calls (EF-039); anonymous cookie bleed refined (EF-040).

## Most important hidden / advanced findings

1. npm packages are scaffolding only — shipping path is GH Releases Level B (+ self-built Docker).
2. Batch MCP and BatchServer share engine but multiply store instances → last-writer-wins (EF-038).
3. Connect rollback safety net is dead for the common CONFIG_FILE/`requiresRestart` path (EF-021).
4. Operator `occam` wrapper vs host verbs split brain (EF-025).
5. Session param text implies Tier-1 parity; heal/extract are Tier-3 (CAP-880).
6. Watch has no un-watch; multi-process store wipe risk (EF-019/020).
7. Level B tarball advertises `connect`/`contract` without packaging their entry scripts (EF-035).

## Artifacts added (Wave 3)

| ID | Name | Notes |
|----|------|-------|
| ART-027 | Batch job snapshot (`jobs.json`) | Full markdown retained; no eviction |
| ART-028 | Watch store (`watch.json` / `OCCAM_WATCH_DB_PATH`) | SI-05 history; no Remove API |
| ART-029 | Onboard state (`~/.occam/onboard.json`) | Written before verify |
| ART-030 | Connect last-run (`~/.occam/connect-last.json`) | Best-effort |
| ART-031 | Host MCP config mutations + `*.occam-bak` | CONFIG_FILE adapters |
| ART-032 | Level B release tarball + manifest | Real published artifact |
| ART-033 | Skill card / `.occam_skill_version` | Stale 0.9.1 / 14-tool copy |

## Code-derived workflows added

| ID | Flow |
|----|------|
| FLOW-012 | Batch submit → status → results (MCP or BatchServer HTTP) |
| FLOW-013 | Watch poll loop → verify mode=history |
| FLOW-014 | Crosscheck multi-vantage → consensus verdict |
| FLOW-015 | Doctor → verify-install → onboard → connect |
| FLOW-016 | Level B get-ff-occam → doctor --skip-build → connect |
| FLOW-017 | Session init/export-state → Tier-1 tool with `session_profile` |
| FLOW-018 | `occam contract` / version-surface public MCP fingerprint |

## Completeness

- Phase 3A map + 12 Phase 3B reports on disk.
- Inventory + EF ledger consolidated; atlas contradiction resolved.
- CAP-995/999 collisions reminted.
- **Out of scope still:** Wave 4 negative space, public `docs/` rewrite, product fixes.

## Envelope (Wave 3)

```
WAVE: 3
STATUS: COMPLETE
CAPABILITIES_BEFORE: 490
CAPABILITIES_AFTER: 674
NEW_CAPS: 184
EF_CANONICAL: EF-019..040 (EF-024 WITHDRAWN)
PRIMARY_ARTIFACTS: NONCORE-SURFACE-MAP, SESSION-LIFECYCLE, CLI-SURFACE, CONNECT-PLATFORM,
  HOST-CAPABILITY-MATRIX, RUNTIME-MODES, WAVE3-REPORT, ENGINEERING-FINDINGS,
  capabilities.json, subsystems/{batch,watch,consensus,atlas,session,verify,doctor,install,packaging}
NEXT: STOP — await GO for Wave 4 (negative space) or DOC-GAP
```
