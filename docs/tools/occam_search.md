# Search the web

**Canonical tool:** `occam_search`

Open-web search (query → result URLs). **Default** when `OCCAM_SEARCH_PROVIDER` is
unset: keyless DuckDuckGo HTML SERP with `provider: "duckduckgo"` disclosed in the
response. Occam does not index the web — it delegates discovery and names the source.

Override with `OCCAM_SEARCH_PROVIDER=searxng` \| `brave` \| `tavily` \| `external_cli`
(plus URL/key/binary as required), or `off` / `none` for the air-gap
`search_unconfigured` contract. For parallel multi-backend discovery set
`OCCAM_SEARCH_PROVIDERS=duckduckgo,brave,searxng` — response uses `provider: "fanout"`
and `providersUsed`. See [configuration](../configuration.md).

## When to use

- No URLs yet → search, then feed **`handle` or `url`** into probe / transcode / digest.
- Each hit includes `id` (`S1`…`Sn`, latest-search shorthand) and `handle` (`H…`, durable in
  this MCP process for 60 minutes / 64 entries). A later search remaps `S1` — keep `handle`
  or the raw `url` to retain a hit.
- Discovering pages within one known site → [`occam_map`](occam_map.md) is cheaper.
- `rerank=true` probes every hit and reorders so clean, HTTP-extractable pages rank above
  paywalls/anti-bot walls/JS stubs — worth the extra latency when you will transcode the winners.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `query` | string | — | **yes** | Search query |
| `max_results` | int | `8` | no | 1–20 |
| `rerank` | bool | `false` | no | Probe each hit and sort by extractability; adds `extractability` (0–1) + `recommendedBackend` per result. Extra probe latency |

## Returns

Success envelope:

- `ok: true`, `query`, `provider`, `count`
- `results[]` — `{id, handle, title, url, snippet?}`; with `rerank=true` also `extractability` and
  `recommendedBackend` (a hit whose probe failed keeps a mid-low score and no backend annotation)
- `providersUsed[]` — when `provider` is `fanout`, backends that returned ok hits
- `handleTtlS` (3600) and `handleScope` (`process`) — once per success envelope
- `agentHints.suggestedNext` — pass `handle` or `url`; `S1` is the latest search only

Failure envelope: `ok: false`, `query`, `failure: {code, message}`.

## Failure codes

`invalid_arguments`, `search_unconfigured` (provider `off` / incomplete explicit config),
`search_timeout` (retry or raise `OCCAM_SEARCH_PROVIDER_TIMEOUT_MS` / `OCCAM_SEARCH_TIMEOUT_MS`),
`search_rate_limited` (local rate window or all arms degraded), `search_http_<status>`,
`search_error` (empty/blocked SERP or parse miss). See [failure codes](../failure-codes.md).

## Example

Call (no env required after install):

```json
{ "query": "nginx rate limiting configuration", "max_results": 5 }
```

Trimmed response:

```json
{
  "ok": true,
  "query": "nginx rate limiting configuration",
  "provider": "duckduckgo",
  "count": 5,
  "results": [
    { "id": "S1", "handle": "H00000001", "title": "Rate Limiting with NGINX", "url": "https://blog.nginx.org/…", "snippet": "…" }
  ],
  "handleTtlS": 3600,
  "handleScope": "process",
  "agentHints": { "suggestedNext": "Pass result.handle or url to occam_transcode…" }
}
```

## Related

- [occam_probe](occam_probe.md) — the scorer rerank uses, on demand for one URL
- [occam_transcode](occam_transcode.md) / [occam_digest](occam_digest.md) — consume the results
- [Configuration](../configuration.md) — provider setup
