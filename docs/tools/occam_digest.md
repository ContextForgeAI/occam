# `occam_digest`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamDigestTool.cs`, `OccamDigestModels.cs`, and `Services/DigestService.cs` @ 2026-09-29  
**Profiles:** `basic`, `reader`, `researcher`, `auditor`, `full` — not exposed in `minimal`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Research several live pages in one call: transcode up to eight URLs into per-page excerpts, optional combined Markdown with `##` titles, and optional link discovery from a `source_url`. Prefer one digest over many separate `occam_transcode` calls when you already have multiple URLs.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Several URLs (or a site entry point) and you want excerpts plus an optional merged block | Full single-page materialization → [`occam_transcode`](occam_transcode.md) |
| A shared `focus_query` should prune each excerpt | Link list only, no bodies → [`occam_map`](occam_map.md) |
| `source_url` discovery (with optional focus ranking) | No URLs yet → [`occam_map`](occam_map.md) or [`occam_search`](occam_search.md) first |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `urls` | yes* | — | *Required when `source_url` is omitted. JSON string (legacy) or array of URL strings. Ignored when `source_url` is set. Native arrays must be URL strings only. |
| `backend_policy` | no | `http_then_browser` | `http`, `browser`, `http_then_browser`, or `http-then-browser`. |
| `max_urls` | no | `8` | Process at most 1–8 URLs. |
| `per_url_max_tokens` | no | null | Per-URL output token budget (minimum **128**). Omit for ambient client budget or full excerpt. |
| `focus_query` | no | null | Focus keywords per URL; also drives focused `source_url` discovery ranking. |
| `fit_markdown` | no | `true` | Paragraph prune per URL (default **true**, unlike transcode). |
| `include_combined` | no | `true` | Include merged markdown with `## {title}` sections. |
| `session_profile` | no | null | Session profile id applied to every URL. |
| `source_url` | no | null | Auto link discovery. When set, **`urls` is ignored**. |
| `max_links` | no | `8` | Max links to discover from `source_url` (1–8). |
| `if_none_match` | no | null | SHA-256 of prior **combined** markdown. Match → `unchanged: true` and empty `combined`. |

Supply **`urls` and/or `source_url`** (at least one). Empty discovery → `invalid_urls`.

## Response shape

**Success (`ok: true`):** `digestId` (16-char hex), `items[]` (per-URL success or failure), `stats`, optional `combined`, `sourceUrl` / `discoveredLinks[]`, `unchanged`, `agentHints`, `timestamp`. Digest-level `ok` may be **true** even when some items failed.

**Failure (`ok: false`):** `failureCode`, `message`, `timestamp`; optional partial fields. Treat failed item bodies as unknown.

## Common failure codes

Digest-level: `invalid_arguments`, `invalid_urls`, `invalid_url`, `private_url_blocked`, `invalid_session_profile`, `session_profile_not_found`, `stale_handle`, `unknown_handle`, `workers_unavailable`, `digest_failed`.

Per-item codes use the transcode taxonomy. Full list: [failure codes](../failure-codes.md).

## Related env vars

| Variable | Effect |
|----------|--------|
| `OCCAM_DIGEST_PARALLEL=0` | Force sequential per-URL transcodes |
| `OCCAM_DIGEST_MAX_PARALLEL` | Cap concurrent transcodes (1–8) |
| `OCCAM_CLIENT_CONTEXT_TOKENS` | Ambient per-URL budget when `per_url_max_tokens` is omitted |
| `OCCAM_PROFILE` | Whether `occam_digest` is exposed |

Full catalog: [configuration](../configuration.md).

## See also

- [`occam_transcode`](occam_transcode.md) · [`occam_map`](occam_map.md) · [`occam_search`](occam_search.md)
- [Choosing a tool](../choosing-a-tool.md) · [Recipes](../recipes.md) · [Failure codes](../failure-codes.md)
