# RC.2 open questions

The defaults below are recommendations, not assumed product decisions. “Blocking” identifies the first
implementation PR that cannot safely complete without an owner answer.

| Priority | Owner question | Recommended default | Blocks |
|---|---|---|---|
| P0 | May RC.2 add an honest array/string union to `occam_digest.urls` while preserving strings? | Yes; treat it as additive prerelease compatibility, contingent on two-client schema proof | PR-B |
| P0 | What is the hard-login policy when status is open but DOM evidence is unavailable/inconclusive? | `unknown`; retain usable content with a warning, never infer from prose | PR-C |
| P0 | Must constrained focus return usable partial content or a typed failure when the minimum answer unit cannot fit? | Return bounded content with `completeness=incomplete`, reason, and suggested minimum; reserve failure for no usable content | PR-E/F |
| P0 | Is `max_tokens` a hard serialized-response ceiling or a planning hint? | Hard ceiling within a documented estimator tolerance; never auto-expand silently | PR-E |
| P1 | Which worker DOM signals are permitted in the Core contract? | Booleans/counts, normalized action route, and blocking/content flags; no raw values/HTML | PR-A/C |
| P1 | How should URL fragments interact with an explicit `focus_query`? | Exact resolved fragment is a high-priority candidate; explicit query supplies ranking context and may report conflict, not silently replace the fragment | PR-D |
| P1 | What evidence is sufficient for `focus=hit`? | Resolved answer section plus body/structured evidence; a heading or TOC mention alone is at most `weak` | PR-D/F |
| P1 | How long do legacy `ok`, `found`, and `focusMatched` aliases remain? | Accepted for RC.2: keep aliases through the RC migration window; remove only at an announced breaking boundary (documented in `pr-f/LEGACY_FIELD_MIGRATION.md`) | PR-F/H — accepted default |
| P1 | Does `claim_check` remain retrieval-only or gain a semantic verdict? | Keep retrieval-only and rename/add `retrieved`; direct agents to `attest`/verification for verdict | PR-F |
| P1 | What classifier recall and `unknown` rate are acceptable on the expanded login corpus? | Require 100% on controlled direct-wall fixtures, zero named public-prose false positives, then approve a measured broader threshold | PR-C |
| P1 | What performance guardrails are release-blocking? | Local PR-H soak + prior PR measurements stay within informational guardrails; formal numeric ratification remains owner-owned before GA | PR-A/H — measured, not formally ratified |
| P1 | Who owns an Occam instance launched through Hermes: dashboard, gateway, or Occam launcher? | External host owns desired state; Occam owns exact descendants and exposes identity/targeted shutdown | PR-G |
| P2 | Is there a stable Hermes lifecycle callback/lease API? | Treat as unavailable until source/API evidence exists; implement host-agnostic controls first | PR-G |
| P2 | Should the six RC.2 ADRs be accepted before code or proposed within their implementation PRs? | Accept the boundary decision before the corresponding behavior PR; tune weights in tests, not ADRs | PR-B onward |
| P2 | Are RC.2 architecture documents intended as permanent public docs? | Accepted for RC.2: keep `docs/rc2/` discoverable through the candidate; archive/summarize durable decisions into ADRs and user contract docs before GA | PR-H — accepted default |

## Decisions that should not be reopened without new evidence

- Authentication words alone cannot justify a hard login verdict.
- Host-specific exemptions are not the primary repair.
- Hidden internal sidecars cannot consume a public response budget.
- Exact caller token limits cannot be silently expanded to make tests pass.
- A TOC/heading lexical hit is not proof that the requested answer body survived.
- L0 does not gain a hosted semantic-ranking dependency for RC.2.
- Multiple legitimate host profiles mean global process deduplication is unsafe.

## Evidence requested from owners or integrators

1. Two representative MCP clients for the digest union compatibility spike.
2. The supported OS/RID list for RC.2 lifecycle acceptance.
3. Hermes lifecycle/ownership API documentation or source reference, if one exists.
4. Product preference for partial focused output versus typed focus failure.
5. The public compatibility window for legacy semantic fields.
6. Approval or replacement thresholds for classifier recall, budget estimator tolerance, latency, and
   memory in [RC2_VALIDATION_PLAN.md](RC2_VALIDATION_PLAN.md).

Until these answers arrive, PR-A can proceed because it is evidence and contract-spike work. PR-B through
PR-G should not invent public policy beyond the recommended defaults above.
