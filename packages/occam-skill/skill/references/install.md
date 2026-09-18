# Install — wire FF-Occam MCP + this skill

> **If you are an automated agent:** read this entire file **before** any shell command.
> Normative install gate (repo root): `INSTALL.md` in the FF-Occam repository.

Host-neutral steps for the public GitHub release tree (`https://github.com/ContextForgeAI/occam`).

---

## DO NOT build from source (agents)

**Install from the release tarball only.** Source builds are **not** supported for agents
(Hermes, Cursor, Claude Code, etc.).

### Forbidden commands (stop — do not run)

| Forbidden | Why |
|-----------|-----|
| `dotnet build` | Builds the SDK project — needs .NET 10; not the install path |
| `dotnet publish` | Same — AOT publish is maintainer/CI, not agent install |
| `dotnet run --project …` | Same — never a substitute for the release host binary |
| `git clone` + doctor without a published binary | Fails or pulls you into the contributor SDK path |
| Edit `FFOccamMcp.Core.csproj` / force `net8.0` | Wrong framework — doctor fails |
| `npm ci` / `npm run bootstrap` at repo root | **Does not exist** |
| `packages/occam-mcp/bin/occam-mcp.js` on a git clone | Exits immediately |
| Invent an alternate install (npx as GA, manual extract-only) | Not the guarded path |

**If the release tarball is missing or download fails — STOP and tell the user.**
Do **not** build from source. Do **not** install .NET “to fix it.”

### Correct agent path

1. Detect platform (bootstrap does this via `uname` / Windows RID).
2. Download the matching release tarball + manifest (`get-ff-occam.sh` / `.ps1`).
3. Extract → doctor (`--skip-build`) → smoke.
4. Wire MCP with `OCCAM_HOME` set.

```bash
# Linux x64 / macOS Apple Silicon
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

```powershell
# Windows x64
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

Needs **Node 20+**. **No .NET SDK.**

---

## Agent hard gates (do not skip)

| Forbidden | Why |
|-----------|-----|
| Edit `FFOccamMcp.Core.csproj` / `TargetFramework` → net8.0 | Project is **net10.0 Native AOT** — doctor fails; use release tarball |
| Install .NET 8 to “fix” build | `NETSDK1045` — wrong SDK; agents must not build |
| `packages/occam-mcp/bin/occam-mcp.js` on a **git clone** | Exits immediately — use `scripts/occam-wrapper.sh` |
| `npm ci` / `npm run bootstrap` at repo root | **Does not exist** |
| `git clone` + `occam-doctor --skip-build` without tarball | No `OccamMcp.Core` — doctor **fails** |
| MCP `env: {}` without `OCCAM_HOME` | Workers won't resolve — `workers_unavailable` |
| `OPENROUTER_API_KEY` in Occam MCP `env` | Hermes LLM key — not the MCP host |

Stack: **release AOT host** + **Node 20+** workers. Expect **11** tools under default `OCCAM_PROFILE=reader`, or **18** under `OCCAM_PROFILE=full`, after smoke.

---

## Canonical install (one path)

### Linux / macOS

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
export OCCAM_HOME="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
node "$OCCAM_HOME/scripts/hermes-smoke.mjs"
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
$env:OCCAM_HOME = if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else { Join-Path $env:USERPROFILE ".local\share\ff-occam" }
node "$env:OCCAM_HOME\scripts\hermes-smoke.mjs"
```

Expect **exit 0** and **18** core tools when `OCCAM_PROFILE=full` (smoke default). Default product profile is **reader=11**. Full details: repo-root `INSTALL.md` · `docs/MCP_MAP.md`.

`npx @ff-occam/mcp` is **not** the GA install channel.

### Advanced: git clone + .NET 10 SDK (human contributors only)

**Agents must not use this section.** Only when a human developer explicitly asks for a source build:

```bash
git clone https://github.com/ContextForgeAI/occam.git
cd occam
export OCCAM_HOME="$(pwd)"
./scripts/occam-doctor.sh
node scripts/hermes-smoke.mjs
```

Launch via `node scripts/launch-mcp-host.mjs` — **not** in-repo `packages/occam-mcp/bin/occam-mcp.js`.

---

## 2. Wire MCP (stdio)

Always set `OCCAM_HOME` to the **install root** (tarball dir or repo root). Never leave `env` empty.

### Hermes

`~/.hermes/config.yaml`:

```yaml
mcp_servers:
  ff-occam:
    command: /path/to/install/root/scripts/occam-wrapper.sh
    env:
      OCCAM_HOME: /path/to/install/root
