# Install / onboard — code-derived capability audit (Wave 3)

**Agent:** S3-09 · **CAP range:** CAP-960 … CAP-979 · **SoT:** current executable code only
(`INSTALL.md`, `docs/getting-started.md`, `docs/operator_journey.md` **not** read for claims —
consulted nowhere in this file).
**Repo:** `c:\PROJECTS\FFOccamMCP`

Scope per `NONCORE-SURFACE-MAP.md` §G: `scripts/install.ps1` / `install.sh`,
`scripts/get-ff-occam.ps1` / `.sh`, `scripts/occam-onboard.mjs` (+ its `scripts/lib/operator/onboard-*.mjs`
dependencies), `scripts/verify-install.ps1` / `scripts/lib/verify-install.mjs`,
`scripts/launch-mcp-host.mjs` (launch-time consumer of onboard state). `occam-doctor.*` is invoked
*by* every install path audited here but is itself S3-08 scope — referenced only where install
code directly delegates to it. `occam-connect.mjs` (S3-10) and `occam_client_capabilities`/core
MCP tools are out of scope — referenced only at the install→connect handoff boundary.

All line numbers are as of the inspection commit at audit time; re-verify before citing in a
public doc.

---

## 0. Entry-point map — five ways an install can start

| Entrypoint | Level | Platform | Requires on target |
|---|---|---|---|
| `scripts/install.ps1 -RepoUrl … -Ref …` | A (clone + build) | Windows | git, Node 20+, .NET SDK 10+ |
| `scripts/install.sh --repo-url … --ref …` | A | macOS/Linux | git, Node 20+, .NET SDK 10+ |
| `scripts/install.ps1 -FromUrl …` (or `OCCAM_RELEASE_URL`) | B (tarball) | Windows | Node 20+ only |
| `scripts/install.sh --from-url …` | B | macOS/Linux | Node 20+ only |
| `get-ff-occam.ps1` (`irm \| iex`) | B, self-contained one-liner | Windows | Node 20+, `tar.exe` |
| `get-ff-occam.sh` (`curl \| bash`) | B, self-contained one-liner | macOS/Linux | Node 20+, `curl`, `tar`, `sha256sum`/`shasum` |

`install.ps1`/`install.sh` are **operator-invoked after cloning** (or with `-FromUrl`, no clone
needed); `get-ff-occam.*` are **self-contained bootstrap scripts** that duplicate the download/verify
logic inline rather than shelling out to `release-install.mjs` (see CAP-963). Both families converge
on the same post-install chain: `occam-doctor.*` → `verify-install.mjs` → `occam-onboard.mjs` →
(get-ff-occam only) `occam-connect.mjs`.

---

## 1. Level A / Level B installer (`install.ps1` / `install.sh`)

- **CAP-960 — Level A: clone-pinned-ref install.** `scripts/install.ps1:104-229`, `scripts/install.sh:122-206`.
  Requires `-RepoUrl`/`--repo-url` (or `OCCAM_REPO_URL`) — no default remote. Ref resolution:
  `-Ref` → `OCCAM_REF` → `OCCAM_BRANCH` → `"main"`. If `$InstallDir/.git` exists: `fetch --tags` then
  `checkout $Ref`, falling back to `checkout origin/$Ref` (detached) if the ref isn't a local branch;
  a local-branch ref additionally does `pull --ff-only` (hard-fails, no auto-merge, if that's not
  possible). If the dir exists but is **not** a git repo, both scripts refuse and require manual
  removal — never silently overwrites a non-git directory. Fresh clone is `git clone --depth 1
  --branch $Ref` (shallow — no full history on target). After checkout: `occam-doctor.*` (full build,
  no `--skip-build` unless `-SkipBuild`/`--skip-build`) → `verify-install.mjs` (unless `-SkipVerify`).
- **CAP-961 — Level B: tarball install delegation.** `install.ps1:39-102`, `install.sh:77-120`.
  Triggered by `-FromUrl`/`--from-url` (or `OCCAM_RELEASE_URL`) — mutually exclusive with the Level-A
  path (checked by branching on `$FromUrl`/`$RELEASE_URL` first). Delegates entirely to
  `install-preflight.mjs release` (Node-only check, see CAP-972) then `release-install.mjs` (CAP-962).
  Sets `OCCAM_HOME=$InstallDir`, runs `occam-doctor.* --skip-build` unconditionally (Level B never
  builds), then `verify-install.mjs --skip-build` unless `-SkipVerify`. Reads `VERSION` file for the
  banner if present, else prints `unknown` — does not fail the install if `VERSION` is missing.
