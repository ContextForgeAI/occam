# Occam recipes

Task flows for MCP tool calling. All examples use JSON argument shapes your host expects.

---

## Read one documentation page

```json
{ "url": "https://developer.mozilla.org/en-US/docs/Web/HTTP" }
```

Optional probe first. Add token savings:

```json
{
  "url": "https://developer.mozilla.org/en-US/docs/Web/HTTP",
  "fit_markdown": true,
  "focus_query": "HTTP request methods"
}
```

**Expect:** `ok: true`, non-empty `markdown`, `receipt.signed` when signing on.

---

## Research a topic (search → digest)

Requires `OCCAM_SEARCH_PROVIDER`.

```
occam_search({ query: "nginx reverse proxy", max_results: 5 })
occam_digest({
  urls: ["https://…", "https://…"],
  focus_query: "reverse proxy configuration",
  fit_markdown: true
})
```

---

## Discover then digest

```
occam_map({ url: "https://nginx.org", source: "sitemap", max_links: 8 })
occam_digest({ urls: map.links.map(link => link.url), focus_query: "install" })
```

If `sitemap_not_found` → retry `source: "homepage"`.

---

## Site playbook (Recipe B)

```
occam_playbook_resolve({ url: "https://nginx.org/en/" })
occam_transcode({ url: "https://nginx.org/en/docs/", playbook_policy: "auto" })
```

---

## Structured facts (Recipe D)

```
occam_playbook_resolve({ url: "https://example.com/product/123" })
occam_extract_knowledge({ url: "https://example.com/product/123" })
```

Only when resolve returns a `knowledge_schema`. Otherwise use transcode.

---

## Verifiable report

```
occam_transcode({ url, json_blocks: true })
occam_claim_check({ claim: "…", url })
occam_verify({ mode: "offline", receipt, markdown })
```

Multi-source before publish:

```
occam_attest({ claims: [{ claim, sourceUrl }, …] })
```

---

## Heal-learn loop (local playbooks)

```
occam_transcode({ url })           → ok:false on hard site
occam_playbook_heal({ url, failure })
# edit draft JSON
occam_playbook_lint({ playbook })
occam_playbook_save({ playbook, verify: true })
occam_transcode({ url, playbook_policy: "auto" })  → verify improvement
```

---

## Honest failure handling

On `ok: false`, read `failure.code` and `agentHints` / `agentMeta.decisions` if present. Never fill gaps from model memory. See [failure-codes.md](failure-codes.md).
