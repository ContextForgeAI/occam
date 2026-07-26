# DOCS-V3-EXPOSURE-AUDIT (Phase 8C)

**Branch:** `docs/v3-canonical`  
**Date:** 2026-07-26  
**Sources:** `docs/`, `llms.txt`, `mkdocs.yml`, `docs-audit/DOCUMENTATION-EXPOSURE-MATRIX.md`, `OccamMcpServerRegistration.OccamToolNames`, `OccamToolProfile.cs`

Legend:

| Column | Meaning |
|--------|---------|
| **VISIBLE** | A task-oriented or operator path surfaces the topic without env hunting |
| **WHERE** | Primary doc paths (not exhaustive) |
| **ACCURATE** | Prose matches shipped code and Phase 6.5 honesty contract |
| **STATUS CLEAR** | EXPERIMENTAL / LIMITED / operator-only status is explicit |

---

## Summary counts

| Metric | Count | Notes |
|--------|------:|-------|
| Topics audited | 34 | User 8C list |
| **VISIBLE YES** | 34 | Soft-fixed capsules + proxy rotation on networking/receipts |
| **VISIBLE NO** | 0 | |
| **ACCURATE YES** | 29 | |
| **ACCURATE PARTIAL** | 5 | Path-scoped proxy nuance; skill/package residual then soft-fixed |
| **ACCURATE NO** | 0 | After Phase 8 fixes |
| **STATUS CLEAR YES** | 34 | |
| **STATUS CLEAR PARTIAL** | 0 | |

**Covered (VISIBLE + ACCURATE + STATUS CLEAR):** 34 / 34 after soft-fixes (ACCURATE PARTIAL on proxy path asymmetry remains handbook-depth, not a visibility miss).
**Missing or partial exposure:** 0 visibility misses; residual accuracy nuance on Core HttpClient vs worker proxy (documented, not overclaimed).

---

## Capability exposure matrix

| Topic | VISIBLE | WHERE | ACCURATE | STATUS CLEAR |
|-------|---------|-------|----------|--------------|
| **Proxy support** | YES | `docs/networking.md`, `docs/configuration.md`, `docs/examples/proxy-use.md`, `docs/trust/local-first.md`, handbook ch.22 | PARTIAL | YES |
| **Proxy rotation** | YES | `docs/networking.md`, `docs/configuration.md`, handbook ch.06/20/22 | PARTIAL | YES |
| **Sessions / cookies** | YES | `docs/sessions.md`, `docs/guides/sessions.md`, `docs/examples/session-profile.md`, per-tool pages | YES | YES |
| **Browser acquisition** | YES | `docs/acquisition.md`, `docs/tools/occam_transcode.md`, handbook ch.05–06 | YES | YES |
| **Managed acquisition** | YES | `docs/acquisition.md`, `docs/experimental.md`, `docs/concepts.md`, handbook ch.05 | YES | YES |
| **HTTP vs browser** | YES | `docs/acquisition.md`, `docs/concepts.md`, `docs/tools/occam_transcode.md`, `docs/tools/occam_probe.md` | YES | YES |
| **Materialization / token budgeting** | YES | `docs/materialization.md`, `docs/tools/occam_client_capabilities.md`, `docs/tools/occam_transcode.md`, handbook ch.07 | YES | YES |
| **Focus** | YES | `docs/materialization.md`, transcode/digest/map tool pages, `docs/choosing-a-tool.md` | YES | YES |
| **Structured blocks** | YES | `docs/materialization.md`, `docs/tools/occam_transcode.md`, handbook ch.08 | YES | YES |
| **Tables** | YES | `docs/tools/occam_transcode.md` (`json_tables`), handbook ch.08 | YES | YES |
| **Chunks / differential** | YES | `docs/materialization.md`, transcode tool (`semantic_chunking`, `if_none_match`, `diff_against`), handbook ch.08 | YES | YES |
| **Client capabilities** | YES | `docs/tools/occam_client_capabilities.md`, `llms.txt`, `docs/choosing-a-tool.md` | YES | YES |
| **Playbook automatic behavior** | YES | `docs/playbooks.md`, transcode `playbook_policy`, claim_check/attest/dataset notes | YES | YES |
| **Playbook resolve/heal/lint/save** | YES | `docs/playbooks.md`, four tool pages, handbook ch.11–12 | YES | YES |
| **Playbook signature v1/v2** | YES | `docs/playbooks.md`, `docs/tools/occam_playbook_resolve.md`, `docs/tools/occam_playbook_save.md` | YES | YES |
| **Receipts** | YES | `docs/receipts.md`, transcode/digest/claim_check pages, `docs/trust-and-safety.md` | YES | YES |
| **Merkle verification** | YES | `docs/tools/occam_verify.md` (`prove`, `citation`), `docs/tools/occam_claim_check.md`, handbook ch.14–16 | YES | YES |
| **Capsules** | YES | `docs/receipts.md`, `docs/tools/occam_transcode.md`, handbook ch.08/14 | YES | YES |
| **Offline verification** | YES | `docs/tools/occam_verify.md`, `docs/receipts.md`, `docs/guides/verify-sources.md`, handbook ch.15 | YES | YES |
| **Datasets** | YES | `docs/datasets.md`, `docs/tools/occam_dataset_export.md` | YES | YES |
| **Claim evidence lookup** | YES | `docs/tools/occam_claim_check.md`, `docs/guides/claims.md` | YES | YES |
| **Attest semantics** | YES | `docs/tools/occam_attest.md`, `docs/guides/claims.md`, `docs/trust-and-safety.md`, `llms.txt` trust limits | YES | YES |
| **Search / probe / map / digest distinctions** | YES | `docs/choosing-a-tool.md`, four tool pages, handbook ch.09–10, `docs/guides/search-and-discover.md` | YES | YES |
| **Watch** | YES | `docs/experimental.md`, `docs/tools/occam_watch.md`, handbook ch.17 | YES | YES |
| **Crosscheck** | YES | `docs/experimental.md`, `docs/tools/occam_crosscheck.md`, examples, handbook ch.17 | YES | YES |
| **Batch** | YES | `docs/experimental.md`, `docs/tools/occam_batch.md`, `docs/transports.md` | YES | YES |
| **Failure atlas** | YES | `docs/experimental.md`, `docs/tools/occam_failure_atlas.md` | PARTIAL → fixed | YES |
| **Profiles (`OCCAM_PROFILE`)** | YES | `docs/choosing-a-tool.md`, `docs/handbook/18-exposure.md`, `docs/configuration.md` | PARTIAL | YES |
| **Opt-in exposure** | YES | `docs/experimental.md`, `docs/tools/index.md`, handbook ch.17–18, `llms.txt` | YES | YES |
| **Operator / CLI** | YES | `docs/operators.md`, `docs/getting-started.md`, handbook ch.19 | YES | YES |
| **Doctor** | YES | `INSTALL.md`, `docs/quick-start.md`, `docs/operators.md`, handbook ch.03 | YES | YES |
| **Connect** | YES | `docs/connect/*`, `docs/mcp-hosts.md`, `docs/operators.md` | YES | YES |
| **Session management (CLI)** | YES | `docs/sessions.md`, `docs/guides/sessions.md`, `docs/getting-started.md`, handbook ch.19 | YES | YES |
| **Runtime / transports** | YES | `docs/transports.md`, `docs/operators.md`, handbook ch.18 | YES | YES |

