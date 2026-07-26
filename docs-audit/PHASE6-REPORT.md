# PHASE 6 — PRODUCT HARDENING BEFORE DOCS

**Date:** 2026-07-26  
**Branch:** `fix/phase6-product-hardening` (from `main` @ 684b2f1)  
**Preserved:** `docs/site-overhaul` @ 23d6453 · stash `wip-before-docs-overhaul`  
**Public docs:** untouched · **Push/PR:** none

---

## Envelope

**STATUS:** NEEDS OWNER DECISIONS

P0/P1 FIX_NOW items are implemented and unit-gated. Remaining blockers are external/policy (marketplace protection, cosign contract, npm publish intent, playbook-sig v2). A technical writer can describe GREEN/YELLOW families with the frozen glosses, but Docs v3 must not claim marketplace trust, cosign-verified install, or signed playbook quality scores until owner actions land.

**BLOCKERS**
- initial: 9 NEEDS_FIX_BEFORE_DOC (EF-052,043+013,002,053,034,035,054,051) + trust EF-058…062
- fixed: EF-043, EF-013, EF-002 (mitigated), EF-041, EF-054, EF-059, EF-061, EF-062, EF-051, EF-045, EF-034 (pack), EF-058 (Inspect interim), EF-035 (contract script; connect absent on main)
- deferred: EF-060 algorithm change; playbook-sig v2; Cosign-required install
- removed surface: Nuxt `attr` eval path (fail-closed)
- owner decision: EA-052 branch protection · EA-053 H vs Cosign-required · EA-034 npm publish · playbook-sig v2 ship timing

**SECURITY**
css-extract now mirrors http-extract private-IP + body-cap; Nuxt eval disabled; anonymous browser contexts clear cookies/storage between extracts; session import no longer retains plaintext `_imports/` by default; InstallShared no longer kills the process-wide pool on every DI. Residual: managed-provider SSRF gap (EF-003), probe SSRF mask (EF-042), CSP bypass + playbook `page.evaluate` (EF-046 — document as trusted input), EF-002 not a hard isolation guarantee.

**TRUST**
- Signature still proves only integrity vs a supplied key.
- Watch: `chainIntegrity` + `signatureStatus`; `history_verified` only if all entries signed+verified; CLI exit 0 only then.
- Playbook Inspect: verify-before-classify; `wrong_key`/`key_mismatch`; score/passesGate remain unsigned under v1.
- Reader profile includes `occam_verify`.
- All 20 Phase-5 forbidden claims remain in force.

**NAMING HONESTY**
- claim_check: KEEP_WITH_STRICT_DEFINITION — claim evidence lookup (BM25+proofs), not fact check
- attest: KEEP_WITH_STRICT_DEFINITION — heuristic citation assessment, not crypto attestation
- crosscheck: KEEP_WITH_STRICT_DEFINITION — multi-source comparison; never “consensus proof”
- watch/history: `history_verified` reserved for full signature success; weaker → `history_chain_ok` / wrong_key / invalid
- receipts: extract `Receipt` = telemetry ≠ Receipt v1; playbook `verify.score` = unsigned gate claim until v2

**RUNTIME REPRODUCTIONS**
- confirmed: EF-041,045,051,058,059,060,043,054
- not reproduced: none overturned
- blocked: EF-002 live Playwright cross-host (mitigation unit-tested); EF-051 full docker daemon absent (host binary cause confirmed)

**PACKAGING**
Docker HEALTHCHECK → `version-surface`. npm pack boundary vendored. Level B includes `check-public-mcp-contract.mjs`; `occam-connect.mjs` still absent on this `main` lineage (skipped). Marketplace workflow requires `l4_result==passed` before auto-merge; branch protection EXTERNAL.

**ACQUISITION CONTRACT**
EF-056 ladder locked as intentional in `PHASE6-ACQUISITION-CONTRACT.md`. No “fix docs by changing routing.” Regression coverage via existing router/gate + contract doc.

**REGRESSION CONTRACT**
- tests added: css-extract.selftest, browser-pool-clear.selftest, occam-session-import.selftest, flipped unit asserts (history, fragment keys, InstallShared, profiles)
- contracts covered: see `REGRESSION-CONTRACT.md`
- verification: `dotnet run --project benchmarks/l0-gate -- --unit-only` → `L0_GATE_OK`

**CANONICAL MODEL CHANGES**
TRUST-MODEL Phase-6 delta header; ENGINEERING-FINDINGS statuses FIXED/MITIGATED/PARTIAL; NAMING-HONESTY frozen for NH-04/08; DOCS-TRUTH-GATE + PRODUCT-READINESS created. Historical Waves 1–4 not rewritten.

**DOCS TRUTH GATE**
- green: 12 families (core read, token/focus, quality, map, digest, mcp-exposure, client-context, install-onboarding core, …)
- yellow: 25 (trust, sessions, playbooks, claims, packaging, experimental opt-ins, …)
- red: `canonical-knowledge-ir`; “consensus proof” claim

**PRODUCT READINESS**
- stable: Core read path, Doctor
- limited: Advanced acquisition, Sessions, Trust, Playbooks, Discovery, Claims/Attest, Datasets, Install, Connect, Packaging, Docker
- experimental: Watch, Crosscheck, Batch
- broken/internal: npm publish channel INTERNAL until EA-034; no remaining FIX_NOW broken ship surfaces after healthcheck/pack/history fixes

**ENGINEERING FINDINGS**
- resolved/fixed: EF-013,034(pack),041,043,045,051,054,059,061,062
- mitigated: EF-002, EF-058 (Inspect)
- partial: EF-035, EF-052
- remaining open (selected): EF-003,005,006,011,012,042,046,047,048,049,050,053,060, v2 of 058, …
- new: none allocated (repros confirmed existing)

**EXTERNAL OWNER ACTIONS**
- EA-052: verify GitHub branch protection / auto-merge settings for marketplace
- EA-053: choose honesty-only sha256 vs Cosign-required install
- EA-034: decide whether npm is a 1.0 channel
- playbook-sig v2: owner timing for signed verify.score envelope

**PUBLIC DOCS MODIFIED:** NO

**PRODUCT CODE MODIFIED:** YES

**COMMITS** (local, unpushed)
```
c4898e9 audit(model): Phase 6 product hardening contract and truth gate
7c2c6c4 fix(runtime): include URL fragment in cache materialization keys
0418b1a fix(packaging): healthcheck, npm pack boundary, marketplace gate
ec2615a fix(trust): honest history verify and Inspect verdicts
2c6a1d6 fix(security): harden css SSRF, Nuxt, pool, session import
```
(+ this report commit if separate)

**READY FOR DOCS V3:** NO

**WHY:** Core and trust honesty are stable enough for GREEN/YELLOW pages, but owner decisions on marketplace trust, cosign, npm channel, and playbook-sig v2 still gate several P0 packaging/trust claims. Starting Docs v3 now risks documenting surfaces we will change next week (marketplace language, signed-quality playbooks) or advertising unpublished npm.

**OWNER DECISIONS REQUIRED BEFORE DOCS**
1. EA-052 — record branch-protection evidence or keep marketplace undocumented as trusted
2. EA-053 — honesty-only vs Cosign-required
3. EA-034 — npm channel yes/no
4. playbook-sig v2 — ship now vs document score as unsigned indefinitely
5. Optional: rename extract `Receipt` / claim_check `proven` fields (schema break)

**NEXT RECOMMENDED PHASE:** Collect owner decisions on EA-052/053/034 and playbook-sig v2; then open Docs v3 synthesis only for GREEN families plus YELLOW with frozen limitation text — still no push until review.
