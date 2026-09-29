# `occam_canary_verify`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamCanaryVerifyTool.cs`, `Canary/CanaryVerifier.cs` @ 2026-09-29  
**Profiles:** `reader` (default), `researcher`, `auditor`, `full` — not `minimal` or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Adjudicate a sentinel an agent claims to have read from a canary page issued by `occam_canary_issue`. The host checks cryptographic match, issuance provenance, and time-bucket freshness. Faking or guessing the sentinel is detected.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| After issue → fetch probe URL → quote the embedded sentinel | You have not fetched the page (guessing → `HALLUCINATED`) |
| Trust gates need proof-of-read evidence | You only need page content → `occam_transcode` |
| Separating real read vs hallucination / stale / unlogged replay | Missing `session_id` or `sentinel` |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `session_id` | yes | — | Session id from `occam_canary_issue`. |
| `sentinel` | yes | — | Sentinel quoted from the canary page (fetched, not guessed). |

Both values are trimmed before verification.

## Response shape

### Tool validation failure

Empty/whitespace `session_id` or `sentinel` → `{ ok: false, failure: { code: "invalid_arguments", message }, timestamp }`.

### Adjudication (inputs passed validation)

Always includes `verdict` — including when `ok: false`:

| Field | Meaning |
|-------|---------|
| `ok` | `true` only for `READ_VERIFIED` or `READ_STALE` |
| `verdict` | One of four wire tokens below |
| `bucket` | Same as `matchedBucket` when matched; else `null` |
| `reason` | Human-readable detail |
| `sessionId`, `currentBucket`, `matchedBucket`, `bucketDistance` | Bucket math |

## Verdicts (exhaustive; fixed precedence)

1. **`HALLUCINATED`** (`ok: false`) — No matching sentinel in the recognised window.
2. **`REPLAY_SUSPECT`** (`ok: false`) — Cryptographically valid, but no issuance record on this host.
3. **`READ_VERIFIED`** (`ok: true`) — Authentic, issued here, inside the fresh window.
4. **`READ_STALE`** (`ok: true`) — Authentic and issued, outside fresh but inside stale horizon.

## Common failure codes

| Code | When |
|------|------|
| `invalid_arguments` | Empty or whitespace-only args at the MCP tool layer |

Other outcomes use the adjudication envelope with `verdict`. See [failure-codes](../failure-codes.md).

## Related env vars

Same canary tunables as issue: `OCCAM_CANARY_BUCKET_SECONDS`, `OCCAM_CANARY_FRESH_TOLERANCE`, `OCCAM_CANARY_STALE_HORIZON`, `OCCAM_CANARY_SENTINEL_BYTES`, `OCCAM_CANARY_ISSUE_LOG_*`, `OCCAM_CANARY_RATE_*`.

Full catalog: [configuration](../configuration.md).

## See also

- [`occam_canary_issue`](occam_canary_issue.md) · [TRUST](../TRUST.md) · [ADR 0010](../adr/0010-proof-of-read-canary.md)
- [Choosing a tool](../choosing-a-tool.md)
