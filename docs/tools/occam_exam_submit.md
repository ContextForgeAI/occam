# `occam_exam_submit`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamExamSubmitTool.cs`, `Exam/ExamMcpRuntime.cs` @ 2026-09-29  
**Profiles:** opt-in — `OCCAM_EXAM_MCP=1`  
**Contract depth:** runtime `tools/list` · harness JSON matches `occam exam grade` (CLI)

## Purpose

Grade a capability-exam harness submission (same JSON as `occam exam grade`). Returns score, tier, recommended profile, and per-task outcomes. When `OCCAM_PROFILE` is **not pinned**, may apply the tier-derived tool surface and send `notifications/tools/list_changed`. Does **not** fetch the web or run the tasks for you.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Harness posts graded behaviour after canary + tool-use tasks | Ordinary URL read → core tools |
| Dynamic `tools/list` by demonstrated competence | Default host with exam flag off |
| Client can re-list after `list_changed` | Expecting surface change while `OCCAM_PROFILE` is pinned |

## Enablement

| Setting | Behaviour |
|---------|-----------|
| `OCCAM_EXAM_MCP=1` | Registers tool; filters list/call by session surface |
| Unpinned profile | Starts at **`reader`**; may move to `minimal` / `basic` / `full` |
| Pinned `OCCAM_PROFILE` | Grade + cache only (`applied: false`) |

Tier → profile when applied: weak → `minimal`, medium → `basic`, strong → `full` (score 0–4).

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `submission` | yes | — | Harness JSON: `clientInfo`, `modelHint`, `sessionId`, `canaryVerdict`, `basicCallArguments`, `focusBudgetArguments`, `chain` (+ optional `selfReportTier`) |

## Response shape

**Success:** `score`, `maxScore`, `tier`, `recommendedProfile`, `applied`, `pinned`, `activeProfile`, `listChangedSent`, `outcomes[]`, …

**Failure:** `failure: { code, message }` — typically `invalid_arguments`. Calls to tools off the exam surface → `tool_not_on_surface`.

## Related env vars

`OCCAM_EXAM_MCP`, `OCCAM_PROFILE` — [configuration](../configuration.md).

## See also

- [experimental](../experimental.md) · [ADR-0016](../adr/0016-capability-exam.md)
- [`occam_canary_issue`](occam_canary_issue.md) · [`occam_canary_verify`](occam_canary_verify.md)
