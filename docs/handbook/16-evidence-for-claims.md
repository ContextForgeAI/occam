# Chapter 16 — Evidence for claims and corpora

**Status:** STABLE · **Prerequisites:** [Chapter 8](08-structured-differential-output.md), [Chapter 14](14-what-a-receipt-proves.md), [Chapter 15](15-verifying.md)

---

## Mental model

**Retrieval plus membership, never stance.** `occam_claim_check` returns blocks that cleared a lexical BM25 floor plus a Merkle proof that each was in the signed extraction. It does not judge whether a claim is true, false, or absent from the page in any semantic sense.

---

## Explanation

### `occam_claim_check`

Workflow:

1. Live-fetch and compile the page (with playbook overlay forced to `auto` — no parameter to disable).
2. Rank extracted blocks against your claim string using BM25.
3. Return matches with Merkle proofs linking each block to the signed `blockMerkleRoot`.

Key fields:

| Field | Honest meaning |
|-------|----------------|
| `found` | A block cleared the lexical relevance floor |
| `proven` (only when `found:false`) | **Retrieval-complete negative:** the leaf set was untruncated, so no extracted block matched. Not proof the fact is false. |
| `verdict` | Always `not_evaluated` — claim_check does not classify support |
| `receipt` | A real signed Receipt v1 over the extraction — verifiable via `occam_verify` |
| `matches[].proof` | Merkle membership in the signed extract — not claim support |

Preferred vocabulary: **EVIDENCE_FOUND / NO_EVIDENCE / SUPPORTED / NOT_SUPPORTED** in prose; wire `proven` is legacy.

Neither `claim_check` nor `dataset_export` applies a token budget — a precondition for `leafSetComplete` / `proven:true`.

### `occam_attest`

Canonical meaning: **heuristic citation assessment**, not cryptographic attestation.

- Re-runs claim-check internally; accepts claim **text**, not claim-check JSON.
- Applies anchored regexes for two English shapes: `X is [a] Y` and `X uses Y`.
- Returns `supported | contradicted | related | unsupported | unknown`.
- Attaches one Merkle proof for the **top** block only.
- The aggregate response is **unsigned JSON**.

Never describe `occam_attest` as attestation in the cryptographic sense.

### `occam_dataset_export`

- Exports a set of URLs with row metadata and a detached manifest signature.
- Top-level `ok` describes **export completion**, not per-row success — inspect rows.
- Manifest verification is **CLI-only** (`occam verify --mode manifest` on the host binary).

### Task R step (conceptual)

Claim-check the exact sentence you plan to quote; take `leaf` + `proof` into `occam_verify mode=citation`; export source URLs as a dataset and verify the manifest on the CLI with `--pubkey`.

---

## CHECK

**NETWORK.** Claim-check a sentence you know is on the page. Re-run with a paraphrase that shares no terms with the original. The second returns `found:false` while the page plainly states the claim — demonstrating lexical retrieval limits, not semantic absence.

**LOCAL.** Confirm `occam_attest` aggregate JSON has no signature field and cannot be passed to `occam_verify`.

---

## Common misconception

**"`found:false` with `proven:true` means the page does not say it."** It means no extracted block cleared a lexical floor over an untruncated leaf set. Paraphrase, synonymy, non-English phrasing, text in images, content behind interaction, and anything the extractor dropped are all outside its reach.

---

## Limitations

- No stance evaluation in claim_check (`verdict` is hardcoded `not_evaluated`).
- Attest understands only two English claim shapes; everything else is `unknown`.
- Attest aggregate is unsigned — not verifiable as a unit.
- Dataset manifest verify requires CLI + out-of-band PEM — not MCP-in-band.
- Merkle proof proves block membership in the signed extract, not that the block supports your claim.
- No truth, origin, identity, or trusted-time claims are licensed by these tools.

---

## Links

- [Chapter 14 — Receipts](14-what-a-receipt-proves.md)
- [Chapter 15 — Verifying](15-verifying.md)
- [Chapter 24 — Composing tools](24-composing-tools.md) — rejected chains (claim_check JSON → attest)
- User docs: [Tools reference](../tools-reference.md)
- Honesty map: `docs-audit/HONESTY-SCHEMA-MAP.md` · `docs-audit/OWNER-DECISIONS.md` OD-6, OD-7
