# SHIPPED-CODE-MAP (Wave 4 Phase 4A)

**SoT:** current executable code. Docs UNTRUSTED. This map establishes the *actual shipped executable boundary* before negative-space discovery.

## Build/ship facts (code-proven)

- **Solution** `FFOccamMcp.slnx`: only `src/FFOccamMcp.Core` + `benchmarks/l0-gate` + `benchmarks/l0-ram-stress`. `benchmarks/rc2-regression` and `packages/*` are **not** in the solution.
- **Core csproj** has **no `<Compile Remove>`** → the default SDK glob compiles **every** `src/FFOccamMcp.Core/**/*.cs` (309 files) into the shipped AOT binary `OccamMcp.Core`. Bench/legacy/dead C# types (`PlannerBench`, `CodecBench`, `TableSemanticMaterializer`, `Knowledge/Legacy/*`, `Knowledge/Canonical/*`) **ship in the binary** even when unreachable at runtime — "dead" ≠ "not shipped."
- **`OCCAM_GATE`** conditional-compilation symbol is defined only when `OccamGateBuild=true` (the gate bench). Code under `#if OCCAM_GATE` is **not** in the production binary — must be checked per file.
- **AOT / trim / InvariantGlobalization / source-gen JSON** → reflection-off; every serialized type needs a `JsonSerializerContext`. Behavior differences vs. a JIT build are possible (trimming, culture-invariant).
- **Docker image** ships: AOT binary (`/app/occam`) + `workers/` + `scripts/`. Does **NOT** ship `profiles/`, `skills/`, `corpora/`, `packages/`, `benchmarks/`.
- **Level B tarball** (per S3-12) ships a wider `scripts/*` allow-list but omits `occam-connect.mjs` + `check-public-mcp-contract.mjs` (EF-035).
- **npm `@ff-occam/*`** (packages/) confirmed unpublished (S3-12). Repo-only scaffolding.

## Classification by area

| Area | Classification | Ships where | Notes |
|------|----------------|-------------|-------|
| `src/FFOccamMcp.Core/**/*.cs` (309) | **SHIPPED_RUNTIME** | AOT binary, Docker, tarball | Whole glob compiled; incl. dead types |
| `workers/http-extract/*.mjs`, `browser-extract/*.mjs`, `css-extract/*.mjs`, `shared/lib/*.mjs`, `shared/plugins/*.mjs` | **SHIPPED_RUNTIME** | Docker `COPY workers/`, tarball | Spawned by host |
| `workers/**/*.selftest.mjs` (18) | **SHIPPED_OPTIONAL** | ships in `workers/` tree | Some invoked by `occam doctor` (egress/pdf/private-ip); others test-only but still copied |
| `workers/browser-extract/lib/recipes/*.mjs` (7) | **SHIPPED_RUNTIME** | Docker/tarball | Per-host browser recipes, registry-driven |
| `scripts/*.mjs/.ps1/.sh` + `scripts/lib/**` (157) | **SHIPPED_OPERATOR** | Docker `COPY scripts/`, tarball | doctor/onboard/connect/session/verify/install/refresh/skill |
| `scripts/bench/**` (24) | **BENCHMARK_ONLY** | not shipped in Docker (subset only) | dev/QA |
| `scripts/templates/*` | **SHIPPED_OPERATOR** | data asset | connect/host template |
| `packages/occam-mcp` | **SHIPPED_PACKAGE** (unpublished) | npm (404) | bin launcher, DOA-if-published (EF-034) |
| `packages/occam-agent-sdk` | **SHIPPED_PACKAGE** (unpublished) | npm (404) | TS client |
| `packages/occam-skill` | **SHIPPED_PACKAGE** (unpublished) | npm (404) + `occam skill install` | skill card |
| `benchmarks/l0-gate` | **BENCHMARK_ONLY** | not shipped | gate runner; `InternalsVisibleTo L0Gate` |
| `benchmarks/l0-ram-stress`, `rc2-regression` | **BENCHMARK_ONLY / DEV_ONLY** | not shipped | rc2-regression not even in slnx |
| `profiles/playbooks/seeds/**` | **SHIPPED_OPTIONAL** (data) | tarball, not Docker | playbook seeds |
| `corpora/**` | **TEST_ONLY / DATA** | not shipped | gate corpora + prompts |
| `skills/occam/**` | **SHIPPED_OPTIONAL** (data) | via `occam skill install` | stale version (EF-036) |
| `.github/workflows/**` (8) | **BUILD_TIME_ONLY** | CI | release/sign/marketplace/gate |
| `Dockerfile`, `docker-compose.yml` | **SHIPPED_INSTALLER** | self-built image | never pushed by CI |
| `docs/`, `*.md`, `llms.txt`, `mkdocs.yml` | **DOC (frozen)** | site | out of scope |

## Executable entrypoints (discovered)

1. `Program.Main` → `OccamCliVerbs.TryRun` (offline verbs) **or** `OccamMcpCli.Parse` → transport (stdio/WS/Remote/BatchServer)
2. Node workers: `extract.mjs`, `http-daemon.mjs`, `browser-extract.mjs`, `browser-daemon.mjs`, `dom-skeleton-capture.mjs`, `css-extract.mjs`
3. Worker selftests invoked by doctor: `egress-proxy/pdf-extract/private-ip.selftest.mjs`
4. Operator scripts: `occam.mjs` (+ ps1/sh wrapper), `occam-doctor.*`, `occam-onboard.mjs`, `occam-connect.mjs`, `occam-session.mjs`, `occam-refresh-host.mjs`, `occam-skill-install.mjs`, `occam-playbook-publish.mjs`, `get-ff-occam.*`, `install.*`, `verify-install.*`, `launch-mcp-host.mjs`, `hermes-smoke.mjs`, `check-public-mcp-contract.mjs`
5. npm bins: `packages/occam-mcp/bin/occam-mcp.js`, `packages/occam-skill/bin/install.mjs`
6. CI workflow entrypoints (release/sign/marketplace)
7. Dockerfile `ENTRYPOINT ["/app/occam"]`

## Coverage-model caveat for Wave 4

Waves 1–3 are a *coverage model*, not SoT. Wave 4 tries to **prove them incomplete**. The 309-file whole-glob compile means any C# file with externally-meaningful behavior that lacks a CAP is a candidate gap, even if Waves 1–3 called its subsystem "done."
