# CONNECT-PLATFORM (Wave 3, S3-10)

**Scope:** `scripts/occam-connect.mjs` + `scripts/lib/operator/connect/**` (platform core + all 15 host adapters).
**Method:** code audit only — no live host mutation/validation performed by this agent. Behavior below is read from source (+ cross-checked against `scripts/lib/operator/connect.selftest.mjs`, which encodes the same contract as executable assertions).
**CAP range:** CAP-980…999.

---

## 1. Entry point

`scripts/occam-connect.mjs` (also reachable as `occam connect` via `scripts/lib/operator/occam-cli-subcommands.mjs` → `registryId: "occam-connect"`, and auto-invoked once at the end of `scripts/get-ff-occam.ps1` / `.sh` after install).

```
node scripts/occam-connect.mjs [--json] [--detect-only] [--force] [--only IDS] [--skip-occam-verify]
Env: OCCAM_HOME, OCCAM_CONNECT=auto|off|detect, OCCAM_CONNECT_FORCE=1
```

Flow: parse args → resolve `connectMode` (policy.mjs) → `runConnect()` (orchestrator.mjs) → write `~/.occam/connect-last.json` (best-effort) → print transcript (`render.mjs`, TTY) or JSON → exit code (`0` unless a mutate-attempted host hard-failed, Occam self-verify failed, or a rollback failed).

## 2. Pipeline: detect → classify → plan → apply → verify → rollback

Implemented in `orchestrator.mjs::runConnect`, per adapter, **independently** (no global all-or-nothing transaction — CAP-980):

1. **Launch-check** — `assertLaunchable(occamHome)` (launch-spec.mjs) throws if `scripts/launch-mcp-host.mjs` is missing; aborts the whole run before touching any host.
2. **Runtime detect** — `detectAllRuntimes()` (runtimes.mjs): Ollama, llama.cpp, LM Studio, MLX — classify-only, **never** registered as MCP targets (Tier D / `MODEL_RUNTIME`).
3. **Host detect** — every adapter's `.detect()` runs regardless of connect mode, populating `report.hosts[]` (confidence high/medium/low, executable, configPath, ambiguous/candidates).
4. **Target selection** — `selectAutoConnectAdapters()` (registry.mjs): only when `mutateHosts` is true. Filters to `detected && confidence∈{high,medium} && !ambiguous && connectionMethod∈{NATIVE_CLI,CONFIG_FILE}`. Tier gating: **Tier A only**, unless the host id is named explicitly via `--only` (then Tier A+B). Tier C/D and `ASSISTED` never auto-connect.
5. **Skip explanation** — every detected-but-not-targeted host gets a reason (`describeSkippedHost`): ambiguous path, assisted-only, Tier B not explicit, or low confidence — never a silent no-op.
6. **Per-host transaction** (targets only):
   - `adapter.plan({force})` → action ∈ `add | update | noop | skip-unmanaged | assisted | ambiguous | jsonc | refuse`.
   - `add`/`update` → `adapter.apply({force})` mutates the host (CLI invocation or config-file write).
   - On apply success → `adapter.verifyHost()` (host-specific: CLI list/get/test/probe output parsing, or config-file re-read + `mcpEntriesEqual`).
   - `evaluateReadyState()` (verification.mjs) combines Occam-side level (own `tools/list` spawn, capped by `EXPECTED_MIN_TOOLS=15`) with host-side level, gated by `adapter.maxVerificationLevel`.
   - `decidePostVerifyCleanup()` (ownership.mjs) decides whether to roll back — **see EF-019 below**.
   - Cleanup executes `adapter.rollback()` (action `add` → remove) or `adapter.restoreEntry(previousEntry)` (action `update` → restore prior bytes/entry).
7. **Occam self-verify** — `verifyOccamMcp()` (occam-verify.mjs) spawns the stable launcher once per run via real MCP `initialize` + `tools/list` over stdio (Levels 2–4); independent of any specific host.
8. **Aggregate** — `aggregateConnectionReady()` (verification.mjs) rolls per-host `readyState` into one of `Ready | Almost ready | Action required | Partial | Not ready`, never over-claiming Ready when any host is still Partial.

