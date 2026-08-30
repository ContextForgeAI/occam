# @ff-occam/mcp

**Occam MCP** — local .NET host that turns a URL into token-budgeted Markdown with typed failures and optional integrity receipts (integrity relative to a key — not truth/origin proof).

> **Package status:** npm is **NOT a GA 1.0 install channel** (OD-3). The primary public package
> name is **`ff-occam`**; this lower-level package is `@ff-occam/mcp`. For the guarded release
> install, use GitHub Release archives + bootstrap (`INSTALL.md` / `docs/install.md`). The
> published install default and package version are **`1.0.0-rc.5`**. Registry / `npx` commands
> below are an experimental RC channel. Core MCP tool
> count is registry-defined and varies by profile/opt-in — do not treat a fixed “14/15” as a health check.

- **Local-first** — default extraction runs on your machine.
- **Honest failures** — typed `failure.code` on `ok:false`; never invent page content from memory.
- **Optional receipts** — signed envelopes for offline integrity checks against a key you supply.

## Quick Start

```bash
# Primary npm name — stdio mode for any MCP client
npx ff-occam@1.0.0-rc.5

# Low-level scoped package (same host and version)
npx @ff-occam/mcp@1.0.0-rc.5

# WebSocket mode (experimental)
npx ff-occam@1.0.0-rc.5 --mcp-server
npx ff-occam@1.0.0-rc.5 --mcp-server --port 5051
```

## Installation

```bash
# One-liner from GitHub Releases (no git, no .NET SDK — Node 20+ only)
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash

# Or via npm RC (not the guarded GA install path)
npm install -g ff-occam@1.0.0-rc.5
ff-occam
```

## MCP tool surface

The default `OCCAM_PROFILE=reader` exposes 8 day-to-day tools. Set
`OCCAM_PROFILE=full` for the complete 15-tool core catalog. Runtime
`tools/list` is authoritative; environment-gated tools can add more.

| Tool | Description |
|------|-------------|
| `occam_client_capabilities` | Set the session context budget used by later reads |
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
      "args": ["-y", "ff-occam@1.0.0-rc.5"],
      "env": {
        "OCCAM_PROFILE": "reader"
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
npx ff-occam@1.0.0-rc.5 --mcp-server
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
| **npm / npx RC** | `npx ff-occam@1.0.0-rc.5` from registry | Primary wrapper delegates to `@ff-occam/mcp` and downloads the matching release binary |

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
- **Live by default** — every call fetches unless the caller opts into the TTL-bound response cache with `cache_ttl_s > 0`
- **Honest failures** — typed `failure.code`, never hallucinate content

## Supported Platforms

- Windows x64
- Linux x64
- macOS ARM64 (Apple Silicon)

Requires Node.js 20+.

## Links

- **Documentation**: https://github.com/ContextForgeAI/occam/tree/main/docs
- **API Spec**: https://github.com/ContextForgeAI/occam/blob/main/MCP_API_SPEC.md
- **Issues**: https://github.com/ContextForgeAI/occam/issues
- **Changelog**: https://github.com/ContextForgeAI/occam/blob/main/CHANGELOG.md

## License

AGPL-3.0-or-later. See [LICENSE](https://github.com/ContextForgeAI/occam/blob/main/LICENSE).
