# Chapter 9 — Spending less: discovery before acquisition

**Part C — Breadth** · Prerequisites: [Ch 2](02-honesty-contract.md), [Ch 5](05-acquisition-ladder.md) · Next: [Chapter 10](10-many-sources-digest.md)

---

## Mental model

**Three signals that are not one scale.**

- **`extractability`** (probe, search rerank) — prediction *before* full fetch
- **`confidence` / `quality`** — measurement *after* fetch
- **Playbook `verify.score`** — authoring gate heuristic (0–100)

Never compare them as interchangeable scores. Discovery tools cheaply answer *whether* and *which URL* before paying extraction cost.

---

## Explanation

### `occam_probe`

- HTTP-only classification: redirects, SPA hints, robots, tier hints, failure prediction.
- **Never escalates** to browser—browser-only pages can be mis-predicted.
- May attach `agentHints` for next steps.
- SSRF blocks may appear as `network_error`—misleading for private URLs.
- Output is **unsigned**—not verifiable extract.

### `occam_map`

- Sources: `sitemap`, `robots`, homepage links, bounded hub expansion.
- Hard cap: **64 links** in `links[]`.
- HTTP-only—JS navigation menus may yield empty/sparse maps.
- Listing, not crawling—no frontier expansion beyond second level.

### `occam_search`

- Registered core tool but **fails closed** without `OCCAM_SEARCH_PROVIDER`.
- Proxies operator-configured provider (Tavily, etc.)—not Occam's index.
- Optional `rerank` can fire many live probes (up to ~20) ordering by **extractability**, not relevance.
- Results are pointers + hints, not page content.

### Task R step 6

1. Probe docs index—cheap extractability read.
2. Map with `source:"sitemap"` to enumerate reference URLs under cap.
3. If site unknown, search with configured provider—then transcode chosen URLs.

Workflow: discover → acquire ([Chapter 5](05-acquisition-ladder.md)), not discover instead of honesty ([Chapter 2](02-honesty-contract.md)).

---

## CHECK

**LOCAL / NETWORK**

1. **`occam_search`** with no provider configured → must fail closed (typed error), not empty success.
2. **`occam_map`** on a large site → assert `links.length ≤ 64`.

---

## Common misconception

**"Probe tells me whether the page will extract."**

Probe predicts from HTTP-only signals. It can be wrong on browser-rendered content. When stakes are high, transcode—or probe plus conservative `backend_policy`.

---

## Limitations

- Nothing in discovery PS-3 is signed or hashed.
- Map/probe never use session `storageState` tier-1 browser state ([Chapter 6](06-when-acquisition-is-hard.md)).
- Search relevance is the provider's job; Occam only reranks by extractability.
- `probe.autoRedirect` may be registered but not selected—do not rely on it.

---

## Links

**Public docs:** [Search & discover](../guides/search-and-discover.md) · [Choosing a tool](../choosing-a-tool.md) · [Tools: occam_probe](../tools/occam_probe.md) · [Tools: occam_map](../tools/occam_map.md) · [Tools: occam_search](../tools/occam_search.md)

**Next chapter:** [Chapter 10 — Many sources in one call: digest](10-many-sources-digest.md)
