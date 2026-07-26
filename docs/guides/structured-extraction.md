# Guide: Structured extraction

## What is this?

Pull typed fields (`facts[]`) when a playbook defines a knowledge schema.

## When should I use it?

You need fields (price, version, author) — not just prose Markdown.

## Minimal flow

```text
occam_playbook_resolve(url)
→ if schema present → occam_extract_knowledge(url)
→ else → occam_transcode(url) for prose
```

```json
{ "name": "occam_extract_knowledge", "arguments": { "url": "https://example.com/product" } }
```

## Expected result

- `facts[]` plus metadata such as `meta.koId` when the schema applies.
- `confidence` — heuristic extraction confidence, not a correctness guarantee.
- **`receipt` — extraction telemetry only** (`confidence`, `elapsedMs`). This is **not** Occam Receipt v1: no `contentHash`, no signature, **`occam_verify` does not accept it**. For signed integrity, use [`occam_transcode`](../tools/occam_transcode.md) or [`occam_claim_check`](../tools/occam_claim_check.md) on the same URL.

## Session note

`occam_extract_knowledge` is **Tier 3** for sessions: HTTP headers apply, but Playwright `storageState` is silently dropped. See [Guide: sessions](sessions.md).

## What can go wrong?

No schema → use transcode. Bad selectors → playbook heal/lint/save (authoring path only).

## Next

- [Example: structured extraction](../examples/structured-extraction.md)
- [`occam_extract_knowledge`](../tools/occam_extract_knowledge.md)
- [Receipts — not Receipt v1](../receipts.md#not-receipt-v1-extract_knowledge-receipt)
