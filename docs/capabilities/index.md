# Capabilities

Capability docs describe **what Occam can do** as product areas — not individual
MCP tools. For tool pickers and schemas, start at the
[Reference overview](../documentation-map.md) or [Tool index](../tools/index.md).

## Who needs this

- Operators deciding which surfaces to enable
- Developers mapping a workflow onto Occam’s domains
- Readers who want stable vs experimental boundaries before diving into tools

## Domains

### Web access

Fetch and reachability: how Occam climbs from cheap HTTP to browser extract, and
how egress / proxies / SSRF controls apply.

- [Acquisition](../acquisition.md) — gated HTTP → browser ladder
- [Networking and proxies](../networking.md) — egress, proxies, private-URL policy

### Knowledge processing

How extracted content is shaped for context windows, and how site recipes
(playbooks) steer structured reads.

- [Materialization](../materialization.md) — token budgets and structured output
- [Playbooks](../playbooks.md) — site recipes and signature model

### Runtime

Long-lived operator concerns: authenticated sessions and day-2 CLI / packaging.

- [Sessions](../sessions.md) — login walls and session profiles
- [Operators](../operators.md) — CLI, doctor, connect, packaging

### Experimental

Opt-in surfaces that are **not** part of the stable default tool set. Enable only
when you accept the documented flags and failure modes.

- [Experimental](../experimental.md) — watch, crosscheck, batch, failure atlas

## Related

- [Task routing](../choosing-a-tool.md) — which tool for which job
- [Trust and security](../trust-and-safety.md) — honesty, receipts, install safety
- [Architecture overview](../how-occam-works.md) — user-level execution model
