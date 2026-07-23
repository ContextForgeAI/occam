# Choosing a tool

**What you'll do:** pick the right `occam_*` tool from your goal — without memorizing all fifteen names.

---

## Decision guide

| I want to… | Call | Notes |
|------------|------|-------|
| **Read one page as Markdown** | `occam_transcode` | Only `url` is required; add `focus_query` + `fit_markdown` to save tokens |
| **Check if a URL is worth fetching** | `occam_probe` | Cheap; returns extractability score and recommended backend |
| **Research several pages** | `occam_digest` | Up to 8 URLs; use `focus_query` for synthesis |
| **Find URLs on a site I don't know yet** | `occam_map` → `occam_digest` | Map discovers links; digest reads them |
| **Search the web for URLs** | `occam_search` → probe/transcode | Requires `OCCAM_SEARCH_PROVIDER` |
| **Get structured fields (price, author, …)** | `occam_playbook_resolve` → `occam_extract_knowledge` | Needs a playbook with `knowledge_schema` |
| **Use a site's tuned extract recipe** | `occam_playbook_resolve` → `occam_transcode` | `playbook_policy=auto` (default) |
| **Fix a hard site (draft a playbook)** | `occam_transcode` fails → `occam_playbook_heal` → edit JSON → `occam_playbook_lint` → `occam_playbook_save` | Local only |
| **Prove a page backs one sentence** | `occam_claim_check` | Returns citation proof; you judge support vs refute |
| **Check all citations before publishing** | `occam_attest` | Batch of `{claim, sourceUrl}` rows |
| **Verify a signed extraction** | `occam_verify` | Offline signature check; optional live drift |
| **Build an auditable URL set for RAG** | `occam_dataset_export` | 1–20 URLs, manifest signature |
| **Watch a page for changes** | `occam_watch` | Opt-in: `OCCAM_WATCH_MCP=1` |
| **Detect cloaking / personalization** | `occam_crosscheck` | Opt-in: `OCCAM_CONSENSUS_MCP=1` |
| **See which hosts are dead ends** | `occam_failure_atlas` | Opt-in: `OCCAM_ATLAS_MCP=1` |
| **Queue many URLs asynchronously** | `occam_batch_submit` → status → results | Opt-in: `OCCAM_BATCH_MCP=1` |

---

## Common flows

### Read one article

```
occam_probe(url)          # optional — skip if you trust the URL
occam_transcode(url)
```

Add `fit_markdown: true` and `focus_query: "your question"` when context is tight.
For technical references, the focus ranker preserves numeric identifiers and exact anchors. A URL such
as `https://example.org/spec#section-15.5.2` uses the fragment as local section intent while fetching the
fragment-free page.

On success, optional `quality.verdict` may be `short_quality` or `rich` — both are usable. Do not treat a short body as failure.

### Multi-source research

```
occam_search(query)       # optional — needs search provider
occam_digest(urls, focus_query="…")
```

Or discover then digest:

```
occam_map(url, source="sitemap")
occam_digest(urls from map)
```

### Structured facts

```
occam_playbook_resolve(url)
occam_extract_knowledge(url)   # only if schema exists
```

If resolve shows no schema, use `occam_transcode` for prose instead.

### Verifiable report

```
occam_transcode(url, json_blocks=true)
occam_claim_check(claim, url)   # per critical sentence
occam_verify(receipt, markdown) # offline check
```

Before shipping a multi-source answer:

```
occam_attest(claims=[{claim, sourceUrl}, …])
```

---

## When **not** to call a tool

| Situation | Do this instead |
|-----------|-----------------|
| `ok: false` on transcode | Read `failure.code`; do not invent content |
| `captcha_or_challenge` | Stop; no CAPTCHA solver in this product |
| `http_404` | Fix or drop the URL |
| No `knowledge_schema` | Use `occam_transcode`, not `occam_extract_knowledge` |
| Several known URLs | One `occam_digest`, not N× `occam_transcode` |
| Short but complete page (`quality.verdict=short_quality`) | Treat as success — do **not** heal / escalate |
| `thin_extract` after browser already tried | Stop; page is genuinely chrome/shell/near-empty |

**Thin ≠ short:** `thin_extract` means **bad extraction** (promo chrome, consent shell, headings-only interstitial). A glossary leaf or status page can be small and still `ok: true` with `quality.verdict=short_quality`.

Full failure actions: [Failure codes](failure-codes.md).

---

## Anti-patterns (measured friction)

| Anti-pattern | Prefer |
|--------------|--------|
| Generic host `web_extract` / memory of the page | `occam_transcode(url)` — default page reader |
| Rename mental model to `occam_read` | Keep **`occam_transcode`** (A/B: rename hurt first-tool pick) |
| 8× `occam_transcode` for research | One `occam_digest` |
| `occam_playbook_heal` on every thin or short page | Heal only for real BE after reading `failure` / `agentMeta` |
| Full tool surface on a small local model | `OCCAM_PROFILE=researcher` (or `reader`) |

---

## Exposing tools to an agent (keep the set narrow)

If you drive occam from a small local model — or any agent that drifts into playbook authoring on a simple read — **narrow the surface with `OCCAM_PROFILE`** instead of manually filtering tools in the host config. A large tool set both dilutes tool-selection and invites heal/save loops on `thin_extract`.

| `OCCAM_PROFILE` | Exposes | Hides (unless `full`) |
|-----------------|---------|------------------------|
| `reader` | `occam_client_capabilities`, `occam_transcode`, `occam_probe`, `occam_map`, `occam_digest`, `occam_extract_knowledge`, `occam_search` | playbook authoring, verify/attest/dataset |
| `researcher` (recommended for coding agents) | reader + `occam_claim_check`, `occam_verify` | heal/save/resolve/lint/attest/dataset |
| `auditor` | researcher + `occam_attest`, `occam_dataset_export`, `occam_playbook_lint` | heal/save/resolve |
| `full` (default) | all fifteen | — |

Set in the MCP host env block, e.g. `"OCCAM_PROFILE": "researcher"`. Details: [configuration.md](configuration.md#tool-surface-profile-occam_profile).

**Client budget:** at session start call `occam_client_capabilities` with your context window (or set `OCCAM_CLIENT_CONTEXT_TOKENS`) so later reads without `max_tokens` stay within what you can hold — see [configuration.md](configuration.md#client-context-budget-occam_client_context_tokens).

Smaller models also emit cleaner tool calls with a narrow set: on the full fifteen, an 8B model sometimes wrote the call as prose instead of a structured tool call; on a single tool it was reliable. Larger tool-calling models stay structured across the full set but still benefit from a role-scoped surface.

---

## Parameter defaults worth knowing

| Tool | Default that matters |
|------|----------------------|
| `occam_transcode` | `backend_policy=http_then_browser`; only `url` required |
| `occam_digest` | `fit_markdown=true`; max 8 URLs; pass `urls` as a native string array (legacy strings are deprecated) |
| `occam_map` | `source=homepage`; HTTP-only, up to 64 links |
| `occam_playbook_save` | `verify=true` dry-runs before write |

Full tables: [Tools reference](tools-reference.md).

---

## Worked recipes

Copy-paste flows: [Recipes](recipes.md).
