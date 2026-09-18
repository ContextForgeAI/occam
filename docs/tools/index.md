# Tool index

Occam exposes **18 core tools** under `OCCAM_PROFILE=full` (product default is **`reader`** = 11);
**more are opt-in** via host environment flags (batch/watch/consensus/atlas/browser/exam).
All tools return a **JSON string** (camelCase). The trust rule everywhere: **`ok: false` means the
page content is unknown** — never substitute model memory.

For an agent, this page is a router, not a prompt payload: choose one tool, then open only its linked
page. Runtime `tools/list` is authoritative for availability and input schemas; these pages explain
intent, outputs, and recovery behavior.

## Pick by job

| You want to… | Use | Notes |
|---|---|---|
| Tell Occam your context window (once) | [`occam_client_capabilities`](occam_client_capabilities.md) | Sizes later reads; or set `OCCAM_CLIENT_CONTEXT_TOKENS` |
| Read one page (cascade: url / task / budget) | [`occam`](occam.md) | Progressive facade; default on `minimal`/`basic` |
| Read one page with full opt-ins | [`occam_transcode`](occam_transcode.md) | Power reader; only `url` required |
| Decide whether a page is worth fetching | [`occam_probe`](occam_probe.md) | Cheap classify: extractability 0–1, recommended backend |
| Research several pages at once | [`occam_digest`](occam_digest.md) | Up to 8 URLs → per-page excerpts + combined Markdown |
| Find pages on a site when you have no URLs | [`occam_map`](occam_map.md) | Same-domain links from homepage/sitemap/robots |
| Search the open web for URLs | [`occam_search`](occam_search.md) | Keyless DuckDuckGo by default; override or `off` via env |
| Extract typed fields (title/price/author…) | [`occam_extract_knowledge`](occam_extract_knowledge.md) | Needs a playbook `knowledge_schema` for the host |
| Look up a site's saved extraction recipe | [`occam_playbook_resolve`](occam_playbook_resolve.md) | Read-only |
| Draft a recipe for a hard site | [`occam_playbook_heal`](occam_playbook_heal.md) | Captures DOM skeleton + selector candidates |
| Validate a recipe without fetching | [`occam_playbook_lint`](occam_playbook_lint.md) | Static, deterministic |
| Save a recipe (with live verify) | [`occam_playbook_save`](occam_playbook_save.md) | Default dry-runs a transcode first |
| Verify a signed receipt / cite a block | [`occam_verify`](occam_verify.md) | offline / live / prove / citation / history modes |
| Prove an agent read a page | [`occam_canary_issue`](occam_canary_issue.md) → fetch → [`occam_canary_verify`](occam_canary_verify.md) | HMAC sentinel; four verdicts |
| Check whether a page backs a claim | [`occam_claim_check`](occam_claim_check.md) | Returns retrieved blocks + Merkle membership proofs; `found:false` is retrieval-only |
| Audit a report's citations in bulk | [`occam_attest`](occam_attest.md) | 1–50 `{claim, sourceUrl}` rows |
| Build a signed, auditable URL corpus | [`occam_dataset_export`](occam_dataset_export.md) | Per-row receipts + one manifest signature |

## Opt-in tools (absent from `tools/list` until enabled)

Set the flag in the host environment **before** starting the MCP server, then reload the client.

| Tool | Enable with | Purpose |
|---|---|---|
| [`occam_batch_submit` / `occam_batch_status` / `occam_batch_results`](occam_batch.md) | `OCCAM_BATCH_MCP=1` | Fire-and-forget async transcode of a URL list |
| [`occam_watch`](occam_watch.md) | `OCCAM_WATCH_MCP=1` | Stateful page-change detection; history entries signed when receipts enabled |
| [`occam_crosscheck`](occam_crosscheck.md) | `OCCAM_CONSENSUS_MCP=1` | Compare vantage points; detect cloaking/personalization |
| [`occam_failure_atlas`](occam_failure_atlas.md) | `OCCAM_ATLAS_MCP=1` | Per-host failure map of the current run; skip walled hosts |
| [`occam_browser_interact`](occam_browser_interact.md) | `OCCAM_BROWSER_ACTIONS_MCP=1` | Declarative browser actions, then materialize Markdown + Receipt v1 |
| [`occam_exam_submit`](occam_exam_submit.md) | `OCCAM_EXAM_MCP=1` | Grade a capability-exam harness JSON; may change tools/list + list_changed |

## Common parameters

- **`backend_policy`** — `http` (fast, no JS), `browser` (Playwright render), or
  `http_then_browser` (default: HTTP first, escalate when the result looks thin or challenged).
  See [concepts](../concepts.md).
- **`session_profile`** — id of a headers profile JSON under `OCCAM_SESSIONS_ROOT/<id>.json`,
  for gated/logged-in pages. See [configuration](../configuration.md).
- **`focus_query` / `fit_markdown` / `max_tokens`** — token-economy controls; see each page.

## Related

- [Failure codes](../failure-codes.md) — the full `failure.code` registry
- [Receipts](../receipts.md) — signed extraction receipts
- [Configuration](../configuration.md) — environment variables
