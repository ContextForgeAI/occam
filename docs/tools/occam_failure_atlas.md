# `occam_failure_atlas`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamFailureAtlasTool.cs` @ 2026-09-29  
**Profiles:** opt-in — `OCCAM_ATLAS_MCP=1` (not gated by `OCCAM_PROFILE`)  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Read the running host’s in-memory per-host failure atlas: which hosts look **walled for this run** (dominant honest closure) vs transient failures. Not persisted; not proof a host is permanently unreachable.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Mid-session crawl: skip `walled: true` hosts | Pre-flight before any extract → [`occam_probe`](occam_probe.md) |
| Closure-oriented summary after extracts this process | Expecting probe-only traffic to populate the atlas |

**Populated by** `TranscodePipeline` outcomes only (transcode, digest, cascade, batch/watch/crosscheck when enabled). **Not** by `occam_probe`. Max 500 hosts.

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `only_walled` | no | `false` | If true, filter `hosts[]` to walled only |

## Response shape

**Always `ok: true`.** Fields: `hostCount`, `walledCount` (full snapshot), `hosts[]` (worst-first), `timestamp`.

Honest closures: `captcha_or_challenge`, `requires_login`, `http_401`, `http_403`, `http_404`, `http_410`.

## Related env vars

`OCCAM_ATLAS_MCP=1` — registers tool and `FailureAtlasSink`.

## See also

- [Failure codes](../failure-codes.md) · [`occam_transcode`](occam_transcode.md) · [`occam_probe`](occam_probe.md)
