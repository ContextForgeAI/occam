# Receipts

**What you'll do:** read signed extraction receipts and verify them offline.

---

## What is a receipt?

On `ok: true`, extract tools can return a `receipt` object:

| Field | Meaning |
|-------|---------|
| `signed.contentHash` | `sha256:` + hex hash of the Markdown body |
| `signed.blockMerkleRoot` | Merkle root over content blocks (when `json_blocks=true`) |
| `blockLeaves` | Leaf hashes for drift and citation proofs |
| `signed.sig` | ECDSA P-256 signature (base64url) over a canonical JSON envelope |
| `signed.keyId` | Public key identifier (`k1:` + fingerprint) |
| `timeAnchor` | Optional RFC3161 timestamp (when `OCCAM_TIME_ANCHOR=1`) |

Signing is **on by default**. Disable with `OCCAM_RECEIPTS=off` (telemetry only, no `signed` envelope).

Keys live in `OCCAM_KEYS_ROOT` (default `~/.occam/keys/`), generated on first use.

---

## Verify with `occam_verify`

### Offline (default)

Check signature and optional markdown match:

```json
{
  "receipt": "{ … receipt object from transcode … }",
  "markdown": "# page text …",
  "mode": "offline"
}
```

Response includes `signatureValid`, `contentHashMatch`, `verdict`.

### Live drift

Re-fetch the page and compare:

```json
{
  "receipt": "…",
  "mode": "live"
}
```

Pass `chunks` as a JSON array of leaf hashes your RAG store holds to see which fragments went stale.

### Citation proof

1. Transcode with `json_blocks: true`.
2. `occam_verify` with `mode: "prove"` and `block_index`.
3. Share `block_text` + `proof` with `mode: "citation"` — verifier needs no page HTML.

### Watch history chain

`mode: "history"` verifies a signed `occam_watch` change chain.

---

## Verify with the CLI

No MCP session required:

```bash
# Export this host's public key
OccamMcp.Core keys export

# Verify receipt + markdown
OccamMcp.Core verify --receipt receipt.json --pubkey pubkey.pem --markdown page.md

# Verify dataset manifest
OccamMcp.Core verify --mode manifest --input manifest.json --pubkey pubkey.pem
```

Exit codes: `0` verified · `1` not verified · `2` usage.

---

## Key trust

A valid signature proves **the holder of key `k1:…` signed this extract**, not that the key belongs to a particular organization. Pin the public key out of band (`keys export`, operator publish).

---

## Normative byte spec

Implement verifiers in any language: [Receipt verification](receipt_verification.md).

---

## Related tools

| Tool | Role |
|------|------|
| `occam_transcode` | Emits receipt on success |
| `occam_claim_check` | Receipt + Merkle citation for a claim |
| `occam_attest` | Per-claim receipts in batch |
| `occam_dataset_export` | Per-row receipts + manifest signature |
| `occam_verify` | Consumer-side verification |

Configuration: [Configuration — receipts](configuration.md#receipts).
