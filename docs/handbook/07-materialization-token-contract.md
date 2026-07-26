# Chapter 7 — Materialization: the token contract

**Part B — Reference path** · **Spine chapter** · Prerequisites: [Chapter 4](04-request-path.md) · Next: [Chapter 8](08-structured-differential-output.md)

---

## Mental model

**Budgeting is not truncation.**

The token budget is a **whole-response contract** shared by markdown and every opt-in sidecar. Reductions are deterministic selection or truncation of text already extracted—no model, no summarization. What was removed is reported in `compile.omitted`. The **`contentHash` covers compiled UTF-8 markdown after budgeting/focus/fit**—so two budgets on the same page produce two legitimate, different hashes.

---

## Explanation

### Ambient vs explicit budget

1. **`occam_client_capabilities(context_tokens=N)`** — Once per session, stores client window. Default read budget ≈ **20% of N**, clamped **[512, 16384]** tokens when `max_tokens` omitted.
2. **`max_tokens` on the call** — Explicit whole-response cap overriding ambient default for that request.

Tokenizer: **`heuristic-unicode-v1`** — fast estimate with **unmeasured error bounds**. Do not publish global "X% reduction" headlines; any figure needs declared baseline and tier.

### Mechanisms (all selection/truncation, never generation)

| Mechanism | Effect |
|-----------|--------|
| Extraction to markdown | Drops nav, scripts, ads via readability/turndown |
| Whole-response budget | Caps markdown + sidecars together (`BudgetOwnership`) |
| `fit_markdown` | BM25 paragraph **prune** toward content |
| `focus_query` | Ranks sections; keeps focus-relevant regions |
| `compile.omitted` | Lists what budget/fit/focus removed |
| `per_url_max_tokens` (digest) | Per-item cap inside one digest budget |

### Sidecars share the budget

Requesting `json_tables`, `json_blocks`, `json_feed`, or chunks consumes the same token pool as markdown. Structured output cannot silently expand the window.

Tools **outside** global budgeting: `occam_claim_check` and `occam_dataset_export` apply no token budget—load-bearing for `leafSetComplete` semantics ([Chapter 14](14-what-a-receipt-proves.md)).

### Hash implications (load-bearing for trust)

- `contentHash = sha256:` + hex(SHA-256(utf8(compiled_markdown))).
- Changing `max_tokens`, `fit_markdown`, `focus_query`, or ambient budget mid-session changes compiled bytes → **different hash**.
- Two receipts for one URL under different budgets are not contradictory—they bind different materializations.
- `translate_to` output is **not** in signed bytes.

### Task R step 4

Rate-limit reference page (~large): read once with defaults, once with explicit `max_tokens`, once with `fit_markdown:true` + `focus_query="rate limit"`. Compare `compile.omitted` across the three—same URL, three contracts.

---

## CHECK

**NETWORK**

1. Transcode the same URL twice with different `max_tokens` values (e.g. 2000 vs 8000).
2. Assert: two different `contentHash` values.
3. Assert: smaller response's `compile.omitted` names what was cut.

Do not assert exact token counts—tokenizer bounds are unmeasured.

---

## Common misconception

**"Occam summarizes the page down to the budget."**

There is no LLM in the host or workers. Every word in output came from extracted page text; omissions are listed. Summarization and Occam are different products.

---

## Limitations

- `max_tokens` bounds content, not every serialized metadata field.
- `ResponseBudgetDiagnostics` may be computed but not exposed on the wire—do not promise observable budget diagnostics beyond `compile.omitted`.
- Reduction percentage is not a quality metric—`compile.omitted` exists because size and usefulness diverge.
- Receipts bind compiled form, not raw HTML—auditors must internalize this before verification ([Chapter 14](14-what-a-receipt-proves.md)).

---

## Links

**Public docs:** [Concepts](../concepts.md) · [Tools reference](../tools-reference.md) (`max_tokens`, `fit_markdown`, `focus_query`) · [Read a page](../guides/read-a-page.md)

**Next chapter:** [Chapter 8 — Structured, differential and replayed output](08-structured-differential-output.md)
