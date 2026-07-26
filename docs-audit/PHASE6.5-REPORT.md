# PHASE 6.5 — OWNER DECISIONS + FINAL DOCS GATE

STATUS:
COMPLETE

OWNER DECISIONS:
OD-1: Marketplace = operational machinery only; trusted auto-merge/distribution requires external branch-protection verification (EA-052). Recorded, external-blocked.
OD-2: Cosign honesty-only for current release; no cosign-verified-install claim; future hardening. Recorded.
OD-3: npm NOT a 1.0 install channel; INTERNAL/EXPERIMENTAL/non-GA until end-to-end contract passes. Recorded.
OD-4: Playbook signature v2 IMPLEMENTED per written contract; v1 preserved. Recorded + shipped.
OD-5: extract `receipt` = extraction telemetry, not Receipt v1; wire kept, code comment + docs map corrected; never routed through occam_verify. Recorded.
OD-6: claim_check `proven` = retrieval-complete negative; wire kept as legacy; never cryptographic/factual proof. Recorded.
OD-7: attest = heuristic citation-support assessment; tool id kept; no crypto/identity/truth claim. Recorded.
OD-8: crosscheck = multi-source comparison / source agreement; "consensus proof" forbidden; experimental. Recorded.

PLAYBOOK SIGNATURE:
v1 compatibility: PRESERVED — v1 (absent/`playbook-sig-v1`) verifies under v1 rules; `sigVersion=1` reported.
v2 implemented: YES — `sigScheme=playbook-sig-v2`; default for new saves.
signed boundary: `v`, `alg`, `keyId`, `contentHash` (body hash), `signedAt`, `verify{score,passesGate,noiseLeakage}` under a domain-separated preimage (`occam-playbook-sig-v2\n` + canonical assertion). Recipe body bound via contentHash. `signature` + `sigScheme` unsigned by design (version also bound inside preimage as `v`).
tests: T1–T11 green in `ReceiptUnitTests` — v1 verify, v2 verify, score/signedAt/keyId/body mutation → invalid, foreign key → wrong_key, malformed sig → invalid, unknown scheme → unsupported_version, v1 gate fields remain unsigned.

NAMING / HONESTY:
extract Receipt: telemetry (unsigned; not verifiable). Code comment updated; `HONESTY-SCHEMA-MAP.md` records docs wording. Wire field kept.
claim_check: `proven` = retrieval-complete negative over the extracted leaf set; documented legacy. Wire kept.
attest: `status`/`grounded` = heuristic support (grounded ⇔ status=supported). Wire kept; XML/docs framed as non-cryptographic.
crosscheck: `verdict:consensus` = vantages agreed (not correctness). XML doc softened ("signals", not "proves"); "consensus proof" forbidden. Wire token kept.

EXTERNAL ACTIONS:
EA-052: REQUIRED EXTERNAL VERIFICATION (marketplace-trust only; not a core-docs blocker).
EA-053: DEFERRED — honesty-only for current release.
EA-034: npm not GA.

REGRESSION:
passed:
- CSS SSRF/body-cap selftest (css-extract.selftest.mjs) — OK
- hostile Nuxt (`__NUXT__` no-eval, same selftest) — OK
- browser pool clear/isolation (browser-pool-clear.selftest.mjs) — OK
- session import no-plaintext-default (occam-session-import.selftest.mjs) — OK
- L0 unit-only gate (`--unit-only`) — L0_GATE_OK (incl. receipt/playbook verify, profile exposure, fragment cache identity, packaging boundary, failure taxonomy)
- playbook v1/v2 fixtures T1–T11 — OK
- Docker health surface (`version-surface` exit 0) — OK
- npm pack boundary (`lib/host-install-gate.mjs` in tarball) — OK
failed: NONE
blocked: EF-002 live cross-host cookie bleed under Playwright — BLOCKED_ENVIRONMENT (mitigation unit-covered by browser-pool-clear selftest)