- **CAP-962 — `release-install.mjs`: download/verify/extract.** `scripts/lib/release-install.mjs`.
  Accepts `--url` (HTTPS-enforced, see below) or a local `--file`; manifest defaults to
  `<tarball>-manifest.json` (`.tar.gz` suffix stripped) unless `--manifest-url`/`--manifest` given.
  `assertHttps` (line 45-60) hard-fails non-HTTPS URLs **unless** `OCCAM_RELEASE_ALLOW_HTTP=1`
  (warns, does not fail) — the one deliberate HTTP-downgrade escape hatch, documented for "LAN/trusted
  forge only". Manifest must carry `sha256`, `version`, `rid` or the whole install aborts before any
  download of the tarball itself for the manifest, and before extraction for the tarball. `sha256File`
  (crypto SHA-256, hex, case-insensitive compare) gates extraction; a Node-version check re-derives
  `manifest.nodeMajorMin` (default 20) independently of `install-preflight.mjs`'s own hardcoded 20 —
  two separate minimum-Node sources that happen to agree today but aren't the same constant.
  `extractTarball` (line 88-104) is a **full destructive replace**: `rmSync(installDir, {recursive:
  true, force:true})` then fresh `mkdirSync`, then `tar -xzf … --strip-components=1` — see CAP-965 for
  the rollback implication. Post-extract, an on-disk `VERSION` file (if the tarball ships one) is
  cross-checked against `manifest.version` and the whole install fails on mismatch — a tarball/manifest
  drift guard that runs *after* the destructive extract, not before.
- **CAP-972 — `install-preflight.mjs` shared prerequisite gate.** `scripts/lib/install-preflight.mjs`.
  Four check groups: `all` (git + Node 20+ + dotnet SDK 10+ — Level A), `release` (Node 20+ only —
  Level B), or individually `git`/`node`/`dotnet`. Every failure is `console.error` + `process.exit(1)`
  with a specific missing-tool message (not a generic failure). **Only** `install.ps1`/`install.sh`
  call this module — `get-ff-occam.ps1`/`.sh` and `occam-onboard.mjs` each re-implement their own
  inline Node-major-version check (`Test-NodeVersion` / `check_node` / none at all in onboard, which
  assumes Node is already running since it *is* the Node process) rather than importing this shared
  gate; not a bug (onboard is already running under Node), but the two bootstrap scripts' duplicate
  version-check logic could drift from this module's `MIN_NODE_MAJOR = 20` constant since nothing
  enforces they stay in sync.
