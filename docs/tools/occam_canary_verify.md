# Verify a proof-of-read canary

**Canonical tool:** `occam_canary_verify`

Adjudicate a sentinel the agent claims to have read from a canary page issued by
`occam_canary_issue`.

## When to use

- After `occam_canary_issue` → fetch → quote sentinel
- To distinguish genuine reads from hallucination / replay / stale context

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `session_id` | string | — | **yes** | From `occam_canary_issue` |
| `sentinel` | string | — | **yes** | Value quoted from the fetched page (not guessed) |

## Returns

```json
{
  "ok": true,
  "verdict": "READ_VERIFIED",
  "bucket": 6000000,
  "reason": "Sentinel is authentic, was issued by this host, and is inside the fresh window.",
  "sessionId": "…",
  "currentBucket": 6000000,
  "matchedBucket": 6000000,
  "bucketDistance": 0
}
```

| Wire verdict | Meaning | `ok` |
|--------------|---------|------|
| `READ_VERIFIED` | Authentic, issued, fresh | `true` |
| `READ_STALE` | Authentic, issued, older bucket | `true` |
| `HALLUCINATED` | No match in recognised window | `false` |
| `REPLAY_SUSPECT` | Authentic crypto, never issued here | `false` |

`ok` mirrors `IsReadEvidence` (verified or stale). Replay is authentic crypto
with bad provenance — treat as failure for trust gates.

## Related

- [occam_canary_issue](occam_canary_issue.md)
- [TRUST](../TRUST.md) · ADR [0010](../adr/0010-proof-of-read-canary.md)