## 3. Verification ladder (CAP-984)

`verification.mjs::VERIFICATION_LEVELS` — 0 Installed, 1 Config valid, 2 Process launches, 3 Initialize OK, 4 tools/list OK, 5 Host discovers, 6 Host-mediated tool call. `adapter.maxVerificationLevel` caps what a given connection method can ever prove:
- `NATIVE_CLI` adapters (Hermes, OpenClaw, Claude Code, Codex, Gemini) → cap at **Level 5** (host CLI reports discovery: `mcp test`, `mcp probe --json`, `mcp get`, `mcp list`).
- `CONFIG_FILE` adapters (Cursor + all 8 Wave-3 config-file hosts) → cap at **Level 1** (`CONFIG_VALID`) — a written, matching JSON entry is the ceiling; none of them can prove the IDE actually loaded it without a restart the tool cannot observe.

## 4. Ownership / safety model (CAP-982, CAP-983)

- **Never overwrite unowned entries.** `looksLikeOccamManagedEntry()` (ownership.mjs) matches only: `env.OCCAM_CONNECT_MANAGED === "occam-managed:v1"`, or command/args pointing at `occam-wrapper.sh` / `launch-mcp-host.mjs`. A pre-existing `ff-occam` entry that matches neither → `action: "skip-unmanaged"` unless `--force`.
- **Structured JSON engine** (`config-engine.mjs`) is product-agnostic: `loadMcpConfig` → `planMcpMerge` → `backupMcpConfig` (`*.occam-bak`) → `applyMergeToDoc` → `writeMcpConfigAtomic` (temp file + JSON round-trip validate + rename) → `restoreMcpConfig`. Supports arbitrary `rootKey` (`mcpServers` / `servers` / `context_servers` / `mcp`) and pluggable `EntryCodec` (stdio identity, VS Code `type:"stdio"`, OpenCode command-array).
- **JSONC refusal.** `looksLikeJsonc()` sniffs `//`/`/* */` outside string literals; if present, the engine refuses to write (`action: "jsonc"`) rather than destroy user comments — surfaced to the user as "requires user action" with a copy-paste hint. Confirmed by `testLiveValidationFindings` #4.
- **Stable launch spec** (`launch-spec.mjs`, CAP-981) is the single source of truth for what gets registered: `node <OCCAM_HOME>/scripts/launch-mcp-host.mjs`, env `OCCAM_HOME` + `OCCAM_BANNER=0` + `WT_OCCAM_BANNER=0` + `OCCAM_CONNECT_MANAGED=occam-managed:v1`; optional `occam-wrapper.sh` projection (POSIX only — Windows never gets the wrapper since hosts spawn stdio without a shell).

## 5. Policy (CAP-986)

`policy.mjs::resolveConnectMode` — CI/server hosts never get their configs mutated unless `OCCAM_CONNECT_FORCE=1`. Desktop default (`OCCAM_CONNECT` unset, not CI-like) is **`auto` with `mutateHosts:true`**, both when interactive (TTY) *and* when not (documented as "North Star one-liner" for `curl | bash` installs) — the only way to suppress this is `OCCAM_CONNECT=off` or `=detect`.

## 6. Host adapter inventory (15, all from code — CAP-987…999)

