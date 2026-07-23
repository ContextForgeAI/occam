# occam_search

Open-web search (query → result URLs) via a configured provider (SearXNG, Brave, or Tavily). Your
discovery step when you don't have URLs yet.

> **Requires configuration.** The tool is always listed, but every call fails with
> `search_unconfigured` until the host sets `OCCAM_SEARCH_PROVIDER` (`searxng` | `brave` | `tavily`)
> plus `OCCAM_SEARCH_URL` (SearXNG) or `OCCAM_SEARCH_API_KEY` (Brave/Tavily).
> See [configuration](../configuration.md).

## When to use

- No URLs yet → search, then feed results into probe / transcode / digest.
- Discovering pages within one known site → [`occam_map`](occam_map.md) is cheaper and needs no provider.
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
- `results[]` — `{title, url, snippet?}`; with `rerank=true` also `extractability` and
  `recommendedBackend` (a hit whose probe failed keeps a mid-low score and no backend annotation)
- `agentHints.suggestedNext` — what to do with the results

Failure envelope: `ok: false`, `query`, `failure: {code, message}`.

## Failure codes

`invalid_arguments`, `search_unconfigured`, `search_timeout` (retry or raise
`OCCAM_SEARCH_TIMEOUT_MS`), `search_http_<status>` (backend endpoint/key problem), `search_error`
(other backend failure). See [failure codes](../failure-codes.md).

## Example

Call:

```json
{ "query": "nginx rate limiting configuration", "max_results": 5, "rerank": true }
```

Trimmed response:

```json
{
  "ok": true,
  "query": "nginx rate limiting configuration",
  "provider": "searxng",
  "count": 5,
  "results": [
    { "title": "Rate Limiting with NGINX", "url": "https://blog.nginx.org/…", "snippet": "…", "extractability": 0.94, "recommendedBackend": "http" }
  ],
  "agentHints": { "suggestedNext": "results reranked by extractability — prefer the top (highest extractability) URLs for transcode" }
}
```

## Related

- [occam_probe](occam_probe.md) — the scorer rerank uses, on demand for one URL
- [occam_transcode](occam_transcode.md) / [occam_digest](occam_digest.md) — consume the results
- [Configuration](../configuration.md) — provider setup
