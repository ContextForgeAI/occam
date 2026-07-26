# CAPABILITY-GRAPH

**Wave 2 baseline + Wave 3 second-surface nodes.**

Machine-readable: `capability-graph.json` (658 nodes, 588 edges; `total_caps` 674).

## Core product paths (code-proven synthesis)

| Tool | Role | Proven path |
|------|------|-------------|
| `occam_transcode` | default page reader | url → session?/proxy? → HTTP → browser → managed? → materialize → opts → Receipt v1 |
| `occam_digest` | multi-URL research | urls|source_url → map? → parallel TranscodePipeline (playbooks/sidecars OFF) → reduced receipts |
| `occam_probe` | cheap diagnose | HTTP-only (no browser/managed/proxy) → extractability + hints |
| `occam_map` | link discovery | HTTP-only sitemap/homepage → rank/filter → suggestedNext=digest |
| `occam_search` | URL discovery | provider API → optional full ProbeService rerank fan-out |
| `occam_extract_knowledge` | typed facts | playbook schema → css-extract → unpooled browser fallback? → facts[] (fake Receipt) |
| `occam_playbook_heal` | recipe evidence | browser-only skeleton → draft for save |
| `occam_playbook_save` | persist recipe | hygiene + optional QualityGate dry-run → local write → unconditional ECDSA sign |
| `occam_playbook_resolve` | lookup recipe | tier overlay → optional genome → self-key signature inspect |
| `occam_playbook_lint` | static schema | offline grade; may disagree with save/resolve parsers |
| `occam_claim_check` | claim blocks | TranscodePipeline (json_blocks forced, playbook auto, NO budget) → BM25 → Merkle citation |
| `occam_attest` | claim status | batch → claim_check → semantic classifier → status+proof |
| `occam_verify` | prove receipts | offline/live/prove/citation/history; live drops session/playbook |
| `occam_dataset_export` | auditable corpus | sequential reduced pipeline → row receipts + manifest (CLI verify) |
| `occam_client_capabilities` | ambient budget | context_tokens → max_tokens/cache identity for transcode+digest |

## High-value cross-tool edges (sample)

