# `occam_transcode`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamTranscodeTool.cs` and `OccamTranscodeModels.cs` @ 2026-09-29  
**Profiles:** exposed in `full`, `reader`, `researcher`, `auditor` (`OCCAM_PROFILE`; default `reader`). Hidden in `minimal` and `basic` (use `occam` cascade there).  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Extract the content of a web page (or PDF) as clean, compact, LLM-ready Markdown. Default page reader: prefer it over any generic web fetch/extract tool. Pass **`url` only**. On failure, **`ok: false` means the page content is UNKNOWN** — never guess it. Everything else is opt-in.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| You need what one URL says **now** (live fetch) | Several URLs → [`occam_digest`](occam_digest.md) |
| Typed failures and optional signed receipts | Cheap pre-flight only → [`occam_probe`](occam_probe.md) first |
| HTTP(S) URL or in-process search handle (`S1` / `H…`) | Expecting model memory to substitute for a failed extract |

## Parameters

Only **`url`** is required. Defaults match the C# tool signature (**29** MCP parameters).

### [core]

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) URL or search handle `S1`/`H…`. |
| `backend_policy` | no | `http_then_browser` | `http`, `browser`, or `http_then_browser`. |

### [tokens]

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `max_tokens` | no | null | Whole-response token budget (min 128). Omit for ambient client budget or full payload. |
| `fit_markdown` | no | `false` | BM25 paragraph prune after extract. |
| `focus_query` | no | null | Focus keywords; schema text says requires `fit_markdown=true`. |
| `content_selectors` | no | null | JSON array or comma-separated heading anchors to keep. |
| `toc` | no | `false` | Heading table of contents. |
| `section` | no | null | Focus a heading/section (enables fit). |
| `compact_links` | no | `false` | Strip markdown link URLs; keep link text. Changes `contentHash`. |

### [fetch]

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `session_profile` | no | null | Session profile id under `OCCAM_SESSIONS_ROOT/<id>.json`. |
| `playbook_policy` | no | `auto` | Playbook merge: `off` or `auto`. |
| `prefer_llms_txt` | no | `false` | Prefer `{origin}/llms.txt` when present. |

### [watch]

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `if_none_match` | no | null | Prior markdown SHA-256. Match → `unchanged:true`, empty markdown. |
| `diff_against` | no | null | Prior block hashes → `diff`. Pair with `if_none_match`. |
| `cache_ttl_s` | no | null | Cache TTL seconds (opt-in; never caches private/session/conditional paths). |
| `delta_only` | no | `false` | Diff-primary empty markdown. Needs `diff_against` + `json_blocks`. |

### [structured]

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `semantic_chunking` | no | `false` | Semantic markdown chunking. |
| `json_blocks` | no | `false` | Structured blocks for RAG. |
| `json_tables` | no | `false` | Tables as JSON. |
| `json_feed` | no | `false` | Parse RSS/Atom/JSON Feed into feed JSON. |
| `rank_blocks` | no | `false` | Per-block salience vs `focus_query`. Needs `json_blocks` + `focus_query`. |
| `tag_trust` | no | `false` | Heuristic `trust=suspicious\|boilerplate`. Needs `json_blocks`. |
| `must_contain` | no | null | Require substring; returns `mustContain` MATCH\|NO_MATCH. |
| `include_media_refs` | no | `false` | Include `mediaRefs`. |
| `compact_block_links` | no | `false` | Clear `blocks[].links` when `json_blocks`. |

### [advanced]

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `capture_screenshot` | no | `false` | Browser screenshot (JPEG base64). |
| `translate_to` | no | null | Target language (needs `OCCAM_TRANSLATE_URL`). Non-fatal on failure. |
| `deadline_ms` | no | null | Deadline ms (1000–300000). |

### [trust]

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `emit_capsule` | no | `false` | Emit `occam://capsule/…` in `receipt.capsule` (needs receipts). |

**Not a parameter:** there is no `auto_recover` flag — recovery is `backend_policy` (e.g. `http_then_browser`).

Bad `backend_policy` at the tool boundary returns `invalid_arguments` (not `invalid_policy`). Handle bind failures: `unknown_handle`, `stale_handle`.

## Response shape

### Success (`ok: true`)

Typical: `ok`, `url` `{url, finalUrl}`, `markdown`, `backend`, `contentHash`, `materializationKey`, `receipt`, `timings`.

Optional / suppressed when null or on conditional paths: `mediaRefs`, `compile`, `session`, `confidence`, `quality`, `recovery[]`, `unchanged`, `deltaOnly`, `chunks`, `screenshot`, `blocks`, `tables`, `feed`, `diff`, translation fields, `llmsTxt`, cache fields, `toc`, `mustContain`, `meta`, `statusCode`, `agentHints`, semantic `access` / `focus` / `completeness`.

### Failure (`ok: false`)

`failure` `{code, message, ...}`, optional `agentMeta` / `agentHints`, `timings`, `recovery[]`, optional negative `receipt`. **Never invent page content from memory.**

## Common failure codes

Representative: `invalid_arguments`, `workers_unavailable`, `unknown_handle`, `stale_handle`, `timeout`, `network_error`, `dns_error`, `tls_error`, `http_*`, `thin_extract`, `render_error`, `content_selectors_miss`, `captcha_or_challenge`, `requires_login`, `paywall`, `session_profile_not_found`, `invalid_session_profile`, `private_url_blocked`, `robots_disallowed`, `response_too_large`, `response_truncated`, `extraction_failed`.

Semantics: [Failure codes](../failure-codes.md).

## Related env vars

| Variable | Role |
|----------|------|
| `OCCAM_HOME` | Worker discovery |
| `OCCAM_SESSIONS_ROOT` | Session profiles |
| `OCCAM_TRANSLATE_URL` | `translate_to` backend |
| `OCCAM_PROFILE` | Tool surface |
| Receipts / client budget | See [configuration](../configuration.md) |

## See also

- [`occam_probe`](occam_probe.md) · [`occam_digest`](occam_digest.md) · [`occam_verify`](occam_verify.md) · [`occam`](occam.md)
- [Failure codes](../failure-codes.md) · [Receipts](../receipts.md)
