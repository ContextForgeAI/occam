# Guide: Verify a source

## What is this?

Check that an extraction receipt is intact — signature, content hash, optional live drift.

## When should I use it?

You need provenance beyond “the chat said so”: audits, RAG freshness, sharing proofs.

## Minimal example

```json
{
  "name": "occam_verify",
  "arguments": {
    "receipt": "{ … receipt from transcode … }",
    "markdown": "# page text …",
    "mode": "offline"
  }
}
```

Human overview: [Receipts](../receipts.md) · Normative: [Receipt verification](../receipt_verification.md)

## Expected result

Fields such as `signatureValid`, `contentHashMatch`, and a verdict.

## What can go wrong?

Missing keys, truncated receipt JSON, or markdown that does not match the signed hash.

## Next

- [Example: verify a receipt](../examples/verify-receipt.md)
- [Check a claim](claims.md)
