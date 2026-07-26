# NAMING-HONESTY-DECISIONS

**Status:** ORCHESTRATOR-FROZEN for Docs Truth Gate — Phase 6 · 2026-07-26  
**Code landed on:** `fix/phase6-product-hardening` (EF-058 Inspect, EF-059 history split, EF-061 reader verify, EF-062 wrong_key)  
**Scope:** Intended **public meaning** of trust-adjacent names. **MCP tool IDs are not renamed.**  
**Binding:** `TRUST-MODEL.md` §2 + §13 forbidden claims; `PHASE6-INTENDED-CONTRACT.md`  
**Fundamental rule:** A signature proves integrity relative to a supplied key. It does not prove truth, origin authenticity, factual accuracy, external identity, trustworthy time, or semantic correctness.

---

## How to use this file

1. Every future public-doc sentence that uses a row’s **Name** must match **Intended meaning**.
2. Anything in **Forbidden expansion** is a docs/CI honesty violation even if historically shipped in marketing prose.
3. **Rename later?** column is advisory only — Phase 6 does not execute renames.
4. When code is patched (EF-058/059/061/062), update **Code target** and set Status to `FROZEN` after owner sign-off.

---

## Decision table

| ID | Name / surface | Status | Intended meaning (public) | Forbidden expansion | Evidence | Rename later? | Code target (when patching) |
|---|---|---|---|---|---|---|---|
| NH-01 | `occam_claim_check` | PROPOSED | Retrieve extracted blocks that clear a lexical BM25 floor for a claim string; attach Merkle membership proofs for returned blocks when a signed root exists. Stance is explicitly `not_evaluated`. `proven:true` (with `found:false`) means retrieval-complete negative over an untruncated leaf set — not semantic absence from the page. | “Checks whether the claim is true”; “evaluates stance”; “proves the page does not say X”; “fact check.” | TRUST-MODEL C8/§4; CAP-697; `ClaimCheckService.cs` | Prefer keep tool name; optionally rename response field `proven` → `retrievalComplete` in a future schema version | Response-field honesty; tool description |
| NH-02 | `occam_attest` | PROPOSED | Run a narrow, unsigned, regex/rule entailment classifier over claim-check hits; return status partition (`supported` / `contradicted` / `related` / `unsupported` / `unknown`). Nested receipts/proofs remain individually checkable; the **aggregate tally is not signed**. | “Cryptographic attestation”; “signed citation report”; “proves citations”; vendor/root attestation. | TRUST-MODEL C9; CAP-721/722/724; forbidden claim #7 | Keep tool name; consider response envelope field `attestationKind: "heuristic_entailment_v1"` | Tool description + response discriminator |
| NH-03 | `occam_crosscheck` / “consensus” | PROPOSED | One host fetches one URL through 2–4 **local** extraction vantages and compares `blockMerkleRoot` or `contentHash`. Verdict is an **unsigned observation**. Narrows bot-vs-browser / anon-vs-authed cloaking from one egress — nothing more. | “Multi-node consensus”; “N-of-M attestation”; “proves content is genuine”; geographic/CDN independence; signed jury. | TRUST-MODEL C10; EF-032; forbidden claims #9–10 | Keep env-gated tool name; prefer prose “crosscheck” over “consensus” in user docs; internal type names may stay | Server instructions + docs vocabulary |
| NH-04 | `history_verified` | **FROZEN** | Watch-history verification succeeded for **chain links and every entry’s signature** under the supplied public key (`signatureStatus=verified`, `chainIntegrity=true`). Weaker outcomes use `history_chain_ok` (unsigned but linked), `history_wrong_key`, `history_invalid`. | “History links are consistent” alone; success for wholly unsigned chains; “change events are authentic from the origin.” | EF-059 fixed in WatchHistory / OccamVerifyTool / CLI | Keep strong string; weaker verdicts shipped | Done |
| NH-05 | Extract-knowledge field `Receipt` | PROPOSED | Telemetry object on knowledge extract success: currently confidence + elapsed timing — **not** Receipt v1. | “Signed receipt”; “verifiable provenance”; interchangeable with transcode `receipt.signed`. | EF-006; CAP-287; `OccamExtractKnowledgeReceiptInfo` | Strong candidate to rename field → `telemetry` or `extractStats` in next breaking schema | Response schema + generators |
| NH-06 | Playbook `verify.score` / `passesGate` | PROPOSED | Local heuristic quality-gate **snapshot recorded at save**. Under playbook-sig **v1**, these fields live in unsigned top-level `provenance` and are **not** covered by the recipe signature. Even if covered in a future v2 assertion, they remain host self-assertions about a heuristic — not proof of recipe quality or safety. | “Signed quality score”; “signature guarantees gate pass”; “cryptographically verified playbook quality.” | EF-058; PlaybookSignature.cs:29-84,143-161; forbidden claim #12 | Keep JSON names; always qualify as `gateClaim` in prose; v2 may integrity-protect the claim without elevating meaning | Playbook sig scheme + Inspect |
| NH-07 | Receipt / verify `verified` | PROPOSED | Supplied receipt bytes verify under the **supplied** public key; optional markdown matches `contentHash`. | Identity of signer; origin served content; truth; freshness; trusted timestamp; “Occam vendor signed this.” | ReceiptVerifier.cs:17-21; forbidden claims #1–6, #15 | Keep | Optional `keySource` field |
| NH-08 | Playbook Inspect `unknown_key` / `wrong_key` | **FROZEN** | After crypto attempt: `wrong_key` when claimed keyId ≠ local and sig fails; `invalid` when key matches but sig fails; `unknown_key` only when appropriate post-verify. **Not** proof of a valid foreign author. score/passesGate remain **unsigned** under v1. | “Valid third-party authorship”; “signed quality score.” | EF-058 interim + EF-062 | Full playbook-sig v2 still OWNER follow-up | Inspect verify-first shipped; v2 deferred |
| NH-09 | Claim-check `proven` | PROPOSED | When `found:false`, absence of BM25 hits over a complete (non-truncated) extracted leaf set. | Claim false; page exhaustively searched; semantic completeness. | TRUST-MODEL §4 C8 | Rename later to `retrievalComplete` / `leafSetCompleteNegative` | Response field |
| NH-10 | `OCCAM_RECEIPTS` / “receipts off” | PROPOSED | Disables **most** receipt emission (transcode/digest/watch/consensus paths per policy). | Master switch that disables all signing, key mint, playbook signatures, contentHash, Merkle. | EF-005, EF-044; forbidden claim #13 | Keep env name; never call “signing master switch” | Policy docs only until engineering unifies |
| NH-11 | Capsule / “signed bundle” | PROPOSED | Transport wrapper; **only** nested receipt envelope is signed; cargo authenticated by hash checks. | Entire capsule JSON is signed; `verifyRecipe` is authoritative. | TRUST-MODEL C6; forbidden claim #11 | Prefer “proof-carrying capsule” over “signed capsule” | Prose only |
| NH-12 | Time anchor “timestamped” | PROPOSED | Optional RFC3161 token over signature bytes; proves signature existed by TSA `genTime` only if operator configured TSA; cert chain **not** rooted in-product. | Proves when page was fetched; trusted wall time by default. | TRUST-MODEL §9 Q8; forbidden claim #6 | Avoid “timestamped” without qualifier | Prose only |

