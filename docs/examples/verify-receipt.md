# Example: Verify a receipt

After a successful transcode, take `receipt` from the response and verify **integrity under a public key you trust**.

## Offline check (MCP)

```json
{
  "name": "occam_verify",
  "arguments": {
    "receipt": "{ … paste receipt object … }",
    "markdown": "# … same markdown body …",
    "mode": "offline",
    "public_key": "-----BEGIN PUBLIC KEY-----\n…"
  }
}
```

For a receipt you produced on **this same host**, you may omit `public_key` (MCP defaults to the local key). For a receipt from another machine, export that machine's PEM first — otherwise you may get `wrong_key` or `signature_invalid`.

Trimmed success:

```json
{
  "ok": true,
  "signatureValid": true,
  "contentHashMatch": true,
  "verdict": "verified",
  "keyId": "k1:…",
  "mode": "offline"
}
```

`verified` = signature and hash check passed under **your supplied key** — not proof of truth or origin.

## Live drift check (re-fetch)

```json
{
  "name": "occam_verify",
  "arguments": {
    "receipt": "{ … }",
    "mode": "live"
  }
}
```

Compares a bare re-fetch to signed hashes. Large drift often means the original used session profile, playbook, or budget knobs the re-fetch dropped — not necessarily that the site changed.

## What a receipt proves

| Proves | Does not prove |
|--------|----------------|
| The key holder asserted URL, backend, content hash, optional Merkle root together | Who the key holder is |
| Signed bytes were not altered since signing | Origin server served that content |
| Merkle citation = block was in the signed extract | Block supports your claim |

Human guide: [Receipts](../receipts.md) · Spec: [Receipt verification](../receipt_verification.md)

## CLI (mandatory `--pubkey`)

```bash
OccamMcp.Core keys export
OccamMcp.Core verify --receipt receipt.json --pubkey pubkey.pem --markdown page.md
```

Dataset manifest verification is **CLI-only**: `--mode manifest`.

## Next

- [Check a claim](check-a-claim.md)
- [Trust & Safety](../trust-and-safety.md)
