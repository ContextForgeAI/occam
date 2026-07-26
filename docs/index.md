# Occam documentation

**Occam** is a locally run host that helps AI agents acquire usable web content, shape it for context windows, fail honestly when content is unknown, and optionally attach integrity artifacts verifiable against a key.

**Version:** 1.0.0-rc.2 · **License:** AGPL-3.0-or-later  
**Docs site:** [https://contextforgeai.github.io/occam/](https://contextforgeai.github.io/occam/)  
**LLM map:** [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt) · [Ask AI](ask-ai.md)

Core MCP tools are registry-defined; runtime `tools/list` varies by **profile** and **opt-in** flags — do not treat a fixed “15” as a health check by itself.

## Start here

| You want… | Go to |
|-----------|-------|
| First success in minutes | [Quick Start](quick-start.md) |
| Product explanation | [What is Occam?](what-is-occam.md) |
| Architecture overview | [How Occam works](how-occam-works.md) |
| Install / operators | [Install](install.md) · [Operators](operators.md) |
| Connect AI hosts | [Connect](connect/index.md) · [Supported hosts](mcp-hosts.md) |
| Task → tool | [Choosing a tool](choosing-a-tool.md) |
| Trust questions | [Trust & Safety](trust-and-safety.md) |
| Deep understanding | [Handbook](handbook/index.md) |
| Ask an assistant | [Ask AI](ask-ai.md) |

## What do you want to do?

| Task | Guide |
|------|-------|
| Read a web page | [Read a page](guides/read-a-page.md) → `occam_transcode` |
| Research several sources | [Research](guides/research-multiple.md) → `occam_digest` |
| Find pages / search | [Search & discover](guides/search-and-discover.md) |
| Login walls | [Sessions](sessions.md) · [Sessions guide](guides/sessions.md) |
| Verify integrity artifacts | [Verify](guides/verify-sources.md) → receipts + `occam_verify` |
| Evidence for a claim | [Claims](guides/claims.md) → `occam_claim_check` / `occam_attest` |
| Structured fields | [Structured extraction](guides/structured-extraction.md) |
| Site recipes | [Playbooks](playbooks.md) |
| Experimental tools | [Experimental](experimental.md) |
| Connect Occam to my AI | [Connect](connect/index.md) |

## Capabilities (by system, not a flat CAP list)

| System | Pages |
|--------|-------|
| Acquisition & networking | [Acquisition](acquisition.md) · [Networking](networking.md) · [Sessions](sessions.md) |
| Materialization | [Materialization](materialization.md) |
| Trust & verification | [Trust & Safety](trust-and-safety.md) · [Receipts](receipts.md) |
| Playbooks & datasets | [Playbooks](playbooks.md) · [Datasets](datasets.md) |
| Experimental | [Experimental](experimental.md) |
| Operators | [Operators](operators.md) · [Connect](connect/index.md) |

## Source-of-truth order

| Priority | Source | Use it for |
|---|---|---|
| 1 | Runtime `tools/list` | Tool availability and input JSON Schema |
| 2 | [MCP API contract](reference/mcp-api.md) | Response shapes and cross-tool semantics |
| 3 | [Per-tool pages](tools/index.md) | Usage, examples, failure handling |
| 4 | Guides / examples / handbook | Learning and workflows |

If two pages disagree, prefer the higher-priority source and report drift.

## Documentation map

| Page | Purpose |
|------|---------|
| [Quick Start](quick-start.md) | First success |
| [What is Occam?](what-is-occam.md) | Product mental model |
| [How Occam works](how-occam-works.md) | User-level architecture |
| [Getting started](getting-started.md) | First web read + operator CLI |
| [Install](install.md) | Canonical install reference |
| [Operators](operators.md) | CLI, doctor, connect, packaging |
| [MCP hosts](mcp-hosts.md) | Connect tiers and safety |
| [Connect](connect/index.md) | Automatic / `--only` / manual |
| [Choosing a tool](choosing-a-tool.md) | Task router |
| [Guides](guides/read-a-page.md) | Task-oriented how-tos |
| [Examples](examples/index.md) | Copy/paste workflows |
| [Recipes](recipes.md) | Additional multi-tool flows |
| [Concepts](concepts.md) | Backends, playbooks, sessions, receipts |
| [Acquisition](acquisition.md) | Gated HTTP→browser ladder |
| [Materialization](materialization.md) | Token budgets and structured output |
| [Networking](networking.md) | Proxies and SSRF scope |
| [Sessions](sessions.md) | Authenticated access tiers |
| [Experimental](experimental.md) | Watch / crosscheck / batch / atlas |
| [Trust & Safety](trust-and-safety.md) | Honesty, local-first, install safety |
| [Receipts](receipts.md) | Human receipt guide |
| [Receipt verification](receipt_verification.md) | Normative receipt format |
| [Playbooks](playbooks.md) | Site recipes and signature v1/v2 |
| [Datasets](datasets.md) | Auditable multi-URL export |
| [Handbook](handbook/index.md) | Deep technical textbook |
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
5. [Handbook](handbook/index.md) for deep comprehension  
6. [MCP API](reference/mcp-api.md) for contract-level detail  

## Packages

This RC ships via **GitHub Release** archives — **npm is not a GA 1.0 channel.**

- [`@ff-occam/mcp`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-mcp) — launcher package (non-GA)  
- [`@ff-occam/agent-sdk`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-agent-sdk) — TypeScript workflows  
- [`@ff-occam/skill`](https://github.com/ContextForgeAI/occam/tree/main/packages/occam-skill) — portable agent skill  

## Review artifacts (repo only)

Maintainer usability tests — not in site navigation: [friend-test.md](friend-test.md)
