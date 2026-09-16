# Methodology

Pre-registration-style plan for the experiments in [`hypothesis.md`](hypothesis.md). Written before
data collection so the analysis cannot drift toward whatever the first run happens to show.

**Status: pipeline ready; Composer exam cell + strong-agent surface A/B exist — Experiments 2–3 are
not complete.** Mock scorecards under `docs/research/results/` prove wiring only. The Composer /
strong-protocol artefacts are not multi-model Experiment 2/3.

---

## 1. Experiment 1 — canary validity and effect (H1)

### 1.1 Design

Within-subjects, two conditions per (model × page):

| Condition | Prompt |
|---|---|
| **Canary** | fetch the page, summarise it, and report the value of the `occam-sentinel` token |
| **Control** | fetch the page, summarise it, and report the page's `<title>` |

The control asks for an equally specific, equally mechanical extraction so that prompt length and
task structure stay comparable. A no-extra-instruction control would confound "asked for a token"
with "asked for one more thing".

A third condition is needed for H1b (deterrence): **Canary-aware / no-canary-page** — the agent is
told canaries may be present, then reads a page that has none. Fabrication rate here, compared with
a naive agent on the same page, isolates the behaviour change from the measurement.

### 1.2 Models

Five models spanning capability and vendor. Selection criteria, not names, because the specific
models will be whatever is current at run time:

- two frontier models from different vendors;
- one mid-tier model;
- one small open-weights model (~7–8B) run locally;
- one deliberately weak model, to populate the low end of the capability axis.

Each model is run through the same MCP host build, recorded by commit hash.

### 1.3 Pages

Three strata, 40 pages each (n = 120):

| Stratum | Rationale |
|---|---|
| **Well-known** (major docs sites, Wikipedia) | Highest fabrication risk: the model plausibly *does* know the content, so it can answer without reading. |
| **Obscure but clean** (small blogs, personal sites) | Model cannot know the content; isolates extraction quality from recall. |
| **Hostile** (consent walls, SPA shells, challenge pages) | The failure mode that matters in production: the fetch returns an empty shell and the model fills the gap. |

Canary probe pages are served from the local probe host with synthetic bodies matched in length and
structure to each stratum, so that sentinel presence is the only manipulated variable.

### 1.4 Outcomes

**Primary.** Fabrication rate: the proportion of responses containing at least one factual assertion
about the page that is not supported by the served bytes.

**Secondary.** Canary verdict distribution; echo fidelity (verbatim reproduction rate by
`sentinelBytes` ∈ {16, 32}); summary quality on a fixed 1–5 rubric; response latency and token cost.

### 1.5 Fabrication labelling

The weak point of this design, and the part to get right before collecting anything.

- Two independent human raters label a stratified 20 % sample; inter-rater agreement reported as
  Cohen's κ, with a pre-set floor of κ ≥ 0.7 for the labelling scheme to be usable at all.
- The remaining 80 % is labelled by an LLM judge that never sees the condition, prompted only with
  the served bytes and the response.
- The judge is calibrated against the human subsample and its agreement reported. If judge–human
  agreement is below the human–human floor, only the human-labelled subsample is analysed and the
  sample size drops accordingly.
- Raters and judge are blind to condition. This requires stripping the sentinel instruction and the
  sentinel-bearing line from the response before labelling.

### 1.6 Analysis

- Mixed-effects logistic regression: `fabricated ~ condition + stratum + (1|model) + (1|page)`.
  Random intercepts for model and page because both are sampled, not exhausted.
- Pre-registered α = 0.05, two-tailed. Holm correction across the three primary contrasts.
- Effect sizes with 95 % CIs reported regardless of significance; a tight null is a result.
- Power: with 5 models × 120 pages × 2 conditions = 1200 observations, detecting a 10-point
  difference from a 30 % baseline at 80 % power is comfortable. The binding constraint is the human
  labelling budget, not n.

### 1.7 Known threats to validity

| Threat | Handling |
|---|---|
| Echo fidelity confounds hallucination | Report verdicts split by `sentinelBytes`; treat near-miss tokens (edit distance ≤ 2) as a separate category, not as hallucinations |
| Probe pages are synthetic | Report the obscure-but-clean stratum separately as the closest analogue to real pages |
| Model updates mid-experiment | Record model version strings per call; abort and restart a cell if a version changes |
| Prompt sensitivity | Three paraphrases per condition, counterbalanced |
| Asking for a token changes reading behaviour | The canary-aware/no-canary-page condition detects this |

