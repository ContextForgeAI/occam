# `occam_playbook_heal`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamPlaybookHealTool.cs` @ 2026-09-29  
**Profiles:** `full` only (`OCCAM_PROFILE=full`). Hidden on `reader` (default) and other narrow profiles.  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

After a heal-eligible transcode failure, load the page in the browser, capture a compact DOM skeleton plus selector candidates, and may emit a mechanical `draftPlaybookJson` stub (selectors only — not LLM-written). Review → lint → [`occam_playbook_save`](occam_playbook_save.md).

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Prior transcode `ok: false` and heal is eligible (`suggestedNext` is `occam_playbook_heal`, or `occam_client_capabilities` with `heal_needs_full` when the surface is `reader`) | Terminal failures (captcha, private URL, many HTTP errors) — heal echoes and stops. On narrow profiles: widen with `profile=full` → `tools/list` → heal |
| Heal-set `failure_reason` (e.g. `thin_extract`, `extraction_failed`, `content_selectors_miss`, playbook verify failures) | Outside heal set → `heal_not_applicable` |
| Login walls **only with** the same `session_profile` as transcode | Profile is not `full` |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | Absolute HTTP(S) URL to heal |
| `failure_reason` | yes | — | Prior `failure.code` from transcode |
| `session_profile` | no | omit | Optional session profile id |
| `max_skeleton_nodes` | no | `600` | Max skeleton nodes (clamped **50–600**) |

## Response shape

**Success:** `url`, `failureReason`, optional `domSkeleton`, `anchors`, `draftPlaybookJson` (omitted if no candidates with score ≥ 0.45), `agentHints` (`suggestedNext: occam_playbook_save`, `maxVerifyRetries: 3`).

**Failure:** `failureCode`, `message` — never invent page content.

## Common failure codes

`invalid_url`, `invalid_failure_reason`, `heal_not_applicable`, terminal prior codes, `workers_unavailable`, `timeout` (120s), `extraction_failed`, `heal_failed`.

Full taxonomy: [failure-codes](../failure-codes.md).

## Related env vars

`OCCAM_PROFILE` (must be `full`), `OCCAM_HOME`, `OCCAM_DOM_SKELETON_SCRIPT`, session profiles — [configuration](../configuration.md).

## See also

- [`occam_transcode`](occam_transcode.md) · [`occam_playbook_lint`](occam_playbook_lint.md) · [`occam_playbook_save`](occam_playbook_save.md)
