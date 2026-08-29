# Chapter 14 — What a receipt proves — and what it does not

**Part E — Trust** · **Spine chapter** · Prerequisites: [Ch 2](02-honesty-contract.md), [Ch 7](07-materialization-token-contract.md) · Next: [Chapter 15 — Verifying](15-verifying.md)

---

## Mental model

**A receipt is a local integrity log entry, not provenance.**

It proves one sentence:

> *The holder of this private key asserted these exact compiled bytes (and optional Merkle block commitment), and they have not been altered since.*

Nothing else. No origin proof, no identity, no truth, no trusted time by default.

---

## Explanation

### Receipt v1 envelope (positive extraction)

Signed fields include url, finalUrl, backend, ts, toolchain, contentHash, blockMerkleRoot (when blocks present), optional `actionPlanHash` (browser-interact plans), keyId, alg, signature.

**Always computed even when signing disabled:** `contentHash` on transcode success—unsigned hash is self-consistency only.

**Hash covers:** UTF-8 **compiled markdown after budgeting/fit/focus/translate policy**—not raw HTML ([Chapter 7](07-materialization-token-contract.md)). Two budgets ⇒ two legitimate hashes.

### Merkle layer

- Leaves hash `utf8(text + '\0' + source_selector)`.
- Root commits to ordered multiset; proofs show **membership**, not truth or on-page context.
- `blockLeaves` travels **unsigned**—verifier reconstructs root. Duplicate-last construction means tail leaf count is not a signed quantity—do not treat `blocksTotal` from leaves alone as committed.
- Merkle math may still appear when `OCCAM_RECEIPTS=off`—cryptographically unanchored without signature.

### Negative receipts

Sign that a wall code was hit for this fetch context. Proves **failure claimed**, not that content is globally inaccessible.

Unreachable branch: `"paywall"` in negative set has no producer—omit from mental model.

### Capsule

`occam://capsule/…` wraps signed core + unsigned cargo (`content`, `blockLeaves`, `timeAnchor`, `verifyRecipe`). Wrapper is **not signed**. `verifyRecipe` is advisory—verifier must check signed hashes, not recipe text.

### Time

- **`ts`** — signer's clock, inside signature—self-asserted.
- **RFC3161 anchor (opt-in)** — timestamps **signature bytes**, not content. TSA cert not chained to trust root. Fail-open to absent anchor with no warning.

### Cache and freshness

Opt-in cache replays stored signed envelope with original `ts`. `cached:true` outside signature. Receipt **cannot prove live fetch** or freshness.

Fragment cache key omission can serve wrong fragment's signed envelope—trust and cache are a risky pair ([Chapter 8](08-structured-differential-output.md)).

### Key and policy

- Key minted on first host start **regardless of `OCCAM_RECEIPTS`** (`~/.occam/keys/signing-key.pem`).
- **`OCCAM_RECEIPTS=off`** stops most receipt emission—not key mint, not **`occam_playbook_save`** signing.
- **`keyId`** — truncated SPKI fingerprint; no identity binding. TOFU over PEM obtained out of band.
- Windows: key file permissions hardening may be no-op; PKCS8 unencrypted.

### What verify licenses (preview of Ch 15)

Offline verify checks signature + optional content hash + Merkle proofs—**arithmetic over bytes and keys**. MCP `occam_verify` defaults to **local host key** if `public_key` omitted—foreign receipts look `signature_invalid`. CLI requires `--pubkey`. Neither proves truth.

### Task R step 11

Hold the receipt for the rate-limit sentence you will quote. Label each field mentally:

- **Signed:** url, finalUrl, backend, ts, contentHash, blockMerkleRoot, keyId, alg, sig, …
- **Unsigned cargo:** blockLeaves arrays, capsule wrapper, cache flags, some diagnostics
- **Self-asserted but signed:** ts, backend string (host claims which path ran)

---

## CHECK

**LOCAL**

1. Run host with `OCCAM_RECEIPTS=off`.
2. Transcode a success URL.
3. Assert:
   - `contentHash` (and likely `blockMerkleRoot`) still present on success.
   - Key file still exists under `~/.occam/keys/`.
4. `occam_playbook_save` still signs playbooks.

This confirms `OCCAM_RECEIPTS` is not a master off-switch.

---

## Common misconception

**"Signed by Occam means it came from the origin."**

It means **an Occam install's locally minted key** signed the host's compiled extraction. A fabricated markdown produces an equally valid-looking signature. No TLS transcript, no origin signature, no third-party witness.

---

## Limitations — eight sentences receipts do **not** license

1. "The origin server served this content."
2. "This install belongs to organization X." (`keyId` is not identity.)
3. "The page said this" without qualification—only "this host asserted this extraction."
4. "The claim is true / absent"—use `claim_check` vocabulary correctly; not proof of fact.
5. "Cryptographically attested report"—`occam_attest` aggregate is unsigned heuristic.
6. "Consensus proof"—crosscheck verdict is unsigned observation from one process.
7. "Fresh/live fetch"—cache replay preserves old `ts`.
8. "Trusted supply chain install"—cosign bundle not verified by shipped install; manifest is sha256-only.

Also: receipts do not prove CAPTCHA bypass, do not prove semantic summarization (there is none), and do not prove npm package authenticity.

Playbook v1: gate scores unsigned. Playbook v2: gate snapshot tamper-evident, still not objective quality proof.

---

## Forbidden phrasing (binding)

| Do not write | Write instead |
|--------------|---------------|
| Verified provenance | Tamper-evident against the signing host's key |
| Proves the page said this | Proves this host asserted this extraction |
| OCCAM_RECEIPTS=off disables signing | Stops most receipt emission; not key mint or playbook save |
| Signed by Occam | Signed by this install's locally minted key |
| history_verified = signed history | Requires every entry signed & verified (Phase 6); unsigned chains differ |
| Consensus proof | Multi-source comparison / source agreement |

Full list: `docs-audit/TRUST-MODEL.md` §13.

---

## Links

**Public docs:** [Receipts](../receipts.md) · [Receipt verification](../receipt_verification.md) · [Verify sources](../guides/verify-sources.md) · [Trust & Safety](../trust-and-safety.md) · [Semantic contract](../architecture/semantic-contract.md)

**Handbook:** [Index](index.md) · Previous: [Chapter 13](13-typed-field-extraction.md) · Planned: Chapter 15 (verification modes)
