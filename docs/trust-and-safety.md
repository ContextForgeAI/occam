# Trust and security

What Occam's trust layer actually does — and what it deliberately does **not** claim. Stated only as far as the shipped product supports today.

## Short answers

| Question | Answer |
|----------|--------|
| Where does processing run? | On your machine (local-first MCP host + workers) |
| Is there a cloud middleman for normal extracts? | No |
| Telemetry / phone-home endpoint? | No remote telemetry endpoint. `occam status` and `occam update` query GitHub Releases; normal extracts do not check for updates |
| Are pages cached on disk? | Default is live extract. Opt-in `cache_ttl_s` can replay a prior local materialization — not a CDN |
| What if extract fails? | Typed `ok: false` + `failure.code` — content is unknown |
| Can I check what was extracted? | Yes — optional signed Receipt v1 + [`occam_verify`](tools/occam_verify.md) (integrity vs a key you supply) |
| Will install rewrite my AI configs unsafely? | Connect uses backups, atomic writes, ownership checks; unmanaged entries are left alone |
| Does CI change my desktop MCP configs? | No — connect does not mutate desktops in CI by default |

## Honesty vs verification

- **Honesty** is behavior: Occam refuses to pretend it read a page when `ok: false`.
- **Verification** is mechanism: Receipt v1 binds URL, backend, content hash, and optional Merkle root to an ECDSA signature — checkable **against a public key you choose**.

Both matter; they are not the same claim. A valid signature proves **integrity relative to a key**, not truth, origin identity, or that the page actually said what was extracted.

## Local-first model

See [Local-first](trust/local-first.md). Session profiles, keys, and playbooks you save stay as local files under your control.

## Honest failures

`ok: false` means **unknown content**. Never summarize the page from model memory. Route: [Honest failures](trust/honest-failures.md) → [Failure codes](failure-codes.md).

---

## Trust primitives — what each proves

### Receipt v1 (signed extraction envelope)

!!! success "What this proves"
    The holder of the signing private key asserted this exact set of fields together (URL, final URL, backend, toolchain, content hash, optional block Merkle root, self-reported timestamp) and the bytes have not been altered since signing.

!!! failure "What this does not prove"
    - Who the key holder is (no PKI, no vendor identity, no registry)
    - That the origin server served that content (no TLS transcript captured)
    - That the extracted text is accurate, complete, or in context
    - That the fetch was live/fresh (cached replays reuse the stored signed envelope)
    - Truth of any claim about the page

Human guide: [Receipts](receipts.md) · Normative format: [Receipt verification](receipt_verification.md) · Tool: [`occam_verify`](tools/occam_verify.md)

### Signature (ECDSA P-256 over canonical bytes)

!!! success "What this proves"
    The canonical receipt (or playbook assertion, manifest row-set, or watch entry) was signed by whoever holds the matching private key; any change to signed fields breaks verification.

!!! failure "What this does not prove"
    - Identity of the signer beyond "whoever had this PEM"
    - That you should trust that key (TOFU only — pin the PEM out of band)
    - Semantic correctness of signed content

Keys are auto-minted locally on first host start (`~/.occam/keys/`). There is no Occam vendor signing key.

### Merkle inclusion (block citations)

!!! success "What this proves"
    A specific `(block text, source_selector)` pair was among the blocks committed to by the signed `blockMerkleRoot` — membership in the extraction the signer committed to.

!!! failure "What this does not prove"
    - That the block appeared on the live page (only in what Occam extracted)
    - That the block supports, refutes, or contextualizes any claim
    - Leaf-set completeness by itself (`blockLeaves` is not signed; only the root is)

Used by [`occam_claim_check`](tools/occam_claim_check.md), [`occam_attest`](tools/occam_attest.md) (per-block proofs), and [`occam_verify`](tools/occam_verify.md) `mode=citation`.

### Playbook signature (v1 vs v2)

!!! success "What this proves"
    - **v1 (`playbook-sig-v1` or absent marker):** the recipe **body** matches `contentHash` and was signed by the local key — integrity of the recipe JSON excluding top-level `provenance`.
    - **v2 (`playbook-sig-v2`):** same body binding **plus** tamper-evident `keyId`, `signedAt`, and the save-time gate snapshot (`verify.score`, `verify.passesGate`, `verify.noiseLeakage` when present).

!!! failure "What this does not prove"
    - Author identity, origin authenticity, or a trusted registry
    - Recipe quality or safety (`verify.score` is a local heuristic snapshot, not a guarantee — even when signed in v2)
    - Under **v1**, gate fields in `provenance` are **unsigned** and editable without invalidating the signature

See [Playbooks](playbooks.md) · [`occam_playbook_resolve`](tools/occam_playbook_resolve.md) · [`occam_playbook_save`](tools/occam_playbook_save.md)

### Watch history chain integrity

!!! success "What this proves"
    - **`history_verified`:** hash-chain links are consistent **and every entry's signature** verifies under the supplied public key.
    - **`history_chain_ok`:** links are consistent but one or more entries are **unsigned** — chain shape only, not per-entry authorship.

!!! failure "What this does not prove"
    - That change events reflect the origin server (only what this host recorded)
    - Authenticity when the chain is wholly unsigned (`history_chain_ok`, not `history_verified`)
    - Which vantage of a page was "correct" when entries disagree

Tool: [`occam_verify`](tools/occam_verify.md) `mode=history` · Opt-in: [`occam_watch`](tools/occam_watch.md)

---

## Related surfaces (honest labels)

| Surface | Honest meaning | Not |
|---------|----------------|-----|
| [`occam_claim_check`](tools/occam_claim_check.md) | Evidence lookup: BM25 retrieval + Merkle membership; `verdict` is always `not_evaluated` | Proof the claim is true/false or absent from the page |
| [`occam_attest`](tools/occam_attest.md) | Heuristic citation-support classifier (`supported` / `contradicted` / …); aggregate tally is **unsigned** | Cryptographic attestation or proof of truth |
| [`occam_crosscheck`](tools/occam_crosscheck.md) | Multi-source comparison from one host; per-vantage receipts may be signed; **agreement verdict is unsigned** | Consensus proof, multi-node quorum, or proof content is genuine |
| [`occam_extract_knowledge`](tools/occam_extract_knowledge.md) | `receipt` field = extraction telemetry (`confidence`, `elapsedMs`) | Receipt v1 — not accepted by `occam_verify` |
| [`occam_dataset_export`](tools/occam_dataset_export.md) | Signed row set + manifest (integrity of the export) | Factual correctness of row content |

---

## Installation and connect safety

Release archives are verified with SHA-256 against the release manifest. There is **no** cosign-enforced install path in the shipped product.

Connect backs up before write, writes atomically, protects unmanaged `ff-occam` entries, supports per-host rollback, and skips desktop mutation in CI unless forced.

Details: [Installation safety](trust/installation-safety.md) · [MCP hosts](mcp-hosts.md)

## Security policy

Vulnerability reporting and boundaries: [Security policy](trust/security-policy.md) (mirrors repository `SECURITY.md`).

!!! warning "Claims we do not make"
    Occam does **not** claim cryptographically verified provenance, tamper-proof content, consensus proof, cryptographic attestation, cosign-verified releases, npm GA install, or that signatures prove truth. Network pages are **untrusted input**.

## Next

- [Receipts](receipts.md)
- [Playbooks](playbooks.md)
- [Datasets](datasets.md)
- [Example: verify a receipt](examples/verify-receipt.md)
- [How Occam works](how-occam-works.md)