```

After tarball install, both paths are usually `$HOME/.local/share/ff-occam`. Reload MCP in Hermes after save.

### Cursor (workspace)

`.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "npx",
      "args": ["-y", "@ff-occam/mcp"],
      "env": {
        "OCCAM_HOME": "/absolute/path/to/ff-occam"
      }
    }
  }
}
```

Git clone: prefer `node scripts/launch-mcp-host.mjs` with the same `OCCAM_HOME`.

### Claude Desktop

`claude_desktop_config.json` → `mcpServers.ff-occam` — same shape as Cursor.

### Generic stdio client

| Field | Value |
|-------|-------|
| Command | `npx` or `node` |
| Args | `["-y", "@ff-occam/mcp"]` or `["scripts/launch-mcp-host.mjs"]` |
| Env | `OCCAM_HOME=<install-root>` (required) |
| Transport | stdio JSON-RPC |

WebSocket: start host with `--mcp-server`, connect to `ws://127.0.0.1:5050`.

---

## 3. Verify before using tools

```bash
export OCCAM_HOME=/path/to/install/root
occam doctor
occam smoke
# Hermes / CI:
node scripts/hermes-smoke.mjs
```

| Check | Pass |
|-------|------|
| `occam doctor` | exits 0 |
| `tools/list` | **11** under default `reader`; **18** under `full` (`occam` + `occam_*`) |
| `hermes-smoke.mjs` | exit 0 |

If MCP is not wired, **stop** — do not guess page content. Read [failure-codes.md](failure-codes.md) on `ok: false`.

---

## 4. Install this skill

From an FF-Occam install root (after MCP verify):

```bash
occam skill install --platform all
```

Platforms: `cursor`, `claude`, `hermes`, `copilot`, `kiro`, `pi`, `devin`, `codex`, `generic`.

| Flag | Effect |
|------|--------|
| `--global` | User-level skills dir (default) |
| `--project` | Current repo `.cursor/skills/occam` (etc.) |
| `--target <dir>` | Copy skill tree to a custom path |
| `--dry-run` | Print destinations only |

npm-only users (no git clone):

```bash
npx @ff-occam/skill install --platform hermes
```

Hermes destination: `~/.hermes/skills/occam/`.

---

## 5. Reload

After wiring MCP, **reload MCP servers** in your IDE or restart Hermes. The skill does not replace MCP — it teaches the agent *how* to use it efficiently.

---

## Troubleshooting

| Symptom | Fix |
|---------|-----|
| `get-ff-occam.sh` / manifest **404** | Release assets missing on GitHub — operator must publish `v1.0.0-rc.2` assets |
| `workers_unavailable` | Wrong or missing `OCCAM_HOME`; run `occam doctor` or `occam install-workers` |
| Worker `ERR_MODULE_NOT_FOUND` (`@mozilla/readability`) | Empty/corrupt `workers/node_modules` | `occam install-workers --force` |
| No `occam_*` tools | MCP not connected; check wrapper + `OCCAM_HOME` + reload |
| `occam-mcp.js` exits on clone | Use `occam-wrapper.sh` or `launch-mcp-host.mjs` |
| `NETSDK1045` / net8.0 | Install .NET 10 SDK or use tarball path |
| `doctor --skip-build` fails | No `OccamMcp.Core` — use tarball or full doctor |
| `npx` download fails | `OCCAM_RELEASE_BASE_URL` + `OCCAM_RELEASE_ALLOW_HTTP=1` |

Full guides: repo `INSTALL.md`, `docs/getting-started.md`, `docs/troubleshooting.md`.
