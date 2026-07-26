# Install and onboarding

**Slug:** `install-onboarding` · **Product system:** PS-9 Operator surface · **CAPs:** 40 · **Public relevance:** HIGH

**Member CAPs:** CAP-940…979  
**Product capability:** CAP-940  
**Engineering findings:** EF-028, EF-029, EF-030, EF-050

## What it is

End-to-end **bootstrap, diagnose, configure, and verify** path that turns a machine into a runnable Occam install. Two layers:

1. **Doctor** (`occam-doctor.ps1`/`.sh`) — CAP-940…959: npm workers, Playwright, RID, optional AOT publish, selftests, hand-off banner.
2. **Install / onboard** — CAP-960…979: Level A clone+build, Level B tarball, `get-ff-occam` one-liners, `occam-onboard.mjs`, `verify-install.mjs`, launch-time consumer of onboard state.

Doctor is invoked by every install path; onboard writes `~/.occam/onboard.json` and optionally host MCP config.

## Why it exists

Production install must work without asking users to manually `dotnet publish` + wire MCP JSON. Level B removes SDK requirement; doctor repairs/bootstraps workers and browser.

## User-visible entrypoints

| Entrypoint | Level | Evidence |
|------------|-------|----------|
| `scripts/install.ps1` / `install.sh` | A (clone) or B (`-FromUrl`) | CAP-960, CAP-961 |
| `get-ff-occam.ps1` / `.sh` | B one-liner | CAP-963, CAP-971 |
| `occam doctor` | Repair / build | CAP-940; CAP-955 Level B skip-build |
| `occam onboard` (`settings`) | Config wizard | CAP-967 |
| `verify-install.mjs` | Post-install checks | CAP-966 |
| `launch-mcp-host.mjs` | Consumes onboard env | **EF-050** |
| `verify-install.ps1` | **Static command reference**, not executable step | CAP-979 |

## Core behavior

### Doctor pipeline (CAP-940…958)

Typical sequence (`subsystems/doctor.md`):

1. Resolve/stamp `OCCAM_HOME` (CAP-941).
2. Guard net10.0 TFM (CAP-942).
3. `npm` workers workspace (CAP-943).
4. Playwright skip/decision + cache diagnostic (CAP-944, CAP-945).
5. Linux-root `install-deps chromium` (**bash only** — CAP-946 asymmetry).
6. Advisory selftests: egress (conditional), PDF, SSRF (CAP-947…949).
7. Chromium launch-probe with one-shot auto-install retry (CAP-950).
8. Community playbook manifest sha256 (CAP-951; also CAP-977 unconditional).
9. RID resolution SoT (CAP-952); Windows publish-lock advisory (CAP-953); MSVC auto-load (CAP-954).
10. Unless `--skip-build` / Level B: `dotnet publish` + root binary copy (CAP-955, CAP-956).
11. `assert-host-binary.mjs` (CAP-957); completion banner + onboard hand-off (CAP-958).

`hermes-smoke.mjs` is a related fidelity smoke (CAP-959) — profile-blind **EF-033**.

### Level A vs B (CAP-960…962)

| | Level A | Level B |
|--|---------|---------|
| Trigger | `-RepoUrl` / clone | `-FromUrl` / `OCCAM_RELEASE_URL` / get-ff-occam |
| Needs | git, Node 20+, .NET 10 SDK | Node 20+ (+ tar/sha tools for bootstrap) |
| Doctor | Full build | `--skip-build` unconditional |
| Extract | git checkout | `release-install.mjs` download+sha256+extract |

HTTPS enforced unless `OCCAM_RELEASE_ALLOW_HTTP=1` (warn). Manifest requires `sha256`, `version`, `rid`.

### Destructive extract (CAP-965; **EF-028**)

`extractTarball` does `rmSync(installDir, {recursive:true, force:true})` **then** extract. **No rollback.** VERSION mismatch check runs *after* destroy. Non-git existing dir on Level A → refuse (never silent overwrite).

### Onboard (CAP-967…970)

- Interactive wizard vs `--non-interactive` vs `--skip`.
- `--write-config`: opt-in, confirmed, **merge-only** host-config mutation (CAP-968).
- Schema versioning via `onboard-schema.mjs` (CAP-970).
- **EF-029:** writes `onboard.json` / optional mcp.json **before** verify completes (CAP-969 order gap).

### Launch merge (**EF-050**)

`launch-mcp-host.mjs` always `mergeOnboardEnv` from `~/.occam/onboard.json` into child env — uncontrolled config surface every launch (GAP-034).

## Advanced behavior

| Behavior | CAP / EF |
|----------|----------|
| Duplicate download/verify in get-ff-occam vs release-install | CAP-963 |
| Inconsistent CI/non-interactive detection across five entrypoints | CAP-964 |
| No PATH persistence on any OS | CAP-975 |
| Eager `-ForcePlaywright` | CAP-976 |
| Community manifest verify inside every doctor | CAP-977 |
| Stale “14 tools” / source-uri copy | CAP-978; **EF-030** |
| Binary discovery forked not shared | CAP-974 |
| Host install gate messaging | CAP-973 |

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| Level B doctor skip-build via CLI | CAP-935 / CAP-955 |
| get-ff-occam → doctor → verify → onboard → **connect** | CAP-971 |
| Onboard env reinjected every MCP launch | **EF-050** |
| `rm -rf` install dir before extract | **EF-028** |

