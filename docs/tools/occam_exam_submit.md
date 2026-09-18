# occam_exam_submit (opt-in)

**Enable:** `OCCAM_EXAM_MCP=1`

Grades a capability-exam harness submission (same JSON as `occam exam grade`). Returns
score / tier / recommended profile. When `OCCAM_PROFILE` is **not** set, applies the
tier→profile surface and sends `notifications/tools/list_changed`.

Does **not** fetch the web or administer the four tasks for you — pass a behaviour record
(canary verdict + argument JSON + chain). See [ADR-0016](../adr/0016-capability-exam.md)
and [configuration — Capability tiers](../configuration.md#capability-tiers-experimental).

Pinned `OCCAM_PROFILE` → grade + cache only (`applied: false`).
