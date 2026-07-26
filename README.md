# Occam

**Occam is a locally run host that helps AI agents acquire usable web content, shape it for context windows, fail honestly when content is unknown, and optionally attach integrity artifacts that can later be checked against a key.**

It is a [Model Context Protocol](https://modelcontextprotocol.io/) server (and CLI). Your agent asks Occam to read a URL; Occam fetches the live page, returns compact Markdown (or structured fields), and can attach a signed receipt. When extraction fails, Occam says so — it does **not** invent the page.

[Documentation](https://contextforgeai.github.io/occam/) · [Quick Start](docs/quick-start.md) · [What is Occam?](docs/what-is-occam.md) · [Trust & Safety](docs/trust-and-safety.md) · [API](MCP_API_SPEC.md) · [Releases](https://github.com/ContextForgeAI/occam/releases)

[![License](https://img.shields.io/badge/license-AGPL--3.0-blue?logo=gnu)](LICENSE)

> **Release:** Occam Core **1.0.0-rc.2** — always-on core MCP tools (registry-defined; runtime `tools/list` varies by profile and opt-in flags), Receipt v1, and `occam connect` for supported AI hosts.  
> **npm is not a GA 1.0 install channel.** Install from the release tarball / bootstrap scripts below. Cosign bundles may exist as release metadata; **installers do not enforce Cosign verification** (integrity check is SHA-256 against the release manifest).

---

## The problem

Agents either skip the fetch and hallucinate from memory, or dump raw HTML into the context window. Neither is trustworthy for research or automation.

## What Occam does

1. **Acquire** the page through a gated ladder (HTTP, then browser when needed; optional managed provider only after local failure).  
2. **Materialize** usable, token-budgeted Markdown (and optional structured blocks/tables/chunks).  
3. **Refuse honestly** — `ok: false` means content is **unknown**; do not fill the gap from model memory.  
4. **Optionally sign** what it produced so the exact bytes can later be checked for tampering against a key obtained out of band.

Occam is **not** “HTML → Markdown” alone, and it is **not** a cryptography product first. Signatures prove **integrity relative to a key** — not truth, origin authenticity, identity, or trusted time.

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

1. Downloads and **SHA-256-verifies** the release archive against the release manifest  
2. Runs **doctor** (Node workers + Playwright)  
3. Verifies the Occam host starts and exposes core `occam_*` tools  
4. Detects installed AI / MCP hosts and runs **`occam connect`** for validated ones  
5. Tells you if a host needs a restart, trust prompt, or a manual paste  

You should not need to edit JSON by hand for a first success.

---

## After install — first successful read

1. Restart or trust the host Occam named (when asked).  
2. Ask your connected agent: *Use Occam to read https://example.com*  
3. Expect `ok: true` and a Markdown body. A signed `receipt` may also be present.

```json
{ "name": "occam_transcode", "arguments": { "url": "https://example.com" } }
```

- `ok: true` — use `markdown`  
- `ok: false` — read `failure.code`; do **not** invent page content  

Then: [JS-heavy pages](docs/examples/difficult-js-page.md) · [login walls](docs/guides/sessions.md) · [verification](docs/guides/verify-sources.md) · [advanced capabilities](docs/choosing-a-tool.md)

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

Details and rollback limits: [docs/mcp-hosts.md](docs/mcp-hosts.md)

---

## Why trust the result?

| Claim | Meaning |
|-------|---------|
| **Local-first** | Default extraction runs on your machine |
| **Honest failures** | `ok: false` means content is unknown |
| **Signed receipts** | Integrity of the returned bytes relative to a local key — not proof the page was true or authentic |
| **Connect** | Backups and careful host config writes; restart-required hosts have limits — see [installation safety](docs/trust/installation-safety.md) |

More: [docs/trust-and-safety.md](docs/trust-and-safety.md)

---

## Documentation map

| | |
|--|--|
| **Docs site** | https://contextforgeai.github.io/occam/ |
| **Docs hub** | [docs/index.md](docs/index.md) |
| **Quick Start** | [docs/quick-start.md](docs/quick-start.md) |
| **What / How** | [what-is-occam.md](docs/what-is-occam.md) · [how-occam-works.md](docs/how-occam-works.md) |
| **Handbook** | [docs/handbook/](docs/handbook/index.md) |
| **Agents** | [llms.txt](llms.txt) · [ask-ai.md](docs/ask-ai.md) · [AGENTS.md](AGENTS.md) |
| **API** | [MCP_API_SPEC.md](MCP_API_SPEC.md) |

---

## License

AGPL-3.0-or-later — see [LICENSE](LICENSE).
