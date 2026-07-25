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

`facts[]` plus metadata such as `meta.koId` when the schema applies.

## What can go wrong?

No schema → use transcode. Bad selectors → playbook heal/lint/save (authoring path only).

## Next

- [Example: structured extraction](../examples/structured-extraction.md)
- [`occam_extract_knowledge`](../tools/occam_extract_knowledge.md)
