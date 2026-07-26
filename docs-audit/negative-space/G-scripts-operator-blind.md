# W4-G — Adversarial negative-space: scripts / operator CLI / connect / installer / session / doctor helpers

**Owner:** W4-G
**SoT:** shipped executable code under `scripts/**` (excl. `scripts/bench/**` = W4-H). Docs and prior `docs-audit/*` treated as UNTRUSTED until §2.
**Method:** blind code enumeration first (§1), then compare against the existing model (§2). Repo root `c:\PROJECTS\FFOccamMCP`.

---

## 1. Blind inventory (independent — before opening prior audit files)

### 1.A `occam` unified CLI dispatch (occam.mjs / occam.ps1 / occam-cli-*)

- **B-01** `scripts/occam.mjs` is the single Node entry; `occam.ps1` is a thin wrapper (`& node occam.mjs @Args`). Both set `OCCAM_HOME` = env or `scripts/..`.
- **B-02** Global flag parse (`occam.mjs:20-37`): only leading `--json` / `-h`/`--help` consumed; first non-flag = subcommand (lowercased).
- **B-03** Bare `occam` with a TTY and not `CI=1/true` → `runControlLoop` (interactive menu); bare `occam` non-TTY → usage + **exit 1** (`occam.mjs:63-70`).
- **B-04** Dispatch table `CLI_SUBCOMMANDS` (`occam-cli-subcommands.mjs`) = 13 names + aliases: `doctor`, `onboard`(alias `settings`), `connect`, `help`, `refresh`(alias `restart`), `smoke`, `update`, `session`, `snippet`, `skill`, `control`, `status`, `contract`(alias `version-surface`). Delegate kinds: `node` | `shell` | `internal` (the typedef also lists `powershell`, **never used**).
- **B-05** `dispatchSubcommand` (`occam-cli-dispatch.mjs`): `node` → `spawnSync(node, [script,...args])`; `shell` → picks `.ps1` (win, `powershell -NoProfile -ExecutionPolicy Bypass -File`) or `.sh` (`bash`); `internal` handled in `occam.mjs`.
- **B-06** `snippet` gets an **implicit** `occamHome` positional when caller passes no args (`occam-cli-dispatch.mjs:39-40`).
- **B-07** `doctor` shell delegate **silently appends `--skip-build`** when `isLevelBInstall` (has `VERSION`, no `.git`) and caller didn't (`occam-cli-dispatch.mjs:61-63, 77-80`).
- **B-08** Second, larger data table `COMMAND_REGISTRY` (`occam-command-registry.mjs`, 21 rows, tier `operator|ci|maintainer`) drives `occam help` only; **8+ rows are NOT reachable via `occam <sub>`** (`install`, `install.ps1`, `launch-mcp-host`, `occam-wrapper`, `occam-playbook-publish`, `print-connection-snippet`, `get-ff-occam`, `ci-agent-mvp-gate`, `run-agent-*`, `run-l0-fast`, `run-wide-cursor-desk`, `build-release`, `OccamMcp.Core`).
- **B-09** Maintainer publish CLI `occam-playbook-publish` (.sh/.ps1) is reachable **only** as a raw script, never via `occam <sub>` — an operator-tier registry row that is not in the dispatch table.
- **B-10** Host-binary offline verbs (`keys export`, `verify`, `install-browser`, `version-surface`, `lifecycle`) are **not** routed through `occam.mjs` — only via the raw binary.

### 1.B control-loop / control-actions / status / update

- **B-11** `control-loop.mjs` menu keys `1-6` → onboard/doctor/update/help/refresh/smoke; `s`→status; `h`→spawns `occam-help.mjs` (bypasses `runControlAction`); `q`/blank/`quit`/`exit` exits. `--json` mode short-circuits to `showStatus`. Non-TTY without `--json` = hard error.
- **B-12** `showStatus` (`control-actions.mjs:40-69`) always calls `checkForUpdate` (network) unless it throws; returns version, onboard meta, `onboardEnvKeys`, update.
- **B-13** `runRefresh` derives reload hints from `~/.occam/onboard.json` `hostTarget` (defaults `cursor`).
- **B-14** `update` (`update-check.mjs`): read-only GitHub Releases API (`https://api.github.com/repos/ContextForgeAI/occam/releases/latest`, override `OCCAM_RELEASES_API_URL`); `OCCAM_LATEST_VERSION` bypasses network; `OCCAM_RELEASE_ALLOW_HTTP=1` needed for `http://`; 15s timeout; RID from `OCCAM_RID`/platform. Never downloads.
- **B-15** `releaseBaseToApiUrl` is exported but has **no production caller** — referenced only by `update-check.selftest.mjs`.

