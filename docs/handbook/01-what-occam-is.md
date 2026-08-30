# Chapter 1 — What Occam is, and the six things it is not

**Part A — Orientation** · Prerequisites: none · Next: [Chapter 2](02-honesty-contract.md)

---

## Mental model

**URL in → compiled, budgeted markdown out, or a typed refusal.**

The object Occam returns is not the HTTP response body. It is a *compiled reading* of the page: extracted text after DOM processing, playbook overlay, token budgeting, optional focus pruning, and optional sidecars. The hash a receipt commits to is over that compiled form, not over the origin's bytes.

Occam is a **locally run host process** (primarily an MCP server) that helps LLM agents read the web honestly: live acquisition, token-aware materialization, typed failures, and optional tamper-evidence—not a hosted service, not a search engine, not a fact-checker.

---

## Explanation

### What Occam does

Occam exposes web content to agents through a reference path and several parallel spines (see [Chapter 4](04-request-path.md)). The default act is `occam_transcode(url)` with only `url` required.

Mechanisms, in order of user impact:

1. **Acquisition** — A gated ladder tries HTTP extraction first, Playwright Chromium when HTTP is unusable, and an operator-configured third-party provider only after both local backends fail under `http_then_browser`. The ladder has exits (404/410, public-reference pages); it is not an unconditional cascade.
2. **Classification** — Post-processors can downgrade apparent success to typed failures: `thin_extract`, `captcha_or_challenge`, `requires_login`, and others.
3. **Materialization** — Surviving content is compiled to markdown under a whole-response token budget (ambient default: 20% of declared client context, clamped 512–16384 tokens).
4. **Honesty** — `ok:false` means content is **unknown**. Failures carry `failure.code`, `recovery[]`, and `agentMeta.decisions`.
5. **Optional integrity** — Receipt v1 can sign compiled bytes and Merkle-committed blocks. Signing proves integrity relative to a local key, not provenance or truth ([Chapter 14](14-what-a-receipt-proves.md)).

Nine product systems sit behind this surface: acquisition, materialization, discovery, schema extraction, playbooks, trust/receipts, composition (digest/watch/batch/crosscheck), MCP exposure, and operator install/connect. This chapter names them only; later chapters teach each.

### Task R — two silent failure modes

Task R: *What rate limits does this API document state?*

Without Occam, an agent typically fails in one of two silent ways:

- **Invented content** — The model recalls training data or guesses policy numbers never on the page.
- **Empty shell mistaken for the page** — A generic fetch returns a JS marketing shell or consent interstitial; the agent treats whitespace as "no limits documented."

Occam's first honest call on a client-rendered landing page should return `ok:false` with a meaningful code (often `thin_extract`), not fabricated limits. A genuinely short changelog page may return `ok:true` with `quality.verdict=short_quality`—same small size, opposite meaning ([Chapter 2](02-honesty-contract.md)).

### Six high-frequency rejections

These are the six categories most often confused with Occam. Twelve more exist in the product definition; they appear as misconceptions in later chapters.

| Occam is **not**… | What the code shows | What to say instead |
|-------------------|---------------------|---------------------|
| **A fetcher** | Returns compiled markdown; `contentHash` is SHA-256 of UTF-8 compiled markdown after budgeting/focus, not raw HTML | "Occam compiles a reading of the page" |
| **A crawler** | `occam_map` caps at 64 links with bounded second-level expansion; no frontier queue | "Bounded link listing from sitemap/homepage/robots" |
| **A CAPTCHA bypass** | No solver; challenges become `captcha_or_challenge` | "Detects walls; sessions/browser/managed may pass some legitimately" |
| **A cache or CDN** | Default path never reads disk cache; live extract every call | "Re-extracts unless caller opts into local TTL replay" |
| **A search engine** | `occam_search` discloses `provider` (default DuckDuckGo HTML; not an Occam index) | "Queries the operator-configured or default search provider" |
| **A fact-checker or summarizer** | No LLM in host/workers; `claim_check` hardcodes `not_evaluated`; reductions are selection/truncation with `compile.omitted` | "Retrieves passages and proves membership in signed extracts; does not judge truth" |

---

## CHECK

**NETWORK** — Compare compiled output to raw transport bytes.

1. Call `occam_transcode` on a stable public page (e.g. `https://example.com/`).
2. Fetch the same URL with `curl` (or your HTTP client).
3. Diff the returned `markdown` against the response body.

They are different objects. Occam's markdown is extracted and compiled; the receipt hash (if present) binds to Occam's markdown, not to `curl` output.

---

## Common misconception

**"It is a fetcher."**

A fetcher returns the response body (or status). Occam returns compiled markdown and optional structured sidecars. Cryptographic commitments apply to the compiled form. Treating Occam as "just HTTP" leads to wrong expectations about hashes, diffs, and verification.

---

## Limitations

- This chapter deliberately avoids token-reduction percentages. The tokenizer is `heuristic-unicode-v1` with unmeasured error bounds; any future figure must declare baseline and tier.
- "15 core tools" is one exposure slice of a larger product surface (51 named entrypoints)—not the whole product ([configuration](../configuration.md), [Chapter 18 — Exposure](18-exposure.md)).
- npm (`ff-occam`, backed by `@ff-occam/mcp`) is a published experimental RC channel, not the guarded GA install path; prefer the signed bootstrap for the release install ([Chapter 3](03-standing-up-an-install.md)).
- Occam does not prove truth, origin, identity, or trusted time—ever.

---

## Links

**Public docs:** [What is Occam?](../what-is-occam.md) · [How Occam works](../how-occam-works.md) · [Concepts](../concepts.md) · [Trust & Safety](../trust-and-safety.md)

**Next chapter:** [Chapter 2 — The honesty contract](02-honesty-contract.md)
