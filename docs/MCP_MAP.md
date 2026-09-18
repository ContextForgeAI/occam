# MCP map

Canonical tool surface for agents and operators. **Do not hand-count** — source of truth is
`OccamMcpServerRegistration.OccamToolNames` in
[`src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs`](../src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs).

**Core catalog (frozen):** **18** tools.  
**Default profile (`OCCAM_PROFILE=reader`):** **11** of those.  
**Opt-in:** env-gated extras (not in the 18).

## Profiles

| Profile | Tools | Notes |
|---------|------:|-------|
| `minimal` | 1 | Exam Weak — `occam` only |
| `basic` | 3 | Exam Medium — `occam`, `occam_digest`, `occam_search` |
| `reader` (default) | 11 | Day-to-day reads + canary |
| `researcher` | 12 | reader + `occam_claim_check` |
| `auditor` | 15 | researcher + attest / dataset / lint |
| `full` | **18** | Entire core catalog |

## 18 core tools

1. `occam_client_capabilities`
2. `occam`
3. `occam_transcode`
4. `occam_probe`
5. `occam_digest`
6. `occam_playbook_resolve`
7. `occam_map`
8. `occam_playbook_heal`
9. `occam_playbook_save`
10. `occam_extract_knowledge`
11. `occam_search`
12. `occam_verify`
13. `occam_claim_check`
14. `occam_attest`
15. `occam_playbook_lint`
16. `occam_dataset_export`
17. `occam_canary_issue`
18. `occam_canary_verify`

Schemas / narratives: [tools-reference.md](tools-reference.md) · [tools/index.md](tools/index.md).

## Opt-in (not core)

| Tools | Env |
|-------|-----|
| `occam_batch_submit` / `_status` / `_results` | `OCCAM_BATCH_MCP=1` |
| `occam_watch` | `OCCAM_WATCH_MCP=1` |
| `occam_crosscheck` | `OCCAM_CONSENSUS_MCP=1` |
| `occam_failure_atlas` | `OCCAM_ATLAS_MCP=1` |
| `occam_browser_interact` | `OCCAM_BROWSER_ACTIONS_MCP=1` |
| `occam_exam_submit` | `OCCAM_EXAM_MCP=1` |

## Smoke note

`occam smoke` (`hermes-smoke.mjs`) launches the **published** host with `OCCAM_PROFILE=full` and
expects **18** core names (`occam` + `occam_*`). An older AOT binary without canary will fail —
republish (`occam doctor` / refresh) after pulling. Counting only `occam_*` (excluding cascade
`occam`) is wrong and historically produced a false **15**.
