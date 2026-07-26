# Subsystem audit: Doctor / Runtime Diagnostics (S3-08)

**Wave 3 subagent S3-08. CAP ID range: CAP-940 – CAP-959 (this file only).**
**Source of truth: executable code only** — `scripts/occam-doctor.ps1`, `scripts/occam-doctor.sh`,
everything they shell out to, plus `scripts/hermes-smoke.mjs` and the `occam doctor` / `occam smoke`
CLI wiring. Docs (`AGENTS.md`, `docs/getting-started.md`, `docs/troubleshooting.md`) were not used
as evidence; any drift noted below is flagged, not corrected.

Repo: `c:\PROJECTS\FFOccamMCP`. Paths relative to repo root unless stated.

---

## 0. Executive summary

`occam-doctor` is a **single-shot preflight pipeline**, not a monitoring/health-check service — it
runs once, top-to-bottom, and either leaves the install runnable or hard-fails with an actionable
message. It is invoked three ways that all converge on the same two script files:

1. Directly: `./scripts/occam-doctor.ps1` / `.sh`.
2. Via the unified operator CLI: `occam doctor` → `occam-cli-dispatch.mjs` (delegate `"shell"`) —
   which also **silently injects `--skip-build`** on a Level B (tarball) install.
3. Indirectly, as step 2 of `occam refresh`/`occam restart` (`occam-refresh-host.mjs`) — stop host →
   re-run doctor → optional `hermes-smoke.mjs`.

The doctor script mixes three different failure postures in one linear run, none of it labeled as
such in the script itself:

- **Hard gates** (`Write-Error` / `exit 1`, `set -euo pipefail` propagates) — stop the whole run.
- **Bootstrap/repair steps** — mutate the environment/filesystem so a fresh clone becomes runnable
  (npm install, dotnet publish, chromium install).
- **Advisory self-tests** — run unit-test-style checks and print a `warning:` line on failure, but
  never stop the run or return a non-zero doctor exit code.

`hermes-smoke.mjs` (`occam smoke`) is a separate, later, optional step: a **read-only,
process-isolated MCP fidelity check** — it never touches disk or the build, it spawns a throwaway
`launch-mcp-host.mjs` subprocess, speaks real JSON-RPC (`initialize` → `tools/list` →
`tools/call occam_probe`), and asserts on the *live* wire contract, not on files.

**Most consequential finding:** `hermes-smoke.mjs` hard-codes `EXPECTED_TOOLS = 15` and asserts
strict equality against `tools/list`. That count is only correct under the default
`OCCAM_PROFILE=full` with all opt-ins off. Under any of the operator-supported configurations this
repo's own Wave 1/2 audit already proved exist (`OCCAM_PROFILE=reader|researcher|auditor` → 7/9/12
tools; any of `OCCAM_BATCH_MCP` / `OCCAM_WATCH_MCP` / `OCCAM_CONSENSUS_MCP` / `OCCAM_ATLAS_MCP=1` →
+1..+3 tools on top of the profile count), `occam smoke` will **FAIL on a correctly functioning
host** — see EF-024 (new, filed by this report).

**Also confirmed (not re-filed — already on the ledger from parallel Wave-3 agents):**
`occam-refresh-host.mjs`'s reload hint hard-codes a stale "9 occam_* tools" count (current is 15) —
see EF-019 (filed by S3-07) and the related "14 occam_*" stale count in `get-install-copy.mjs`, see
EF-023 (filed by S3-09). Both are distinct from EF-024: those are simply outdated numbers; EF-024 is
that `hermes-smoke.mjs`'s number, while currently accurate for the default config, has **no
profile/opt-in awareness at all** and is wrong by design under supported non-default configs.

---

## 1. Diagnostic vs. repair/bootstrap classification

