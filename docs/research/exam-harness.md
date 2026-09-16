# Exam harness and H2/H3 scorecards

## Exam harness (administration)

MCP cannot push an arbitrary task to the client and await a result (ADR-0016). Administration is
therefore offline:

1. Publish the catalogue: `occam exam tasks --out tasks.json`
2. A harness / human / sampling-capable host produces a **submission** JSON (behaviour record)
3. Grade: `occam exam grade --submission S.json --out grade.json --write-env profile.env`
4. Operator starts the next MCP process with `OCCAM_PROFILE` from that env file

Markers:

| Command | Marker |
|---------|--------|
| `exam harness-selftest` | `EXAM_HARNESS_SELFTEST_OK` |
| `exam grade --out …` | `EXAM_GRADE_OK` |

Sample + fixtures: [`samples/exam-harness/`](../../samples/exam-harness/).

### Real-agent grade (n=1)

One Composer session produced [`results/exam-composer-submission.json`](results/exam-composer-submission.json)
with a live canary `READ_VERIFIED` and a real `if_none_match` chain hash. Grade:
score **4/4**, tier **`strong`**, recommended `OCCAM_PROFILE=full`
([`results/exam-composer-grade.json`](results/exam-composer-grade.json)).

`StaticModelMap` assigned **`medium`** for `composer-agent` (no named entry) — a real H3
*assignment disagreement* datapoint, not predictive validity.

## H2 — progressive disclosure

| Mode | What it is | Marker / artefact |
|------|------------|-------------------|
| Mock pipeline | Deterministic selection-failure model × tasks × 4 surfaces × 3 capabilities | `H2_PIPELINE_OK`, `results/h2-mock.jsonl` |
| Live extract | Real `cascade run` for `needs=read` tasks (extract + `must_contain` only) | `H2_LIVE_READS_OK`, `results/h2-live-reads.jsonl` |
| Surface A/B | Real MCP host per `OCCAM_PROFILE`; strong preferred-tool policy | `H2_SURFACE_AB_OK`, `results/h2-surface-ab.jsonl` |
| Agent selection cell | Composer chose tools for 3 tasks (IDE surface) | `results/h2-composer-selection.json` |
| Weak / multi-model × surfaces | Methodology §2 | **Not run** |

### Surface A/B snapshot (strong policy, 24 tasks)

| Surface | Tools listed | Success | Missing capability |
|---------|--------------|---------|--------------------|
| `minimal` | 1 | 17/24 (0.71) | 7 (digest/search) |
| `basic` | 3 | 24/24 (1.00) | 0 |
| `full` | 16 | 24/24 (1.00) | 0 |

`strongMinimalBelowFull=true` — for a strong selector the cost of `minimal` is missing digest/search,
not irrelevant-tool picks. Weak-model selection rates remain unmeasured.

Mock expectations (pipeline selftest, not a scientific result): weak success rises when the surface
shrinks to `minimal`; strong success on `minimal` is *lower* than on `full` because digest/search
tasks are impossible — the LOD distinguishing prediction. The strong-agent A/B above matches that
half of the prediction.

## H3 — behavioural vs self-report vs static map

| Strategy | Source |
|----------|--------|
| Self-report | `selfReportTier` on the submission |
| Static map | `StaticModelMap` over `modelHint` (emitted on every grade) |
| Behavioural | Exam score → tier → profile |

Mock runner: `node scripts/research/run-h3-competence.mjs` → `H3_PIPELINE_OK`.  
Real cell: [`results/h3-composer-cell.json`](results/h3-composer-cell.json) (n=1 Composer + strong A/B rates).  
**No real-model predictive validity (AUC) has been measured.** On this one strong cell, assigning
`basic` (static) vs `full` (behavioural) both yield successRate 1.0 — no discrimination yet.

## Expanding the task set

Methodology asks for 60 checkable tasks. The checked-in corpus has **24** seeds under
`docs/research/tasks/h2-web-reading.jsonl`. Add rows with `must_contain` / `needs` before claiming
Experiment 2 power.
