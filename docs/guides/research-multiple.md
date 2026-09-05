# Guide: Research several sources

## What is this?

Read up to eight URLs in one research call and synthesize with a focus query.

## When should I use it?

You already have several URLs (or a search/map result) and need a combined brief — not N separate full-page dumps.

## Minimal example

```json
{
  "name": "occam_digest",
  "arguments": {
    "urls": [
      "https://example.com/a",
      "https://example.com/b"
    ],
    "focus_query": "installation steps"
  }
}
```

Prefer **one** `occam_digest` over N× `occam_transcode`. Same-site batches (typical after `occam_map`) stay **polite**: one extract in flight per host; distinct hosts may still run in parallel.

Tool: [`occam_digest`](../tools/occam_digest.md)

## Expected result

- Per-URL items with extracts or typed failures  
- Combined synthesis when enabled (default)  

## What can go wrong?

Individual URLs can fail while others succeed — read per-item failures; do not invent missing pages.

## Next

- [Search and discover](search-and-discover.md)
- [Example: research several URLs](../examples/research-several.md)
- [Check a claim](claims.md)
