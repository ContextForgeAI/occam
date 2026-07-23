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
| `backend_policy` | string | `http_then_browser` | no | `http`, `browser`, or `http_then_browser`; the playbook's routing can steer the effective backend |
| `session_profile` | string? | null | no | Headers profile id |

## Returns

Success envelope:

- `ok: true`, `url`, `playbookId`, `pageClass`
- `facts[]` — `{name, value, selector}` — each fact carries the CSS selector it came from
- `meta.koId`, `latencyMs`, `backend?`, `confidence`
- `receipt` — `{confidence, elapsedMs}`. **Note:** this is telemetry only — unlike
  `occam_transcode`, this receipt is *not* a signed envelope and cannot be fed to
  [`occam_verify`](occam_verify.md).

Failure envelope: `ok: false`, `url`, `failureCode`, `message`, `playbookId?`, `pageClass?`,
`partialFacts[]?` (fields that did extract before the failure), `agentHints?`, `latencyMs`.

## Failure codes

`invalid_arguments`, `workers_unavailable`, `playbook_not_found`, `knowledge_schema_missing`
(playbook exists but has no schema block), `page_class_unmatched` (URL matched no page class and no
default schema exists), `knowledge_schema_empty`, plus the transcode fetch taxonomy (`timeout`,
`http_*`, `captcha_or_challenge`, …). All of the schema-related codes mean: fall back to
`occam_transcode` for prose. See [failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://shop.example/product/123" }
```

Trimmed response:

```json
{
  "ok": true,
  "url": "https://shop.example/product/123",
  "playbookId": "shop.example",
  "pageClass": "product",
  "facts": [
    { "name": "title", "value": "Widget Pro", "selector": "h1.product-title" },
    { "name": "price", "value": "49.99", "selector": ".price-current" }
  ],
  "meta": { "koId": "ko_…" },
  "latencyMs": 850,
  "confidence": 0.9,
  "receipt": { "confidence": 0.9, "elapsedMs": 850 }
}
```

## Related

- [occam_playbook_resolve](occam_playbook_resolve.md) — check the schema exists first
- [occam_transcode](occam_transcode.md) — prose fallback
- [Failure codes](../failure-codes.md)
