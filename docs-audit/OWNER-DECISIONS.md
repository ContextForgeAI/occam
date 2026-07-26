# OWNER-DECISIONS (canonical)

**Status:** Phase 6.5A · 2026-07-26 · branch `fix/phase6-product-hardening`  
These are canonical product decisions unless later explicitly reversed. They govern Docs v3.

## OD-1 — Marketplace branch protection (EA-052)

| Field | Value |
|-------|-------|
| DECISION | Marketplace may be documented as **operational machinery**, NOT as a trusted auto-merge / trusted distribution guarantee, until external branch-protection evidence is recorded. |
| RATIONALE | Repository code cannot prove GitHub branch protection / required-check / merge-restriction state (EF-052). |
| CODE IMPACT | None beyond the Phase-6 in-repo workflow hardening (`l4_result==passed`). No external settings changed automatically. |
| COMPATIBILITY IMPACT | None. |
| DOCS IMPACT | Docs v3 must not claim "validated/trusted community auto-merge". `consensus-crosscheck` and marketplace trust remain excluded from trust claims. |
| TEST REQUIREMENT | Workflow gate already tested by review; external state verified manually per EA-052 checklist. |
| RELEASE IMPACT | Marketplace-as-trusted-channel is release-blocked until EA-052 verified; core product docs are NOT blocked. |

## OD-2 — Cosign (EA-053)

| Field | Value |
|-------|-------|
| DECISION | **Honesty now.** Do not make Cosign mandatory this phase. Classify release `.bundle` as release metadata / unused signing surface. |
| RATIONALE | No shipped install/update path verifies the cosign bundle (EF-053); install trust bar is sha256 vs unsigned manifest. |
| CODE IMPACT | None (do not expand theater; do not enforce). |
| COMPATIBILITY IMPACT | None. |
| DOCS IMPACT | Do not claim cosign-verified releases/installers or signed supply chain. Document install integrity as sha256-manifest only. |
| TEST REQUIREMENT | None new; honesty is a docs constraint. |
| RELEASE IMPACT | Not a Docs v3 blocker. Cosign-required install kept as future hardening. |

## OD-3 — npm channel (EA-034)

| Field | Value |
|-------|-------|
| DECISION | npm is **NOT a supported 1.0 install channel**. Classify INTERNAL / EXPERIMENTAL / NOT PUBLIC INSTALL PATH until an end-to-end npm contract passes. |
| RATIONALE | Package was DOA if published (EF-034); pack boundary fixed but no end-to-end install contract verified; publish intent undecided. |
| CODE IMPACT | None beyond Phase-6 vendored `lib/host-install-gate.mjs`. Package code retained. |
| COMPATIBILITY IMPACT | None. |
| DOCS IMPACT | Do not advertise npm/npx installation as GA in Docs v3. |
| TEST REQUIREMENT | Future gate: package available → install → host runtime → doctor → MCP launch → first read → verify/update. |
| RELEASE IMPACT | Not a Docs v3 blocker if exposure matrix marks npm non-GA. |

## OD-4 — Playbook signature v2

| Field | Value |
|-------|-------|
| DECISION | **Implement playbook signature v2 now** per `PLAYBOOK-SIGNATURE-V2-CONTRACT.md`. Preserve v1 verification; do not reinterpret v1 as v2. |
| RATIONALE | v1 excludes the whole top-level `provenance` (incl. trust-relevant `keyId`/`signedAt`/`verify.*`) from the signed preimage (EF-058); we must not normalize that as the long-term contract. |
| CODE IMPACT | New v2 preimage + signer/verifier/inspect version dispatch; save emits v2. v1 verify retained. |
| COMPATIBILITY IMPACT | Existing v1 artifacts still verify under v1 rules. New saves are v2. Inspect/verify report version. |
| DOCS IMPACT | Docs may state that v2 signatures cover keyId, signedAt, and the verify gate snapshot (as tamper-evident self-assertions — still integrity-vs-key, not truth/identity/time). |
| TEST REQUIREMENT | v1/v2 golden fixtures + mutation/wrong-key/unsupported-version verdicts (Phase 6.5C). |
| RELEASE IMPACT | Improves trust honesty; required before documenting playbook provenance as protected. |

