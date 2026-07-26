# Operator CLI

**Slug:** `operator-cli` · **Product system:** PS-9 Operator surface · **CAPs:** 77 · **Public relevance:** HIGH

**Member CAPs:** CAP-350…395 (host env/config knobs), CAP-880…885 (session operator), CAP-900…904 (host offline verbs / verify surface), CAP-920…939 (unified `occam` CLI)  
**Product capability:** CAP-920  
**Engineering findings:** EF-022, EF-023, EF-025, EF-049

## What it is

The **human/operator control plane** outside MCP tool calls: three distinct surfaces that must not be conflated (`CLI-SURFACE.md` §1):

| Surface | Entry | Scope |
|---------|-------|-------|
| **A — Host offline verbs** | `OccamMcp.Core` / AOT binary | `keys export`, `verify`, `install-browser`, `version-surface`, `lifecycle` |
| **B — Unified operator CLI** | `scripts/occam.mjs` → `occam <sub>` | 13 names (+ aliases) |
| **C — Help registry** | `COMMAND_REGISTRY` | Documents B + maintainer-only rows |

Plus the large **env/config knob set** (CAP-350…395) that operators set to shape host behavior — audited under this family as the operator-reachable configuration surface (`subsystems/config-env.md`).

## Why it exists

Install, diagnose, connect, refresh, session-manage, and inspect without going through an MCP client. Agents rarely see Surface A/B; operators live here (`ENTRYPOINT-MODEL.md` PS-9).

## User-visible entrypoints

| Entrypoint | Evidence |
|------------|----------|
| `node scripts/occam.mjs <sub>` / `occam` / `occam.ps1` | CAP-923; `occam-cli-subcommands.mjs` |
| Bare `occam` on TTY | Auto `control` if interactive & not CI (`CAP-934`) |
| Host binary verbs | `OccamCliVerbs.TryRun` before transport (`CAP-002`/`CAP-900`) |
| `occam-playbook-publish` | Maintainer CLI, not MCP (`CAP-932`) |

## Core behavior

### Group 1 — Lifecycle & health

| Command | Delegate | Behavior | CAP |
|---------|----------|----------|-----|
| `occam doctor` | shell `occam-doctor.*` | Preflight + optional publish | CAP-935 injects `--skip-build` on Level B |
| `occam smoke` | `hermes-smoke.mjs` | Stdio MCP fidelity | EF-033 (expects 15) |
| `occam status` | internal | Version + onboard + update; `--json` | CAP-925 |
| `occam update` | internal | Read-only GitHub Releases check | CAP-926 |
| `occam refresh` / `restart` | `occam-refresh-host.mjs` | Stop hosts → doctor → manual reload hint | CAP-928 |
| `occam control` | TTY menu | Maps keys to onboard/doctor/update/help/refresh/smoke/status | CAP-924 |

**EF-049:** refresh/`stop-occam-processes` kill by **binary name machine-wide**, ignoring `OCCAM_HOME` (Win Name-eq; POSIX `mentionsHost` bypass). Not scoped to this install.

### Group 2 — Configure & connect

| Command | Behavior | CAP |
|---------|----------|-----|
| `occam onboard` (`settings`) | Wizard / non-interactive onboard.json | → install-onboarding family |
| `occam connect` | Host MCP config mutation | → host-connectors |
| `occam snippet` | Print MCP snippet; auto-appends `occamHome` | CAP-930 |
| `occam skill` | Skill install delegate | CAP-931 |

### Group 3 — Session profiles (operator)

`occam session` → `occam-session.mjs` (import/export-state/…). Bifurcated session tiers documented in CAP-880…885 (`session-lifecycle.md`): headers-only vs full storageState; export-state browser hint is sole surfacing of backend dependency (`CAP-885`). Same sessions-root default as MCP host (`CAP-884`).

### Group 4 — Contract & diagnostics

| Command | Behavior | CAP / EF |
|---------|----------|----------|
| `occam contract` (`version-surface`) | Full public-contract pipeline (fingerprint, optional `--ws`, `--invoke-smoke`) | CAP-929 |
| Host `version-surface` | Thin assembly metadata only | CAP-920 / **EF-023** name collision |
| `occam help` / `help next-steps` | Registry-driven catalog | CAP-927, CAP-936, CAP-937 |
| Global `--json` | Reshapes stdout for Surface B | CAP-933 |

### Group 5 — Host offline verbs (Surface A)

| Verb | Behavior | CAP |
|------|----------|-----|
| `install-browser` | Playwright chromium into cache unless system browser configured | CAP-900, CAP-901 |
| `verify --mode …` | Offline receipt/citation/manifest/history | CAP-902 notes wrapper gap |
| `keys export` | Public PEM | |
| `lifecycle self` / `diagnose` | INV-10 identity; diagnose takes caller-supplied peers — **never scans/kills by name** | CAP-921, CAP-922 |

**EF-025:** operator wrapper has **no route** to `verify` / `keys` / `install-browser` / `lifecycle` — must invoke host binary directly (`CAP-902`).

### Group 6 — Host env/config knobs (CAP-350…395)

Operators shape the host via environment (see `ENVIRONMENT-VARIABLES.md`, `CONFIG-NEGATIVE-SPACE.md`). Condensed groups:

