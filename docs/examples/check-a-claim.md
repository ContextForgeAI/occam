# Example: Check a claim

Retrieve lexically relevant blocks and Merkle membership proofs — then judge support yourself.

```json
{
  "name": "occam_claim_check",
  "arguments": {
    "claim": "HTTP status 404 is not escalated to the browser backend",
    "url": "https://example.com/docs/backends"
  }
}
```

## Reading the response

```json
{
  "ok": true,
  "found": true,
  "verdict": "not_evaluated",
  "matches": [
    {
      "text": "…",
      "score": 0.68,
      "leaf": "…",
      "proof": [ { "hash": "…", "siblingIsRight": true } ]
    }
  ],
  "receipt": { "signed": { "contentHash": "sha256:…", "sig": "…" } }
}
```

- **`found: true`** — a block was retrieved; it may support, refute, or merely mention the topic.
- **`verdict: not_evaluated`** — Occam did not classify support; you do.
- **`proof` + `receipt`** — verify block **membership** in the signed extract with [`occam_verify`](../tools/occam_verify.md) `mode=citation`, not claim truth.

When `found: false` and `proven: true`, that is a **retrieval-complete negative** over the extracted leaf set — not proof the page does not state the claim.

## Batch heuristic stance (optional)

[`occam_attest`](../tools/occam_attest.md) runs a narrow regex entailment classifier — **heuristic citation assessment**, not cryptographic attestation:

```json
{
  "name": "occam_attest",
  "arguments": {
    "claims": [
      {
        "claim": "…",
        "sourceUrl": "https://example.com/a"
      },
      {
        "claim": "…",
        "sourceUrl": "https://example.com/b"
      }
    ]
  }
}
```

Gate on per-claim `status`. The aggregate counts are **unsigned** JSON.

## Next

- [Verify a receipt](verify-receipt.md)
- [Guide: claims](../guides/claims.md)
