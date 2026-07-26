# DOCS-V3-USER-JOURNEY (Phase 8J)

**Branch:** `docs/v3-canonical`  
**Date:** 2026-07-26  
**Method:** Simulate a naive user starting at [README.md](../README.md) (no prior Occam knowledge, no coaching beyond linked docs). Answer 15 discovery questions; count navigation steps; flag dead ends.

**Step counting:** Each click / explicit doc hop = 1 step. Inline README answer = 0 steps.

---

## 15 discovery questions

| # | Question | Steps from README | Path | Dead end? | Result |
|---|----------|------------------:|------|-----------|--------|
| 1 | What is Occam and what problem does it solve? | 0–1 | README §Problem + §What Occam does; optional [what-is-occam.md](../docs/what-is-occam.md) | No | **PASS** |
| 2 | How do I install? | 0–1 | README §Install + [INSTALL.md](../INSTALL.md) / [quick-start.md](../docs/quick-start.md) | No | **PASS** |
| 3 | How do I connect my AI host? | 0–2 | README `occam connect`; [quick-start.md](../docs/quick-start.md) §2; [mcp-hosts.md](../docs/mcp-hosts.md) | No | **PASS** |
| 4 | How do I read my first page? | 0 | README §After install — JSON example + success/failure | No | **PASS** |
| 5 | What does `ok:false` mean? | 0–2 | README inline; [quick-start.md](../docs/quick-start.md) §5 → [honest-failures.md](../docs/trust/honest-failures.md) | No | **PASS** |
| 6 | When does HTTP vs browser run? | 2–3 | README doc map → [how-occam-works.md](../docs/how-occam-works.md) → [acquisition.md](../docs/acquisition.md); or [choosing-a-tool.md](../docs/choosing-a-tool.md) | **Was:** “difficult pages” → sessions only | **PASS** (after README link fix) |
| 7 | How do I handle login walls? | 1 | README → [guides/sessions.md](../docs/guides/sessions.md) · [sessions.md](../docs/sessions.md) | No | **PASS** |
| 8 | What do receipts prove? | 1 | README trust table → [trust-and-safety.md](../docs/trust-and-safety.md) / [receipts.md](../docs/receipts.md) | No | **PASS** |
| 9 | How do I verify a receipt? | 1 | README → [guides/verify-sources.md](../docs/guides/verify-sources.md) | No | **PASS** |
| 10 | What does Occam store on disk? | 2–3 | README → [handbook/index.md](../docs/handbook/index.md) → [21-state-and-footprint.md](../docs/handbook/21-state-and-footprint.md); alt: [index.md](../docs/index.md) → handbook | **Was:** handbook index said ch 15–27 “planned” | **PASS** (after index fix) |
| 11 | What runs automatically without asking? | 2–3 | README → handbook → [20-automatic-behaviors.md](../docs/handbook/20-automatic-behaviors.md); alt: [llms.txt](../llms.txt) `access-consent` → ch 20 | **Was:** handbook index dead-end | **PASS** (after index fix) |
| 12 | How do I enable watch/batch/crosscheck? | 2 | README doc map → [index.md](../docs/index.md) → [experimental.md](../docs/experimental.md) | No | **PASS** |
| 13 | How do I configure HTTP proxy? | 2–3 | [index.md](../docs/index.md) Capabilities → [networking.md](../docs/networking.md) → [configuration.md](../docs/configuration.md) | No direct README hop | **PASS** (hub detour acceptable) |
| 14 | Which tool for my task? | 1 | README → [choosing-a-tool.md](../docs/choosing-a-tool.md) | No | **PASS** |
| 15 | What trust claims are forbidden? | 1–2 | README trust table → [trust-and-safety.md](../docs/trust-and-safety.md); deep: [handbook/02](../docs/handbook/02-honesty-contract.md) · [handbook/14](../docs/handbook/14-what-a-receipt-proves.md) | No | **PASS** |

---

## Navigation statistics

| Metric | Value |
|--------|------:|
| Questions answerable in ≤1 step from README | 8 |
| Questions needing 2–3 steps | 7 |
| Dead ends before fixes | 3 (Q6 misleading link; Q10–Q11 handbook index) |
| Dead ends after fixes | 0 |
| Longest path (typical) | 3 steps (disk state, automation, proxy via hub) |

---

## Dead ends flagged (and fixes)

| Issue | Severity | Fix |
|-------|----------|-----|
| README “difficult pages” linked only to sessions (login), not JS/browser ladder | Medium | **Fixed** — split JS-heavy vs login-wall links in README |
| Handbook index claimed ch 15–27 “planned” though files exist | **High** | **Fixed** — index updated with full chapter map |
| Stale “future exposure chapter” / “Ch 25 planned” cross-refs in handbook body | Medium | **Fixed** — linked to ch 18 / ch 25 / ch 17 |
| Proxy not linked from README | Low | Acceptable — [index.md](../docs/index.md) Capabilities table sufficient |
| `friend-test.md` not linked anywhere (review artifact) | N/A | By design; excluded from MkDocs nav/build |

---

## llms.txt-only agent path (supplement)

An LLM starting from [llms.txt](../llms.txt) alone reaches all 15 answers in 1–2 hops via Agent routing + capability family tables + Handbook section. See journey supplement in automation audit and llms section below.

| Need | llms.txt route |
|------|----------------|
| Tool selection | Agent routing step 2 → choosing-a-tool |
| Trust limits | Trust limits section + handbook ch 2/14 |
| Experimental | Experimental table + env gates |
| Operator / install | Operator surface section |
| Automation / state | Handbook ch 20/21 (added in llms Phase 8M) |

---

## Phase 8J verdict

| Gate | Result |
|------|--------|
| User journey (15/15 reachable) | **PASS** (after handbook index + README link fixes) |
| ≤3 steps for all questions | **PASS** |
| No material dead ends | **PASS** |
