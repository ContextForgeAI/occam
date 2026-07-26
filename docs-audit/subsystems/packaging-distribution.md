# S3-12 — Packaging / distribution / shipping boundary

**Agent:** S3-12 (Wave 3 Phase 3B)
**CAP range:** CAP-1020 … CAP-1039 (allocated 1020–1039, full range used)
**SoT:** current executable code + **live** GitHub Releases / npm registry checks (fetched during this audit). Docs untrusted where they conflict with what actually exists on the wire.

---

## AUDIT TARGET

`packages/occam-mcp`, `packages/occam-agent-sdk`, `packages/occam-skill`; `scripts/build-release*`, `scripts/ci-release-build.*`; `Dockerfile` / `docker-compose.yml`; `.github/workflows/{occam-release,sign-release,playbook-marketplace,ci}.yml`. Question answered: **what actually ships vs what only exists in the repo tree**, and which capabilities are reachable from each shipped artifact.

## FILES INSPECTED

- `packages/occam-mcp/{package.json,bin/occam-mcp.js,lib/{index.ts,client.ts,discover-repo.mjs,resolve-host-binary.mjs},test/*.test.js,README.md}`
- `packages/occam-agent-sdk/{package.json,src/{index.ts,client.ts,research.ts,transcode.ts,digest.ts,map.ts}}`
- `packages/occam-skill/{package.json,bin/install.mjs,lib/install.mjs,skill/SKILL.md,skill/.occam_skill_version}`
- `scripts/occam-skill-install.mjs` + `scripts/lib/operator/install-occam-skill.mjs` (operator-CLI duplicate of the npm installer)
- `scripts/sync-occam-skill-package.mjs`
- `scripts/build-release.ps1` / `.sh`, `scripts/build-release-all.sh`, `scripts/lib/build-release.mjs`
- `scripts/lib/host-install-gate.mjs` (imported cross-package by `bin/occam-mcp.js`)
- `Dockerfile`, `docker-compose.yml`
- `.github/workflows/occam-release.yml`, `sign-release.yml`, `playbook-marketplace.yml`, `ci.yml`
- `scripts/occam.mjs`, `scripts/lib/operator/occam-cli-subcommands.mjs`, `scripts/lib/operator/occam-cli-dispatch.mjs`, `scripts/lib/operator/occam-command-registry.mjs` (to trace `occam connect` / `occam contract` reachability post-install)
- Repo root: confirmed **no root `package.json`** — `packages/*` are not an npm workspace of anything
- Live checks (not code, but ground truth for "what ships"): `https://registry.npmjs.org/@ff-occam/{mcp,agent-sdk,skill}` (all **404**), `https://api.github.com/repos/ContextForgeAI/occam/releases` (real `v1.0.0-rc.2` assets: linux-x64, osx-arm64, win-x64 tarball+manifest; **no** `.bundle` cosign signatures present at fetch time; **no** osx-x64 asset)

## EXECUTABLE ENTRYPOINTS

1. `packages/occam-mcp/bin/occam-mcp.js` — npm `bin: occam-mcp` — download-or-delegate MCP host launcher (stdio/WebSocket)
2. `packages/occam-skill/bin/install.mjs` — npm `bin: occam-skill-install`
3. `scripts/occam-skill-install.mjs` — reached via `occam skill install` (operator CLI, **separate implementation**, not the npm bin)
4. `scripts/lib/build-release.mjs` — reached via `build-release.ps1`/`.sh` and `ci-release-build.*` — the actual Level B tarball assembler
5. `Dockerfile` — `docker build` / `docker compose up` — multi-stage AOT + Node + Playwright image, `ENTRYPOINT ["/app/occam"]` (stdio MCP by default)
6. `.github/workflows/occam-release.yml` — tag `v*` → builds + publishes GitHub Release tarballs (linux-x64 on every push/PR/tag; osx-arm64 + win-x64 tag-only)
7. `.github/workflows/sign-release.yml` — `release: published` → cosign keyless sign of the three tarballs
8. `.github/workflows/playbook-marketplace.yml` — PR touching `profiles/playbooks/community/**` → L4 gate + cosign sign of the **playbook**, not the host

---

## CAPABILITIES

