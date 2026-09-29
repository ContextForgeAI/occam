# `occam_extract_knowledge`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamExtractKnowledgeTool.cs`, `Services/KnowledgeExtractService.cs` @ 2026-09-29  
**Profiles:** `reader` (default), `researcher`, `auditor`, `full` — not exposed in `minimal` or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Extract typed structured fields from a page as `facts[]`, driven by the site playbook `knowledge_schema`. Use when you need specific data points, not prose; requires a resolvable schema for the host (check with `occam_playbook_resolve` on `full`, or rely on internal resolve during extract).

Path: resolve playbook → match `knowledge_schema` / `page_class` → CSS extract worker → `facts[]` + `meta.koId`.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Named fields from a page with a matching playbook + schema | Readable prose only → [`occam_transcode`](occam_transcode.md) |
| You confirmed schema routing for the host | No playbook / schema / unmatched `page_class` / empty fields |
| Login or custom headers via `session_profile` | Session not configured when the page requires it |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) URL or search handle. |
| `backend_policy` | no | `http_then_browser` | `http`, `browser`, or `http_then_browser`. Default may follow playbook routing when left at default. |
| `session_profile` | no | — | Profile id — headers and Playwright `storageState` from `OCCAM_SESSIONS_ROOT/<id>.json`. |

## Response shape

### Success (`ok: true`)

`url`, `playbookId`, `pageClass`, `facts[]` (`{ name, value, selector }`), `meta.koId`, `latencyMs`, optional `backend`, optional `confidence` (often omitted at `0`), `receipt` — **extraction telemetry only** (`confidence`, `elapsedMs`). **Not Receipt v1** — not accepted by [`occam_verify`](occam_verify.md).

### Failure (`ok: false`)

Read **`failureCode`** and `message`. Optional: `playbookId`, `pageClass`, `partialFacts[]`, `agentHints`, `latencyMs`. Do not invent page content.

## Common failure codes

**Arguments / environment:** `invalid_arguments`, `workers_unavailable`, `invalid_url`, `private_url_blocked`.

**Handles / session:** `stale_handle`, `unknown_handle`, `invalid_session_profile`, `session_profile_not_found`.

**Playbook / schema:** `playbook_not_found`, `knowledge_schema_missing`, `page_class_unmatched`, `knowledge_schema_empty`.

**Fetch / extract:** `timeout`, `http_*`, `extraction_failed`.

With `http_then_browser`, the host may retry with browser after `http_401`, `http_403`, `http_429`, `timeout`, or `extraction_failed` on HTTP.

Full taxonomy: [failure-codes](../failure-codes.md).

## Related env vars

| Variable | Role |
|----------|------|
| `OCCAM_HOME` | Worker discovery |
| `OCCAM_SESSIONS_ROOT` | Session profiles |
| `OCCAM_ALLOW_PRIVATE_URLS` | Private URL gate |
| `OCCAM_PROFILE` | Tool surface |

Full catalog: [configuration](../configuration.md).

## See also

- [`occam_playbook_resolve`](occam_playbook_resolve.md) · [`occam_transcode`](occam_transcode.md) · [Receipts](../receipts.md)
- [Choosing a tool](../choosing-a-tool.md) · [Recipes](../recipes.md)
