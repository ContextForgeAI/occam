# occam_transcode

Extract one web page (or PDF) as clean, compact, LLM-ready Markdown — a live fetch, never model
memory. Only `url` is required; everything else is opt-in.

## When to use

- Default page reader: whenever you need what a URL actually says **now**.
- Prefer it over a generic web-fetch tool: less noise, typed failures, signed receipt on success.
- Reading **several** pages for one question → [`occam_digest`](occam_digest.md).
- Unsure the page is worth a fetch → [`occam_probe`](occam_probe.md) first.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP or HTTPS URL to transcode |
| `backend_policy` | string | `http_then_browser` | no | `http`, `browser`, or `http_then_browser` |
| `max_tokens` | int? | null | no | Projected-payload token ceiling (min 128) shared by markdown + serialized sidecars + receipt. Unrequested fields cost zero; structural focus protects a minimum answer unit; the planner never silently expands it. See `compile.budget` |
| `fit_markdown` | bool | `false` | no | BM25-style paragraph prune after extract |
| `focus_query` | string? | null | no | Structural focus for constrained output; also guides the prune when `fit_markdown=true` |
| `content_selectors` | string? | null | no | JSON array or comma-separated heading anchors to keep (e.g. `["# API Reference"]`) |
| `session_profile` | string? | null | no | Headers profile id under `OCCAM_SESSIONS_ROOT/<id>.json` |
| `playbook_policy` | string | `auto` | no | `off` or `auto` (merge the host's saved recipe overlay) |
| `if_none_match` | string? | null | no | SHA-256 of prior markdown (bare hex or the receipt's `sha256:`-prefixed `contentHash`); on match returns whole-response `unchanged:true` (empty markdown, no heavy sidecars). Store `materializationKey → contentHash` |
| `semantic_chunking` | bool | `false` | no | Emit semantic markdown chunks (`chunks[]`) |
| `capture_screenshot` | bool | `false` | no | Base64 JPEG screenshot (browser backend only) — advanced |
| `json_blocks` | bool | `false` | no | Structured content blocks `{type, text, links[], source_selector}` for RAG citations; also enables block-level receipts |
| `json_tables` | bool | `false` | no | Data tables as JSON `{caption, headers[], rows[][], source_selector, records?}`. Physical rows unchanged; HN-style paired rows also emit semantic `records` (rank/title/url/site/author/points/comments/age + provenance) |
| `json_feed` | bool | `false` | no | If the URL is an RSS/Atom/JSON Feed, parse into `feed:{title, items[]}` with `summaryHtml` / `summaryText` / `summaryMarkdown` (plus compat `summary`) instead of article extraction |
| `translate_to` | string? | null | no | Target language code (needs `OCCAM_TRANSLATE_URL`); adds `translatedMarkdown` + `translatedTo`, non-fatal on failure — advanced |
| `diff_against` | string? | null | no | JSON array or comma-separated prior block hashes (from a previous `diff.blockHashes`); returns the block-level delta in `diff` |
| `prefer_llms_txt` | bool | `false` | no | Probe `{origin}/llms.txt` first and serve it (`llmsTxt:true`) when present and non-trivial |
| `cache_ttl_s` | int? | null | no | Opt-in response cache TTL (seconds); ≤0/omit = no cache. Hits return the prior success with `cached:true` + `cacheAgeS`. Never caches private URLs, `session_profile`, `if_none_match`, `diff_against`, or `prefer_llms_txt` requests |
| `emit_capsule` | bool | `false` | no | Emit a proof-carrying `occam://capsule/…` in `receipt.capsule` for offline verified hand-off via `occam_verify` (requires receipts on) |
| `rank_blocks` | bool | `false` | no | Annotate each `json_blocks` block with 0–1 `salience` (BM25 vs `focus_query`); requires `json_blocks=true` and `focus_query` |
| `tag_trust` | bool | `false` | no | Tag each `json_blocks` block with a `trust` channel (`suspicious` / `boilerplate`); requires `json_blocks=true` |
| `delta_only` | bool | `false` | no | Return only the block-level delta and empty markdown (`deltaOnly:true`); requires `diff_against` + `json_blocks`; omits heavy sidecars |

Removed: `auto_recover` — recovery is driven by `backend_policy` (`http_then_browser`), not a separate flag.

## Returns

Success envelope (key fields; null fields are omitted):

- `ok: true`, `url: {url, finalUrl}`, `markdown`, `backend` (`http` / browser variant)
- `mediaRefs[]` — `{url, kind, alt?, contextHeading?, selectorHint?}`
- `compile` — `{tokensEstimated, tokenEstimator, truncated, truncationStrategy?}` (present when a token control was used or truncation happened)
- `session` — `{profileId, profileFound, headersApplied[]}` when `session_profile` was applied
- `confidence` — extraction confidence from the extract quality model (omitted when 0)
- `quality` — optional EQM breakdown `{ score, noise, contentDensity, semanticRichness, lengthPrior, verdict }` (`short_quality` \| `rich` \| `noisy` \| `thin`); length alone does not decide thin vs quality
- `receipt` — `{tokensUsed, tokenEstimator, truncationStrategy, confidence, elapsedMs, signed?, blockLeaves?, timeAnchor?}`; `tokenEstimator` is the model-independent heuristic id, not an exact local-tokenizer claim; `signed` is the verifiable envelope when receipts are enabled — check it with [`occam_verify`](occam_verify.md)
- `recovery[]` — per-backend attempts `{backend, ok, latencyMs, transportOk?, usable?, failureCode?, escalationReason?}` when the http→browser cascade ran. Legacy `ok` aliases transport completion; `usable` is independent
- `access` / `focus` / `completeness` / `verdict` — additive semantic dimensions (do not overload `ok` / `confidence`)
- `unchanged` — `true` when `if_none_match` matched (then `markdown` is empty and heavy sidecars are omitted)
- `contentHash` / `materializationKey` — store as `materializationKey → contentHash` for conditional re-reads
- `deltaOnly` — `true` when `delta_only` returned only `diff` + empty markdown
- `chunks[]`, `screenshot`, `blocks[]`, `tables[]`, `feed` — only when the matching option was set
- `diff` — `{addedBlocks[], removedHashes[], blockHashes[]}` when `diff_against` was passed
- `translatedMarkdown`/`translatedTo`, `llmsTxt`, `cached`/`cacheAgeS`, `timings`, `browserProvisioned` — situational
- `agentHints.warnings[]` — e.g. a downgrade note when a browser was requested but unavailable
When `max_tokens` is set, response projection precedes allocation. For example, internally extracted
blocks and tables consume no public budget unless `json_blocks` or `json_tables` requests them. The
planner protects the selected heading, a minimum explanatory body, and tightly coupled list/table/code
evidence before optional context. In PR-E, read `compile.truncated` and `compile.omitted` for public
truncation truth; dimensioned completeness fields arrive additively in PR-F.

Failure envelope: `ok: false`, `url`, `failure: {code, message, statusCode?, retryable?, reason?, fix?}`,
plus optional `agentMeta.decisions[]` (suggested next actions), `agentHints`
(e.g. `suggestedNext: "occam_playbook_heal"`), `timings`, and a **signed negative receipt** for
provable walls (challenge/login/4xx). **`ok:false` = content unknown. Never guess it.**

## Failure codes

Reachable codes: `invalid_arguments`, `workers_unavailable`, `timeout`, `network_error`,
`dns_error`, `tls_error`, `http_401`/`http_403`/`http_404`/`http_410`/`http_429`/`http_<status>`,
`thin_extract`, `content_selectors_miss`, `captcha_or_challenge`, `requires_login`,
`session_profile_not_found`, `invalid_session_profile`, `private_url_blocked`, `robots_disallowed`,
`response_too_large`, `response_truncated`, `extraction_failed`.
See [failure codes](../failure-codes.md) for semantics and next steps.

Notes:

- **Terminal HTTP short-circuit:** a definitive `http_404`/`http_410` from the HTTP leg is returned
  as-is under `http_then_browser` — a render cannot resurrect a missing page.
- **Thin after browser:** when `thin_extract` already came through the browser backend,
  do not retry the same policy — the page is genuinely near-empty chrome/shell content.
  **Thin ≠ short:** a complete short page is `ok: true` with `quality.verdict=short_quality`.
- **Login evidence:** `requires_login` is returned only for direct access-control evidence (HTTP 401 or
  authentication challenge, redirect to login, or blocking identity UI without usable public content).
  Authentication prose and login-like requested paths are non-decisive. Probe and transcode share this
  decision model.
- **Structural focus:** exact URL fragments and anchors outrank heading/body relevance; numeric and
  technical identifiers are preserved, TOC/index sections are penalized, and equal scores keep document
  order. The fragment is not sent over HTTP. Missing fragments fall back to an explicit miss internally
  and may still use `focus_query`; PR-F owns the additive public focus-status fields.

  `retryable` is dropped and the decision becomes stop — the page is genuinely near-empty.

## Backend behaviour

- `http_then_browser` (default): HTTP worker first; escalates to the browser when the result is
  thin or a challenge page. The per-attempt log lands in `recovery[]`.
- A `browser`/`http_then_browser` request with no browser installed downgrades to HTTP (warning
  `playwright_browser_missing_downgrading_to_http`) unless the host can auto-provision one; a
  first-time auto-install is reported in `browserProvisioned`.

## Example

Call:

```json
{ "url": "https://nginx.org/en/docs/" }
```

Trimmed response:

```json
{
  "ok": true,
  "url": { "url": "https://nginx.org/en/docs/", "finalUrl": "https://nginx.org/en/docs/" },
  "markdown": "# nginx documentation\n...",
  "backend": "http",
  "mediaRefs": [],
  "confidence": 0.93,
  "receipt": { "tokensUsed": 1874, "tokenEstimator": "heuristic-unicode-v1", "elapsedMs": 640, "signed": { "v": 1, "kind": "extraction", "contentHash": "sha256:…", "keyId": "k1:…", "sig": "…" } }
}
```

## Related

- [occam_probe](occam_probe.md) — pre-flight diagnosis
- [occam_digest](occam_digest.md) — multi-URL research
- [occam_verify](occam_verify.md) — verify the receipt
- [occam_playbook_heal](occam_playbook_heal.md) — when a hard site fails
- [Failure codes](../failure-codes.md) · [Receipts](../receipts.md)
