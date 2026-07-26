# Chapter 13 — Typed field extraction

**Part D — Site-specific** · Prerequisites: [Ch 11](11-playbooks-resolution.md), [Ch 2](02-honesty-contract.md) · Next: [Chapter 14](14-what-a-receipt-proves.md)

---

## Mental model

**A separate spine with its own worker and narrower failure taxonomy.**

Recipe D: **`occam_playbook_resolve` → schema match → `occam_extract_knowledge`**. Caller must supply `knowledge_schema` (or rely on resolved schema). There is **no schema-free mode**. Output is `facts[]` plus **`Receipt` telemetry**—not Receipt v1.

Hard dependency: **PS-4 extraction requires PS-5 playbook resolution.**

---

## Explanation

### What the tool returns

- **`facts[]`** — Typed fields per caller schema (selectors, types, row shapes where supported).
- **`meta.koId`** — Knowledge object identifier when configured.
- **`Receipt` object `{ confidence, elapsedMs }`** — **Extraction telemetry only** (OD-5). Unsigned. No `contentHash`, no signature, no Merkle root. **`occam_verify` does not accept it.**
- **`confidence`** in telemetry is heuristic—often `0.0`; not a correctness guarantee.

### What this path skips

Compared to `occam_transcode` ([Chapter 4](04-request-path.md)):

- No `OccamRouter` post-processor pipeline (thin/challenge reclassification path differs)
- No token budget (`max_tokens` ignored)
- No Receipt v1 signing
- Browser leg may spawn throwaway Playwright per call (no pool reuse on browser fallback)
- **`session_profile` `storageState` silently dropped** on browser fallback leg (tier 3)

### Row mode

Host parsers do not set `base_selector` for row mode—**row mode is dead**. Document field/list selectors only.

### Security (do not point at untrusted URLs)

- **css-extract** lacks DNS pinning and body cap on some paths.
- **Nuxt `readNuxtPath`** evaluates page-controlled state via `eval()`.

Typed extraction is for **known site shapes with operator-authored schemas**, not arbitrary open-web scraping.

### Task R step 10

Extract `{limit, window, scope}` via schema saved in [Chapter 12](12-authoring-playbook.md) instead of re-parsing markdown.

---

## CHECK

**NETWORK**

1. Call `occam_extract_knowledge` and `occam_transcode` on the same URL with `max_tokens` set on both.
2. Assert: extract ignores budget; transcode respects it.
3. Assert: extract `Receipt.confidence` is heuristic (often `0.0`); no signed `receipt.signed` envelope on extract response.

Attempt `occam_verify` on extract telemetry—it must reject/not apply.

---

## Common misconception

**"`occam_extract_knowledge` returns a Receipt, so its output is verifiable."**

The wire field named `Receipt` is **extraction telemetry**, not Occam Receipt v1. Only signed extraction envelopes from transcode/claim paths verify.

---

## Limitations

- Worker timeout hardcoded ~45s.
- No Merkle citations from extract path—for quotable proofs, use transcode + `claim_check` ([Chapter 14](14-what-a-receipt-proves.md), future Ch 16).
- Values are as-extracted, unsigned—schema validation is structural, not truth.
- Do not present as safe for untrusted URLs until EF-013/043 mitigations ship.

---

## Links

**Public docs:** [Structured extraction](../guides/structured-extraction.md) · [Examples: structured extraction](../examples/structured-extraction.md) · [Tools: occam_extract_knowledge](../tools/occam_extract_knowledge.md)

**Next chapter:** [Chapter 14 — What a receipt proves](14-what-a-receipt-proves.md)
