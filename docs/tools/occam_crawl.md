# occam_crawl

**Status:** EXPERIMENTAL · enable with `OCCAM_CRAWL_MCP=1`  
**Contract:** runtime `tools/list` when the flag is set · [ADR-0018](../adr/0018-crawl-mcp.md)

Same-domain crawl: discover links from a seed (`sitemap` then `homepage` fallback), then transcode up to `max_pages`. Pass `resume_token` from a prior response to continue the queue. Optional `jsonl` of page rows.

Not a CAPTCHA unlocker. Soft challenges still follow probe → transcode → optional `session_profile`.

## Parameters

| Name | Required | Default | Notes |
|------|----------|---------|-------|
| `url` | if no resume | — | Seed URL |
| `resume_token` | if no url | — | Caller-held queue from prior crawl |
| `max_pages` | no | 8 | 1–32 |
| `max_links` | no | 32 | Discovery budget (1–64) |
| `map_source` | no | `sitemap` | `sitemap` or `homepage` |
| `backend_policy` | no | `http_then_browser` | |
| `focus_query` / `fit_markdown` / `per_url_max_tokens` | no | prune defaults | Per-page compile |
| `session_profile` / `playbook_policy` | no | — / `auto` | |
| `include_jsonl` | no | `true` | |

## Related

- [experimental.md](../experimental.md)
- [occam_map](occam_map.md) · [occam_digest](occam_digest.md)
