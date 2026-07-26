# Wave 2 assignment — remaining CORE MCP tools

**SoT:** executable code only. Docs UNTRUSTED.  
**Do not audit opt-in tools** (batch/watch/crosscheck/atlas) — Wave 3.  
**Reuse Wave-1 CAP IDs** before minting new ones. New IDs only for genuinely new product behavior: **CAP-400+**.

## Core catalog from code (`OccamToolNames`)

| # | Tool | Wave status | Report | New CAP range (if needed) |
|---|------|-------------|--------|---------------------------|
| 1 | `occam_client_capabilities` | Wave 2 | `docs-audit/tools/occam_client_capabilities.md` | CAP-400…419 |
| 2 | `occam_transcode` | **Wave 1 DONE** | `docs-audit/tools/occam_transcode.md` | — |
| 3 | `occam_probe` | Wave 2 | `docs-audit/tools/occam_probe.md` | CAP-420…449 |
| 4 | `occam_digest` | Wave 2 | `docs-audit/tools/occam_digest.md` | CAP-450…489 |
| 5 | `occam_playbook_resolve` | Wave 2 | `docs-audit/tools/occam_playbook_resolve.md` | CAP-490…509 |
| 6 | `occam_map` | Wave 2 | `docs-audit/tools/occam_map.md` | CAP-510…529 |
| 7 | `occam_playbook_heal` | Wave 2 | `docs-audit/tools/occam_playbook_heal.md` | CAP-530…559 |
| 8 | `occam_playbook_save` | Wave 2 | `docs-audit/tools/occam_playbook_save.md` | CAP-560…589 |
| 9 | `occam_extract_knowledge` | Wave 2 | `docs-audit/tools/occam_extract_knowledge.md` | CAP-590…619 |
| 10 | `occam_search` | Wave 2 | `docs-audit/tools/occam_search.md` | CAP-620…649 |
| 11 | `occam_verify` | Wave 2 (S19 partial — still need tool file) | `docs-audit/tools/occam_verify.md` | CAP-650…689 |
| 12 | `occam_claim_check` | Wave 2 | `docs-audit/tools/occam_claim_check.md` | CAP-690…719 |
| 13 | `occam_attest` | Wave 2 | `docs-audit/tools/occam_attest.md` | CAP-720…749 |
| 14 | `occam_playbook_lint` | Wave 2 | `docs-audit/tools/occam_playbook_lint.md` | CAP-750…769 |
| 15 | `occam_dataset_export` | Wave 2 | `docs-audit/tools/occam_dataset_export.md` | CAP-770…799 |

**Remaining agents:** 14 (all except transcode).

## Envelope (return ONLY this)

```
TOOL:
REPORT:
FILES_INSPECTED:
NEW_CAPABILITIES: (IDs + one-liners; prefer empty if only reuse)
EXISTING_CAPABILITIES_USED: (Wave-1 CAP IDs referenced)
CROSS_SUBSYSTEM_EDGES: (short bullets: tool→subsystem/backend/artifact)
HIDDEN_ADVANCED:
UNCERTAINTIES:
COMPLETENESS: COMPLETE | NEEDS SECOND PASS | BLOCKED
```

## Graph contribution

Each report must include a section `## Capability graph edges` listing edges as:

`TOOL|USES|CAP-xxx` / `PARAM|ENABLES|CAP-xxx` / `CAP-xxx|ROUTES_TO|backend` / `CAP-xxx|PRODUCES|receipt` etc.

## Quality

Prefer PRODUCT CAPABILITY over implementation detail. Mark implementation-only notes without minting CAP IDs.
