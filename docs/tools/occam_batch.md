# `occam_batch_submit` / `occam_batch_status` / `occam_batch_results`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamBatchTools.cs` @ 2026-09-29  
**Profiles:** **Opt-in** — require `OCCAM_BATCH_MCP=1` (not filtered by `OCCAM_PROFILE`)  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Fire-and-forget asynchronous transcode of many URLs: submit → poll status → page results. Background processor uses the same pipeline as `occam_transcode`. **No Receipt v1** on batch item envelopes.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Many URLs and you can poll later | ≤8 URLs inline → [`occam_digest`](occam_digest.md) |
| MCP batch is enough | Signed corpus → [`occam_dataset_export`](occam_dataset_export.md) |
| You accept local JSON job store | Per-URL Receipt v1 proof → `occam_transcode` |

## Workflow

1. **`occam_batch_submit`** → `job_id`, `accepted_count`, `state`  
2. **`occam_batch_status`** → `progress` (`total`, `done`, `failed`, `running`)  
3. **`occam_batch_results`** → `items[]`, `next_cursor`

Terminal job state is usually **`done`**; check **`progress.failed`** for failures.

## Submit parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `urls` | yes | — | JSON array string or delimiter-separated URLs |
| `backend_policy` | no | `http_then_browser` | Per-URL backend |
| `focus_query` / `fit_markdown` / `max_tokens` | no | fit default `true` | Prune / budget |
| `session_profile` / `playbook_policy` | no | `auto` | Session / playbooks |
| `idempotency_key` | no | null | 24h dedupe |
| `on_oversize` | no | `fail` | `fail` or `partial` |

## Errors

Tool errors use **`error: { code, message }`** (not `failure`): `invalid_request`, `job_not_found`. Per-item `ok:false` → content unknown.

## Related env vars

`OCCAM_BATCH_MCP=1`, `OCCAM_BATCH_MAX_URLS`, `OCCAM_BATCH_PARALLEL`, `OCCAM_BATCH_DB_PATH`, `OCCAM_BATCH_PORT` (HTTP `--batch-server` only).

## See also

- [`occam_transcode`](occam_transcode.md) · [`occam_digest`](occam_digest.md) · [experimental](../experimental.md)
