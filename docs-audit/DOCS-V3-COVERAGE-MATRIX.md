# DOCS-V3-COVERAGE-MATRIX

**Branch:** `docs/v3-canonical` · Phase 8B  
**Source classes:** `DOCUMENTATION-EXPOSURE-MATRIX.md` (39 families)  
**Date:** 2026-07-26

Legend for path columns: file path or `—`. Status: **OK** / **PARTIAL** / **GAP** / **N/A** (correctly undocumented as feature).

| Family | Class | Public expl. | Task guide | Reference | Handbook | Example | llms.txt | Entrypoint | Label | Status |
|--------|-------|--------------|------------|-----------|----------|---------|----------|------------|-------|--------|
| acquisition-routing | PUBLIC_CORE | acquisition.md | choosing-a-tool, how | concepts, failure-codes | Ch5 | difficult-js | yes | MCP transcode | STABLE | OK |
| http-acquisition | PUBLIC_CORE | acquisition.md | read-a-page | occam_transcode | Ch5 | read-one-page | yes | workers/http | STABLE | OK |
| browser-acquisition | PUBLIC_CORE | acquisition.md | read-a-page, difficult-js | occam_transcode | Ch5–6 | difficult-js | yes | Playwright | STABLE | OK |
| managed-acquisition | EXPERIMENTAL | acquisition, experimental | — | configuration | Ch5,17 | — | yes (exp) | managed backend | EXPERIMENTAL | OK |
| network-safety | PUBLIC_ADVANCED | networking.md | — | configuration, failure-codes | Ch6,23 | — | yes | preflight/SSRF | LIMITED | OK |
| proxy-egress | OPERATOR | networking.md | examples/proxy-use | configuration | Ch6,19 | proxy-use | yes (ops) | env proxy | LIMITED | OK (rotation callout on networking) |
| session-fetch | PUBLIC_ADVANCED | sessions.md | guides/sessions | configuration | Ch6 | session-profile | yes | session_profile | LIMITED | OK |
| access-consent | PUBLIC_ADVANCED | networking.md (Automatic consent) | — | probe classification | Ch6,20 | — | yes | auto dismiss | LIMITED | OK |
| token-budget | PUBLIC_CORE | materialization.md | read-a-page | client_capabilities, transcode | Ch7 | read-one-page | yes | max_tokens/ambient | STABLE | OK |
| focus-selection | PUBLIC_CORE | materialization.md | read-a-page | transcode | Ch7 | read-one-page | yes | focus_query | STABLE | OK |
| structured-materialization | PUBLIC_ADVANCED | materialization.md | structured-extraction | transcode json_* | Ch8 | structured-extraction | yes | json_blocks/tables | LIMITED | OK |
| differential-materialization | PUBLIC_ADVANCED | materialization.md | verify-sources | transcode if_none_match | Ch8 | verify-receipt | yes | diff/etag | LIMITED | OK |
| response-cache | EXPERIMENTAL | materialization, faq | — | configuration | Ch8,21 | — | yes (exp) | cache_ttl_s | EXPERIMENTAL | OK |
| quality-failure-semantics | PUBLIC_CORE | honest-failures | quick-start | failure-codes | Ch2 | — | yes | ok:false | STABLE | OK |
| probe-diagnostics | PUBLIC_CORE | choosing-a-tool | search-and-discover | occam_probe | Ch9 | — | yes | occam_probe | STABLE | OK |
| site-mapping | PUBLIC_CORE | choosing-a-tool | search-and-discover | occam_map | Ch9 | discover-then-research | yes | occam_map | STABLE | OK |
| web-search | PUBLIC_ADVANCED | choosing-a-tool | search-and-discover | occam_search | Ch9 | search-then-research | yes | OCCAM_SEARCH_PROVIDER | LIMITED | OK |
| digest-synthesis | PUBLIC_CORE | choosing-a-tool | research-multiple | occam_digest | Ch10 | research-several | yes | occam_digest | STABLE | OK |
| schema-knowledge-extraction | PUBLIC_ADVANCED | playbooks, structured | structured-extraction | extract_knowledge | Ch13 | structured-extraction | yes | extract_knowledge | LIMITED | OK |
| canonical-knowledge-ir | DO_NOT_DOCUMENT | — | — | — | mention discard only | — | no feature | dead | N/A | N/A |
| playbook-resolution | PUBLIC_ADVANCED | playbooks.md | choosing-a-tool | playbook_resolve | Ch11 | — | yes | resolve/auto | LIMITED | OK |
| playbook-authoring | PUBLIC_ADVANCED | playbooks.md | — | playbook_save | Ch12 | — | yes | save | LIMITED | OK |
| playbook-healing | PUBLIC_ADVANCED | playbooks.md | — | playbook_heal | Ch12 | — | yes | heal | LIMITED | OK |
| playbook-validation | PUBLIC_ADVANCED | playbooks.md | — | playbook_lint | Ch12 | — | yes | lint | LIMITED | OK |
| receipts | PUBLIC_ADVANCED | receipts, trust | verify-sources | receipt_verification | Ch14 | verify-receipt | yes | receipt | LIMITED | OK |
| verification | PUBLIC_ADVANCED | receipts, trust | verify-sources | occam_verify | Ch15 | verify-receipt | yes | verify | LIMITED | OK |
| claims-attestation | PUBLIC_ADVANCED | trust, claims | guides/claims | claim_check, attest | Ch16 | check-a-claim | yes | claim/attest | LIMITED | OK |
| dataset-provenance | PUBLIC_ADVANCED | datasets.md | — | dataset_export | Ch16 | dataset-export | yes | export+CLI | LIMITED | OK |
| batch-jobs | EXPERIMENTAL | experimental.md | — | occam_batch, transports | Ch17 | — | yes | OCCAM_BATCH_MCP | EXPERIMENTAL | OK |
| change-monitoring | EXPERIMENTAL | experimental.md | — | occam_watch | Ch17 | watch-experimental | yes | OCCAM_WATCH_MCP | EXPERIMENTAL | OK |
| consensus-crosscheck | DO_NOT_DOCUMENT *claim* / EXPERIMENTAL *observe* | experimental.md | — | occam_crosscheck | Ch17 | crosscheck-experimental | yes (exp, limited) | OCCAM_CONSENSUS_MCP | EXPERIMENTAL | OK |
| failure-atlas | EXPERIMENTAL | experimental.md | — | occam_failure_atlas | Ch17 | — | yes | OCCAM_ATLAS_MCP | EXPERIMENTAL | OK |
| runtime-transports | PUBLIC_CORE (stdio) | transports.md | quick-start | transports | Ch18–19 | — | yes | stdio/WS | STABLE/LIMITED | OK |
| mcp-exposure | PUBLIC_CORE | choosing-a-tool | — | tools index, profiles | Ch18 | — | yes | profiles/opt-in | STABLE | OK |
| client-context | PUBLIC_CORE | materialization | — | client_capabilities | Ch7 | — | yes | client_capabilities | STABLE | OK |
| operator-cli | OPERATOR | operators.md | — | troubleshooting | Ch19 | — | yes | occam CLI | LIMITED | OK |
| install-onboarding | PUBLIC_CORE | install, quick-start | quick-start | INSTALL.md | Ch3 | — | yes | bootstrap | STABLE | OK |
| host-connectors | OPERATOR | connect/*, mcp-hosts | connect | mcp-hosts | Ch19 | — | yes | occam connect | LIMITED | OK |
| packaging-distribution | OPERATOR | install, installation-safety | — | INSTALL, operators | Ch3,18 | — | yes | tarball | LIMITED | OK |

## Numeric coverage

| Class | Families | Discoverable OK | PARTIAL | GAP | N/A |
|-------|---------:|----------------:|--------:|----:|----:|
| PUBLIC_CORE | 13 | 13 | 0 | 0 | 0 |
| PUBLIC_ADVANCED | 15 | 15 | 0 | 0 | 0 |
| OPERATOR | 4 | 4 | 0 | 0 | 0 |
| EXPERIMENTAL | 5 | 5 | 0 | 0 | 0 |
| DO_NOT_DOCUMENT_AS_FEATURE | 2 | — | — | — | 2 (IR dead; consensus *claim*) |
| **Total live product families** | **37** (+2 DND) | | | | |

**PUBLIC_CORE:** 13/13 clear discovery paths.  
**PUBLIC_ADVANCED:** 15/15 OK after Phase 8 soft-fix (access-consent callout on `networking.md`).  
**OPERATOR:** 4/4 OK (proxy rotation callout on `networking.md`).

**Rule:** every PUBLIC_CORE/ADVANCED has ≥ task or capability page + llms and/or reference. OPERATOR families discoverable from operators/connect/install. EXPERIMENTAL labeled.
