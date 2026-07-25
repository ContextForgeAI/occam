# Occam documentation

**Occam** is the verifiable web and data layer for AI agents: live local extraction, compact Markdown, typed failures, and signed receipts.

**Version:** 1.0.0-rc.2 · **Core tools:** 15 · **License:** AGPL-3.0-or-later  
**Docs site:** [https://contextforgeai.github.io/occam/](https://contextforgeai.github.io/occam/)  
**LLM map:** [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt) · [Ask AI](ask-ai.md)

## Start here

| You want… | Go to |
|-----------|-------|
| First success in minutes | [Quick Start](quick-start.md) |
| Product explanation | [What is Occam?](what-is-occam.md) |
| Install reference | [Install](install.md) |
| Connect AI hosts | [Connect](connect/index.md) · [Supported hosts](mcp-hosts.md) |
| Task → tool | [Choosing a tool](choosing-a-tool.md) |
| Copy/paste workflows | [Examples](examples/index.md) |
| Trust questions | [Trust & Safety](trust-and-safety.md) |
| Ask an assistant for help | [Ask AI](ask-ai.md) |

## What do you want to do?

| Task | Guide |
|------|-------|
| Read a web page | [Read a page](guides/read-a-page.md) → `occam_transcode` |
| Research several sources | [Research](guides/research-multiple.md) → `occam_digest` |
| Find pages on a site | [Search & discover](guides/search-and-discover.md) → `occam_map` |
| Search the web | [Search & discover](guides/search-and-discover.md) → `occam_search` |
| Verify what was extracted | [Verify](guides/verify-sources.md) → receipts + `occam_verify` |
| Check whether a source supports a claim | [Claims](guides/claims.md) → `occam_claim_check` |
| Extract structured fields | [Structured extraction](guides/structured-extraction.md) → `occam_extract_knowledge` |
| Connect Occam to my AI | [Connect](connect/index.md) → `occam connect` |
| Fix a failed page | [Failure codes](failure-codes.md) · [Troubleshooting](troubleshooting.md) |

## Source-of-truth order

| Priority | Source | Use it for |
|---|---|---|
| 1 | Runtime `tools/list` | Tool availability and input JSON Schema |
| 2 | [MCP API contract](reference/mcp-api.md) | Response shapes and cross-tool semantics |
| 3 | [Per-tool pages](tools/index.md) | Usage, examples, failure handling |
| 4 | Guides / examples | Learning and workflows |

If two pages disagree, use the higher-priority source and report the documentation drift.

## Documentation map

| Page | Purpose |
|---|---|
| [Quick Start](quick-start.md) | First success |
| [What is Occam?](what-is-occam.md) | Product mental model |
| [How Occam works](how-occam-works.md) | User-level architecture |
| [Getting started](getting-started.md) | First web read + operator CLI |
| [Install](install.md) | Canonical install reference |
| [MCP hosts](mcp-hosts.md) | Connect tiers and safety |
| [Connect](connect/index.md) | Automatic / `--only` / manual |
| [Choosing a tool](choosing-a-tool.md) | Task router |
| [Guides](guides/read-a-page.md) | Task-oriented how-tos |
| [Examples](examples/index.md) | Copy/paste workflows |
| [Recipes](recipes.md) | Additional multi-tool flows |
| [Concepts](concepts.md) | Backends, playbooks, sessions, receipts |
| [Trust & Safety](trust-and-safety.md) | Honesty, local-first, install safety |
| [Receipts](receipts.md) | Human receipt guide |
| [Receipt verification](receipt_verification.md) | Normative receipt format |
| [Per-tool index](tools/index.md) | One page per tool |
| [Tools reference](tools-reference.md) | Compact reference |
| [Configuration](configuration.md) | Environment variables |
| [Transports](transports.md) | stdio, WebSocket, batch HTTP |
| [Failure codes](failure-codes.md) | Typed failures |
| [Troubleshooting](troubleshooting.md) | Symptom → fix |
| [FAQ](faq.md) | Short answers |
| [Ask AI](ask-ai.md) | LLM / assistant entry |
| [Quality baseline](quality-baseline.md) | Public quality claims |
| [Roadmap](roadmap.md) | Shipped / not shipped |
| [Semantic contract](architecture/semantic-contract.md) | Developer invariants |

## LLM reading order

1. [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt) once  
2. [Choosing a tool](choosing-a-tool.md)  
3. One page under [tools/](tools/index.md)  
4. [Failure codes](failure-codes.md) only after `ok: false`  
5. [MCP API](reference/mcp-api.md) only for contract-level detail  

## Packages

Package sources live under `packages/`. This RC ships via **GitHub Release** archives — not npm/NuGet/VSIX.

- [`@ff-occam/mcp`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-mcp) — launcher package (compatibility name)  
- [`@ff-occam/agent-sdk`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-agent-sdk) — TypeScript workflows  
- [`@ff-occam/skill`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-skill) — portable agent skill  