### CAP-1020 — `@ff-occam/mcp` npm bin: download-or-delegate host launcher
- **Impl:** `packages/occam-mcp/bin/occam-mcp.js` — resolves RID (`win32/linux/darwin` × `x64/arm64` → 4 RIDs), then either (a) uses `OCCAM_HOME` local binary, (b) auto-detects a git-clone/tarball root via `discoverRepoRoot` and delegates to `scripts/launch-mcp-host.mjs`, or (c) downloads+verifies (sha256) a tarball from `OCCAM_RELEASE_BASE_URL` (default GitHub Releases) into a per-version/per-RID cache dir
- **Reach:** intended for `npx @ff-occam/mcp` / `npm i -g @ff-occam/mcp` — **currently unreachable by any real user** (CAP-1032)
- **Confidence:** PROVEN (code); reachability disproven live

### CAP-1021 — `@ff-occam/mcp` TypeScript client (`OccamMcpClient`)
- **Impl:** `lib/client.ts` — hand-rolled JSON-RPC-over-stdio client (spawn + line-delimited framing), `MCP_PROTOCOL_VERSIONS` negotiation (`2025-11-25` preferred, 3 fallbacks), typed `transcode/probe/digest/map/playbookResolve/playbookHeal/playbookSave/extractKnowledge` methods
- **Note:** **duplicates** the MCP client role that any MCP host (Cursor, Claude, Hermes) already provides natively — this package exists for programmatic Node.js consumers with **no** MCP host in the loop
- **Confidence:** PROVEN

### CAP-1022 — Clone/tarball auto-detect + refuse-on-in-repo-npx guard
- **Impl:** `lib/discover-repo.mjs` (`isOccamRepoRoot` checks for `workers/http-extract/extract.mjs` + `scripts/launch-mcp-host.mjs`) + `rejectInRepoNpmEntry()`/`failCloneWithoutBinary()` in `bin/occam-mcp.js`, using `scripts/lib/host-install-gate.mjs::formatInstallBlockerMessage`
- **Effect:** running `node packages/occam-mcp/bin/occam-mcp.js` from inside a git clone refuses to run (points at `INSTALL.md`) unless `OCCAM_NPX_ON_CLONE=1`
- **Confidence:** PROVEN

### CAP-1023 — `@ff-occam/agent-sdk` high-level recipe wrappers
- **Impl:** `src/{research,transcode,digest,map,client}.ts` — `OccamAgentClient` (extends `OccamMcpClient`) with named recipes: `probeAndTranscode` (Recipe A), `mapAndDigest` (Recipe B), `resolveAndExtract` (Recipe D), `healAndSave` (Recipe E), plus a full `research()` orchestrator (probe → map(optional) → digest → extract-knowledge-if-schema → optional auto-heal/save) and `quickResearch()`
- **Reach:** peer-depends on `@ff-occam/mcp` (exact-pinned `1.0.0-rc.2`) — **also unpublished** (CAP-1032), so unreachable
- **Confidence:** PROVEN

### CAP-1024 — `@ff-occam/skill` portable installer (10 harness targets)
- **Impl:** `packages/occam-skill/lib/install.mjs` — `SKILL_PLATFORMS` = cursor, claude, hermes, copilot, kiro, pi, devin, codex, generic, all; per-platform destination table (`~/.cursor/skills/occam`, `~/.claude/skills/occam`, `~/.hermes/skills/occam`, `~/.copilot/skills/occam`, `<project>/.kiro/skills/occam`, `~/.pi/agent/skills/occam`, `~/.config/devin/skills/occam`, `<project>/.agents/skills/occam` for codex); `writeAgentsMdSection()` injects a marked block into project `AGENTS.md` for the `codex` platform + project scope
- **Skill source resolution:** bundled `packages/occam-skill/skill/` first, else `$OCCAM_HOME/skills/occam`
- **Confidence:** PROVEN

### CAP-1025 — `occam skill install` operator-CLI installer (duplicate implementation)
- **Impl:** `scripts/occam-skill-install.mjs` → `scripts/lib/operator/install-occam-skill.mjs` — **separately reimplements** the same `SKILL_PLATFORMS` table, destination logic and `AGENTS.md` marker injection found in CAP-1024, rather than importing the npm package's `lib/install.mjs`
- **Reach:** this is the version that actually ships and runs (Level B tarball + git clone), since `occam skill install` (via `scripts/lib/operator/occam-cli-subcommands.mjs`) resolves `scripts/occam-skill-install.mjs`, not anything under `packages/`
- **Engineering note:** two independent copies of the same install-destination logic (one npm-published-but-unreachable, one shipped-and-live) — see EF-019 for drift risk
- **Confidence:** PROVEN

