# `occam_probe`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamProbeTool.cs` and `OccamProbeModels.cs` @ 2026-09-29  
**Profiles:** `full`, `reader` (default), `researcher`, `auditor` — not exposed on `minimal` or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Cheap pre-fetch check over **HTTP only** (no Node extract workers): page class, risk signals, redirect chain, a 0–1 extractability score, and a recommended backend for `occam_transcode`. Use it to decide whether a full fetch is worth the cost.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Unknown or risky URL before `occam_transcode` | You already trust the URL — call `occam_transcode` with `{ "url": "…" }` only |
| You want `backend_policy` or session hints before a fetch | You need page body text — probe returns classification, not markdown |
| Search hit triage (same scorer as `occam_search` with `rerank=true`) | You need structured fields — use playbook + `occam_extract_knowledge` |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) URL or search handle (`S1` / `H…`). |
| `timeout_ms` | no | `10000` | Probe timeout in milliseconds (clamped to 1–120000 on the HTTP fetch). |
| `include_social_meta` | no | `false` | Extract OpenGraph/Twitter meta from HTML head. |
| `session_profile` | no | null | Optional session profile id — loads headers from `OCCAM_SESSIONS_ROOT/<id>.json`. |

## Response shape

### Success (`ok: true`)

- `url` — `{ requested, final? }`
- `classification` — `{ pageClass, requiresJavascript, likelyCookieConsent, likelyChallenge, likelyLoginRequired, likelyPaywall, riskFlags[], domainTier?, httpOnlyRoute?, challenge? }`
- `recommendation` — `{ backend, estimatedLatencyMs, extractability }` where `backend` is `http` | `http_then_browser` | `none`
- `policy` — `{ privacyMode }`
- `statusCode`, `contentType`, `probeLatencyMs`, `timestamp`
- Optional: `redirectChain[]`, `socialMeta`, `agentHints`, `access`

PDF URLs can succeed with `classification.pageClass` = `pdf` and `recommendation.backend` = `http`.

### Failure (`ok: false`)

- `url`, `failureCode`, `message`, `policy`, `probeLatencyMs`, `timestamp`
- Optional: `statusCode`, `redirectChain`, `agentHints`

`ok: false` means the page nature is **unknown** — do not invent content from model memory.

## Common failure codes

| Code | Typical cause |
|------|----------------|
| `invalid_arguments` | Missing/invalid URL at the tool boundary |
| `invalid_url` | Preflight or fetch URL validation failed |
| `private_url_blocked` | Private/local URL blocked (unless `OCCAM_ALLOW_PRIVATE_URLS=1`) |
| `stale_handle` / `unknown_handle` | Search handle missing or expired |
| `invalid_session_profile` / `session_profile_not_found` | Bad or missing session profile |
| `timeout` / `network_error` / `redirect_loop` | Probe HTTP path |
| `unsupported_content_type` | Not treated as HTML/plain/XML (PDF uses the success path) |
| `http_401`, `http_403`, `http_404`, `http_410`, `http_<status>`, `http_error` | Non-success HTTP status |

Full taxonomy: [failure codes](../failure-codes.md).

## Related env vars

| Variable | Role |
|----------|------|
| `OCCAM_SESSIONS_ROOT` | Directory for `session_profile` JSON files |
| `OCCAM_ALLOW_PRIVATE_URLS` | Set to `1` to allow private/local URLs |
| `OCCAM_REQUEST_HEADERS_FILE` | Optional JSON header bag merged before the probe GET |

Full catalog: [configuration](../configuration.md).

## See also

- [occam_transcode](occam_transcode.md) · [occam_search](occam_search.md)
- [Choosing a tool](../choosing-a-tool.md) · [Recipes](../recipes.md)