### 1.C occam-refresh-host + stop-occam-processes (process kill)

- **B-16** `occam-refresh-host.mjs`: flags `--dry-run`, `--skip-stop`, `--skip-doctor`, `--smoke`, `--include-dotnet`. Stops hosts → runs doctor (platform ps1/sh) → optional hermes-smoke → prints reload hint.
- **B-17** Reload hint hardcodes **"9 occam_* tools"** (`occam-refresh-host.mjs:89`) — wrong (core = 15).
- **B-18** `stop-occam-processes.mjs` `listOccamHostProcesses`: on **win32** the WHERE clause matches `Name -eq 'OccamMcp.Core.exe' -or Name -eq 'FFOccamMcp.Core.exe'` with **NO root filter**; the root filter is applied **only** to the `launch-mcp-host.mjs` command-line match. On **POSIX**, `mentionsHost` (`trimmed.includes(base)`) **bypasses** the `rootNorm` check.
- **B-19** ⇒ `stopOccamHostProcesses` (used by `occam refresh` and by the script's own `main()`) **force-kills every process on the machine named after the host binary regardless of `OCCAM_HOME`** — collateral across parallel installs / other users' hosts. `killPid` = `taskkill /F` (win) / `SIGTERM`→`SIGKILL` (posix).
- **B-20** `stopOccamHostByPid` (INV-10) is the *targeted* path with the "never process-name-wide" comment — but it is **not** what `refresh` uses.
- **B-21** Grace delay is a **busy-spin** loop (`sleepMs`, `stop-occam-processes.mjs:159-167`) burning CPU for 1500ms per stop — reachable from `occam refresh`, not just maintainer runs.
- **B-22** `--include-dotnet` broadens matching to `dotnet ... FFOccamMcp.Core.csproj` (win: command-line regex; posix adds `FFOccamMcp.Core.csproj`).

### 1.D launch-mcp-host (the canonical launcher)

- **B-23** `launch-mcp-host.mjs` runs the resolved AOT binary with a **hardcoded `[]` arg list** — no `--mcp-server`/`--batch-server`/`--port`/`--remote` ever reach the child ⇒ **stdio only**; WS/Remote/Batch unreachable via launcher.
- **B-24** It **injects `mergeOnboardEnv(...)`** into the child env: keys from `~/.occam/onboard.json` (or `OCCAM_CONFIG`) `env` map become live host process env on every launch (explicit process env wins).
- **B-25** Stamps `OCCAM_RUNTIME_ID`, `OCCAM_SESSION_ID`, `OCCAM_PARENT_PID`, `OCCAM_PARENT_LABEL`; forwards SIGINT/SIGTERM/SIGHUP to the **exact child pid only**.
- **B-26** No binary + `OCCAM_FORCE_DOTNET_RUN=1` + project present → `dotnet run` (needs SDK ≥10, else install-blocked). No binary + project present without the flag → `exitInstallBlocked`.

### 1.E connect platform (occam-connect.mjs + lib/operator/connect/**)

- **B-27** 15 host adapters registered (`registry.mjs`): hermes, openclaw, claude-code, codex, gemini, cursor, claude-desktop, vscode, cline, roo, windsurf, zed, opencode, goose, junie. Waves 1/2/3 grouping.
- **B-28** `resolveConnectMode` (`policy.mjs`): CI-like (`CI`/`GITHUB_ACTIONS`/`OCCAM_CONNECT_CI`) never mutates unless `OCCAM_CONNECT_FORCE=1`; **default desktop AND non-TTY curl|bash default = `auto` with `mutateHosts:true`** ("desktop bootstrap default"). Only `OCCAM_CONNECT=off|detect` suppresses.
- **B-29** ⇒ `occam connect` / bootstrap **silently writes MCP registration into up to 15 host config files** (`~/.cursor/mcp.json`, `~/.claude.json`, VSCode, Zed, etc.) without an interactive confirm.
- **B-30** `selectAutoConnectAdapters`: Tier A only (Tier A+B when `--only`), `detected && confidence∈{high,medium} && !ambiguous && method∈{NATIVE_CLI,CONFIG_FILE}`.
- **B-31** Ownership (`ownership.mjs`): unmanaged existing `ff-occam` entry is skipped unless `--force`; managed = env marker `OCCAM_CONNECT_MANAGED=occam-managed:v1` OR command points at `occam-wrapper.sh`/`launch-mcp-host.mjs`.
- **B-32** config-engine transaction: load→plan→backup(`*.occam-bak`)→atomic temp+rename→round-trip validate→restore; refuses JSONC (comments) rather than destroying them; redacts secret-ish env/headers in diagnostics.
- **B-33** Per-host rollback (`decidePostVerifyCleanup`): `remove` (add) / `restore` (update) only when applied && !verifyOk && **not** preserved. **`requiresRestart:true` preserves registration** → CONFIG_FILE hosts (Cursor always `requiresRestart:true`) never roll back a broken write.
- **B-34** `writeConnectLast` unconditionally writes `~/.occam/connect-last.json` (best-effort); bootstrap scripts parse it to decide whether to skip the manual snippet.
- **B-35** connect exit code: 1 only on hard apply failure (excluding `requiresUserAction`), occamVerify fail, or rollback fail; else 0. `--detect-only` never populates `report.skipped` (only when `mutateHosts`).
- **B-36** `occam-verify.mjs`: spawns host via stable launch spec, does stdio `initialize`+`tools/list`, asserts `EXPECTED_MIN_TOOLS = 15`, 60s per-request timeout.
- **B-37** Native-CLI adapters (e.g. claude-code) shell out to `claude mcp add/get/remove` — mutate via the host's own CLI, not a JSON file; PowerShell `--` quirk noted in code.

### 1.F installers / bootstrap (install.sh/.ps1, get-ff-occam.sh/.ps1, release-install)

- **B-38** `install.sh`: Level A (git clone `--depth 1 --branch $REF`, or fetch+checkout+ff-only pull if existing repo) or Level B (`--from-url`, sha256-manifest-verified tarball). Refuses non-git existing dir. Warns on pipe install.
- **B-39** `release-install.mjs` & get-ff-occam both **destructively `rm -rf` / `rmSync` the install dir before extract** (`release-install.mjs:91-93`, `get-ff-occam.sh:226-231`, `get-ff-occam.ps1:152`). No rollback if extract fails; user-provided `OCCAM_INSTALL_DIR` is wiped wholesale.
- **B-40** get-ff-occam verifies sha256 vs manifest before extract; HTTPS enforced unless `OCCAM_RELEASE_ALLOW_HTTP=1`. Default base `https://github.com/ContextForgeAI/occam/releases/download/v<VERSION>`, VERSION default `1.0.0-rc.2`.
- **B-41** get-ff-occam auto-runs: doctor(`--skip-build`) → verify-install → hermes-smoke → onboard (auto/manual) → **occam-connect (host mutation)** → connection snippet.
- **B-42** **Platform divergence:** `get-ff-occam.sh` delegates the product welcome + setup prompt to `get-install-welcome.mjs` (rich banner, node-driven auto/manual) **when `ROOT_DIR` resolves**; in the real `curl|bash` pipe `ROOT_DIR` is empty ⇒ falls back to an **inline banner hardcoding "14 occam_*"** (`get-ff-occam.sh:70`). `get-ff-occam.ps1` has **no** get-install-welcome path at all — always inline minimal banner + inline `Read-Host`.
- **B-43** get-ff-occam env surface: `OCCAM_VERSION/RID/INSTALL_DIR/HOST/SETUP/RELEASE_BASE/RELEASE_URL/RELEASE_MANIFEST_URL/RELEASE_ALLOW_HTTP/NO_COLOR`.

### 1.G session CLI (occam-session.mjs)

- **B-44** Subcommands: `init`, `list`, `import`, `export-state`. Sessions root = `OCCAM_SESSIONS_ROOT` or `~/.occam/sessions`.
- **B-45** `import` writes a profile JSON containing a `Cookie` header (plaintext secrets on disk); `list` claims "no secret values".
- **B-46** `import` **keeps a raw copy of the source cookies.txt in `_imports/` by default** (`keepImport` true unless `--no-keep-import`) — retains plaintext credentials beyond the profile.
- **B-47** `--all` multi-site import warns that Occam workers do not domain-filter cookies (leak-across-hosts warning); `cf_clearance` detection warning; >8192-byte Cookie warning.
- **B-48** `export-state` launches **headed Playwright** for manual login and saves storageState.

### 1.H skill install (occam-skill-install.mjs + install-occam-skill.mjs)

- **B-49** Two code paths: `occam skill install` (this script) and `npx @ff-occam/skill install`. Source resolved from 3 candidates (`$OCCAM_HOME/skills/occam`, repo `skills/occam`, `packages/occam-skill/skill`).
- **B-50** `installOccamSkill` **`fs.rmSync(dest, recursive, force)` before copy** — wipes an existing skill dir at `~/.cursor/skills/occam`, `~/.claude/skills/occam`, etc., with no backup.
- **B-51** Destinations write into user/project harness dirs (cursor/claude/hermes/copilot/kiro/pi/devin/codex/generic).
- **B-52** `writeAgentsMdSection` **mutates the project `AGENTS.md`** (marker-fenced block) when `platform===codex||all` && `scope===project`.
- **B-53** **Inconsistency:** `all` expands to `[cursor,claude,hermes,copilot,kiro,pi,devin]` (7) — excludes `codex` and `generic` — so `--platform all --project` writes/edits `AGENTS.md` even though no codex `.agents/skills/occam` tree was copied.

### 1.I misc

- **B-54** `occam-onboard.mjs` writes `~/.occam/onboard.json` + optionally `~/.cursor/mcp.json` **before** running verify (doctor/smoke) — config persisted even if verify later fails.
- **B-55** `hermes-smoke.mjs` (per registry) asserts a fixed tool count; inherits caller env (profile/opt-in unaware).
- **B-56** `occam-wrapper.sh` prepends a Node ≥20 dir to PATH heuristically before exec-ing launch-mcp-host (silent PATH mutation for the child).

---

## 2. Gap classification (vs CAPABILITY-INVENTORY / capabilities.json / CLI-SURFACE / CONNECT-PLATFORM / RUNTIME-MODES / doctor.md / install-onboard.md / session-lifecycle.md / ARTIFACT-MAP / NONCORE-SURFACE-MAP / ENGINEERING-FINDINGS)

Wave 3 already covered this surface heavily. Findings below are ONLY where the blind read diverges from or exceeds the model.

| # | Behavior | Label | Evidence | Model status |
|---|----------|-------|----------|--------------|
| G1 | `stopOccamHostProcesses` kills by **host-binary name machine-wide, ignoring `OCCAM_HOME`** (root filter applied only to the `launch-mcp-host.mjs` match, and POSIX `mentionsHost` bypass) → collateral kill of other installs/users | **MISSING_SECURITY_SEMANTIC** | `stop-occam-processes.mjs:77-92` (win `hostNameClause`, no root), `:135-138` (posix bypass), `:229-246` (`main()`+refresh force-kill) | doctor.md & CLI-SURFACE explicitly **defer** stop-occam kill mechanics to "S3-07"; CLI-SURFACE only modeled `occam refresh` as "stop running host processes" (CAP-928) — the name-wide collateral is **unaudited**. Contradicts the "never scans/kills by process name" framing (CAP-921/922, CLI-SURFACE §2) |
| G2 | `mergeOnboardEnv` injects `~/.occam/onboard.json` `env` map into the **MCP host process env on every launch** | **MISSING_RUNTIME_SURFACE** | `onboard-config.mjs:17-29`, `launch-mcp-host.mjs:29-36` | ARTIFACT-MAP/CLI-SURFACE model `onboard.json` write+consume (status/refresh hints) but NOT its role as launch-time host-env injection |
| G3 | `releaseBaseToApiUrl` exported, used only by its selftest — no production caller | **DEAD_CODE_MISTAKEN_AS_PRODUCT** | `update-check.mjs:74-82`; only `update-check.selftest.mjs:5,13` reference it | not flagged anywhere |
| G4 | skill install `fs.rmSync(dest,…force)` before copy (destructive, no backup) | **MISSING_EDGE** | `install-occam-skill.mjs:250-253` | S3-12 modeled skill card version/npm bin (EF-034/036), not the destructive overwrite |
| G5 | skill install mutates project `AGENTS.md`; `all` excludes codex/generic yet AGENTS block still writes on `all`+`project` | **MISSING_EDGE** | `install-occam-skill.mjs:82-84, 162-202, 258-263` | not modeled |
| G6 | session `import` retains **raw plaintext cookies.txt in `_imports/`** by default | **MISSING_SECURITY_SEMANTIC** | `occam-session.mjs:123-128` | CAP-175 covers import→profile; raw-source retention understated |
| G7 | `occam refresh` grace delay is a **busy-spin** (`sleepMs`) — CPU burn reachable from an operator command | **MISSING_FAILURE_SEMANTIC** (perf) | `stop-occam-processes.mjs:159-167` (comment: "maintainer script only", but used by refresh) | not modeled |
| G8 | get-ff-occam `curl|bash` real path uses the **inline fallback banner "14 occam_*"** (ROOT_DIR empty in a pipe → never reaches get-install-welcome.mjs) | **COVERED_PARTIALLY** (new location) | `get-ff-occam.sh:12-14, 70` | EF-030 covers stale "14 tools" in `get-install-copy.mjs` only; this is a **second, pipe-only** stale-count location |
| G9 | get-ff-occam **`.ps1` vs `.sh` capability divergence**: `.sh` has a node-driven product welcome + interactive setup prompt (get-install-welcome.mjs); `.ps1` has neither | **MISSING_CONFIG** (platform diff) | `get-ff-occam.sh:57-123` vs `get-ff-occam.ps1:110-116` | install-onboard.md did not flag this divergence |
| G10 | `occam refresh` reload hint "9 occam_* tools" | **COVERED_EXACTLY** | `occam-refresh-host.mjs:89` | = EF-022 |
| G11 | wrapper does not route `install-browser`/`verify`/`keys` to host binary | **COVERED_EXACTLY** | dispatch table lacks these | = EF-025 |
| G12 | install `rm -rf INSTALL_DIR` before extract, no rollback | **COVERED_EXACTLY** | `release-install.mjs:91-93`, get-ff-occam | = EF-028 |
| G13 | onboard writes config before verify | **COVERED_EXACTLY** | `occam-onboard.mjs:171-191` | = EF-029 |
| G14 | connect rollback dead for CONFIG_FILE `requiresRestart:true` | **COVERED_EXACTLY** | `ownership.mjs:55-65`, `orchestrator.mjs:303-313` | = EF-021 |
| G15 | launcher stdio-only; WS/Remote/Batch unreachable via scripts | **COVERED_EXACTLY** | `launch-mcp-host.mjs:79` | = CAP-1001 (RUNTIME-MODES) |
| G16 | default desktop + non-TTY bootstrap `mutateHosts:true` (15-host silent registration) | **COVERED_EXACTLY** | `policy.mjs:64-71` | = CONNECT-PLATFORM §policy |
| G17 | 15 connect adapters; ownership marker; JSONC-refuse; atomic write + `*.occam-bak` | **COVERED_EXACTLY** | `registry.mjs`, `config-engine.mjs` | CONNECT-PLATFORM |
| G18 | `connect-last.json` artifact under `~/.occam` | **COVERED_EXACTLY** | `occam-connect.mjs:90-98` | ARTIFACT-MAP |
| G19 | powershell-tier `delegate` typedef value declared, never used | **COVERED_PARTIALLY** (minor dead enum) | `occam-cli-subcommands.mjs:3` | not modeled (trivial) |
| G20 | `--detect-only` leaves `report.skipped` empty (no per-host reasons in detect mode) | **MISSING_EDGE** | `orchestrator.mjs:142-149` | not modeled |

---

## Return envelope

```
OWNER: W4-G
SCOPE_FILES_READ: ~30 (occam.mjs, occam.ps1, occam-wrapper.sh, occam-cli-dispatch/subcommands/command-registry, control-loop, control-actions, update-check, occam-refresh-host, stop-occam-processes, launch-mcp-host, occam-connect + connect/{registry,policy,orchestrator,ownership,config-engine,occam-verify,adapters/cursor,adapters/claude-code}, onboard-config, occam-onboard, occam-session, occam-skill-install, install-occam-skill, playbook-publish + .sh/.ps1, install.sh, get-ff-occam.sh/.ps1, release-install, get-install-welcome)
BLIND_BEHAVIORS: 56
GAPS: covered_exact=9 partial=3 wrong=0 missing_cap=0 missing_edge=4 missing_artifact=0 missing_workflow=0 missing_config=1 missing_failure=1 missing_security=2 dead_as_product=1 product_as_internal=0
TOP_MISSED:
  1. stop-occam-processes kills by host-binary NAME machine-wide, ignoring OCCAM_HOME (collateral) — stop-occam-processes.mjs:77-92,135-138,229-246
  2. mergeOnboardEnv injects ~/.occam/onboard.json env into the host process every launch — onboard-config.mjs:17-29 / launch-mcp-host.mjs:29
  3. skill install fs.rmSync(dest) destructive overwrite, no backup — install-occam-skill.mjs:250-253
  4. skill install mutates project AGENTS.md; 'all' excludes codex/generic yet still writes AGENTS block — install-occam-skill.mjs:82-84,258-263
  5. session import retains raw plaintext cookies.txt in _imports/ by default — occam-session.mjs:123-128
  6. releaseBaseToApiUrl is test-only dead code — update-check.mjs:74
  7. occam refresh grace delay is a CPU busy-spin — stop-occam-processes.mjs:159-167
  8. get-ff-occam curl|bash pipe path uses inline stale "14 occam_*" banner + .ps1/.sh welcome divergence — get-ff-occam.sh:70 / .ps1:110-116
NEW_CAP_CANDIDATES: CAP-NEW-G-1 (onboard.json env → host-launch injection); CAP-NEW-G-2 (stop-occam name-wide kill scope, distinct from INV-10 targeted pid path)
NEW_EDGES: launch-mcp-host.mjs |INJECTS| onboard.json.env into host process env; occam refresh |USES| stopOccamHostProcesses (name-wide) NOT stopOccamHostByPid (INV-10); skill-install |MUTATES| project AGENTS.md
NEW_ARTIFACTS: ~/.occam/sessions/_imports/<cookies.txt> (retained plaintext source); project AGENTS.md marker-fenced occam block
NEW_WORKFLOWS: none (workflows already modeled; refinements only)
AUTOMATIC_SILENT: bootstrap/`occam connect` mutates up to 15 host config files with no confirm (policy default); `occam refresh` name-wide process kill; launcher onboard-env injection; skill install destructive rmSync
FAILURE_FALLBACK: install/get-ff-occam rm -rf before extract has no rollback (EF-028); connect CONFIG_FILE rollback dead when requiresRestart (EF-021); refresh busy-spin grace
CONFIG_GAPS: OCCAM_RELEASE_ALLOW_HTTP gates http release in 3 independent places (get-ff-occam.sh/.ps1, release-install, update-check); onboard.json env keys are an uncontrolled host-env surface; releaseBaseToApiUrl dead
PLATFORM_DIFFS: stop-occam win (name-eq, no root) vs posix (mentionsHost bypass) — both name-wide but different match logic; get-ff-occam .ps1 lacks node welcome/setup of .sh; killPid taskkill/F vs SIGKILL
EFC:
  EFC-G-1 (SECURITY-CANDIDATE, PROVEN in code): occam refresh / stop-occam-processes force-kill every process named OccamMcp.Core[.exe]/FFOccamMcp.Core[.exe] regardless of OCCAM_HOME — collateral kill of sibling installs/other users; contradicts INV-10 "never process-name-wide" framing which only guards stopOccamHostByPid. stop-occam-processes.mjs:77-92,135-138.
  EFC-G-2 (DESIGN/PRIVACY, PROVEN): session import keeps raw cookies.txt in _imports/ by default; list claims "no secret values". occam-session.mjs:123-128.
  EFC-G-3 (BUG-CANDIDATE, PROVEN): skill `--platform all --project` writes AGENTS.md occam block though 'all' never copies a codex/.agents tree. install-occam-skill.mjs:82-84,258-263.
  EFC-G-4 (OBSERVATION, PROVEN): releaseBaseToApiUrl dead (test-only). update-check.mjs:74.
  EFC-G-5 (OBSERVATION, PROVEN): get-ff-occam curl|bash real path prints stale "14 occam_*" (ROOT_DIR empty → inline banner). get-ff-occam.sh:70. Distinct location from EF-030.
CONVERGENCE_IN_SCOPE: YES — Wave 3 (CLI-SURFACE, CONNECT-PLATFORM, RUNTIME-MODES, doctor.md, install-onboard.md, session-lifecycle.md) already models the bulk; independent discovery surfaced a small residue clustered on (a) process-kill scope, (b) launch-time env injection, (c) destructive overwrite/retention semantics that prior owners explicitly deferred or under-specified. No large unmodeled subsystem remains.
UNCERTAINTIES:
  - Whether taskkill /F on Windows can reach another OS user's OccamMcp.Core.exe depends on caller privilege (elevation); collateral within the same user is certain.
  - Did not open every one of the 15 adapters (read cursor + claude-code as representatives of CONFIG_FILE vs NATIVE_CLI); the other 13 assumed consistent with CONNECT-PLATFORM.
  - print-connection-snippet.mjs / verify-install.mjs internals read only indirectly; classified from callers.
  - AGENTS.md-write vs 'all' mismatch (EFC-G-3) assumes intended parity between copied trees and the AGENTS block; could be deliberate.
```
