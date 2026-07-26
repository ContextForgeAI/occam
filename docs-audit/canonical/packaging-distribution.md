# Packaging and distribution

**Slug:** `packaging-distribution` · **Product system:** PS-9 Operator surface · **CAPs:** 20 · **Public relevance:** HIGH

**Member CAPs:** CAP-1020…1039  
**Product capability:** CAP-1020  
**Engineering findings:** EF-034, EF-035, EF-036, EF-051, EF-052, EF-053  
**Artifacts:** ART-038 (cosign `.bundle` — produced, unused by install)

## What it is

How Occam **ships**: Level B release tarballs (canonical installable tree), GitHub Releases CI, Docker image, npm package scaffolding, skill installers, and community playbook marketplace CI. `SHIPPED-CODE-MAP.md` governs “ships vs repo-only.”

## Why it exists

Distribute an AOT host + workers + operator scripts without requiring every user to clone and SDK-build (Level B). Secondary surfaces (npm, Docker, skill) broaden embedding — several are **scaffolded but unpublished or broken**.

## User-visible entrypoints

| Entrypoint | Reachability | CAP |
|------------|--------------|-----|
| GitHub Release tarball + `get-ff-occam` / `install -FromUrl` | **Live** (linux-x64, osx-arm64, win-x64) | CAP-1026, CAP-1027, CAP-1037 |
| `Dockerfile` ENTRYPOINT `/app/occam` | Buildable | CAP-1029; **EF-051** HEALTHCHECK |
| `docker-compose.yml` | Local dev convenience | CAP-1030 |
| `npx @ff-occam/mcp` | **Unpublished** (registry 404) | CAP-1020, CAP-1032 |
| `@ff-occam/agent-sdk` / `@ff-occam/skill` npm | Unpublished | CAP-1023, CAP-1024, CAP-1032 |
| `occam skill install` | **Ships** in tarball/clone | CAP-1025 |
| `.github/workflows/occam-release.yml` | Tag `v*` | CAP-1027 |
| `sign-release.yml` | Cosign keyless | CAP-1028; **EF-053** |
| `playbook-marketplace.yml` | Community PB PRs | CAP-1031; **EF-052** |

## Core behavior

### Level B tarball composition (CAP-1026) — SoT for “what ships”

`scripts/lib/build-release.mjs::stageReleaseTree`:

- AOT `OccamMcp.Core[.exe]`
- All `workers/` (minus node_modules)
- Allow-listed `scripts/*` + **all** `scripts/lib/`
- All `profiles/`, optional `skills/occam/`, `VERSION`, `release-manifest.json`

Allow-list includes doctor/onboard/session/refresh/skill/smoke/wrapper — **entire operator surface**, not host-only (`CAP-1036`).

**Omitted from allow-list:** `occam-connect.mjs`, `check-public-mcp-contract.mjs`, standalone `verify-install.*` — help may still advertise connect/contract (**EF-035**; CAP-1035).

### Release CI asymmetry (CAP-1027)

linux-x64 on push/PR/tag; osx-arm64 + win-x64 **tag-only**. npm wrapper advertises `osx-x64` but CI never builds it (`CAP-1038`).

### npm bin behavior (CAP-1020…1022) — code exists, publish does not

Resolve RID → local `OCCAM_HOME` / discover clone / download tarball from `OCCAM_RELEASE_BASE_URL`. Refuse in-repo npx unless `OCCAM_NPX_ON_CLONE=1`.  
**EF-034 / CAP-1033:** published `files` set would omit imports outside package → **DOA if published**.

### Skill install (CAP-1024 / CAP-1025)

Two implementations: npm package vs operator `occam skill install` (live). Platforms: cursor, claude, hermes, copilot, kiro, pi, devin, codex, generic, all.  
**EF-036 / CAP-1039:** skill metadata version 0.9.1 + “14 tools” vs product 1.0.0-rc.2 / 15 tools.

## Advanced behavior

