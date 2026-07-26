# PHASE 6 — BLOCKER TRIAGE

**Status:** Phase 6B · Orchestrator · 2026-07-26  
**Inputs:** Phase-5 shortlist + P6-01/02/04/06 agent reports + runtime repros  
**Rule:** Phase-5 rank ≠ implementation order. Re-rank by P0–P4 below.

## Priority bands

| Band | Meaning |
|------|---------|
| **P0** | Security / trust boundary — fix or explicitly block docs claim |
| **P1** | Installability / broken shipped surface |
| **P2** | Public semantics honesty (misleading success/verdicts) |
| **P3** | Operational correctness |
| **P4** | Polish |

## Re-ranked disposition table

| New rank | ID | Band | Disposition | Why this order | Owner? |
|---:|---|---|---|---|---|
| 1 | EF-043 | P0 | **FIX_NOW** | Live SSRF/body gap on extract_knowledge path; runtime-confirmed | No |
| 2 | EF-013 | P0 | **FIX_NOW** | Page-controlled `eval` in Node | Soft (disable chosen) |
| 3 | EF-002/040 | P0 | **FIX_NOW** | Cross-caller cookie/storage bleed | No |
| 4 | EF-041 | P0 | **FIX_NOW** | Cross-session availability kill; runtime-confirmed | No |
| 5 | EF-054 | P0 | **FIX_NOW** | Plaintext auth retention by default | No |
| 6 | EF-059 | P0 | **FIX_NOW** | Trust naming lying at success path; runtime-confirmed | Soft |
| 7 | EF-058 | P0 | **FIX_NOW** (Inspect honesty) + **OWNER** (v2) | Tamper→unknown_key; runtime-confirmed | Yes for v2 |
| 8 | EF-062 | P0 | **FIX_NOW** | Verdict collapse | No |
| 9 | EF-061 | P2 | **FIX_NOW** | Reader self-contained verify | No |
| 10 | EF-051 | P1 | **FIX_NOW** | Docker health broken by construction | No |
| 11 | EF-035 | P1 | **FIX_NOW** | Advertised scripts missing from tarball | No |
| 12 | EF-034 | P1 | **FIX_BEFORE_PUBLIC_DOCS** | npm DOA if published | EA-034 |
| 13 | EF-045 | P2 | **FIX_NOW** | Cache collision; runtime-confirmed; gate asserts bug today | No |
| 14 | EF-052 | P0/P1 | **FIX_BEFORE_PUBLIC_DOCS** + **OWNER_DECISION** | Supply chain; external protection | EA-052 |
| 15 | EF-053 | P1 | **DOCUMENT_LIMITATION** + **OWNER_DECISION** | Cosign theater | EA-053 |
| 16 | EF-060 | P2 | **DOCUMENT_LIMITATION** | Algorithm change deferred | Yes for alg change |

## Disposition counts (Phase 6 plan)

| Disposition | Count | IDs |
|-------------|------:|-----|
| FIX_NOW | 12 | EF-043,013,002,041,054,059,058-interim,062,061,051,035,045 |
| FIX_BEFORE_PUBLIC_DOCS | 2 | EF-034,052 (+ workflow) |
| DOCUMENT_LIMITATION | 2 | EF-053,060 |
| OWNER_DECISION | 4 | EF-058-v2,052-protection,053-contract,034-publish |
| DEFER_EXPERIMENTAL | 0 | — |
| REMOVE_SURFACE | 0 | (Nuxt attr disabled, not whole extract) |
| NOT_A_BUG | 0 | Acquisition ladder EF-056 is intentional |

## Implementation waves (this branch)

**Wave A — Security isolation (commit `fix(security): …`)**  
EF-043, EF-013, EF-002/040, EF-054, EF-041

**Wave B — Trust honesty (commit `fix(trust): …`)**  
EF-059, EF-058 Inspect, EF-062, EF-061

**Wave C — Packaging (commit `fix(packaging): …`)**  
EF-051, EF-035, EF-034 (vendor), EF-052 workflow harden

**Wave D — Cache / runtime (commit `fix(runtime): …`)**  
EF-045

**Wave E — Contracts & tests (commit `test(contract): …`)**  
Acquisition contract tests, regression map

**Wave F — Audit reconcile (commit `audit(model): …`)**  
Canonical model updates only after green tests

## Explicit non-goals this phase

- Do not change acquisition ladder to match old docs (EF-056 intentional).
- Do not rename MCP tool IDs.
- Do not implement Cosign-required install without EA-053.
- Do not claim marketplace trust without EA-052 evidence.
- Do not start Docs v3.
- Do not push / merge / open PR.
