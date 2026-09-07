# Example: Search then research

**Prerequisite:** none for search — keyless DuckDuckGo is the default ([Configuration](../configuration.md)). Set `OCCAM_SEARCH_PROVIDER=off` for air-gap.

```json
{
  "name": "occam_search",
  "arguments": {
    "query": "nginx reverse proxy setup",
    "max_results": 5
  }
}
```

Then:

```json
{
  "name": "occam_digest",
  "arguments": {
    "urls": ["https://…", "https://…"],
    "focus_query": "reverse proxy configuration",
    "fit_markdown": true
  }
}
```

Replace the URL list with search hits from the first call (`url` or `handle`; `S1` is the latest search only).

## Next

- [Discover then research](discover-then-research.md)
- [Guide: search and discover](../guides/search-and-discover.md)