## Parameters

Install scripts: `-RepoUrl`/`--repo-url`, `-Ref`, `-FromUrl`/`--from-url`, `-InstallDir`, `-SkipBuild`, `-SkipVerify`, `-ForcePlaywright`, env `OCCAM_REPO_URL`, `OCCAM_REF`, `OCCAM_BRANCH`, `OCCAM_RELEASE_URL`, `OCCAM_RELEASE_ALLOW_HTTP`.  
Onboard: `--non-interactive`, `--skip`, `--write-config`, schema fields in onboard.json.  
Doctor: `--skip-build`, force-playwright flags.

## Configuration

| Path / env | Role |
|------------|------|
| `OCCAM_HOME` | Install root |
| `OCCAM_CONFIG` / `~/.occam/onboard.json` | Onboard schema (ART-029; ST-22) |
| Playwright cache envs | Browser bits outside `.occam` (ST-21) |

## Backends

Doctor may provision Playwright Chromium. Install does not run extract backends except via smoke/verify.

## Sessions / state

| State | Risk |
|-------|------|
| Install tree | Destructive replace on Level B (**EF-028**) |
| onboard.json | May hold env map secrets; merged at launch (**EF-050**) |
| Optional mcp.json via `--write-config` | Merge-only; backups via connect family when used |
| No PATH | Session-only shell hints (CAP-975) |

## Network behavior

Downloads release tarball + manifest (HTTPS). Doctor may hit npm/Playwright CDN. Community manifest integrity is local sha256 of shipped files (CAP-951).

## Artifacts produced

| Artifact | Notes |
|----------|-------|
| AOT/host binary at install root | CAP-956/957 |
| workers node_modules | CAP-943 |
| onboard.json | CAP-970 |
| VERSION file | Level B |
| `*.occam-bak` | Only if write-config/connect path |

## Trust / provenance properties

- Level B: sha256(manifest) before extract — **not** cosign (EF-053 unused by install).
- Community playbook sha256 gate in doctor (CAP-951).
- Stale install copy understates tool count (**EF-030**) — honesty gap in operator messaging.

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Preflight missing tool | exit 1 with specific message (CAP-972) |
| Non-git dir on Level A | Refuse |
| Sha256 mismatch | Abort before extract (tarball) / fail after destroy if VERSION drift (CAP-962) |
| Verify after onboard write | Config may exist even if verify fails (**EF-029**) |
| No automatic rollback of rmSync | **EF-028** |

## Platform differences

| Item | Difference |
|------|------------|
| Doctor scripts | `.ps1` vs `.sh` |
| Linux root chromium deps | bash doctor only (CAP-946) |
| MSVC / publish-lock | Windows-only (CAP-953/954) |
| Bootstrap | `irm \| iex` vs `curl \| bash` |
| tar/sha tools | Win `tar.exe`; Unix `tar` + `sha256sum`/`shasum` |

## Composition with other capabilities

- Hands off to **host-connectors** (`occam connect`) after get-ff-occam.
- Produces tree consumed by **packaging-distribution** Level B composition inverse.
- Launch path ties to **runtime-transports** (stdio-only launcher) + **EF-050**.

## Known limitations

- Destructive Level B extract without rollback.
- Onboard-before-verify order.
- Unconditional onboard env merge at launch.
- PATH never persisted.
- Duplicate bootstrap crypto paths can drift (CAP-963).
- `verify-install.ps1` is documentation-shaped, not a step (CAP-979).

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-028** | `rm -rf` INSTALL_DIR before extract — no rollback |
| **EF-029** | Onboard writes config before verify |
| **EF-030** | Stale install copy (source-uri + “14 tools”) |
| **EF-050** | `launch-mcp-host` always merges `~/.occam/onboard.json` env |

## Code evidence

- `scripts/occam-doctor.ps1`, `occam-doctor.sh`
- `scripts/install.ps1`, `install.sh`, `get-ff-occam.ps1`, `get-ff-occam.sh`
- `scripts/lib/release-install.mjs`, `install-preflight.mjs`, `verify-install.mjs`
- `scripts/occam-onboard.mjs`, `scripts/lib/operator/onboard-*.mjs`
- `scripts/launch-mcp-host.mjs`
- Deep: `docs-audit/subsystems/doctor.md`, `subsystems/install-onboard.md`
- Peer: `STATE-MODEL.md` ST-22/26, `ENTRYPOINT-MODEL.md`

## Public-doc relevance

**HIGH.** Must be honest about: destructive extract, backups/rollback limits, onboard write order, launch env merge, no PATH, Level A vs B prerequisites.

## Handbook relevance

**Operator install chapter.** Lead with danger callouts (EF-028/029/050), then Level A/B matrix, then doctor repair, then onboard → connect hand-off.
