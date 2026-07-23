# Getting started

**Agents / Hermes:** read [INSTALL.md](../INSTALL.md) first.

**What you'll do:** install FF-Occam MCP, wire it into an MCP client, and run your first successful `occam_transcode`.

---

## Choose an install path

| Path | When to use | Requirements |
|------|-------------|--------------|
| **Release tarball** | **Supported RC path** — Hermes / VPS — **no .NET SDK** | Node 20+ |
| **Git clone** | Contributors, custom builds, full control | Node 20+, .NET 10 SDK |
| **npm / npx** | **Not part of `1.0.0-rc.2`** — future / private registry only | Node.js 20+ |

`1.0.0-rc.2` ships **GitHub release archives for Linux x64, macOS arm64, and Windows x64**. npm packages,
NuGet packages, and the VS Code extension are out of RC scope. A source
checkout works today with the git-clone path below.

---

## Production server (Hermes, no .NET SDK)

**Use the release tarball** — not a bare git clone. Clone has source only; the AOT binary comes from
GitHub Releases or `dotnet publish` on a .NET 10 machine. See [INSTALL.md](../INSTALL.md).

### From downloaded release assets (no checkout)

Download `ff-occam-<ver>-linux-x64.tar.gz` and `ff-occam-<ver>-linux-x64-manifest.json` from the
GitHub Release, then extract and run doctor from the **extracted** tree:

```bash
INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
mkdir -p "$INSTALL_DIR"
tar -xzf ff-occam-1.0.0-rc.2-linux-x64.tar.gz -C "$INSTALL_DIR" --strip-components=1
export OCCAM_HOME="$INSTALL_DIR"
cd "$OCCAM_HOME"
bash scripts/occam-doctor.sh --skip-build
node scripts/hermes-smoke.mjs
```

Do not call `scripts/lib/release-install.mjs` unless that script is already on disk (checkout or
extracted release). Without a checkout you can also bootstrap from the public raw script:

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

### From an existing checkout

```bash
cd /path/to/FFOccamMCP
export OCCAM_VERSION=1.0.0-rc.2
export OCCAM_RELEASE_BASE="https://github.com/ContextForgeAI/occam/releases/download/v${OCCAM_VERSION}"
bash scripts/get-ff-occam.sh
# or: ./scripts/install.sh --from-url "$OCCAM_RELEASE_BASE/ff-occam-${OCCAM_VERSION}-linux-x64.tar.gz"
```

There is **no** root `npm run bootstrap`. Do **not** install .NET 8.

**Git clone** is for machines with **.NET 10 SDK** only:

```bash
git clone https://github.com/ContextForgeAI/occam.git /srv/hermes/mcp-tools/FFOccamMCP
export OCCAM_HOME=/srv/hermes/mcp-tools/FFOccamMCP
cd "$OCCAM_HOME"
./scripts/occam-doctor.sh
```

Wire Hermes (or any stdio MCP host) to the canonical launcher:

| Field | Value |
|-------|-------|
| Command | `bash` |
| Args | `["/path/to/FFOccamMCP/scripts/occam-wrapper.sh"]` |
| Env | `OCCAM_HOME=/path/to/FFOccamMCP` |

Equivalent: `node scripts/launch-mcp-host.mjs` with the same `OCCAM_HOME`.

**Do not** put `OPENROUTER_API_KEY` (or other LLM API keys) in Occam's env — those belong to Hermes's LLM config, not the MCP host.

Smoke test after wiring:

```bash
node scripts/hermes-smoke.mjs
```

Expect **15** `occam_*` tools and a successful `occam_transcode` probe.

**Git clone on a build machine:** run `./scripts/occam-doctor.sh` once — it publishes the host and copies `OccamMcp.Core` to the repo root so launchers find it without a deep publish path.

---

## Install via npm (not part of 1.0.0-rc.2)

npm packages are **not** included in the `1.0.0-rc.2` release candidate. Prefer the release tarball
or git-clone path. The commands below remain for a future registry publication or a private
registry build — do not treat them as the supported RC install path.

```bash
# Stdio — default for MCP clients (post-RC / private registry only)
npx @ff-occam/mcp

# WebSocket on 127.0.0.1:5050
npx @ff-occam/mcp --mcp-server
```

