# ADR-0016 — Capability exam and behaviour-derived tool tiers

**Status:** Accepted (engine); **MCP opt-in** via `OCCAM_EXAM_MCP` (see Consequences)
**Date:** 2026-09-16
**Updated:** 2026-09-18 — MCP submit + list_changed path
**Hypothesis:** [H2 and H3](../research/hypothesis.md)

## Context

Occam advertises fifteen tools. A capable model handles that; a weak one does not fail at *using* a
tool, it fails at *picking* one — reaching for `occam_playbook_heal` when it wanted to read a page,
then never recovering because the next step is chosen from the same confused state.

The repository already had a partial answer: static role profiles (`OCCAM_PROFILE=reader |
researcher | auditor | full`) exposing 8/9/12/15 tools. Static profiles have two gaps. They are set
by the *operator*, who does not know which model is behind the client this session; and they encode
a *role*, not a capability, so a weak model in a research workflow still gets the research surface.

The question is whether a surface sized to measured capability beats one sized to a declared role —
and whether capability can be measured cheaply enough to be worth it.

## Decision

**Four graded tasks, one point each, three tiers, three surfaces.**

| Task | Measures | Passes when |
|---|---|---|
| `canary` | whether the agent reads at all | the reported sentinel adjudicates `READ_VERIFIED` |
| `basic_call` | schema binding | arguments parse to an object with an absolute http(s) `url` |
| `focus_budget` | context-budget awareness | a non-empty `focus_query` plus a plausible `max_tokens` |
| `chain` | multi-step state handling | a later call carries the exact value an earlier one produced |

Score 0–1 → `weak` → `minimal` profile (1 tool). Score 2–3 → `medium` → `basic` (3 tools).
Score 4 → `strong` → `full` (15 tools). Default for a client that never sat the exam is **medium**.

Specific choices and why:

- **The canary task is the anchor.** The other three check form, which a model can satisfy by
  pattern-matching the schema. The canary cannot be satisfied that way: the answer did not exist at
  training time. Reusing ADR-0010's verdict machinery means the exam inherits its distinctions —
  `READ_STALE` fails the task, because "you read this, earlier" is not "you read this when asked".
- **Equal weights.** Weighting tasks requires evidence about their relative predictive value. There
  is none, and a weighted formula would invite tuning that looks like progress.
- **Default medium, not weak.** An unknown client is assumed competent. Starving a capable agent is
  a silent failure the user attributes to the product; over-trusting a weak one produces a visible,
  recoverable mess.
- **Only a perfect score widens to fifteen tools.** Widening is the risky direction, so it has the
  highest bar. Surfaces are also nested, so a promotion never removes a tool the agent already had.
- **Narrow profiles get their own `instructions` text.** This was a bug the tests caught: the shared
  trust-and-default block names `occam_digest` and `occam_client_capabilities`, so a one-tool client
  was being told about tools absent from its `tools/list`. Narrow surfaces now use a trust block
  with no tool menu, and a test asserts the general invariant — *no profile's instructions may name
  a tool that profile does not expose*.
- **Rolling competence with hysteresis.** Scoring continues on real calls (schema-valid arguments,
  caller-attributable failures). Two guards: no score below five observations, and no tier change
  until a suggestion has held for three consecutive observations. The tracker reports its own
  oscillation count, because a surface that flips under a running agent is plausibly worse than one
  that is stably wrong — and that is an outcome to measure, not a knob to hide.
- **A page failure is not the agent's fault.** `http_404` on a well-formed call does not count
  against the caller. Conflating page failures with caller failures would score the web and label
  the result agent competence.
- **The grader takes a record of behaviour, not the agent.** MCP has no general mechanism for a
  server to hand a client an arbitrary task and await a result; `sampling/createMessage` is the
  closest and is optional plus client-gated. So administration is pluggable — a harness, a
  sampling-capable host, or a human can all produce the record — and the engine stays a pure
  function, testable without a model in the loop.

## Consequences

**Implemented and verified.** Grading, tiering, tier → surface mapping, the two narrow profiles with
their own instructions, TTL-bounded result caching keyed by `{clientInfo, modelHint, sessionId}`,
and rolling competence with hysteresis. 163 tests; 96.7 % line and 84.5 % branch coverage on
`OccamMcp.Core.Exam`; `occam exam selftest` runs 36 assertions from the shipped binary on macOS
arm64, Linux x64 and Windows x64.

**Not implemented previously; now opt-in:** dynamic re-advertisement when `OCCAM_EXAM_MCP=1`.
The host registers the full core catalog, filters `tools/list` / `tools/call` through a
session-scoped `SessionToolSurface`, and may send `notifications/tools/list_changed` after
`occam_exam_submit`. Default (flag off) behaviour is unchanged: surface fixed at DI registration;
`listChanged` advertised as false.

**Administration:** still not automatic at initialize (no server-driven agent task loop). Offline
`occam exam grade` remains. With `OCCAM_EXAM_MCP=1`, agents/harnesses POST a behaviour record via
`occam_exam_submit`. A pinned `OCCAM_PROFILE` always wins (exam recommends but does not apply).
Cache miss / never-sat does **not** apply `ExamResult.Default`→`basic` — the session stays at
`reader` until a real grade or an operator pin.

**Still deferred:** rolling `CompetenceTracker` mid-session adjustments; sampling-based administration.

**So what is this worth today?** An engine that is correct, measured and reproducible, plus two new
profiles an operator can use immediately. The research claim it exists to test — H3, that
behavioural tiering beats a model-name lookup — is **unmeasured**, and H3 is the hypothesis most
likely to be refuted. If it is, the right response is to delete `CompetenceTracker`, not to tune the
thresholds.

## Alternatives considered

**Trust the client's self-report.** Rejected as the baseline to beat, not a design: asking a model
to rate its own tool-use ability is the unfalsifiable thing the exam replaces.

**Static map from `clientInfo` / model name.** This is the *other* baseline, and a genuinely strong
one — it is free, stable, and needs no exam. It is what H3 must beat. Rejected as the sole mechanism
because it cannot see a client that changed models behind a stable name, but it is deliberately
retained as the comparison arm in the methodology.

**Grade on quality rather than form.** "Was that a good focus query" is not checkable, and a grader
that pretended otherwise would produce a tier nobody could reason about or appeal.

**One task instead of four.** The canary alone would measure reading and nothing else. Four tasks
that fail for different reasons make the score diagnostic — a 2 tells you *which* two.

**Per-call tier recomputation with no hysteresis.** Rejected: alternating behaviour would flip the
tool surface on every other call. The alternating case is now a test.
