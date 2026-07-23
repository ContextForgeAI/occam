# occam_map

Discover a site's same-domain links from its homepage, sitemap, or robots.txt — HTTP-only, up to
64 URLs.

## When to use

- You know the site but not the page URLs — map first, then feed picks into
  [`occam_digest`](occam_digest.md).
- Prefer `source=sitemap` for large/well-structured sites; `homepage` (default) for the rest.
- Searching the open web (not one site) → [`occam_search`](occam_search.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP or HTTPS seed URL |
| `source` | string | `homepage` | no | `homepage`, `sitemap`, or `robots` |
| `max_links` | int | `32` | no | 1–64 |
| `same_domain` | bool | `true` | no | Drop off-origin links |
| `filter_nonsense` | bool | `true` | no | Drop asset/webpack/mailto links |
| `focus_query` | string? | null | no | Re-rank by primary anchors (identifiers) over supporting terms; path/title/phrase before BM25. Homepage may expand one hub level when no strong hit |
| `timeout_ms` | int | `15000` | no | Total map/discovery timeout, including response bodies and sitemap traversal (3000–30000) |
| `session_profile` | string? | null | no | Headers profile id |

## Returns

Success envelope:

- `ok: true`, `url`, `finalUrl`, `source`, `timestamp`
- `links[]` — `{url, title?, path}` (no relevance score is returned; `focus_query` only affects ordering)
- `linkCount`, `filtered` (how many candidates were dropped), `focusQuery?`
- `partial: true` when sitemap/robots discovery found links but exhausted `timeout_ms` before finishing
- `expanded: true` when homepage focus ranking found no strong hit and a second-level hub crawl ran
- `agentHints` — `{suggestedNext: "occam_digest", maxDigestUrls, warnings[]}` (`focus_expand:…` when expanded)

On a partial success, `agentHints.warnings` says that `links[]` is incomplete. If the total deadline
expires before any link is found, the tool returns `ok: false` with `failureCode: "timeout"`.

Failure envelope: `ok: false`, `failureCode`, `message`, `url`, `finalUrl?`, `statusCode?`,
`agentHints?`, `timestamp`.

## Failure codes

`invalid_arguments`, `invalid_url`, `sitemap_not_found` (sitemap/robots discovery empty — retry
`source=homepage`), `thin_extract` (homepage had no extractable same-domain links),
`unsupported_content_type`, `timeout`, `private_url_blocked`, `http_<status>`,
`extraction_failed`. See [failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://nginx.org", "source": "sitemap", "max_links": 10, "focus_query": "load balancing" }
```

Python Docs focus (prefer `/library/asyncio` over What’s-new version pages):

```json
{ "url": "https://docs.python.org/3/", "source": "homepage", "focus_query": "asyncio", "max_links": 8 }
```

Trimmed response:

```json
{
  "ok": true,
  "url": "https://nginx.org",
  "source": "sitemap",
  "links": [ { "url": "https://nginx.org/en/docs/http/load_balancing.html", "title": "Using nginx as HTTP load balancer", "path": "/en/docs/http/load_balancing.html" } ],
  "linkCount": 10,
  "filtered": 122,
  "agentHints": { "suggestedNext": "occam_digest", "maxDigestUrls": 8, "warnings": [] }
}
```

## Related

- [occam_digest](occam_digest.md) — consume the discovered links
- [occam_search](occam_search.md) — open-web discovery instead of one site
