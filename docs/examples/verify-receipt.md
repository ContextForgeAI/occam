# Example: Verify a receipt

After a successful transcode, take `receipt` from the response:

```json
{
  "name": "occam_verify",
  "arguments": {
    "receipt": "{ … paste receipt object … }",
    "markdown": "# … same markdown body …",
    "mode": "offline"
  }
}
```

Live drift check (re-fetch):

```json
{
  "name": "occam_verify",
  "arguments": {
    "receipt": "{ … }",
    "mode": "live"
  }
}
```

## What a receipt proves

Another process can check: URL, time, content hash, backend, and signature validity.

Human guide: [Receipts](../receipts.md) · Spec: [Receipt verification](../receipt_verification.md)

## Next

- [Check a claim](check-a-claim.md)
- [Trust & Safety](../trust-and-safety.md)
