# `occam_canary_issue`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamCanaryIssueTool.cs` @ 2026-09-29  
**Profiles:** `reader` (default), `researcher`, `auditor`, `full` — not `minimal` or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Issue a proof-of-read canary URL and session. Returns a loopback probe URL the agent must fetch; **never** returns the sentinel. Quote the sentinel only after you actually read the page. Typical chain: `occam_transcode(url)` → `occam_canary_verify(session_id, sentinel)`.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Cryptographic proof that a page entered the agent context | Expecting the sentinel in the issue response |
| Eval / trust gates paired with `occam_canary_verify` | Profile is `minimal` or `basic` |
| After minting, you will fetch `url` and quote what you read | Ordinary page markdown with no proof workflow |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `session_id` | no | generated | Optional session id (1..128 chars of `[A-Za-z0-9._-]`). |
| `ttl_seconds` | no | protocol bucket window | Advisory TTL for `expiresAt` (30–86400). Crypto buckets unchanged. |

## Response shape

### Success (`ok: true`)

`url`, `sessionId`, `expiresAt` (ISO-8601), `bucket`.

### Failure (`ok: false`)

`failure: { code, message }`, `timestamp`. Do not invent a sentinel.

## Common failure codes

| Code | When |
|------|------|
| `invalid_arguments` | `ttl_seconds` outside 30–86400; invalid `session_id` |
| `canary_unavailable` | Loopback canary probe host failed to start |

## After issue: fetch the page

1. `occam_transcode` with the returned `url` (in-process for this host’s loopback canary URLs — `backend: "canary"`), or HTTP GET the URL.
2. Read the sentinel from the document.
3. `occam_canary_verify` with `session_id` and the quoted sentinel.

## Related env vars

| Variable | Role |
|----------|------|
| `OCCAM_CANARY_MCP_PORT` | Loopback probe port (`0` = OS-assigned) |
| `OCCAM_CANARY_BUCKET_SECONDS` | Bucket width (default 300) |
| `OCCAM_CANARY_FRESH_TOLERANCE` / `OCCAM_CANARY_STALE_HORIZON` | Fresh / stale windows |
| `OCCAM_CANARY_SENTINEL_BYTES` | HMAC tag length |
| `OCCAM_CANARY_ISSUE_LOG_*` / `OCCAM_CANARY_RATE_*` | Issuance log and rate limit |

Full catalog: [configuration](../configuration.md).

## See also

- [`occam_canary_verify`](occam_canary_verify.md) · [`occam_transcode`](occam_transcode.md)
- [TRUST](../TRUST.md) · [ADR 0010](../adr/0010-proof-of-read-canary.md) · [PROBE_PROTOCOL.md](https://github.com/ContextForgeAI/occam/blob/main/PROBE_PROTOCOL.md)
