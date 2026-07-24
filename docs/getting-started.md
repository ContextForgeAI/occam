# Getting started

**Agents / Hermes:** read [INSTALL.md](../INSTALL.md) first.

**What you'll do:** install FF-Occam MCP, wire it into an MCP client, and run your first successful `occam_transcode`.

---

## Canonical install

**One path** for operators and agents — see [INSTALL.md](../INSTALL.md):

```bash
# Linux / macOS (Node 20+)
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

```powershell
# Windows (PowerShell, Node 20+)
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

`npx @ff-occam/mcp` is **not** part of `1.0.0-rc.2`. Contributor clone + `.NET 10` doctor is documented under Advanced in [INSTALL.md](../INSTALL.md).

Wire any stdio MCP host to:

| Field | Value |
|-------|-------|
| Command | `node` |
| Args | `["$OCCAM_HOME/scripts/launch-mcp-host.mjs"]` |
| Env | `OCCAM_HOME=<install root>` |

**Do not** put `OPENROUTER_API_KEY` (or other LLM API keys) in Occam's env.

Smoke:

```bash
node "$OCCAM_HOME/scripts/hermes-smoke.mjs"
```

Expect **15** `occam_*` tools and a successful `occam_transcode` probe.

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