- **CAP-975 — No installer persists `PATH` on any platform.** Grep across every script in this file's
  scope (plus `scripts/occam-wrapper.sh`) found zero calls to `[Environment]::SetEnvironmentVariable`
  with a `User`/`Machine` target, `setx`, or any `.bashrc`/`.zshrc`/`.profile` append. Every completion
  banner instead prints a **shell-session-only** hint: `install.ps1:92` \` `Next:
  $env:PATH = "$InstallDir\scripts;$env:PATH" ; occam` \`, `install.sh:197` \`export
  PATH="$INSTALL_DIR/scripts:$PATH"\`. This means the `occam` CLI convenience alias never survives a
  new terminal/session unless the operator re-runs that export every time, or re-sources whatever
  they added manually — the *actual* durable state left by every install path is the printed/written
  MCP snippet (absolute paths baked in, CAP-968), not a `PATH` entry.

---

## 2. `get-ff-occam.*` — self-contained one-liner bootstrap

- **CAP-963 — Independent download/verify/extract implementation (duplicated, not shared).**
  `scripts/get-ff-occam.ps1` (234 lines) and `scripts/get-ff-occam.sh` (333 lines) each re-implement
  HTTPS-scheme assertion, sha256 comparison, and tarball extraction **inline** rather than shelling
  out to `scripts/lib/release-install.mjs` — by necessity, since these are meant to run via
  `irm | iex` / `curl | bash` *before* any repo files exist on disk to `node` a `.mjs` against. Same
  security properties as CAP-962 (manifest-first sha256 gate, HTTPS-only unless
  `OCCAM_RELEASE_ALLOW_HTTP=1`) but a **second, independently-maintained copy** of that logic — a
  change to the sha256/HTTPS policy in one place does not propagate to the other three
  implementations (`release-install.mjs`, `get-ff-occam.ps1`, `get-ff-occam.sh`) automatically.
  Platform difference: `get-ff-occam.sh:18-32` auto-detects OS+arch → RID (`osx-arm64`/`osx-x64` on
  Darwin by `uname -m`, `win-x64` for MinGW/MSYS/Cygwin, else `linux-x64` fallback for "unknown"), while
  `get-ff-occam.ps1:20` **hardcodes `win-x64`** unless `OCCAM_RID` is set explicitly — no arm64
  Windows auto-detection branch exists.
- **CAP-964 — Non-interactive/CI detection is inconsistent across the five entrypoints.**
  - `install.ps1:33-35` / `install.sh:71-73`: detect redirected stdin, **warn only** ("For production,
    clone the repo and run directly"), then continue anyway — never blocks or changes behavior.
  - `get-ff-occam.ps1:74-78` / `get-ff-occam.sh:101-105`: redirected stdin/non-TTY **silently switches**
    `SetupMode` to `auto` (no warning needed — this is the documented pipe-install default).
  - `occam-onboard.mjs:151-153`: the **strictest** of the five — no TTY *and* no `--non-interactive`
    flag *and* no `--skip` is a hard `process.exit(2)` with `"No TTY — use --non-interactive or
    --skip"`. This is why `get-ff-occam.*` always pass `--non-interactive` explicitly in their
    scripted call (`occam-onboard.mjs --non-interactive --profile … --host-target … --skip-doctor
    --plain`) — omitting that flag under a piped install would hard-fail onboarding.
  - `occam-onboard.mjs:94` also treats `CI=1` or `CI="true"` as an implicit `--skip-doctor` inside
    `runVerify` regardless of the `--skip-doctor` flag's own value — a second, independent
    CI-detection branch inside the same file, not unified with the TTY check above.
- **CAP-971 — get-ff-occam post-install orchestration order.** `get-ff-occam.sh:main()` (lines
  312-331) and the equivalent tail of `get-ff-occam.ps1` (lines 175-237) run, in fixed sequence:
  welcome banner → setup-mode resolution → `install_release` (CAP-963) → `post_install` (doctor
  `--skip-build` → `verify-install.mjs --skip-build` → `hermes-smoke.mjs`, **not** individually
  skippable — no flag suppresses any of these three) → `run_onboard` (manual: interactive wizard
  minus welcome/doctor; auto: `--non-interactive --profile <derived> --host-target <derived>
  --skip-doctor --plain`) → `run_connect` (`occam-connect.mjs`, explicitly allowed to fail — `|| true`
  in the shell variant, "Do not fail the whole install on partial host connect failures" comment in
  the PowerShell variant) → `print_connection_snippet`, which reads
  `~/.occam/connect-last.json` and **suppresses** the fallback JSON/YAML snippet only if that file
  shows `mutateHosts: true` **and** at least one connection was actually `applied`/`noop` — i.e. the
  manual snippet is the safety net whenever auto-connect ran in report-only mode or touched zero hosts.
- **CAP-976 — Eager `-ForcePlaywright`/`--force-playwright` browser install.** `install.ps1:65-74,
  185-194`, `install.sh:96-99, 173-176` — runs `npx playwright install chromium` inside
  `workers/browser-extract` unconditionally when the flag is passed, for **both** Level A and Level B.
  This is redundant with (but intentionally precedes) the lazy auto-provision path
  (`OCCAM_BROWSER_AUTOINSTALL`, prior-wave CAP-206/CAP-361) that `occam-doctor.*`'s own launch-probe
  (`ensure-chromium-usable.mjs`) already performs on every doctor run — the flag exists purely to make
  the download happen deterministically at install time (e.g. for image-baking) rather than lazily on
  first extract call.
