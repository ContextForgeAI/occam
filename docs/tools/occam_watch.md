# `occam_watch`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamWatchTool.cs` @ 2026-09-29  
**Profiles:** opt-in — `OCCAM_WATCH_MCP=1` (not gated by `OCCAM_PROFILE`)  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Stateful page-change watch. Each call live-extracts the URL, compares against a local JSON baseline, and reports whether content changed. No daemon — the agent polls. Response carries hashes/diff/history — **not** full page markdown.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Recurring “did this URL change?” with server-side baseline | You keep baseline yourself → `occam_transcode` + `if_none_match` / `diff_against` |
| Optional signed history when receipts enabled | One-shot read → `occam_transcode` |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) URL to watch |
| `backend_policy` | no | `http_then_browser` | Backend policy |
| `focus_query` | no | null | Focus prune (enables fit when set) |
| `session_profile` | no | null | Session profile id |
| `playbook_policy` | no | `auto` | `off` or `auto` |
| `include_diff` | no | `true` | Block-level diff when changed |
| `reset` | no | `false` | Restart baseline / history |
| `include_history` | no | `false` | Return full retained chain |

Internally always requests `json_blocks`. History appends only on first_seen or changed (max 64 entries).

## Response shape

**Success:** `firstSeen`, `changed`, `contentHash`, `blockCount`, timestamps, optional `diff`, `contentDeltaTokens`, `historyLength`, `latestEntry`, optional `history`.

**Failure:** extract fail → baseline unchanged; `failure: { code, message }`.

## Related env vars

`OCCAM_WATCH_MCP=1`, `OCCAM_WATCH_DB_PATH`, `OCCAM_RECEIPTS`.

## See also

- [`occam_transcode`](occam_transcode.md) · [`occam_verify`](occam_verify.md) (`mode=history`) · [watch example](../examples/watch-experimental.md)
