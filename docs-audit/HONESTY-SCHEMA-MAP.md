# HONESTY-SCHEMA-MAP

**Status:** Phase 6.5D · 2026-07-26. Maps trust-adjacent wire fields to their true semantics so Docs v3 is not forced to lie by misleading terminology. No public docs edited here. Wire compatibility preserved per OD-5..OD-8.

Legend for action: **KEEP** (wire unchanged, docs carry the honest meaning) · **ALIAS** (add accurate name alongside legacy) · **DEPRECATE** (mark legacy, keep) · **CHANGE** (breaking — avoided this phase).

## occam_extract_knowledge

| WIRE NAME | CURRENT IMPLIED MEANING | ACTUAL SEMANTICS | ACTION | BREAKING? | DOCS WORDING |
|-----------|-------------------------|------------------|--------|-----------|--------------|
| `receipt` (object `{confidence, elapsedMs}`) | "an Occam Receipt v1" | Unsigned extraction telemetry: heuristic confidence + latency. No contentHash, no signature, no Merkle root. `occam_verify` does NOT accept it. | KEEP + DEPRECATE label (OD-5); code comment now marks it telemetry | No | "Extraction telemetry (confidence + elapsed time). Not a signed Receipt v1 and not verifiable via occam_verify." |
| `confidence` | quality guarantee | Heuristic extraction confidence, not a proof of correctness. | KEEP | No | "Heuristic confidence, not a correctness guarantee." |
| `facts[]` | verified data | Playbook-schema-driven field extraction; values are as-extracted, unsigned. | KEEP | No | "Typed fields as extracted; unsigned." |

## occam_claim_check

