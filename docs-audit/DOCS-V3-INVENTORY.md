# DOCS-V3-INVENTORY

**Branch:** `docs/v3-canonical` @ Phase 8A  
**Date:** 2026-07-26  
**Scope:** public `docs/**/*.md` (excluding `maintenance/`, `development/`, `rc2/` build artifacts), plus root README/INSTALL/llms when referenced from the site.

## Counts

| Class | Count |
|-------|------:|
| ONBOARDING | 8 |
| TASK GUIDE | 8 |
| CAPABILITY | 6 |
| TRUST | 9 |
| OPERATOR | 8 |
| REFERENCE | 28 |
| EXAMPLE | 14 |
| HANDBOOK | 29 |
| DEVELOPER | 5 |
| EXPERIMENTAL | 5 (dedicated + labeled examples/tools) |
| **Public md pages (excl. excluded)** | **~110** |

`friend-test.md` exists under `docs/` but is **excluded from MkDocs nav/site** (review artifact).

## Classification (primary class per page)

### ONBOARDING
`index.md`, `quick-start.md`, `what-is-occam.md`, `how-occam-works.md`, `getting-started.md`, `install.md`, `ask-ai.md`, root `README.md` / `INSTALL.md`

### TASK GUIDE
`choosing-a-tool.md`, `guides/read-a-page.md`, `guides/research-multiple.md`, `guides/search-and-discover.md`, `guides/structured-extraction.md`, `guides/verify-sources.md`, `guides/claims.md`, `guides/sessions.md`

### CAPABILITY
`acquisition.md`, `materialization.md`, `networking.md`, `sessions.md`, `playbooks.md`, `datasets.md`

### TRUST
`trust-and-safety.md`, `trust/local-first.md`, `trust/honest-failures.md`, `trust/installation-safety.md`, `trust/security-policy.md`, `receipts.md`, `receipt_verification.md`, (+ playbooks/datasets also trust-adjacent)

### OPERATOR
`operators.md`, `mcp-hosts.md`, `connect/*` (6), `troubleshooting.md` (shared)

### REFERENCE
`concepts.md`, `configuration.md`, `failure-codes.md`, `faq.md`, `transports.md`, `tools-reference.md`, `tools/index.md`, `tools/occam_*.md` (15 core + 4 opt-in), `reference/mcp-api.md`, `recipes.md`

### EXAMPLE
`examples/index.md` + 13 example pages (incl. watch/crosscheck experimental, proxy, difficult-js)

### HANDBOOK
`handbook/index.md`, `01`–`27`, `appendix-status-labels.md` (**27/27 + appendix + index**)

### DEVELOPER
`developers/contributing.md`, `developers/vision.md`, `architecture/semantic-contract.md`, `quality-baseline.md`, `roadmap.md`

### EXPERIMENTAL
`experimental.md`, `examples/watch-experimental.md`, `examples/crosscheck-experimental.md`, `tools/occam_watch.md`, `tools/occam_batch.md`, `tools/occam_crosscheck.md`, `tools/occam_failure_atlas.md`

## MkDocs nav coverage

All handbook chapters 01–27 + appendix are in `mkdocs.yml` nav.  
Capabilities, Operators, Experimental, Trust, Connect, Guides, Examples present.

**Excluded from site (intentional):** `friend-test.md`, `requirements.txt`, `hooks.py`, `stylesheets/**`, `rc2/**`, `maintenance/**`, `development/**`.

## Orphans / duplicates / contradictions

| Finding | Severity | Disposition |
|---------|----------|-------------|
| `docs/friend-test.md` not in nav | intentional | Review-only; linked from `index.md` “Review artifacts” |
| `concepts.md` vs capability pages overlap | low | KEEP both; concepts = cross-cut, capability = focused |
| `getting-started.md` vs `quick-start.md` | low | Quick Start = short path; Getting Started = longer first-read |
| `install.md` vs root `INSTALL.md` | low | Site points to install.md; root INSTALL is operator SoT |
| Stale `docs/rc2/**` | n/a | Excluded from nav |
| Package README / skill “14 tools” | medium | Outside MkDocs; noted in honesty audit — fix separately if shipping those packages as user-facing |

## Stale Docs v2 survivors

Task guides, connect suite, and tool pages from Docs v2 remain as **editorial base** and were honesty-updated in Phases 7–8. No known dead-capability feature pages (`canonical-knowledge-ir` not featured).

## Dead / non-feature references

| Item | Status in Docs v3 |
|------|-------------------|
| `canonical-knowledge-ir` | Not featured; handbook may mention build-and-discard |
| Sanitizer as live Core feature | Not claimed as live |
| Consensus proof | Explicitly forbidden / negated |
| npm GA / Cosign-enforced install | Explicitly non-GA / not enforced |
