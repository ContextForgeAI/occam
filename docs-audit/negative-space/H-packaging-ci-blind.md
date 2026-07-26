# W4-H — Adversarial negative-space audit: packages / Docker / CI workflows / shipped-boundary + bench reachability

**Owner:** W4-H. **SoT:** current shipped executable code. Prior `docs-audit/*` (esp. Wave 3 S3-12 `packaging-distribution.md`) treated as UNTRUSTED until §2.

Scope read blind, then compared. S3-12 is unusually complete for this boundary; this report concentrates on **what it missed** — chiefly the CI workflow behavior (Docker healthcheck, playbook-marketplace signing/auto-merge, cosign consumption) and package edge-cases (arm64 RID, dead deps, skill installer branches).

---

## 1. Blind inventory (independent, code-first)

### 1A. `packages/occam-mcp` (npm launcher `@ff-occam/mcp`)
- **B1** `bin/occam-mcp.js` — download-or-delegate host launcher. RID resolution `getRid()` maps only `win32-x64`, `linux-x64`, `darwin-x64`→`osx-x64`, `darwin-arm64`→`osx-arm64` (`bin/occam-mcp.js:50-55`). Any other `${platform}-${arch}` → `console.error("Unsupported platform/arch")` + `process.exit(1)` (`:67-71`).
- **B2** Top-level import `import { formatInstallBlockerMessage } from "../../../scripts/lib/host-install-gate.mjs";` (`bin/occam-mcp.js:23`) — resolves to **repo-root** `scripts/lib/host-install-gate.mjs`, **outside** `package.json` `"files": ["bin/","lib/"]` (`package.json:28-31`). Executes at module load.
- **B3** Host resolution order (`resolveHost`, `:281-299`): local install roots (`findInstallRoots`) → local AOT binary via `resolveHostBinary` → `scripts/launch-mcp-host.mjs` launcher (which then `failCloneWithoutBinary`) → `ensureBinary` (download).
- **B4** `ensureBinary` (`:192-275`): downloads `${RELEASE_BASE_URL}/v${VERSION}/ff-occam-${VERSION}-${rid}.tar.gz` + `-manifest.json`, verifies **sha256 against the downloaded (unsigned) manifest** (`:237-241`), extracts with `tar strip:1`, chmod 0755 (non-win). No cosign / signature check anywhere.
- **B5** `getCacheDir` (`:79-89`): platform-specific cache — `%LOCALAPPDATA%/ff-occam` (win) or `~/.cache/ff-occam`. `OCCAM_HOME` overrides.
- **B6** `rejectInRepoNpmEntry` (`:169-184`) — refuses in-repo npx unless `OCCAM_NPX_ON_CLONE=1`; uses B2's import.
- **B7** `printHelp` (`:301-340`): banner says "15 MCP tools" in description but help footer hardcodes **"MCP TOOLS (14):"** and lists 14, omitting `occam_client_capabilities`.
- **B8** `lib/client.ts` — hand-rolled JSON-RPC stdio client, protocol negotiation (`2025-11-25` + 3 fallbacks), process-tree kill (`taskkill /T /F` on win, `kill(-pid)` on posix), typed tool methods. `lib/resolve-host-binary.mjs::resolveRid` supports `win-arm64`/`osx-arm64`/`linux-arm64` — **broader** than B1's `getRid()`.
- **B9** `package.json` advertises `os: [win32, linux, darwin]`, `cpu: [x64, arm64]` (`:41-49`) and dep `tar@^7.4.3`.

### 1B. `packages/occam-agent-sdk` (`@ff-occam/agent-sdk`)
- **B10** `src/client.ts` `OccamAgentClient extends OccamMcpClient` (imported from `@ff-occam/mcp`) with recipes `probeAndTranscode`/`mapAndDigest`/`resolveAndExtract`/`healAndSave`; `createAgentClient` uses `Object.setPrototypeOf` on a started base client (`src/client.ts:176`).
- **B11** `package.json`: `peerDependencies: { "@ff-occam/mcp": "1.0.0-rc.2" }` (exact pin), `dependencies: { "@modelcontextprotocol/sdk": "^1.0.0" }`, devDep `@ff-occam/mcp: file:../occam-mcp`.
- **B12** `@modelcontextprotocol/sdk` is **never imported** in any `src/*.ts` (grep: only appears in package.json). Dead runtime dependency.
- **B13** `research()` orchestrator (`src/research.ts`): probe → optional map → digest → per-item playbookResolve→extractKnowledge → optional auto-heal/save; `finally { client.stop() }`.

