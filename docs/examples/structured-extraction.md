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

`facts[]` aligned to the playbook schema.

## Next

- [Guide: structured extraction](../guides/structured-extraction.md)
- [Dataset export](dataset-export.md)
