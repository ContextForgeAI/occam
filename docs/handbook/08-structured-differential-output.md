# Chapter 8 — Structured, differential and replayed output

**Part B — Reference path** · Prerequisites: [Chapter 7](07-materialization-token-contract.md) · Next: [Chapter 9](09-discovery-before-acquisition.md)

---

## Mental model

**Sidecars are opt-in projections of the same compiled content, and they share its budget.**

Blocks, tables, feed items, and chunks are slices of the materialized extract—not separate fetches. **`if_none_match` / `diff_against`** express the same contract as a delta against a caller-held `contentHash`; they compare Occam's hash of compiled markdown, not an HTTP ETag from the origin.

---

## Explanation

### Structured sidecars (opt-in)

| Parameter | Output | Honest note |
|-----------|--------|-------------|
| `json_blocks` | Citation blocks with selectors | Blocks collected internally always; param controls wire emission |
| `json_tables` | Tabular records | Useful for limits/pricing tables (Task R) |
| `json_feed` | RSS/Atom normalized items | When page is a feed |
| `semantic_chunking` | Fixed-size line chunks + heading breadcrumbs | **Not** semantic embedding chunking |
| `content_selectors` | Heading anchors | Not arbitrary CSS selectors |
| `rank_blocks` | Reorders blocks | Affects materialization |
| `tag_trust` | Heuristic trust annotation | Off by default; tag outside signature |
| `emit_capsule` | Transport wrapper | Signed core in unsigned wrapper ([Chapter 14](14-what-a-receipt-proves.md)) |

Only `MarkdownPassthroughCodec` runs in production; codec registry has no live selector surface.

### Differential reads

- **`if_none_match`** — Pass prior `contentHash`; unchanged page returns cheap unchanged envelope.
- **`diff_against`** — Returns delta vs prior hash; may force blocks into response even when `json_blocks=false`.
- **`delta_only`** — Emit only changed regions when paired with diff machinery.

These save tokens on **stable compiled output**, not on origin revalidation.

### Opt-in disk cache (`cache_ttl_s`)

When `cache_ttl_s > 0`, eligible responses persist whole signed envelopes locally. TTL checked **on read only**—no background sweep.

**Experimental / limits-first:**

- Cache key omits some materialization flags (`rank_blocks`, `tag_trust`, `emit_capsule`).
- URL **fragment omitted** from cache key—`page#a` can replay `page#b`'s stored envelope including its signed receipt.
- Do not use cache for fragment-sensitive or trust-sensitive reads without understanding collision risk.

### Translation

`translate_to` calls an external LibreTranslate endpoint. Translated markdown is **never** in signed bytes.

### Task R step 5

`json_tables` on the limits table; retain `contentHash`. Next week, `if_none_match` with that hash for a cheap unchanged check.

---

## CHECK

**NETWORK** — Fragment cache collision (reproduces known defect; use only on test URLs).

1. Set `cache_ttl_s` to a small positive value on a stable page URL with fragments, e.g. `https://example.com/doc#a`.
2. Fetch `…/doc#a`, then `…/doc#b`.
3. Inspect whether the second response replays the first (same `contentHash`, `cached:true`).

If yes, avoid fragment-scoped cache for production trust paths.

---

## Common misconception

**"`semantic_chunking` chunks semantically."**

It is a fixed-size line accumulator with heading breadcrumbs—not embeddings, not model segmentation. Name reflects intent history, not behavior.

---

## Limitations

- `diff_against` can inflate response by injecting blocks silently.
- Cache replays preserve original `ts`—receipts cannot prove freshness ([Chapter 14](14-what-a-receipt-proves.md)).
- LibreTranslate can block a request thread; external dependency.
- Public docs should mention cache limits whenever cache is mentioned.

---

## Links

**Public docs:** [Tools reference](../tools-reference.md) · [Concepts](../concepts.md) · [Read a page](../guides/read-a-page.md)

**Next chapter:** [Chapter 9 — Discovery before acquisition](09-discovery-before-acquisition.md)