| WIRE NAME | CURRENT IMPLIED MEANING | ACTUAL SEMANTICS | ACTION | BREAKING? | DOCS WORDING |
|-----------|-------------------------|------------------|--------|-----------|--------------|
| `proven` (bool?, only on found=false) | "truth of absence is proven" | Retrieval-completeness flag: `true` iff the extracted leaf set was complete, so the claim's supporting text was not among the extracted blocks. NOT semantic proof of absence, NOT cryptographic. `null` when found. | KEEP + DEPRECATE label (OD-6) | No | "Retrieval-complete negative: the claim text was not found in the fully-extracted block set. Not proof the fact is false." |
| `found` / `retrieved` | claim is true / false | A relevant block was retrieved (BM25). Says nothing about support vs refute. | KEEP | No | "A relevant source block was retrieved; you judge support vs refute." |
| `verdict` (=`not_evaluated`) | semantic judgment | claim_check does not classify support; verdict is always `not_evaluated` here (use occam_attest for status). | KEEP | No | "claim_check does not judge support; verdict is not_evaluated." |
| `receipt` (ReceiptEnvelope) | — | A REAL signed Receipt v1 over the extraction (unlike extract_knowledge's telemetry). Verifiable via occam_verify. | KEEP | No | "Signed extraction Receipt v1 (verifiable)." |
| `matches[].` Merkle proof | "proves the claim" | Proves the cited block existed in the signed extract (membership), not that it supports the claim. | KEEP | No | "Merkle citation proof = block membership in the signed extract, not claim support." |

## occam_attest

| WIRE NAME | CURRENT IMPLIED MEANING | ACTUAL SEMANTICS | ACTION | BREAKING? | DOCS WORDING |
|-----------|-------------------------|------------------|--------|-----------|--------------|
| tool id `occam_attest` | cryptographic attestation | Heuristic assessment of cited/retrieved support. Not cryptographic attestation, not truth/provenance/identity certification. | KEEP (OD-7) | No | "Heuristic citation assessment (supported/contradicted/…); not cryptographic attestation." |
| `status` (supported\|contradicted\|related\|unsupported\|unknown) | verified truth | Semantic classifier verdict per claim; fail-closed. | KEEP | No | "Semantic classifier status; gate on it." |
| `grounded` (bool) | proven true | Compat alias: `true` ONLY when `status==supported`. Never derived from BM25 score. | KEEP | No | "grounded = status is supported; not a proof of truth." |
| per-claim Merkle proof | "proves the claim" | Block existed in the signed extract only. | KEEP | No | "Proof = block membership, not claim truth." |

## occam_crosscheck (consensus)

| WIRE NAME | CURRENT IMPLIED MEANING | ACTUAL SEMANTICS | ACTION | BREAKING? | DOCS WORDING |
|-----------|-------------------------|------------------|--------|-----------|--------------|
| tool id / `OCCAM_CONSENSUS_MCP` ("consensus") | consensus proof / quorum | Multi-source comparison / source agreement across vantages. Experimental, opt-in. | KEEP (OD-8) | No | "Multi-source comparison (source agreement); never 'consensus proof'." |
| `verdict` value `consensus` | proven correct | Vantages agreed. Agreement ≠ correctness. | KEEP wire token | No | "Vantages agreed; not proof the content is correct." |
| `verdict` `divergent`/`access_divergent` | — | Vantages differ / differ due to access-walling. Signals cloaking/personalization/geo. | KEEP | No | "Signals cloaking/personalization/geo/access variance." |
| per-vantage `receipt` | "signed verdict" | Each vantage extract carries a signed Receipt v1; the AGREEMENT verdict itself is computed, not signed. | KEEP | No | "Individual reads are signed & re-derivable; the agreement verdict is computed, not signed." |

## occam_verify

| WIRE NAME | CURRENT IMPLIED MEANING | ACTUAL SEMANTICS | ACTION | BREAKING? | DOCS WORDING |
|-----------|-------------------------|------------------|--------|-----------|--------------|
| `verdict` `verified` | truth verified | Integrity + signature valid relative to a KEY; offline/live/citation/history modes. Not truth, not trusted-key PKI. | KEEP | No | "Integrity verified against a key you supply; not proof of truth or key ownership." |
| history `signatureStatus` `unsigned` vs `chainIntegrity` | — | (Phase 6, EF-059) hash-chain integrity is reported separately from signature status; unsigned chains report `history_chain_ok`, never `history_verified`. | KEEP | No | "history_verified requires every entry signed & verified; unsigned chains show chain integrity only." |

## playbook inspection (occam_playbook_resolve → signature)

| WIRE NAME | CURRENT IMPLIED MEANING | ACTUAL SEMANTICS | ACTION | BREAKING? | DOCS WORDING |
|-----------|-------------------------|------------------|--------|-----------|--------------|
| `signature.status` (unsigned\|verified\|invalid\|wrong_key\|key_mismatch\|unsupported_version) | trusted recipe | Integrity of the recipe relative to the LOCAL key. `verified` = signed by your local key. Not origin/author identity. | KEEP | No | "Integrity vs your local key; not author identity or a trusted registry." |
| `signature.sigVersion` (1\|2) | — | NEW (OD-4): 1 = legacy (gate fields UNSIGNED); 2 = keyId/signedAt/verify snapshot are signed (tamper-evident). | KEEP (additive) | No | "v2 covers the gate snapshot; v1 leaves score/passesGate unsigned." |
| `signature.score` / `passesGate` | quality proof | Verify-gate heuristic snapshot. v1: unsigned claim. v2: signed (tamper-evident) but still a heuristic, not a proof of quality. | KEEP | No | "Heuristic gate score; signed in v2 (tamper-evident), still not a quality guarantee." |

## Cross-cutting docs constraints (frozen wording)

- Never: "consensus proof", "proves the claim/fact", "cryptographic attestation", "verified true", "trusted registry/author".
- Always disclose: retrieval = BM25 + regex; attest status = heuristic classifier; verify/signature = integrity-vs-key; playbook v1 gate fields unsigned.
- `occam_verify` accepts only Receipt v1 envelopes / signed history — never extract_knowledge telemetry.
