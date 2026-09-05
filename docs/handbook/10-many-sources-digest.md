# Chapter 10 — Many sources in one call: digest

**Part C — Breadth** · Prerequisites: [Ch 7](07-materialization-token-contract.md), [Ch 9](09-discovery-before-acquisition.md) · Next: [Chapter 11](11-playbooks-resolution.md)

---

## Mental model

**Digest is the acquisition spine run N times under one budget**—plus combine ordering hints. It is not a discovery tool and **not a synthesizer**. `suggestedReadOrder` is a read hint, not generated summary across sources.

Rule: **several URLs → one `occam_digest`, not N× `occam_transcode`**—when one combined budget and focus apply.

---

## Explanation

### What digest does

`occam_digest(urls, …)`:

1. Validates the URL list (clamps apply—silent truncation at limits).
2. Applies SSRF/session preflight to the **whole batch** (not always per-item isolated).
3. For each URL, runs acquisition + compile similar to transcode (per-item policies possible).
4. Applies **one shared focus/budget** across items (`focus_query`, global `max_tokens`, `per_url_max_tokens`).
5. Returns per-item results with individual `ok`, markdown snippets, failures, and reduced receipts.

### What digest gives up vs N transcodes

| Aspect | Digest | N transcodes |
|--------|--------|--------------|
| Per-item full sidecars | Limited / absent on items | Full transcode sidecars each call |
| Playbook overlay on items | Not on digest items | Available per transcode |
| Receipt strength | **Reduced** — content hash only, no block leaves / time anchor | Full Receipt v1 when enabled |
| Failure isolation | Batch preflight can affect whole call | Independent per URL |
| Parallelism | Internal fan-out, **one in-flight per host** (cross-host up to cap), one MCP call | Agent-orchestrated |

### Per-item failures

Digest can return **`ok:true` overall** with some items `ok:false`—each item failure is typed. Task R: five reference pages + one deliberate 404 in the batch.

### Honest framing

Digest **concatenates and orders** bounded per-URL materializations under shared focus—it does not merge facts, resolve contradictions, or judge truth.

---

## CHECK

**NETWORK**

1. `occam_digest` five URLs including one known 404.
2. Assert: overall response succeeds structurally with a typed per-item failure for the 404.
3. Assert: successful items' receipts lack full block-leaf structure (reduced receipt).

Compare token totals vs five separate transcodes with the same `per_url_max_tokens`—digest should win on orchestration overhead, not on magic compression.

---

## Common misconception

**"Digest summarizes across sources."**

No model synthesizes across URLs. You receive bounded excerpts and hints; synthesis remains the agent's job—with citation discipline from each item's hash/receipt where present.

---

## Limitations

- Reduced receipts weaken per-item trust vs transcode ([Chapter 14](14-what-a-receipt-proves.md)).
- Batch SSRF/session preflight is whole-batch—not per-item session granularity.
- Silent URL count/token clamps—verify list length in response.
- Same-host URLs are extracted **sequentially** (polite); only distinct hosts fan out in parallel.
- For claim-level proofs, you may still need full transcode receipts on critical URLs.

---

## Links

**Public docs:** [Research multiple](../guides/research-multiple.md) · [Examples: research several](../examples/research-several.md) · [Tools reference](../tools-reference.md) (`occam_digest`)

**Next chapter:** [Chapter 11 — Playbooks in band](11-playbooks-resolution.md)