### CAP-1026 — Level B release tarball composition (canonical "what ships")
- **Impl:** `scripts/lib/build-release.mjs::stageReleaseTree` — `dotnet publish` the AOT binary, then stages: the binary (`OccamMcp.Core[.exe]`), **all of `workers/`** (minus `node_modules`), an explicit allow-list of `scripts/*` files, **all of `scripts/lib/`** (minus `node_modules`), **all of `profiles/`**, `skills/occam/` (if present), `VERSION`, `release-manifest.json`
- **Explicit `scripts/*` allow-list (verbatim):** `launch-mcp-host.mjs`, `occam.mjs`, `occam`, `occam.ps1`, `install.sh`, `install.ps1`, `get-ff-occam.sh`, `get-ff-occam.ps1`, `occam-doctor.sh`, `occam-doctor.ps1`, `occam-onboard.mjs`, `occam-help.mjs`, `occam-skill-install.mjs`, `sync-occam-skill-package.mjs`, `occam-refresh-host.mjs`, `occam-session.mjs`, `hermes-smoke.mjs`, `occam-wrapper.sh`, `build-release.sh`, `build-release.ps1`
- **Not on the list (confirmed by omission):** `occam-connect.mjs`, `check-public-mcp-contract.mjs`, `verify-install.*`, `print-mcp-snippet.mjs` (standalone; only reachable via `lib/print-mcp-snippet.mjs` which **is** shipped since all of `scripts/lib/` is copied) — see CAP-1035 / EF-020
- **Confidence:** PROVEN (static read of the exact array)

### CAP-1027 — GitHub Releases publish pipeline, asymmetric per-OS trigger
- **Impl:** `.github/workflows/occam-release.yml` — 3 jobs: `release-linux` (every push to `main`, every PR, every tag; publishes to GH Releases **only** on `refs/tags/v*`), `release-macos` (`macos-latest`, **tag-only**, builds `osx-arm64` **only** — no `osx-x64` job exists), `release-windows` (**tag-only or `workflow_dispatch`**, `needs: release-linux` so the GitHub Release object exists first)
- **RID coverage shipped:** `linux-x64`, `osx-arm64`, `win-x64`. **`osx-x64` (Intel Mac) is never built by CI.**
- **Confidence:** PROVEN (workflow text) + confirmed live (v1.0.0-rc.2 release has exactly these 3 tarballs, no osx-x64)

### CAP-1028 — Release signing (cosign keyless, post-publish)
- **Impl:** `.github/workflows/sign-release.yml` — triggers on `release: published` (or manual `workflow_dispatch` with a tag); downloads all `ff-occam-*.tar.gz` from the release (retries up to 18×20s for the multi-job race), `cosign sign-blob --bundle` each, uploads `.bundle` files back onto the same GitHub Release with `--clobber`
- **Live check:** at fetch time, `v1.0.0-rc.2` release assets list has **no** `.bundle` files alongside the 3 tarball+manifest pairs — either the workflow has not yet run/completed for this tag, or it silently did not attach signatures. **Not provable as a bug from code alone** (timing-dependent); flagged as UNCERTAINTY, not EF.
- **Confidence:** PROVEN (workflow); signing outcome for the current release UNCERTAIN

### CAP-1029 — Dockerfile: 3-stage AOT + Node + Playwright image
- **Impl:** `Dockerfile` — Stage 1 (`dotnet/sdk:10.0` + clang/zlib1g-dev) AOT-publishes `linux-x64` only; Stage 2 (`node:20-slim`) `npm ci --omit=dev` for the `workers/` npm workspace; Stage 3 (`dotnet/runtime-deps:10.0`) installs Node 20 via NodeSource, copies binary→`/app/occam`, workers, and `scripts/`, installs Playwright OS deps + Chromium as root, then drops to non-root `occam` user; `HEALTHCHECK` runs `/app/occam --version`; `ENTRYPOINT ["/app/occam"]` (stdio MCP by default)
- **Not copied into the image:** `profiles/`, `skills/occam/` (unlike the Level B tarball in CAP-1026) — a Docker user gets **no** playbooks and **no** portable skill unless they bind-mount them
- **No workflow builds/pushes this image** — no `docker build`/`docker push` step exists anywhere under `.github/workflows/`; this is a **build-it-yourself** artifact, not a published one (no image on Docker Hub / GHCR referenced in code)
- **Confidence:** PROVEN

