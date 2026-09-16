# Exam harness — grade behaviour, recommend a profile

The host **does not** administer the capability exam on the MCP request path (ADR-0016). This sample
is the missing piece: a harness that turns a behaviour record into `OCCAM_PROFILE` for the *next*
process start.

## Grade a submission

```bash
dotnet build src/FFOccamMcp.Core -c Release
dotnet run --project src/FFOccamMcp.Core -c Release --no-build -- exam grade \
  --submission samples/exam-harness/fixtures/perfect-submission.json \
  --out /tmp/grade.json \
  --write-env /tmp/occam-profile.env
```

Or:

```bash
node scripts/research/exam-harness.mjs grade \
  --submission samples/exam-harness/fixtures/weak-submission.json \
  --out /tmp/weak-grade.json
```

`perfect` → `tier=strong`, `OCCAM_PROFILE=full`.  
`weak` → `tier=weak`, `OCCAM_PROFILE=minimal` (even if `selfReportTier` claimed `strong`).

## Pipeline selftests (no model)

```bash
dotnet run --project src/FFOccamMcp.Core -c Release --no-build -- exam harness-selftest
# EXAM_HARNESS_SELFTEST_OK

node scripts/research/run-h2-disclosure.mjs
# H2_PIPELINE_OK

node scripts/research/run-h3-competence.mjs
# H3_PIPELINE_OK
```

These markers prove the **measurement pipeline**, not H2/H3. Real-model cells are still unmeasured.

## Submission schema

See fixtures. Fields:

| Field | Role |
|-------|------|
| `canaryVerdict` | Wire token from canary verify (`READ_VERIFIED` …) |
| `basicCallArguments` | JSON object or string with `url` |
| `focusBudgetArguments` | `task`+`budget` or `focus_query`+`max_tokens` |
| `chain.calls` / `producedValue` | Multi-step carry |
| `selfReportTier` | Optional H3 baseline |
| `modelHint` | Feeds static model-name map (H3 baseline) |

## H2 / H3

- Task corpus: [`docs/research/tasks/h2-web-reading.jsonl`](../../docs/research/tasks/h2-web-reading.jsonl) (12 tasks; methodology targets 60)
- Scorecard runners: `scripts/research/run-h2-disclosure.mjs`, `run-h3-competence.mjs`
- Design: [`docs/research/methodology.md`](../../docs/research/methodology.md) §2–3
