# Example: Discover then research

```json
{
  "name": "occam_map",
  "arguments": {
    "url": "https://nginx.org",
    "source": "sitemap",
    "max_links": 8
  }
}
```

Then digest the discovered links:

```json
{
  "name": "occam_digest",
  "arguments": {
    "urls": ["https://…"],
    "focus_query": "install"
  }
}
```

Or skip map and let digest discover:

```json
{
  "name": "occam_digest",
  "arguments": {
    "source_url": "https://nginx.org/en/docs/",
    "max_links": 4,
    "focus_query": "configuration"
  }
}
```

If map returns `sitemap_not_found`, retry with `"source": "homepage"`.

## Next

- [Research several URLs](research-several.md)
- [Read one page](read-one-page.md)
