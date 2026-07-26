# P6-02 — Trust Semantics (Phase 6)

**Agent:** P6-02  
**Status:** ANALYSIS ONLY — no product patches, no public-doc edits  
**Binding baselines:** `docs-audit/TRUST-MODEL.md`, `ENGINEERING-FINDINGS.md` (EF-058…062, EF-005, EF-006, EF-011, EF-044), `PRODUCT-VS-ENGINEERING.md` §5–7  
**Code re-read:** `Playbooks/PlaybookSignature.cs`, `Watch/WatchHistory.cs`, `Receipts/ReceiptVerifier.cs`, `Tools/OccamVerifyTool.cs`, `Transport/OccamToolProfile.cs`, extract-knowledge receipt models  

---

## 0. Fundamental rule (LOCKED)

A cryptographic signature in Occam may prove **integrity of a byte-string relative to a supplied public key** (and, transitively, membership under a root that was itself so signed).

It **MUST NOT** be treated as proving any of:

| Forbidden expansion | Why |
|---|---|
| Truth / factual accuracy | Content is host-asserted extraction output |
| Origin authenticity | No TLS transcript, origin signature, or independent witness |
| External identity | `keyId` is a local SPKI fingerprint; no PKI/registry |
| Trustworthy timestamp | Default `ts`/`signedAt` = signer clock; TSA is opt-in, unsigned sidecar, unchained |
| Semantic correctness | Heuristics (`verify.score`, attest status, BM25, consensus strings) are not crypto |

**Product implication:** every success verdict (`verified`, `history_verified`, playbook `Status=verified`, `proven`, attest `supported`) is a **bytes/keys/links** claim unless a separate, explicitly non-crypto field says otherwise. Names that sound like truth/attestation require frozen honest glosses (see § Naming + `NAMING-HONESTY-DECISIONS.md`).

**Related policy findings (context, not patched here):**

| ID | Relevance |
|---|---|
| EF-005 | Playbook save signs even when `OCCAM_RECEIPTS=off` — signing policy ≠ receipts master switch |
| EF-044 | Key mint on every host start regardless of receipts |
| EF-006 | Extract-knowledge `Receipt` is not Receipt v1 |
| EF-011 | Unknown verify `mode` silently becomes `offline` — honesty of verify surface |

---

## EF-058 — Playbook signature vs top-level `provenance`

### CURRENT BEHAVIOR