| Step (doctor script, in order) | Class | Blocking on failure? |
|---|---|---|
| `OCCAM_HOME` resolution + stamp (CAP-940) | Diagnostic (side-effecting env stamp) | n/a |
| net10.0 csproj guard (CAP-942) | **Hard gate** | Yes — unless no csproj (prebuilt install) |
| `node` on PATH check | Hard gate | Yes |
| `workers/package.json` presence check | Hard gate | Yes |
| `npm install` if `node_modules` missing (CAP-943) | **Bootstrap/repair** | Yes (npm failure propagates) |
| Playwright bundled-skip decision (CAP-944) | Diagnostic (branch only) | n/a |
| `playwright install-deps chromium`, Linux+root only (CAP-946) | **Bootstrap/repair** | No (bash: `\|\| echo WARN`, continues) |
| Playwright cache path print (CAP-945) | Diagnostic (informational) | n/a |
| egress-proxy selftest (CAP-947) | Advisory self-test | No — warning only |
| pdf-extract selftest (CAP-948) | Advisory self-test | No — warning only |
| private-ip / SSRF selftest (CAP-949) | Advisory self-test | No — warning only |
| Chromium launch probe + one auto-install + one retry (CAP-950) | **Repair, then hard gate** | Yes if still failing after the one retry |
| Community manifest sha256 verify (CAP-951) | Hard gate (integrity, not repair) | Yes |
| Publish-lock advisory (Windows only) (CAP-953) | Diagnostic (warns; does not stop, does not kill) | No |
| MSVC dev-env auto-load (CAP-954) | Bootstrap (process-local, not persisted) | No — warns, lets `dotnet publish` fail naturally |
| `dotnet publish` + copy to `OCCAM_HOME` root (CAP-956) | **Bootstrap/repair** | Yes, unless `--skip-build` |
| `assert-host-binary.mjs` final gate (CAP-957) | Hard gate | Yes |
| Completion banner + onboarding hints (CAP-958) | Diagnostic (informational only) | n/a |

Net: doctor is **~40% repair/bootstrap** (npm install, chromium install, dotnet publish + binary
copy, install-deps on CI containers) and **~35% hard integrity/version gates** that refuse to
proceed rather than fix anything (net10 guard, manifest sha256, final binary presence), with the
remaining **~25%** being non-blocking advisory self-tests that only ever print a warning.

---

## 2. Capabilities (CAP-940 – CAP-959)

### CAP-940 — `occam-doctor.ps1` / `.sh` unified preflight pipeline
`scripts/occam-doctor.ps1`, `scripts/occam-doctor.sh`. One linear script per platform, same step
order and same script dependencies (§1 table); PowerShell uses `$ErrorActionPreference = "Stop"` +
explicit `$LASTEXITCODE` checks after every `node` call, Bash uses `set -euo pipefail`. Reachable
three ways (direct invocation, `occam doctor` CLI verb, `occam refresh` step 2). **Status: PROVEN.**

### CAP-941 — `OCCAM_HOME` resolution + env stamping as a doctor side effect
Both scripts resolve `root` (arg-independent: `$env:OCCAM_HOME` else parent of `scripts/`) and then
**write it back** into the process env (`$env:OCCAM_HOME = $root` / `export OCCAM_HOME="$ROOT"`)
before any child `node`/`dotnet` call, so every downstream helper sees a normalized absolute path
even if the caller passed a relative one or none at all. Reuses the same three-tier resolution
contract as core `WorkerPaths.Resolve()` (CAP-350, Wave 1 `config-env.md`) but is an independent
Node/PowerShell-side implementation, not a call into the C# resolver — two parallel
`OCCAM_HOME`-resolution implementations (script-side vs. host-side) that must be kept in sync by
convention, not by shared code.

### CAP-942 — net10.0 `TargetFramework` downgrade guard
`scripts/lib/assert-net10-csproj.mjs`. Regex-checks
`src/FFOccamMcp.Core/FFOccamMcp.Core.csproj` for `<TargetFramework>net10.0</TargetFramework>`; hard
exit 1 with a revert hint if changed. **Deliberately no-ops (exit 0)** when the csproj file itself
is absent — the comment is explicit that a Level B/tarball install has no `src/` and this is not an
error case, only a git-clone build context is guarded. First doctor step, before the `node` PATH
check even runs (this script itself is what proves `node` works).

