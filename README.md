# Occam

**Occam gives AI agents clean web context with verifiable provenance.**

It is a local [Model Context Protocol](https://modelcontextprotocol.io/) server: your AI asks Occam to read a URL; Occam fetches the live page, returns compact Markdown (or structured data), and can attach a signed receipt. When extraction fails, Occam says so — it does not invent the page.

[Documentation](https://contextforgeai.github.io/occam/) · [Quick Start](docs/quick-start.md) · [Supported hosts](docs/mcp-hosts.md) · [Trust & Safety](docs/trust-and-safety.md) · [API](MCP_API_SPEC.md) · [Releases](https://github.com/ContextForgeAI/occam/releases)

[![License](https://img.shields.io/badge/license-AGPL--3.0-blue?logo=gnu)](LICENSE)

> **Release:** Occam Core **1.0.0-rc.2** — fifteen core MCP tools, Receipt v1, and `occam connect` for supported AI hosts.  
> npm / NuGet / VS Code extension are **not** part of this RC.

---

## Install Occam

**Linux / macOS** (Node.js 20+):

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

**Windows** (PowerShell, Node.js 20+):

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

No .NET SDK required on the install machine. Full reference: [INSTALL.md](INSTALL.md) · Walkthrough: [docs/quick-start.md](docs/quick-start.md)

### What the installer does

1. Downloads and **SHA-256-verifies** the release archive  
2. Runs **doctor** (Node workers + Playwright)  
3. Verifies the Occam host (**15** core `occam_*` tools)  
4. Detects installed AI / MCP hosts and runs **`occam connect`** for validated ones  
5. Tells you if a host needs a restart, trust prompt, or a manual paste  

You should not need to edit JSON by hand for a first success.

---

## After install

1. Restart or trust the host Occam named (when asked).  
2. Ask your connected agent: *Use Occam to read https://example.com*  
3. Expect `ok: true`, Markdown body, and usually a signed `receipt`.

Re-check or add hosts any time:

```bash
occam connect
```

---

## Supported AI hosts (summary)

| Tier | Behavior | Examples |
|------|----------|----------|
| **Live validated** | `occam connect` configures automatically | Hermes, OpenClaw, Claude Code, Codex CLI, Gemini CLI, Cursor, Claude Desktop |
| **Config validated** | Implemented; use `occam connect --only <id>` | VS Code, Cline, Roo, Windsurf, Zed, OpenCode |
| **Assisted** | Detected; paste guidance only | Goose, Junie |
| **Model runtimes** | Detected; **not** MCP hosts | Ollama, LM Studio, llama.cpp |

Details: [docs/mcp-hosts.md](docs/mcp-hosts.md)

---

## Minimal example

Once a host is connected:

```json
{ "name": "occam_transcode", "arguments": { "url": "https://example.com" } }
```

- `ok: true` — use `markdown` (and optional signed `receipt`)  
- `ok: false` — read `failure.code`; do **not** invent page content  

---

## Why trust the result?

- **Local-first** — extraction runs on your machine; no normal cloud middleman  
- **Honest failures** — `ok: false` means content is unknown  
- **Signed receipts** — prove URL, time, content hash, and backend offline  
- **Safe connect** — backups, atomic writes, unmanaged-entry protection; CI does not mutate desktops by default  

More: [docs/trust-and-safety.md](docs/trust-and-safety.md)

---

## Documentation map

| | |
|--|--|
| **Docs site** | https://contextforgeai.github.io/occam/ |
| **Quick Start** | [docs/quick-start.md](docs/quick-start.md) |
| **Hub (GitHub)** | [docs/index.md](docs/index.md) |
| **LLM map** | [llms.txt](llms.txt) |
| **API contract** | [MCP_API_SPEC.md](MCP_API_SPEC.md) |
| **Contributing** | [CONTRIBUTING.md](CONTRIBUTING.md) · [AGENTS.md](AGENTS.md) |

---

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).
