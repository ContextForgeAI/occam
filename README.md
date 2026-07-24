# FF-Occam MCP

**Turn any URL into clean, token-budgeted Markdown locally — with honest failures and a verifiable receipt.**

FF-Occam MCP is a [Model Context Protocol](https://modelcontextprotocol.io/) server: a Native AOT .NET host plus Node.js extract workers. Core MCP tools ship by default; batch, watch, cross-check, and failure atlas are opt-in.

[![License](https://img.shields.io/badge/license-AGPL--3.0-blue?logo=gnu)](LICENSE)

**Install:** [INSTALL.md](INSTALL.md) · **Human docs:** [docs/index.md](docs/index.md) ·
**LLM map:** [llms.txt](llms.txt) · **API:** [MCP_API_SPEC.md](MCP_API_SPEC.md) ·
**Contributing:** [CONTRIBUTING.md](CONTRIBUTING.md) · **Agents:** [AGENTS.md](AGENTS.md)

> **Release:** Occam Core **1.0.0-rc.2** — install via the one-liner below (GitHub Release tarball).  
> npm / NuGet / VS Code extension are **not** part of this RC.

---

## Install (canonical)

**Linux / macOS** (Node 20+):

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

**Windows** (PowerShell, Node 20+):

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

Full details: [INSTALL.md](INSTALL.md). Contributors with .NET 10 SDK: see Advanced section there.

**Cursor** — after install, use the printed snippet, or:

```json
{
  "mcpServers": {
    "ff-occam": {
      "command": "node",
      "args": ["C:\\path\\to\\ff-occam\\scripts\\launch-mcp-host.mjs"],
      "env": { "OCCAM_HOME": "C:\\path\\to\\ff-occam" }
    }
  }
}
```

---

## Minimal example

Once the MCP host is connected, call:

```json
{ "name": "occam_transcode", "arguments": { "url": "https://example.com" } }
```

- `ok: true` — use `markdown` (and optional signed `receipt`).
- `ok: false` — read `failure.code`; do **not** invent page content.

---

## Core MCP tools

| Tool | One line |
|------|----------|
| `occam_transcode` | One page → Markdown + signed receipt |
| `occam_probe` | Cheap URL diagnosis before a full fetch |
| `occam_digest` | Up to 8 URLs → research digest |
| `occam_map` | Discover same-domain links |
| `occam_search` | Web search → candidate URLs |
| `occam_playbook_resolve` | Read-only playbook lookup |
| `occam_playbook_heal` | DOM skeleton for playbook authoring |
| `occam_playbook_save` | Save a local playbook |
| `occam_extract_knowledge` | Structured `facts[]` from playbook schema |
| `occam_verify` | Verify a signed receipt |
| `occam_claim_check` | Ground one claim in one page |
| `occam_attest` | Batch citation check |
| `occam_playbook_lint` | Validate playbook JSON |
| `occam_dataset_export` | Signed dataset export |

Agent guide: [docs/choosing-a-tool.md](docs/choosing-a-tool.md) · Reference: [docs/tools-reference.md](docs/tools-reference.md)

---

## Architecture

Native AOT **.NET 10** MCP host + **Node.js** workers (`http-extract`, `browser-extract`, `css-extract`).  
Stdio (default) or optional WebSocket. No file cache — every call is a live extract.  
See [docs/concepts.md](docs/concepts.md) and [docs/architecture/semantic-contract.md](docs/architecture/semantic-contract.md).

---

## Roadmap

[docs/roadmap.md](docs/roadmap.md) — shipped log and explicit non-goals.

---

## Trust model

- `ok: true` — content came from a live extract.
- `ok: false` — content is **unknown**; read `failure.code`, never hallucinate the page.

---

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).
