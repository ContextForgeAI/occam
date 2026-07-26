# Guide: Check a claim

## What is this?

**Evidence lookup** over extracted page content: retrieve blocks that lexically match a claim string (BM25), attach Merkle membership proofs when a signed root exists, and optionally run a separate heuristic stance pass.

[`occam_claim_check`](../tools/occam_claim_check.md) does **not** prove truth, falsity, or semantic absence from the page.

## When should I use it?

You have a sentence to ground (“Does this source contain text relevant to X?”) and want cited blocks with verifiable membership in the signed extract — before **you** (or [`occam_attest`](../tools/occam_attest.md)) judge support vs refute.

## Minimal example

```json
{
  "name": "occam_claim_check",
  "arguments": {
    "claim": "Occam returns typed failures when extraction fails",
    "url": "https://example.com/docs"
  }
}
```

For many citations with a heuristic stance partition, use [`occam_attest`](../tools/occam_attest.md) — **not** cryptographic attestation; an unsigned aggregate tally over regex-driven classification.

## Expected result

| Field | Honest reading |
|-------|----------------|
| `found: true` | A block cleared the BM25 relevance floor — read the text; judge support yourself |
| `found: false` | No block cleared the floor |
| `proven: true` (only when `found: false`) | **Retrieval-complete negative:** the leaf set was not token-truncated and no block matched — **not** proof the page omits the claim (paraphrase, images, unextracted regions still possible) |
| `proven: null` | When `found: true` — field not used |
| `verdict` | Always `not_evaluated` — claim_check does not classify support |
| `matches[].proof` | Merkle proof of **block membership** in the signed extract, not claim truth |
| `receipt` | Signed Receipt v1 for the page extract (verifiable via [`occam_verify`](../tools/occam_verify.md)) |

## Workflow

1. **`occam_claim_check`** — retrieve evidence + Merkle proofs; stance `not_evaluated`.
2. **You** read matching blocks and decide support vs refute.
3. Optional: **`occam_attest`** — heuristic `status` (`supported` / `contradicted` / …) for batch gatekeeping; gate on `status`, not on BM25 score alone.

## Next

- [Example: check a claim](../examples/check-a-claim.md)
- [Verify a source](verify-sources.md)
- [Trust & Safety](../trust-and-safety.md)