### CAP-943 — Workers npm workspace bootstrap
Conditional `npm install --no-fund --no-audit` in `workers/` (npm workspace root), gated purely on
`node_modules` directory absence — **not** on lockfile hash or staleness, so a corrupted or
out-of-date `node_modules` from a prior partial install is not detected or repaired by doctor; only
a fully-missing directory triggers reinstall.

### CAP-944 — Playwright bundled-chromium skip decision
Both scripts compute `skipPlaywrightBundled` from `OCCAM_BROWSER_CHANNEL` (case-insensitive;
`chrome`/`msedge`/`chrome-beta`/`msedge-beta` trigger skip, `chromium` explicitly does not) OR
`OCCAM_BROWSER_EXECUTABLE_PATH`/`OCCAM_CHROME_PATH` being set. When skipped, doctor never even prints
the Playwright cache path — purely a branch that gates CAP-945/946, does not gate CAP-950 (the
launch probe below still runs and is the actual arbiter for system-browser configs too, via
`usesSystemBrowser()`).

### CAP-945 — Playwright cache path diagnostic
`scripts/lib/playwright-cache.mjs` — pure informational print (Windows `%LOCALAPPDATA%\ms-playwright`,
macOS `~/Library/Caches/ms-playwright`, Linux `~/.cache/ms-playwright`, or `PLAYWRIGHT_BROWSERS_PATH`
/ `OCCAM_PLAYWRIGHT_BROWSERS_PATH` override). File header comment explicitly claims this must stay
in sync with `PlaywrightEnvironment.cs` — a doc-in-code cross-file contract, not enforced by any
test. Only reached when CAP-944 does not skip.