- **CAP-977 — `verify-community-manifest.mjs` runs unconditionally inside every doctor invocation.**
  `occam-doctor.ps1:111-118`, `occam-doctor.sh:107-114` — both installer families call `occam-doctor.*`
  as part of their chain, and doctor unconditionally re-verifies the shipped community-playbook
  manifest's sha256 (if `scripts/lib/verify-community-manifest.mjs` exists) **before** asserting the
  host binary is ready, hard-failing the whole doctor run (and therefore the whole install) on
  mismatch. This is a second, independent sha256 gate from the release-tarball manifest check
  (CAP-962) — one protects the *distribution artifact*, this one protects a *bundled content file*
  inside that artifact.

---

## 3. `occam-onboard.mjs` — profile wizard / persisted state

- **CAP-967 — Core flow: interactive wizard vs `--non-interactive` vs `--skip`.**
  `scripts/occam-onboard.mjs:122-192`. `--skip` exits 0 immediately, writing nothing (not even a
  banner beyond "Onboard skipped"). Otherwise: `--non-interactive` requires `--profile` to be one of
  `PROFILE_IDS` (`default`/`hermes-headless`/`mass-scrape`, `onboard-steps.mjs:10`) or hard-exits(2);
  interactive mode requires a TTY (else exit 2, CAP-964) and walks `STEP_DEFS`
  (`occamHome`/`hostTarget`/`browser`/`proxy`/`profile`, `onboard-steps.mjs:74-107`) via
  `collectInteractiveAnswers`, each step validated before acceptance. `runFlow` →
  `normalizeAnswers` → `buildOnboardResult` (`onboard-flow.mjs:12-47`) derives an `env` object by
  composing `applyProfile` + `applyBrowserChoice` + `applyProxyChoice` (`onboard-steps.mjs:16-67`) —
  e.g. `mass-scrape` sets `OCCAM_BROWSER_PROFILE=isolated`, `OCCAM_BROWSER_DAEMON=0`,
  `OCCAM_BROWSER_MAX_PARALLEL=4`, `OCCAM_BROWSER_POOL_SIZE=4`, `OCCAM_DIGEST_MAX_PARALLEL=4` in one
  step; `hermes-headless` additionally forces `OCCAM_BANNER=0`/`WT_OCCAM_BANNER=0`. **Unconditionally**
  writes `~/.occam/onboard.json` (`writeOnboardConfig`, line 173) before running any verification —
  see EF-020 for the ordering implication.
- **CAP-968 — `--write-config`: opt-in, confirmed, merge-only host-config mutation.**
  `scripts/lib/operator/write-mcp-config.mjs`. Only fires if the CLI flag is passed (default is
  print-only). For `cli-only` target: no-op, prints a message. For `hermes`: **never writes** — always
  prints the YAML for manual merge, even with `--force` (comment: "automatic write not enabled for
  YAML v1"). For every other target: merges **only** the `mcpServers["ff-occam"]` key into
  `~/.cursor/mcp.json` (or a caller-supplied path), preserving every other key in the existing file
  (`mergeCursorConfig`, lines 26-45); a malformed existing JSON file throws rather than silently
  overwriting it. Without `--force`, requires a literal `YES` typed at a TTY prompt
  (`confirmWrite`, line 50-62) — with no TTY and no `--force`, hard-exits(2) rather than silently
  skipping or silently writing.
- **CAP-969 — `runVerify()`: doctor + smoke re-check, order-of-operations gap.**
  `occam-onboard.mjs:93-120`. Skipped entirely (`{ok:true, skipped:true}`) if `--skip-doctor` **or**
  `CI=1`/`CI=true`. Otherwise spawns `occam-doctor.sh --skip-build` (bash, `stdio:"inherit"`) — note:
  **shells out to the `.sh` doctor unconditionally**, even on Windows, so this step silently no-ops
  with a spawn error if `bash` isn't on `PATH` on a Windows box without WSL/Git-Bash (no `.ps1`
  branch exists in this file) — then `hermes-smoke.mjs`. A non-zero exit from either sets
  `{ok:false, step:"doctor"|"hermes-smoke"}`. Per EF-020, this check's *result* only affects the
  process's own exit code at the very end of `main()` — the onboard.json write (CAP-967) and any
  `--write-config` host mutation (CAP-968) both already happened by the time `verify.ok` is
  inspected.