---

## Consensus / crosscheck terminology (freeze)

| Prefer in user-facing text | Avoid |
|---|---|
| crosscheck, multi-vantage compare, fingerprint agreement | consensus network, quorum, majority vote, attested agreement |
| “unsigned verdict” when describing the aggregate | “verified consensus” |
| “per-vantage receipts may be signed” | “consensus is signed” |

Internal code names (`ConsensusService`, CAP “consensus”) may remain; public meaning follows the Prefer column.

---

## Receipt word discipline

| Phrase | Allowed when | Disallowed when |
|---|---|---|
| Receipt v1 / `receipt.signed` | Transcode/digest/dataset/watch artifacts with ECDSA envelope | Extract-knowledge `Receipt` field |
| receipt (lowercase, informal) | Only with immediate qualifier of which artifact | As a synonym for any success blob |
| telemetry / extract stats | Knowledge-extract timing/confidence | Implying verify/CLI compatibility |

---

## Owner sign-off

| Item | Decision | Date |
|---|---|---|
| Accept NH-01…NH-03, NH-05…NH-07, NH-09…NH-12 glosses for Docs v3 | **ORCHESTRATOR-ACCEPTED** (docs must use these glosses; tool IDs unchanged) | 2026-07-26 |
| NH-04 / NH-08 code targets | **SHIPPED** on this branch | 2026-07-26 |
| Allow future rename of extract-knowledge `Receipt` | PENDING (optional schema break) | |
| Allow future rename of claim-check `proven` | PENDING (optional schema break) | |
| Prefer “crosscheck” over “consensus” in public docs | **YES** | 2026-07-26 |
| Defer all MCP tool ID renames | **YES** | 2026-07-26 |
| playbook-sig v2 (sign verify.score etc.) | **OWNER PENDING** — interim honesty shipped | |

---

## Related engineering

- EF-058: Inspect honesty shipped; provenance fields still unsigned under v1 — do not document as signed quality  
- EF-059: fixed — unsigned ≠ `history_verified`  
- EF-061: fixed — reader includes `occam_verify`  
- EF-062: fixed — `wrong_key` / `key_mismatch` verdicts exist  
- EF-006 / EF-011 / EF-005 / EF-044 remain open limitations for docs warnings