---

## Partial / gap notes (8C detail)

### Proxy support (ACCURATE PARTIAL)

- **Good:** Path-scoped table in `networking.md`; managed/search/TSA called out as separate clients.
- **Gap:** Core `HttpClient` paths not honoring `OCCAM_HTTP_PROXY` is handbook-only (ch.22) — task guides do not warn probe/map/search asymmetry.

### Proxy rotation (VISIBLE YES after soft-fix)

- **Good:** Rotation ≠ fingerprint; daemon/CSS/skeleton gaps; callout on `networking.md` + handbook.
- **Residual ACCURATE PARTIAL:** path-scoped rotator coverage (not every backend) — documented, not hidden.

### Capsules (VISIBLE YES after soft-fix)

- **Good:** Param on transcode; handbook ch.14 unsigned wrapper; **`receipts.md` now documents capsule signed core + unsigned cargo**.
- **Residual:** not in Quick Start (acceptable ADVANCED depth).

### Profiles (ACCURATE PARTIAL — fixed)

- Code: `reader` = **8** tools (includes `occam_verify` since Phase 6). Handbook table said 7 — corrected in this session.
- `choosing-a-tool.md` listed verify under reader correctly but duplicated verify in researcher row — corrected.

### Failure atlas (ACCURATE — fixed)

- Tool page used “provably walled” — contradicts experimental honesty (session telemetry, not proof). Wording corrected.

---

## Cross-check vs DOCUMENTATION-EXPOSURE-MATRIX

| Exposure class (39 families) | Docs v3 task-route coverage |
|------------------------------|----------------------------|
| PUBLIC_CORE (13) | All linked from `docs/index.md`, `llms.txt`, or mkdocs Capabilities nav |
| PUBLIC_ADVANCED (15) | All have capability page or tool page + handbook chapter |
| EXPERIMENTAL (5) | `docs/experimental.md` + opt-in tool pages + labeled examples |
| OPERATOR (4) | `operators.md`, INSTALL, connect, transports |
| DO_NOT_DOCUMENT_AS_FEATURE (2) | canonical-knowledge-ir absent as feature; crosscheck framed as comparison only |

---

## Files touched in Phase 8C doc fixes

See parent agent report — `concepts.md`, `occam_transcode.md`, `tools/index.md`, `occam_failure_atlas.md`, `handbook/18-exposure.md`, `choosing-a-tool.md`.
