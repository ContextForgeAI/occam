# `occam_search`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamSearchTool.cs` @ 2026-09-29  
**Profiles:** `reader` (default), `basic`, `researcher`, `auditor`, `full` — not exposed in `minimal`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Open-web search: turn a query into normalized result URLs (`title`, `url`, optional `snippet`). Occam does not crawl or index the web — it delegates to a configured backend and discloses `provider` in the response. Default when `OCCAM_SEARCH_PROVIDER` is unset: keyless DuckDuckGo HTML (`provider: "duckduckgo"`). Pass a hit’s `handle` or `url` to probe, transcode, or digest next.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| You have no URLs yet and need discovery | You already know the URL(s) — use [`occam_transcode`](occam_transcode.md) or [`occam_digest`](occam_digest.md) |
| You want ranked links before fetching pages | Links **within one site** → [`occam_map`](occam_map.md) |
| Optional `rerank=true` when you will transcode winners | Guaranteed probe on a **single** hit — `rerank` runs only when there are **two or more** results |

Each hit gets `id` (`S1`…`Sn`) and `handle` (`H` + 8 hex digits, process-local). A later search remaps `S1`; keep `handle` or the raw `url`.

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `query` | yes | — | Search query. |
| `max_results` | no | `8` | Max results (1–20). |
| `rerank` | no | `false` | Rerank by extractability (extra probe latency). Only when more than one result is returned. |

## Response shape

- **Success (`ok: true`):** `query`, `provider`, `count`, `results[]` (`id`, `handle`, `title`, `url`, optional `snippet`). With `rerank=true` and multiple results: `extractability`, optional `recommendedBackend` (probe failure → score `0.4`). Fan-out may include `providersUsed[]`. Also `handleTtlS` (3600), `handleScope` (`process`), `agentHints`. Empty `count: 0` is still `ok: true`.
- **Failure (`ok: false`):** `query`, `failure: { code, message }` — do not invent page content.

## Common failure codes

| Code | Typical cause |
|------|----------------|
| `invalid_arguments` | Empty `query`, or `max_results` not in 1–20 |
| `search_unconfigured` | Disabled (`off`/`none`), unknown provider, missing URL/key, or empty fan-out |
| `search_timeout` | Backend or fan-out timeout |
| `search_rate_limited` | Local rate window or provider degraded |
| `search_http_<status>` | Backend HTTP non-success |
| `search_error` | Empty/blocked SERP, parse miss, network, or `external_cli` without binary |

Full taxonomy: [failure-codes](../failure-codes.md).

## Related env vars

| Variable | Role |
|----------|------|
| `OCCAM_SEARCH_PROVIDER` | Single provider (`duckduckgo`, `mojeek`, `qwant`, `startpage`, `bing`, `searxng`, `brave`, `tavily`, `external_cli`, or `off`/`none`) |
| `OCCAM_SEARCH_PROVIDERS` | CSV fan-out; token `keyless` → all built-in HTML SERPs without key/URL; **overrides** singular |
| `OCCAM_SEARCH_URL` / `OCCAM_SEARCH_API_KEY` | Required for searxng / brave / tavily as applicable |
| `OCCAM_EXTERNAL_SEARCH_PATH` | Optional path to `external_cli` binary |
| `OCCAM_SEARCH_TIMEOUT_MS` / `OCCAM_SEARCH_PROVIDER_TIMEOUT_MS` / `OCCAM_SEARCH_FANOUT_TIMEOUT_MS` | Timeouts |
| `OCCAM_SEARCH_RATE_MAX` / `OCCAM_SEARCH_RATE_WINDOW_S` / `OCCAM_SEARCH_DEGRADE_MINUTES` | Rate / degrade |

Full catalog: [configuration](../configuration.md).

## See also

- [`occam_probe`](occam_probe.md) · [`occam_transcode`](occam_transcode.md) · [`occam_digest`](occam_digest.md) · [`occam_map`](occam_map.md)
- [Choosing a tool](../choosing-a-tool.md) · [Recipes](../recipes.md)
