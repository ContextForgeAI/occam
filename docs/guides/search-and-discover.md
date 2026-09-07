# Guide: Search and discover

## What is this?

Find candidate URLs (web search) or same-site links (map), then research them.

## When should I use it?

You have a question or a site root, not a finished URL list.

## Search the web

Requires no search env for the default DuckDuckGo path (see [Configuration](../configuration.md)).
Set `OCCAM_SEARCH_PROVIDER=off` to disable, or configure SearXNG/Brave/Tavily.

```json
{ "name": "occam_search", "arguments": { "query": "nginx reverse proxy", "max_results": 5 } }
```

Then pass URLs into `occam_digest`.

## Discover on a site

```json
{ "name": "occam_map", "arguments": { "url": "https://nginx.org", "source": "sitemap", "max_links": 8 } }
```

Or let digest discover from a `source_url` (see digest tool page).

For a bounded, resumable pass (URL/page/time/byte budgets, honest stop),
use the operator CLI `occam research` — it calls `occam_map` then
`occam_transcode`. It is not a new MCP tool. Example:
[site research](../examples/site-research/).

## Expected result

Search/map return candidate links — not full page bodies. Follow with digest/transcode.

## What can go wrong?

| Signal | Action |
|--------|--------|
| Search misconfigured | Set provider env; see configuration |
| `sitemap_not_found` | Retry map with `source: "homepage"` |

## Next

- [Research several sources](research-multiple.md)
- [Examples](../examples/search-then-research.md)