### 1C. `packages/occam-skill` (`@ff-occam/skill`)
- **B14** `lib/install.mjs`: `SKILL_PLATFORMS` = cursor, claude, hermes, copilot, kiro, pi, devin, codex, generic, all. `resolveInstallDestinations` for `platform==="all"` enumerates **cursor, claude, hermes, copilot, kiro, pi, devin — NOT codex** (`lib/install.mjs:59-62`). `generic` has **no switch case** → empty unless `--target` given (`:71-112`, default break).
- **B15** `installOccamSkill`: for each dest, if exists → `fs.rmSync(dest, {recursive,force})` then `copyTree` (`:193-197`) — silent destructive overwrite. Reported `version` read from `.occam_skill_version` (`:171-173`).
- **B16** `packages/occam-skill/skill/.occam_skill_version` = **`0.9.1`** while `package.json version` = `1.0.0-rc.2`. Installer reports v0.9.1.
- **B17** `writeAgentsMdSection` fires for `(platform==="codex" || platform==="all") && scope==="project"` (`:200-207`) and injects a marked block referencing `.agents/skills/occam/SKILL.md` — the codex dir that B14 did **not** create under `all`.
- **B18** `scripts/sync-occam-skill-package.mjs` (prepublishOnly) just copies `skills/occam` → `packages/occam-skill/skill` verbatim — carries the stale `.occam_skill_version` along; never bumps it.

### 1D. Docker
- **B19** `Dockerfile` 3-stage: build (AOT `linux-x64` only) → workers (`npm ci --omit=dev`, playwright is a **prod** dep so it survives) → runtime (`runtime-deps:10.0` + NodeSource Node20). Copies binary→`/app/occam`, `workers/`, `scripts/`. **Does NOT copy `profiles/`, `skills/`, `corpora/`.** `OCCAM_HOME=/app`.
- **B20** `HEALTHCHECK ... CMD /app/occam --version` (`Dockerfile:75-76`). **The host has no `--version` flag** — CLI verbs are `keys/verify/install-browser/version-surface/lifecycle` (`Cli/OccamCliVerbs.cs:36-55`); `OccamMcpCli.Parse` recognizes only `--help/--mcp-server/--remote/--batch-server/--port/--bind/--tls-*/--jwt-*` and **silently ignores unknown args** (no failure set → `IsValid=true`, `Transport/OccamMcpCli.cs:53-160`). `--version` therefore starts the **stdio transport**, which blocks on stdin indefinitely → the 5s healthcheck **times out every cycle** → container reported perpetually **unhealthy** (and leaks a short-lived host per 30s probe).
- **B21** `.dockerignore` excludes `*.md`, `**/bin`, `**/publish`, etc. `docker-compose.yml`: single `occam` service, `stdin_open`/`tty`, `restart: unless-stopped`, commented-out `OCCAM_ALLOW_PRIVATE_URLS`/`OCCAM_HTTP_PROXY`.
- **B22** Host reads at runtime, relative to OCCAM_HOME: `profiles/playbooks/seeds/` + `.../community/` (`Playbooks/PlaybookSeedResolver.cs:334-338`), `profiles/tiers/domain-tier.v1.json` (`Routing/DomainTierRegistry.cs:312`), `profiles/occam-fetch-defaults.json` (`Session/OccamFetchDefaults.cs:25`) → **all missing in Docker** (B19). Seed/community playbook tiers, domain-tier registry, and fetch-defaults fall back to built-ins.

