# Research results

Honest inventory of artefacts under this directory. Mock rows are never mixed with live or
agent rows in analysis.

## Mock pipelines (wiring only)

| Artefact | Marker | Claim |
|----------|--------|-------|
| `h2-mock.jsonl` + `h2-mock-summary.json` | `H2_PIPELINE_OK` | Deterministic selection model |
| `h3-mock.jsonl` + `h3-mock-summary.json` | `H3_PIPELINE_OK` | Mock strategy comparison |

```bash
node scripts/research/run-h2-disclosure.mjs
node scripts/research/run-h3-competence.mjs
```

## Live extract arm (not tool-selection)

| Artefact | Marker | Claim |
|----------|--------|-------|
| `h2-live-reads.jsonl` + `h2-live-reads-summary.json` | `H2_LIVE_READS_OK` | Cascade extract + `must_contain` on `needs=read` seeds |

```bash
node scripts/research/run-h2-live-reads.mjs
```

## H2 surface A/B (strong-agent protocol)

| Artefact | Marker | Claim |
|----------|--------|-------|
| `h2-surface-ab.jsonl` + `h2-surface-ab-summary.json` | `H2_SURFACE_AB_OK` | Real `OCCAM_PROFILE` MCP hosts; strong selector; 24 tasks × minimal/basic/full |
| `h2-surface-ab-smoke.jsonl` | smoke | 3-task sanity (read/digest/search) |

```bash
node scripts/research/run-h2-surface-ab.mjs
```

**What this measures:** for a strong preferred-tool policy, `minimal` loses digest/search tasks
(`missingCapability`) so success is below `basic`/`full`. **What it does not measure:** weak-model
irrelevant-tool rates, multi-model Experiment 2, or mid-session `list_changed`.

Corpus: [`../tasks/h2-web-reading.jsonl`](../tasks/h2-web-reading.jsonl) (24 seeds; methodology target 60).

## Real-agent cells (n=1 Composer session, 2026-09-16)

| Artefact | What it is | What it is not |
|----------|------------|----------------|
| `exam-composer-submission.json` | Behaviour record with live canary `READ_VERIFIED` + real `contentHash` chain | Multi-model exam |
| `exam-composer-grade.json` / `exam-composer-profile.env` | `EXAM_GRADE_OK` → score=4, tier=`strong`, `OCCAM_PROFILE=full` | Mid-session surface change |
| `h2-composer-selection.json` | 3 selection+extract trials under the IDE MCP surface | Controlled profile A/B (see `h2-surface-ab` instead) |
| `h3-composer-cell.json` | Assignment cell: self-report=`strong`, behavioural=`strong`, static map=`medium` | Predictive AUC / Experiment 3 |

Details: [exam-harness.md](../exam-harness.md).
