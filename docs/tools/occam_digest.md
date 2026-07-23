# occam_digest

Research several pages at once: digest up to 8 URLs into per-page excerpts plus an optional
combined Markdown block.

## When to use

- Multi-source synthesis — instead of many single `occam_transcode` calls.
- Pass `focus_query` to keep only the parts relevant to your question (recommended).
- Need the raw full text of one page → [`occam_transcode`](occam_transcode.md).
- Don't have URLs yet → [`occam_map`](occam_map.md) or [`occam_search`](occam_search.md) first,
  or let digest discover links itself via `source_url`.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `urls` | array<string> \| string? | null | no* | Preferred: native URL string array. Deprecated compatibility: JSON-encoded string/object entries or newline/comma-separated string. *Required only when `source_url` is omitted |
| `backend_policy` | string | `http_then_browser` | no | Applied to each URL |
| `max_urls` | int | `8` | no | 1–8; extra URLs are dropped |
| `per_url_max_tokens` | int? | null | no | Per-URL token budget (min 128) |
| `focus_query` | string? | null | no | Focus keywords applied to each URL (a per-entry `focus_query` wins). Also ranks `source_url` discovery (entity-first; same ranker as `occam_map`) |
| `fit_markdown` | bool | `true` | no | Paragraph prune per URL (note: default **true** here, unlike transcode) |
| `include_combined` | bool | `true` | no | Include the combined markdown block with `##` titles |
| `session_profile` | string? | null | no | Applied to every URL in the batch |
| `source_url` | string? | null | no | Auto-discover links; when set, **`urls` is ignored**. With `focus_query`: homepage (+ hub expand) ∪ sitemap → shared ranker → `max_links`. Without focus: lighter sitemap → HTML path |
| `max_links` | int | `8` | no | Max links to discover from `source_url` (1–8) |
| `if_none_match` | string? | null | no | SHA-256 of prior combined (bare hex or receipt `sha256:` contentHash); returns `unchanged:true` when matched |

**Input contract:** supply `urls` and/or `source_url` (at least one). MCP `tools/list` publishes
`urls` as a native string-array/string union and does not require it when `source_url` is present.
Prefer native arrays. Legacy strings remain temporarily supported for prerelease compatibility,
including JSON-encoded `{url, focus_query?}` entries, but are deprecated.

Native arrays must contain only URL strings. Empty, mixed-type, nested, malformed, or oversized
transport input returns typed `invalid_arguments` after binding. Normalization accepts at most 256
entries and 65,536 characters; `max_urls` separately limits executed URLs to 8. Both inputs set →
`source_url` wins. Empty discovery → typed `invalid_urls` (no fallback to `urls`).

## Returns

Success envelope:

- `ok: true`, `digestId`, `stats: {requested, succeeded, failed, totalTokensEstimated}`, `timestamp`
- `items[]` — per URL: `{url, ok, title?, excerpt?, backend?, tokensEstimated, failure?,
  focusQuery?, focusMatched?, mediaRefs[], confidence, receipt?}`; a failed item carries
  `failure: {code, message}` while the digest as a whole stays `ok:true`. `focusMatched` is scored
  (phrase / ideal with stem+synonym / partial for ≥3-term queries; 2-term queries require both terms)
- successful item receipts identify their token-count heuristic as `tokenEstimator: "heuristic-unicode-v1"`
- `combined` — the merged markdown (when `include_combined=true`)
- `sourceUrl` + `discoveredLinks[]` — when link discovery ran
- `unchanged` — for conditional digests
- `agentHints` — `{suggestedReadOrder, warnings[], decisions[]?}`. When every ok item has
  `focusMatched: false`, includes `focus_not_found` (warning + decision) and
  `suggestedReadOrder: "items_only"` — do not treat the result as a successful focused digest.

Failure envelope: `ok: false`, `failureCode`, `message`, plus partial `items`/`stats` when some
URLs were attempted. `ok:false` (or a per-item `ok:false`) = that content is unknown.

## Failure codes

Digest-level: `invalid_arguments` (neither input; malformed/empty/mixed/oversized `urls`; bad
policy/budget), `invalid_urls` (`source_url` discovery empty), `digest_failed` (all URLs failed).
Per-item `failure.code` uses the transcode taxonomy (`timeout`, `http_*`, `thin_extract`,
`captcha_or_challenge`, …). See [failure codes](../failure-codes.md).

## Example — `source_url` only

```json
{
  "source_url": "https://nginx.org/en/docs/",
  "max_links": 4,
  "focus_query": "configuration"
}
```

(`urls` may be omitted; MCP schema does not require it when `source_url` is set.)

## Example — `urls` only

Call:

```json
{
  "urls": ["https://a.example/post", "https://b.example/post"],
  "focus_query": "rate limiting",
  "per_url_max_tokens": 800
}
```

Trimmed response:

```json
{
  "ok": true,
  "digestId": "dg_…",
  "items": [
    { "url": "https://a.example/post", "ok": true, "title": "…", "excerpt": "…", "backend": "http", "tokensEstimated": 640 },
    { "url": "https://b.example/post", "ok": false, "failure": { "code": "http_404", "message": "HTTP 404 (http_404)." }, "mediaRefs": [] }
  ],
  "combined": "## A post\n…",
  "stats": { "requested": 2, "succeeded": 1, "failed": 1, "totalTokensEstimated": 640 }
}
```

## Related

- [occam_map](occam_map.md) — find URLs to feed in
- [occam_transcode](occam_transcode.md) — single-page deep read
- [Failure codes](../failure-codes.md)
