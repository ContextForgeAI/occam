# MCP API Specification — FF-Occam MCP

**Version:** `1.0.0-rc.2` (fifteen tools + opt-in batch/watch/crosscheck; Agent-First Enhancements AF-1..AF-6; Receipt v1; live-only)
**Transport:** stdio MCP + optional local WebSocket and authenticated WSS (see [docs/transports.md](docs/transports.md))
**Tools:** 15 — `occam_client_capabilities`, `occam_transcode`, `occam_probe`, `occam_digest`, `occam_playbook_resolve`, `occam_map`, `occam_playbook_heal`, `occam_playbook_save`, `occam_extract_knowledge`, **`occam_search`**, **`occam_verify`**, **`occam_claim_check`**, **`occam_attest`**, **`occam_playbook_lint`**, **`occam_dataset_export`** · **opt-in tools:** +3 async batch (`occam_batch_*`) when `OCCAM_BATCH_MCP=1`, `occam_watch` (stateful change-watch) when `OCCAM_WATCH_MCP=1`, `occam_crosscheck` (SI-14 consensus/cloaking cross-check) when `OCCAM_CONSENSUS_MCP=1`, and `occam_failure_atlas` (SI-10 per-host closure map) when `OCCAM_ATLAS_MCP=1` — see [docs/tools-reference.md](docs/tools-reference.md#opt-in-tools). Runtime `tools/list` may be narrower via `OCCAM_PROFILE` (`full` default | `reader` | `researcher` | `auditor`) — see [docs/configuration.md](docs/configuration.md#tool-surface-profile-occam_profile). Client context sizing: `occam_client_capabilities` / `OCCAM_CLIENT_CONTEXT_TOKENS` — see [docs/configuration.md](docs/configuration.md#client-context-budget-occam_client_context_tokens).

**Audience:** agent (contract) · operator (install/env in [docs/configuration.md](docs/configuration.md))

When-to-use guides: [probe](docs/tools-reference.md#3-occam_probe) · [transcode](docs/tools-reference.md#2-occam_transcode) · [digest](docs/tools-reference.md#4-occam_digest) · [map](docs/tools-reference.md#6-occam_map) · [search](docs/tools-reference.md#10-occam_search) · Examples: [docs/recipes.md](docs/recipes.md)

**Quality & freshness signals** — see [choosing-a-tool.md](docs/choosing-a-tool.md) and [concepts.md](docs/concepts.md).

---

## Transport contract

The public TypeScript client offers MCP revision `2025-11-25` and accepts server negotiation to
`2025-11-25`, `2025-06-18`, `2025-03-26`, or `2024-11-05`. An absent or unknown negotiated revision
is a connection error; the client closes the host before any tool call. The selected value is
available as `client.negotiatedProtocolVersion`.

Remote mode is MCP JSON-RPC over Occam's authenticated WSS transport, not Streamable HTTP. It
requires TLS plus a signed, unexpired JWT with exact issuer and audience validation. Signing keys
come from HTTPS OpenID Connect metadata (explicit `--jwt-metadata-uri` /
`OCCAM_JWT_METADATA_URI`, or discovery from an HTTPS issuer). The WebSocket upgrade must use
`Authorization: Bearer`; URI query tokens are rejected. Remote bind may be a numeric public/listen
address, while unauthenticated WebSocket and batch modes remain loopback-only. Concurrent remote
sessions default to four and are bounded by `OCCAM_REMOTE_MAX_SESSIONS` (`1`–`32`).
Local and remote WebSocket messages are text-only and capped at 4 MiB by default through
`OCCAM_MCP_MAX_MESSAGE_BYTES` (`65536`–`16777216`); stdio framing is unaffected.

---

## Goals

1. **Parseable output** — JSON envelope, not raw Markdown strings.
2. **Honest failures** — typed `failure.code`; agents must not hallucinate page content.
3. **Backend transparency** — success includes the winning extractor identifier (for example `node_readability_turndown`, `browser_playwright`, `pdf`, or `managed_<provider>`). This is not the request's abstract `backend_policy`.
4. **Live extract** — every call fetches the page (no file cache in Core).
5. **Token economy (L1a)** — optional `max_tokens`, `fit_markdown`, `focus_query`, `content_selectors`. Defaults preserve L0 full-markdown behavior.
6. **Probe (L1b)** — cheap HTTP diagnosis before transcode; routing hints, no workers.

**Killer features (product surface):** K1 honest compiler · K2 token contract · K3 Recipe A (probe → transcode) · **Recipe B** (multi-URL digest) · Recipe D extract · Recipe E heal-learn.

---

## Response envelope

All tools return a **single string** containing JSON. Property names are **camelCase**. Treat stderr as decoration; treat stdout as contract.

### Agent parsing checklist

```
result = parse_json(tool_output)

if not result.ok:
    handle(result.failure.code or result.failureCode)   # see docs/failure-codes.md
    return

use(result.markdown | result.items | result.links | ...)
log(result.backend)   # transcode only — winning extractor identifier
```

- There is **no** `markdown` field on transcode failure responses.
- Do not strip or regex-hunt Markdown inside the raw tool string without parsing JSON first.

### `occam_transcode` — success (`ok: true`)

```json
{
  "ok": true,
  "url": {
    "url": "https://example.com/page",
    "finalUrl": "https://example.com/page"
  },
  "markdown": "# Title\n\nBody text…",
  "backend": "http",
  "mediaRefs": [],
  "compile": {
    "tokensEstimated": 512,
    "tokenEstimator": "heuristic-unicode-v1",
    "truncated": false,
    "truncationStrategy": null
  }
}
```

| Field | Type | Meaning |
|-------|------|---------|
| `ok` | boolean | Always `true` on success |
| `url.url` | string | URL you requested |
| `url.finalUrl` | string \| null | URL after redirects |
| `markdown` | string | Extracted Markdown (after compile knobs) |
| `backend` | string | Winning extractor identifier, such as `node_readability_turndown`, `browser_playwright`, `pdf`, `css_extract_http`, or `managed_<provider>` (for example `managed_firecrawl`). Do not compare this field to `backend_policy`. |
| `mediaRefs` | array? | Structured media handles (`url`, `kind`, `alt`, `contextHeading`, `selectorHint`) |
| `meta` | object? | Always-on page metadata when found: `{ publishedAt, author, lang, canonical }` (og/meta/JSON-LD; each field omitted when absent). Useful for RAG freshness/citations |
| `compile` | object? | Omitted when no token knobs were set; `tokenEstimator` identifies the heuristic behind `tokensEstimated` |

Types: `OccamTranscodeSuccessResponse` in `OccamTranscodeModels.cs`.

### `occam_transcode` — failure (`ok: false`)

```json
{
  "ok": false,
  "url": { "url": "https://example.com/missing", "finalUrl": "https://example.com/missing" },
  "failure": {
    "code": "http_404",
    "message": "HTTP 404 (http_404).",
    "statusCode": 404
  },
  "agentMeta": {
    "decisions": [
      { "action": "stop", "reason": "Page not found — fix the URL. Do not hallucinate content." }
    ]
  }
}
```

| Field | Type | Meaning |
|-------|------|---------|
| `failure.code` | string | Machine branch key |
| `failure.message` | string | Human-readable detail |
| `failure.statusCode` | number? | HTTP status when `failure.code` is `http_*` |
| `failure.retryable` | boolean? | Transient errors (`timeout`, `network_error`, `dns_error`, some `http_5xx`). `thin_extract` is retryable until the browser backend has been tried — once a browser render is still thin, `retryable` is omitted and the decision becomes `stop` |
| `failure.reason` | string? | Human "why" for a browser-availability failure (`workers_unavailable` from a missing browser) |
| `failure.fix` | object? | Actionable remedy: `{ kind, command, rootRequired }`. `command` is the exact thing to run (e.g. `occam install-browser` for a missing browser binary; `npx playwright install-deps chromium` for missing system libs); `rootRequired` marks the boundary occam can't cross for you (system libs need root) |
| `agentMeta.decisions` | array? | Actionable next steps — same shape as probe `agentHints.decisions` |

Types: `OccamTranscodeFailureResponse`, `OccamTranscodeFailureInfo`, `OccamTranscodeAgentMetaInfo`.

### Other tools

| Tool | Success shape | Failure shape |
|------|---------------|---------------|
| `occam_probe` | `classification`, `recommendation`, `agentHints` | top-level `failureCode` + `message` (not nested `failure`) |
| `occam_digest` | `digestId`, `items[]`, optional `combined`, `stats`, `agentHints` | top-level `failureCode`; per-item `items[].failure` when partial success |
| `occam_map` | `links[]`, `source`, `agentHints` | top-level `failureCode` or nested per tool pattern |
| heal/save/resolve/extract | tool-specific — see sections below | typed `failureCode` or `failure.code` |

---

## `occam_digest`

Linear live digest of up to **8 URLs**. Each URL is transcoded independently (no cache). Each **ok**
item carries its own **signed Receipt v1** envelope under `items[].receipt.signed` (contentHash +
provenance + signature) — so a research digest is independently verifiable per source via `occam_verify`,
the same as a single transcode. No per-item time anchor (a digest would otherwise make N TSA calls) and
no block root (digest does not request `json_blocks`). Omitted under `OCCAM_RECEIPTS=off`.

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `urls` | array<string> \| string? | omit | Preferred: native URL string array. Deprecated compatibility: a JSON-array/object string or delimited URL string. Optional when `source_url` is set |
| `backend_policy` | string | `http_then_browser` | Per-URL policy |
| `max_urls` | int | `8` | Cap **8** |
| `per_url_max_tokens` | int? | omit | Min **128** when set |
| `focus_query` | string? | omit | Recommended for research digests |
| `fit_markdown` | bool | `true` | BM25 prune per URL |
| `include_combined` | bool | `true` | Combined `## Title` markdown |
| `session_profile` | string? | omit | One profile for **all** URLs in the batch |
| `source_url` | string? | omit | AF-5: auto-discover links from sitemap/HTML. When set, **`urls` is ignored** |
| `max_links` | int | `8` | AF-5: cap on discovered links (1–8) |
| `if_none_match` | string? | omit | AF-6: SHA-256 of prior combined (bare hex or `sha256:` receipt form). Returns `unchanged: true` on match |

**Input contract:** provide `urls` and/or `source_url` (at least one). The runtime schema truthfully
publishes `urls` as a string-array/string union; `source_url` alone remains valid. Native arrays may
contain only strings and must not be empty. Mixed, nested, malformed, empty, or oversized transport
inputs return typed `invalid_arguments` after binding, not an opaque MCP error. The normalization
boundary accepts at most 256 entries and 65,536 input characters; `max_urls` still caps execution at 8.
Legacy strings remain accepted during RC.2 prerelease compatibility, including JSON-encoded
`{url, focus_query?}` entries, but are deprecated. When both inputs are set, `source_url` wins and
`urls` is ignored. Empty discovery from `source_url` returns typed `invalid_urls` (no silent fallback).

### Success response

```json
{
  "ok": true,
  "digestId": "a1b2c3d4e5f67890",
  "items": [
    {
      "url": "https://nginx.org/en/docs/",
      "ok": true,
      "title": "nginx documentation",
      "excerpt": "# nginx documentation\n\n...",
      "backend": "http",
      "tokensEstimated": 420,
      "focusMatched": true,
      "confidence": 0.85,
      "receipt": {
        "tokensUsed": 420,
        "tokenEstimator": "heuristic-unicode-v1",
        "truncationStrategy": null,
        "confidence": 0.85,
        "elapsedMs": 320,
        "signed": {
          "v": 1, "kind": "extraction", "url": "https://nginx.org/en/docs/",
          "finalUrl": "https://nginx.org/en/docs/", "backend": "http",
          "ts": "2026-07-04T12:00:00Z", "toolchain": "ff-occam/1.0.0-rc.2",
          "contentHash": "sha256:7c1e…", "tokens": 420, "confidence": 0.85,
          "keyId": "k1:1a2b3c4d5e6f7a8b", "alg": "ecdsa-p256-sha256", "sig": "MEQCIH…"
        }
      },
      "mediaRefs": [
        {
          "url": "https://nginx.org/img/nginx_logo.png",
          "kind": "image",
          "alt": "nginx logo",
          "contextHeading": "## nginx documentation",
          "selectorHint": "img"
        }
      ]
    }
  ],
  "combined": "## nginx documentation\n\n...",
  "stats": {
    "requested": 1,
    "succeeded": 1,
    "failed": 0,
    "totalTokensEstimated": 420
  },
  "agentHints": {
    "suggestedReadOrder": "items_by_focusMatched",
    "warnings": ["check_items_before_combined: 1 URL(s) weak focus match — prefer items[].excerpt over combined."],
    "decisions": [{ "action": "skip_failed", "reason": "Do not invent content for failed digest items." }]
  },
  "timestamp": "2026-06-14T12:00:00.0000000Z",
  "sourceUrl": "https://nginx.org/en/docs/",
  "discoveredLinks": [
    { "url": "https://nginx.org/en/docs/syntax.html" },
    { "url": "https://nginx.org/en/docs/http.html" }
  ],
  "unchanged": null
}
```

### Failure response

Top-level `failureCode` + `message`. Partial per-URL failures still allow `ok: true` when any URL succeeds.

| Code | Meaning |
|------|---------|
| `invalid_arguments` | Neither `urls` nor `source_url`; bad policy or token budget |
| `invalid_urls` | Parse error, empty URL list, or `source_url` discovery yielded no links |
| `workers_unavailable` | Workers missing |
| `digest_failed` | All URLs failed |

Guide: [docs/tools-reference.md](docs/tools-reference.md).

### Agent recipe (Research)

```
1. occam_digest(urls='["https://a.example/doc","https://b.example/guide"]', focus_query="auth flow", per_url_max_tokens=1024)
2. if not ok → branch on failureCode
3. else → read `agentHints` when `focus_query` was set; prefer `items[].excerpt` where `focusMatched` is true; use `combined` only when hints allow. If warnings include `focus_not_found`, treat the digest as unfocused — do not cite `combined` as a focused answer.
```

---

## `occam_map`

Live same-domain link discovery (HTTP only). Returns ranked links — not markdown.

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `url` | string | **required** | HTTP(S) seed URL |
| `source` | string | `homepage` | `homepage` \| `sitemap` \| `robots` |
| `max_links` | int | `32` | Cap **64** |
| `same_domain` | bool | `true` | Drop off-origin links |
| `filter_nonsense` | bool | `true` | Drop assets / webpack / `#` anchors |
| `focus_query` | string? | omit | Re-rank discovered links with entity-first scoring (primary anchors vs supporting terms; shared with digest `source_url`). Homepage may expand hubs when no strong hit (`expanded: true`) |
| `timeout_ms` | int | `15000` | Total map/discovery budget, including response bodies and sitemap traversal; **3000–30000** |
| `session_profile` | string? | omit | Optional session headers for HTTP fetches |

### Success response

```json
{
  "ok": true,
  "url": "https://nginx.org/en/docs/",
  "finalUrl": "https://nginx.org/en/docs/",
  "source": "sitemap",
  "links": [
    { "url": "https://nginx.org/en/docs/syntax.html", "title": null, "path": "/en/docs/syntax.html" }
  ],
  "linkCount": 12,
  "filtered": 4,
  "focusQuery": null,
  "agentHints": {
    "suggestedNext": "occam_digest",
    "maxDigestUrls": 8,
    "warnings": []
  },
  "timestamp": "2026-06-15T12:00:00.0000000Z"
}
```

When sitemap discovery finds links but reaches `timeout_ms` before finishing, success adds
`partial: true` and `agentHints.warnings` says that `links[]` is incomplete. A timeout before any
link is found returns `ok: false` with `failureCode: "timeout"`.

### Failure response

Top-level `failureCode` + `message`. Optional `statusCode` for HTTP errors.

| Code | Meaning |
|------|---------|
| `invalid_url` | Bad URL |
| `invalid_arguments` | Bad `source`, `max_links`, or `timeout_ms` |
| `private_url_blocked` | Localhost / private IP (stub v1) |
| `timeout` | Fetch timed out |
| `sitemap_not_found` | No links from sitemap/robots discovery |
| `extraction_failed` | Homepage fetch failed |
| `thin_extract` | Homepage had no extractable links |
| `unsupported_content_type` | Non-HTML seed (e.g. PDF) |

Guide: [docs/tools-reference.md](docs/tools-reference.md).

### Agent recipe (Map → digest)

```
1. occam_map(url="https://nginx.org/en/docs/", source="sitemap", focus_query="configuration syntax", max_links=32)
2. if not ok → branch on failureCode
3. pick ≤8 links from links[] (read agentHints.maxDigestUrls)
4. occam_digest(urls='[...picked urls...]', backend_policy="http", focus_query="configuration syntax")
```

---

## `occam_playbook_resolve`

Read-only lookup of playbook JSON for a URL or hostname. Resolver order: **local** (`OCCAM_PLAYBOOKS_LOCAL_ROOT` or `~/.occam/playbooks/local/`) → **user** (`WT_PLAYBOOKS_PATH`) → **community** (`profiles/playbooks/community/*.json`) → **bundled seeds** (`profiles/playbooks/seeds/*.seed.json`). Higher tier shadows lower on host match. Optional well-known genome fetch when explicitly requested. No heal/save.

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `url` | string | **required** | Absolute HTTP(S) URL or bare hostname (e.g. `nginx.org`) |
| `schema_version` | string | `"1.0"` | Negotiation warn on unsupported minor; major mismatch → `schemaVersionWarning` |
| `include_lessons` | bool | `false` | Export `lessons[]` from **local tier only** (max 10); redacts token-like `host` fields |
| `fetch_site_genome` | bool | `false` | `GET https://{host}/.well-known/agent-genome.v1.json` (8s, 32 KiB cap) |

Env `OCCAM_SITE_GENOME_FETCH=1` enables well-known fetch when param omitted (desk only; default off in CI).

### Success response

```json
{
  "ok": true,
  "url": "https://kubernetes.io/docs/concepts/overview/",
  "matchedHost": "kubernetes.io",
  "playbookId": "kubernetes.io",
  "schemaVersion": "1.0",
  "provenance": "community",
  "sourcePath": "profiles/playbooks/community/kubernetes.io.json",
  "contentSelectors": ["main", ".td-content", "article"],
  "preferredBackend": "http_then_browser",
  "agentNotes": "Community genome pilot: concepts/tasks/reference page classes.",
  "genome": {
    "site_type": "documentation",
    "page_classes": { "concepts": "/docs/concepts/*" }
  },
  "knowledgeSchema": { "title": { "selector": "h1", "attr": "text" } },
  "pageClass": "concepts",
  "genomeFetch": { "ok": false, "wellKnownUrl": "https://nginx.org/.well-known/agent-genome.v1.json", "failureCode": "http_404" },
  "signature": { "present": true, "status": "verified", "keyId": "k1:…", "score": 85, "passesGate": true },
  "timestamp": "2026-06-16T12:00:00.0000000Z"
}
```

Optional blocks: `genome`, `knowledgeSchema` (matched class fields), `pageClass`, `lessons` (when `include_lessons=true`), `genomeFetch` (when fetch attempted), `schemaVersionWarning`.

**`signature` (SI-08 consumer loop):** classifies the winning recipe against the local signing key — `status` ∈ `verified` | `invalid` (tampered) | `unknown_key` (foreign author) | `unsigned` (no provenance block). A trust signal, not a resolve failure. `score`/`passesGate` echo the recipe's verify-gate claim and are only authoritative when `status = verified`. Playbooks written by `occam_playbook_save` are signed with this key; bundled seeds and site genomes are `unsigned`.

### Failure response

| Code | Meaning |
|------|---------|
| `invalid_arguments` | Empty input or unparseable URL/host |
| `playbook_not_found` | No playbook matches the host (and no site-only genome) |

`provenance` values: `local`, `user`, `community`, `seed`, `site` (well-known only).

Does not ship: publish CLI, signed manifest enforcement at load.

---

## `occam_extract_knowledge`

Recipe D structured facts from playbook `knowledge_schema`. **Requires** resolvable schema (call `occam_playbook_resolve` first on schema hosts).

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `url` | string | **required** | Same URL as resolve |
| `backend_policy` | string | `http_then_browser` | Default from playbook `routing.preferred_backend` when policy is `http_then_browser` |
| `session_profile` | string? | omit | Same as `occam_transcode` |

### Success response

```json
{
  "ok": true,
  "url": "https://kubernetes.io/docs/concepts/overview/",
  "playbookId": "kubernetes.io",
  "pageClass": "concepts",
  "facts": [
    { "name": "title", "value": "Overview", "selector": "h1" }
  ],
  "meta": { "koId": "a1b2c3d4e5f67890" },
  "latencyMs": 842,
  "backend": "css_extract_http",
  "confidence": 0.92,
  "receipt": {
    "confidence": 0.92,
    "elapsedMs": 842
  }
}
```

### Failure codes

| Code | Meaning |
|------|---------|
| `invalid_arguments` | Empty URL or bad `backend_policy` |
| `playbook_not_found` | No playbook for host |
| `knowledge_schema_missing` | Playbook has no `knowledge_schema` block |
| `page_class_unmatched` | No pattern match and no `default` schema |
| `knowledge_schema_empty` | Matched class has zero fields |
| `workers_unavailable` | CSS extract worker missing |
| `timeout` | Worker timed out |
| `extraction_failed` | Structured field extract failed |
| `private_url_blocked` | Private/local URL blocked |
| `session_profile_not_found` | Session profile missing |

Gate: `L4_GENOME_OK` (PB4b rows) · Corpus: `corpora/l4-genome.jsonl`

---

## `occam_playbook_heal`

DOM skeleton capture after a heal-eligible transcode failure. **Host drafts playbook JSON** — Core never LLM-drafts.

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `url` | string | **required** | Absolute HTTP(S) URL |
| `failure_reason` | string | **required** | Prior `failure.code` (e.g. `thin_extract`) |
| `session_profile` | string? | omit | Same as `occam_transcode` |
| `max_skeleton_nodes` | int | `600` | Cap **600** |

### Success response

```json
{
  "ok": true,
  "url": "https://nginx.org/en/docs/",
  "failureReason": "thin_extract",
  "domSkeleton": { "root": { "tag": "body" }, "stats": { "nodeCount": 120, "maxDepth": 8, "interactiveCount": 14 } },
  "anchors": {
    "landmarks": ["main"],
    "dataTestIds": [],
    "mainCandidates": [{ "selector": "#content", "textAnchor": "nginx documentation", "score": 0.85 }]
  },
  "agentHints": {
    "suggestedNext": "occam_playbook_save",
    "doNot": ["dump_raw_html", "retry_transcode_before_save"],
    "maxVerifyRetries": 3
  }
}
```

### Failure codes

| Code | Meaning |
|------|---------|
| `invalid_url` | URL not absolute |
| `invalid_failure_reason` | Empty `failure_reason` |
| `heal_not_applicable` | Failure not in heal_set |
| `captcha_or_challenge` | Terminal — echoed from policy |
| `workers_unavailable` | Browser / skeleton worker missing |
| `timeout` | Skeleton capture exceeded budget |
| `extraction_failed` | Worker could not produce skeleton JSON |

---

## `occam_playbook_save`

Save host-drafted playbook JSON to **local tier only** (`OCCAM_PLAYBOOKS_LOCAL_ROOT`). Default `verify=true` runs live dry-run transcode before write.

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `url` | string | **required** | Host key for playbook `id` |
| `playbook_json` | string | **required** | Full playbook document (`schema_version` 1.x) |
| `verify` | bool | `true` | Dry-run transcode with draft overlay |
| `verify_url` | string? | `url` | May differ for hub vs leaf |
| `lesson_note` | string? | omit | Appended to `lessons[]` on verified save (≤500 chars) |
| `failure_reason` | string? | omit | Echo for lesson entry |
| `host_id` | string? | omit | Host agent id for lesson (never secrets) |

### Success response

```json
{
  "ok": true,
  "playbookId": "nginx.org",
  "writtenPath": "C:\\Users\\me\\.occam\\playbooks\\local\\nginx.org.playbook.json",
  "verify": { "passesGate": true, "score": 78, "noiseLeakage": 0.08 },
  "lessonAppended": true
}
```

### Failure codes

| Code | Meaning |
|------|---------|
| `playbook_schema_invalid` | Malformed JSON or missing `schema_version` / `id` / `hosts` |
| `playbook_save_rejected` | Forbidden secret keys or write outside local tier |
| `playbook_verify_failed` | Dry-run still `thin_extract` / selectors miss / below quality floor |
| `playbook_verify_low_score` | Verify score &lt; 70 |
| `playbook_verify_high_noise` | Verify noise &gt; 0.12 |

Gate: `L3_HEAL_LEARN_OK` · Corpus: `corpora/l3-heal-learn.jsonl`

### Transcode heal hints

On heal-eligible `occam_transcode` failures, response may include:

```json
"agentHints": { "suggestedNext": "occam_playbook_heal", "doNot": ["max_heal_per_url_per_turn=1"] }
```

Core **never** auto-calls heal.

---

## `occam_probe`

Cheap diagnosis without full transcode. HTTP fetch only.

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `url` | string | **required** | HTTP/HTTPS |
| `timeout_ms` | int | `10000` | 1000–120000 ms |
| `include_social_meta` | bool | `false` | OpenGraph/Twitter `socialMeta` block |
| `session_profile` | string? | omit | Optional session headers for probe fetch |

### Success response

```json
{
  "ok": true,
  "url": { "requested": "https://nginx.org/en/docs/", "final": "https://nginx.org/en/docs/" },
  "classification": {
    "pageClass": "documentation",
    "requiresJavascript": false,
    "likelyCookieConsent": false,
    "likelyChallenge": false,
    "likelyLoginRequired": false,
    "likelyPaywall": false,
    "riskFlags": [],
    "domainTier": "tier_a_docs",
    "httpOnlyRoute": true
  },
  "recommendation": { "backend": "http", "estimatedLatencyMs": 800, "extractability": 0.9 },
  "policy": { "privacyMode": "local_public" },
  "statusCode": 200,
  "probeLatencyMs": 420,
  "agentHints": { "suggestedNextTool": "occam_transcode", "warnings": [] },
  "timestamp": "2026-06-14T12:00:00.0000000Z"
}
```

`recommendation.backend`: `http` | `http_then_browser` | `browser` | `none`.

`recommendation.extractability` (0–1): cheap readability estimate (same scorer `occam_search` uses for rerank) — dead/blocked/paywall/anti-bot/JS-stub score low, clean docs/articles high. Lets an agent skip low-yield transcodes.

### Failure response

`ok: false`, `failureCode` (e.g. `http_404`), `message`, `statusCode`, `policy`, `probeLatencyMs`, optional `redirectChain`.

Guide: [docs/tools-reference.md](docs/tools-reference.md).

### Agent recipe (K3)

```
1. occam_probe(url)
2. if not ok → branch on failureCode
3. if ok → occam_transcode(url, backend_policy=recommendation.backend)
```

---

## `occam_transcode`

Converts one HTTP(S) URL to Markdown. **Always live extract.**

### Parameters

**Only `url` is required** — everything else is an off-by-default opt-in. Each parameter's live `[Description]` carries a `[tag]` (`[core]`/`[tokens]`/`[structured]`/`[fetch]`/`[watch]`/`[advanced]`); see [Tools reference — occam_transcode](docs/tools-reference.md#2-occam_transcode) for the grouped view.

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `url` | string | **required** | HTTP/HTTPS |
| `backend_policy` | string | `http_then_browser` | `http` \| `browser` \| `http_then_browser` (also `http-then-browser`) |
| `max_tokens` | int? | omit | **Whole-response projected-payload** token budget (min **128**) shared across markdown and sidecars eligible for serialization (`blocks` / `tables` / `chunks` / `mediaRefs` / `feed` / receipt). Unrequested fields cost zero. Markdown keeps a ≥50% floor of the pool; leftovers fill requested structured fields greedily. The planner receives only the surface share, protects a minimum answer unit, and never auto-expands the budget. `compile.budget` reports the allocation. Omit = no cap (L0 behavior). |
| `fit_markdown` | bool | `false` | BM25 paragraph prune; strips MDN/Carbon boilerplate; with `focus_query`, filters list/TOC bullets by anchor text |
| `focus_query` | string? | omit | Structural focus intent when `max_tokens` constrains the surface; also guides `fit_markdown=true`. Technical/numeric identifiers are retained |
| `content_selectors` | string? | omit | JSON array or comma-separated heading anchors (e.g. `["# API Reference"]`) |
| `session_profile` | string? | omit | Loads headers from `{OCCAM_SESSIONS_ROOT}/<id>.json` |
| `playbook_policy` | string | `auto` | `off` \| `auto` (default) — when `auto`, internal resolve + winning-tier `extract`/`postMarkdown`/`preferred_backend` overlay |
| `if_none_match` | string? | omit | AF-6: SHA256 of prior markdown (bare hex or `sha256:`-prefixed receipt `contentHash`). On match returns **`unchanged: true` as a whole-response minimal envelope**: empty `markdown`, no `blocks`/`chunks`/`tables`/`feed`/`mediaRefs`/`screenshot`/translation sidecars. Echoes `contentHash` + `materializationKey`. Store **`materializationKey → contentHash`**, not merely `URL → contentHash` |
| `semantic_chunking` | bool | `false` | Splits the extracted markdown into header-scoped `chunks[]` (worker `semantic_chunking` plugin). Omit = no chunking |
| `capture_screenshot` | bool | `false` | Browser backend only: returns a base64 JPEG in `screenshot`. Ignored on the HTTP backend |
| `json_blocks` | bool | `false` | Emits DOM-derived `blocks[]` for RAG citations alongside markdown (both backends). Each block is `{ type, text, links[], source_selector }`; `source_selector` is a real CSS path — document-absolute and round-trip-verified when the content root is connected to the page DOM, otherwise anchored to the extracted fragment |
| `json_tables` | bool | `false` | Emits data tables as `tables[]` alongside markdown (both backends). Each table is `{ caption, headers[], rows[][], source_selector, records? }`. Physical `rows` stay one-per-`<tr>` (markdown/GFM unchanged). When the table uses paired rows (Hacker News `tr.athing` + subtext), `records[]` reconstructs semantic objects `{ rank, title, url, site, author, points, comments, age, schema, provenance }` so one story is one knowledge object with row provenance. Layout tables (those containing a nested table) and single-column header-less tables are skipped. `source_selector` follows the same semantics as `json_blocks` |
| `json_feed` | bool | `false` | When the URL resolves to an RSS 2.0 / Atom / RSS 1.0 / JSON Feed, parse it into `feed: { title, items[] }` and skip article extraction (HTTP backend). Opt-in — non-feed responses are unaffected and extraction proceeds normally |
| `translate_to` | string? | omit | Target language code (e.g. `ru`, `pt-BR`). Requires `OCCAM_TRANSLATE_URL` (LibreTranslate). Adds `translatedMarkdown` + `translatedTo`. Non-fatal: on failure/unconfigured the original markdown is returned with a warning |
| `diff_against` | string? | omit | diff-codec: JSON array (or comma-separated) of prior `diff.blockHashes`. Returns a `diff` block-level delta. Never cached. Pair with `if_none_match` as the cheap boolean gate |
| `delta_only` | bool | `false` | **delta-as-primary.** When you already hold the prior extract, return only the `diff` and an **empty** `markdown` (`deltaOnly:true`) — a re-read costs delta-size tokens, not full-page tokens. Reconstruct `current = prior blocks, drop removedHashes, apply addedBlocks in blockHashes order`, then verify against the returned `contentHash` (hash of the full current markdown). Requires `diff_against` + `json_blocks`; otherwise the full markdown is returned with a `delta_only_ignored_*` warning. On success, heavy sidecars (`blocks`/`chunks`/…) are omitted — the delta is the transport |
| `prefer_llms_txt` | bool | `false` | Probe `{origin}/llms.txt` (sanctioned LLM-friendly markdown) via the HTTP backend first; return it with `llmsTxt:true` (and `finalUrl` = the llms.txt URL) when present and non-empty, else fall back to normal extraction. Opt-in; never cached |
| `cache_ttl_s` | int? | omit | **Opt-in** response cache TTL in seconds. Omit or `<=0` = no cache (default; behavior unchanged). On a hit within TTL the prior success envelope is returned with `cached: true` + `cacheAgeS`. **Never** caches private/RFC1918/localhost URLs, `session_profile` requests, or `if_none_match` calls. Stored under `OCCAM_CACHE_DIR`. |
| `emit_capsule` | bool | `false` | **Opt-in.** Adds `receipt.capsule` — a proof-carrying `occam://capsule/…` string bundling the signed receipt + this markdown, so another agent verifies it offline via `occam_verify` with **no re-fetch** (verified hand-off). Repeats the markdown → costs tokens. Requires receipts on (`OCCAM_RECEIPTS`). |
| `rank_blocks` | bool | `false` | **Opt-in.** Annotates each `blocks[]` entry with a `salience` (0–1) = its BM25 relevance to `focus_query`, normalized to the top block — an explicit per-span attention signal so a consuming LLM weights/cites the right spans without re-reading everything. Requires `json_blocks=true` + `focus_query`; no `fit_markdown` needed. |
| `tag_trust` | bool | `false` | **Opt-in.** Tags each `blocks[]` entry with a `trust` channel: `suspicious` (the text reads like an instruction to the reader/model — a prompt-injection shape) or `boilerplate` (a non-content region: nav/footer/aside/comment). Normal content is untagged. A machine-checkable signal so a harness can hard-isolate untrusted spans instead of trusting all extracted text equally. Heuristic — a signal, not a guarantee. Requires `json_blocks=true`. |

### Success response

```json
{
  "ok": true,
  "url": { "url": "https://example.com/", "finalUrl": "https://example.com/" },
  "markdown": "# Title\n\n...",
  "backend": "http",
  "compile": {
    "tokensEstimated": 842,
    "tokenEstimator": "heuristic-unicode-v1",
    "truncated": false,
    "truncationStrategy": null
  },
  "confidence": 0.85,
  "receipt": {
    "tokensUsed": 842,
    "tokenEstimator": "heuristic-unicode-v1",
    "truncationStrategy": null,
    "confidence": 0.85,
    "elapsedMs": 1240,
    "signed": {
      "v": 1, "kind": "extraction",
      "url": "https://example.com/docs", "finalUrl": "https://example.com/docs",
      "backend": "http", "ts": "2026-07-04T12:00:00Z", "toolchain": "ff-occam/1.0.0-rc.2",
      "contentHash": "sha256:9f2b…", "tokens": 842, "confidence": 0.85,
      "keyId": "k1:1a2b3c4d5e6f7a8b", "alg": "ecdsa-p256-sha256", "sig": "MEUCIQ…"
    }
  },
  "timings": {
    "totalMs": 1240,
    "preflightMs": 3,
    "routeMs": 1221,
    "networkMs": 870,
    "parseMs": 340,
    "postProcessMs": 11,
    "compileMs": 4
  },
  "session": {
    "profileId": "work",
    "profileFound": true,
    "headersApplied": ["Cookie"]
  },
  "mediaRefs": [
    {
      "url": "https://example.com/assets/diagram.png",
      "kind": "image",
      "alt": "Architecture diagram",
      "contextHeading": "## Overview",
      "selectorHint": "img"
    }
  ],
  "recovery": [
    { "backend": "http", "ok": true, "transportOk": true, "usable": false, "failureCode": "thin_extract", "latencyMs": 1240 },
    { "backend": "browser", "ok": true, "transportOk": true, "usable": true, "escalationReason": "thin_extract", "latencyMs": 2400 }
  ],
  "access": { "disposition": "open", "confidence": 0.9, "evidenceCodes": ["usable_public_content"], "recommendedAction": "continue" },
  "focus": { "status": "not_requested" },
  "completeness": { "status": "complete" },
  "verdict": "not_evaluated",
  "unchanged": null
}
```

| Field | Type | Meaning |
|-------|------|---------|
| `confidence` | number | AF-1: 0.0–1.0 extract fitness (ADR-0004 EQM `quality.score`, plus a small backend/truncation adjustment). On `ok:false` → `0`. **Not** focus correctness or completeness |
| `access` | object? | PR-F shared access dimension: `{ disposition: open\|restricted\|unknown, confidence, evidenceCodes[], recommendedAction }` |
| `focus` | object? | PR-F focus dimension: `{ status: hit\|weak\|miss\|not_requested, confidence?, matchedAnchor? }` |
| `completeness` | object? | PR-F answer completeness: `{ status: complete\|partial\|incomplete, incompleteReason?, suggestedMinTokens? }` |
| `verdict` | string? | Semantic judgment when computed; retrieval/transcode paths emit `not_evaluated` |
| `quality` | object? | ADR-0004 extract quality breakdown on success: `{ score, noise, contentDensity, semanticRichness, lengthPrior, verdict }` where `verdict` is `short_quality` \| `rich` \| `noisy` \| `thin`. Length alone never decides thin vs quality. Omitted on `unchanged` / `delta_only` heavy-omit paths |
| `receipt` | object? | AF-3 telemetry `{ tokensUsed, tokenEstimator, truncationStrategy, confidence, elapsedMs }` **plus Receipt v1 verifiable fields on success**: `signed` (the signed extraction envelope — `contentHash`, `blockMerkleRoot`, provenance, ECDsa P-256 signature), `blockLeaves` (unsigned sidecar that reconstructs the root; present with `json_blocks`), and `timeAnchor` (optional RFC3161 when `OCCAM_TIME_ANCHOR=1`). `tokenEstimator` names the model-independent heuristic; it is not a claim of exact tokenizer parity. Verify with `occam_verify` or the offline CLI (see [receipt_verification.md](docs/receipt_verification.md)). `OCCAM_RECEIPTS=off` → telemetry only, no `signed`. Blocks are reconciled to the returned (post-prune) markdown, so `contentHash`/`blockMerkleRoot`/`blocks` are mutually consistent |
| `timings` | object? | Per-stage wall-clock breakdown (ms): `{ totalMs, preflightMs, routeMs, networkMs, parseMs, postProcessMs, compileMs }`. `networkMs` is the with-internet leg (DNS+connect+TLS+download), `parseMs` is the without-internet CPU leg (DOM+Readability+Turndown); `routeMs − networkMs − parseMs` ≈ worker spawn/IPC dispatch overhead. Present on http-backed transcodes (success and most failures) |
| `recovery` | array? | AF-4 / PR-F: `[{ backend, ok, latencyMs, transportOk?, usable?, failureCode?, escalationReason? }]`. Legacy `ok` aliases `transportOk` (raw transport/extract completion). `usable` is independent router quality. Present on the `http_then_browser` recovery path |
| `unchanged` | boolean? | AF-6: `true` when `if_none_match` matched — **whole-response** conditional: empty `markdown` and omitted heavy sidecars (`blocks`, `chunks`, `tables`, `feed`, `mediaRefs`, `screenshot`, translation). Not a Markdown-only 304 |
| `deltaOnly` | boolean? | `true` when `delta_only` suppressed the full body: the empty `markdown` is intentional — reconstruct current content from `diff` + your prior blocks and verify against `contentHash`. Heavy sidecars omitted. Omitted otherwise |
| `contentHash` | string? | Always-on bare-hex SHA-256 of the materialized markdown (no receipts required). Two uses: (1) store it and pass as `if_none_match` next time for a 304-style skip; (2) it is the **KV-cache prefix key** — an identical `contentHash` means byte-identical markdown, so a harness can reuse cached prompt tokens instead of re-encoding. Same digest as `receipt.signed.contentHash` without the `sha256:` prefix (either form is accepted by `if_none_match`). On `unchanged: true` the body is empty but this **echoes** the matching hash. Under `deltaOnly:true` the body is empty but this hashes the **full** current markdown, so you can verify a delta reconstruction |
| `materializationKey` | string? | Deterministic `sha256:…` identity of the representation being hashed (canonical URL + backend policy + playbook id/version + `max_tokens` / `fit_markdown` / `focus_query` / selectors / structured flags / translation / rank+trust options / schema version). Clients must store **`materializationKey → contentHash`**. A focus or budget change yields a new key — that is materialization drift, not source-page drift |
| `chunks` | array? | Present when `semantic_chunking=true`: `[{ text, headers: [breadcrumb...] }]` — header-scoped markdown segments |
| `screenshot` | string? | Present when `capture_screenshot=true` and the browser backend ran: base64-encoded JPEG of the rendered page |
| `blocks` | array? | Present when `json_blocks=true`: `[{ type, text, links: [{ text, href }], source_selector, level?, salience? }]` — ordered DOM content blocks (`heading`/`paragraph`/`list_item`/`code`/`quote`/`table`/`figure`) with a CSS `source_selector` for provenance/citations. `level` (1–6) is present on `heading` blocks only (the `h1`…`h6` depth, so the heading hierarchy is recoverable). `salience` (0–1) is added per block when `rank_blocks=true` (relevance to `focus_query`); `trust` (`suspicious`/`boilerplate`) is added per block when `tag_trust=true` |
| `tables` | array? | Present when `json_tables=true`: `[{ caption, headers: [string], rows: [[string]], source_selector, records? }]`. Physical `rows` are row-major cell strings (one array per `<tr>`). Optional `records` are semantic reconstructions (e.g. HN title+subtext → `{ rank, title, url, site, author, points, comments, age, schema:"hn_item", provenance:{ source_selector, row_indexes, table_selector } }`). Layout/nested tables are skipped |
| `feed` | object? | Present when `json_feed=true` and the URL is a feed: `{ title, items: [{ title, link, publishedAt, summary, summaryHtml, summaryText, summaryMarkdown }] }` — RSS 2.0 / Atom / RSS 1.0 / JSON Feed. `summaryText` is plain (no tags); `summaryMarkdown` is clean markdown; `summaryHtml` is source HTML when present; `summary` is a compat alias of `summaryText` |
| `diff` | object? | Present when `diff_against` set: `{ addedBlocks: [{ hash, type, text, source_selector }], removedHashes: [...], blockHashes: [...] }` — block-level delta vs the supplied prior hashes. Store `blockHashes` for the next call |
| `llmsTxt` | boolean? | `true` when `prefer_llms_txt=true` and the site's `/llms.txt` was served instead of normal extraction (`finalUrl` is the llms.txt URL) |
| `translatedMarkdown` | string? | Present when `translate_to` is set and translation succeeded: the markdown translated into `translatedTo`. **Machine translation — lossy for humor, idioms, wordplay, sarcasm and tone**; a `translation_machine_generated` warning is added to `agentHints.warnings`. Original `markdown` is preserved alongside — verify nuance against it |
| `translatedTo` | string? | The language code the markdown was translated into (echoes `translate_to`). Present only with `translatedMarkdown` |
| `cached` | boolean? | Present (`true`) only when the response was served from the opt-in cache (`cache_ttl_s`). Absent on live extracts. Real prior content — not a trust-model violation |
| `cacheAgeS` | number? | Age of the cached entry in seconds; present only alongside `cached: true` |

`mediaRefs` omitted when no media/download links found in main content (max **32** per page). Host may fetch URLs separately; Occam does not return binary bodies.

`session` is omitted when `session_profile` was not passed. **Never** includes header values — names only.

`compile.truncationStrategy`: `null` when within budget; `head_safe` for boundary-safe head cut; `sandwich` when truncated with `focus_query` (marker `…` between head and tail); `focus_window` when focus-centered truncation applied; `response_budget` when only structured sidecars were trimmed under the shared budget.

`compile.budget` (when `max_tokens` was set): factual per-bucket spend `{ total, markdown, blocks, tables, chunks, media, feed, receipt }` for the projected payload. Projection precedes allocation: `json_blocks=false` and `json_tables=false` mean those hidden IR buckets cost zero; the same rule applies to other opt-in sidecars. `tokensEstimated` equals `budget.total`, not markdown alone. Conditional (`unchanged`) and `delta_only` modes allocate **no markdown floor** — the planner reserves receipt first, then delta/metadata.

With constrained structural focus, the planner protects the smallest answer-bearing heading/body unit and tightly coupled list/table/code evidence before optional context. `compile.truncated` and `compile.omitted` remain the public RC.1-compatible truncation signals in PR-E; dimensioned completeness is added by PR-F.

**Structured blocks after focus prune:** `blocks[]` are reconciled to the returned markdown (SI-02). Among survivors, focus-relevant blocks are ordered ahead of boilerplate for the shared budget; filler-only leftovers are dropped rather than shipped as the sole structured evidence. Receipt leaves always hash the returned blocks — never a pre-prune full page.

`compile.omitted` (present only when `truncated` is `true`): a structured manifest of what token budgeting dropped, so a consumer never mistakes a truncated body for the whole page. Fields: `reason` (the `truncationStrategy` that produced the holes), `tokensDropped` (estimated tokens removed), `regions` (array of `tail` / `middle` / `unchosen` / `structured`), `sections` (count of top-level markdown sections that vanished from the returned body; omitted when none), and optional `structured` counts (`blocks`, `tables`, `chunks`, `media`, `feedItems`, `screenshot`) when sidecar trim ran. This promotes the in-band `<!-- SNIP … -->` markers to a first-class field.

`compile.tokenEstimator` is currently `heuristic-unicode-v1`: an AOT-safe, script-aware,
model-independent estimate. Use the id as provenance; exact counts vary with the local model's tokenizer.

Per-item digest field `focusMatched` (boolean, omitted when no `focus_query`): scored digest focus evaluation (`FocusMatcher.EvaluateForDigest`). **Matched** when (1) the full query phrase appears, (2) **ideal** — every query term hits via exact / soft-stem / synonym, or (3) **partial** — for queries with ≥3 terms, at least `max(2, ceil(2n/3))` terms hit. Two-term queries stay strict (both terms required) so a hub/TOC that only shares one word stays `false`. Soft-stem equates `configuration`≈`configure`; a small closed synonym map covers auth/config/query/syntax/… .

Success responses include `agentHints` (`suggestedReadOrder`, `warnings`, optional `decisions`) when digest completes — read before citing `combined`.

**Focus discovery ranking** (`occam_map` and digest `source_url` with `focus_query`): the query is decomposed into **primary anchors** (rare identifiers, `CamelCase` / `snake_case`, path-like tokens, quoted phrases) and **supporting** topical terms (generic ops/concepts). Ranking prefers exact path-segment / title matches on primaries over supporting overlap and BM25. Candidates that miss all primary anchors receive a strong penalty; documentation version roots (`/3.12/`, `/docs/3.10/`, …) are penalized when the focus names a concrete entity. Pipeline order is discover → normalize/dedupe → hub expand when needed → score → penalties → **then** `max_links` cap. Map and digest share `MapLinkRanker`; focused digest `source_url` also runs homepage hub expansion and merges sitemap candidates before the final rank.

**Transcode structural focus:** when a constrained surface needs selection, Core indexes heading hierarchy,
section spans, explicit/synthetic anchors, and index-like sections. Ranking priority is exact URL fragment,
exact anchor/normalized identity, heading coverage, nearby body evidence, then deterministic document order;
TOC/index entries are penalized. The fragment is stripped from the fetched URL and retained only as local
intent. A missing fragment does not invent a target; the regular focus query may still rank a section.

**Unfocused digest honesty:** when `focus_query` was set and every ok item has `focusMatched: false`, `agentHints` includes warning `focus_not_found: …`, decision `action: "focus_not_found"`, and `suggestedReadOrder: "items_only"`. The digest stays `ok: true` (excerpts are real) but must not be treated as a successful focused answer.

`compile` appears when token knobs were used or output was truncated.

### Failure response

```json
{
  "ok": false,
  "url": { "url": "https://example.com/missing", "finalUrl": "https://example.com/missing" },
  "failure": {
    "code": "http_404",
    "message": "HTTP 404 (http_404).",
    "statusCode": 404
  },
  "agentMeta": {
    "decisions": [
      { "action": "stop", "reason": "Page not found — fix the URL or remove it from the corpus. Do not hallucinate content." }
    ]
  },
  "receipt": {
    "signed": {
      "v": 1, "kind": "negative",
      "url": "https://example.com/missing", "finalUrl": "https://example.com/missing",
      "backend": "http", "ts": "2026-07-04T12:00:00Z", "toolchain": "ff-occam/1.0.0-rc.2",
      "failureCode": "http_404", "statusCode": 404,
      "keyId": "k1:1a2b3c4d5e6f7a8b", "alg": "ecdsa-p256-sha256", "sig": "MEUCIA…"
    }
  }
}
```

On **provable-unavailability** failures (`captcha_or_challenge`, `requires_login`, paywall, HTTP
401/403/404/410), the failure response also carries a **signed negative `receipt`** — a claim only an
honest tool can make (SI-03). Transient failures (`timeout`, `network_error`, `workers_unavailable`,
`thin_extract`) and pre-pipeline argument errors are **not** signed. Verify a negative receipt the same
way as a positive one (`occam_verify offline`).

### Failure codes (implemented)

| Code | Meaning |
|------|---------|
| `invalid_arguments` | Bad `backend_policy`, token parameters, or `content_selectors` |
| `workers_unavailable` | Workers missing or `OCCAM_HOME` wrong |
| `http_401`, `http_403`, `http_404`, `http_410`, `http_429`, `http_5xx`, … | Non-2xx HTTP from worker |
| `timeout` | Host worker timeout (35s HTTP / 120s browser) |
| `network_error` | Connection reset/refused/timeout (retryable) |
| `dns_error` | Host did not resolve — `ENOTFOUND`/`EAI_AGAIN` (retryable) |
| `tls_error` | Certificate invalid/expired/untrusted (not retryable) |
| `extraction_failed` | Worker error or empty markdown (non-HTTP) |
| `thin_extract` | Quality gate — content too thin (or empty after compile) |
| `response_too_large` | HTTP body exceeded `OCCAM_MAX_RESPONSE_BYTES` during download |
| `response_truncated` | Partial extract after oversize (`on_oversize: partial` or `OCCAM_HTTP_OVERSIZE_MODE=partial`) — `ok:false` with partial `markdown` |
| `content_selectors_miss` | No selector matched any section |
| `captcha_or_challenge` | Challenge / anti-bot page |
| `requires_login` | Direct access-control evidence (401/authentication challenge, redirect to login, or blocking identity UI); no `session_profile` |
| `session_profile_not_found` | Profile id set but file missing/unreadable |
| `invalid_session_profile` | Bad profile id |
| `private_url_blocked` | Private/local URL blocked |
| `transcode_failed` | Generic pipeline failure |

Full agent handling: [docs/failure-codes.md](docs/failure-codes.md).

### Agent recipe

```
1. occam_probe(url)                                    # optional (K3)
2. occam_transcode(url, backend_policy=http_then_browser)
3. result = JSON.parse(tool_return)
4. if not result.ok → branch on result.failure.code
5. else → use result.markdown
```

Token economy (K2):

```
occam_transcode(url, fit_markdown=true, focus_query="authentication", max_tokens=2048)
```

---

## `occam_search`

Open-web search (query → result URLs) — the agent's **discovery** step before `probe`/`transcode`/`digest`. Occam does not crawl or index; it delegates to a configured backend (SearXNG / Brave / Tavily) and normalizes results. **Opt-in** — off unless `OCCAM_SEARCH_PROVIDER` is set (returns `search_unconfigured` otherwise). Source: `Tools/OccamSearchTool.cs`.

### Parameters

| Name | Type | Default | Notes |
|------|------|---------|-------|
| `query` | string | **required** | Search query (must not be empty) |
| `max_results` | int | `8` | Max results, range **1–20** |
| `rerank` | bool | `false` | Rerank by extractability — cheaply probes each hit and reorders so clean HTTP-extractable pages rank above paywalls/anti-bot/JS-stubs/dead links. Adds `extractability` (0–1) + `recommendedBackend` per result. Opt-in (extra probe latency) |

Config (env): `OCCAM_SEARCH_PROVIDER` (`searxng` \| `brave` \| `tavily`), `OCCAM_SEARCH_URL` (SearXNG instance), `OCCAM_SEARCH_API_KEY` (Brave/Tavily), `OCCAM_SEARCH_TIMEOUT_MS` (default 20000) — see [Environment](#environment).

### Success response

```json
{
  "ok": true,
  "query": "occam mcp",
  "provider": "searxng",
  "count": 2,
  "results": [
    { "title": "…", "url": "https://…", "snippet": "…" }
  ],
  "agentHints": { "suggestedNext": "occam_transcode (fetch a result URL) or occam_digest (compare several)" }
}
```

With `rerank=true`, `results` are ordered by `extractability` (desc) and each also carries `extractability` (0–1) + `recommendedBackend` (e.g. `"extractability": 0.9, "recommendedBackend": "http"`).

### Failure codes

| Code | When |
|------|------|
| `invalid_arguments` | Empty `query` or `max_results` outside 1–20 |
| `search_unconfigured` | No `OCCAM_SEARCH_PROVIDER` (or missing url/key) |
| `search_http_*` | Backend returned non-2xx |
| `search_timeout` | Backend timed out |
| `search_error` | Network/parse failure |

---

## `occam_verify`

Verify a Receipt v1 extraction receipt — the consumer half of the verifiable-extraction flagship.
Source: `Tools/OccamVerifyTool.cs`. See [Tools reference](docs/tools-reference.md#11-occam_verify).

Five modes: `offline` (sig + hash), `live` (re-fetch + drift, incl. SI-02 granular "N/M blocks"),
`prove` (emit a compact citation proof for a block), `citation` (verify a block + proof against the
signed root **without the page**), `history` (verify a signed `occam_watch` change-chain, SI-05).

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `receipt` | string | **required** | Receipt JSON — a transcode `receipt` object (`{signed, blockLeaves}`) or a bare envelope; **or** a proof-carrying `occam://capsule/…` capsule (carries its own markdown → verified offline, no re-fetch). **history:** the watch `history` array or `{history:[...]}` |
| `markdown` | string? | omit | Check against the receipt's `contentHash` (offline) |
| `public_key` | string? | omit | PEM key to verify against; omit → this host's local key |
| `mode` | string | `offline` | `offline` \| `live` \| `prove` \| `citation` \| `history` |
| `block_index` | int? | omit | **prove:** which block to build a proof for |
| `block_text` / `block_selector` | string? | omit | **citation:** the block to check |
| `proof` | string? | omit | **citation:** proof JSON (`[{hash, siblingIsRight}]`) from a `prove` call |
| `chunks` | string? | omit | **live (SI-12):** JSON array of chunk leaf-hashes your RAG store holds — response reports which of these went stale |

Response (offline/live): `{ ok, signatureValid, contentHashMatch?, keyId, mode, live?, verdict }` —
`live` carries `{ refetched, contentHashMatch?, blockRootMatch?, blocksTotal?, blocksStillPresent?,
drift?, chunkStaleness? }`; `verdict` ∈ `verified` / `signature_invalid` / `content_mismatch` /
`drifted` / `refetch_failed`. **SI-12 chunk-level RAG expiry:** `chunkStaleness:{ total, present,
stale, staleChunks[] }` reports *which* specific chunks went stale (against `chunks` if supplied, else
the receipt's block leaves) so a RAG store invalidates individual fragments, not whole documents. **prove** → `{ ok, keyId, root, leafIndex, leaf, proof[] }`. **citation** →
`verdict` ∈ `citation_verified` / `citation_invalid` / `signature_invalid`. **history** → `{ ok,
signatureValid, keyId, mode, history:{ entriesTotal, signedCount, headSeq, chainValid }, verdict }`
with `verdict` ∈ `history_verified` / `history_invalid`. Failure codes
`invalid_receipt`, `invalid_arguments`.

**Time anchor (SI-15):** when the receipt carries a `timeAnchor` sidecar, `offline`/`live` add
`timeAnchor: { present, valid, genTime?, tsa?, tsaSubject? }` — an independent verification of the
RFC3161 token over the receipt signature. `valid:true` proves a TSA attested the signed receipt
existed **no later than** `genTime`. Produced by `occam_transcode` when `OCCAM_TIME_ANCHOR=1` +
`OCCAM_TSA_URL` are set (opt-in; fail-open). TSA chain-trust is out of scope for v1 — `tsaSubject` is
reported for the consumer to judge. Receipts are emitted by `occam_transcode` (`receipt.signed`
+ unsigned `receipt.blockLeaves` when `json_blocks` ran on success; `receipt.capsule` when
`emit_capsule=true`; a signed negative receipt on captcha/login/paywall/4xx). Toggle `OCCAM_RECEIPTS`;
key dir `OCCAM_KEYS_ROOT`.

**Proof-carrying capsule (`occam://capsule/…`):** the agent-to-agent form of a receipt — a single
self-contained string bundling the signed envelope, the markdown it commits to, the block leaves, and
a self-describing verify recipe. `occam_verify` accepts a capsule anywhere it accepts a receipt (it
supplies its own markdown for the offline `contentHash` check), so a receiving agent trusts a peer's
extraction offline without re-fetching. See [verified hand-off](skills/occam/references/verified-handoff.md).

**Verifying without the host.** A third party does not need the MCP host to check a receipt: the binary
exposes offline CLI verbs — `FFOccamMcp.Core keys export` (publish/pin the public key) and
`FFOccamMcp.Core verify [--mode receipt|citation|manifest|history]` (exit `0` verified / `1` not / `2`
usage). The byte-level contract for re-implementing verification in any language is
[docs/receipt_verification.md](docs/receipt_verification.md); the CLI is documented in
[docs/receipts.md](docs/receipts.md#verify-with-the-cli).

**Setup verb — `install-browser`.** `FFOccamMcp.Core install-browser` provisions the user-level
Playwright chromium the browser backend needs (no root, no system libs). It is the exact command a
browser-availability failure's `failure.fix.command` points at, so an agent or script can act on the
typed error without a human. A JSON marker goes to stdout (`{ ok, action:"install_browser",
status:"installed"|"already_present"|"worker_missing"|"failed", exitCode }`), playwright's own progress
to stderr. Exit `0` browser ready / `1` install failed / `2` worker tree not found. A configured system
browser (`OCCAM_BROWSER_CHANNEL=chrome|msedge` or `OCCAM_BROWSER_EXECUTABLE_PATH`) short-circuits to
`already_present` — nothing to download.

---

## `occam_claim_check`

Ground a claim in a page's provable source (SI-16, layer ③ building block). Source:
`Tools/OccamClaimCheckTool.cs`. See [Tools reference](docs/tools-reference.md#12-occam_claim_check).

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `claim` | string | **required** | The assertion to ground (a sentence) |
| `url` | string | **required** | Page to check the claim against |
| `backend_policy` | string | `http_then_browser` | `http` \| `browser` \| `http_then_browser` |
| `session_profile` | string? | omit | Session id for gated pages |
| `max_matches` | int | `3` | Top-K relevant blocks (1–10) |

Extracts with `json_blocks`, ranks blocks by BM25 relevance to the claim, and returns the top matches
each with a Merkle citation proof + the signed extraction receipt. Response: `{ ok, url, claim, found,
retrieved, verdict, blockMerkleRoot?, keyId?, matches:[{ blockIndex, text, sourceSelector?, score, leaf, proof[] }],
receipt?, proven?, timestamp }`. `found`/`retrieved` are identical retrieval-relevance booleans
(`found:false` / empty `matches` is an honest "does not appear to address this"
— a match must cover ≥ ~40% of the claim's content terms). **`found`/`retrieved` are retrieval only** — they
do **not** mean the page semantically supports the claim. `verdict` is always `not_evaluated` on this tool
(use `occam_attest` for fail-closed `status`). **Provable absence:** on `found:false`,
`proven:true` means the signed receipt attests a **complete** leaf set (`receipt.leafSetComplete` —
the extract wasn't truncated), so matching text provably does **not** appear in the extracted content —
not a silent miss. `proven:false` when completeness is unknown (truncated/empty extract). The tool proves
**which** block is lexically relevant (verifiable via `occam_verify citation` from the returned text +
proof, no re-fetch); **stance** (support vs refute) is the caller's judgment — or `occam_attest` —
never inferred from BM25. On extraction failure: the typed `{ failure:{ code, message } }` (+ signed
negative receipt on provable unavailability).

---

## `occam_attest`

Attest an LLM report against its own citations (SI-11, three-layer trust model over
`occam_claim_check`). Source: `Tools/OccamAttestTool.cs`. See
[Tools reference](docs/tools-reference.md#13-occam_attest) · [occam_attest](docs/tools/occam_attest.md).

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `claims` | string | **required** | JSON array of `{"claim","sourceUrl"}` rows (1–50) |
| `backend_policy` | string | `http_then_browser` | Applied to every cited page |
| `session_profile` | string? | omit | Session id applied to every cited page |

Per claim, three independent layers: (1) **retrieval** — claim-check BM25 top-K blocks (scores only;
never decide support); (2) **semantic classifier** — fail-closed `status` ∈
`supported` \| `contradicted` \| `related` \| `unsupported` \| `unknown`; (3) **Merkle proof** — when a
block is attached, `leaf` + `proof` prove only that the block existed in the signed extract, **never**
that the claim is true. Lexical co-occurrence alone must not yield `supported`.

Response: `{ ok, claimsTotal, supported, contradicted, related, unsupported, unknown, grounded,
unsupportedTotal, perClaim:[…], timestamp }`. Named status counts are canonical. `grounded` is a
compat alias for `supported` (`true` iff `status=supported`). `unsupportedTotal` = sum of all
non-supported statuses (`grounded + unsupportedTotal == claimsTotal`). Per-row: `{ claim, sourceUrl,
status, grounded, blockIndex?, text?, score?, leaf?, proof?, blockMerkleRoot?, receipt?, reason? }`.
Extraction failures → `status=unknown` (fail-closed). Invalid `claims` JSON or > 50 rows → typed
`{ ok:false, failure:{ code, message } }`.

---

## `occam_playbook_lint`

Statically validate a playbook/genome JSON against the 1.x schema (SI-13, no network). Source:
`Tools/OccamPlaybookLintTool.cs`. See [Tools reference](docs/tools-reference.md#14-occam_playbook_lint).

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `playbook_json` | string | **required** | The playbook / genome JSON to validate (an object) |

Returns `{ grade, agentReady, errors, warnings, infos, issues:[{ severity, field, code, message }] }`.
`grade` ∈ `ready` (clean) \| `usable` (warnings only) \| `broken` (has errors); `agentReady` is true iff
`errors == 0` (resolve/save would accept it). **Errors:** missing/invalid `schema_version` (1.x), missing
`id`, missing/empty `hosts`, missing/empty `extract.contentSelectors`. **Warnings:** non-bare host, invalid
`routing.preferred_backend`, blank selector, unrouted `knowledge_schema` class, missing `meta.title`.
**Info:** missing `agent_notes`. Use before a live `occam_playbook_save` to avoid a wasted verify.

---

## `occam_dataset_export`

Export a set of URLs as a signed, auditable dataset (SI-17). Source:
`Tools/OccamDatasetExportTool.cs`. See [Tools reference](docs/tools-reference.md#15-occam_dataset_export).

| Parameter | Type | Default | Notes |
|-----------|------|---------|-------|
| `urls` | string | **required** | JSON array of HTTP/HTTPS URLs (1–20) |
| `backend_policy` | string | `http_then_browser` | Applied to every URL |
| `session_profile` | string? | omit | Session id applied to every URL |

Each URL is transcoded (`json_blocks`) into a row `{ url, finalUrl, ok, contentHash?, blockMerkleRoot?,
failureCode?, rowLeaf, receipt? }` with its own signed receipt; the response `manifest` `{ v, createdAt,
rowCount, manifestRoot, keyId, alg, sig }` carries one detached signature over the Merkle root of the
per-row leaves. Verifiable per-row (`occam_verify`) and per-set: recompute each `rowLeaf`
(`url\nfinalUrl\nok\ncontentHash\nblockMerkleRoot\nfailureCode`, SHA-256), rebuild the ordered root, and
check `manifest.sig` — any add/drop/edit/reorder breaks it (row order is significant). Failed URLs are
honest rows (`ok:false` + signed negative receipt). `sig`/`keyId` omitted under `OCCAM_RECEIPTS=off`.
Invalid `urls` JSON or > 20 URLs → typed `{ ok:false, failure:{ code, message } }`.

---

## Environment

| Variable | Purpose |
|----------|---------|
| `OCCAM_HOME` | Repository root |
| `OCCAM_HTTP_EXTRACT_SCRIPT` | HTTP worker override |
| `OCCAM_BROWSER_EXTRACT_SCRIPT` | Browser worker override |
| `OCCAM_NODE_BIN` | Explicit `node` path |
| `OCCAM_BROWSER_PROFILE` | Browser mode preset (`shared`/`isolated` aliases) |
| `OCCAM_BROWSER_DAEMON` | `0` disables browser daemon |
| `OCCAM_BROWSER_POOL_SIZE` | Shared daemon slot count (1–8) |
| `OCCAM_BROWSER_DAEMON_PORT` | Daemon port |
| `OCCAM_BROWSER_DAEMON_IDLE_TTL_MS` | Browser daemon idle TTL in ms (`120000` default, `0` disables auto-stop) |
| `OCCAM_BROWSER_MAX_PARALLEL` | Browser concurrency cap (fallback: `WT_BROWSER_MAX_PARALLEL`) |
| `OCCAM_BROWSER_TIMEOUT_MS` | Per-browser extract timeout (queue wait derives from slot count) |
| `OCCAM_HTTP_DAEMON` | `0` disables HTTP daemon (one-shot mode) |
| `OCCAM_HTTP_DAEMON_IDLE_TTL_MS` | HTTP daemon idle TTL in ms (`120000` default, `0` disables auto-stop) |
| `OCCAM_DIGEST_PARALLEL` | `0` forces sequential digest execution |
| `OCCAM_MAX_RESPONSE_BYTES` | HTTP body size cap for extraction |
| `OCCAM_MAX_PDF_BYTES` | PDF body size cap (default 16 MiB; PDFs exceed the 1 MiB HTML cap). Oversize → `response_too_large` |
| `OCCAM_REQUEST_HEADERS_FILE` | Extra HTTP headers JSON |
| `OCCAM_SESSIONS_ROOT` | Directory for `session_profile` JSON files (default `~/.occam/sessions/`) |
| `OCCAM_RECEIPTS` | Signed Receipt v1 on transcode responses. On by default; `off`/`0`/`false` → telemetry-only receipt |
| `OCCAM_KEYS_ROOT` | Directory for the local ECDsa P-256 receipt signing key (default `~/.occam/keys/`) |
| `OCCAM_CACHE_DIR` | Directory for the opt-in `cache_ttl_s` response cache (default `{TEMP}/occam-cache`). Delete the dir to clear. Only written when a request opts in and is not private/session-bound |
| `OCCAM_PLAYBOOKS_LOCAL_ROOT` | Local learn tier directory (default `~/.occam/playbooks/local/`) |
| `WT_PLAYBOOKS_PATH` | User/org playbook tier directory |
| `OCCAM_SITE_GENOME_FETCH` | `1` enables well-known genome fetch on resolve when `fetch_site_genome` omitted (default off) |
| `OCCAM_ALLOW_PRIVATE_URLS` | Maintainer-only `1` — disables `private_url_blocked` locally (never an MCP param) |
| `OCCAM_RESPECT_ROBOTS` | `1` enables robots.txt `Disallow` enforcement (→ `robots_disallowed`) + `Crawl-delay`. Off by default (Occam is user-directed, not a crawler) |
| `OCCAM_HOST_THROTTLE_MS` | Per-host minimum request interval in ms (politeness throttle). `0`/unset = no throttle (default). Combined with robots `Crawl-delay` via max() |
| `OCCAM_ROBOTS_TIMEOUT_MS` | robots.txt fetch timeout (default `10000`, used only when `OCCAM_RESPECT_ROBOTS=1`) |
| `OCCAM_DOMAIN_TIERS_PATH` | Extra domain tier JSON (probe routing hints) |
| `OCCAM_BANNER` | `0` disables stderr banner (fallback alias: `WT_OCCAM_BANNER`) |
| `OCCAM_LOG` | `1` enables stderr profiler (fallback alias: `WT_OCCAM_LOG`) |
| `OCCAM_TLS_CERT_PATH` | TLS certificate for `--remote` |
| `OCCAM_TLS_CERT_PASSWORD` | PFX password; prefer env to the process-visible CLI flag |
| `OCCAM_JWT_ISSUER` | Expected JWT issuer; HTTPS discovery base when metadata is not explicit |
| `OCCAM_JWT_AUDIENCE` | Expected JWT audience |
| `OCCAM_JWT_METADATA_URI` | HTTPS OpenID Connect metadata document for signing-key discovery |
| `OCCAM_REMOTE_MAX_SESSIONS` | Remote WSS session cap (`4` default, `1`–`32`) |
| `OCCAM_MCP_MAX_MESSAGE_BYTES` | WebSocket MCP text-message cap (`4194304` default, `65536`–`16777216`) |

Complete table: [docs/configuration.md](docs/configuration.md).

---

## `session_profile` (P2-2 — shipped)

Optional per-call session headers (`Cookie`, `Authorization`, `User-Agent`, …) from a local profile file. Host exports browser login state offline; Core merges into HTTP + browser workers.

**Limits:** Netscape `cookies.txt` is not read directly by MCP — use `scripts/occam-session.mjs` (`import`, `import --all`, `export-state`). Exported `cf_clearance` cookies often **fail** on HTTP transcode (TLS bind); `storageState` + browser backend is the supported Cloudflare path. See [docs/configuration.md#session-profiles](docs/configuration.md#session-profiles).

**On:** `occam_transcode`, `occam_probe`, `occam_digest`, `occam_map`, `occam_playbook_heal`, `occam_extract_knowledge` · **Not on:** `occam_playbook_resolve` (no fetch).

| Item | Value |
|------|-------|
| Path | `{OCCAM_SESSIONS_ROOT}/<sanitized_id>.json` |
| Default root | `~/.occam/sessions/` |
| Format | Flat JSON: header name → string value; optional `storageState` path (under sessions root, browser worker) |
| Id sanitize | `[a-zA-Z0-9._-]` only; reject `..`, `/`, `\` → `invalid_session_profile` |

Merge precedence: `OCCAM_REQUEST_HEADERS_FILE` < session profile. Temp merged headers file per worker call — delete retries on cleanup and emits warning metadata on failure; header values are **never** logged or echoed in MCP JSON.

**Private URLs / SSRF:** `private_url_blocked` on all fetch tools (RFC1918, loopback, link-local `169.254.0.0/16`, `localhost`, `*.local`, `*.internal`, non-HTTP(S)). The worker also **resolves the host across both IPv4 and IPv6 and blocks any private answer** (e.g. `::1`, `fc00::/7`) — so a public hostname pointing at an internal address is rejected, not just literal-IP URLs. On the http backend the connection is **pinned to the validated IP** to defeat DNS-rebinding (TOCTOU). `session_profile` does **not** bypass. The worker emits the raw codes `private_ip_blocked` / `dns_resolution_failed`, canonicalized by the host to `private_url_blocked` / `dns_error`.

Gate: `L2_SESSION_OK` · Corpus: `corpora/l2-session.jsonl` · Operator CLI: `scripts/occam-session.mjs` · Guide: [docs/configuration.md#session-profiles](docs/configuration.md#session-profiles)

---

## Agent-First Enhancements (v0.9)

AF-1..AF-6 are additive quality-of-life improvements for LLM agents. No new MCP tools.

| Enhancement | Param | Response field | Purpose |
|-------------|-------|----------------|---------|
| AF-1 Confidence | — | `confidence: 0.0–1.0` (+ optional `quality` EQM breakdown) | Trust score from multi-signal extract quality (ADR-0004); length is not a sole gate |
| AF-2 Semantic transcript | — | `<!-- SNIP -->` markers in markdown | LLM sees document structure through truncation |
| AF-3 Receipt | — | `receipt: { tokensUsed, tokenEstimator, truncationStrategy, confidence, elapsedMs }` | Meta-context for every extract response; estimator provenance is explicit |
| AF-4 Auto-recovery | `backend_policy: http_then_browser` (default) | `recovery: [{ backend, ok, latencyMs }]` | One call, two backends, one response |
| AF-5 Intent-aware digest | `source_url`, `max_links` | `discoveredLinks[]`, `sourceUrl` | Auto-discover links from sitemap/HTML |
| AF-6 Differential | `if_none_match: <sha256>` | `unchanged: true` | Skip body when content hash matches |

### Agent recipe (AF-4 + AF-6)

```
1. occam_transcode(url, if_none_match=last_hash)   # recovery is automatic on the default http_then_browser
2. if unchanged → reuse prior analysis
3. if recovery.length > 1 → log fallback for observability
4. use receipt.confidence to decide trust level
```

---

## Out of scope

Anything not listed in parameters above.

| Ships | Does not ship |
|-------|---------------|
| Core MCP tools + opt-in batch/watch/crosscheck (see header) | Legacy `web_*` tools, `occam_bundle`, publish playbook MCP |
| Live extract only | File transcode cache |
| Linear digest ≤8, HTTP map ≤64 | Adaptive crawl, bundle |
| Read-only resolve + local heal/save + genome/extract | Community auto-merge from save |
| stdio + optional local WS | Public WS bind, TLS/OAuth on WS v1 |

Agent guide: [docs/choosing-a-tool.md](docs/choosing-a-tool.md) · Docs hub: [docs/index.md](docs/index.md)
