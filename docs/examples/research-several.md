# Example: Research several URLs

```json
{
  "name": "occam_digest",
  "arguments": {
    "urls": [
      "https://example.com/a",
      "https://example.com/b",
      "https://example.com/c"
    ],
    "focus_query": "key setup steps",
    "fit_markdown": true
  }
}
```

## Expected

- Per-URL items
- Combined digest when `include_combined` is true (default)

Use **one** digest call — not three separate transcodes.

## Next

- [Search then research](search-then-research.md)
- [Check a claim](check-a-claim.md)