ENGINEERING FINDINGS:
fixed: EF-058 (playbook signature v2), EF-059, EF-061, EF-062, EF-013, EF-043, EF-054, EF-051, EF-035, EF-045, EFC-P5-05-1, EFC-P5-05-2 (per prior phases + this phase)
mitigated: EF-002 (anonymous context clear-state; live blocked)
deferred: EF-053 (cosign — OD-2 honesty-only), EF-034 (npm — OD-3 non-GA), EF-052 (marketplace — OD-1 external verify)
remaining docs blockers: NONE for core product documentation. Marketplace-trust claims remain excluded pending EA-052.

DOCS TRUTH GATE:
green: 12 families (core read path, http-acquisition, token-budget, focus-selection, structured/differential-materialization, quality-failure-semantics, site-mapping, digest-synthesis, mcp-exposure, client-context)
yellow: 25 families (documentable with frozen limitation wording; includes playbooks/receipts/verification/claims-attestation/packaging/sessions/etc.)
red: canonical-knowledge-ir (dead); "consensus proof" as a claim (crosscheck usable only as experimental multi-source comparison)

PRODUCT READINESS CHANGES:
Trust/Receipts and Playbooks: v2 playbook signature shipped (gate snapshot tamper-evident, still integrity-vs-key). npm reclassified explicitly non-GA (OD-3). Cosign framed honesty-only (OD-2). No area moved to BROKEN; core read path + doctor STABLE; experimental areas (watch/crosscheck/batch) unchanged.

PUBLIC DOCS MODIFIED:
NO

PRODUCT CODE MODIFIED:
YES (playbook signature v2 + backward-compatible honesty comments; no wire/schema breaks; no acquisition/API redesign; no cosign enforcement; npm/marketplace untouched)

LOCAL COMMITS:
- audit(contract): record owner product decisions + v2 preimage contract
- fix(trust): add versioned playbook signature v2 (v1 preserved)
- fix(semantics): clarify evidence and telemetry status (crosscheck/extract)
- test(trust): add playbook v1/v2 compatibility fixtures
- audit(model): reconcile final pre-doc semantics

READY FOR DOCS V3:
YES

WHY:
Every NEEDS_FIX_BEFORE_DOC trust/security item is fixed or has a frozen honest limitation; playbook provenance now has a real signed-boundary contract (v2) with distinguishable verdicts; wire terminology (receipt/proven/attest/consensus) is mapped to honest meanings with wire compatibility preserved; unsupported channels (npm, cosign-install, marketplace-trust) are classified non-GA/deferred/external. A technical writer can describe the intended product without documenting a bug as a feature, overstating trust, promising unsupported installs, or implying external guarantees.

DOCS V3 LIMITATIONS THAT MUST BE STATED:
- Trust = integrity relative to a key; never truth/identity/origin/trusted-time. Playbook v2 gate snapshot is tamper-evident, not a quality proof; v1 gate fields are unsigned.
- `occam_verify` accepts Receipt v1 / signed history only; extract `receipt` is unsigned telemetry.
- claim_check `proven` = retrieval-complete negative; attest = heuristic support; crosscheck = multi-source comparison (never "consensus proof").
- npm install is NOT GA; cosign is not enforced by any install path (sha256-manifest only); marketplace auto-merge is not a trusted distribution guarantee.
- Experimental/env-gated: watch, crosscheck, batch, atlas. Sessions/import store secrets on disk (operator must protect). Anonymous browser pooling is not a hard security boundary.

EXTERNAL ACTIONS THAT MAY REMAIN AFTER DOCS:
- EA-052: verify + record GitHub branch protection / required checks / merge restrictions before any marketplace-trust claim.
- EA-053: optional future cosign-required install hardening.
- EA-034: end-to-end npm install contract before advertising npm as a channel.

NEXT RECOMMENDED PHASE:
CANONICAL DOCUMENTATION SYNTHESIS