| Cluster | Examples | CAP span |
|---------|----------|----------|
| Home / workers / Node | `OCCAM_HOME`, worker path overrides, heap | 350–352 |
| Launcher / identity | Force dotnet run, process stamp, onboard merge | 353–355; **EF-050** |
| Sessions / headers / SSRF | sessions root, headers file, private URL block | 356–358 |
| Browser / pool / timeouts | channel, autoinstall, daemon, concurrency, timeouts | 359–366 |
| HTTP daemon / body caps / robots | size caps, PDF, politeness | 367–370 |
| Cache / receipts / playbooks | cache dir, receipts, TSA, playbook paths, genome fetch | 371–375 |
| Search / managed / translate / proxy | providers, rotation, digest parallel | 376–382 |
| Opt-in MCP / profile / client / batch / watch | `OCCAM_*_MCP`, `OCCAM_PROFILE`, client tokens, batch, watch path | 383–387 |
| Remote TLS/JWT/limits | remote transport | 388–390 |
| Banner / log / telemetry / internals | banner, log, cost rate, feature plumbing | 391–395 |

These knobs are **configuration**, not subcommands — listed here because the canonical family membership places them under `operator-cli`.

## Advanced behavior

| Behavior | Evidence |
|----------|----------|
| Dual-name resolution (id/alias/path-suffix) | CAP-937 |
| Level B auto `--skip-build` for doctor | CAP-935 |
| Stale “9 tools” string in refresh copy | CAP-938; **EF-022** |
| `version-surface` name collision A vs B | CAP-939; **EF-023** |
| Per-call GUID headers temp-file vs pool warm | CAP-881; EF-039 (related) |
| Anonymous pool cookie bleed framing | CAP-882 |
| Merkle NUL in public doc prose | CAP-904 (doc hygiene) |
| Stale `FFOccamMcp.Core` name in docs | CAP-903 |

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| Bare `occam` → control on TTY | CAP-934 |
| Refresh machine-wide kill | **EF-049** |
| Launcher merges onboard.json | **EF-050** (launch path) |
| `update` never installs | CAP-926 print-only |

## Parameters

Surface B: global `--json`, `-h/--help`; per-command args passed through to delegates.  
Surface A: verb-specific (`verify --mode`, `lifecycle diagnose --peers`, `keys export --keys-root`).  
Env knobs: defaults and clamps in `ENVIRONMENT-VARIABLES.md` (SoT = code sites cited there).

## Configuration

See Group 6. Critical operator safety knobs: `OCCAM_HOME`, `OCCAM_CONFIG` / onboard path, `OCCAM_SESSIONS_ROOT`, opt-in MCP flags, `OCCAM_PROFILE`.

## Backends

CLI does not extract pages (except indirectly via smoke/contract invoke). `install-browser` provisions Playwright Chromium.

## Sessions / state

| State | Notes |
|-------|-------|
| Session JSON / storageState | Operator `occam session`; ST-01…03 |
| Onboard / connect-last | Written by onboard/connect |
| Kill targets | OS process table; **EF-049** |

`lifecycle` verbs are read-only for identity — they do **not** perform the dangerous kill path (`CAP-922`).

## Network behavior

`update` hits GitHub Releases API (overridable). `contract`/`smoke` spawn local MCP. `connect` may call host CLIs. Refresh doctor may download browser bits.

## Artifacts produced

Snippets, status JSON, contract fingerprint checks, session files, stderr diagnostics. No page receipts from CLI itself (except verify consuming portable artifacts).

## Trust / provenance properties

- `verify` / `keys` are trust-adjacent offline tools (Surface A).
- Refresh kill is an **availability/security** concern (**EF-049**), not a trust proof.
- Help registry may advertise commands missing from Level B tarball (→ packaging EF-035).

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Unknown subcommand | Usage + exit 1 |
| Non-TTY control | Hard error unless `--json` |
| Wrapper missing host verbs | Operator confusion (**EF-025**) |
| Refresh kill collateral | Other installs’ hosts die (**EF-049**) |

## Platform differences

| Area | Difference |
|------|------------|
| Doctor shell | `.ps1` vs `.sh`; Linux-root `install-deps` bash-only (install family) |
| Process stop | Win vs POSIX name matching (**EF-049**) |
| Wrapper | `occam-wrapper.sh` POSIX; Windows hosts spawn node directly |

## Composition with other capabilities

- Delegates into **install-onboarding**, **host-connectors**, packaging skill install.
- Surfaces A trust verbs compose with PS-6 artifacts.
- Does not replace MCP tool surface — parallel L0/L8 (`PRODUCT-ARCHITECTURE.md`).

## Known limitations

- Three CLI surfaces with overlapping names.
- Wrapper gap for host verbs (**EF-025**).
- Refresh unsafe across multi-install machines (**EF-049**).
- CAP-350…395 membership makes this family also the “env encyclopedia” — split carefully in handbook.
- Stale copy in refresh (**EF-022**).

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-022** | Refresh hardcodes stale tool-count copy |
| **EF-023** | `version-surface` names two non-equivalent commands |
| **EF-025** | Wrapper does not route install-browser/verify/keys/lifecycle |
| **EF-049** | Refresh/stop kills by binary name machine-wide, ignores `OCCAM_HOME` |

## Code evidence

- `scripts/occam.mjs`, `scripts/lib/operator/occam-cli-subcommands.mjs`, `occam-cli-dispatch.mjs`, `occam-command-registry.mjs`, `control-loop.mjs`, `update-check.mjs`
- `scripts/occam-refresh-host.mjs`, `scripts/lib/stop-occam-processes.mjs`
- `src/FFOccamMcp.Core/Cli/OccamCliVerbs.cs`, `Program.cs`
- `docs-audit/CLI-SURFACE.md`, `subsystems/config-env.md`, `subsystems/session-lifecycle.md`, `subsystems/verify-cli.md`
- Peer: `ENTRYPOINT-MODEL.md`, `STATE-MODEL.md` ST-29

## Public-doc relevance

**HIGH** for operator journey. Separate “MCP tools” from “occam CLI.” Disclose refresh kill scope and host-verb wrapper gap.

## Handbook relevance

**Operator handbook spine.** Structure chapters as Groups 1–5 above; put env knobs in a configuration appendix cross-linked from PS-8.