Global install (same caveat):

```bash
npm install -g @ff-occam/mcp
occam-mcp
```

From a checkout, install a published **tarball** (supported RC path):

```bash
export OCCAM_VERSION=1.0.0-rc.2
export OCCAM_RELEASE_BASE="https://github.com/ContextForgeAI/occam/releases/download/v${OCCAM_VERSION}"
bash scripts/get-ff-occam.sh
```

Without a checkout, download the GitHub Release assets and follow
[Installation from downloaded release assets](../INSTALL.md#installation-from-downloaded-release-assets-no-checkout).

If you deliberately use a future/private `@ff-occam/mcp` build without a local clone, set
`OCCAM_RELEASE_BASE_URL` to `https://github.com/ContextForgeAI/occam/releases/download`.
---

## Install via git clone

```powershell
git clone https://github.com/ContextForgeAI/occam.git occam
cd FFOccamMCP
$env:OCCAM_HOME = (Get-Location).Path
.\scripts\occam-doctor.ps1
```

macOS / Linux:

```bash
git clone https://github.com/ContextForgeAI/occam.git occam
cd FFOccamMCP
export OCCAM_HOME="$(pwd)"
./scripts/occam-doctor.sh
```

Doctor installs npm workspaces, Playwright Chromium, and publishes the .NET host.

Launch the MCP host:

```bash
node scripts/launch-mcp-host.mjs
```

---

## Operator CLI

After installation, add `$OCCAM_HOME/scripts` to `PATH`. Run `occam --help` for the live command
list; the table below is the task-oriented summary.

| Command | Use it for |
|---|---|
| `occam` / `occam control` | Interactive operator menu in a TTY |
| `occam doctor` | Validate workers, browser, and the AOT host |
| `occam onboard` | Create operator settings and an MCP connection snippet |
| `occam snippet` | Print paste-ready MCP configuration |
| `occam smoke` | Initialize stdio, list tools, and probe a public URL |
| `occam status` | Show install version and onboarding state |
| `occam update` | Check for a newer release without changing files |
| `occam refresh` | Stop Occam MCP hosts and rerun doctor |
| `occam session` | Create, list, import, or export session profiles |
| `occam skill` | Install the portable agent skill |

Do not expose the interactive menu to a non-interactive agent. Use explicit subcommands and inspect
their exit codes or `--json` output.

---

## Wire into Cursor

Create or edit `.cursor/mcp.json` (gitignored locally). For a **git clone**, prefer the launcher + a narrow tool profile so the agent reaches for Occam without drifting into heal/save:

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "node",
      "args": ["${workspaceFolder}/scripts/launch-mcp-host.mjs"],
      "cwd": "${workspaceFolder}",
      "env": {
        "OCCAM_HOME": "${workspaceFolder}",
        "OCCAM_PROFILE": "researcher",
        "WT_OCCAM_BANNER": "0"
      }
    }
  }
}
```

`OCCAM_PROFILE=researcher` exposes eight core tools (read + claim_check + verify). Use `full` when authoring playbooks. See [configuration.md](configuration.md#tool-surface-profile-occam_profile).

Published package path (after registry release):

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "npx",
      "args": ["@ff-occam/mcp"],
      "env": {
        "OCCAM_HOME": "C:\\path\\to\\FFOccamMCP",
        "OCCAM_PROFILE": "researcher"
      }
    }
  }
}
```

For a **git clone** install, set `OCCAM_HOME` to the repository root. For **npx-only**, the wrapper auto-discovers the install directory when possible; set `OCCAM_HOME` if you see `workers_unavailable`.

Reload MCP servers in Cursor after saving.

---

## Wire into Claude Desktop

Add to `claude_desktop_config.json` (path varies by OS):

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "npx",
      "args": ["-y", "@ff-occam/mcp"],
      "env": {
        "OCCAM_HOME": "/path/to/FFOccamMCP"
      }
    }
  }
}
```

Restart Claude Desktop.

---

## Generic MCP client (stdio)

Any client that spawns a process and speaks JSON-RPC over stdin/stdout:

| Field | Value |
|-------|-------|
| Command | `npx` |
| Args | `["@ff-occam/mcp"]` or `["node", "scripts/launch-mcp-host.mjs"]` for git clone |
| Env | `OCCAM_HOME` = install root |
| Transport | stdio (default) |

WebSocket clients: start the host with `--mcp-server` and connect to `ws://127.0.0.1:5050`. See [Transports](transports.md).

