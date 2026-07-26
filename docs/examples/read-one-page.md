# Example: Read one page

## Minimal

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://example.com"
  }
}
```

## With focus (optional)

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://developer.mozilla.org/en-US/docs/Web/HTTP",
    "fit_markdown": true,
    "focus_query": "HTTP request methods"
  }
}
```

## Expected

- `ok: true`
- Non-empty `markdown`
- `receipt.signed` when signing is on (default)

## If it fails

Read `failure.code` — do not invent the page. [Failure codes](../failure-codes.md)

## Next

- [Research several URLs](research-several.md)
- [Verify a receipt](verify-receipt.md)
