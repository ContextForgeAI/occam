# RC.2 implementation status

Starting commit for this controlled sequence: `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`.
No commits are created by the sequence unless the owner explicitly requests them.

## Stage summary

| Stage | Scope | Owned red tests | Status | Stop gate |
|---|---|---|---|---|
| PR-A | Characterization and regression baseline | 12 future-stage regressions | Complete input | 32/32 characterization green; 4/4 spikes complete; 12/12 expected red |
| PR-B | Digest MCP boundary | D12 schema and typed-boundary cases | Complete | Pass: focused 13/13; characterization 32/32; unit, fast, docs, AOT green |
| PR-C | Unified access classification | D9, D19 | Complete | Pass: focused 22/22 cumulative with PR-B; characterization 32/32; unit, fast, docs, AOT green |
| PR-D | SectionIndex and structural focus | D15, supported D17 | Complete | Pass: focused 34/34; characterization 32/32; unit, fast, docs, AOT green |
| PR-E | Projection-first budget | D10, D11, C10b | Complete | Pass: focused 43/43; characterization 32/32; unit, fast, full L0, docs, AOT green |
| PR-F | Semantic result contract | Semantic-honesty cases | Complete | Pass: focused 54/54; cumulative regression 15/15; characterization 32/32; unit, fast, full L0, docs, AOT green |
| PR-G | Identity-scoped lifecycle | D3 | Complete | Pass: focused 61/61; characterization 32/32; cumulative regression green; unit, fast, full L0, docs, AOT green |
| PR-H | Integration and candidate preparation | All original regressions | Complete (local) | Pass local: focused PR-B…G green; regression 22/22; characterization 32/32; unit/fast/full L0; docs; soak 0 failures; win-x64 AOT recorded; linux/osx AOT pending native OS; remote packs prepared not executed |

## PR-B working record

- Starting worktree: owner-provided PR-A inputs were untracked under `benchmarks/rc2-regression/`,
  `docs/rc2/`, and `validation/`; `.gitignore` was modified. These inputs are preserved.
- Production scope: digest transport binding, normalization, schema publication, and the hand-off of
  canonical `DigestUrlEntry` values to `DigestService`.
- Invariant: INV-3.
- Compatibility: native string arrays are preferred. Legacy strings remain accepted but deprecated.
- Performance boundary: normalization is deterministic, bounded to 256 entries and 65,536 input
  characters, and performs no network work.
- Ending commit: unchanged; the worktree remains uncommitted by owner instruction.
- Detailed changed-file inventory, commands, results, limitations, and final stop-gate disposition are
  recorded in the PR-B implementation and validation reports.
- Green tests: all five D12 desired-boundary assertions (the two original PR-A reds plus mixed,
  nested, and malformed compatibility checks).
- Still red: 10 assertions owned by PR-C through PR-F; their failure reasons are unchanged.
- Commands: focused `--pr-b`, characterization, cumulative `--regression`, existing `--unit-only`,
  `run-l0-fast.ps1`, `check-docs.mjs`, `git diff --check`, and isolated win-x64 Native AOT publish.
- Stop gate: pass. PR-C may begin as a separate worktree stage.

## PR-C working record

- Production scope: shared access evidence, classification, probe/transcode adapters, worker evidence
  plumbing, and removal of the path-only transcode prefetch hard stop.
- Invariant: INV-1. Authentication terminology and a login-like requested path are non-decisive.
- Hard verdicts: HTTP 401/authentication challenge, redirect to a login route, or blocking identity UI
  without usable public content.
- Privacy boundary: workers return bounded booleans only; assessment diagnostics contain stable codes,
  never page text or field values.
- Compatibility: existing public `likelyLoginRequired` and `requires_login` shapes remain; the broader
  public semantic dimensions are deferred to PR-F.
- Green tests: D9 and D19 desired assertions, shared-classifier production units, worker selftest, and
  all PR-B D12 checks.
- Still red: eight assertions owned by PR-D through PR-F; their failure reasons are unchanged.
- Commands: focused `--pr-c`, characterization, cumulative `--regression`, existing `--unit-only`,
  worker syntax/selftest, `run-l0-fast.ps1`, `check-docs.mjs`, `git diff --check`, and isolated win-x64
  Native AOT publish.
- Starting and ending commit: unchanged; the worktree remains uncommitted by owner instruction.
- Detailed design and evidence: [ADR-0005](pr-c/ADR-0005-UNIFIED-ACCESS-CLASSIFICATION.md),
  [access evidence model](pr-c/ACCESS_EVIDENCE_MODEL.md), [implementation report](pr-c/PR_C_IMPLEMENTATION_REPORT.md),
  and [validation report](pr-c/PR_C_VALIDATION_REPORT.md).
- Stop gate: pass. PR-D may begin as a separate worktree stage.

