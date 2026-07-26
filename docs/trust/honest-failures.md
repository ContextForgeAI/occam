# Honest failures

When Occam cannot extract a page, it returns **`ok: false`**.

That means the page content is **unknown**.

## What you should do

1. Read `failure.code` (and `agentMeta.decisions` when present).  
2. Follow [Failure codes](../failure-codes.md) for retry / stop / remediate.  
3. Do **not** summarize or quote the page from model memory.  

## What “thin extract” means

`thin_extract` means a **bad extraction** (chrome, shell, near-empty) — not “the page is short.”

A genuinely short but complete page can be `ok: true` with `quality.verdict=short_quality`.

## Honesty is not verification

Refusing to invent content is **honesty**. Checking a signed receipt later is **verification**. Use both when the stakes are high.

## Next

- [Failure codes](../failure-codes.md)
- [Troubleshooting](../troubleshooting.md)
- [Receipts](../receipts.md)
