# Hypotheses

Occam started as an engineering project and grew a research question. This file states that question
in falsifiable form. Instruments and a few n=1 cells exist; the experiments below are not complete.
Each claim still names the observation that would kill it.

Author context, stated because it shapes the hypotheses: the author is a Unity/C# game developer,
not an ML researcher. The three ideas here are transplanted game-engineering patterns — server-side
validation from anti-cheat, tutorial gating from onboarding design, and level-of-detail from
rendering. Whether those analogies hold under measurement is exactly what is in question.

---

## H1 — A proof-of-read canary reduces fabricated page content

**Claim.** When an agent is asked to report a sentinel embedded in a fetched page, the rate at which
it produces content not present in that page decreases, relative to the same agent fetching the same
page with no sentinel present.

**Mechanism under test.** Two distinct effects are bundled together in the informal version of this
claim, and they must be separated:

- **H1a (detection).** The canary *measures* fabrication. A verdict of `HALLUCINATED` correlates
  with independently judged fabrication in the same response.
- **H1b (deterrence).** The canary *changes* fabrication. Knowing a verifiable token is present
  causes an agent to fall back on the actual payload instead of prior knowledge of the URL.

H1a is a measurement-validity question and is the prerequisite. H1b is the interesting claim and
the harder one, because it predicts a behaviour change on pages where no canary is present.

**Falsified if.** `HALLUCINATED` verdicts show no better than chance agreement with human-judged
fabrication (kills H1a). Or: fabrication rates on non-probe pages are statistically
indistinguishable between the canary-aware and canary-naive conditions (kills H1b).

**Known confounds.**

- *Echo fidelity.* A small model may fail to reproduce a 43-character base64url token verbatim while
  having read the page perfectly. This is scored as a hallucination and inflates the apparent
  fabrication rate. Must be measured separately at `sentinelBytes` of 16 and 32.
- *Attention hijacking.* Asking for a sentinel is an instruction. A model that spends capacity
  locating the token may read the rest of the page *worse*, which would show up as improved canary
  verdicts alongside degraded summary quality.
- *Prompt-content confound.* Any sentinel instruction also lengthens the prompt. The control must
  keep prompt length and structure constant with a decoy instruction.

---

## H2 — Progressive tool disclosure improves task success for weak agents

**Claim.** An agent presented with a single entry point (`occam(url, task, budget)`) completes
web-reading tasks more often than the same agent presented with all fifteen tools, and the effect
size is inversely related to model capability.

**Mechanism under test.** The failure this predicts is *tool selection*, not tool use: a weak model
picks `occam_playbook_heal` when it wanted to read a page, and never recovers. A strong model does
not have that problem, and for it the collapsed surface should be neutral or mildly negative (it
loses the ability to ask for exactly what it wants).

**Falsified if.** Success rate is flat across surface sizes for all model tiers. Or: the interaction
term with capability is absent — that is, disclosure helps or hurts every tier equally — which would
mean the mechanism is something other than selection difficulty.

**Prediction that distinguishes it from "fewer tools are simply better".** The strong tier should
show a small *negative* effect. If collapsing the surface helps everyone monotonically, the finding
is about tool-count and not about capability-matched disclosure, and the LOD analogy is wrong.

**Status.** The graded surfaces and the cascade facade both exist. `OCCAM_PROFILE` accepts six values
exposing 1/3/9/10/13/16 tools (`minimal`, `basic`, `reader`, `researcher`, `auditor`, `full`). The
`minimal` vs `basic` vs `full` contrast is runnable. Single-entry `occam(url, task, budget)` ships
(ADR-0017). Dynamic mid-session `list_changed` still does not (ADR-0016).