---

## 2. Experiment 2 — progressive disclosure (H2)

Between-conditions on tool surface size, within-subjects on model.

| Condition | Surface | Available |
|---|---|---|
| Minimal | `OCCAM_PROFILE=minimal`, 1 tool | ✅ today |
| Basic | `OCCAM_PROFILE=basic`, 3 tools | ✅ today |
| Reader | `OCCAM_PROFILE=reader`, 9 tools | ✅ today |
| Full | `OCCAM_PROFILE=full`, 16 tools | ✅ today |

All four conditions are runnable on the current build. Task seeds (24 of the planned 60) live in
[`tasks/h2-web-reading.jsonl`](tasks/h2-web-reading.jsonl). Mock runner:
`node scripts/research/run-h2-disclosure.mjs` → `H2_PIPELINE_OK`. Live extract arm:
`node scripts/research/run-h2-live-reads.mjs` → `H2_LIVE_READS_OK`. Strong-agent surface A/B:
`node scripts/research/run-h2-surface-ab.mjs` → `H2_SURFACE_AB_OK` (minimal 0.71 / basic 1.0 /
full 1.0 on 24 tasks). **Weak-model and multi-model Experiment 2 have not been run.**

~~The cascade remains unimplemented and is no longer a prerequisite.~~ Cascade ships (ADR-0017);
surface variation via `OCCAM_PROFILE` remains the primary H2 manipulation.

Tasks: 60 web-reading tasks with programmatically checkable answers (a named value present on the
page), so success needs no human judgement. Primary outcome: task success. Secondary: number of tool
calls to first success, rate of calls to a tool irrelevant to the task, and rate of unrecoverable
loops.

Analysis: `success ~ surface × capability_tier + (1|model) + (1|task)`. The prediction that
distinguishes H2 from "fewer tools are better" is a **negative** coefficient for Minimal in the
strong tier; that interaction is the finding, not the main effect. A monotone improvement as the
surface narrows would mean the result is about tool *count*, not about capability-matched
disclosure, and the level-of-detail analogy would be wrong.

**This is the cheapest first experiment in the whole programme** — four environment-variable values,
60 programmatically checkable tasks, no human labelling, and no new code.

---

## 3. Experiment 3 — competence scoring (H3)

The engine is implemented (grading, tiering, TTL cache, rolling scoring with hysteresis). The
offline harness grades a submission JSON and emits `OCCAM_PROFILE`
([`exam-harness.md`](exam-harness.md)). Mock comparison of the three strategies:
`node scripts/research/run-h3-competence.mjs` → `H3_PIPELINE_OK`.

One Composer grade cell shows behavioural=`strong` vs static map=`medium`
([`results/h3-composer-cell.json`](results/h3-composer-cell.json)). **Predictive validity (AUC) has
not been measured** — until multi-model Experiment 3 runs, H3 remains largely unmeasured.

Compare three tier-assignment strategies against observed task success on the Experiment 2 task set:

1. self-report (ask the model to rate its own tool-use ability);
2. static map from `clientInfo` / model name;
3. behavioural: exam score plus rolling signals.

Outcome: predictive validity of each strategy (AUC for predicting per-task success), plus tier
stability within a session for strategy 3. Strategy 2 is the baseline to beat; if it is not beaten,
H3 is refuted and the scoring apparatus should be deleted rather than tuned.

---

## 4. Reproducibility commitments

- Host commit hash, `dotnet --version` and full environment recorded per run.
- Page corpus frozen as stored HTML, not live URLs, so the dataset is re-runnable after the sites
  change. Live-fetch runs are reported separately and never mixed with frozen-corpus numbers.
- All prompts, raw responses, verdicts and labels published as JSONL alongside the analysis script.
- Negative and null results published. The instrument was built before the hypotheses, which makes
  it cheap to report a null — there is no sunk cost in the finding being interesting.

## 5. What would make these experiments not worth running

Stated up front, because the honest answer to "should this be a paper" may be no:

- If H1a fails, the canary is not a measurement instrument and H1b is unaskable.
- If echo fidelity dominates — if most `HALLUCINATED` verdicts on small models are transcription
  errors — then the protocol measures tokenisation, and the finding belongs in an engineering note,
  not a paper.
- If the Reader-versus-Full contrast in Experiment 2 is flat, H2's mechanism is absent and the
  cascade is not worth implementing.
