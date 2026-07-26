# Documentation map

Exhaustive page index for Occam Docs v3. For a short entry path, start at the [home page](index.md) or [Choosing a tool](choosing-a-tool.md).

## Source-of-truth order

| Priority | Source | Use it for |
|---|---|---|
| 1 | Runtime `tools/list` | Tool availability and input JSON Schema |
| 2 | [MCP API contract](reference/mcp-api.md) | Response shapes and cross-tool semantics |
| 3 | [Per-tool pages](tools/index.md) | Usage, examples, failure handling |
| 4 | Guides / examples / handbook | Learning and workflows |

If two pages disagree, prefer the higher-priority source and report drift.

## Full map

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
