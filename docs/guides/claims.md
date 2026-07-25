# Guide: Check a claim

## What is this?

Ask whether a page contains text relevant to a specific claim — with citation proof.

## When should I use it?

You have a sentence to ground (“Does this source support X?”) rather than “read the whole page.”

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

For many citations at once, use [`occam_attest`](../tools/occam_attest.md).

## Expected result

Matching blocks with Merkle citation proof and receipt — or honest `found: false`.

The tool does **not** decide support vs refute; the model (or you) reads the proven text.

## Next

- [Example: check a claim](../examples/check-a-claim.md)
- [Verify a source](verify-sources.md)