---

## Programmatic TypeScript client

Use the SDK when your Node application owns the orchestration loop:

```bash
npm install @ff-occam/agent-sdk @ff-occam/mcp
```

```typescript
import { createClient } from "@ff-occam/agent-sdk";

const client = await createClient();
try {
  const tools = await client.listTools();
  const page = await client.transcode({ url: "https://example.com" });
  if (page.ok) console.log(page.markdown);
} finally {
  await client.stop();
}
```

`createClient()` completes the MCP initialize handshake before returning. It offers the current
stable revision (`2025-11-25`), accepts the server's negotiated revision only when it is in the
client's explicit compatibility set, and otherwise disconnects. Inspect
`client.negotiatedProtocolVersion` when protocol provenance matters. Typed methods return the
decoded Occam JSON object rather than the raw MCP `content[]` envelope. Use
`callTool<T>(name, arguments)` for opt-in or newly added tools. For a clone, set `OCCAM_HOME` and run
`occam doctor` first; the client discovers the current platform's RID-specific AOT publish path.

Always stop a reusable client in `finally`. Shutdown is idempotent and first closes stdio cleanly so
the host can dispose its worker daemons; a bounded process-tree termination is the fallback.

---

## First tool call

Call `occam_transcode` with a stable public URL:

```json
{
  "url": "https://example.com"
}
```

### Success shape (abbreviated)

```json
{
  "ok": true,
  "url": { "requested": "https://example.com", "final": "https://example.com/" },
  "markdown": "# Example Domain\n\nThis domain is for use in documentation examples...",
  "backend": "http",
  "receipt": {
    "signed": {
      "v": 1,
      "contentHash": "sha256:…",
      "sig": "…"
    }
  }
}
```

### Failure shape

```json
{
  "ok": false,
  "failure": {
    "code": "http_404",
    "message": "HTTP 404 (http_404).",
    "retryable": null
  }
}
```

Do not summarize the page when `ok` is false.

---

## Agent skill (any harness)

FF-Occam ships a **portable skill** that wraps MCP with lazy-loaded recipes — for Cursor, Claude Code, Hermes, Codex, Copilot, and other agents. The skill teaches *when* and *how* to call `occam_*` without loading all tool docs into every prompt.

**Install skill** (after MCP host is available):

```bash
occam skill install --platform all
```

npm-only, after registry publication:

```bash
npx @ff-occam/skill install --platform cursor
```

| Flag | Effect |
|------|--------|
| `--platform cursor\|claude\|hermes\|…\|all` | Target harness (default `all`) |
| `--project` | Install into current repo (e.g. `.cursor/skills/occam/`) |
| `--target <dir>` | Custom skills directory |

Source tree: `skills/occam/` (`SKILL.md` + `references/`). Wire MCP first (sections above), then reload your agent so it discovers the skill.

Package: [`@ff-occam/skill`](../packages/occam-skill/README.md).

---

## Session profiles (login walls)

When `failure.code` is `requires_login` or `http_403`:

1. Log in to the site in a normal browser.
2. Export cookies: `node scripts/occam-session.mjs export-state --profile mysite`
3. Retry with `session_profile: "mysite"`.

Details: [Configuration — session profiles](configuration.md#session-profiles).

---

## Verify the install

| Symptom | Fix |
|---------|-----|
| `workers_unavailable` | Set `OCCAM_HOME`, run `occam-doctor` |
| MCP shows zero tools | Reload MCP; check stderr for crash |
| Browser failures | `cd workers/browser-extract && npx playwright install chromium` |

More: [Troubleshooting](troubleshooting.md).

---

## Next steps

- Agents: [Choosing a tool](choosing-a-tool.md)
- Reference: [Tools reference](tools-reference.md)
- Env vars: [Configuration](configuration.md)