### 1E. CI workflows (`.github/workflows/`)
- **B23** `occam-release.yml`: `release-linux` (push/PR/tag; publishes only on tag) builds **linux-x64**; `release-macos` (**tag-only**) builds **osx-arm64 only**; `release-windows` (tag/dispatch, `needs: release-linux`) builds **win-x64**. Shipped RIDs = {linux-x64, osx-arm64, win-x64}. **No osx-x64, no *-arm64 for win/linux.**
- **B24** No workflow anywhere runs `npm publish` / references `NPM_TOKEN` (grep of `.github`: 0 matches). The three `@ff-occam/*` packages are never auto-published.
- **B25** `sign-release.yml` (`release: published` / dispatch): downloads `ff-occam-*.tar.gz`, `cosign sign-blob --yes --bundle f.bundle` (keyless, `id-token: write`), uploads `.bundle` (`--clobber`). Signs **only the three tarballs** (`select_release_archives` hardcodes linux-x64/osx-arm64/win-x64) — **not** manifests, **not** npm packages.
- **B26** `playbook-marketplace.yml`: trigger `pull_request` on `profiles/playbooks/community/**/*.json{,.sig}` (recursive `**`); diff detection uses `git diff ... -- 'profiles/playbooks/community/*.json'` (**single-level**, `.yml:74`). On L4 gate pass: `cosign sign-blob --blob=P --output=P.sig --yes` with env `COSIGN_PRIVATE_KEY`/`COSIGN_PASSWORD` but **no `--key` flag** and job perms lack `id-token: write` (`:16-19`). Commits `.sig` back + `git push`. Separate **`auto-merge`** job: `if needs.validate.result == 'success'` → `gh pr merge --auto --squash`.
- **B27** `l4-gate` step: when `PLAYBOOKS` empty → `result=skipped`, `exit 0` (job **succeeds**). Combined with B26's recursive-trigger / single-level-diff mismatch and `.sig`-only edits, a PR can trigger the workflow, find **no** `*.json` to validate, skip the gate, and still satisfy `validate.result == 'success'` → **auto-merge of unvalidated community content**.
- **B28** `ci.yml`: build + NuGet/npm vuln scan + AOT publish + `gate-fast` (spawns published host, live tools/list). **No `packages/*` job.** `agent-mvp-gate.yml`: doctor + hermes + popular-hosts. `nightly-gate.yml`: full L0–L8 on cron. `eval-harness.yml`: corpus matrix (l0-smoke/l4-genome/quality-audit-rotation) with accuracy/latency thresholds; `compare` job is an **echo-only no-op** (`:198-201`). `docs.yml`: check-docs + mkdocs strict + **deploys GitHub Pages** (shipped artifact). `playbook-marketplace.yml` `Update Leaderboard` step is an **echo-only no-op** (`:128-137`).

### 1F. `scripts/bench/**`
- **B29** Standalone benchmark/eval harness (own `package.json`, sole dep `tiktoken`; sweep/summarize/extract-bench/hallucination-bench/tool-selection-ab/leak-probe/resource-profile/capture-proxy/crawl4ai compare, `correctness-bench.py`). **Not** referenced by any shipped launcher, **not** in `build-release.mjs` `scriptFiles` allow-list, **not** copied by Docker, **not** published. Referenced only by docs-audit/CHANGELOG/corpora. → **BENCHMARK_ONLY / not a product surface.**

---

## 2. Gap classification (vs CAPABILITY-INVENTORY, capabilities.json, NONCORE-SURFACE-MAP, ARTIFACT-MAP, subsystems/packaging-distribution.md = S3-12)

S3-12 (CAP-1020…1039, EF-019/020/021) already covers a large share of this boundary. Classifications below are relative to it.

