# Issue a proof-of-read canary

**Canonical tool:** `occam_canary_issue`

Mint a short-lived canary **URL + session** so an agent can prove it actually
read a page. The response **never** includes the sentinel — quote it only after
fetching `url` (typically via `occam_transcode`).

**Trust rule:** Returns a canary URL. Quote the sentinel only if you actually
fetched the page. Faking it will be detected.

## When to use

- Research / eval harnesses that need cryptographic proof-of-read
- Before trusting an agent summary of a sensitive page
- Pair with `occam_canary_verify` after the fetch

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `session_id` | string? | generated | no | 1..128 chars `[A-Za-z0-9._-]`; generated when omitted |
| `ttl_seconds` | int? | protocol | no | Advisory TTL for `expiresAt` (30–86400); crypto buckets unchanged |

## Returns

```json
{
  "ok": true,
  "url": "http://127.0.0.1:<port>/probe/canary/<sessionId>",
  "sessionId": "…",
  "expiresAt": "2026-09-17T18:00:00.0000000+00:00",
  "bucket": 6000000
}
```

Next step: `occam_transcode({ "url": "<url>" })` → read `Sentinel: \`…\`` from
markdown → `occam_canary_verify`.

## Related

- [occam_canary_verify](occam_canary_verify.md)
- [TRUST](../TRUST.md) · [PROBE_PROTOCOL.md](https://github.com/ContextForgeAI/occam/blob/main/PROBE_PROTOCOL.md)
- CLI: `occam canary` (unchanged)