| Adapter (file) | id | kind | connectionMethod | supportTier | maxVerifyLevel | Config / registry surface |
|---|---|---|---|---|---|---|
| Hermes Agent (`adapters/hermes.mjs`) | `hermes` | MCP_HOST | NATIVE_CLI | A | 5 (HOST_DISCOVERS) | `%LOCALAPPDATA%\hermes\config.yaml` via `hermes mcp add/list/test/remove`; hand-rolled indentation-based YAML mini-parser (`parseHermesMcpServer`) |
| OpenClaw (`adapters/openclaw.mjs`) | `openclaw` | MCP_HOST | NATIVE_CLI | A | 5 | `~/.openclaw/openclaw.json` (`mcp.servers`) via `openclaw mcp add/probe/reload/unset`; falls back to `npx --yes openclaw` if not on PATH |
| Claude Code (`adapters/claude-code.mjs`) | `claude-code` | MCP_HOST | NATIVE_CLI | A | 5 | `~/.claude.json` (`mcpServers`) via `claude mcp add -s user … -- <cmd>`; verify = `claude mcp get` → `Status: √ Connected` |
| Codex CLI (`adapters/codex.mjs`) | `codex` | MCP_HOST | NATIVE_CLI | A | 5 | Global registry (no scope flag) via `codex mcp add/get --json/remove`; verify reads `enabled` from JSON |
| Gemini CLI (`adapters/gemini.mjs`) | `gemini` | MCP_HOST | NATIVE_CLI | A | 5 | `gemini mcp add -s user …`; falls back to `npx --yes @google/gemini-cli`; verify parses `mcp list` text incl. **Disabled** (untrusted-folder) → `Configured — action required`, not a hard fail |
| Cursor (`adapters/cursor.mjs`) | `cursor` | IDE_EXTENSION | CONFIG_FILE (bespoke, not via `createConfigFileAdapter`) | A | 1 (CONFIG_VALID) | `~/.cursor/mcp.json` (`mcpServers`), absolute paths, `includeCwd:true`, `requiresRestart:true` |
| Claude Desktop (`adapters/claude-desktop.mjs`) | `claude-desktop` | MCP_HOST | CONFIG_FILE (generic factory) | A | 1 | Classic (`%APPDATA%\Claude\...`) vs Windows MSIX (`Packages\Claude_*\LocalCache\Roaming\Claude\...`) — **ambiguous if both exist**, never guesses; `includeCwd:false` (host strips `cwd` on launch, would cause endless re-apply) |
| VS Code / Copilot (`adapters/vscode.mjs`) | `vscode` | IDE_EXTENSION | CONFIG_FILE | **B** | 1 | `Code/User/mcp.json`, root key **`servers`**, `VSCODE_ENTRY_CODEC` (`type:"stdio"`); refuses (ambiguous) when per-profile `mcp.json` files exist, since writing the default profile could register into a profile the user isn't using |
| Cline (`adapters/cline.mjs`) | `cline` | IDE_EXTENSION | CONFIG_FILE | B | 1 | `…/globalStorage/saoudrizwan.claude-dev/settings/cline_mcp_settings.json` (`mcpServers`) |
| Roo Code (`adapters/roo.mjs`) | `roo` | IDE_EXTENSION | CONFIG_FILE | B | 1 | `…/globalStorage/rooveterinaryinc.roo-cline/settings/mcp_settings.json` (`mcpServers`) |
| Windsurf (`adapters/windsurf.mjs`) | `windsurf` | IDE_EXTENSION | CONFIG_FILE | B | 1 | `~/.codeium/windsurf/mcp_config.json` (`mcpServers`) |
| Zed (`adapters/zed.mjs`) | `zed` | IDE_EXTENSION | CONFIG_FILE | B | 1 | `settings.json`, root key **`context_servers`**; `includeCwd:false` (not part of documented schema); real user files are JSONC → usually refused, never rewritten |
| OpenCode (`adapters/opencode.mjs`) | `opencode` | AI_AGENT | CONFIG_FILE | B | 1 | `~/.config/opencode/opencode.json`, root key **`mcp`**, `OPENCODE_ENTRY_CODEC` (`type:"local", command:[bin,...args], environment`) |
| Goose (`adapters/goose.mjs`) | `goose` | AI_AGENT | **ASSISTED** | B | n/a | YAML `extensions:` — no safe JSON auto-write path exists; detect-only + "run `goose configure`" hint |
| Junie (`adapters/junie.mjs`) | `junie` | IDE_EXTENSION | **ASSISTED** | **C** | n/a | No confirmed config path at all — hint dirs only (`.../JetBrains`); explicitly annotated "NEEDS LIVE VALIDATION" in-code |

