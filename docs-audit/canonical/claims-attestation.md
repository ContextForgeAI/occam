# Claims and attestation

**Slug:** `claims-attestation` · **Product system:** PS-6 Trust and provenance · **CAPs:** 25 · **Public relevance:** HIGH.

## What it is

This family has two distinct layers:

- `occam_claim_check` extracts a source, retrieves lexically relevant blocks, and returns per-match Merkle citations; it never evaluates stance (`verdict=not_evaluated`) (CAP-690–703).
- `occam_attest` feeds those matches to a narrow rule/regex classifier for `supported|contradicted|related|unsupported|unknown`; its aggregate is unsigned (CAP-720–730; TRUST-MODEL C8/C9).

Names overstate guarantees. Claim check is retrieval, and attest is a heuristic classifier, not cryptographic attestation (TRUST-MODEL §4 C8/C9 and forbidden claims 7/8).

## Why it exists

- Retrieve compact source blocks relevant to a claim (CAP-690/695).
- Prove those blocks belonged to the signed extraction, when receipts are enabled (CAP-703).
- Distinguish retrieval from semantic stance explicitly (CAP-696).
- Apply fail-closed, narrow entailment rules to report citations before publication (CAP-720–730).

## User-visible entrypoints

| Tool | Profile | Inputs | Evidence |
|---|---|---|---|
| `occam_claim_check` | researcher/full/auditor | claim, URL, policy, session, max matches | CAP-690 |
| `occam_attest` | full/auditor | JSON claims array, policy, session | CAP-720/727 |

Both indirectly use the full extraction pipeline. Attest serially calls claim-check service for each row (CAP-728).

## Core behavior

Claim check:
1. Validate claim/URL/policy; force `JsonBlocks=true` and `PlaybookPolicy=Auto`; no token cap (CAP-690–693).
2. Extract full page through pipeline (CAP-691/702).
3. BM25-rank blocks and require coverage of at least ceil(40% of distinct claim terms) (CAP-695).
4. Return top 1–10 matches with text, selector, score, leaf, and proof (CAP-694/703).
5. Report `found/retrieved`, hardcoded `not_evaluated`, and narrow `proven` absence semantics (CAP-696/697).

Attest:
1. Parse 1–50 rows; batch overflow/invalid JSON fails before network (CAP-727).
2. Sequentially claim-check each source with one shared policy/session (CAP-728).
3. Classify top three retrieved blocks with two supported English claim shapes (CAP-721/722).
4. Return per-claim status/reason and unsigned aggregate counts (CAP-725/726).

## Advanced behavior

| Mechanism | Behavior | Evidence |
|---|---|---|
| Rank floor | Prefix/substring-aware BM25 plus ≥40% term coverage | CAP-695 |
| Provable absence | Complete untruncated extracted leaf set + no retrieved match | CAP-697/CAP-263 |
| IsA grammar | `X is [a|an|the] Y` | CAP-721/722 |
| Uses grammar | `X uses|using|utilizes… Y` | CAP-721/722 |
| Negation | Bounded copula/uses windows detect explicit contradiction | CAP-723 |
| Type conflict | Closed software-vs-data type heads infer contradiction | CAP-724 |
| Aggregation | Any contradiction beats support; unsupportedTotal includes all non-supported | CAP-721/726 |
| Proof attachment | Claim check: every match; attest: top match only | CAP-703/720 |

## Automatic / silent behavior

- Claim checking is uncapped by ambient client budget and always live/uncached (CAP-691/698).
- Playbook auto-policy is forced with no opt-out and not surfaced in response (CAP-693).
- `max_matches` silently clamps 1–10 (CAP-694).
- Merkle math persists with receipts off, but lacks signed root anchoring (CAP-699).
- Attest processes claims serially and applies one session/policy to all (CAP-728).
- Claims outside two regex shapes become `unknown` (CAP-722).
- Attest's semantic decision can use up to three blocks but exposes proof only for top block (CAP-720).
- Aggregate counts/status response is not signed (TRUST-MODEL C9).

## Parameters

Claim check:

| Name | Default | Effect |
|---|---|---|
| `claim` | required | Retrieval query |
| `url` | required | Source |
| `backend_policy` | `http_then_browser` | Pipeline route |
| `session_profile` | null | Source session |
| `max_matches` | 3 | Clamp 1–10 |

Attest:

| Name | Default | Effect |
|---|---|---|
| `claims` | required JSON string | 1–50 `{claim,sourceUrl}` rows |
| `backend_policy` | `http_then_browser` | Shared route |
| `session_profile` | null | Shared source session |

Evidence: `OccamClaimCheckTool.cs:19-39`; `OccamAttestTool.cs:18-59`; CAP-690/694/727/728.

## Configuration

