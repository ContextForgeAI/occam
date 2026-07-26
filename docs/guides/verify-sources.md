# Guide: Verify a source

## What is this?

Check that a Receipt v1 envelope is **intact under a public key you trust** — signature, content hash, optional Merkle root, optional watch-chain links.

This is **integrity vs key**, not proof that the page said what was extracted or that the key belongs to anyone in particular.

## When should I use it?

You need tamper-evidence beyond “the chat said so”: audits, RAG freshness checks, sharing citation packages, or confirming a hand-off capsule's signed core.

## Minimal example

```json
{
  "name": "occam_verify",
  "arguments": {
    "receipt": "{ … receipt from transcode … }",
    "markdown": "# page text …",
    "mode": "offline",
    "public_key": "-----BEGIN PUBLIC KEY-----\n…"
  }
}
```

When verifying a receipt from **another** Occam install, always pass that install's public key PEM. Omitting `public_key` on MCP defaults to **this host's key** — fine for self-checks, wrong for third-party verification.

Human overview: [Receipts](../receipts.md) · Normative: [Receipt verification](../receipt_verification.md)

## Expected result

Fields such as `signatureValid`, `contentHashMatch`, and a verdict:

| Verdict (examples) | Meaning |
|--------------------|---------|
| `verified` | Signature valid under the supplied key; optional hash match |
| `wrong_key` | Signature fails under this key; claimed `keyId` differs from local |
| `signature_invalid` | Signature fails; same-key tamper suspected |
| `history_verified` | Watch chain linked and **every entry signed** |
| `history_chain_ok` | Watch chain linked but unsigned entries present |

`verified` means **bytes and key** — not truth, origin, or identity.

## MCP vs CLI

| Capability | MCP `occam_verify` | CLI `OccamMcp.Core verify` |
|------------|-------------------|---------------------------|
| `--pubkey` / `public_key` | Optional (defaults to local host key) | **Mandatory** |
| Dataset manifest verify | Not available | `--mode manifest` |
| Live drift / prove | Yes | No |
| Time anchor gates verdict | No (reported only) | Yes for `verified` |

## What can go wrong?

- Missing or wrong public key (foreign receipt verified against local key)
- Truncated receipt JSON or `blockLeaves` that do not reconstruct the signed root
- Markdown that does not match the signed `contentHash` (different budget/fit/focus than original extract)
- Treating Merkle citation as proof the quote supports your claim

## Next

- [Example: verify a receipt](../examples/verify-receipt.md)
- [Check a claim](claims.md)
- [Datasets](../datasets.md)
