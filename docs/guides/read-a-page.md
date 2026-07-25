# Guide: Read a page

## What is this?

Fetch one live URL as compact Markdown for your AI.

## When should I use it?

You have a single page (docs, article, issue) and need its content in context.

## Minimal example

```json
{ "name": "occam_transcode", "arguments": { "url": "https://example.com" } }
```

Optional savings:

```json
{
  "name": "occam_transcode",
  "arguments": {
    "url": "https://example.com",
    "fit_markdown": true,
    "focus_query": "what you care about"
  }
}
```

Tool details: [`occam_transcode`](../tools/occam_transcode.md)

## Expected result

- `ok: true` with `markdown`  
- Often `receipt.signed`  
- Optional `quality.verdict` (`short_quality` / `rich`) — both usable  

## What can go wrong?

| Signal | Action |
|--------|--------|
| `ok: false` | Read `failure.code` — [failure codes](../failure-codes.md) |
| `requires_login` | [Sessions guide](sessions.md) |
| `thin_extract` | Bad extract — escalate backend or heal playbook (advanced) |

## Next

- [Research several sources](research-multiple.md)
- [Verify a source](verify-sources.md)
- [Example: read one page](../examples/read-one-page.md)
