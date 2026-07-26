# HOST-CAPABILITY-MATRIX (Wave 3, S3-10)

Companion to `CONNECT-PLATFORM.md`. One row per host adapter, code-derived only. `maxVerificationLevel` values per `verification.mjs::VERIFICATION_LEVELS` (0 Installed … 6 Host tool call).

| CAP | Adapter id | Product name | kind | connectionMethod | supportTier | maxVerifyLevel | Auto-connects by default? | Detect signals (code) | Mutate mechanism | Verify mechanism | Rollback mechanism |
|-----|-----------|--------------|------|-------------------|-------------|-----------------|----------------------------|------------------------|-------------------|-------------------|---------------------|
| CAP-987 | `hermes` | Hermes Agent | MCP_HOST | NATIVE_CLI | A | 5 | Yes | `which("hermes")`, `HERMES_HOME`/platform default dir, `config.yaml` exists | `hermes mcp add --command … --env … --args …` (stdin `Y\n`/`y\nY\n`) | `hermes mcp list` (name present) + `hermes mcp test <name>` (regex `Connected` + `Tools discovered: N`) | `hermes mcp remove <name>` (stdin `Y\n`) |
| CAP-988 | `openclaw` | OpenClaw | MCP_HOST | NATIVE_CLI | A | 5 | Yes | `which("openclaw")` or `npx` fallback, `~/.openclaw` dir, `openclaw.json` exists | `openclaw mcp add --command … --cwd … --arg … --env … --no-probe` then `mcp reload` | `openclaw mcp status` (name present) + `openclaw mcp probe <name> --json` (`tools >= 15`) | `openclaw mcp unset <name>` |
| CAP-989 | `claude-code` | Claude Code | MCP_HOST | NATIVE_CLI | A | 5 | Yes | `which("claude")`, `~/.claude.json` exists | `claude mcp add -s user <name> -e K=V -- <cmd> <args>` | `claude mcp get <name>` → regex `Status:.*Connected` / `√ Connected` | `claude mcp remove -s user <name>` |
| CAP-990 | `codex` | Codex CLI | MCP_HOST | NATIVE_CLI | A | 5 | Yes | `which("codex")`, `~/.codex` dir exists | `codex mcp add <name> --env K=V -- <cmd> <args>` (global registry, no scope) | `codex mcp get <name> --json` → `enabled !== false` | `codex mcp remove <name>` |
| CAP-991 | `gemini` | Gemini CLI | MCP_HOST | NATIVE_CLI | A | 5 (4 if Disabled) | Yes | `which("gemini")` (never bare `npx`), `~/.gemini` dir exists | `gemini mcp add -s user <name> <cmd> <args> -e K=V` then best-effort `mcp enable` | `gemini mcp list` text parse (`parseGeminiListEntry`); **Disabled** (untrusted folder) → `Configured — action required`, not fail | `gemini mcp remove -s user <name>` |
| CAP-992 | `cursor` | Cursor | IDE_EXTENSION | CONFIG_FILE (bespoke) | A | 1 | Yes | `which("cursor")`, `~/.cursor/mcp.json` or `<workspace>/.cursor/mcp.json` exists | Direct `commitMcpRegistration` call (not via `createConfigFileAdapter`), root key `mcpServers`, `includeCwd:true` | Re-read + `mcpEntriesEqual(entry, desired)` | `rollbackMcpRegistration` via `lastSnap`, else strip-entry fallback |
| CAP-993 | `claude-desktop` | Claude Desktop | MCP_HOST | CONFIG_FILE | A | 1 | Yes | Config path exists (classic or MSIX) OR `/Applications/Claude.app` / `Packages\Claude_*` / non-empty `Claude` roaming dir — **never** `which("claude")` (that's Claude Code) | Generic factory, root key `mcpServers`, `includeCwd:false` | Re-read + entry match | Generic factory surgical rollback (undo only Occam's entry key) |
| CAP-994 | `vscode` | VS Code / GitHub Copilot | IDE_EXTENSION | CONFIG_FILE | **B** | 1 | Only with `--only vscode` | `which("code"/"code-insiders")`, non-empty extensions/globalStorage dirs, or `settings.json` exists | Generic factory, root key **`servers`**, `VSCODE_ENTRY_CODEC` (`type:"stdio"`), `includeCwd:true` | Re-read + entry match | Generic factory surgical rollback |
| CAP-995 | `cline` | Cline | IDE_EXTENSION | CONFIG_FILE | B | 1 | Only with `--only cline` | `cline_mcp_settings.json` exists, or `saoudrizwan.claude-dev` globalStorage dir non-empty | Generic factory, root key `mcpServers`, `includeCwd:true` | Re-read + entry match | Generic factory surgical rollback |
| CAP-995 | `roo` | Roo Code | IDE_EXTENSION | CONFIG_FILE | B | 1 | Only with `--only roo` | `mcp_settings.json` exists, or `rooveterinaryinc.roo-cline` globalStorage dir non-empty | Generic factory, root key `mcpServers`, `includeCwd:true` | Re-read + entry match | Generic factory surgical rollback |
| CAP-996 | `windsurf` | Windsurf | IDE_EXTENSION | CONFIG_FILE | B | 1 | Only with `--only windsurf` | `which("windsurf")`, `mcp_config.json` exists, or non-empty `~/.codeium/windsurf` / `Programs\Windsurf` | Generic factory, root key `mcpServers`, `includeCwd:true` | Re-read + entry match | Generic factory surgical rollback |
| CAP-997 | `zed` | Zed | IDE_EXTENSION | CONFIG_FILE | B | 1 | Only with `--only zed` | `which("zed")`, `settings.json` exists | Generic factory, root key **`context_servers`**, `includeCwd:false`; **refuses JSONC** (`action:"jsonc"`) instead of destroying comments | Re-read + entry match | Generic factory surgical rollback |
| CAP-998 | `opencode` | OpenCode | AI_AGENT | CONFIG_FILE | B | 1 | Only with `--only opencode` | `which("opencode")`, `opencode.json` exists, or non-empty `~/.config/opencode` / `~/.local/share/opencode` | Generic factory, root key **`mcp`**, `OPENCODE_ENTRY_CODEC` (`type:"local"`, `command:[bin,...args]`, `environment`), `includeCwd:true` | Re-read + entry match | Generic factory surgical rollback |
| CAP-999 | `goose` | Goose | AI_AGENT | **ASSISTED** | B | n/a (plan always `assisted`) | Never (detect-only) | `which("goose")`, `config.yaml` exists | **None** — YAML `extensions:` schema has no safe JSON auto-write path; message: run `goose configure` | n/a | n/a |
| CAP-999 | `junie` | JetBrains Junie | IDE_EXTENSION | **ASSISTED** | **C** | n/a | Never (detect-only) | `which("junie")`, non-empty JetBrains data dirs (hint only) | **None** — no confirmed config path at all; explicitly marked "NEEDS LIVE VALIDATION" in source | n/a | n/a |

