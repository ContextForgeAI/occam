# occam_extract_knowledge

Extract typed structured fields from a page (e.g. title, price, author) as `facts[]`, driven by the
site's playbook `knowledge_schema`.

## When to use

- You need specific data points, not prose — and the host has a resolvable schema
  (check with [`occam_playbook_resolve`](occam_playbook_resolve.md) first).
- No schema for the host → use [`occam_transcode`](occam_transcode.md) and read the markdown.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP or HTTPS URL (same URL you'd pass to resolve) |
| `backend_policy` | string | `http_then_browser` | no | `http`, `browser`, or `http_then_browser` |
| `session_profile` | string? | null | no | Headers profile id |

## Returns

Success envelope:

- `ok: true`, `url`, `playbookId`, `pageClass`
- `facts[]` — `{name, value, selector}` — typed fields as extracted; **unsigned**, as-is from DOM
- `meta.koId`, `latencyMs`, `backend?`, `confidence` — heuristic confidence, not a correctness guarantee
- `receipt` — **extraction telemetry only** (`confidence`, `elapsedMs`). **Not Receipt v1.** No
  `contentHash`, no signature, **not accepted by [`occam_verify`](occam_verify.md)**.

Failure envelope: `ok: false`, `url`, `failureCode`, `message`, `playbookId?`, `pageClass?`,
`partialFacts[]?`, `agentHints?`, `latencyMs`.

## Failure codes

`invalid_arguments`, `workers_unavailable`, `playbook_not_found`, `knowledge_schema_missing`,
`page_class_unmatched`, `knowledge_schema_empty`, plus transcode fetch taxonomy. See
[failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://shop.example/product/123" }
```

Trimmed response:

```json
{
  "ok": true,
  "playbookId": "shop.example",
  "pageClass": "product",
  "facts": [
    { "name": "title", "value": "Widget Pro", "selector": "h1.product-title" }
  ],
  "confidence": 0.9,
  "receipt": { "confidence": 0.9, "elapsedMs": 850 }
}
```

The `receipt` field is telemetry — do not feed it to `occam_verify`.

## Related

- [occam_playbook_resolve](occam_playbook_resolve.md)
- [occam_transcode](occam_transcode.md) — prose + Receipt v1
- [Receipts](../receipts.md) — Receipt v1 vs telemetry
