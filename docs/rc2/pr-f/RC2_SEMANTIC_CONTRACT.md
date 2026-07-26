# RC.2 semantic result contract

**Status:** Implemented in PR-F
**Invariant:** INV-9
**ADR:** ADR-0009 (outcome dimensions are not interchangeable)

## Dimensions

| Dimension | Public field(s) | Meaning |
|---|---|---|
| Transport | `recovery[].transportOk` (legacy alias `recovery[].ok`) | Backend invocation completed and returned a raw extract result |
| Usability | `recovery[].usable` | Result passed router quality policy (not thin/challenge) |
| Access | `access` | Shared PR-C assessment: `open` \| `restricted` \| `unknown` with scoped confidence |
| Focus | `focus.status` | `hit` \| `weak` \| `miss` \| `not_requested` |
| Completeness | `completeness` | `complete` \| `partial` \| `incomplete` (+ optional `incompleteReason`, `suggestedMinTokens`) |
| Verdict | `verdict` | Semantic support/refute judgment when computed; otherwise `not_evaluated` |
| Retrieval | `retrieved` (claim tools; legacy alias `found`) | Relevant candidate material was retrieved — never support |

## Overloaded legacy fields (unchanged meanings)

| Field | Still means | Must not be read as |
|---|---|---|
| Top-level `ok` | Tool path returned its success shape | Focus hit, complete answer, open access, or claim support |
| `confidence` | Extraction-quality confidence | Focus correctness or completeness |
| `focusMatched` | Digest lexical focus evidence | Exact section identity or completeness |
| `found` | Claim retrieval relevance | Semantic support |
| `recovery[].ok` | Transport/extract completion | Usability |

## Agent decision rules

1. Retry/escalate from `transportOk` / `usable` / `failureCode` / `escalationReason`, not from top-level `ok` alone.
2. Session/login decisions from `access.disposition`, not from authentication prose in Markdown.
3. Trust focused answers only when `focus` and `completeness` agree; incomplete focus stays incomplete even if `ok` is true.
4. Treat `found`/`retrieved` as retrieval; use `verdict` or `occam_attest` for support/refute.
5. Prefer structured dimensions over free-standing hint heuristics; hints are derived from those fields.
