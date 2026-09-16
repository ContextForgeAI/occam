# Cascade page reader

**Canonical tool:** `occam`

Progressive-disclosure page reader: pass `url`, optionally `task` and `budget`. Internally runs
playbook → HTTP → browser → focus/budget → receipt with per-step timeouts and graceful degradation.

On failure `ok:false` means the page content is UNKNOWN — never guess it.

## When to use

- Default one-page read on narrow profiles (`OCCAM_PROFILE=minimal|basic`) and as the simple entry on
  `reader` / `full`.
- You want a focused extract without memorising `fit_markdown` / `focus_query` / `max_tokens`.
- Full opt-in surface (tables, blocks, llms.txt, screenshots, …) → [`occam_transcode`](occam_transcode.md).

## Parameters

| Param | Required | Notes |
|-------|----------|-------|
| `url` | yes | Absolute http(s) URL |
| `task` | no | What you need from the page → focus + fit |
| `budget` | no | Token cap (min 128) → `max_tokens` |
| `mode` | no | `auto` (default) or `advanced` (same cascade; hints at specialised tools) |

## Response

JSON with `ok`, `partial`, `markdown`, `steps[]`, `omitted[]`, optional `failure`, `contentHash`,
`backend`, `playbookId`.

- `partial:true` — a usable body was returned, but one or more cascade stages were omitted (timeout).
- `steps` — ordered log: `playbook_resolve`, `http_extract`, `browser_fallback`, `focus_budget`,
  `receipt` with status `ok|skipped|omitted|failed|degraded`.

## CLI

```bash
dotnet run --project src/FFOccamMcp.Core -- cascade selftest
dotnet run --project src/FFOccamMcp.Core -- cascade run --url=https://example.com --task=closures --budget=800
```

## See also

- [ADR-0017](../adr/0017-cascade-facade.md) — design decisions
- [`occam_transcode`](occam_transcode.md) — full opt-in reader
- [Capability exam](../adr/0016-capability-exam.md) — when this is the only tool a client sees
