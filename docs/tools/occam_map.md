# `occam_map`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamMapTool.cs` @ 2026-09-29  
**Profiles:** `reader` (default), `researcher`, `auditor`, `full` — not exposed in `minimal` or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Discover a site's same-domain links from its homepage, sitemap, or robots.txt (HTTP-only, up to 64 URLs). Use to find pages to feed into `occam_digest` when you do not have the URLs yet.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| You know the site but not specific page URLs — map first, then [`occam_digest`](occam_digest.md) | Open-web discovery across sites → [`occam_search`](occam_search.md) |
| Large sites → prefer `source=sitemap`; otherwise default `homepage` | Full page body → [`occam_transcode`](occam_transcode.md) |
| Topic-oriented ranking → set `focus_query` | Browser render / heavy login walls beyond session headers — map is HTTP-only |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) seed URL or search handle (`S1` / `H…`). |
| `source` | no | `homepage` | `homepage`, `sitemap`, or `robots`. |
| `max_links` | no | `32` | Maximum links to return (1–64). |
| `same_domain` | no | `true` | When true, drop off-origin links. |
| `filter_nonsense` | no | `true` | Drop asset/webpack/mailto links. |
| `focus_query` | no | null | Focus keywords: entity-first re-rank; homepage may expand one hub level. |
| `timeout_ms` | no | `15000` | Total map/discovery timeout (3000–30000). |
| `session_profile` | no | null | Session profile id under `OCCAM_SESSIONS_ROOT/<id>.json`. |

Out-of-range `max_links` / `timeout_ms` → `invalid_arguments` before fetch.

## Response shape

**Success (`ok: true`):** `url`, `finalUrl`, `source`, `timestamp`, `links[]` (`{ url, title?, path }`), `linkCount`, `filtered`, optional `focusQuery`, `partial` (sitemap/robots timed out with some links), `expanded` (homepage hub crawl), `agentHints` (`suggestedNext: "occam_digest"`, `maxDigestUrls: 8`, `warnings[]`).

**Failure (`ok: false`):** read `failureCode` and `message` — never invent page content.

## Common failure codes

| Code | Typical cause |
|------|----------------|
| `invalid_arguments` | Bad `source`, or `max_links` / `timeout_ms` out of range |
| `invalid_url` | Not a valid absolute HTTP(S) URL |
| `stale_handle` / `unknown_handle` | Search handle expired or missing |
| `invalid_session_profile` / `session_profile_not_found` | Bad or missing session profile |
| `private_url_blocked` | Private/local URL blocked |
| `unsupported_content_type` | Not HTML suitable for link discovery |
| `sitemap_not_found` | Sitemap/robots found no links |
| `thin_extract` | Homepage had no extractable same-domain links after filtering |
| `timeout` | Deadline exceeded with no usable link set |
| `http_<status>` | HTTP error from fetch |
| `extraction_failed` | Homepage fetch failed / no usable HTML |

Full taxonomy: [failure codes](../failure-codes.md).

## Related env vars

| Variable | Role |
|----------|------|
| `OCCAM_SESSIONS_ROOT` | Session profile JSON directory |
| `OCCAM_ALLOW_PRIVATE_URLS` | Allow private/local seed URLs |
| `OCCAM_REQUEST_HEADERS_FILE` | Optional JSON headers for map HTTP fetches |

Full catalog: [configuration](../configuration.md).

## See also

- [`occam_digest`](occam_digest.md) · [`occam_search`](occam_search.md)
- [Choosing a tool](../choosing-a-tool.md) · [Recipes](../recipes.md)