- `TOOL` —**USES**→ `CAP-720` (occam_attest)
- `TOOL` —**USES**→ `CAP-721` (occam_attest)
- `TOOL` —**USES**→ `CAP-722` (occam_attest)
- `TOOL` —**USES**→ `CAP-723` (occam_attest)
- `TOOL` —**USES**→ `CAP-724` (occam_attest)
- `TOOL` —**USES**→ `CAP-725` (occam_attest)
- `TOOL` —**USES**→ `CAP-726` (occam_attest)
- `TOOL` —**USES**→ `CAP-727` (occam_attest)
- `TOOL` —**USES**→ `CAP-728` (occam_attest)
- `TOOL` —**USES**→ `CAP-729` (occam_attest)
- `TOOL` —**USES**→ `CAP-730` (occam_attest)
- `TOOL` —**USES**→ `CAP-262` (occam_attest)
- `TOOL` —**USES**→ `CAP-263` (occam_attest)
- `TOOL` —**USES**→ `CAP-278` (occam_attest)
- `TOOL` —**USES**→ `CAP-279` (occam_attest)
- `TOOL` —**USES**→ `CAP-051` (occam_attest)
- `TOOL` —**USES**→ `CAP-068` (occam_attest)
- `TOOL` —**USES**→ `CAP-191` (occam_attest)
- `TOOL` —**USES**→ `CAP-008` (occam_attest, occam_client_capabilities)
- `TOOL` —**USES**→ `CAP-384` (occam_attest)
- `PARAM:claims` —**ENABLES**→ `CAP-727` (occam_attest)
- `PARAM:backend_policy` —**ENABLES**→ `CAP-051` (occam_attest, occam_claim_check)
- `PARAM:session_profile` —**ENABLES**→ `CAP-068` (occam_attest, occam_claim_check, occam_digest)
- `CAP-720` —**ROUTES_TO**→ `occam_claim_check` (occam_attest)
- `CAP-720` —**CONSUMES**→ `ClaimCheckService` (occam_attest)
- `CAP-729` —**CONSUMES**→ `ClaimCheckService` (occam_attest)
- `CAP-730` —**CONSUMES**→ `CAP-263` (occam_attest)
- `CAP-720` —**PRODUCES**→ `OccamAttestResponse` (occam_attest)
- `CAP-726` —**PRODUCES**→ `OccamAttestResponse` (occam_attest)
- `CAP-720` —**PRODUCES**→ `receipt` (occam_attest)
- `CAP-720` —**PRODUCES**→ `merkle_proof` (occam_attest)
- `CAP-262` —**PRODUCES**→ `merkle_proof` (occam_attest)
- `CAP-720` —**ROUTES_TO**→ `http_extract_backend` (occam_attest)
- `CAP-720` —**ROUTES_TO**→ `browser_extract_backend` (occam_attest)
- `CAP-720` —**FALLS_BACK_TO**→ `managed_extract_backend` (occam_attest)
- `CAP-720` —**CONSUMES**→ `session` (occam_attest)
- `CAP-720` —**CONSUMES**→ `playbook_genome` (occam_attest)
- `occam_claim_check` —**ROUTES_TO**→ `TranscodePipeline` (occam_attest)
- `TranscodePipeline` —**ROUTES_TO**→ `OccamRouter` (occam_attest)
- `OccamRouter` —**FALLS_BACK_TO**→ `managed_extract_backend` (occam_attest)
- `TOOL:occam_claim_check` —**USES**→ `CAP-690` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-691` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-692` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-693` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-694` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-695` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-696` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-697` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-698` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-699` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-700` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-701` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-702` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-703` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-051` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-052` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-068` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-069` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-070` (occam_claim_check)
- `TOOL:occam_claim_check` —**USES**→ `CAP-078` (occam_claim_check)

## Tool coverage matrix

| Tool | Wave2 new CAPs | Report |
|------|----------------|--------|
| `occam_attest` | 11 | docs-audit/tools/occam_attest.md |
| `occam_claim_check` | 14 | docs-audit/tools/occam_claim_check.md |
| `occam_client_capabilities` | 5 | docs-audit/tools/occam_client_capabilities.md |
| `occam_dataset_export` | 10 | docs-audit/tools/occam_dataset_export.md |
| `occam_digest` | 11 | docs-audit/tools/occam_digest.md |
| `occam_extract_knowledge` | 13 | docs-audit/tools/occam_extract_knowledge.md |
| `occam_map` | 20 | docs-audit/tools/occam_map.md |
| `occam_playbook_heal` | 25 | docs-audit/tools/occam_playbook_heal.md |
| `occam_playbook_lint` | 14 | docs-audit/tools/occam_playbook_lint.md |
| `occam_playbook_resolve` | 8 | docs-audit/tools/occam_playbook_resolve.md |
| `occam_playbook_save` | 18 | docs-audit/tools/occam_playbook_save.md |
| `occam_probe` | 18 | docs-audit/tools/occam_probe.md |
| `occam_search` | 12 | docs-audit/tools/occam_search.md |
| `occam_transcode` | 0 | docs-audit/tools/occam_transcode.md |
| `occam_verify` | 4 | docs-audit/tools/occam_verify.md |

## Related Wave-2 synthesis

- Profiles: `docs-audit/PROFILE-TOOL-MATRIX.md` (`PROFILE|EXPOSES|TOOL`)
- Artifacts: `docs-audit/ARTIFACT-MAP.md` (`CAP|PRODUCES|ARTIFACT` narrative)
- Workflows: `docs-audit/CODE-DERIVED-WORKFLOWS.md`

## Notes

- Edges extracted from per-tool `## Capability graph edges` sections.
- Wave-1 CAP IDs reused heavily via edges without re-minting.
- Opt-in tools (watch/batch/crosscheck/atlas) deferred to Wave 3.