A mock selection pipeline (`scripts/research/run-h2-disclosure.mjs`) proves the scorecard wiring.
A live extract arm (`run-h2-live-reads.mjs`, marker `H2_LIVE_READS_OK`) confirms cascade readability
of seed URLs — **not** tool-selection. A strong-agent surface A/B
(`run-h2-surface-ab.mjs`, marker `H2_SURFACE_AB_OK`) runs real `OCCAM_PROFILE` hosts on 24 tasks:
`minimal` 0.71 vs `basic`/`full` 1.0, driven by `missingCapability` on digest/search — matching the
LOD cost for strong agents. **Weak-model irrelevant-tool rates and multi-model Experiment 2 remain
unmeasured.**

---

## H3 — Behavioural competence scoring beats self-report and static profiles

**Claim.** A tier assigned from an agent's observed behaviour (a short capability exam plus rolling
signals from real calls — schema-valid arguments, repeated failure on the same tool, retry patterns)
predicts task success better than either the agent's own declaration of capability or a static map
from `clientInfo`/model name.

**Falsified if.** Behaviour-derived tiers have no greater predictive validity than the static
`clientInfo` map. This is the most likely of the three to fail, and the most useful to know: if a
model-name lookup table is as good, the entire scoring apparatus is unnecessary complexity.

**Secondary question.** Tier stability. If rolling scores oscillate within a session, the tool
surface changes under the agent, which is plausibly worse than a wrong-but-stable tier. Oscillation
rate is a primary outcome, not a diagnostic.

**Status.** The mechanism is implemented and tested: four graded tasks, score → tier, tier → tool
surface, TTL-bounded result caching keyed by `{clientInfo, modelHint, sessionId}`, and rolling
competence with a minimum sample of five observations plus three-observation hysteresis.
`CompetenceTracker` reports its own oscillation count, so the secondary outcome is instrumented
rather than inferred. 96.7 % line coverage; `occam exam selftest` proves 36 assertions from the
shipped binary on three platforms.

**What was missing is the data, and one wire.** The external harness exists
([`exam-harness.md`](exam-harness.md)). One real Composer submission grades to `strong`/`full` while
`StaticModelMap` says `medium` — an assignment disagreement cell, not AUC. Mid-session surface
changes remain unwired (ADR-0016). Multi-model predictive validity is still unmeasured: if the
static map predicts as well as behavioural scoring on real models, delete `CompetenceTracker`
rather than tune thresholds.

---

## What is implemented versus hypothesised

| | Implemented | Verified | Measured against a hypothesis |
|---|---|---|---|
| Canary protocol + 4-state verdicts | ✅ | ✅ 314 tests, 3 platforms | ❌ |
| Canary → hallucination-rate effect (H1) | — | — | ❌ no dataset yet |
| Graded tool surfaces, 1/3/8/9/12/15 tools | ✅ | ✅ tests + gate | ❌ |
| Single-entry cascade (H2) | ✅ | ✅ selftest + live smoke | ❌ no model data |
| Exam grading + tiering + rolling scoring (H3) | ✅ engine | ✅ tests, 3 platforms | ❌ |
| Exam administration harness | ✅ offline grade CLI + mock runners | ✅ `EXAM_HARNESS_SELFTEST_OK`, `H2_PIPELINE_OK`, `H3_PIPELINE_OK` | ⚠ n=1 Composer grade + H3 assignment cell; no AUC |
| Live H2 extract arm | ✅ `run-h2-live-reads.mjs` | ✅ `H2_LIVE_READS_OK` | ❌ not selection / not surface × model |
| H2 surface A/B (strong policy) | ✅ `run-h2-surface-ab.mjs` | ✅ `H2_SURFACE_AB_OK` (24×3) | ⚠ strong only; weak selection unmeasured |
| Exam administration in the MCP request path | ❌ | ❌ | ❌ |

The honest summary: **instruments exist; one Composer exam cell, a live extract arm, and a
strong-agent surface A/B are on disk.** Experiment 2 still needs weak/medium models and ~60 tasks;
Experiment 3 still needs multi-model AUC. Next cheapest: weak-agent protocol or a second model
under the same A/B harness.
