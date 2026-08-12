# Ask AI

## For AI assistants

Start with the compact map — do **not** ingest the whole documentation site:

- Repository: [`llms.txt`](https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt) — agent routing, capability-family index, experimental gates, operator surface, and **trust limits**
- Docs site entry: this page + task router below

Then:

1. Use the [task router](choosing-a-tool.md)
2. Open **one** focused [tool page](tools/index.md)
3. Open [failure codes](failure-codes.md) only after `ok: false`
4. Open [configuration](configuration.md) only when setup is required
5. Use [MCP API](reference/mcp-api.md) only for normative response semantics
6. For proof boundaries and name corrections (`ok:false`, receipts, claim_check, attest, crosscheck), read [Handbook — honesty contract](handbook/02-honesty-contract.md) or the **Trust limits** section in `llms.txt`

Runtime `tools/list` wins for tool availability and input schemas.

**Honesty defaults:** `ok:false` means content is unknown. Receipts prove integrity relative to a key — not truth, origin, or trusted time. Crosscheck is multi-source comparison, not consensus proof. npm is not GA. Cosign on install is policy-gated (`required-cosign-v1` for the `1.0.0-rc.3` candidate; published `v1.0.0-rc.2` remains SHA-256-only) and proves release authenticity/signer identity, not page-content truth.

## For humans using an AI assistant

Paste a prompt like:

> Read https://raw.githubusercontent.com/ContextForgeAI/occam/main/llms.txt and help me use Occam to research these URLs: …

Or point the assistant at the docs site:

> https://contextforgeai.github.io/occam/

## Future: docs-aware assistant

A hosted “Ask AI” search over this documentation may arrive later. **Not shipped today.** This navigation slot is reserved so we can add it without reorganizing the site.

## Next

- [Quick Start](quick-start.md)
- [Task router](choosing-a-tool.md)
- [Handbook](handbook/index.md)
- [Examples](examples/index.md)
