# ADR-0006: structure-aware deterministic focus

Status: accepted for RC.2 PR-D on 2026-07-22.

## Context

Flat Markdown scoring dropped numeric identifiers, let repeated terms and TOC entries displace answer
sections, and ignored URL fragments. Adding more lexical weights would preserve the same ownership error:
focus selection needs document structure and stable identity.

## Decision

Core builds a compact `SectionIndex` over the existing Markdown surface at the materialization seam. An
entry records ordinal, heading level and parent, normalized heading, explicit or deterministic synthetic
anchors, source span, body, link density, and an index-like flag. This does not replace canonical IR.

The local ranker preserves numeric, dotted, snake-case, and hyphenated identifiers. Priority is:

1. exact decoded URL fragment;
2. exact anchor/normalized identity;
3. exact heading and heading-term coverage;
4. nearby body phrase/term evidence;
5. answer-bearing body evidence;
6. deterministic document ordinal.

Index/TOC-like entries receive a strong penalty. Every candidate has an observable score and stable reason
codes. Exact fragments are stripped from the network request and retained as local intent. A missing
fragment is an internal miss; absent dedicated frozen evidence, PR-D does not invent a public fallback
contract.

## Consequences

- INV-4, INV-5, and INV-6 have one production owner.
- Existing `focus_query` behavior is strengthened additively; no MCP field is removed.
- Fragment-only focus affects constrained selection without becoming a new public parameter.
- Budget protection and completeness remain planner work in PR-E; public focus dimensions remain PR-F.
- Index construction is linear in the Markdown surface plus local candidate ranking; no network, LLM,
  embedding, or site allowlist is introduced.

## Rejected alternatives

- More flat-text keyword weights: cannot model anchor identity or TOC/body roles.
- Hosted embeddings/LLM ranking: non-deterministic, network-dependent, and outside L0.
- Replacing canonical IR: unnecessary for the approved lightweight seam and too broad for PR-D.
- Treating unresolved fragments as hard failures: unsupported by frozen D17 evidence.

## Evidence

See the [SectionIndex design](SECTION_INDEX_DESIGN.md) and
[validation report](PR_D_VALIDATION_REPORT.md).