## Auto-connect eligibility summary

- **Auto-connects today (Tier A, no flags needed):** Hermes Agent, OpenClaw, Claude Code, Codex CLI, Gemini CLI, Cursor — 6 of 15.
- **Implemented, opt-in only (Tier B, `--only <id>` required):** VS Code/Copilot, Cline, Roo Code, Windsurf, Zed, OpenCode, Goose — 7 of 15 (Goose is additionally `ASSISTED`, so `--only goose` still only detects + prints instructions, never writes).
- **Detect-only, manual setup always (Tier C, ASSISTED, no write path exists):** Junie — 1 of 15.
- **Classified but never targeted for MCP registration at all (Tier D, `MODEL_RUNTIME`):** Ollama, llama.cpp, LM Studio, MLX (`runtimes.mjs`) — reported in `report.runtimes[]`, never in `report.hosts[]`/`report.connections[]`.

## Root-key / schema diversity (why one generic engine, not 15 bespoke writers)

| Root key | Hosts |
|----------|-------|
| `mcpServers` | Cursor (bespoke), Claude Desktop, Cline, Roo Code, Windsurf |
| `servers` | VS Code / Copilot |
| `context_servers` | Zed |
| `mcp` | OpenCode |
| *(none — CLI-owned store)* | Hermes (`config.yaml`), OpenClaw (`mcp.servers`), Claude Code (`~/.claude.json` `mcpServers`, but written via CLI not the engine), Codex (opaque global store), Gemini (opaque global store) |
| *(none — assisted only)* | Goose (`extensions:` YAML), Junie (unconfirmed) |

## Graph edges (adapter → shared primitive)

- `CAP-987,988,989,990,991 (NATIVE_CLI)|USES|scripts/lib/operator/connect/process.mjs` (`runCapture`, `which`, Windows `cmd.exe` quoting)
- `CAP-993,994,995,995,996,997,998 (CONFIG_FILE via factory)|USES|scripts/lib/operator/connect/config-file-adapter.mjs|USES|CAP-983 (config-engine)`
- `CAP-992 (Cursor, bespoke)|USES|CAP-983 (config-engine)` directly, bypassing the factory
- `CAP-994,995,995,996,997,998,999,999|USES|scripts/lib/operator/connect/paths.mjs` (appDataDir/localAppDataDir/dirHasEntries/resolveUniqueConfigPath/listWindowsClaudeMsixConfigs)
- All 15 `|USES|CAP-981` (stable launch spec) for the `desired` stdio entry, except Goose/Junie (ASSISTED — spec is computed but never written)
- All 15 `|USES|CAP-982` (ownership heuristic) before deciding `add` vs `update` vs `skip-unmanaged`

## Completeness verdict

**COMPLETE.** 15/15 adapters enumerated directly from `registry.mjs::createHostAdapters` and cross-checked against `index.mjs` exports and `NONCORE-SURFACE-MAP.md` §H. Every row above cites the exact detect/mutate/verify/rollback call sites read from source in this session. No live host validation performed (out of scope per task instructions).