- **CAP-970 — Onboard config schema versioning (`onboard-schema.mjs`).**
  `SUPPORTED_ONBOARD_SCHEMA = "1.0"`. `loadOnboardConfig` (lines 144-190) reads
  `defaultOnboardPath()` (`OCCAM_CONFIG` override, else `~/.occam/onboard.json`) and classifies the
  on-disk `schema_version` into `major_mismatch` (major differs → **ignore entire file**, `env: {}`,
  stderr `onboard_schema_unsupported`), `newer_minor` (file's minor > supported minor → apply anyway,
  stderr `onboard_schema_newer` warning), or `ok`. Missing file → silently `{}` (no warning — this is
  the expected first-run state, not an error). Malformed JSON or missing `schema_version` also degrade
  to `{}` with a distinct warning code each (`onboard_parse_error`, `onboard_schema_invalid`). Every
  failure mode is soft — nothing in this module can itself abort a launch or an onboard run; it only
  ever narrows what gets merged (see CAP-355, Wave 1, for the consumer side of this same file's
  `mergeOnboardEnv`).

---

## 4. Post-install verification (`verify-install.mjs` / `verify-install.ps1`)

- **CAP-966 — `verify-install.mjs`: binary + browser + optional supply-chain checks.**
  `scripts/lib/verify-install.mjs`. Resolves `OCCAM_HOME` (env or `../..` from its own file location),
  best-effort captures a git short-SHA (silently `"unknown"` on a non-git Level-B tree — expected, not
  an error). **Hard-fails** if `resolveHostBinary(root)` (CAP-974) finds nothing — message differs by
  `--skip-build`: "pre-built MCP host binary not found in OCCAM_HOME" (Level B framing) vs "published
  MCP host binary not found — run doctor without --skip-build" (Level A framing). **Hard-fails** if
  `workers/browser-extract/lib/verify-browser-launch.mjs` is missing, then **hard-fails** if that
  script itself exits non-zero (`execSync`, `stdio:"inherit"` — real browser launch attempt, not a
  file-existence stub). Two **optional, best-effort** supply-chain checks run only if their input
  artifacts exist on disk: SLSA (`occam-mcp-provenance.intoto.jsonl` + `slsa-verifier` binary) and
  cosign (`<binary>.bundle` + `cosign` binary) — both wrapped in try/catch that only `console.warn`s
  on failure ("verify-install: SLSA/cosign verification skipped") rather than failing the whole
  script, i.e. these two checks can never block an install even when they fail, only when their
  *artifacts* are entirely absent (which is also silently skipped, not warned).
- **CAP-979 — `verify-install.ps1` is a static command-reference file, not an executable installer
  step.** `scripts/verify-install.ps1` (8 lines) contains three copy-paste PowerShell example
  invocations (`slsa-verifier verify-artifact`, `cosign verify-blob`, `syft packages`) against
  hardcoded example filenames (`occam-mcp-win-x64.exe`) that don't match any RID-specific binary name
  actually produced by this repo's build (`OccamMcp.Core.exe`/`OccamMcp.Core`, per CAP-974). No install
  path (`install.ps1`, `install.sh`, `get-ff-occam.*`, `occam-doctor.*`) ever invokes this file —
  it is reference documentation living in `scripts/` rather than `docs/`, and it is **not** the file
  that actually performs SLSA/cosign verification during a real install (that's `verify-install.mjs`,
  CAP-966). It also carries a stale `--source-uri github.com/FF-Occam/FFOccamMCP`, which does not
  match the `github.com/ContextForgeAI/occam` URI used consistently everywhere else in this audit
  (`host-install-gate.mjs:83,121`, `get-ff-occam.*` release URLs) — see EF-021.

---

## 5. Runtime consumption of onboard state (`launch-mcp-host.mjs`, `host-install-gate.mjs`, `resolve-host-binary.mjs`)

These three files are **not** install-time scripts but are the mechanism by which everything
installed/onboarded above actually gets *used* at MCP-host-launch time — included here because they
are S3-09's direct dependency chain (`NONCORE-SURFACE-MAP.md` §G lists `launch-mcp-host.mjs` under
this area) and because CAP-967/968/970's persisted state is otherwise inert without them.

