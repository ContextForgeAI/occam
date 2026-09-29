# Reference overview

Task-oriented map of Occam’s **MCP tools** and supporting reference pages.
Friendly names below are for navigation; the canonical identifier is always the
``occam_*`` tool name on each page and in runtime `tools/list`.

**Canon order:** runtime `tools/list` → [MCP API](reference/mcp-api.md) →
[per-tool pages](tools/index.md) → narrative guides. When pages disagree, prefer
the higher source and treat older hubs as suspect until verified.

Learning path: [Why Occam](why-occam.md) · [Guides](guides/read-a-page.md) ·
[workflow gallery](examples/gallery.md) · [Examples](examples/index.md).
Deep hubs (optional): [TRUST](TRUST.md), [CASCADE](CASCADE.md), [SEARCH](SEARCH.md),
[Capabilities](capabilities/index.md), [CAPABILITIES](CAPABILITIES.md).

## Which tool should I use?

| You want to… | Open | Canonical id |
|---|---|---|
| Size later reads to your model window | [Client capabilities](tools/occam_client_capabilities.md) | `occam_client_capabilities` |
| Decide if a URL is worth fetching | [Probe](tools/occam_probe.md) | `occam_probe` |
| Read one page as Markdown | [Transcode](tools/occam_transcode.md) | `occam_transcode` |
| Research several URLs at once | [Digest](tools/occam_digest.md) | `occam_digest` |
| Extract typed fields | [Extract knowledge](tools/occam_extract_knowledge.md) | `occam_extract_knowledge` |
| Search the open web | [Search](tools/occam_search.md) | `occam_search` |
| Discover links on a site | [Map](tools/occam_map.md) | `occam_map` |
| Look up / draft / save a site recipe | [Playbooks group](tools/occam_playbook_resolve.md) | `occam_playbook_*` |
| Prove a receipt or cite a block | [Verify](tools/occam_verify.md) | `occam_verify` |
| Check whether a page backs a claim | [Claim check](tools/occam_claim_check.md) | `occam_claim_check` |

Full picker table: [Tool index](tools/index.md). Compact table: [Compact tool reference](tools-reference.md).

## Groups in this Reference section

### Client and diagnostics

Inspect before you spend a full extract.

- [Client capabilities](tools/occam_client_capabilities.md) — `occam_client_capabilities`
- [Probe](tools/occam_probe.md) — `occam_probe`

### Read and transform

Stable page readers and transformers.

- [Transcode](tools/occam_transcode.md) — `occam_transcode`
- [Digest](tools/occam_digest.md) — `occam_digest`
- [Extract knowledge](tools/occam_extract_knowledge.md) — `occam_extract_knowledge`

### Search and discovery

Find URLs when you do not already have them.

- [Search](tools/occam_search.md) — `occam_search`
- [Map](tools/occam_map.md) — `occam_map`

### Playbooks

Site recipes for harder extracts (stable tools; authoring is advanced).

- [Resolve](tools/occam_playbook_resolve.md) · [Lint](tools/occam_playbook_lint.md) · [Heal](tools/occam_playbook_heal.md) · [Save](tools/occam_playbook_save.md)

### Validation and receipts

Integrity and citation workflows. A verified receipt proves **integrity relative to a key you supply** — not factual truth or authorship.

- [Verify](tools/occam_verify.md) · [Claim check](tools/occam_claim_check.md) · [Attest](tools/occam_attest.md) · [Dataset export](tools/occam_dataset_export.md)

### Experimental tools

Absent from default `tools/list` until an env flag is set. See also
[Experimental capabilities](experimental.md).

- [Batch](tools/occam_batch.md) · [Watch](tools/occam_watch.md) · [Crosscheck](tools/occam_crosscheck.md) · [Failure atlas](tools/occam_failure_atlas.md)

### Protocol and configuration

How the host is wired and spoken to.

- [Configuration](configuration.md) · [Transports](transports.md) · [MCP API](reference/mcp-api.md)

### Contracts and errors

Schemas-adjacent tables and typed failure registry.

- [Tool index](tools/index.md) · [Compact tool reference](tools-reference.md) · [Failure codes](failure-codes.md)

## Source-of-truth order

| Priority | Source | Use it for |
|---|---|---|
| 1 | Runtime `tools/list` | Tool availability and input JSON Schema |
| 2 | [MCP API contract](reference/mcp-api.md) | Response shapes and cross-tool semantics |
| 3 | [Per-tool pages](tools/index.md) | Usage, examples, failure handling |
| 4 | Guides / examples / handbook | Learning and workflows |

If two pages disagree, prefer the higher-priority source and report drift.

## Supporting reference

| Page | Purpose |
|------|---------|
| [Concepts](concepts.md) | Backends, sessions, playbooks, receipts |
| [FAQ](faq.md) | Short answers |
| [Trust and security](trust-and-safety.md) | Honesty, receipts, install safety |
| [Signed datasets](datasets.md) | Auditable multi-URL export |

## LLM reading order

1. [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt) once  
2. [Why Occam](why-occam.md) · [Task routing](choosing-a-tool.md)  
3. One page under [tools/](tools/index.md)  
4. [Failure codes](failure-codes.md) only after `ok: false`  
5. [MCP API](reference/mcp-api.md) for contract-level detail  
6. [Handbook](handbook/index.md) only for optional deep mechanisms (verify against code)  