## PR-D working record

- Production scope: compact structural section index/ranker, exact fragment intent, and integration at
  the existing TokenBudget/MaterializationPlanner seam. Canonical IR was not replaced.
- Invariants: INV-4, INV-5, and INV-6.
- Determinism: explicit anchors win; synthetic anchors use stable document-order suffixes; final ties use
  ordinal order. Score traces contain stable reason codes.
- Compatibility: no public parameter or field was removed. Existing `focus_query` gains structural
  behavior; URL fragments are fetched without the fragment and used locally as intent.
- Green tests: D15 numeric and wrong-section cases, exact supported D17 fragment, structural D11 TOC/body
  case, production SectionIndex units, and cumulative PR-B/PR-C checks.
- Still red: two D10 projection/answer-unit assertions and the PR-F semantic-attempt assertion. C10b's
  fixture now retains its body structurally, but explicit completeness remains PR-E/PR-F work.
- Performance observation: a 56,576-character / 1,600-section synthetic document indexed in 2.853 ms
  with 3,196,312 allocated bytes on the recorded local run; no network or hosted model is involved.
- Commands: focused `--pr-d`, frozen characterization, cumulative `--regression`, existing `--unit-only`,
  `run-l0-fast.ps1`, `check-docs.mjs`, `git diff --check`, and isolated win-x64 Native AOT publish.
- Starting and ending commit: unchanged; the worktree remains uncommitted by owner instruction.
- Detailed design and evidence: [ADR-0006](pr-d/ADR-0006-STRUCTURE-AWARE-FOCUS.md), [SectionIndex design](pr-d/SECTION_INDEX_DESIGN.md), [implementation report](pr-d/PR_D_IMPLEMENTATION_REPORT.md), and [validation report](pr-d/PR_D_VALIDATION_REPORT.md).
- Known evidence gap: no dedicated frozen D17 archive establishes missing-fragment public semantics; PR-D exposes an internal miss and does not invent a public contract.
- Stop gate: pass. PR-E may begin as a separate worktree stage.

## PR-E working record

- Production scope: explicit public sidecar projection before allocation, minimum answer-unit retention,
  and internal focus/completeness/budget diagnostics for PR-F.
- Invariants: INV-7 and INV-8; planner/codec ownership remains unchanged.
- Projection: raw extraction inventory is unchargeable until `ResponseProjection` applies request flags.
  Hidden blocks/tables/chunks/feed/screenshots receive zero allocation; default media and requested
  sidecars remain chargeable.
- Answer policy: selected heading, minimum explanatory prose, and coupled list/table/code evidence are
  protected before optional context. The planner never enlarges `max_tokens`.
- Internal truth: retained constrained focus is `Partial/context_truncated`; found focus without its body is
  `Incomplete/focus_body_truncated` with a bounded suggested minimum. Public mapping remains PR-F work.
- Green tests: all D10, D11, and C10b desired assertions; focused cumulative 43/43; frozen characterization
  32/32; cumulative regression 14/15 with only the PR-F semantic-attempt assertion red.
- Calibration: requested 700, estimated allocation 457, serialized projection 439, tolerance 21; the
  128-token answer fixture estimated 126 and retained its answer list.
- Performance: 200 projection plus answer-planning iterations took 19.753 ms with zero planner retries.
- Full-gate follow-up: restored the already-computed HTTP worker `access` field to its success payload after
  L9 `golden-login` exposed the missing PR-C wiring. The repeated full L0 gate passed unchanged fixtures.
- AOT: win-x64 size 38,320,640 bytes; SHA-256
  `7d6790b1335860098aba6416a1c30d3f1d49df87fe9675b02f397f4bf8a75a54`.
- Commands: focused `--pr-e`, cumulative `--regression`, frozen `--characterization`, worker syntax/selftest,
  PR-C focused, unit, fast, full L0, docs, diff/frozen/temp audits, and isolated Native AOT publish.
- Starting and ending commit: unchanged; the worktree remains uncommitted by owner instruction.
- Detailed evidence: [ADR-0007](pr-e/ADR-0007-PROJECTION-FIRST-BUDGET.md), [accounting model](pr-e/BUDGET_ACCOUNTING_MODEL.md), [implementation report](pr-e/PR_E_IMPLEMENTATION_REPORT.md), and [validation report](pr-e/PR_E_VALIDATION_REPORT.md).
- Stop gate: pass. PR-F may begin as a separate worktree stage.

## PR-F working record

- Production scope: additive public semantic envelope for transport, usability, access, focus,
  completeness, and verdict; recovery attempt dimension split; claim retrieval/verdict distinction;
  agent-hint derivation from structured fields.
