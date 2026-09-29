# `occam`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamCascadeTool.cs` @ 2026-09-29  
**Profiles:** `minimal`, `basic`, `reader` (default), `researcher`, `auditor`, `full`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Read a web page as compact Markdown via the Occam cascade (playbook resolve → focus/budget mapping → HTTP extract → browser fallback → receipt stage). Pass only `url` for a default read. On failure `ok:false` means the page content is **UNKNOWN** — never guess it.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| One-page read, especially on **`minimal`** or **`basic`** profiles | You need transcode opt-ins (tables, blocks, `prefer_llms_txt`, session cookies, …) → [`occam_transcode`](occam_transcode.md) |
| You want focus and a token cap without naming `focus_query`, `fit_markdown`, `max_tokens` | Several URLs → [`occam_digest`](occam_digest.md) |
| You want the progressive ladder with a per-step log | Cheap “worth fetching?” only → [`occam_probe`](occam_probe.md) |
| `mode=advanced` to record that specialised tools exist on wider profiles | Playbook authoring (heal/save) — use playbook tools on **`full`** / auditor surfaces |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | Absolute HTTP(S) URL to read. |
| `task` | no | — | What you need from the page — mapped to `focus_query` + `fit_markdown` when non-empty. |
| `budget` | no | — | Token budget (min **128**). Mapped to `max_tokens`; values below 128 → `invalid_arguments`. |
| `mode` | no | `auto` | `auto` (default cascade) or `advanced` (same extract; may include a `hint`). Unknown values normalize to `auto`. |

## Response shape

JSON string (camelCase). Top-level fields:

| Field | When present | Meaning |
|-------|----------------|---------|
| `ok` | always | `true` if a usable extract was produced |
| `partial` | always | `true` if any stage was **omitted** but a body was still returned |
| `url` | always | Requested URL |
| `markdown` | success | Page body |
| `failure` | `ok: false` | `{ "code": string, "message": string }` |
| `playbookId` | optional | Playbook id when resolve/extract supplied one |
| `backend` | optional | Backend that produced the body |
| `maxTokens` / `focusQuery` | optional | From `budget` / `task` |
| `contentHash` | optional | Content hash when computed |
| `mode` | always | `auto` or `advanced` |
| `hint` | `mode=advanced` | Notes about specialised tools on wider profiles |
| `steps` | always | Ordered stage log |
| `omitted` | always | Stage kind names cut by timeout |

**`steps[]` kinds (response order):** `playbook_resolve` → `focus_budget` → `http_extract` → `browser_fallback` → `receipt`.

**Statuses:** `ok` · `skipped` · `omitted` · `failed` · `degraded` (browser succeeded after HTTP did not produce a body).

## Common failure codes

| Code | Typical cause |
|------|----------------|
| `invalid_arguments` | Missing/invalid `url`; `budget` < 128 |
| `timeout` | Cascade total wall-clock budget exhausted |
| `workers_unavailable` | Workers not configured (`OCCAM_HOME` / doctor) |
| `extraction_failed` | Generic extract failure |
| *(pipeline codes)* | e.g. `http_404`, `thin_extract` — often in step `detail` / `failure.code` |

Full taxonomy: [failure-codes](../failure-codes.md).

## Related env vars

| Variable | Effect |
|----------|--------|
| `OCCAM_PROFILE` | Whether `occam` is exposed |
| `OCCAM_HOME` | Worker discovery |
| `OCCAM_RECEIPTS` | When off, receipt **stage** is skipped; hash may still be computed |

Full catalog: [configuration](../configuration.md).

## See also

- [ADR-0017](../adr/0017-cascade-facade.md) · [`occam_transcode`](occam_transcode.md) · [`occam_client_capabilities`](occam_client_capabilities.md)
- [Choosing a tool](../choosing-a-tool.md) · [Recipes](../recipes.md)