## OD-5 — extract "Receipt"

| Field | Value |
|-------|-------|
| DECISION | Do not present the extract_knowledge `Receipt` field as Occam Receipt v1. Non-breaking: keep the wire field, add an accurate conceptual name (extraction telemetry / trace), mark legacy, never route through `occam_verify`. |
| RATIONALE | Field is unsigned telemetry, not Receipt v1 (EF-006, CAP-287). |
| CODE IMPACT | Minimal/none required for wire; add clarifying description/label; ensure verify path never accepts it as Receipt v1 (already true). |
| COMPATIBILITY IMPACT | None if wire field retained. |
| DOCS IMPACT | Docs call it extraction telemetry; explicitly "not a signed Receipt v1". |
| TEST REQUIREMENT | Assert extract response telemetry is not accepted by verify as a receipt (documented in HONESTY-SCHEMA-MAP). |
| RELEASE IMPACT | None. |

## OD-6 — claim_check "proven"

| Field | Value |
|-------|-------|
| DECISION | Freeze semantics: `claim_check` = evidence/support lookup over retrieved content; does NOT prove truth. Preferred vocabulary SUPPORTED / NOT_SUPPORTED / EVIDENCE_FOUND / NO_EVIDENCE. Keep wire `proven` for compatibility, documented as legacy; never described as cryptographic or factual proof. |
| RATIONALE | BM25 + regex retrieval is not proof (TRUST-MODEL NH-01/09). |
| CODE IMPACT | Non-breaking: retain field; optionally add interpreted status field where feasible. |
| COMPATIBILITY IMPACT | None if `proven` retained. |
| DOCS IMPACT | `proven:true` (with `found:false`) = retrieval-complete negative over the extracted leaf set, not semantic absence. |
| TEST REQUIREMENT | Description/schema honesty check. |
| RELEASE IMPACT | None. |

## OD-7 — attest

| Field | Value |
|-------|-------|
| DECISION | Keep tool ID. Canonical meaning: heuristic assessment of cited/retrieved support. Not cryptographic attestation / truth / provenance / identity. Smallest backward-compatible correction if schema overstates. |
| RATIONALE | Unsigned heuristic entailment (NH-02). |
| CODE IMPACT | Non-breaking description/label only. |
| COMPATIBILITY IMPACT | None. |
| DOCS IMPACT | Never "attestation" in the cryptographic sense; "heuristic citation assessment". |
| TEST REQUIREMENT | Schema honesty check. |
| RELEASE IMPACT | None. |

## OD-8 — crosscheck / consensus

| Field | Value |
|-------|-------|
| DECISION | Keep technical surface. Canonical concept: multi-source comparison / source agreement. Forbidden claim: "consensus proof". Experimental classification remains. |
| RATIONALE | Agreement across sources is not proof of correctness; verdict unsigned (EF-031/032, NH-03). |
| CODE IMPACT | None. |
| COMPATIBILITY IMPACT | None. |
| DOCS IMPACT | Prefer "crosscheck / multi-source comparison"; never "consensus proof / quorum / attested agreement". |
| TEST REQUIREMENT | Vocabulary honesty check. |
| RELEASE IMPACT | Experimental, env-gated. |

---

## Summary

| OD | Verdict | Docs v3 blocker? |
|----|---------|------------------|
| OD-1 | External verification required for marketplace trust | Only for marketplace-trust claims |
| OD-2 | Honesty-only cosign | No |
| OD-3 | npm not GA | No (classify non-GA) |
| OD-4 | Implement v2 now | Was; resolved this phase |
| OD-5 | Rename concept, keep wire | No |
| OD-6 | Freeze evidence semantics | No |
| OD-7 | Keep ID, strict meaning | No |
| OD-8 | Multi-source compare, no "consensus proof" | No |
