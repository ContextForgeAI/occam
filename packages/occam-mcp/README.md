# @ff-occam/mcp

**FF-Occam MCP** — Native AOT .NET 10 host that turns a URL into clean, token-budgeted Markdown **locally**, with typed failures and optional signed receipts.

> **Not the RC install path.** For `1.0.0-rc.2` use root [INSTALL.md](../../INSTALL.md):
> - Linux/macOS: `curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash`
> - Windows: `irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex`
>
> `@ff-occam/mcp` / `npx` apply only after a future npm publication or a private registry build.

- **Local & private** — the URL and its content never leave the machine.
- **Honest failures** — a typed `failure.code` on `ok:false`; the tool never hallucinates page content from memory.
- **Verifiable** — every extraction can emit a signed receipt (`contentHash` + block Merkle root); verify offline with the bundled CLI.

## Quick Start (post-RC / private registry only)

```bash
# Stdio mode (for Cursor, Claude, any MCP client)
npx @ff-occam/mcp

# WebSocket mode
npx @ff-occam/mcp --mcp-server
npx @ff-occam/mcp --mcp-server --port 5051
```

## Installation

```bash
# One-liner from GitHub Releases (no git, no .NET SDK — Node 20+ only)
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash

# Or via npm (not part of 1.0.0-rc.2 — requires a published registry package)
npm install -g @ff-occam/mcp
occam-mcp
```

## MCP Tools (14)

| Tool | Description |
|------|-------------|
| `occam_transcode` | Convert a URL to clean Markdown (live extract) + signed receipt |
| `occam_probe` | Cheap HTTP diagnosis before a transcode |
| `occam_digest` | Linear multi-URL digest (≤8 URLs) |
| `occam_map` | Live same-domain link discovery (≤64) |
| `occam_search` | Web search returning candidate URLs to transcode |
| `occam_playbook_resolve` | Read-only playbook lookup |
| `occam_playbook_heal` | DOM skeleton capture for playbook authoring |
| `occam_playbook_save` | Save a playbook to the local tier (with verify) |
| `occam_extract_knowledge` | Structured fact extraction against a playbook schema |
| `occam_verify` | Verify a signed extraction receipt offline |
| `occam_claim_check` | Check whether a claim is grounded in extracted content |
| `occam_attest` | Attest that a statement is supported by a live extraction |
| `occam_playbook_lint` | Validate a playbook's schema and grade it |
| `occam_dataset_export` | Export a signed, Merkle-committed extraction dataset |

## Usage with Cursor

Add to `.cursor/mcp.json`:

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "npx",
      "args": ["@ff-occam/mcp"],
      "env": {
        "OCCAM_HOME": "${workspaceFolder}/.occam"
      }
    }
  }
}
```

Or use the WebSocket transport:

```json
{
  "mcpServers": {
    "ff-occam": {
      "url": "ws://127.0.0.1:5050"
    }
  }
}
```

Then start the server:
```bash
npx @ff-occam/mcp --mcp-server
```

## Environment Variables

| Variable | Purpose |
|----------|---------|
| `OCCAM_HOME` | Repo root (git clone or Level B tarball) — skips release download |
| `OCCAM_RELEASE_BASE_URL` | GitHub release download base (default: `https://github.com/ContextForgeAI/occam/releases/download`) |
| `OCCAM_RECEIPTS=off` | Disable signed extraction receipts |
| `OCCAM_BANNER=0` | Disable stderr banner |
| `OCCAM_LOG=1` | Enable stderr profiler |

## Git clone / local install (any MCP host)

Two install modes — do not mix them:

| Mode | When | MCP launcher |
|------|------|----------------|
| **Local tree** | `git clone`, `install.sh`, Level B tarball | `node scripts/launch-mcp-host.mjs` + `OCCAM_HOME` |
| **npm / npx** | `npx @ff-occam/mcp` from registry | npm wrapper downloads release binary |

For a **git clone or tarball** (not `npx`):

```bash
export OCCAM_HOME=/path/to/FFOccamMCP
./scripts/occam-doctor.sh
occam onboard   # paste-ready snippet for Cursor, Hermes, Claude Desktop, …
```

Canonical MCP wiring:

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "node",
      "args": ["/path/to/FFOccamMCP/scripts/launch-mcp-host.mjs"],
      "env": { "OCCAM_HOME": "/path/to/FFOccamMCP" }
    }
  }
}
```

If MCP still points at `node …/packages/occam-mcp/bin/occam-mcp.js`, v0.9.0+ **auto-detects** the local tree (package path, cwd, or script path) and delegates to `launch-mcp-host.mjs` instead of downloading from GitHub Releases. **`occam doctor` is still required** before the host can serve tools.

**Do not** rely on the npm bin for clone installs — use `launch-mcp-host.mjs` or `occam onboard`.

## TypeScript SDK

For programmatic access, use the companion package:

```bash
npm install @ff-occam/agent-sdk @ff-occam/mcp
```

```typescript
import { createClient } from "@ff-occam/agent-sdk";

const client = await createClient();
try {
  const result = await client.transcode({
    url: "https://developer.mozilla.org/en-US/docs/Web/JavaScript/Guide",
    backend_policy: "http",
    fit_markdown: true,
    focus_query: "closures"
  });
  if (result.ok) console.log(result.markdown);
} finally {
  await client.stop();
}
```

`createClient()` performs the MCP `initialize` handshake before it returns. It offers revision
`2025-11-25`, validates the server-selected revision against the supported compatibility set, and
disconnects on an unknown revision. The selected value is exposed as
`client.negotiatedProtocolVersion`. Tool methods return the decoded Occam JSON object, not the raw
MCP `content[]` envelope. Use `listTools()` for runtime
discovery and `callTool<T>(name, arguments)` for opt-in or future tools that do not yet have a typed
convenience method.

For a git clone, set `OCCAM_HOME` and run `occam doctor` first. Advanced lifecycle controls are
available through `handshakeTimeoutMs`, `requestTimeoutMs`, and `shutdownTimeoutMs`; always call
`stop()` in `finally` for long-lived applications.

## Architecture

- **Core**: Native AOT .NET 10 (single binary, ~15MB)
- **Workers**: Node.js (http-extract, browser-extract, css-extract)
- **Transport**: stdio (default) + optional WebSocket
- **No file cache** — every call is live extraction
- **Honest failures** — typed `failure.code`, never hallucinate content

## Supported Platforms

- Windows x64
- Linux x64
- macOS x64 (Intel)
- macOS ARM64 (Apple Silicon)

Requires Node.js 20+.

## Links

- **Documentation**: https://github.com/ContextForgeAI/occam/tree/main/docs
- **API Spec**: https://github.com/ContextForgeAI/occam/blob/main/MCP_API_SPEC.md
- **Issues**: https://github.com/ContextForgeAI/occam/issues
- **Changelog**: https://github.com/ContextForgeAI/occam/blob/main/CHANGELOG.md

## License

AGPL-3.0-or-later. See [LICENSE](https://github.com/ContextForgeAI/occam/blob/main/LICENSE).