No family-specific env variable. Pipeline/session/proxy/robots/managed/browser settings apply. `OCCAM_RECEIPTS` controls signatures but not Merkle computation (CAP-699). Playbook roots affect forced auto extraction (CAP-693).

## Backends

Both use `TranscodePipeline` and corrected router cascade. HTTP, browser, and configured managed success are reachable; 404/410/public-reference and failure-ranking corrections apply (CAP-702; EF-056).

## Sessions / state

`session_profile` has full Tier-1 pipeline behavior. No claim/attest cache or persistent state. Repeated checks re-fetch and re-rank (CAP-698).

Attest shares one session profile across all claims; no per-row override (CAP-728).

## Network behavior

Claim check performs one full extraction. Attest performs up to 50 sequential full extractions (CAP-728). No token budget or cache, so large pages can be expensive (CAP-691; EF-016).

Managed provider may receive cited URLs if operator configured it and router chooses it (CAP-702; EF-003).

## Artifacts produced

- ART-019 claim matches and citation proofs.
- ART-020 attest status batch, unsigned aggregate.
- Nested ART-007 receipt when policy enabled (`ARTIFACT-ONTOLOGY.md:111-113`).

No capsule or time anchor is produced by these tools (CAP-701).

## Trust / provenance properties

Claim match proof establishes exact extracted `(text,selector)` membership under signed root, not truth, stance, context, or origin-page presence (TRUST-MODEL C7/C8).

`proven:true` means no extracted block cleared a lexical floor over an untruncated extracted leaf set. It does not prove the page lacks a paraphrase, image text, unextracted region, other language, or semantically equivalent claim (TRUST-MODEL §4 C8).

Attest status is an unsigned classifier opinion. Only nested receipt/proof components can be independently verified; aggregate and semantic verdict cannot (TRUST-MODEL C9).

## Failure / fallback behavior

- Claim validation/policy errors → `invalid_arguments`.
- Pipeline failure returns typed extraction code and selected negative receipt (CAP-700).
- Claim retrieval failure under attest → `status=unknown`, never unsupported/contradicted (CAP-729).
- Zero matches with complete retrieval → unsupported; incomplete retrieval → unknown (CAP-730).
- Attest outer malformed/oversized batch fails all before network; invalid individual row becomes per-row unknown (CAP-727).
- No cache/retry; pipeline cascade is the only fallback.

## Platform differences

No classifier/ranker semantic platform differences. Acquisition inherits worker/process platform behavior. Key protection differs Windows/POSIX as described in receipts.

## Composition with other capabilities

- Forced playbook resolution shapes extraction (CAP-693).
- Claim check produces inputs consumed by attest (CAP-720).
- `occam_verify(citation)` can verify each returned proof with receipt/key (CAP-703).
- Dataset export is separate; no claim annotations enter manifests.
- Ambient budget does not apply despite client capability state (CAP-691/EF-016).

## Known limitations

- Retrieval is lexical, English-ish tokenizer, no semantic embeddings (CAP-695).
- No stance in claim_check (CAP-690/696).
- Two attest claim forms only (CAP-722).
- Closed type vocabularies (CAP-724).
- No cross-language translation.
- No cache or token budget (CAP-691/698).
- Playbook auto use hidden (CAP-693).
- Attest serial latency and one top proof (CAP-720/728).
- Aggregate unsigned and unverifiable (TRUST-MODEL C9).

## Engineering findings

- EF-016: claim_check/attest path lacks token budget.
- EF-003/046 may affect signed acquisition integrity through managed/browser behavior.
- CAP-693: hidden forced playbook auto.
- CAP-699: unsigned proofs can look cryptographic when receipts are off.
- Binding trust correction: never call claim absence page-proof or attest cryptographic attestation.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamClaimCheckTool.cs`
- `src/FFOccamMcp.Core/Claims/ClaimCheckService.cs:27-108`
- `src/FFOccamMcp.Core/Claims/ClaimBlockRanker.cs`
- `src/FFOccamMcp.Core/Tools/OccamAttestTool.cs`
- `src/FFOccamMcp.Core/Attest/AttestService.cs:20-175`
- `src/FFOccamMcp.Core/Attest/ClaimSemanticClassifier.cs`
- CAP-690–703, CAP-720–730; ART-019/020; TRUST-MODEL C8/C9.

## Public-doc relevance

Critical. Separate retrieval, membership proof, and heuristic stance. Define `proven` narrowly; state forced playbooks, no budget/cache, two claim grammars, serial batch behavior, and unsigned aggregate. Forbidden: “proves absent from page” or “attests citations cryptographically.”

## Handbook relevance

Provide two workflows: retrieve/cite with claim_check, then optional heuristic report screening with attest. Include status/reason interpretation, supported claim grammar, and a mandatory reminder that human/model semantic judgment remains necessary.
