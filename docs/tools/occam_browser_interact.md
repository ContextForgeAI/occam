# `occam_browser_interact`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamBrowserInteractTool.cs` @ 2026-09-29  
**Profiles:** none — only when `OCCAM_BROWSER_ACTIONS_MCP=1`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Run a short **declarative** Playwright action plan (click, type, wait, scroll, …), then materialize Markdown. Success may include Receipt v1. Results are **never cached**.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Page needs click/type/wait before readable content | Plain `occam_transcode` + `session_profile` is enough |
| Closed action vocabulary (no arbitrary page JS) | Long automation, CAPTCHA solving, or arbitrary JS |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) URL to open |
| `actions` | yes | — | JSON array of 1–16 step objects |
| `session_profile` | no | — | Session profile id |
| `focus_query` | no | — | Focus prune after extract when over budget |
| `max_tokens` | no | ambient | Whole-response budget (min 128) |
| `deadline_ms` | no | `90000` | Overall deadline (1000–180000) |

**Allowed `do`:** `wait`, `wait_selector`, `wait_text`, `click`, `hover`, `type`, `press`, `scroll`.  
**Forbidden on steps:** `js`, `js_before_wait`, `evaluate`. Stops on first failed step.

## Response shape

**Success:** `markdown`, `finalUrl`, `actionPlanHash`, `stepsRun`, `actionTrace` (typed text redacted), `contentHash`, `receipt?`, `cached: false`.

**Failure:** top-level `failureCode` + `message` (not nested `failure`); optional `failedIndex`, `actionTrace`.

## Common failure codes

`invalid_arguments`, `invalid_url`, `private_url_blocked`, session errors, `action_failed`, `workers_unavailable`, `spawn_failed`, `timeout`, `extraction_failed`.

## Related env vars

`OCCAM_BROWSER_ACTIONS_MCP=1`, `OCCAM_RECEIPTS`, client budget env — [configuration](../configuration.md).

## See also

- [`occam_transcode`](occam_transcode.md) · [`occam_client_capabilities`](occam_client_capabilities.md) · [experimental](../experimental.md)
