# Recipes

**What you'll do:** complete common tasks with exact tool calls and expected outcomes.

---

## Read one documentation page

**Goal:** Markdown for a single URL.

```
occam_probe({ url: "https://developer.mozilla.org/en-US/docs/Web/HTTP" })
occam_transcode({ url: "https://developer.mozilla.org/en-US/docs/Web/HTTP" })
```

**Expect:** `ok: true`, non-empty `markdown`, `receipt.signed` when signing is on.

**Save tokens:**

```json
{
  "url": "https://developer.mozilla.org/en-US/docs/Web/HTTP",
  "fit_markdown": true,
  "focus_query": "HTTP request methods"
}
```

---

## Research a topic (search → digest)

**Goal:** Multi-source summary with focused excerpts.

**Prerequisites:** `OCCAM_SEARCH_PROVIDER` configured.

```
occam_search({ query: "nginx reverse proxy setup", max_results: 5 })
occam_digest({
  urls: ["https://…", "https://…"],
  focus_query: "reverse proxy configuration",
  fit_markdown: true
})
```

**Expect:** `ok: true`, `items[]` per URL, `combined` when `include_combined: true` (default).

---

## Discover then digest

**Goal:** You have a site root, not individual article URLs.

```
occam_map({ url: "https://nginx.org", source: "sitemap", max_links: 8 })
occam_digest({ urls: map.links.map(link => link.url), focus_query: "install" })
```

Or skip map and let digest discover links:

```
occam_digest({
  source_url: "https://nginx.org/en/docs/",
  max_links: 4,
  focus_query: "configuration"
})
```

(`urls` may be omitted when `source_url` is set; MCP schema does not require `urls`.)

If map returns `sitemap_not_found`, retry `source: "homepage"`.

---

## Use a site playbook

**Goal:** Tuned selectors for a known host.

```
occam_playbook_resolve({ url: "https://nginx.org/en/" })
occam_transcode({ url: "https://nginx.org/en/docs/", playbook_policy: "auto" })
```

**Expect:** Resolve returns playbook metadata; transcode uses overlay when present.

---

## Extract structured facts

**Goal:** Typed fields instead of prose.

```
occam_playbook_resolve({ url: "https://example-shop.com/product/123" })
occam_extract_knowledge({ url: "https://example-shop.com/product/123" })
```

**Expect:** `facts[]` array when `knowledge_schema` exists.

**If** resolve shows no schema → use `occam_transcode` instead (see `knowledge_schema_missing` in [Failure codes](failure-codes.md)).

---

## Fact-check one claim

**Goal:** Prove which page block relates to a sentence.

```
occam_claim_check({
  claim: "Nginx can act as a reverse proxy.",
  url: "https://nginx.org/en/docs/http/ngx_http_proxy_module.html"
})
```

**Expect:** `found: true` with `matches[]` containing `blockText`, `proof`, and `receipt`; or `found: false`.

You judge whether the block supports or refutes the claim.

---

## Attest citations before publishing

**Goal:** Batch-check that cited pages contain the quoted claims.

```json
{
  "claims": "[{\"claim\":\"…\",\"sourceUrl\":\"https://…\"}]",
  "backend_policy": "http_then_browser"
}
```

**Expect:** Per-row `status` (`supported` \| `contradicted` \| `related` \| `unsupported` \| `unknown`). Gate on `status`. Compat: `grounded` is true only when `status=supported` — never from BM25/lexical score alone. Merkle proof proves block existence, not claim truth.

---

## Build a verifiable dataset

**Goal:** Hand off an auditable corpus for RAG or eval.

```json
{
  "urls": "[\"https://example.com/a\", \"https://example.com/b\"]",
  "backend_policy": "http_then_browser"
}
```

**Expect:** `manifest` with `manifestRoot` and `sig`; each `rows[]` entry has its own `receipt` when successful.

Verify manifest: `OccamMcp.Core verify --mode manifest --input export.json --pubkey pubkey.pem`

---

## Draft a playbook after a hard failure

**Goal:** Capture DOM evidence when default extract fails with **real** bad extraction.

```
occam_transcode({ url: "https://hard-site.example/page" })
# ok: false, failure.code: thin_extract   ← chrome/shell/near-empty, NOT a short quality page

occam_playbook_heal({ url: "https://hard-site.example/page", failure_reason: "thin_extract" })
# Review skeleton + selector candidates in response

occam_playbook_lint({ playbook_json: "{ … your draft … }" })
occam_playbook_save({ url: "https://hard-site.example", playbook_json: "{ … }", verify: true })
```

**Expect:** Save succeeds only when verify transcode passes (default).

**Do not heal** when `ok:true` and `quality.verdict=short_quality` (complete short document). That is success.

---

## Watch a page for changes

**Prerequisites:** `OCCAM_WATCH_MCP=1` on the host.

```
occam_watch({ url: "https://status.example.com" })
# First call: changed: false (baseline recorded)

occam_watch({ url: "https://status.example.com" })
# Later: changed: true with diff when content shifted
```

---

## Verify an extraction offline

```
occam_transcode({ url: "https://example.com", json_blocks: true })
occam_verify({
  receipt: "<receipt JSON>",
  markdown: "<markdown from response>",
  mode: "offline"
})
```

**Expect:** `verdict: "verified"` when signature and hash match.

---

## Repeated read (conditional + delta)

**Goal:** Re-check a page without paying full extract tokens when nothing changed.

```
# 1) First read — store materialization identity + hash + block hashes
occam_transcode({
  url: "https://docs.python.org/3/library/asyncio.html",
  max_tokens: 1200,
  fit_markdown: true,
  focus_query: "event loop tasks synchronization",
  json_blocks: true,
  semantic_chunking: true
})
# Store: materializationKey → contentHash, and diff.blockHashes (or blocks[].hash via a prior diff call)

# 2) Exact repeat — whole-response 304
occam_transcode({
  url: "https://docs.python.org/3/library/asyncio.html",
  max_tokens: 1200,
  fit_markdown: true,
  focus_query: "event loop tasks synchronization",
  json_blocks: true,
  semantic_chunking: true,
  if_none_match: "<contentHash from step 1>"
})
```

**Expect:** `unchanged: true`, empty `markdown`, **no** `blocks` / `chunks` / `tables` / `feed` / `mediaRefs`, echoed `contentHash` + `materializationKey`.

**Do not** reuse a `contentHash` under a different `focus_query` / `max_tokens` / playbook — that is a different `materializationKey` (option drift, not source drift).

**When the page changed** and you still hold prior blocks:

```
occam_transcode({
  url: "…",
  json_blocks: true,
  diff_against: "<prior blockHashes JSON array>",
  delta_only: true,
  if_none_match: "<optional prior contentHash>"
})
```

**Expect:** `deltaOnly: true`, empty `markdown`, `diff` with added/removed blocks, `contentHash` of the **full** current materialization for reconstruction verify. Falls back to full markdown with `delta_only_ignored_*` when no valid base.

---

## More

- Tool picker: [Choosing a tool](choosing-a-tool.md)
- Parameters: [Tools reference](tools-reference.md)