### CAP-1030 — docker-compose.yml (local dev convenience only)
- **Impl:** single `occam` service, builds the local `Dockerfile`, sets `OCCAM_HOME=/app` + `OCCAM_LOG=info`, `stdin_open`/`tty` for interactive stdio MCP, `restart: unless-stopped`
- **Reach:** local-only; not referenced by any CI workflow or install doc flow inspected
- **Confidence:** PROVEN

### CAP-1031 — Playbook Marketplace CI (community content shipping boundary, distinct from host shipping)
- **Impl:** `.github/workflows/playbook-marketplace.yml` — on PR touching `profiles/playbooks/community/**/*.json{,.sig}`: builds the AOT host from source, runs `dotnet run --project benchmarks/l0-gate -- --genome-pilot --playbook <path>` (L4 gate) per changed file, on pass `cosign sign-blob` the **playbook JSON** (not the host binary), commits the `.sig` back to the PR branch, enables auto-merge; on failure posts a PR comment
- **Distinction from CAP-1028:** this signs *user-contributed content* merged into the repo tree (reachable by any consumer who later resolves that community playbook at runtime via `occam_playbook_resolve`), not a release binary artifact
- **Confidence:** PROVEN

### CAP-1032 — npm packages exist in repo but are **not published** to any registry (repo-only scaffolding)
- **Impl/evidence:** live `GET https://registry.npmjs.org/@ff-occam/{mcp,agent-sdk,skill}` → **404** for all three (fetched during this audit); no `.github/workflows/*.yml` and no `scripts/*` contains `npm publish`, `NPM_TOKEN`, or `registry.npmjs.org` publish calls (grepped repo-wide) — only `sync-occam-skill-package.mjs` (a local file-copy helper wired as `packages/occam-skill`'s `prepublishOnly`, never itself invoked by CI)
- **Repo is honest about this:** `packages/occam-mcp/README.md` explicitly states *"Not the RC install path… `@ff-occam/mcp` / `npx` apply only after a future npm publication"*; `docs/install.md` lists `npx @ff-occam/mcp` under "Wrong — Not part of this RC". This is **not a documentation-drift bug** — it is accurately-labeled aspirational scaffolding.
- **Product implication:** the *entire* `packages/` tree (CAP-1020…1025) is currently **reachable by nobody** in the shipped 1.0.0-rc.2 product. The only real distribution channels are: (a) git clone + `occam-doctor`, (b) Level B GitHub-Releases tarball via `get-ff-occam.sh`/`.ps1`, (c) self-built Docker image.
- **Confidence:** PROVEN (live registry check + repo-wide grep)

### CAP-1033 — `@ff-occam/mcp` published `bin` entrypoint imports outside its own npm `files` set (latent DOA-on-publish)
- **Impl:** `packages/occam-mcp/package.json` → `"files": ["bin/", "lib/"]`. `bin/occam-mcp.js` line 23: `import { formatInstallBlockerMessage } from "../../../scripts/lib/host-install-gate.mjs";` — this resolves to the **repo-root** `scripts/lib/host-install-gate.mjs`, which is **outside** the package's `files` allow-list and therefore **would not be included** in the tarball produced by `npm pack`/`npm publish`
- **Effect if ever published:** any consumer who installs `@ff-occam/mcp` from the npm registry and runs it from **outside** a git clone (i.e. the exact `npx @ff-occam/mcp` distribution path the package exists for) would hit `Cannot find module '.../scripts/lib/host-install-gate.mjs'` inside `rejectInRepoNpmEntry()`, which runs unconditionally near the top of `main()` — **the package would fail on every invocation**, not just the clone-detection branch
- **Why untested:** `packages/occam-mcp`'s own `npm test` (`npm run build && node --test test/*.test.js`) runs from inside the monorepo, where the relative path still resolves on disk regardless of the `files` restriction — `npm pack --dry-run` (the package's own `pack` script) lists files but does not statically verify that every `import`/`require` target is included. CI (`.github/workflows/ci.yml`) never runs any `packages/*` script at all (CAP-1034). This exact anti-pattern is independently documented as a standing rule in this repo's own `CLAUDE.md` ("A publishable npm package must not import outside its `files` set… Vendor shared helpers into `lib/`") — but the rule was not (yet) applied to this file.
- **Fix shape (not applied — audit only):** vendor/copy `host-install-gate.mjs`'s logic into `packages/occam-mcp/lib/`, or move `host-install-gate.mjs` under `packages/occam-mcp/lib/` and have the repo-root scripts import it instead.
- **Confidence:** PROVEN in code (static import-path math); consequence PROVEN in theory (matches the repo's own documented failure mode), moot today only because CAP-1032 means nobody has published or installed it yet
- → **ENGINEERING FINDING EF-019** (see below)

### CAP-1034 — `packages/*` test suites are fully excluded from CI
- **Impl:** repo has **no root `package.json`** (no npm workspaces tying `packages/*` together); `.github/workflows/ci.yml` has exactly 3 jobs (`build`, `gate-fast`, `docs-check`) — none installs, builds, or tests anything under `packages/`. The four test files (`occam-mcp/test/{discover-repo,resolve-host-binary,public-mcp-contract,client}.test.js`, `occam-agent-sdk/test/client.test.js`) only run if a maintainer manually `cd packages/<name> && npm test`
- **Confidence:** PROVEN (workflow enumeration + absence of root package.json)

### CAP-1035 — Two operator CLI subcommands ship a dangling delegate script in the Level B tarball
- **Impl:** `scripts/lib/operator/occam-cli-subcommands.mjs` registers `connect` → `occam-connect.mjs` and `contract`/`version-surface` → `check-public-mcp-contract.mjs`, both resolved at runtime via `resolveScriptPath(occamHome, sub.script)` = `join(occamHome, "scripts", sub.script)`. Neither `occam-connect.mjs` nor `check-public-mcp-contract.mjs` is in the `build-release.mjs` `scriptFiles` allow-list (CAP-1026)
- **Effect in a Level B install:** `occam connect` and `occam contract`/`occam version-surface` fail cleanly (`dispatchSubcommand` checks `existsSync` first and prints `error: missing <path>`, exit 1 — not a crash) but are **silently absent** even though `occam help`/`occam` usage banner (shipped, since `occam-help.mjs` + the full `CLI_SUBCOMMANDS` table ship) still **lists them as available commands**
- **Scope caveat:** the underlying library code for connect (`scripts/lib/operator/connect/*`) **does** ship, since all of `scripts/lib/` is copied — only the top-level entry scripts are missing. A git clone or `install.sh`-based Level A/A′ install is unaffected (full `scripts/` tree present).
- **Confidence:** PROVEN (static: allow-list vs. registry cross-reference)
- → **ENGINEERING FINDING EF-020**

### CAP-1036 — Level B tarball ships the **entire** operator surface, not just the MCP host
- **Impl:** per CAP-1026, the release tarball includes `doctor`, `onboard`, `session`, `skill install` (CAP-1025), `help`, `refresh`/`restart`, `smoke`, `snippet`, `status`, `control` (soft TUI), `update` — i.e. essentially the full `occam <verb>` operator CLI (minus `connect`/`contract`, CAP-1035) — reachable by **any** end user who only intended to install the MCP server, not just git-clone contributors
- **Product implication:** "MCP-only" framing in `docs/` undersells what a Level B install actually contains; a curious end user running `node scripts/occam.mjs` (or the shipped `occam`/`occam.ps1` wrapper) inside their install directory gets the full maintainer-adjacent control surface (S3-07/S3-08/S3-09 territory), not a minimal MCP runtime
- **Confidence:** PROVEN

### CAP-1037 — Bootstrap installers materialize a Level B tree without git or .NET SDK
- **Impl:** `scripts/get-ff-occam.sh` / `.ps1` (shipped inside the tarball itself per CAP-1026, and also the documented one-liner: `curl … | bash` / `irm … | iex`) — these are the actual **zero-prerequisite** entrypoint referenced in the live GitHub Release body (`v1.0.0-rc.2` release notes literally paste these two commands) and in `packages/occam-mcp/README.md`'s "Installation" section
- **Relationship to CAP-1020:** this is the **real** equivalent of what `npx @ff-occam/mcp` was designed to be — same GitHub-Releases tarball target, same sha256-manifest verification pattern — but reachable *today*, unlike CAP-1020/1032
- **Confidence:** PROVEN (cross-referenced against live release body + README); deep install-script internals owned by S3-09, referenced here only for the shipping-boundary comparison

### CAP-1038 — RID coverage gap: npm wrapper advertises `osx-x64` support that CI never builds
- **Impl:** `packages/occam-mcp/bin/occam-mcp.js` `RID_MAP` includes `"darwin-x64": "osx-x64"` and `BINARY_NAMES["osx-x64"]`; `packages/occam-mcp/README.md` lists "macOS x64 (Intel)" under Supported Platforms; `scripts/build-release-all.sh` (local maintainer script) includes `osx-x64` in its RID loop — but `.github/workflows/occam-release.yml`'s `release-macos` job **only** runs `bash ./scripts/ci-release-build.sh osx-arm64` (hardcoded, no matrix)
- **Effect:** even in a hypothetical future where CAP-1032 is resolved and `@ff-occam/mcp` is published, an Intel Mac user running `npx @ff-occam/mcp` would compute `rid = "osx-x64"` and attempt to download `.../v{VERSION}/ff-occam-{VERSION}-osx-x64.tar.gz` from GitHub Releases — an asset that **does not exist** for `v1.0.0-rc.2` (confirmed live: only `osx-arm64` present) — hard download failure, not a graceful degrade
- **Confidence:** PROVEN (workflow read + live release asset list cross-check)
- Folds into **EF-019/EF-020 sibling observation** — not filed as a separate EF since it is currently unreachable (gated behind CAP-1032)

### CAP-1039 — Shipped skill metadata is out of sync with the product version / core tool count
- **Impl:** `packages/occam-skill/skill/.occam_skill_version` = `0.9.1`; `packages/occam-skill/skill/SKILL.md` frontmatter `metadata.version: "0.9.1"` and body text "Smoke check… → **14** `occam_* tools`" and "Full decision guide… Copy-paste flows" sections — while the shipped product version is `1.0.0-rc.2` and the current core registry (`OccamMcpServerRegistration.OccamToolNames`, confirmed Wave 1/2/3-S0) is **15** tools (client_capabilities added since the skill's "14" count was written). `packages/occam-mcp/bin/occam-mcp.js`'s own `--help` text independently prints "MCP TOOLS (14):" with a hand-written list that also omits `occam_client_capabilities`.
- **Reach:** this **does** ship — `occam skill install` (CAP-1025) copies `skills/occam/` (source of truth, same drift) into every harness's skill directory; an agent reading the installed `SKILL.md` gets a stale tool count and no mention of the client-capabilities budget-sizing step
- **Confidence:** PROVEN (direct file read of shipped content)
- → **ENGINEERING FINDING EF-021**

---

## ARTIFACTS CREATED / CONSUMED

| Artifact | Producer | Consumer | Ships? |
|---|---|---|---|
| `artifacts/releases/ff-occam-<ver>-<rid>.tar.gz` + `-manifest.json` | `build-release.mjs` / `ci-release-build.*` | `get-ff-occam.sh/.ps1`, GitHub Releases page | **Yes** — live on `v1.0.0-rc.2` for linux-x64/osx-arm64/win-x64 |
| `<tarball>.bundle` (cosign) | `sign-release.yml` | offline signature verification (not code-inspected further; out of scope) | UNCERTAIN — absent on current release at fetch time |
| `@ff-occam/mcp` / `@ff-occam/agent-sdk` / `@ff-occam/skill` npm tarballs | `npm pack`/`npm publish` (manual only — no CI step) | npm registry | **No** — confirmed 404 |
| Docker image (untagged, local) | `docker build .` | `docker compose run` | **No** — not pushed anywhere by CI |
| `profiles/playbooks/community/*.json.sig` | `playbook-marketplace.yml` | `occam_playbook_resolve` community tier (runtime) | **Yes** — committed back to the repo on PR merge |
| `skills/occam/` (canonical) | maintainer-edited | `sync-occam-skill-package.mjs` → `packages/occam-skill/skill/`; `occam-skill-install.mjs`/`install-occam-skill.mjs` → harness dirs | **Yes**, via the operator CLI path (CAP-1025), **not** via the unpublished npm package |

---

## "INVISIBLE PRODUCT" ANSWER

What an **MCP-only user** (wires the stdio server, calls `occam_*` tools, never opens a shell in the install dir) never sees:

1. **The entire `packages/` tree** (CAP-1020–1025) — none of it is reachable; it is pure repo-only scaffolding today (CAP-1032).
2. **The full operator CLI** (`occam doctor/onboard/session/skill/help/refresh/smoke/snippet/status/control`) that ships alongside the host in every Level B install (CAP-1036) — an MCP-only user's install directory silently contains a maintainer-grade control surface they never invoke.
3. **The Docker packaging path** — exists, builds locally, but is not part of any documented "how do I run this" flow for an MCP client user, and is never published as a pullable image.
4. **The Playbook Marketplace signing pipeline** (CAP-1031) — community playbook trust provenance is invisible unless the user inspects `profiles/playbooks/community/*.sig` directly; `occam_playbook_resolve`'s `provenance: "community"` field is the only runtime signal.
5. **The `connect`/`contract` CLI gap** (CAP-1035) — an MCP-only user who *does* poke at the shipped CLI and tries the two commands most relevant to "is my host correctly wired" (`connect`, `contract`/`version-surface`) hits a silent "missing script" instead of the intended functionality, in a Level B install specifically.

---

## GRAPH EDGES

- `TOOL:occam_playbook_resolve` --USES--> `CAP-1031` (community provenance tier signed by Playbook Marketplace CI)
- `CLI:occam skill install` --USES--> `CAP-1025` (not CAP-1024 — the npm package installer is unreachable)
- `CLI:occam connect` --BROKEN_IN--> `CAP-1026` (Level B tarball) per `CAP-1035`
- `CLI:occam contract`/`version-surface` --BROKEN_IN--> `CAP-1026` per `CAP-1035`
- `PACKAGE:@ff-occam/mcp` --BLOCKED_BY--> `CAP-1032` (unpublished) and `CAP-1033` (would DOA if published)
- `PACKAGE:@ff-occam/agent-sdk` --DEPENDS_ON--> `PACKAGE:@ff-occam/mcp` (peerDependency, exact-pinned) --BLOCKED_BY--> `CAP-1032`
- `SKILL:skills/occam/SKILL.md` (shipped copy) --DRIFTED_FROM--> `CAP-007` (Wave-1/S0 core tool registry, 15 tools) per `CAP-1039`
- `WORKFLOW:occam-release.yml` --PRODUCES--> `CAP-1026` artifacts consumed by `CAP-1037` (get-ff-occam bootstrap) and `CAP-1020` (npm wrapper's download path, currently unreachable)
- `WORKFLOW:sign-release.yml` --SIGNS--> artifacts produced by `occam-release.yml` (cross-workflow dependency, event-triggered not `needs:`-linked)
- `Dockerfile` --REUSES--> same `src/FFOccamMcp.Core` + `workers/` sources as `CAP-1026`, independently staged (no shared staging code between Docker and tarball paths)

---

## HIDDEN / ADVANCED

- Level B tarball's full operator CLI surface (CAP-1036) is not marketed as part of "installing the MCP server" in the public docs skimmed for this audit (deep doc-accuracy pass is out of scope for S3-12; flagged for the docs owner).
- `occam skill install` and the npm `@ff-occam/skill` package are two independent code paths for the same feature (CAP-1024 vs CAP-1025); only one is live.
- `sync-occam-skill-package.mjs` ships inside the Level B tarball (in the `scriptFiles` allow-list) despite being a maintainer-only dev tool with no end-user use case — harmless but unnecessary tarball weight.
- `profiles/` (all playbooks, including community ones with `.sig` files) ship in full inside the Level B tarball but **not** inside the Docker image.

---

## ENGINEERING FINDINGS (candidates for ENGINEERING-FINDINGS.md, EF-019+)

| ID | Class | Related CAPs | Summary | Confidence | Needs repro? | Security review? |
|----|-------|--------------|---------|------------|--------------|-------------------|
| EF-019 | BUG-CANDIDATE | CAP-1033, CAP-1032 | `packages/occam-mcp/bin/occam-mcp.js` imports `../../../scripts/lib/host-install-gate.mjs`, which is outside the package's own `"files": ["bin/", "lib/"]` allow-list in `package.json`. If `@ff-occam/mcp` is ever `npm publish`ed as-is, every invocation of the published `occam-mcp` bin fails immediately at `rejectInRepoNpmEntry()` (module-not-found) — the package would be DOA on first real-world use outside the monorepo, exactly the failure mode this repo's own `CLAUDE.md` warns about in the abstract. Currently non-impacting only because the package is unpublished (CAP-1032) and CI never exercises `packages/*` (CAP-1034). | PROVEN in code | No (static import-path math is conclusive; a `npm pack` + isolated install would confirm) | No | OPEN |
| EF-020 | BUG-CANDIDATE | CAP-1035, CAP-1026 | `build-release.mjs::stageReleaseTree`'s `scriptFiles` allow-list omits `occam-connect.mjs` and `check-public-mcp-contract.mjs`, while `scripts/lib/operator/occam-cli-subcommands.mjs` (which **does** ship, via the full `scripts/lib/` copy) still registers `connect` and `contract`/`version-surface` as valid subcommands and `occam-help.mjs`/the usage banner still advertises them. In a Level B tarball install, `occam connect` and `occam contract`/`occam version-surface` fail with `error: missing <path>` (handled gracefully, exit 1, no crash) despite appearing in `occam --help`. | PROVEN in code (allow-list vs. registry cross-reference) | Yes (trivial: install a Level B tarball, run `occam connect`) | No | OPEN |
| EF-021 | OBSERVATION | CAP-1039 | Shipped skill metadata drift: `skills/occam/.occam_skill_version` / `SKILL.md` frontmatter pin `version: "0.9.1"` and hardcode "14 `occam_*` tools" in body prose, while the product is `1.0.0-rc.2` with a 15-tool core registry (client_capabilities added later). `packages/occam-mcp/bin/occam-mcp.js --help` independently hardcodes a different stale 14-tool list. Both are shipped (skill via `occam skill install`; help text via any install). No functional break — misleads an agent reading the skill card about the tool inventory and misses the client-capabilities budget step. | PROVEN (direct read of shipped file content) | No | No | OPEN |

*(Appended to `ENGINEERING-FINDINGS.md` ledger as part of this report; see that file for the canonical numbered table.)*

---

## CONFIG / ENV (this subsystem)

`OCCAM_RELEASE_BASE_URL` (npm wrapper download base override), `OCCAM_HOME` (skip-download / clone-detection), `OCCAM_NPX_ON_CLONE` (bypass the in-repo-npx guard), `OCCAM_RELEASE_VERSION` / `OCCAM_RELEASE_OUTPUT_DIR` (CI build-release env surface). No new env vars introduced beyond what S3-07/S3-09 already catalog.

## FAILURES

`occam-mcp.js`: unsupported-platform hard exit(1) with a short message (not a typed `failure.code` — this is pre-MCP-protocol, plain CLI text); download failures print actionable next steps (local-tree hint vs. clone/tarball hint) then exit(1); sha256 mismatch throws and is caught into the same generic download-failure branch. Operator CLI dispatch (`occam <sub>`): missing-script → `error: missing <path>`, exit 1 (no typed code, no JSON shape even under `--json` for this specific pre-dispatch failure — worth a note for S3-07's CLI-surface report, not fixed here).

## SECURITY / TRUST

- npm-path binary download verifies sha256 against a manifest fetched over HTTPS from the same release base URL (no separate out-of-band trust anchor beyond TLS + GitHub Releases integrity) — same trust model as `get-ff-occam.sh`/`.ps1` (S3-09 territory).
- Cosign keyless signing (CAP-1028 host tarballs, CAP-1031 community playbooks) relies on GitHub OIDC (`id-token: write`) — standard supply-chain pattern, not independently re-verified in this pass.
- Docker image drops to non-root `occam` user after Playwright/Chromium install as root (CAP-1029) — good practice, correctly ordered (`USER occam` after the root-only `apt`/`playwright install-deps` steps).

## TEST EVIDENCE

`packages/occam-mcp/test/*.test.js` (4 files) and `packages/occam-agent-sdk/test/client.test.js` exist and are plausible unit/contract tests, but per CAP-1034 **none run in CI** — their pass/fail status is unverified by this audit (out of scope to execute product code per Wave 3 rules; static existence confirmed only).

## UNCERTAINTIES

- Whether `sign-release.yml` actually completed for `v1.0.0-rc.2` (bundles absent at fetch time — could be pending, failed silently, or the release predates the workflow's current form). Not filed as an EF because the signing workflow's *code* is correct and reachable; only the *live outcome* for one specific release is unverified.
- Whether `packages/*` `npm test` suites currently pass if run manually (not executed, per audit-only constraint).
- Whether any **private/internal** registry publish happens outside this repo's CI (the READMEs explicitly leave room for "private registry build" — cannot be disproven from this repo alone).
- Exact interaction between `docker-compose.yml`'s `OCCAM_HOME=/app` and the `profiles/`/`skills/` gap noted in CAP-1029 (i.e., whether any documented compose override mounts them) — not found in the files inspected, but a docs-side compose override could exist unlisted.

## COMPLETENESS VERDICT

**COMPLETE** for the assigned boundary (packages/occam-mcp, occam-agent-sdk, occam-skill; build-release scripts; Dockerfile/compose; the 4 shipping-relevant workflows) cross-checked against live GitHub Releases and npm registry state. Deep internals of `scripts/lib/operator/connect/*` (S3-10), full CLI verb catalog (S3-07), and install-script bootstrap mechanics (S3-09) are intentionally not re-derived here — only referenced where they intersect the shipping boundary.