- `PlaybookSignature.ContentHash` canonicalizes the playbook JSON with the **entire top-level `provenance` key excluded** (`PlaybookSignature.cs:29-39`).
- `BuildSignedJson` writes into `provenance`: `keyId`, `alg`, `contentHash`, `signature`, `signedAt`, and nested `verify{score,passesGate,noiseLeakage}` (`:63-84`).
- Detached signature covers **only** `utf8(contentHash)` (`:45-46`), not those provenance fields.
- `Verify` recomputes the provenance-excluding hash and checks the detached sig (`:143-161`) — so editing score/keyId/signedAt does **not** invalidate the signature.
- `Inspect` reads **unsigned** `provenance.keyId` and, on mismatch with local key, returns `unknown_key` **before** calling `Verify` (`:126-134`). Tamper of a self-signed recipe can be relabelled “foreign author”.
- Docstring still claims the signature carries “verify-gate proof (score/passesGate)” and that score/passesGate are “only trustworthy when Status == verified” (`:11-21`) — **false relative to code** (TRUST-MODEL §12 X1/X2; forbidden claim #12).

### PROBLEM

Unsigned, mutable fields sit next to a cryptographic signature and are echoed by `Inspect` as if gate-backed. Consumers (and future public docs) over-read “signed playbook” as “signed quality / authorship / time”.

### OPTIONS

| Option | Meaning |
|---|---|
| **A** | Provenance is intentionally unsigned metadata; product must never present it as protected |
| **B** | Trust-relevant fields must be covered by the signed preimage (or a second signed assertion) |

### RECOMMENDED SEMANTICS

**B for the trust-relevant subset; A only for non-trust display that is never surfaced as verified.**

**Trust-relevant (MUST be integrity-protected under the playbook signature scheme):**

1. Recipe body (already: `contentHash` over body-without-provenance) — keep.
2. Author claim used for local classification: `keyId`, `alg`.
3. Self-asserted signing instant: `signedAt` (still not a trusted third-party time — only tamper-evident self-claim).
4. Gate snapshot the product currently implies is “proof”: `verify.score`, `verify.passesGate`, `verify.noiseLeakage` **if** product continues to echo them from `Inspect` / resolve.

**Non-trust (MAY remain unsigned under A):** pure operator notes, free-form comments, future UI labels — never returned inside a `verified` trust signal.

**Canonicalization sketch (playbook-sig v2):**

- Keep body hash `H_body = ContentHash(json without provenance)` (v1-compatible computation).
- Build assertion object `A = { v:2, contentHash:H_body, keyId, alg, signedAt, verify:{…} }` with fixed field order.
- `signature = SignDetached(canonical_utf8(A without signature))`.
- Store `{ …A, signature }` under `provenance`.
- `Verify`: recompute `H_body`, require match to `A.contentHash`, then verify detached sig over `A`.
- `Inspect`: **never** short-circuit on claimed `keyId` alone. Order: parse → if no signature → `unsigned`; compute local keyId from PEM → if `A.keyId` ≠ local → attempt verify with local key anyway; if body+assertion fail → `invalid`; if keyId mismatch and sig fails under local key → `unknown_key` only after crypto attempt (or introduce `wrong_key` for “PEM keyId ≠ claimed keyId”). Prefer verifying first so tamper cannot manufacture a softer class.

**Honesty if owner chooses A instead:** freeze that `verify.*` / `signedAt` / claimed `keyId` are **UNSIGNED_CLAIMS**; change `Inspect` to stop echoing score/passesGate as trustworthy; fix misleading docstrings; still fix the `unknown_key`-before-verify branch (security honesty, not optional).

### BREAKING?

| Path | Breaking? |
|---|---|
| A + Inspect honesty only | Soft break: verdicts/fields reinterpreted; old files still Verify |
| B + v2 assertion | Hard break for consumers that assume v1 provenance shape; **mitigate with version field** |
| Re-sign-on-load | Not required if verify accepts v1 (body-only) and v2 (assertion) |

### COMPATIBILITY

- Add `provenance.v` or `provenance.sigScheme`: `playbook-sig-v1` (current) | `playbook-sig-v2` (assertion-covered).
- Verifier: accept both; writers: emit v2 only.
- Existing on-disk playbooks remain body-integrity-checkable under v1 rules; **do not** silently treat their `verify.score` as signed after upgrade.
- Gate: golden vectors for both schemes; tamper vectors on score/keyId must fail v2 and must not yield softer Inspect class.

### ACCEPTANCE CRITERIA

1. Editing `provenance.verify.score` or `passesGate` on a v2 playbook → signature invalid (or Inspect `invalid`), never `verified`.
2. Editing unsigned-only metadata (if any remains) does not affect Verify.
3. Editing claimed `keyId` on a self-signed playbook cannot turn `invalid` into `unknown_key` without a failed Verify attempt being observable.
4. Public/internal trust prose never says signature vouches for gate score unless v2 is live.
5. Fundamental rule preserved: even v2 signed `signedAt`/`score` prove **host assertion integrity**, not quality truth or identity.

### OWNER_DECISION REQUIRED?

**YES** — choose A (honesty-only) vs B/v2 (crypto cover). Recommendation: **B/v2**. Secondary: whether `noiseLeakage` is in the assertion.

### PATCH PLAN (deferred — no code this phase)

1. Introduce `PlaybookSigScheme` + dual Verify path.
2. Rewrite `BuildSignedJson` for v2 assertion canonicalizer (hand-written fixed order, AOT-safe, domain tag e.g. `occam-playbook-assertion-v2\0`).
3. Fix `Inspect` control flow (verify-before-classify).
4. Align docstrings with TRUST-MODEL forbidden claims.
5. Coordinate EF-005: decide whether v2 signing still ignores `OCCAM_RECEIPTS` (policy separate from semantics).

### TEST PLAN

- Unit: body tamper → invalid; score/keyId/signedAt tamper on v2 → invalid; v1 fixture still verifies body.
- Unit: self-signed + rewritten `keyId` → `invalid` (or `wrong_key`), not silent `unknown_key`.
- Gate: playbook heal/save/resolve path emits v2; L3/L4 corpora updated only after owner approves version bump.

---

## EF-059 — `history_verified` on unsigned watch chains

### CURRENT BEHAVIOR

- `WatchHistoryChain.Verify` checks seq continuity + `prevEntryHash` links; signature check runs **only if** `e.Sig is not null` (`WatchHistory.cs:126-163`). Docstring states unsigned entries skip sig check deliberately (`:128-130`).
- With `signer == null` (receipts off), `Append` writes empty `KeyId`/`Alg` and `Sig: null` (`:105-119`).
- MCP `occam_verify` mode `history`: `Verdict = chainValid ? "history_verified" : "history_invalid"`; `signedCount` is a **non-gating** side field (`OccamVerifyTool.cs:92-106`).
- CLI `verify --mode history`: same `history_verified` / exit 0; **no** `signedCount` in result (`OccamCliVerbs.cs:403-408`).

### PROBLEM

The string `history_verified` reads as “signatures checked.” An entirely unsigned, link-consistent chain returns success (TRUST-MODEL forbidden claim #14; D3).

### RECOMMENDED SEMANTICS

Split two independent predicates:

| Predicate | Meaning | Crypto? |
|---|---|---|
| **CHAIN_INTEGRITY** | Consecutive `seq`, each `prevEntryHash` equals `EntryHash(prior)` (hash includes sig when present) | Hash links only |
| **SIGNATURE_COVERAGE** | Every entry has non-null `Sig` | Presence |
| **SIGNATURE_VERIFIED** | Every present `Sig` verifies under the supplied PEM (and optionally keyId matches PEM) | ECDSA |

**Proposed response schema (MCP + CLI parity):**

```text
history: {
  entriesTotal: int,
  signedCount: int,
  headSeq: int,
  chainIntegrity: "valid" | "broken",
  signatureStatus:
    "all_verified" |      // signedCount == entriesTotal && all sigs OK
    "partial" |           // 0 < signedCount < entriesTotal, present sigs OK
    "unsigned" |          // signedCount == 0 (and chain may still be valid)
    "signature_invalid" | // at least one Sig fails under PEM
    "wrong_key" |         // feasible when entry.keyId ≠ keyId(PEM)
    "malformed"           // unparseable / inconsistent required fields
}
verdict:  // single machine string — see mapping below
ok: bool  // CLI exit 0 only when policy says so
```

**Verdict mapping (recommended):**

| Condition | `verdict` | CLI exit 0? |
|---|---|---|
| chain broken | `history_invalid` | no |
| chain valid + unsigned | `history_chain_ok` (**not** `history_verified`) | **no** by default (honest); owner may allow warn-only |
| chain valid + partial signatures all OK | `history_partially_signed` | no |
| chain valid + all signed + all verified | `history_verified` | yes |
| sig failure | `history_signature_invalid` / `wrong_key` | no |

**Deprecate** using `history_verified` for unsigned chains. Keep `CHAIN_INTEGRITY` visible so operators still detect reorder/drop without conflating authorship.

Optional verify flag later: `require_signatures=true` (default true for MCP history / CLI). Not required if verdict rename alone is enough.

### BREAKING?

**YES** for any consumer that treats `history_verified` + exit 0 as “OK” under `OCCAM_RECEIPTS=off`. Intentional trust-semantics break (PRODUCT-VS-ENGINEERING already flags this).

### COMPATIBILITY

- Ship new fields first (additive), then change verdict string in a versioned verify response (`verifyResult.v` or document in CHANGELOG when product phase allows).
- Dual-read: old agents that only look at `History.ChainValid` / `signedCount` can be taught; MCP already exposes `signedCount`.
- CLI must emit `signedCount` + `signatureStatus` (parity with MCP).

### ACCEPTANCE CRITERIA

1. Fully unsigned consistent chain → **not** `history_verified`; `signatureStatus=unsigned`; `chainIntegrity=valid`.
2. Fully signed good chain → `history_verified` + `signatureStatus=all_verified`.
3. Mixed chain → not `history_verified`.
4. CLI and MCP agree on verdict vocabulary and signedCount.
5. Docs/naming freeze: `history_verified` ⇒ signatures present and verified (see naming file).

### OWNER_DECISION REQUIRED?

**YES** — (1) confirm unsigned chains must not exit 0; (2) exact verdict strings; (3) whether partial chains are soft-OK for monitoring-only hosts.

### PATCH PLAN

1. Change `WatchHistoryChain.Verify` to return a structured result (not `bool`), or add `VerifyDetailed`.
2. Update MCP History() + CLI VerifyHistory emitters.
3. Gate: receipts-off watch append → verify history asserts new verdict.
4. Update TRUST-MODEL D3 when code lands (audit doc only in later sync).

### TEST PLAN

- Build unsigned windowed chain; assert chain OK + not verified.
- Tamper one link → `history_invalid`.
- Sign all; flip one byte of Sig → signature_invalid.
- Strip all Sigs but keep prev hashes rebuilt for unsigned canonicalization → unsigned success path.

---

## EF-060 / EF-062 — Verifier verdict vocabulary (and unsigned leaf counts)

### Note on ID scope

- **EF-062:** no `wrong_key` — wrong PEM and tamper both yield `signature_invalid` (`ReceiptVerifier.cs:6-15,40-64`); MCP defaults `public_key` to local host key (`OccamVerifyTool.cs:40`).
- **EF-060:** Merkle duplicate-last ambiguity — leaf-count-derived values are **unsigned** (`TRUST-MODEL` EFC-P5-05-3). Not the same enum, but same honesty family: do not let `verified` imply leaf-count integrity.
- **EF-011:** unknown `mode` → silent offline — related surface honesty; reject with `unsupported_mode` / `invalid_arguments` when patching verify.

### CURRENT BEHAVIOR

| Surface | Verdicts today |
|---|---|
| Receipt offline | `verified`, `signature_invalid`, `content_mismatch`, `invalid_receipt` |
| History | `history_verified`, `history_invalid` |
| Playbook Inspect | `unsigned`, `verified`, `invalid`, `unknown_key` |
| Manifest CLI | `unsigned`, `manifest_verified`, `manifest_invalid` |

No `wrong_key`, no distinct `unsupported_version`, no distinct `unsigned` on receipt offline (null sig → `invalid_receipt`).

### PROBLEM

Operators cannot tell “I pointed at the wrong PEM” from “bytes were tampered,” which pushes them to ignore failures (especially under MCP default key). Leaf counts/`blocksTotal`/`drift` look authoritative under a verified envelope though only the root is signed.

### RECOMMENDED SEMANTICS

Unify **where feasible** on a layered model:

**Layer 1 — parse/version (before crypto):**

| Code | When |
|---|---|
| `MALFORMED` | JSON/structure unusable |
| `UNSUPPORTED_VERSION` | `v` present but ≠ supported set (split out from today’s `invalid_receipt` when `V != CurrentVersion`) |
| `UNSIGNED` | Explicitly no signature (receipts-off artifact) — prefer over lumping into malformed |

**Layer 2 — key binding:**

| Code | When |
|---|---|
| `WRONG_KEY` | Envelope/entry carries `keyId` **and** `keyId(PEM) ≠ claimed keyId` (fingerprint mismatch). Do this **before or beside** ECDSA so wrong-PEM is distinguishable even when VerifyData returns false. |
| `INVALID_SIGNATURE` | `keyId` matches PEM (or claim absent) but ECDSA fails → integrity failure under the intended key |
| `VALID` / `verified` | ECDSA OK (+ optional content hash match) |

**Layer 3 — optional content / membership:**

| Code | When |
|---|---|
| `CONTENT_MISMATCH` | keep |
| Citation / merkle | keep membership verdicts; add note field `leafCountTrusted:false` always (EF-060), or refuse to report `blocksTotal` as authoritative |

**Playbook mapping:** replace misleading `unknown_key` (pre-verify) with: `wrong_key` (claim ≠ PEM) vs `invalid` (claim matches, crypto fails) vs true foreign (`unknown_key` only when verifying under a provided foreign PEM is out of scope / not attempted).

**MCP default key:** keep convenience but surface `keySource: "caller" | "local_host"` on every verify response so agents see self-check vs third-party check.

### BREAKING?

Additive fields: low. Splitting `invalid_receipt` → `unsupported_version` / `unsigned`: **mild breaking** for string-matchers. Adding `wrong_key`: **compatible** if old clients treat unknown strings as failure.

### COMPATIBILITY

- Keep legacy `signature_invalid` as alias of `INVALID_SIGNATURE` for one release if needed, or emit both `verdict` + `verdictClass`.
- Document enum in MCP_API_SPEC only when product phase allows public docs.

### ACCEPTANCE CRITERIA

1. Verify receipt signed by key A against PEM of key B → `wrong_key` (not only `signature_invalid`).
2. Same key, tampered field → `signature_invalid` / `INVALID_SIGNATURE`.
3. `v` bump unsupported → `unsupported_version`.
4. Null sig → `unsigned` (receipts-off) distinct from malformed JSON.
5. Verified envelope + duplicate-tail leaves: membership of original leaves still OK; **no API claim** that `leaves.length` is signed (EF-060).
6. Unknown mode → hard error (EF-011), not silent offline.

### OWNER_DECISION REQUIRED?

**YES** — exact string enum vs namespaced codes; whether MCP must require `public_key` for non-local trust (stricter, like CLI).

### PATCH PLAN

1. Extend `ReceiptVerification` (+ history detailed result).
2. Compute `ReceiptSigner.ComputeKeyId` from PEM in verifier path.
3. EF-011: explicit mode allowlist.
4. EF-060: documentation + optional `leafSetAmbiguity` warning when verifying proofs; no Merkle algorithm change required for honesty (algorithm change is a separate design if owner wants unique leaf-count binding).

### TEST PLAN

- Cross-key verify matrix; version field matrix; unsigned receipt; unknown mode → 4xx-style tool failure.
- Construct `[A,B,C]` vs `[A,B,C,C]` same root; assert verifier does not treat length as signed.

---

## EF-061 — `reader` profile emits receipts but hides `occam_verify`

### CURRENT BEHAVIOR

- `ReaderTools` includes `occam_transcode` (receipt producer) but not `occam_verify` (`OccamToolProfile.cs:17-32`).
- `occam_verify` appears in `ResearcherExtra` (`:28-32`).
- Default receipts policy still signs eligible successes (TRUST-MODEL A2); reader agents receive `receipt.signed` with no in-band verifier (D9 / EFC-P5-05-4).

### PROBLEM

Produce-but-cannot-verify breaks self-contained trust capability for the narrowest profile. Agents must leave-band to CLI (also poorly wrapped — EF-025) or escalate profile.

### OPTIONS

| Option | Action |
|---|---|
| **A** | Expose `occam_verify` on `reader` |
| **B** | Stop emitting receipts under `reader` |
| **C** | Verification CLI-only for reader; MCP remains producer-only |

### RECOMMENDED SEMANTICS

**Prefer A** — self-contained capability: any profile that can receive a signed receipt can offline-verify it in-band.

- Still omit playbook authoring / attest / dataset_export from reader (scope discipline unchanged).
- Optional: reader verify allowlist = `offline` + `citation` only (no `live` network, no `prove` emission) if owner wants least privilege — **sub-decision**.
- Reject B: removes tamper-evidence for the common read path without reducing key mint (EF-044 still mints).
- Reject C as steady state: CLI is not reachable via friendly wrapper (EF-025); agents in MCP hosts are the primary consumer.

### BREAKING?

A: **non-breaking** additive exposure (tool appears in `tools/list`).  
B: breaking for reader workflows that persist receipts.  
C: status quo (broken self-containment).

### COMPATIBILITY

Profile matrix / server instructions for reader must mention verify once A lands. No receipt schema change.

### ACCEPTANCE CRITERIA

1. `OCCAM_PROFILE=reader` → `tools/list` contains `occam_verify`.
2. Reader can `offline` verify a receipt it just received from `occam_transcode` with local or supplied PEM.
3. Reader still cannot call heal/save/attest (unless owner expands).
4. Banner/tool count dynamics remain profile-derived (no fixed literal).

### OWNER_DECISION REQUIRED?

**YES** — confirm A; optionally restrict reader modes (`offline`/`citation` vs full verify).

### PATCH PLAN

1. Add `"occam_verify"` to `ReaderTools` (or to a shared ReaderTrustExtra).
2. Adjust `OccamServerInstructions` ReaderText to one line on receipts → verify.
3. Profile matrix gate / unit test on exposed names.

### TEST PLAN

- Unit: `GetExposedToolNames("reader")` contains verify, excludes playbook_save.
- Manual/MCP: reader session transcode → verify offline → `verified`.

---

## Naming freeze (do NOT rename tools yet)

Intended **public meaning** (honest gloss). Full decision draft: `docs-audit/NAMING-HONESTY-DECISIONS.md`.

| Name | Intended public meaning | Must never mean |
|---|---|---|
| `occam_claim_check` | Lexical BM25 retrieval of extracted blocks + optional Merkle membership proofs; `verdict=not_evaluated` | Truth check; stance; “page lacks claim” as semantic absence |
| `occam_attest` | Unsigned regex entailment tally over claim-check hits | Cryptographic attestation; signed report |
| `occam_crosscheck` / consensus | Same-host multi-vantage fingerprint comparison; unsigned verdict | Multi-node / N-of-M / geographic / crypto consensus |
| `history_verified` | Chain links OK **and** every entry signature verified under supplied key | Link-only integrity; unsigned chain success |
| extract-knowledge `Receipt` | Telemetry `{confidence, elapsedMs}` | Receipt v1; signed provenance |
| `verify.score` / `passesGate` | Local heuristic gate snapshot at save (unsigned under v1) | Signed quality certificate |

---

## Cross-cutting patch sequencing (when product phase opens)

1. **Honesty-first (low risk):** EF-059 verdict split; EF-061 expose verify; EF-011 mode reject; naming glosses; Inspect verify-before-classify.
2. **Vocabulary (medium):** EF-062 `wrong_key` / `unsigned` / `unsupported_version`.
3. **Crypto cover (design):** EF-058 playbook-sig v2.
4. **Policy coherence (separate owner track):** EF-005 / EF-044 receipts master-switch story — do not pretend semantics patches fix mint/sign policy.

**PATCH_READY:** No — analysis and contracts only; awaits owner decisions checked below.

---

## Owner decision checklist

| # | Decision | Recommendation |
|---|---|---|
| D1 | EF-058 A vs B/v2 | **B/v2** |
| D2 | EF-059 unsigned chain exit code | **Non-zero**; verdict ≠ `history_verified` |
| D3 | EF-059 partial signature chains | Not `history_verified` |
| D4 | EF-062 require MCP `public_key`? | No (keep default) but emit `keySource` |
| D5 | EF-061 reader verify | **A** expose; optional mode subset |
| D6 | Rename tools for honesty? | **Not now** — freeze glosses only |
| D7 | EF-060 change Merkle construction? | **Not required** for honesty; bind leaf count only if product needs signed cardinality |
