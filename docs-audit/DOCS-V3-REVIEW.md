# DOCS-V3-REVIEW

**Branch:** `docs/v3-canonical`  
**Base (hardened product):** `f9c9a95` (Phase 6.5 HEAD before Docs v2 integrate)  
**BOM cleanup:** contract commit subject fixed → `a4437e2` (no UTF-8 BOM)  
**Date:** 2026-07-26  
**Public docs frozen until owner review:** NOT PUSHED

## Major changes from Docs v2

| Area | Change |
|------|--------|
| Product story | README / what-is / how / home rewritten around canonical definition (acquire → materialize → honest refuse → optional integrity) — not “HTML→Markdown” and not crypto-first |
| Acquisition | New `docs/acquisition.md` locks EF-056 ladder; obsolete cascade prose removed from architecture pages |
| Trust | Precise proves/does-not-prove boxes; playbook v1 vs v2; extract telemetry ≠ Receipt v1; claim/attest/crosscheck naming honesty |
| Capabilities IA | New Capabilities nav: acquisition, materialization, networking, sessions, experimental, operators |
| Handbook | Full 27-chapter + appendix under `docs/handbook/` per `HANDBOOK-OUTLINE.md` |
| Machine map | `llms.txt` rebuilt; discoverability + honesty gates added to `check-docs.mjs` |
| Install honesty | npm **not GA**; Cosign **not enforced**; SHA-256 manifest is the install integrity bar |
| Experimental | First-class `docs/experimental.md` + labeled examples for watch/crosscheck |

## Docs v2 integration method

Unrelated histories (`docs/site-overhaul` public-scrubbed vs full hardening line). Integration via **path checkout** of MkDocs IA + docs/guides/examples/trust/connect + docs CI onto hardened HEAD, then honesty rewrite. Six Docs v2 commit *contents* preserved as editorial base; commit SHAs not replayed as-is.

## Capability families newly / better exposed

Acquisition routing, networking/proxies, sessions tiers, materialization, playbooks (v2), datasets, experimental (watch/crosscheck/batch/atlas), operators/CLI, handbook spine, discoverability for all PUBLIC_CORE + PUBLIC_ADVANCED families (gate green).

## Major corrected misconceptions

1. Receipts ≠ origin/truth/identity proof  
2. Universal HTTP→browser→managed cascade  
3. Absolute “no disk cache”  
4. `session_profile` identical on every tool  
5. claim_check “proves” a claim  
6. attest = cryptographic attestation  
7. crosscheck = consensus proof  
8. extract_knowledge `receipt` = Receipt v1  
9. Playbook gate score always signed (v1 unsigned; v2 tamper-evident heuristic)  
10. npm / npx as GA install  
11. Cosign-enforced install  
12. Marketplace trusted auto-merge  
13. Fixed “15 tools” as product health model  
14. Proxy rotation = fingerprint rotation  
15. Connect rollback universal on all hosts  

## Trust wording (frozen)

A successful cryptographic verification proves that the checked bytes match what the holder of the referenced key signed. It does **not** prove truth, origin authenticity, identity, or trusted time. Playbook v2 makes the gate snapshot tamper-evident; it does not make the quality judgment objectively true.

## Experimental surfaces documented

watch, crosscheck, batch, failure atlas, managed acquisition (operator-configured), opt-in cache — with enablement, limits, why-not-default.

## Operator surface

`docs/operators.md` + install/connect/doctor/transports retained and honesty-updated. Connect Docs v2 structure kept; rollback limits stated.

## Handbook structure

Parts A–G, chapters 01–27 + status-labels appendix. Spine chapters: honesty, request path, acquisition, materialization, receipts, exposure.

## Limitations intentionally retained (document, don’t fix)

YELLOW families from Docs Truth Gate; EF-002 live isolation BLOCKED_ENVIRONMENT; marketplace trust pending EA-052; npm/cosign deferred per OD-2/OD-3; managed/search path guard asymmetry; always-sign playbook save (EF-005) warned.

## External actions still pending

- **EA-052** — marketplace branch-protection evidence  
- **EA-053** — optional future Cosign-required install  
- **EA-034** — npm end-to-end GA contract  

## Validation snapshot

| Gate | Result |
|------|--------|
| `node scripts/check-docs.mjs` | OK (incl. discoverability + honesty) |
| `node scripts/check-docs-brand.mjs` | OK |
| `node scripts/check-docs-discoverability.mjs` | OK |
| `node scripts/check-docs-honesty.mjs` | OK |
| `mkdocs build --strict` | OK (local `.venv-docs`) |
| L0 `--unit-only` | `L0_GATE_OK` / `L_RECEIPT_OK` |

## Ready for

Owner review → fix delta → push/PR. **Do not push until owner review.**
