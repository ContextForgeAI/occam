# FF-Occam MCP documentation

FF-Occam MCP turns live public URLs into compact Markdown on your machine. A success can carry a
locally signed receipt. A failure is explicit: **`ok: false` means the page content is unknown**.

**Version:** 1.0.0-rc.2 · **Core tools:** 15 · **License:** AGPL-3.0-or-later  
**Distribution (RC):** GitHub release archives for **Linux x64**, **macOS arm64**, and **Windows x64** (no npm / NuGet / VSIX in this RC).
**LLM-readable map:** [`llms.txt`](../llms.txt)

## Choose your path

| You are… | Start here | Then |
|---|---|---|
| **A person trying the product** | [Getting started](getting-started.md) | [Choose a tool](choosing-a-tool.md) |
| **An operator installing on Hermes/server** | [INSTALL.md](../INSTALL.md) | [Troubleshooting](troubleshooting.md) |
| **An LLM agent using the MCP tools** | [`llms.txt`](../llms.txt) | [Tool router](choosing-a-tool.md) → [one tool page](tools/index.md) |
| **A TypeScript SDK user** | [Programmatic client](getting-started.md#programmatic-typescript-client) | [Package README](../packages/occam-agent-sdk/README.md) |
| **A verifier or auditor** | [Receipts](receipts.md) | [Normative receipt format](receipt_verification.md) |
| **A contributor changing this repository** | [AGENTS.md](../AGENTS.md) | [Quality baseline](quality-baseline.md) |

## First success in one minute

1. Install or build the host using [Getting started](getting-started.md).
2. Connect it to an MCP client over stdio.
3. Call `occam_transcode` with only `{"url":"https://example.com"}`.
4. If `ok` is false, follow [Failure codes](failure-codes.md). Do not summarize the missing page.

## LLM reading order

Keep the context small and deterministic:

1. Read [`llms.txt`](../llms.txt) once.
2. Use [Choosing a tool](choosing-a-tool.md) to select the smallest workflow.
3. Open only that tool's page in the [per-tool index](tools/index.md).
4. Open [Failure codes](failure-codes.md) only after `ok: false`, or
   [Configuration](configuration.md) when setup is required.
5. Use [MCP_API_SPEC.md](../MCP_API_SPEC.md) only for contract-level detail.

Runtime rules for agents:

- `tools/list` wins for available tools and input schemas.
- Use snake_case parameter names exactly as exposed by the server.
- Do not enter the heal/lint/save loop unless the user asked to author a playbook.
- Do not treat token reduction as evidence of extraction quality.

## Source-of-truth order

| Priority | Source | Use it for |
|---|---|---|
| 1 | Runtime `tools/list` | Tool availability and input JSON Schema |
| 2 | [MCP API contract](../MCP_API_SPEC.md) | Response shapes and cross-tool semantics |
| 3 | [Per-tool pages](tools/index.md) | Human/agent usage, examples, failure handling |
| 4 | Narrative guides below | Learning, installation, and task workflows |

If two pages disagree, use the higher-priority source and report the documentation drift.

## Documentation map

| Page | Audience | Purpose |
|---|---|---|
| [Getting started](getting-started.md) | People, operators | Install, connect, operator CLI, first call |
| [Concepts](concepts.md) | People, agents | Trust model, backends, playbooks, sessions |
| [Choosing a tool](choosing-a-tool.md) | Agents, SDK users | Goal-to-tool routing and role-scoped tool sets |
| [Per-tool index](tools/index.md) | Agents, people | Focused page for each core and opt-in tool |
| [Compact tools reference](tools-reference.md) | People | All tools on one searchable page |
| [Recipes](recipes.md) | People, agents | Exact multi-tool workflows |
| [Configuration](configuration.md) | Operators | Environment variables and defaults |
| [Transports](transports.md) | Operators, integrators | stdio, WebSocket, remote WSS, batch HTTP |
| [Receipts](receipts.md) | Verifiers | Receipt concepts and verification workflow |
| [Receipt verification](receipt_verification.md) | Implementers | Normative byte-level receipt format |
| [Failure codes](failure-codes.md) | Agents, operators | Retry, stop, and remediation actions |
| [Troubleshooting](troubleshooting.md) | Operators | Symptom-to-fix runbook |
| [FAQ](faq.md) | Everyone | Short operational answers |
| [Quality baseline](quality-baseline.md) | Contributors | Public quality claims and how to reproduce them |
| [Roadmap](roadmap.md) | Everyone | Shipped, active, and explicitly unshipped work |
| [Semantic contract](architecture/semantic-contract.md) | Contributors | Durable access/focus/budget invariants |
| [Repository map](maintenance/REPOSITORY_MAP.md) | Contributors | Public tree layout |
| [Fixture sources](maintenance/FIXTURE_SOURCES.md) | Contributors | Golden fixture attribution and immutability |

## Packages and release state

Package sources live under `packages/`. GitHub Release/npm publication is an owner-controlled release
step; check [Roadmap](roadmap.md#active-engineering-maintainer) before assuming a registry command is
available. From a source checkout, use the documented git-clone path.

- [`@ff-occam/mcp`](../packages/occam-mcp/README.md) — MCP launcher and low-level client.
- [`@ff-occam/agent-sdk`](../packages/occam-agent-sdk/README.md) — high-level TypeScript workflows.
- [`@ff-occam/skill`](../packages/occam-skill/README.md) — portable lazy-loaded agent skill.
