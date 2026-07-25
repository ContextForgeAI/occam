# Example: Check a claim

```json
{
  "name": "occam_claim_check",
  "arguments": {
    "claim": "HTTP status 404 is not escalated to the browser backend",
    "url": "https://example.com/docs/backends"
  }
}
```

Batch citations:

```json
{
  "name": "occam_attest",
  "arguments": {
    "claims": [
      {
        "claim": "…",
        "sourceUrl": "https://example.com/a"
      },
      {
        "claim": "…",
        "sourceUrl": "https://example.com/b"
      }
    ]
  }
}
```

## Expected

Proven matching blocks — or honest `found: false` / unsupported rows. You still judge support vs refute.

## Next

- [Verify a receipt](verify-receipt.md)
- [Guide: claims](../guides/claims.md)