**Already covered (no new claim):**
- **B2 DOA cross-boundary import** → `COVERED_EXACTLY` (CAP-1033 / EF-019 = query's EF-034).
- **B23 osx-x64 never built** → `COVERED_EXACTLY` (CAP-1038).
- **B24 npm unpublished** → `COVERED_EXACTLY` (CAP-1032).
- **B7 bin help "14 tools"** + **B16 skill `.occam_skill_version` 0.9.1 drift** → `COVERED_EXACTLY` (CAP-1039 / EF-021 = query's EF-036).
- **B19/B22 Docker omits profiles/skills** → `COVERED_PARTIALLY` (CAP-1029 noted the omission but not the runtime consequences in B22, nor B20).
- **B26 auto-merge exists / community cosign** → `COVERED_PARTIALLY` (CAP-1031 described the happy path only).
- **B8 client duplicates host-native MCP** → `COVERED_EXACTLY` (CAP-1021).

**NEW — missed by S3-12:**

- **[MISSING_FAILURE_SEMANTIC] EFC-H-1 — Docker HEALTHCHECK is permanently red.** `Dockerfile:76` `CMD /app/occam --version`; `--version` is not a CLI flag/verb (`Transport/OccamMcpCli.cs:34-160`, `Cli/OccamCliVerbs.cs:36-55`). Unknown args are silently dropped → stdio transport starts and blocks → 5s timeout → `unhealthy` every cycle. S3-12 recited the healthcheck line as fact (CAP-1029) but did not test the flag. Correct verb would be `version-surface`.
- **[MISSING_FAILURE_SEMANTIC] EFC-H-2 — playbook-marketplace signing step is misconfigured.** `playbook-marketplace.yml:100-110` calls `cosign sign-blob` with `COSIGN_PRIVATE_KEY`/`COSIGN_PASSWORD` in env but **no `--key env://COSIGN_PRIVATE_KEY`** (so cosign attempts **keyless**), while the job grants only `contents/checks/pull-requests: write` — **no `id-token: write`** → keyless OIDC unavailable → sign step fails (or, if it fell through to key mode, the key is never referenced). The community-playbook `.sig` provenance chain (CAP-1031) is thus likely non-functional as written. S3-12 assumed it works.
- **[MISSING_SECURITY_SEMANTIC] EFC-H-3 — auto-merge can fire without validation.** `l4-gate` returns success on `result=skipped` when the diff yields no playbooks (`:83-86`), and the `auto-merge` job triggers on `needs.validate.result == 'success'` (`:142-147`). Combined with EFC-H-4, a community PR can auto-`gh pr merge --squash` into `main` **without the L4 gate ever running**. This is an unvalidated-community-content supply-chain hole feeding `occam_playbook_resolve`'s `community` tier.
- **[MISSING_EDGE] EFC-H-4 — trigger/diff glob mismatch.** Workflow triggers on recursive `profiles/playbooks/community/**/*.json` (`:6`) but detects changed files with single-level `-- 'profiles/playbooks/community/*.json'` (`:74`). A nested community playbook (`community/<sub>/pb.json`) or a `.sig`-only edit triggers the run but is **invisible to the validator** → skipped gate (→ EFC-H-3).
- **[MISSING_EDGE / PLATFORM] EFC-H-5 — npm launcher rejects win-arm64 & linux-arm64 it advertises.** `package.json` declares `cpu:[x64,arm64]`, `os:[win32,linux,darwin]` (`:41-49`) and `resolve-host-binary.mjs::resolveRid` handles `win-arm64`/`linux-arm64`, but `bin/occam-mcp.js` `RID_MAP` (`:50-55`) omits both → those platforms hit `exit(1) "Unsupported platform/arch"`. S3-12's CAP-1038 covered only osx-x64 (which is in RID_MAP but unbuilt); the arm64-Windows/Linux gap is the inverse (advertised + code-capable in one module, hard-rejected in the entrypoint).
- **[MISSING_SECURITY_SEMANTIC] EFC-H-6 — cosign bundles are consumed by no shipped install path.** The npm launcher verifies **sha256 vs the unsigned downloaded manifest** (`bin/occam-mcp.js:237-241`); `get-ff-occam.sh` does the same (`:205-220`) — neither fetches or verifies the `.bundle`. So `sign-release.yml`'s keyless signatures (B25) are never checked by the real distribution channels (npx / bootstrap). The signing pipeline is trust-theater for installs; only a manual third-party `cosign verify-blob` would use them. S3-12 flagged bundles *absent live* but not that *nothing consumes them by design*.
- **[DEAD_CODE_MISTAKEN_AS_PRODUCT] EFC-H-7 — agent-sdk ships an unused dependency.** `@modelcontextprotocol/sdk@^1.0.0` is a declared runtime dep (`package.json:63`) but imported nowhere in `src/` (B12). A published SDK would pull the full MCP SDK for zero use.
- **[MISSING_EDGE] EFC-H-8 — skill `--platform all` skips codex yet writes its AGENTS.md pointer.** `resolveInstallDestinations` excludes codex from `all` (`lib/install.mjs:59-62`) but `installOccamSkill` writes the AGENTS.md section for `all`+project (`:200-207`) referencing `.agents/skills/occam/SKILL.md` — a file it never created. Also `--platform generic` without `--target` → empty destinations → `{ok:false}` (dead branch).
- **[MISSING_SECURITY_SEMANTIC / AUTOMATIC_SILENT] EFC-H-9 — skill installer wipes existing target without confirmation.** `installOccamSkill` `fs.rmSync(dest,{recursive,force})` before copy (`lib/install.mjs:193-196`) — a user-customized `~/.cursor/skills/occam` (or any dest) is silently destroyed. No prompt, no `--force` gate, not in `--dry-run`-only.
- **[DEAD_CODE_MISTAKEN_AS_PRODUCT] EFC-H-10 — workflow scaffolding no-ops shipped as pipeline stages.** `eval-harness.yml` `compare` job (`:198-201`) and `playbook-marketplace.yml` `Update Leaderboard` (`:128-137`) are echo-only placeholders presented as real jobs/steps.
- **[PRODUCT_MISTAKEN_AS_INTERNAL — correction upheld] agent-sdk IS a real product surface, but currently unreachable.** `@ff-occam/agent-sdk` (recipes A/B/D/E + `research()`) is genuine consumer-facing product code, not scaffolding — but its exact-pinned peer on unpublished `@ff-occam/mcp` (B11) + that package's DOA import (B2) mean it is unusable via npm today; usable only from a monorepo checkout with `OCCAM_HOME` set. Matches S3-12's reachability conclusion; recorded as the deliberate shipped-vs-internal boundary.
- **[shipped-boundary correction upheld] `scripts/bench/**` = BENCHMARK_ONLY.** No shipped/reachable path imports it; excluded from tarball/Docker/npm. Consistent with `DEAD-OR-UNREACHABLE.md` / `SHIPPED-CODE-MAP.md`. No new claim.

---

## Return envelope

```
OWNER: W4-H
SCOPE_FILES_READ: ~34 (packages/occam-mcp/{bin,lib/*,package.json,tsconfig}; occam-agent-sdk/{package.json,src/index,client,research,tsconfig}; occam-skill/{package.json,bin/install,lib/install,.occam_skill_version}; Dockerfile; docker-compose.yml; .dockerignore; all 8 .github/workflows/*.yml; scripts/{sync-occam-skill-package,lib/host-install-gate,lib/build-release,lib/sign-release-archives,get-ff-occam.sh}; src/.../Cli/OccamCliVerbs.cs, Transport/OccamMcpCli.cs, Program.cs; scripts/bench/{README,package.json}; workers/browser-extract/package.json)
BLIND_BEHAVIORS: 29
GAPS: covered_exact=6 partial=3 wrong=0 missing_cap=0 missing_edge=3 missing_artifact=0 missing_workflow=0 missing_config=0 missing_failure=2 missing_security=3 dead_as_product=2 product_as_internal=1
TOP_MISSED:
- Docker HEALTHCHECK /app/occam --version hangs → perpetual unhealthy (Dockerfile:76; Transport/OccamMcpCli.cs:53-160 ignores unknown args → stdio blocks)
- playbook-marketplace cosign sign-blob misconfigured: env key but no --key + no id-token:write → signing fails (playbook-marketplace.yml:16-19,100-110)
- auto-merge fires on l4-gate 'skipped'==success → unvalidated community PB merged (playbook-marketplace.yml:83-86,142-147)
- recursive trigger '**/*.json' vs single-level diff 'community/*.json' bypasses validation (playbook-marketplace.yml:6,74)
- npm launcher RID_MAP omits win-arm64/linux-arm64 despite cpu:arm64+os advertised (bin/occam-mcp.js:50-55 vs package.json:41-49)
- cosign .bundle consumed by no install path; npx+get-ff-occam verify sha256 vs unsigned manifest (bin/occam-mcp.js:237-241; get-ff-occam.sh:205-220)
- agent-sdk unused runtime dep @modelcontextprotocol/sdk (package.json:63; imported nowhere)
- skill --platform all skips codex but writes AGENTS.md pointer to uncreated .agents dir (lib/install.mjs:59-62,200-207)
NEW_CAP_CANDIDATES: none (all fold into S3-12 CAP-1020..1039 as corrections/edges)
NEW_EDGES:
- WORKFLOW:playbook-marketplace.yml --AUTO_MERGES--> profiles/playbooks/community/** WITHOUT_GATE (skip-path)
- WORKFLOW:sign-release.yml --PRODUCES--> .bundle CONSUMED_BY nothing (npx/get-ff-occam ignore it)
- PACKAGE:@ff-occam/mcp bin --REJECTS--> win-arm64,linux-arm64 (advertised in package.json)
- Dockerfile HEALTHCHECK --INVOKES--> nonexistent --version flag
NEW_ARTIFACTS: none (GitHub Pages docs site via docs.yml is the only under-modeled shipped artifact; low priority)
NEW_WORKFLOWS: none unmodeled (eval-harness/nightly-gate/agent-mvp-gate/ci/docs newly detailed here; none ship a product binary — all gate-only except docs Pages deploy)
AUTOMATIC_SILENT: skill installer rmSync wipes existing dest before copy (lib/install.mjs:193-196); playbook-bot auto-commits .sig + git push to PR branch; auto-merge --squash on validate success
FAILURE_FALLBACK: unknown CLI args silently ignored → stdio starts (root cause of Docker healthcheck hang); Docker missing profiles/ → seed/community/domain-tier/fetch-defaults fall back to built-ins silently
CONFIG_GAPS: OCCAM_RELEASE_BASE_URL (npx), OCCAM_NPX_ON_CLONE, OCCAM_RELEASE_VERSION (CI); COSIGN_PRIVATE_KEY/COSIGN_PASSWORD set-but-ineffective in playbook-marketplace.yml (no --key)
PLATFORM_DIFFS: shipped RIDs {linux-x64,osx-arm64,win-x64}; launcher maps 4 (adds osx-x64, none built); win-arm64/linux-arm64 advertised (cpu) but hard-rejected by RID_MAP; Docker linux-x64 only
EFC:
- EFC-H-1 MISSING_FAILURE_SEMANTIC high — Docker HEALTHCHECK --version invalid → always unhealthy
- EFC-H-2 MISSING_FAILURE_SEMANTIC high — playbook-marketplace cosign sign misconfigured (no --key + no id-token) → signing broken
- EFC-H-3 MISSING_SECURITY_SEMANTIC high — auto-merge on skipped gate → unvalidated community PB into main
- EFC-H-4 MISSING_EDGE med — recursive trigger vs single-level diff bypasses PB validation
- EFC-H-5 MISSING_EDGE med — npm launcher rejects advertised win-arm64/linux-arm64
- EFC-H-6 MISSING_SECURITY_SEMANTIC med — cosign bundles verified by no shipped install path
- EFC-H-7 DEAD_DEP low — agent-sdk ships unused @modelcontextprotocol/sdk
- EFC-H-8 MISSING_EDGE low — skill 'all' excludes codex but writes its AGENTS.md pointer; 'generic' w/o --target dead
- EFC-H-9 AUTOMATIC_SILENT low — skill installer destructive rmSync without confirm
- EFC-H-10 DEAD_AS_PRODUCT low — eval-harness compare + PB leaderboard steps are echo no-ops
CONVERGENCE_IN_SCOPE: PARTIAL. S3-12 already mapped the package/distribution shipping boundary thoroughly (CAP-1020..1039); independent re-derivation converged on its structural findings AND surfaced a distinct new cluster in CI workflow *behavior* (Docker healthcheck, marketplace signing/auto-merge/glob, cosign consumption) plus package edge-cases (arm64 RID, dead dep, skill installer branches) that S3-12 recited-but-did-not-test. New major-behavior discovery slowed sharply after the workflow pass — likely near saturation for this boundary.
UNCERTAINTIES:
- Whether cosign's default mode with COSIGN_PRIVATE_KEY-in-env-but-no-flag is keyless (my read) or key-based across cosign v3 versions — either way the step is misconfigured (missing id-token OR missing --key). Not runtime-verified.
- Whether a repo branch-protection ruleset blocks the auto-merge path in practice (config outside the repo tree; code path is open as written).
- Whether any documented docker-compose override bind-mounts profiles/skills (none found in-tree; matches S3-12 uncertainty).
- packages/* unit tests not executed (audit-only); pass/fail unverified.
```
