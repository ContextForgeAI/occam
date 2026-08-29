# Receipts

**What you'll do:** understand Receipt v1 integrity checks, then verify envelopes offline or via MCP.

---

## In one sentence

A **Receipt v1** (`receipt.signed` on transcode/digest/claim-check/dataset/watch paths) lets another process check whether a signed envelope is **intact under a public key you supply** — URL, backend, content hash, optional Merkle root, and self-reported timestamp were asserted together and not altered since signing.

That is **integrity vs key** (mechanism). Occam's refusal to invent missing pages is **honesty** (behavior). See [Trust & Safety](trust-and-safety.md).

Normative byte-level rules: [Receipt verification](receipt_verification.md) · Tool: [`occam_verify`](tools/occam_verify.md)

---

## Integrity vs key

A signature answers one question: **did the holder of this private key assert these exact bytes, unchanged?**

It does **not** answer:

- Who that holder is (no PKI; `keyId` is a truncated key fingerprint, not an identity)
- Whether the origin server served the content
- Whether the markdown is true, complete, or fairly extracted
- Whether the fetch was fresh (cache hits replay the stored signed envelope)

Pin the public key out of band (`occam keys export` on the producer host, operator publish). Trust-on-first-use only.

---

## What is Receipt v1?

On eligible success paths, tools can return a `receipt` object:

| Field | Meaning |
|-------|---------|
| `signed.contentHash` | `sha256:` + hex hash of the **compiled** Markdown body (after budget/fit/focus — not raw HTML) |
| `signed.actionPlanHash` | Present on `occam_browser_interact` successes — `sha256:` hash of the canonical action plan (omitted on ordinary transcode) |
| `signed.blockMerkleRoot` | Merkle root over content blocks (when `json_blocks=true`) |
| `blockLeaves` | Leaf hashes for drift checks and citation proofs (**not signed** — authenticated only via root reconstruction) |
| `capsule` (opt-in `emit_capsule`) | `occam://capsule/…` wrapper: **signed core** plus **unsigned cargo** (content, leaves, optional timeAnchor/verifyRecipe). The wrapper itself is not signed. Verify the signed hashes — do not trust unsigned cargo as integrity proof. |
| `signed.sig` | ECDSA P-256 signature (base64url) over a canonical JSON envelope |
| `signed.keyId` | Public key identifier (`k1:` + fingerprint) |
| `timeAnchor` | Optional RFC3161 token over signature bytes (operator-configured; TSA cert chain not rooted in-product) |

`contentHash` is computed even when signing is disabled — an unsigned hash is a self-consistency token, not provenance.

---

## `OCCAM_RECEIPTS` is not a master switch

`OCCAM_RECEIPTS=off` disables **most** receipt emission on transcode, digest, watch, and crosscheck paths.

It does **not** turn off everything cryptographic:

| Still happens when `OCCAM_RECEIPTS=off` | Why |
|---------------------------------------|-----|
| Private key minted on disk | Host startup behavior |
| [`occam_playbook_save`](tools/occam_playbook_save.md) signs saved playbooks | Save path ignores `OCCAM_RECEIPTS` |
| `contentHash` and Merkle math on extracts | Computed unconditionally |
| Per-match Merkle proofs in claim-check | Root/leaves from blocks — unanchored without `signed` |

Never describe `OCCAM_RECEIPTS=off` as "signing off" for the whole product.

Configuration: [Configuration — receipts](configuration.md#receipts)

---

## Not Receipt v1: extract_knowledge `receipt`

[`occam_extract_knowledge`](tools/occam_extract_knowledge.md) returns a **`receipt` object that is extraction telemetry only** — typically `{ confidence, elapsedMs }`. It is unsigned, has no `contentHash`, no signature, and **`occam_verify` does not accept it**.

Call it **extraction telemetry**, not Receipt v1.

---

## Verify with `occam_verify` (MCP)

### Offline (default)

Check signature and optional markdown match:

```json
{
  "receipt": "{ … receipt object from transcode … }",
  "markdown": "# page text …",
  "mode": "offline",
  "public_key": "-----BEGIN PUBLIC KEY-----\n…"
}
```

`public_key` is **optional** on MCP — it defaults to **this running host's key**. Verifying a foreign receipt without supplying the producer's PEM usually yields `signature_invalid` or `wrong_key`, not a useful third-party check.

Response includes `signatureValid`, `contentHashMatch`, `verdict` (e.g. `verified`, `wrong_key`, `signature_invalid`).

### Live drift

Re-fetch the page and compare hashes (context-light re-fetch — drift often means missing session/playbook, not necessarily a changed page):

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

Proves **block membership** in the signed extract, not claim truth.

### Watch history chain

`mode: "history"` verifies a watch change chain:

- **`history_verified`** — every entry signed and valid under the key
- **`history_chain_ok`** — links consistent but unsigned entries present (integrity of shape, not authorship)

---

## Verify with the CLI

The CLI forces explicit key choice — **`--pubkey` is mandatory**:

```bash
# Export this host's public key
OccamMcp.Core keys export

# Verify receipt + markdown
OccamMcp.Core verify --receipt receipt.json --pubkey pubkey.pem --markdown page.md

# Verify dataset manifest (CLI-only — no MCP mode)
OccamMcp.Core verify --mode manifest --input export.json --pubkey pubkey.pem
```

Exit codes: `0` verified · `1` not verified · `2` usage.

!!! note "MCP vs CLI asymmetry"
    - **`manifest` verification** exists on the CLI only — MCP agents cannot verify dataset manifest signatures through `occam_verify`.
    - **`live` and `prove`** exist on MCP only.
    - **Time anchor** gates the CLI `verified` verdict but is reported non-gating on MCP for the same receipt.
    - The friendly `occam` operator wrapper does not expose `verify` / `keys` — use the host binary directly.

---

## Merkle citations (related, not a separate receipt type)

Merkle proofs prove a block was in the multiset committed by `blockMerkleRoot`. They do not prove the block was on the live page or that it supports a claim.

Tools: [`occam_claim_check`](tools/occam_claim_check.md), [`occam_attest`](tools/occam_attest.md) (per-claim block proof), [`occam_verify`](tools/occam_verify.md) `mode=citation`.

---

## Related tools

| Tool | Role |
|------|------|
| `occam_transcode` | Emits Receipt v1 on success (when receipts enabled) |
| `occam_claim_check` | Signed extraction receipt + Merkle proofs for retrieved blocks |
| `occam_attest` | Per-claim nested receipts; aggregate status tally is unsigned |
| `occam_dataset_export` | Per-row receipts + manifest signature |
| `occam_verify` | Consumer-side verification |
| `occam_extract_knowledge` | Telemetry `receipt` only — **not** Receipt v1 |

---

## Normative byte spec

Implement verifiers in any language: [Receipt verification](receipt_verification.md).
