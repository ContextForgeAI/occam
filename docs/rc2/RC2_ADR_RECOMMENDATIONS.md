# RC.2 ADR recommendations

These decisions are durable enough to record as repository ADRs. Exact weights, thresholds, and class
names remain implementation details validated by corpus tests.

## ADR-0005 — One evidence-based access assessment

**Proposed decision:** probe and transcode consume one `AccessAssessment` derived from structured
status, redirect, DOM, route, and usable-content evidence. Authentication prose cannot independently
produce `requires_login`; inconclusive evidence is `unknown`.

**Why an ADR:** this defines ownership of a security- and agent-critical verdict across tools and
prevents future independent classifier drift.

**Consequences:** workers expose bounded DOM signals; post-processors stop reclassifying Markdown;
diagnostics use redacted evidence codes. Some previously hard failures become usable warnings or
`unknown`. Host allowlists remain priors, not correctness mechanisms.

**Alternatives rejected:** phrase tuning, growing domain exemptions, and DOM-password-only detection.

## ADR-0006 — Structure-aware deterministic focus planning

**Proposed decision:** preserve a section/anchor/block index in canonical IR and rank candidates using
exact identifiers/fragments, heading/body coverage, proximity, document-relative rarity, and TOC
penalties. The materialization planner, not the codec, owns answer selection and retention.

**Why an ADR:** this refines the existing parse → canonical IR → planner → codec boundary and determines
where future focus features belong.

**Consequences:** IR grows modestly; workers preserve source structure; fragments become explicit intent;
focus has calibrated `hit|weak|miss` outcomes. Ranking remains local, deterministic, testable, and AOT
safe.

**Alternatives rejected:** substring-weight patches, silent budget growth, and hosted embedding/LLM
ranking in L0.

## ADR-0007 — Public response budget follows serialized projection

**Proposed decision:** separate internal planning inventory from public response inventory. Only fields
actually serialized consume `max_tokens`. The planner protects a minimum answer unit and reports
incompleteness rather than silently exceeding the limit or returning a confident heading-only result.

**Why an ADR:** budget ownership is a public contract and cuts across Markdown, structured sidecars,
media, receipts, and codecs.

**Consequences:** projection must precede response allocation; final serialization is checked against the
same inventory; hidden IR remains available without public cost. Receipts account for their serialized
surface only.

**Alternatives rejected:** fixed Markdown ratios, charging all internal IR, or automatic budget expansion.

## ADR-0008 — MCP unions normalize before domain services

**Proposed decision:** when a public MCP argument legitimately has structured and legacy textual forms,
the published schema represents that union honestly and a boundary normalizer converts it to one domain
type. All user-shape errors are typed tool results, not framework prose.

**Why an ADR:** D12 exposes a repeatable transport-boundary failure class beyond digest. The rule prevents
service parsers from pretending they can handle values the binder rejects first.

**Consequences:** an SDK schema/binding spike is mandatory; explicit tool registration is acceptable when
attribute inference is dishonest. Domain services remain strongly typed. New aliases/tools are not the
default compatibility strategy.

**Alternatives rejected:** breaking array-only parameters, duplicate alias parameters, and versioned tool
proliferation.

## ADR-0009 — Outcome dimensions are not interchangeable

**Proposed decision:** transport completion, extraction usability, access state, focus relevance,
completeness, retrieval, and semantic verdict are separate fields with scoped confidence. Generic legacy
booleans retain their old meanings only for a documented transition.

**Why an ADR:** agents make unsafe decisions when `ok`, `found`, or generic confidence is overloaded.
This is a cross-tool response-design principle.

**Consequences:** payloads grow temporarily; agent hints derive from structured outcomes; recovery traces
explain escalation without rewriting prior attempts. Compatibility aliases need an explicit removal
schedule.

**Alternatives rejected:** documentation-only clarification or silently redefining existing fields.

## ADR-0010 — Host lifecycle is identity-scoped, not globally singleton

**Proposed decision:** launchers forward signals to their exact child; hosts expose instance identity and
operator controls target that identity. Duplicate detection is diagnostic and considers root/config/owner.
No process-name-wide termination or global singleton is introduced.

**Why an ADR:** process ownership crosses Node launchers, Core, workers, operating systems, dashboards,
gateways, and multiple legitimate profiles.

**Consequences:** launcher state transitions and platform process primitives receive explicit tests;
doctor gains read-only instance diagnostics; Hermes hooks remain optional adapters. The parent-side
`prctl` assumption must be corrected.

**Alternatives rejected:** kill-all cleanup, an OS-global mutex, and mandatory Hermes-specific ownership.

## Recommended ADR order

1. ADR-0008 before the digest boundary PR.
2. ADR-0005 before shared classifier implementation.
3. ADR-0006 and ADR-0007 before focus/materialization work.
4. ADR-0009 before public response fields merge.
5. ADR-0010 before mutating lifecycle controls merge.

If the repository's next ADR number differs, preserve this order and renumber; do not overwrite an
existing record. Each accepted ADR should link the corresponding frozen acceptance tests and migration
notes.
