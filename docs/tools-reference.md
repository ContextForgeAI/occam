# Tools reference

**What you'll do:** look up every MCP tool, parameter, and response shape.

**Fifteen core tools** are always registered. **Opt-in tools** require env flags — see [Opt-in tools](#opt-in-tools).

All tools return a **JSON string** (camelCase). Unless noted, `ok: false` means content is unknown.

---

## 1. `occam_client_capabilities`

Declare the host LLM context window so Occam can size later extracts (ambient `max_tokens` for
`occam_transcode` / `occam_digest` when those calls omit a budget).

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `context_tokens` | int? | null | Context window size; omit to inspect current config |
| `model_id` | string? | null | Optional model id echo |
| `reset` | bool | `false` | Clear session override; re-read env |

### Success response

`ok`, `configured`, `contextTokens`, `outputBudgetTokens`, `suggestedProfile`, `source`, `note`

---

## 2. `occam_transcode`

Read one web page → clean, compact LLM-ready Markdown (live extract, not model memory). Only `url` is required; other parameters are opt-in.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | **required** | HTTP or HTTPS URL |
| `backend_policy` | string | `http_then_browser` | `http`, `browser`, or `http_then_browser` |
| `max_tokens` | int? | null | Projected-payload token ceiling (min 128) across markdown + serialized sidecars. Unrequested fields cost zero; focused output protects a minimum answer unit; never auto-expands |
| `fit_markdown` | bool | `false` | BM25-style paragraph prune |
| `focus_query` | string? | null | Structural focus for constrained output; also guides prune when `fit_markdown=true`. Preserves numeric/technical identifiers |
| `content_selectors` | string? | null | JSON array or comma-separated heading anchors |
| `session_profile` | string? | null | Profile id under `OCCAM_SESSIONS_ROOT` |
| `playbook_policy` | string | `auto` | `off` or `auto` (merge playbook overlay) |
| `if_none_match` | string? | null | Prior content hash (bare hex or `sha256:` receipt form); on match → whole-response `unchanged: true` (no heavy sidecars). Pair with stored `materializationKey` |
| `semantic_chunking` | bool | `false` | Semantic markdown chunking |
| `capture_screenshot` | bool | `false` | JPEG base64 screenshot (browser backend) |
| `json_blocks` | bool | `false` | Structured blocks for RAG citations |
| `json_tables` | bool | `false` | Tables as JSON (`rows` physical + optional semantic `records`) |
| `json_feed` | bool | `false` | Parse RSS/Atom/JSON Feed to JSON (`summaryHtml`/`summaryText`/`summaryMarkdown`) |
| `translate_to` | string? | null | Target language (needs `OCCAM_TRANSLATE_URL`) |
| `diff_against` | string? | null | Prior block hashes (JSON array or comma-separated) for block-level `diff` |
| `prefer_llms_txt` | bool | `false` | Prefer `{origin}/llms.txt` when present |
| `cache_ttl_s` | int? | null | Opt-in cache TTL seconds; never caches private URLs, `session_profile`, `if_none_match`, `diff_against`, `prefer_llms_txt` |
| `emit_capsule` | bool | `false` | Proof-carrying `occam://capsule/…` in `receipt.capsule` |
| `rank_blocks` | bool | `false` | Per-block `salience` (needs `json_blocks` + `focus_query`) |
| `tag_trust` | bool | `false` | Per-block `trust` channel (needs `json_blocks`) |
| `delta_only` | bool | `false` | Return only `diff` + empty markdown (needs `diff_against` + `json_blocks`); omits heavy sidecars |

### Success response (key fields)

`ok`, `url`, `markdown`, `backend`, `confidence`, `receipt`, `compile`, `unchanged`, `contentHash`, `materializationKey`, `blocks`, `tables`, `diff`, `deltaOnly`, `cached`, `agentHints`

`compile.tokensEstimated` is the whole-response estimate when `max_tokens` is set (`compile.budget.total`);
`receipt.tokensUsed` mirrors it. Their `tokenEstimator` field is currently `heuristic-unicode-v1`;
exact counts vary by local model tokenizer. `compile.budget` breaks spend down by markdown / blocks /
tables / chunks / media / feed / receipt.
Projection happens before allocation: sidecars whose matching option is false consume zero budget. A
constrained structural focus retains its minimum heading/body/list/table/code unit before optional context.

### Failure response

`ok: false`, `failure: { code, message, statusCode?, retryable? }`, optional `agentMeta`, `agentHints`

### Example

```json
{ "url": "https://example.com" }
```

---

## 3. `occam_probe`

Cheaply diagnose a URL before a full fetch: page class, risks, extractability (0–1), recommended backend.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | **required** | HTTP or HTTPS URL |
| `timeout_ms` | int | `10000` | Probe timeout |
| `include_social_meta` | bool | `false` | OpenGraph/Twitter meta |
| `session_profile` | string? | null | Session profile id |

### Success response

`ok`, `classification`, `extractability`, `recommendedBackend`, `redirectChain`, `policy`, `agentHints`

### Example

```json
{ "url": "https://example.com" }
```

---

## 4. `occam_digest`

Digest up to 8 URLs into per-page excerpts plus optional combined Markdown.

**Input contract:** supply `urls` and/or `source_url` (at least one). Prefer a native string array.
Legacy string forms remain accepted during RC.2 compatibility but are deprecated. Mixed, nested,
malformed, empty, or oversized `urls` inputs return typed `invalid_arguments`. When `source_url` is
set, **`urls` is ignored**. Empty discovery returns typed `invalid_urls` (no fallback).

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `urls` | array<string> \| string? | null | Preferred native URL string array; deprecated legacy JSON/delimited string (legacy object entries may carry `focus_query`). Optional with `source_url`; normalization cap 256 entries / 65,536 characters |
| `backend_policy` | string | `http_then_browser` | Per-URL backend |
| `max_urls` | int | `8` | Max URLs (1–8) |
| `per_url_max_tokens` | int? | null | Per-URL token budget (min 128) |
| `focus_query` | string? | null | Global focus keywords |
| `fit_markdown` | bool | `true` | Prune per URL (default **true**, unlike transcode) |
| `include_combined` | bool | `true` | Combined markdown with `##` titles |
| `session_profile` | string? | null | Applied to every URL |
| `source_url` | string? | null | Auto-discover URLs from sitemap/links (**ignores `urls`**) |
| `max_links` | int | `8` | Max links from `source_url` (1–8) |
| `if_none_match` | string? | null | Prior combined hash (bare hex or `sha256:`); `unchanged: true` on match |

### Success response

`ok`, `digestId`, `items[]`, `combined`, `stats`, `agentHints`, `sourceUrl?`, `discoveredLinks[]?`, `unchanged?`

(`items` / `combined` — not `pages` / `combinedMarkdown`.)

### Example — `source_url` only

```json
{ "source_url": "https://nginx.org/en/docs/", "max_links": 4, "focus_query": "configuration" }
```

---

## 5. `occam_playbook_resolve`

Read-only playbook lookup: selectors, `knowledge_schema`, `agent_notes`, signature trust.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | **required** | URL or bare hostname |
| `schema_version` | string | `1.0` | Playbook schema version |
| `include_lessons` | bool | `false` | Export local `lessons[]` (max 10) |
| `fetch_site_genome` | bool | `false` | Fetch `/.well-known/agent-genome.v1.json` |

---

## 6. `occam_map`

Discover same-domain links (HTTP-only, up to 64).

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | **required** | Seed URL |
| `source` | string | `homepage` | `homepage`, `sitemap`, or `robots` |
| `max_links` | int | `32` | Max links (1–64) |
| `same_domain` | bool | `true` | Drop off-origin links |
| `filter_nonsense` | bool | `true` | Drop assets/mailto |
| `focus_query` | string? | null | Entity-first re-rank (primary anchors over supporting terms); may expand hubs on homepage |
| `timeout_ms` | int | `15000` | Total map/discovery timeout, including response bodies and sitemap traversal (3k–30k) |
| `session_profile` | string? | null | Session profile |

### Success response

`ok`, `links[]` with `url`, `title`, `path`. Sitemap/robots success adds `partial: true` plus an
`agentHints.warnings` entry when the deadline expires after some links were found; timeout before
the first link returns `ok: false`, `failureCode: "timeout"`.

---

## 7. `occam_playbook_heal`

Capture DOM skeleton and selector candidates to draft a playbook after a failed transcode.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | **required** | URL to heal |
| `failure_reason` | string | **required** | Prior `failure.code` (e.g. `thin_extract`) |
| `session_profile` | string? | null | Session profile |
| `max_skeleton_nodes` | int | `600` | Max nodes (cap 600) |

---

## 8. `occam_playbook_save`

Save a playbook JSON locally. Default `verify=true` dry-runs transcode before write.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | **required** | Host key URL |
| `playbook_json` | string | **required** | Full playbook document |
| `verify` | bool | `true` | Dry-run transcode before write |
| `verify_url` | string? | null | URL for verify (default: `url`) |
| `lesson_note` | string? | null | Optional lesson (1–500 chars) |
| `failure_reason` | string? | null | Echo for lesson entry |
| `host_id` | string? | null | Host id for lesson (no secrets) |

---

## 9. `occam_extract_knowledge`

Extract typed `facts[]` driven by playbook `knowledge_schema`.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `url` | string | **required** | Same URL used with resolve |
| `backend_policy` | string | `http_then_browser` | Extract backend |
| `session_profile` | string? | null | Session profile |

### Success response

`ok`, `facts[]`, `meta` (schema metadata)

---

## 10. `occam_search`

Open-web search → result URLs. Requires `OCCAM_SEARCH_PROVIDER`.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `query` | string | **required** | Search query |
| `max_results` | int | `8` | Results (1–20) |
| `rerank` | bool | `false` | Rerank by extractability (extra probe latency) |

### Success response

`ok`, `results[]` with `title`, `url`, `snippet`; optional `extractability`, `recommendedBackend` when `rerank=true`

---

## 11. `occam_verify`

Verify or cite a receipt without trusting the host.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `receipt` | string | **required** | Receipt object or envelope; history array in history mode |
| `markdown` | string? | null | Markdown for offline hash check |
| `public_key` | string? | null | PEM key; default = host local key |
| `mode` | string | `offline` | `offline`, `live`, `prove`, `citation`, `history` |
| `block_index` | int? | null | Prove mode: block index |
| `block_text` | string? | null | Citation mode: block text |
| `block_selector` | string? | null | Citation mode: CSS selector |
| `proof` | string? | null | Citation mode: proof JSON from prove |
| `chunks` | string? | null | Live mode: JSON array of chunk leaf hashes |

See [Receipts](receipts.md).

---

## 12. `occam_claim_check`

Check whether a page contains evidence for one claim; returns Merkle citation proof.
`found`/`retrieved` mean retrieval relevance only; `verdict` is `not_evaluated` (use `occam_attest` for support/refute).

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `claim` | string | **required** | Sentence to ground |
| `url` | string | **required** | Page URL |
| `backend_policy` | string | `http_then_browser` | Extract backend |
| `session_profile` | string? | null | Session profile |
| `max_matches` | int | `3` | Max blocks (1–10) |

---

## 13. `occam_attest`

Batch-attest claims against cited pages (retrieval → semantic `status` → Merkle existence proof).
Gate on `status`; `grounded` is true only when `status=supported`. See [occam_attest](tools/occam_attest.md).

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `claims` | string | **required** | JSON array of `{claim, sourceUrl}` (1–50) |
| `backend_policy` | string | `http_then_browser` | Per-claim backend |
| `session_profile` | string? | null | Applied to all pages |

---

## 14. `occam_playbook_lint`

Static playbook validation (no network).

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `playbook_json` | string | **required** | Playbook JSON object |

### Response

`grade` (`ready` \| `usable` \| `broken`), `agentReady`, `errors`, `warnings`, `issues[]`

---

## 15. `occam_dataset_export`

Build a signed dataset from 1–20 URLs.

### Parameters

| Parameter | Type | Default | Description |
|-----------|------|---------|-------------|
| `urls` | string | **required** | JSON array of URLs (1–20) |
| `backend_policy` | string | `http_then_browser` | Per-row backend |
| `session_profile` | string? | null | Applied to all URLs |

### Success response

`ok`, `manifest`, `rows[]` with per-row `receipt`

---

## Opt-in tools

Enable via host environment before starting MCP.

### Batch (`OCCAM_BATCH_MCP=1`)

| Tool | Purpose |
|------|---------|
| `occam_batch_submit` | Queue URLs; returns `job_id` |
| `occam_batch_status` | Poll `queued` / `running` / `done` / `failed` |
| `occam_batch_results` | Page results with `next_cursor` |

**Submit parameters:** `urls`, `backend_policy`, `focus_query`, `max_tokens`, `fit_markdown`, `session_profile`, `playbook_policy`, `idempotency_key`, `on_oversize` (`fail` \| `partial`)

### Watch (`OCCAM_WATCH_MCP=1`)

**`occam_watch`** — stateful page-change detection.

Parameters: `url`, `backend_policy`, `focus_query`, `session_profile`, `playbook_policy`, `include_diff`, `reset`, `include_history`

### Cross-check (`OCCAM_CONSENSUS_MCP=1`)

**`occam_crosscheck`** — compare HTTP vs browser (and optional session) vantage points.

Parameters: `url`, `vantages` (default `"http,browser"`), `session_profile`, `focus_query`

Verdicts: `consensus`, `divergent`, `access_divergent`, `inconclusive`

### Failure atlas (`OCCAM_ATLAS_MCP=1`)

**`occam_failure_atlas`** — per-host failure summary for current host process.

Parameter: `only_walled` (default `false`)

---

## Backend policies

| Value | Meaning |
|-------|---------|
| `http` | HTTP worker only (35 s) |
| `browser` | Playwright (60 s default) |
| `http_then_browser` | HTTP then browser fallback |

Alias: `http-then-browser`

---

## Related

- [Choosing a tool](choosing-a-tool.md)
- [Failure codes](failure-codes.md)
- [MCP_API_SPEC.md](../MCP_API_SPEC.md) — full JSON contract