| Behavior | CAP / EF |
|----------|----------|
| Agent-sdk recipe wrappers | CAP-1023 (unpublished) |
| Cosign release bundles | CAP-1028; install never verifies (**EF-053**) |
| Marketplace L4 `skipped` counts success + recursive trigger | **EF-052** |
| packages/* tests excluded from CI | CAP-1034 |
| Bootstrap materializes Level B without git/SDK | CAP-1037 |

## Automatic / silent behavior

| Silent | Effect |
|--------|--------|
| Tag publish builds multi-RID tarballs | CAP-1027 |
| sign-release on `release: published` | May produce `.bundle` unused by install |
| Marketplace auto-merge path | Can land unvalidated community PB (**EF-052**) |
| Docker HEALTHCHECK | Perpetual unhealthy (**EF-051**) |

## Parameters

Build: `scripts/build-release.ps1 -Version X.Y.Z` / `.sh` / `build-release.mjs`.  
Docker: standard build args per Dockerfile.  
npm: `OCCAM_RELEASE_BASE_URL`, `OCCAM_HOME`, `OCCAM_NPX_ON_CLONE`.  
Skill: platform flags in installers.

## Configuration

Release base URL / repo URL must stay **public + reachable** or env-overridden (publishable hygiene). No private forge defaults.

## Backends

Image bundles Node + Playwright for extract workers. Packaging itself does not select http/browser policy.

## Sessions / state

| State | Notes |
|-------|-------|
| Per-version RID download cache (npm path) | When/if published |
| Skill trees under `~/.cursor/skills/occam` etc. | Destructive reinstall (`rmSync`) |
| GH Release assets | Portable ART |
| Cosign `.bundle` | ART-038; trust theater without consumer (**EF-053**) |

## Network behavior

CI pulls SDKs/npm; users download tarballs over HTTPS; marketplace workflow hits repo contents. Cosign keyless needs OIDC (`id-token`) — misconfigured per EF-053.

## Artifacts produced

| Artifact | Consumer |
|----------|----------|
| `*-win-x64.tar.gz` + manifest | Level B install (sha256) |
| Docker image | Compose / run |
| Skill files | Agent harnesses |
| Cosign `.bundle` | **None in install path** |
| Community playbook merges | Runtime resolve tier |

## Trust / provenance properties

- **Real:** tarball sha256 vs manifest at install.
- **Theater:** cosign step present but unused/misconfigured (**EF-053**); marketplace can auto-merge without solid gate (**EF-052**).
- Skill/version drift mis-teaches tool count (**EF-036**).
- Align with `TRUST-MODEL.md` / `ARTIFACT-ONTOLOGY.md`: do not claim install verifies cosign.

## Failure / fallback behavior

| Case | Behavior |
|------|----------|
| Docker HEALTHCHECK `occam --version` | Unknown args ignored → stdio block → unhealthy (**EF-051**) |
| npm publish as-is | Broken imports (**EF-034**) |
| Level B user runs `occam connect` | Missing script (**EF-035**) |
| Marketplace skipped L4 | Treated success → merge risk (**EF-052**) |

## Platform differences

| RID | CI |
|-----|-----|
| linux-x64 | Every push/PR/tag |
| osx-arm64, win-x64 | Tag-only |
| osx-x64 | Advertised in npm code; **not built** (CAP-1038) |

Docker is linux-oriented multi-stage AOT. Compose is dev-only.

## Composition with other capabilities

- Feeds **install-onboarding** Level B.
- Tarball ships **operator-cli** subset but drops connect/contract scripts.
- Docker ENTRYPOINT is stdio host — related **runtime-transports**; HEALTHCHECK broken.
- Marketplace feeds **playbooks** community tier (PS-5) with supply-chain risk.

## Known limitations

- npm packages exist in repo but are not on registry.
- Cosign unused by install.
- HEALTHCHECK broken by construction.
- Marketplace validation gaps.
- Skill metadata stale.
- osx-x64 gap.
- Connect/contract dangling after Level B extract.

## Engineering findings

| ID | Finding (not a feature) |
|----|-------------------------|
| **EF-034** | `@ff-occam/mcp` bin imports outside npm `files` — DOA if published |
| **EF-035** | Level B omits `occam-connect.mjs` / `check-public-mcp-contract.mjs` while help advertises them |
| **EF-036** | Skill card version/tool-count drift |
| **EF-051** | Docker HEALTHCHECK uses non-verb `--version` → perpetual unhealthy |
| **EF-052** | Marketplace auto-merge without solid validation |
| **EF-053** | Cosign unused / misconfigured — trust theater |

## Code evidence

- `scripts/lib/build-release.mjs`, `scripts/build-release.ps1`
- `packages/occam-mcp/bin/occam-mcp.js`, `packages/occam-agent-sdk/**`, `packages/occam-skill/**`
- `scripts/occam-skill-install.mjs`, `Dockerfile`, `docker-compose.yml`
- `.github/workflows/occam-release.yml`, `sign-release.yml`, `playbook-marketplace.yml`
- Deep: `docs-audit/subsystems/packaging-distribution.md`, `SHIPPED-CODE-MAP.md`
- Peer: `ARTIFACT-ONTOLOGY.md` ART-038, `ENTRYPOINT-MODEL.md`

## Public-doc relevance

**HIGH** for “how to get Occam.” Lead with GitHub tarball / get-ff-occam. Label npm/Docker caveats. Never claim cosign-verified installs or marketplace as fully gated.

## Handbook relevance

**Distribution chapter:** what ships matrix; RID table; operator-surface-in-tarball; explicit non-goals (unpublished npm, broken HEALTHCHECK, EF-052/053).
