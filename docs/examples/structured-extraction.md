# Example: Structured extraction

```json
{ "name": "occam_playbook_resolve", "arguments": { "url": "https://example.com/item" } }
```

If resolve shows a knowledge schema:

```json
{
  "name": "occam_extract_knowledge",
  "arguments": {
    "url": "https://example.com/item"
  }
}
```

If there is no schema, use [`occam_transcode`](read-one-page.md) for prose instead.

## Expected

```json
{
  "ok": true,
  "facts": [{ "name": "title", "value": "…", "selector": "…" }],
  "meta": { "koId": "…" },
  "confidence": 0.82,
  "receipt": { "confidence": 0.82, "elapsedMs": 1240 }
}
```

- `facts[]` aligned to the playbook schema — values are as-extracted, unsigned.
- **`receipt` is extraction telemetry**, not Receipt v1. Do not pass it to [`occam_verify`](../tools/occam_verify.md). For signed integrity on the same page, use `occam_transcode` or `occam_claim_check`.

## Next

- [Guide: structured extraction](../guides/structured-extraction.md)
- [Receipts — telemetry vs Receipt v1](../receipts.md#not-receipt-v1-extract_knowledge-receipt)
- [Dataset export](dataset-export.md)