15 adapters total (matches `NONCORE-SURFACE-MAP.md` §H and `registry.mjs::AUTO_CONNECT_HOST_IDS` = `WAVE1_HOST_IDS(2) + WAVE2_HOST_IDS(4) + WAVE3_HOST_IDS(9)`).

Wave grouping in code (`registry.mjs`): Wave 1 = hermes, openclaw. Wave 2 = claude-code, codex, gemini, cursor. Wave 3 = claude-desktop, vscode, cline, roo, windsurf, zed, opencode, goose, junie.

Rollback per adapter: NATIVE_CLI adapters shell out to the host's own remove command (`mcp remove`/`mcp unset`) and re-check via `.inspect()`; CONFIG_FILE adapters use the shared `config-engine.mjs` snapshot/restore (surgical: undo *only* the Occam entry, preserving any changes the host made to the rest of the file after apply — proven by `testLiveValidationFindings` #5).

## 7. Detailed capability graph edges

- `TOOL/CLI|occam connect (occam-connect.mjs)|USES|CAP-980` (orchestrator)
- `CAP-980|USES|CAP-981` (stable launch spec — every adapter's `desired` entry derives from this)
- `CAP-980|USES|CAP-982` (ownership guard before any add/update)
- `CAP-980|USES|CAP-983` (config-file adapters only — NATIVE_CLI adapters bypass this and shell out directly)
- `CAP-980|USES|CAP-984` (verification ladder + ready aggregation)
- `CAP-980|USES|CAP-985` (post-verify cleanup/rollback)
- `CAP-980|USES|CAP-986` (CI/desktop mutate gating, resolved once per run)
- `CAP-980|USES|CAP-987..999` (each host adapter, filtered by `selectAutoConnectAdapters`)
- `CAP-987..991 (NATIVE_CLI)|USES|scripts/lib/operator/connect/process.mjs::runCapture` (host CLI subprocess invocation, incl. Windows `cmd.exe` quoting hardening)
- `CAP-992..998 (CONFIG_FILE)|USES|CAP-983` (all go through the shared JSON merge engine, `cursor.mjs` re-implements the same primitives inline rather than calling `createConfigFileAdapter`)
- `CAP-984|USES|scripts/lib/operator/connect/occam-verify.mjs` (Occam-side stdio MCP client: `initialize` → `tools/list`, `EXPECTED_MIN_TOOLS=15` — same 15-tool contract as core MCP, Wave 1–2 `OccamToolNames`)
- `INSTALLER (scripts/get-ff-occam.ps1, .sh)|USES|TOOL/CLI|occam connect` (auto-run once post-install; result gates whether the manual MCP snippet is printed — see `docs-audit/subsystems/install-onboard.md` territory, S3-09)
- `CLI (scripts/lib/operator/occam-cli-subcommands.mjs registryId="occam-connect")|USES|TOOL/CLI|occam connect` (operator-facing subcommand wrapper — S3-07 territory)
- `CAP-987..999|PRODUCES|host config file mutation + *.occam-bak backup` (CONFIG_FILE) or `host-native CLI state file` (NATIVE_CLI)
- `CAP-980|PRODUCES|~/.occam/connect-last.json` (best-effort, read by the installer for snippet-skip logic)

## 8. Artifacts

**Consumed:** `OCCAM_HOME` env; `scripts/launch-mcp-host.mjs` (must exist); host-native binaries on `PATH` (`hermes`, `openclaw`/`npx`, `claude`, `codex`, `gemini`/`npx`, `cursor`, `code`/`code-insiders`, `windsurf`, `zed`, `opencode`, `goose`, `junie`); existing host config files (read + backed up, never destroyed without a `.occam-bak` first for CONFIG_FILE hosts).

**Produced:** mutated host config files (`mcpServers`/`servers`/`context_servers`/`mcp` root key, per host); `<config>.occam-bak` backup on every write; `~/.occam/connect-last.json` (full JSON report, written unconditionally by `occam-connect.mjs`, independent of `--json`); stdout transcript (`render.mjs`) or full JSON report (`--json`); process exit code (0/1).

## 9. "INVISIBLE PRODUCT" — what an MCP-only user never sees

A user who only ever calls Occam's MCP tools through an already-configured host never observes:

- That **detection** happened at all — model-runtime probes (Ollama/llama.cpp/LM Studio/MLX), host-binary `which()` lookups, and app-signal directory scans (e.g. `Code/User/globalStorage`, MSIX `Packages\Claude_*`) run silently on every `occam connect` invocation, including the one auto-triggered by the installer.
- The **Tier system** — 15 adapters exist, but only 6 (Hermes, OpenClaw, Claude Code, Codex, Gemini, Cursor — all Tier A) auto-connect by default; the other 9 (VS Code, Cline, Roo, Windsurf, Zed, OpenCode = Tier B; Goose = Tier B/ASSISTED; Junie = Tier C/ASSISTED) require the user to already know to pass `--only <id>`, or they are never even attempted.
- The **ownership marker** — every entry Occam writes carries `env.OCCAM_CONNECT_MANAGED=occam-managed:v1` so a later `occam connect --force` (or a version bump) can tell "this is ours" from "the user hand-edited this." A user who copies an Occam-managed entry into another host manually does not get this protection.
- The **backup file** (`*.occam-bak`) sitting next to every host config Occam ever touched.
- The **verification ladder** — a host showing "connected" in its own UI is not the same signal Occam trusts; Occam re-derives a 0–6 level per host and will report "Not ready"/"Almost ready" even when the host itself shows no error (e.g. Gemini's untrusted-folder "Disabled" state, or any restart-pending IDE).
- The **per-host independence** — one host failing verification never blocks or reverts a sibling host that already reached Ready; there is no global transaction.
- `~/.occam/connect-last.json` — a full machine-readable audit trail of the last connect run, outside both the repo and `OCCAM_HOME`.

## 10. Engineering findings (candidates, appended to `ENGINEERING-FINDINGS.md`)

See EF-019 in the shared ledger. Summary: the post-verify rollback safety net (`decidePostVerifyCleanup`) is effectively **dead for every CONFIG_FILE host with `requiresRestart:true`** (9 of 15 adapters: Cursor + all 8 Wave-3 config-file hosts), because `requiresRestart` is a **static per-adapter property**, not a dynamic "verify actually failed only due to pending restart" signal, and `decidePostVerifyCleanup` treats `requiresRestart===true` as an unconditional "preserve, never roll back" gate — even when the underlying config write demonstrably does **not** match the desired entry (`mcpEntriesEqual` false, `hostVerify.ok:false`, `hostVerify.level:INSTALLED`). See finding for full trace and repro sketch.

## 11. Uncertainties

- No live host was mutated or verified in this audit (per instructions: code audit only). All behavior above is read from source + `connect.selftest.mjs`; live findings comments embedded in the adapters themselves (dated 2026-07-25) are treated as adapter-author claims, not independently re-verified by this agent.
- `resolveHermesInvoker`/OpenClaw/Gemini `npx` fallback paths were not executed; whether `npx --yes openclaw` / `npx --yes @google/gemini-cli` actually resolve on a machine without a global install is unverified by this audit.
- Cursor's bespoke (non-factory) implementation duplicates `config-file-adapter.mjs` logic; the two are not proven byte-for-byte behaviorally identical outside of what `connect.selftest.mjs` exercises (this audit found no functional divergence by inspection, only architectural duplication — noted, not filed as a bug).

## 12. Completeness verdict

**COMPLETE.** All files under `scripts/occam-connect.mjs` + `scripts/lib/operator/connect/**` were read in full (16 platform-core files + all 15 adapter files = 31 files, matching the glob enumeration). Adapter count (15) cross-checked against `registry.mjs::createHostAdapters` object keys, `index.mjs` barrel exports, and `NONCORE-SURFACE-MAP.md` §H's list — all three agree. No adapter files exist outside `adapters/` that were missed (glob returned exactly these 31 paths, zero additional matches for `**/connect/**/*.mjs`).
