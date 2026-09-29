# Occam

**Live URL → compact Markdown for LLM agents — or a typed `ok:false`.**  
Never invent page text from model memory.

Occam is a **local MCP server** (`ff-occam`): it fetches the real page, strips chrome, fits a token budget, and returns usable context — or a typed refusal. Receipts prove **integrity relative to a key**, not truth.

[![Release](https://img.shields.io/github/v/release/ContextForgeAI/occam?label=release)](https://github.com/ContextForgeAI/occam/releases/latest)
[![License: AGPL-3.0](https://img.shields.io/badge/license-AGPL--3.0-blue.svg)](LICENSE)
[![MCP](https://img.shields.io/badge/MCP-18%20core%20tools-0ea5e9)](docs/tools/index.md)

| Generic fetch / model memory | Occam |
|------------------------------|--------|
| Empty shell or invented text | Live Markdown **or** typed `ok:false` |
| Raw HTML burns the context window | Token budget + focus prune |
| Silent failure looks like “a short page” | `thin_extract` ≠ successful `short_quality` |
| No integrity of what was returned | Optional Receipt v1 → `occam_verify` |

---

## Install (recommended)

> **Use the release tarball. Do not build from source** (`dotnet build` / `publish` / `run`).  
> Needs **Node 20+**. No .NET SDK on the install machine. Full rules: [INSTALL.md](INSTALL.md).

**Windows (PowerShell):**

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/v1.3.0/scripts/get-ff-occam.ps1 | iex
```

**Linux x64 / macOS Apple Silicon:**

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/v1.3.0/scripts/get-ff-occam.sh | bash
```

Then connect your MCP host and run a first read — [quick-start](docs/quick-start.md) · [getting-started](docs/getting-started.md).

`npx ff-occam@1.3.0` is **experimental** (MCP host only; not the guarded GA path).

---

## First call

Default profile **`OCCAM_PROFILE=reader`** exposes **11** day-to-day tools. Full core catalog: **18**.

1. Once per session: `occam_client_capabilities(context_tokens=…)` (or set `OCCAM_CLIENT_CONTEXT_TOKENS`).
2. One page: **`occam_transcode`** with only `url` — or the cascade facade **`occam`**.
3. Several URLs: one **`occam_digest`**, not N× transcode.
4. On `ok: false`: read `failure.code` — **do not** fill in the page from memory.

Tool picker: [choosing-a-tool](docs/choosing-a-tool.md) · Per-tool pages: [docs/tools/index.md](docs/tools/index.md).

---

## Why it exists (five capabilities)

1. **Honesty** — typed failures; unknown content stays unknown.  
2. **Token contract** — ambient budget from client capabilities; optional `max_tokens` / focus.  
3. **Acquisition ladder** — cheap HTTP first, browser when needed.  
4. **Evidence** — receipts, verify, claim-check, attest, proof-of-read canary.  
5. **Playbooks** — per-site recipes (resolve / heal / lint / save) when authoring.

Flashcard: [why-occam](docs/why-occam.md) · Trust limits: [trust-and-safety](docs/trust-and-safety.md) · Config: [configuration](docs/configuration.md).

---

## Docs map

| Audience | Start here |
|----------|------------|
| Human (this page) | Install → first call → [docs hub](docs/index.md) |
| Operator / install agent | [INSTALL.md](INSTALL.md) |
| Tool-using agent | [`llms.txt`](llms.txt) → why-occam → choosing-a-tool → one tool page |
| API contract | [MCP_API_SPEC.md](MCP_API_SPEC.md) |
| Contributor | [AGENTS.md](AGENTS.md) |

Deep topic hubs (not the front door): [TRUST](docs/TRUST.md) · [CASCADE](docs/CASCADE.md) · [SEARCH](docs/SEARCH.md) · [receipts](docs/receipts.md).

**Doc rescue in progress:** treat runtime `tools/list` and code as authoritative when a narrative page disagrees. Engineering piles such as `docs/rc2/` are excluded from the published site and are not part of the product funnel.
