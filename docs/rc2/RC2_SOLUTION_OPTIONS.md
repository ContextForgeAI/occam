# RC.2 solution options

**Status:** architecture recommendation
**Evidence base:** [independent evidence review](CODEX_EVIDENCE_REVIEW.md)

## Decision criteria

Options are compared on correctness, compatibility, Native AOT suitability, latency and memory,
security, observability, migration cost, failure behavior, and testability. The recommendation favors
deterministic local logic and explicit uncertainty over host exceptions or inferred success.

## 1. Digest input contract (D12)

| Option | Benefits | Costs and failure modes | Decision |
|---|---|---|---|
| A. Replace `string` with `string[]` | Clean generated schema and direct binding | Breaks every legacy string caller; framework binding can still fail before typed handling | Reject |
| B. Accept a JSON union at the MCP boundary, then normalize | Preserves native arrays and legacy JSON/newline strings; one validation path; typed errors | Requires an SDK/schema spike; manual schema registration may be needed | **Recommend** |
| C. Keep `urls` and add `url_array` | Low binding risk and additive migration | Two competing inputs, precedence rules, larger public contract, permanent ambiguity | Fallback only |
| D. Add `occam_digest_v2` | Complete isolation | Tool proliferation, discoverability cost, duplicated semantics | Reject |

For B, the boundary type should preserve the incoming JSON value (`JsonElement` or an equivalent SDK
union type). `DigestInputNormalizer` converts it to a canonical URL list before service invocation. It
accepts an array of strings, a JSON-array string, or newline-delimited text and returns a structured
`invalid_arguments` result for every other shape. The contract spike must prove the published
`tools/list` schema before production work starts.

## 2. Access and login classification (D9/D19)

| Option | Correctness and security | Compatibility and operations | Decision |
|---|---|---|---|
| A. Tighten phrases and word boundaries | Removes known substring collisions | Remains prose-driven and page-family fragile; poor diagnostics | Reject as primary fix |
| B. Expand host/path exemptions | Quickly fixes frozen public references | Becomes an allowlist treadmill; attackers can host deceptive UI on trusted domains | Reject |
| C. Require DOM password controls | Stronger than prose and cheap | Misses HTTP/redirect access denial and non-password identity flows | Use as one signal |
| D. Shared multi-signal `AccessClassifier` | One verdict for probe and transcode; explainable; calibrated uncertainty | Requires worker signal contract and compatibility mapping | **Recommend** |

The shared classifier consumes status/redirect facts, final URL, password controls, blocking form or
overlay evidence, and route shape. Authentication prose is topical content and has no authority to
produce a hard login verdict. Public-reference status may lower the prior but never overrides direct
access-control evidence. Output is `open`, `login_likely`, or `unknown`, with confidence, stage, and
non-sensitive evidence codes.

## 3. Focus ranking and fragment intent (D15/D17)

| Option | Benefits | Costs and failure modes | Decision |
|---|---|---|---|
| A. Retune substring weights | Small diff and fast | Still loses numeric IDs, structure, proximity, and document rarity | Reject as sufficient fix |
| B. Build a section index and deterministic ranker | Models anchors, headings, body, TOCs, coverage, and exact identifiers; AOT-safe | More IR metadata and regression surface | **Recommend** |
| C. Use embeddings or an LLM ranker | Better paraphrase recall in some corpora | Network/model dependency, latency, nondeterminism, privacy, AOT and offline failure | Reject for L0 |
| D. Increase focus budgets adaptively | Can mask some misses | Expensive, violates caller intent, and does not distinguish wrong sections | Mitigation only |

The ranker should use exact fragment/anchor and heading matches, phrase proximity, query-term coverage,
document-level rarity, and answer-bearing body evidence. TOC/index sections receive an explicit penalty.
Numeric, dotted, hyphenated, and snake-case identifiers remain atomic query features. A URL fragment is
intent, not a fetch input: Core preserves it, normalizes it against indexed anchors, and reports whether
it resolved.

## 4. Budget ownership and body retention (D10/C10b)

| Option | Benefits | Costs and failure modes | Decision |
|---|---|---|---|
| A. Stop charging hidden sidecars | Restores the documented public budget; minimal conceptual change | Does not alone guarantee answer-bearing body retention | **Required** |
| B. Unified answer-preserving materialization planner | Coordinates selection, minimum evidence body, and truncation | Touches a central path; needs staged metrics and broad regression tests | **Recommend with A** |
| C. Silently exceed or auto-expand `max_tokens` | Often returns more useful text | Breaks caller cost/control contract and hides inability to fit | Reject |
| D. Reserve a fixed Markdown percentage | Predictable | Static ratios waste budget across response shapes and codecs | Reject |

Internal blocks and tables remain available to planning, but whole-response accounting includes only
fields actually serialized. The planner protects the smallest answer-bearing unit (for example, a
definition plus its list or table) when focus is strong. If it cannot fit, the response remains within
budget and reports `incomplete` with a reason and suggested minimum budget; it does not claim success.

## 5. Semantic outcome contract

| Option | Benefits | Costs and failure modes | Decision |
|---|---|---|---|
| A. Clarify prose documentation only | No compatibility risk | Agents continue to infer semantics from overloaded field names | Reject |
| B. Change existing booleans in place | Small payload | Breaking and still collapses multiple dimensions | Reject |
| C. Add dimensioned outcome fields; deprecate aliases | Honest migration and machine-readable decisions | Temporary payload duplication | **Recommend** |

Separate `transportOk`, `usable`, `focusStatus`, `completeness`, and semantic `verdict`. Recovery entries
also carry `failureCode` and `escalationReason`. Existing `ok`, `found`, and `focusMatched` aliases remain
for a documented RC transition only; they must not be redefined silently.

## 6. Host lifecycle and Hermes integration

| Option | Benefits | Costs and failure modes | Decision |
|---|---|---|---|
| A. Kill every duplicate Occam process | Simple operator story | Can terminate valid profiles or other users; unsafe | Reject |
| B. OS-global singleton lock | Prevents duplicates | Incompatible with multiple roots/configurations and parallel clients | Reject |
| C. Instance identity, signal-safe launcher, targeted control | Safe diagnosis and ownership-aware cleanup | Requires launcher and operator-surface work | **Recommend** |
| D. Hermes-only lifecycle hook | Best integration if available | Vendor coupling and currently unconfirmed API | Optional adapter |

The launcher should forward termination signals and wait for the exact child. Each host exposes
read-only identity: PID, parent PID, resolved root, version, start time, transport, and owner label.
Doctor/control commands detect overlapping instances and stop only an explicitly selected identity.

## Recommended integrated architecture

Adopt B/D/B/A+B/C/C above as one coherent design: normalize inputs at the MCP boundary, classify access
once from structured evidence, index document structure, rank focus deterministically, materialize only
answer-bearing public fields, and expose dimensioned outcomes. These changes extend the existing
parse → canonical IR → materialization planner → codec boundary; they do not add a new tool, hosted
model, file cache, or codec mode.

## Weak fixes explicitly rejected

- Adding httpwg, datatracker, OpenID, or oauth.net to an exemption list.
- Removing only the phrase `authentication required` while leaving prose authoritative.
- Raising default token budgets until the frozen cases happen to pass.
- Treating a heading/TOC lexical hit as focus success.
- Charging internal IR as though it were serialized output.
- Reinterpreting `ok` or `found` without a compatibility field.
- Automatically killing all processes named `FFOccamMcp.Core`.