- **CAP-973 — `host-install-gate.mjs`: shared fail-fast messaging, extends CAP-354.** Prior-wave
  CAP-354 (`docs-audit/subsystems/config-env.md`) documents `OCCAM_FORCE_DOTNET_RUN`'s
  binary-selection/dev-fallback *switch*; this file is the **messaging and assertion layer** built on
  top of that switch, consumed from two different call sites with two different exit behaviors:
  `launch-mcp-host.mjs` → `exitInstallBlocked`/`formatInstallBlockerMessage` (runtime launch attempt —
  "no silent dotnet run fallback", prints the exact wrapper/launcher wiring plus a copy-paste
  `get-ff-occam.sh` fix command) vs `occam-doctor.*` → `assertHostBinaryPresent`/
  `formatDoctorBinaryMissing` (build-time assertion — different copy, framed around "doctor cannot
  complete" and branches on whether an unsuitable `.NET` SDK (<10) was detected on `PATH`). Both paths
  share `assessHostInstall` (binary/workers/dotnet-major triage) as the single source of the
  diagnostic facts each message renders differently.
- **CAP-974 — `resolve-host-binary.mjs`: binary discovery, intentionally forked not imported.**
  Candidate list (`listHostBinaryCandidates`, lines 28-51): OCCAM_HOME root (Level B tarball layout)
  first, then `src/FFOccamMcp.Core/bin/Release/net10.0/<rid>/publish/` (RID-specific dotnet-publish
  layout), then the same path without a RID segment (flat publish fallback) — each checked for **both**
  base names `OccamMcp.Core` (current) and `FFOccamMcp.Core` (legacy tarballs), `.exe`-suffixed on
  Windows. The module's own header comment states it must stay **self-contained** and never import
  from `../../packages/`, because it ships inside the Level-B release tarball
  (`scripts/lib/`) which excludes `packages/` entirely — an import there would produce
  `ERR_MODULE_NOT_FOUND` and silently prevent the MCP host from ever starting on a tarball install.
  `packages/occam-mcp/lib/resolve-host-binary.mjs` is a **second, manually-synced copy** of this exact
  logic (per the comment, not verified byte-identical in this audit) — a real drift risk if one copy's
  candidate list changes without the other.

---

## Capability graph edges

```
INSTALL install.ps1|.sh (CAP-960/961)  USES        CAP-972  (install-preflight.mjs)
INSTALL install.ps1|.sh Level B         USES        CAP-962  (release-install.mjs)
CAP-962/963                             GATES       CAP-965  (destructive extract, no rollback)
INSTALL install.ps1|.sh                 CALLS       occam-doctor.* [S3-08]
INSTALL install.ps1|.sh                 CALLS       CAP-966  (verify-install.mjs)
INSTALL install.ps1|.sh -ForcePlaywright RELATES_TO CAP-206  (Wave1 S18 — browser auto-provision)
INSTALL install.ps1|.sh -ForcePlaywright RELATES_TO CAP-207  (Wave1 S18 — occam install-browser CLI)
CAP-977 (community manifest doctor gate) RELATES_TO CAP-962  (tarball manifest gate — same pattern, different artifact)
get-ff-occam.* (CAP-963)                DUPLICATES  CAP-962  (independent sha256/HTTPS logic)
get-ff-occam.* (CAP-971)                CALLS       CAP-966, occam-onboard.mjs (CAP-967), occam-connect.mjs [S3-10]
occam-onboard.mjs (CAP-967)             USES        CAP-970  (onboard-schema.mjs versioning)
occam-onboard.mjs --write-config        PRODUCES    CAP-968  (~/.cursor/mcp.json merge)
occam-onboard.mjs runVerify (CAP-969)   CALLS       occam-doctor.sh, hermes-smoke.mjs [S3-08]
launch-mcp-host.mjs                     USES        CAP-355  (Wave1 S24 — mergeOnboardEnv, OCCAM_CONFIG)
launch-mcp-host.mjs                     USES        CAP-354  (Wave1 S24 — OCCAM_FORCE_DOTNET_RUN switch)
launch-mcp-host.mjs / occam-doctor.*    USES        CAP-973  (host-install-gate.mjs messaging)
CAP-973                                 USES        CAP-974  (resolve-host-binary.mjs)
verify-install.mjs (CAP-966)            USES        CAP-974  (resolve-host-binary.mjs)
CLI `occam install-browser`             = CAP-207   (Wave1 S18 — not re-audited here)
```

---

## "INVISIBLE PRODUCT" — what an MCP-only user never sees

A user who only ever calls MCP tools through an **already-configured** host (Cursor/Hermes/etc. with
`ff-occam` already wired) never touches, sees, or is affected by any of this file's surface at
runtime, specifically:

- The entire install decision tree (Level A vs B, git-clone vs tarball, `-ForcePlaywright`, preflight
  tool checks) — invisible once `OCCAM_HOME` + a working binary exist.
- `~/.occam/onboard.json` and its schema-version drift handling (CAP-970) — silently merged under
  `process.env` by `launch-mcp-host.mjs` on every launch; a user has no MCP-visible signal that this
  file exists, is stale, or was ignored due to a schema mismatch (only a host-launcher stderr line the
  MCP client typically doesn't surface).
- The `--write-config` confirmation flow and the exact merge semantics that protect the rest of
  `~/.cursor/mcp.json` — a user just sees their host already has an `ff-occam` entry.
- Every supply-chain check in `verify-install.mjs` (SLSA/cosign/community-manifest sha256) — these run
  once at install time and leave no runtime-visible trace in any MCP tool response.
- The `PATH`-is-never-persisted fact (CAP-975) — a user inside an already-launched MCP session never
  needs `PATH` at all (the host config hardcodes absolute paths, CAP-968/CAP-963's
  `mcp-snippet.mjs`), so this operator-only friction point is completely absent from the product
  surface they interact with.
- `get-ff-occam.*`'s product-welcome banner and setup-mode prompt (CAP-963/964) — pure one-time CLI
  theater, never reachable from any MCP tool call.

---

## Engineering findings (appended to `ENGINEERING-FINDINGS.md`)

See ledger entries **EF-019, EF-020, EF-021** (below) — all three are proven by direct code reading in
this audit (no execution/repro performed).

---

## Uncertainties

1. Whether `packages/occam-mcp/lib/resolve-host-binary.mjs` is currently byte-identical to
   `scripts/lib/resolve-host-binary.mjs` (CAP-974) was **not diffed** in this audit (out of the
   assigned `scripts/`-focused scope, and `packages/` is S3-12's area) — flagged as a drift risk based
   on the source file's own comment, not independently confirmed as already-drifted.
2. `occam-onboard.mjs`'s `runVerify` shelling out to `occam-doctor.sh` (bash) unconditionally, even
   when the onboard wizard itself is running under Windows PowerShell-invoked Node — whether this is a
   real breakage on a Windows box without Git-Bash/WSL, or whether every documented Windows install
   path always has bash available by the time onboard runs, was not tested live (static-code
   observation only, noted under CAP-969).
3. Exact current byte content of `INSTALL.md`/`docs/getting-started.md` claims about PATH/rollback
   was intentionally **not** read (source of truth is code only, per Wave 3 instructions) — CAP-975's
   "no installer persists PATH" and CAP-965's "no rollback" findings are code-only claims; whether the
   public docs already disclose these honestly is a separate downstream doc-audit question, not
   answered here.

---

## Completeness verdict

All six files/areas listed for S3-09 in `NONCORE-SURFACE-MAP.md` §G were read in full:
`install.ps1`, `install.sh`, `get-ff-occam.ps1`, `get-ff-occam.sh`, `occam-doctor.ps1`/`.sh` (read as
install-path dependency, full capability ownership left to S3-08), `occam-onboard.mjs` (+ all seven of
its `scripts/lib/operator/onboard-*.mjs` dependencies: `onboard-flow`, `onboard-config`,
`onboard-schema`, `onboard-steps`, `write-mcp-config`, plus `get-install-welcome`/`get-install-copy`
for the get-ff-occam welcome banner), `scripts/verify-install.ps1`, `scripts/lib/verify-install.mjs`,
`scripts/launch-mcp-host.mjs`. Supporting dependency modules also read in full: `install-preflight.mjs`,
`release-install.mjs`, `resolve-host-binary.mjs`, `host-install-gate.mjs`, `mcp-snippet.mjs`,
`print-connection-snippet.mjs`. Twenty capabilities minted (CAP-960…979, full assigned range used).
No capability in this file was left as `UNKNOWN` — all are `proven` by direct code reading. **CLOSED**
for this range; downstream synthesis may still want a live cross-platform install run (Windows +
Linux + macOS) to confirm the RID-autodetect (CAP-963) and bash-dependency (CAP-969 uncertainty #2)
observations behaviorally, which this audit did not execute.