- Invariant: INV-9.
- Compatibility: legacy `ok`, `confidence`, `focusMatched`, and `found` retain their previous meanings.
  Additive fields and aliases are documented in `pr-f/RC2_SEMANTIC_CONTRACT.md` and
  `pr-f/LEGACY_FIELD_MIGRATION.md`.
- Green tests: SEMANTIC transport/usability/completeness/claim assertions; focused cumulative 54/54;
  frozen characterization 32/32; cumulative expected-red suite 15/15 fully green.
- Still red: none owned by PR-F. Lifecycle identity remains PR-G.
- Commands: focused `--pr-f`, cumulative `--regression`, frozen `--characterization`, unit, fast, full L0,
  docs, diff/temp audits, and isolated win-x64 Native AOT publish.
- AOT: win-x64 size 38,458,880 bytes; SHA-256
  `84fa0c32670c8fde6a017de46f7ed253532804ed71082bed5b0969410aed5694`.
- Starting and ending commit: unchanged; the worktree remains uncommitted by owner instruction.
- Detailed evidence: [semantic contract](pr-f/RC2_SEMANTIC_CONTRACT.md),
  [legacy migration](pr-f/LEGACY_FIELD_MIGRATION.md),
  [implementation report](pr-f/PR_F_IMPLEMENTATION_REPORT.md), and
  [validation report](pr-f/PR_F_VALIDATION_REPORT.md).
- Stop gate: pass. PR-G must not begin until the owner explicitly requests it.

## PR-G working record

- Production scope: identity-scoped lifecycle diagnostics and targeted shutdown planning only.
  No Hermes external API was invented; adapter boundary + internal model + CLI diagnose/self.
- Invariant: INV-10.
- Compatibility: no MCP response fields removed; launcher stamps identity env vars and forwards signals
  to the exact child; parent-side `prctl` child-PID myth removed from `WorkerProcessGroup.Attach`.
- Green tests: D3 production descriptor, dual-tree targeted stop, exact-target rejection, overlap
  diagnose-only, no global singleton; focused cumulative 61/61; characterization 32/32.
- Still red: none owned by PR-G. Integration/soak/release remains PR-H.
- Commands: focused `--pr-g`, cumulative `--regression`, frozen `--characterization`, unit, fast, full L0,
  docs, diff/temp audits, and isolated win-x64 Native AOT publish.
- AOT: win-x64 size 38,630,400 bytes; SHA-256
  `184d6e7ce8024339eb560f7af91bb3860174c75725712b19b59c1d73202fdaff`.
- Starting and ending commit: unchanged; the worktree remains uncommitted by owner instruction.
- Detailed evidence: [lifecycle model](pr-g/LIFECYCLE_IDENTITY_MODEL.md),
  [Hermes notes](pr-g/HERMES_LIFECYCLE_NOTES.md),
  [implementation report](pr-g/PR_G_IMPLEMENTATION_REPORT.md), and
  [validation report](pr-g/PR_G_VALIDATION_REPORT.md).
- Stop gate: pass. PR-H must not begin until the owner explicitly requests it.

## PR-H working record

- Production scope: none. PR-H is integration validation, soak, release-candidate documentation, and
  remote validation packaging only.
- Invariants: revalidated INV-1…INV-10 via focused and cumulative suites; no new architecture.
- Green tests: PR-B 13/13, PR-C 22/22, PR-D 34/34, PR-E 43/43, PR-F 54/54, PR-G 61/61; cumulative
  regression 22/22; characterization 32/32; unit `L0_GATE_OK`; fast `L0_GATE_FAST_OK`; full
  `L0_GATE_OK`; docs OK; soak 3/3 iterations with 0 failures.
- AOT: win-x64 size 38,630,400 bytes; SHA-256
  `184d6e7ce8024339eb560f7af91bb3860174c75725712b19b59c1d73202fdaff`. linux-x64 and osx-arm64 were not
  built on Windows (`Cross-OS native compilation is not supported`).
- Remote: macOS ARM64 and Linux x64/Hermes packages prepared under `docs/rc2/pr-h/` and
  `scripts/rc2-remote-*.sh`; not executed on remote hardware in this session.
- Starting and ending commit: unchanged; the worktree remains uncommitted by owner instruction.
- Detailed evidence: [implementation report](pr-h/PR_H_IMPLEMENTATION_REPORT.md),
  [validation report](pr-h/PR_H_VALIDATION_REPORT.md),
  [integration matrix](pr-h/RC2_INTEGRATION_MATRIX.md),
  [soak report](pr-h/RC2_SOAK_REPORT.md),
  [release artifacts](pr-h/RC2_RELEASE_ARTIFACTS.md),
  [final owner report](pr-h/RC2_FINAL_OWNER_REPORT.md).
- Stop gate: pass (local). Recommended next action is owner review, then remote RID validation, then
  the first consolidated commit on explicit request; do not tag/publish RC.2 until remote packs are green.
