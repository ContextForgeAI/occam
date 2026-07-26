# Example: Signed dataset export

```json
{
  "name": "occam_dataset_export",
  "arguments": {
    "urls": [
      "https://example.com/a",
      "https://example.com/b"
    ]
  }
}
```

## Expected

Per-URL rows with receipts, plus a manifest signature over the Merkle root of row leaves — an auditable set for later verification.

Tool page: [`occam_dataset_export`](../tools/occam_dataset_export.md)

## Next

- [Verify a receipt](verify-receipt.md)
- [Trust & Safety](../trust-and-safety.md)