### CAP-946 — Linux-root `install-deps chromium` (bash doctor only, asymmetric with `.ps1`)
Bash-only branch: `uname -s == Linux && id -u == 0` → runs
`npx playwright install-deps chromium` in `workers/browser-extract`, swallowing failure into a
`WARN:` echo (`|| echo …`, does not trip `set -e`). Comment states this targets CI containers
specifically (dev machines — Windows/macOS/non-root Linux — never hit it, and don't need it).
**No PowerShell equivalent exists** — not a gap, `apt`/root semantics don't apply on Windows.

### CAP-947 — Egress-proxy selftest gate (advisory, conditional)
`workers/shared/lib/egress-proxy.selftest.mjs`, run only when `OCCAM_HTTP_PROXY` or
`OCCAM_HTTPS_PROXY` is set. Unit-asserts `validateProxyUrl`, `shouldBypassProxy`,
`resolveProxyForUrl`, `redactProxyUrl`, env-var parsing (`readEgressConfig`) — pure logic, makes no
network call. Failure prints a `warning:` (both scripts) plus a pointer to the full gate
(`L2_EGRESS_OK`) and a "corporate PAC/NTLM (v2 sidecar)" note; **never blocks doctor**.

### CAP-948 — PDF-extract selftest gate (advisory, unconditional)
`workers/shared/lib/pdf-extract.selftest.mjs`, run unconditionally whenever the file exists (no env
gate, unlike CAP-947/949's env/file gates — this one always runs). Builds a hand-crafted minimal PDF
byte string in-memory and round-trips it through `extractPdfMarkdown` plus the detection helpers
(`isPdfContentType`, `looksPdfUrl`, `shouldTryPdf`, `hasPdfMagic`) — a true end-to-end unit test, not
a mock. Failure → `warning: PDF transcode may be unavailable (is 'unpdf' installed?)`; never blocks.

### CAP-949 — SSRF / private-IP guard selftest gate (advisory, unconditional)
`workers/shared/lib/private-ip.selftest.mjs`, run unconditionally. Exercises the same guard already
audited as core (`private-ip.mjs`, CAP-151/CAP-155 in `network-fetch-proxy.md`) — IPv4 private
ranges, IPv4-mapped-IPv6 folding (bypass-guard regression), and the IPv6-native ranges (`::1`,
`fc00::`, `fe80::`) — plus `resolveAndValidateHost` rejection behavior. Doctor is the **only place**
this selftest is wired into an operator-visible flow outside the full gate; failure here is a
warning, not a block, so an operator running only `occam doctor` (not the full `l0-gate`) could ship
with a degraded SSRF guard and only see a yellow warning line, never a hard stop.

### CAP-950 — Chromium launch-probe with one-shot auto-install-and-retry
`workers/browser-extract/lib/ensure-chromium-usable.mjs` (`ensurePlaywrightChromiumUsable`), invoked
by doctor as `node lib/ensure-chromium-usable.mjs` from inside `workers/browser-extract`. Design
(explicit in the file's own header comment): **an actual `chromium.launch()` + `newPage` +
`goto("about:blank")` is the only source of truth** — a resolvable `executablePath` does not prove
the runtime Playwright will actually select (headless-shell vs. full chromium) is present. Only a
launch failure matching `MISSING_RUNTIME_PATTERNS` (Playwright's own "Executable doesn't exist" /
"npx playwright install" text, explicitly excluding `install-deps`-style missing-system-library
errors via `SYSTEM_DEPS_PATTERN`) triggers `playwright install chromium` — and only **once**; if the
retry launch still fails, doctor hard-fails (`Write-Error "browser runtime unavailable"` / bash
`exit 1`) with no second retry. System-library failures (`libnspr4`, sandbox, permissions) are never
auto-remediated — reported as-is so the operator installs OS packages themselves (see CAP-946's
narrower, root-only auto-remediation of exactly this class of failure). `allowInstall` defaults to
`!usesSystemBrowser()` — if the operator has configured a system Chrome/Edge channel, a missing
runtime is reported as `system_browser_missing` and doctor never attempts a Playwright-managed
install on top of it.

### CAP-951 — Community playbook manifest sha256 integrity gate
`scripts/lib/verify-community-manifest.mjs`. Verifies every row in
`profiles/playbooks/community/manifest.json` against the on-disk file (LF-normalized SHA-256, so
CRLF checkouts still match) and, in the other direction, flags any on-disk community `*.json` file
**not** listed in the manifest ("orphan playbook") as an error too — a two-way integrity check, not
just tamper detection. Any single mismatch/missing-row/orphan → non-zero exit, doctor hard-fails.
This is a hard gate, not a self-test — it validates data, not code.

### CAP-952 — Runtime-identifier (RID) resolution, single source of truth
`scripts/lib/resolve-rid.mjs` — `win32`→`win-x64`/`win-arm64`, `darwin`→`osx-x64`/`osx-arm64`,
`linux`→`linux-x64`/`linux-arm64`; throws on anything else. Doctor uses it to pick the
`dotnet publish -r <rid>` target and to locate the expected publish output path; also independently
reused by `stop-occam-processes.mjs` (publish-exe lock/PID detection) and
`resolve-host-binary.mjs`/`resolve-host-binary` candidate list — one function, ≥3 call sites, no
duplication (contrast with CAP-941's `OCCAM_HOME` resolution, which *is* duplicated).

### CAP-953 — Publish-lock advisory (Windows-only, best-effort, non-blocking)
`occam-doctor.ps1` only (no bash equivalent). Before publishing, opens the existing publish exe with
exclusive `ReadWrite`/`None`-share access as a lock probe; on failure, falls back to
`Get-Process -Name "OccamMcp.Core"` to list PIDs, and prints a **warning** (reload MCP in Cursor,
etc.) — it does **not** kill anything itself (that is `occam-refresh-host.mjs`'s job, out of this
report's scope) and does **not** skip the subsequent `dotnet publish` call, which will then fail on
its own if the file truly is locked. Purely advisory UX, not a real gate.

### CAP-954 — MSVC toolchain auto-load for Native AOT publish (Windows-only)
`scripts/lib/load-vs-dev-env.ps1` (`Enter-OccamVsDevEnv`), dot-sourced only from the `.ps1` doctor
right before `dotnet publish`. No-ops (returns `$true`) if not Windows or if `link.exe` already
resolves on PATH. Otherwise uses `vswhere.exe` to find a VS install with the
`Microsoft.VisualStudio.Component.VC.Tools.x86.x64` component, imports
`Microsoft.VisualStudio.DevShell.dll`, and calls `Enter-VsDevShell`; also manually re-prepends the VS
Installer directory to `PATH` because AOT's ILCompiler shells out to bare `vswhere.exe` by name,
which `Enter-VsDevShell` alone does not put on PATH (explicit comment). On any failure to locate VS,
`Write-Warning` only — returns `$false` and lets the subsequent `dotnet publish` fail with its own
native-link error rather than pre-empting it with a clearer message. **No bash equivalent** — POSIX
build assumes `clang`/`ld` are already present, correctly out of scope.

### CAP-955 — `--skip-build` / Level B (tarball) doctor mode, auto-injected by the CLI
Doctor accepts `-SkipBuild` (ps1) / `--skip-build` (sh) to skip the entire `dotnet publish` step and
go straight to `assert-host-binary.mjs --skip-build` (CAP-957) against whatever binary already
exists. The `occam doctor` CLI wrapper (`occam-cli-dispatch.mjs`, `isLevelBInstall()`) **silently
appends `--skip-build`** whenever it detects a `VERSION` file with no `.git` directory at
`OCCAM_HOME` — i.e. an operator who typed exactly `occam doctor` on a release-tarball install gets a
different effective command than what they typed, with no confirmation printed before dispatch (the
flag only becomes visible in the downstream doctor script's own stdout, if at all).

### CAP-956 — `dotnet publish` + canonical root-binary copy (bootstrap/repair core)
`dotnet publish src/FFOccamMcp.Core -c Release -r <rid>`, then (ps1 only — see Uncertainties)
copies the publish-dir binary to `OCCAM_HOME/OccamMcp.Core[.exe]`. This root copy is not cosmetic:
`resolve-host-binary.mjs`'s candidate list (shared by `launch-mcp-host.mjs`, `hermes-smoke.mjs`'s
launcher, and `assert-host-binary.mjs`) checks the **root-level name first**, before any
`bin/Release/.../publish/` path — so doctor's copy step is what makes the fast-path candidate exist
at all; without it every launch would fall through to the slower/second-tier publish-dir candidates.

### CAP-957 — `assert-host-binary.mjs` final presence gate
`scripts/lib/host-install-gate.mjs` (`assertHostBinaryPresent` via `assert-host-binary.mjs`),
doctor's last hard gate, run in **both** the build and `--skip-build` paths (different arg, same
function). On success, silently returns the resolved path. On failure, prints a long, context-aware
actionable message: distinguishes `--skip-build` (points at `get-ff-occam.sh`, "git clone alone does
NOT ship the binary") from a full-build failure (checks installed `dotnet --version` major and
explicitly refuses to suggest downgrading to a `.NET 8` SDK), and always includes the
`hermes-smoke.mjs` command as the "Verify:" step — i.e. **doctor's own final error message
recommends the next tool this report also covers (CAP-959).**

### CAP-958 — Doctor completion banner + onboarding hand-off
Final `doctor: OK` (green) plus: resolved `OCCAM_SESSIONS_ROOT` path (default
`~/.occam/sessions`), the `occam onboard` hint, a raw `print-connection-snippet.mjs` fallback
command, a repeated reminder of the canonical launcher (`launch-mcp-host.mjs`), an explicit
anti-pattern warning ("Avoid on git clone: `packages/occam-mcp/bin/occam-mcp.js` without
`OCCAM_HOME`"), and "Reload MCP servers in your host after saving config." Pure diagnostic output —
no exit-code effect, doctor has already succeeded by this point.

### CAP-959 — `hermes-smoke.mjs` stdio MCP fidelity smoke
`scripts/hermes-smoke.mjs` (`occam smoke`). Distinct execution model from every doctor step above:
spawns a **fresh** `launch-mcp-host.mjs` child process (own stdio pipes, `Logging__LogLevel__Default:
"None"`, `WT_OCCAM_BANNER: "0"`), speaks real MCP JSON-RPC over that pipe
(`initialize` → `notifications/initialized` → `tools/list` → `tools/call occam_probe` against a
fixed MDN URL), and asserts: (a) exactly `EXPECTED_TOOLS = 15` names starting with `occam_` in
`tools/list`, (b) the probe call returns `ok:true`, and (c) `agentMeta`/`agentHints.suggestedNextTool`
is present on the probe response. Structured JSON report (`{ok, steps, errors}`) is the **last**
line of stdout; process exit code mirrors `report.ok` (0 pass / 1 fail). 120s hard kill timer,
60s per-request timeout. Never touches disk, never rebuilds — purely a live-wire read. **See EF-019 —
the hard-coded `15` is provably wrong under two classes of supported operator configuration
(`OCCAM_PROFILE` ≠ `full`, or any opt-in MCP flag set).**

---

## 3. Exit semantics (both scripts, consolidated)

| Exit path | `.ps1` mechanism | `.sh` mechanism | Blocking? |
|---|---|---|---|
| net10 guard fail | `process.exit(1)` in helper → `if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }` | `set -e` propagates node's exit 1 | Yes |
| `node` missing | `Write-Error` (terminating, `$ErrorActionPreference=Stop`) | `exit 1` explicit | Yes |
| `workers/package.json` missing | `Write-Error` | `exit 1` explicit | Yes |
| `npm install` failure | Uncaught non-zero from `npm` under `Stop` policy propagates | `set -e` propagates | Yes |
| `install-deps` failure (Linux root) | n/a (no such branch) | Caught, `\|\|` → warn, continues | **No** |
| egress/pdf/private-ip selftest failure | `$LASTEXITCODE -ne 0` checked but only logged as `Write-Host … Yellow` warning, **not** re-raised | `if ! node …; then echo warning; fi` — swallowed | **No** |
| Chromium launch-probe failure (after retry) | `Write-Error "browser runtime unavailable"` | `exit 1` explicit | Yes |
| Community manifest verify failure | `Write-Error` | `exit 1` explicit | Yes |
| Publish-lock detection | Advisory `Write-Host … Yellow` only | n/a (Windows-only step) | No |
| `dotnet publish` failure | `Write-Error` (custom message re: locked exe) | Implicit (no post-check in `.sh`, relies on `set -e`) | Yes |
| Publish output missing but `dotnet publish` reported 0 | `Write-Error "publish output missing"` | Explicit `-f` check → `exit 1` + delegates to `assert-host-binary.mjs` for the message | Yes |
| Final binary-presence gate | `if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }` | Implicit (last statement of the script; its own exit code becomes the script's) | Yes |
| Overall success | Falls through to `doctor: OK` banner, implicit exit 0 | Same | — |

**Consolidated rule:** every step in the "hard gate" and "bootstrap/repair" rows of §1 is fail-fast
and propagates a **non-zero, but not step-specific**, exit code (mostly plain `1`; there is no
distinct exit-code taxonomy — a caller cannot distinguish "csproj downgraded" from "chromium
unavailable" from "dotnet not on PATH" by exit code alone, only by re-reading stderr text). Every
step in the "advisory self-test" row is fully non-blocking and has **zero effect on doctor's own
exit code**, regardless of platform.

`hermes-smoke.mjs` has its own, separate, binary contract: `report.ok` (all steps in `report.errors`
empty) ⇒ exit 0 and a `{"ok":true,...}` JSON line; any error (missing tools, probe failure, MCP
timeout, launch crash) ⇒ exit 1 and `{"ok":false,"errors":[...]}`. It is the only script in this
report whose stdout is machine-parseable by contract (last line = JSON) — doctor's stdout is
human-oriented log lines only.

---

## 4. `occam smoke` vs. the L0 gate vs. `corpora/l0-smoke.jsonl` (boundary note, brief per scope)

Three unrelated things share the word "smoke" in this codebase, which is worth naming explicitly
since an agent reading only filenames could conflate them:

1. **`occam smoke` / `hermes-smoke.mjs`** (this report) — one live stdio MCP round-trip, no corpus,
   no assertions on markdown quality, checks *shape* (tool count + `ok` + `agentHints`) not content.
2. **`corpora/l0-smoke.jsonl` + `run-l0-fast.ps1`** — a URL corpus consumed by the `benchmarks/l0-gate`
   console app (out of S3-08 scope — maintainer/CI territory per `NONCORE-SURFACE-MAP.md` §K),
   asserts extraction *quality* against golden expectations, not wire-protocol shape.
3. **Doctor's own advisory "selftests"** (CAP-947/948/949) — unit tests of pure JS helper modules,
   no MCP, no network, no corpus.

None of the three invoke each other. `hermes-smoke.mjs` and the gate corpus could theoretically
diverge in "is the host healthy" verdicts (e.g. gate green but `occam smoke` red under a non-default
profile, per EF-019) with no cross-check between them.

---

## 5. "INVISIBLE PRODUCT" — what an MCP-only user never sees

An agent that only ever calls MCP tools (`tools/call occam_*`) has **no visibility whatsoever** into
this entire subsystem:

- There is no MCP tool that runs a doctor check, reports Playwright/dotnet/workers health, or
  triggers a chromium reinstall — all of CAP-940–958 is operator-CLI/shell-only, unreachable from
  inside a live MCP session.
- The **one-shot, no-further-retry** browser repair policy (CAP-950) is invisible: if a chromium
  launch fails for a reason other than "missing runtime" (e.g. a half-corrupted cache from an
  interrupted prior install), doctor will not detect or repair it — an MCP agent just sees
  `workers_unavailable` failures on `occam_transcode`/`occam_probe` forever, with no self-heal path
  and no indication *why*, until an operator manually reruns doctor or clears the Playwright cache.
- The AOT binary an MCP session is actually running against is **silently copied** from a deep
  `bin/Release/net10.0/<rid>/publish/` path to the `OCCAM_HOME` root by doctor (CAP-956) specifically
  because the resolver checks the root path first — an agent inspecting "which binary am I talking
  to" via any host-side identity field has no way to know this copy-then-prefer-root indirection
  exists.
- `occam smoke`'s pass/fail is **never surfaced to the agent's own session** — it spawns a
  completely separate, disposable host subprocess; a green `occam smoke` says nothing about the
  actual live MCP host the agent is connected to right now (different process, possibly different
  binary if doctor hasn't been rerun since a rebuild).
- The advisory self-tests (CAP-947/948/949) mean an operator can run `occam doctor`, see a solitary
  yellow "warning: private-ip selftest failed" line among dozens of other lines, ignore it, and ship
  with a degraded SSRF guard — the MCP session itself has zero indication downstream that this
  happened; no failure code or receipt field reflects "SSRF selftest status."
- Level B (tarball) installs get a **behaviorally different `occam doctor`** (`--skip-build`
  silently injected, CAP-955) than the command the operator actually typed, with the divergence
  visible only if they read the doctor script's own log output closely.

---

## 6. Capability graph edges

```
CLI("occam doctor")           |USES|   CAP-940 (occam-doctor.ps1/.sh)
CLI("occam doctor")           |USES|   CAP-955 (Level B --skip-build auto-inject)
CAP-940                       |USES|   CAP-941 (OCCAM_HOME stamp)
CAP-940                       |USES|   CAP-942 (net10 guard)
CAP-940                       |USES|   CAP-943 (npm install bootstrap)
CAP-940                       |USES|   CAP-944 (playwright-skip decision)
CAP-944                       |GATES|  CAP-945 (cache path print)
CAP-944                       |GATES|  CAP-946 (Linux install-deps, bash only)
CAP-940                       |USES|   CAP-947 (egress selftest)
CAP-940                       |USES|   CAP-948 (pdf-extract selftest)
CAP-940                       |USES|   CAP-949 (private-ip selftest)
CAP-949                       |REUSES| CAP-151, CAP-155 (network-fetch-proxy.md — SSRF guard core logic)
CAP-940                       |USES|   CAP-950 (chromium launch-probe + repair)
CAP-950                       |REUSES| Playwright browser-launch options (browser-extract subsystem, Wave 1/2 — not re-audited here)
CAP-940                       |USES|   CAP-951 (community manifest sha256 gate)
CAP-940                       |USES|   CAP-952 (RID resolution)
CAP-952                       |SHARED_BY| CAP-956, CAP-953-adjacent stop-occam-processes.mjs (S3-07 territory), resolve-host-binary.mjs
CAP-940 (.ps1 only)           |USES|   CAP-953 (publish-lock advisory)
CAP-940 (.ps1 only)           |USES|   CAP-954 (MSVC dev-env auto-load)
CAP-940                       |USES|   CAP-956 (dotnet publish + root copy)
CAP-940                       |USES|   CAP-957 (assert-host-binary final gate)
CAP-957                       |RECOMMENDS| CAP-959 (points operator at hermes-smoke in its own error text)
CAP-940                       |ENDS_WITH| CAP-958 (completion banner)
CLI("occam smoke")            |USES|   CAP-959 (hermes-smoke.mjs)
CAP-959                       |SPAWNS| Program.cs/StdioMcpTransport (Wave 1/2 runtime-mcp — reused, not re-audited)
CAP-959                       |ASSERTS_ON| CAP-008 / CAP-384 (OCCAM_PROFILE tool-count contract, Wave 1) — see EF-024, mismatch not enforced
CLI("occam refresh"/"restart")|USES|   CAP-940 AND CAP-959 (S3-07 territory — `occam-refresh-host.mjs` composes both; not re-audited as its own capability here)
```

---

## 7. Engineering findings

**New finding filed by this report:** EF-024 (appended to `ENGINEERING-FINDINGS.md`).

- **EF-024 (BUG-CANDIDATE, proven in code):** `hermes-smoke.mjs` hard-codes
  `EXPECTED_TOOLS = 15` and asserts strict equality; the smoke subprocess inherits the caller's full
  `process.env`, so any operator running `occam smoke` (or `occam refresh --smoke`) with
  `OCCAM_PROFILE` set to anything other than `full`, **or** any one of `OCCAM_BATCH_MCP` /
  `OCCAM_WATCH_MCP` / `OCCAM_CONSENSUS_MCP` / `OCCAM_ATLAS_MCP=1` set, gets a false-negative smoke
  FAIL against a correctly functioning host. Both configurations are explicitly supported per
  `AGENTS.md` and proven present in code by Wave 1/2 (`PROFILE-TOOL-MATRIX.md`, CAP-008/CAP-384).

**Already on the ledger (parallel Wave-3 agents, not re-filed):** EF-019 (S3-07,
`occam-refresh-host.mjs` stale "9 occam_* tools" hint) and EF-023 (S3-09, `get-install-copy.mjs`
stale "14 occam_*" count) — both discovered independently while auditing S3-08's own doctor→smoke
composition, cross-referenced above in §0 rather than duplicated on the ledger.

---

## 8. Completeness verdict

**Complete for assigned scope** (`scripts/occam-doctor.ps1`/`.sh` + every script/helper they call;
`hermes-smoke.mjs`; the `occam doctor`/`occam smoke` CLI wiring; exit-code semantics for both).
Not re-audited here (owned by other Wave-3 agents per `NONCORE-SURFACE-MAP.md`): `occam-refresh-host.mjs`
composition logic and `stop-occam-processes.mjs` process-kill mechanics (S3-07), `occam-onboard.mjs`
(S3-09), `install.ps1`/`.sh` and `get-ff-occam.*` (S3-09), `check-public-mcp-contract.mjs` (S3-07),
browser-extract's own recipe/launch-option internals beyond the launch-probe contract (Wave 1/2,
browser-workers.md).

---

## 9. Uncertainties

- Whether `occam-doctor.sh` also copies the publish binary to `OCCAM_HOME` root was confirmed by
  direct read (`cp -f "$PUBLISH_BIN" "$ROOT/OccamMcp.Core"` + `chmod +x`) — **confirmed present**,
  not asymmetric; CAP-956 applies to both platforms equally (corrected from an initial assumption
  while drafting this report that it might be ps1-only).
- Whether any CI pipeline actually exercises the Linux-root `install-deps` branch (CAP-946) in
  practice was not verified — no `.github/workflows/*.yml` was read as part of this report (out of
  scope; packaging/CI is S3-12).
- Whether `OCCAM_LOG=1` or other verbosity flags change any doctor exit-code behavior (as opposed to
  just log verbosity) was not found in either script — `OCCAM_LOG` appears only in
  `assert-net10-csproj.mjs`'s own verbose-print branch (CAP-942), not doctor-wide.
