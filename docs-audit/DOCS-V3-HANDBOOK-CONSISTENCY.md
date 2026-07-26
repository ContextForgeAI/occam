# DOCS-V3-HANDBOOK-CONSISTENCY (Phase 8L)

**Branch:** `docs/v3-canonical`  
**Date:** 2026-07-26  
**Scope:** `docs/handbook/01`–`27` + `appendix-status-labels.md` vs public docs (`docs/`, README, `llms.txt`).

**Mechanical checks:** `node scripts/check-docs.mjs` — **OK** (1072 local links, 42 anchors, discoverability + honesty gates).

---

## Summary

| Gate | Result |
|------|--------|
| Broken internal handbook links | **PASS** (checker green) |
| Terminology drift vs public trust/acquisition docs | **PASS** (no consensus proof, 60s browser, cache honesty) |
| Stale “planned chapter” references | **FAIL → FIXED** |
| Contradictions with public session import default | **PASS** (handbook aligns with P6: no retain unless `--keep-import`) |

---

## Issues found and fixes

| ID | Location | Issue | Public docs truth | Action |
|----|----------|-------|-------------------|--------|
| H1 | [handbook/index.md](../docs/handbook/index.md) §“After chapter 14” | Claimed ch 15–27 “planned in later wave” while all files exist on disk | Handbook is shipped 27-chapter spine | **Fixed** — full Parts E–G table + reading orders updated |
| H2 | [handbook/index.md](../docs/handbook/index.md) shortest path | “handbook exposure chapter planned” | [18-exposure.md](../docs/handbook/18-exposure.md) exists | **Fixed** — link to ch 18 |
| H3 | [handbook/01-what-occam-is.md](../docs/handbook/01-what-occam-is.md) limitations | “future exposure chapter” | ch 18 shipped | **Fixed** — link to ch 18 |
| H4 | [handbook/14-what-a-receipt-proves.md](../docs/handbook/14-what-a-receipt-proves.md) header | “Ch 15+ planned” | ch 15–27 shipped | **Fixed** — link to ch 15 |
| H5 | [handbook/02-honesty-contract.md](../docs/handbook/02-honesty-contract.md) | “Chapter 25 planned” for diagnosis | [25-diagnosing-bad-results.md](../docs/handbook/25-diagnosing-bad-results.md) exists | **Fixed** |
| H6 | [handbook/04-request-path.md](../docs/handbook/04-request-path.md) | “outline Ch 17 planned” | [17-opt-in-surfaces.md](../docs/handbook/17-opt-in-surfaces.md) exists | **Fixed** |
| H7 | [handbook/05-acquisition-ladder.md](../docs/handbook/05-acquisition-ladder.md) | “Chapter 25 planned” | ch 25 exists | **Fixed** |

---

## Consistency spot-checks (no fix needed)

| Topic | Handbook | Public docs | Align? |
|-------|----------|-------------|--------|
| Acquisition ladder (EF-056) | ch 5 | acquisition.md, how-occam-works | Yes |
| Browser timeout 60s | ch 5 | concepts, MCP spec (post-v3) | Yes |
| Receipt proves integrity not truth | ch 14–15 | receipts.md, trust-and-safety | Yes |
| `OCCAM_RECEIPTS` not master switch | ch 14, 20 | receipts.md | Yes |
| Session tiers | ch 6 | sessions.md, guides/sessions | Yes |
| Import default (no `_imports/` retain) | ch 6, 21 | sessions.md, guides/sessions | Yes |
| crosscheck ≠ consensus proof | ch 17, 14 | experimental.md, tools page | Yes |
| 15 tools = exposure slice not product model | ch 18, 01 | index.md, choosing-a-tool | Yes |
| Opt-in env gates co-located | ch 17 | experimental.md, llms.txt | Yes |
| npm not GA | ch 1, 3 | README, install, operators | Yes |

---

## Intentional handbook-only depth (not a contradiction)

- Runnable **CHECK** blocks per chapter — handbook pedagogical layer; public quick-start stays shorter.
- [26-architecture-internals.md](../docs/handbook/26-architecture-internals.md) — contributor depth; public [architecture/semantic-contract.md](../docs/architecture/semantic-contract.md) is thinner pointer.
- References to `docs-audit/` in handbook index authority note — engineering provenance for contributors; not duplicated in user task pages.

---

## Residual notes (document, do not “fix” as drift)

| Note | Severity |
|------|----------|
| Handbook index references `docs-audit/HANDBOOK-OUTLINE.md` as design provenance | Low — acceptable for handbook audience |
| Public [roadmap.md](../docs/roadmap.md) still uses internal gate names (PB4a, L4_GENOME_OK) | Pre-existing; out of Phase 8L handbook scope |
| `docs-audit/STATE-MODEL.md` ST-03 still says `_imports/` permanent by default | Audit ledger stale; public docs + handbook correct |

---

## Phase 8L verdict

**PASS** after stale “planned chapter” fixes. No material contradictions remain between handbook 01–27 and public v3 canonical docs on trust, acquisition, sessions, exposure, or experimental limits.
