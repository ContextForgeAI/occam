# Example: Search then research

**Prerequisite:** `OCCAM_SEARCH_PROVIDER` configured ([Configuration](../configuration.md)).

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

Replace the URL list with search hits from the first call.

## Next

- [Discover then research](discover-then-research.md)
- [Guide: search and discover](../guides/search-and-discover.md)
