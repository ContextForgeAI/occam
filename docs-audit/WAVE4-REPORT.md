# WAVE 4 — ADVERSARIAL NEGATIVE-SPACE AUDIT REPORT

**Date:** 2026-07-26  
**SoT:** current shipped/executable code. Public docs frozen/untrusted.  
**Prior waves:** 1–3 accepted as COMPLETE coverage *model* (not SoT). Wave 4 tried to prove them incomplete.

## Compact envelope

## WAVE 4 — ADVERSARIAL NEGATIVE-SPACE AUDIT

STATUS:
COMPLETE

SHIPPED SOURCE COVERAGE:
100%
(unowned paths: none — 8 partitions W4-A…H cover all shipped executable trees per SOURCE-COVERAGE-MATRIX.md)

EXECUTABLE ENTRYPOINTS:
7/7 families audited (Program/CLI verbs, MCP transports, Node workers+daemons, doctor-invoked selftests, operator scripts, npm bins, CI/Docker ENTRYPOINT)

BLIND AUDIT PARTITIONS:
8 (A host/runtime, B routing/backends, C compile/knowledge, D tools/services, E trust/state, F workers, G scripts/operator, H packaging/CI) + Phase 4P red-team

CAPABILITIES:
before: 674
after: 674
genuinely new: 0 bulk-minted (policy: edges/ART/FLOW/EF/corrections over CAP inflation)
corrected: CAP-052/104, CAP-021, CAP-106, CAP-600, CAP-758, CAP-1029, CAP-1031 (+ CAP-151 parity edge)
merge candidates: see CAPABILITY-NORMALIZATION.md Wave 4 pass (deferred optional handbook CAPs: InstallShared visibility, printable-escapes, onboard env inject, name-wide kill)

NEGATIVE-SPACE GAPS:
critical: 2
major: 12
medium: ~35
minor: ~20

TOP MISSED CAPABILITIES:
1. Cascade truth: 404/410 + IsPublicReferencePage short-circuit; FailureRanking fallback; managed-fail never surfaces (GAP-001 / EF-056)
2. InstallShared StopAll on every WS/Remote session DI (GAP-002 / EF-041)
3. Probe SSRF typed codes masked as network_error (GAP-003 / EF-042)
4. css-extract SSRF/body-cap parity gap + EF-013 still live (GAP-004 / EF-043)
5. playbook_save ignores OCCAM_RECEIPTS; key always minted (GAP-005 / EF-005+044)
6. URL fragment omitted from cache + MaterializationKey (GAP-006 / EF-045)
7. Always-on bypassCSP + playbook page.evaluate (GAP-007 / EF-046)
8. PlaybookCommunitySanitizer Core-dead; save≠publish sanitize (GAP-008 / EF-047)
9. stop-occam name-wide kill ignoring OCCAM_HOME (GAP-033 / EF-049)
10. onboard.json env injection on every launch (GAP-034 / EF-050)
11. Docker HEALTHCHECK --version → perpetual unhealthy (GAP-035 / EF-051)
12. Marketplace auto-merge without L4 validation (GAP-036 / EF-052)
13. Cosign theater: misconfigured community sign + unused release .bundle (GAP-037 / EF-053)
14. ThinExtractBrowserExhausted / robots fail-open / empty PROXY_LIST_FILE (GAP-016…019)
15. OccamJsonPrintableEscapes + instructions advertise watch ungated (GAP-010/012)

TOP MISSING GRAPH EDGES:
1. CAP-052 → IsPublicReferencePage short-circuit + FailureRanking (not density)
2. CAP-054 recovery-only-on-managed-fail (never surface winner)
3. CAP-151 ↛ css-extract (SSRF/body-cap)
4. ReceiptsPolicy ↛ playbook_save (already EF-005; reinforce)
5. Hygiene ≠ Sanitizer ≠ Lint
6. WS/Remote session DI → InstallShared StopAll
7. fragment → TranscodeCacheKey / MaterializationKey collision
8. cache → full envelope incl. receipt/capsule replay
9. launch-mcp-host |INJECTS| onboard.json.env
10. occam refresh |USES| name-wide kill NOT INV-10 pid path
11. playbook-marketplace --AUTO_MERGES--> community WITHOUT_GATE
12. sign-release .bundle CONSUMED_BY nothing shipped
13. Dockerfile HEALTHCHECK --INVOKES--> nonexistent --version
14. CAP-106 ← ThinExtractBrowserExhausted stop
15. OutboundHttpGuard wired ≠ {managed, search, translate, css}

NEW ARTIFACT FAMILIES:
ART-034 signing-key.pem; ART-035 OccamCacheEntry; ART-036 temp CSS field-spec; ART-037 session _imports cookies; ART-038 cosign .bundle (unused); ART-039 translatedMarkdown

NEW WORKFLOWS:
FLOW-019 cache replay; FLOW-020 implicit fragment focus; FLOW-021 refresh/name-wide kill; FLOW-022 marketplace CI → community resolve

AUTOMATIC / SILENT BEHAVIORS:
InstallShared pool kill; bypassCSP always; consent/CSS-hide/virtual-scroll; key mint; save-always-sign; features-scope inject; public-ref skip browser; Canonical build-then-discard; onboard env inject; name-wide refresh kill; skill rmSync; marketplace auto-merge; robots/proxy fail-open

FAILURE / FALLBACK FINDINGS:
Cascade short-circuits; managed fail excluded from surface; probe SSRF→network_error; css unbounded/unguarded; genome CT/DoS; robots fail-open; unknown CLI args → stdio start (Docker health); marketplace skipped gate → success; batch Persist swallow

CONFIGURATION GAPS:
OCCAM_RECEIPTS incomplete master (save + key mint); OCCAM_RECEIPTS dual-parse (ReceiptsPolicy + ConsensusService); empty PROXY_LIST_FILE swallows inline; OCCAM_MANAGED_DOMAINS unset=all hosts; COSIGN env set-but-ineffective; no major unexplained feature gate beyond honesty holes

PLATFORM DIFFERENCES:
stop-occam Win vs POSIX match logic (both name-wide); ReceiptSigner harden no-op on Windows; get-ff-occam .ps1/.sh welcome divergence; release RIDs {linux-x64,osx-arm64,win-x64}; npm advertises arm64 then rejects; Docker linux-x64 only; VectorizedHtmlScanner SIMD paths (perf only)

SHIPPED VS INTERNAL CORRECTIONS:
Whole Core glob ships dead Canonical/Legacy/bench/Sanitizer; Docker omits profiles/ → silent built-ins; Level B omits connect/contract; npm unpublished/DOA; agent-sdk real but npm-unreachable; cosign .bundle ships unused; HEALTHCHECK ships broken; FreeBSD cleanup code vs no FreeBSD RID

PRODUCT MODEL RED-TEAM:
statement 1: MISLEADING — spine is OccamRouter (+ multi-entry), not TranscodePipeline alone
statement 2: MISLEADING — A+M is agent center; product includes trust/operator/CI/session
statement 3: MISLEADING — receipts/Merkle/capsules are partial/holed, not unified proof layer
statement 4: FALSE — profile×opt-in misses instructions/banner/transport/DI/side-effects
statement 5: FALSE — Wave 3 catalogued names; Wave 4 proved hostile semantics uncaptured
statement 6: MISLEADING — last-rung only on one path; open domain default; fail never surfaces
statement 7: FALSE — playbooks are in-band overlays + schema gates (worker recipes are parallel)
statement 8: PARTIALLY_TRUE — separate yes; “weaker-trust” hides SSRF/eval + fake receipt field
statement 9: MISLEADING — grounded dual-meaning + silent playbook_auto + unsigned aggregates
statement 10: FALSE — fragment poison, InstallShared, key mint, imports cookies, uncapped watch
statement 11: FALSE — AUTOMATIC map + CRITICAL silent behaviors falsify completeness
statement 12: FALSE — whole-glob dead, Docker/HEALTHCHECK/cosign/marketplace ship-boundary wrong

ENGINEERING FINDINGS:
EF-041 InstallShared×DI pool kill (BUG/avail)
EF-042 probe SSRF→network_error (SECURITY)
EF-043 css SSRF/cap parity (SECURITY); EF-013 remains OPEN
EF-044 key mint vs receipts-off (DESIGN)
EF-045 fragment cache/MaterializationKey collision (BUG)
EF-046 bypassCSP + playbook evaluate (SECURITY)
EF-047 Sanitizer dead / save≠sanitize (DESIGN/SECURITY)
EF-048 genome CT/DoS (BUG)
EF-049 name-wide process kill (SECURITY)
EF-050 onboard env injection (DESIGN)
EF-051 Docker HEALTHCHECK hang (BUG)
EF-052 marketplace auto-merge without gate (SECURITY supply-chain)
EF-053 cosign theater (SECURITY)
EF-054 session _imports plaintext cookies (PRIVACY)
EF-055 malformed schema escape + max_tokens not hard bound (BUG)
EF-056 cascade model wrong (MODEL correction)
EF-057 empty proxy-file + LibreTranslate sync-block (BUG/PERF)

REMAINING UNCERTAINTIES:
- Live multi-session InstallShared repro not executed (source-proven)
- Branch-protection may block marketplace auto-merge in practice (code path open)
- Cosign v3 keyless vs key-with-env-but-no-flag across versions (misconfigured either way)
- SocketsHttpHandler exception wrap shape for OutboundUrlBlockedException
- Whether any docker-compose override bind-mounts profiles/ (none in-tree)
- Tokenizer error bounds for heuristic-unicode-v1 model-dependent

DISCOVERY CONVERGENCE:
YES

WHY:
100% shipped executable ownership (8/8 blind reports landed). All known entrypoint families audited. The 2 CRITICAL and 12 MAJOR gaps found are now ledgered (GAP/EF/ART/FLOW/corrections) — no further CRITICAL *unmodeled* subsystem remains after tree exhaustion. Config reverse-audit found no unexplained major feature gate. Artifact reverse-audit found families now ART-034…039 (none left as unknown major). Error-path and automatic audits found honesty/parity/CI holes inside known surfaces, not a new fallback/silent subsystem. Remaining uncertainties are explicitly bounded. Partition agents report in-scope convergence or near-saturation.

TARGETED FOLLOW-UPS REQUIRED:
NONE blocking discovery. Optional (non-blocking): live WS multi-session InstallShared repro; confirm GitHub branch-protection vs marketplace auto-merge; cosign v3 behavior smoke.

PRODUCT INTERPRETATION CHANGES:
- OccamRouter (not TranscodePipeline alone) is the escalation spine; cascade prose CAP-052/104 was wrong
- Managed is not a narrow last-rung safety valve (open domain default; fail never surfaces; short-circuits skip it)
- Playbooks are in-band overlays/schema gates, not a parallel recipe system
- OCCAM_RECEIPTS is not a complete signing master (save + key mint)
- “15 + profile × opt-in” does not describe MCP exposure (instructions/banner/transport/DI)
- Wave 3 CLI catalog ≠ behavioral understanding (name-wide kill, onboard inject, Docker health, marketplace)
- Shipped ≠ reachable ≠ modeled (whole-glob dead types; Docker/tarball/npm asymmetries; cosign theater)
- Persistent/automatic surfaces were understated (InstallShared, bypassCSP, fragment cache, imports cookies)

RECOMMENDATION:
READY FOR CANONICAL DOCUMENTATION SYNTHESIS

(Synthesis MUST consume Wave 4 corrections/EFs/ART/FLOW — do not regenerate docs from Waves 1–3 alone. Public docs remain frozen until an explicit synthesis wave starts.)

---

## Evidence index

| Artifact | Path |
|----------|------|
| Shipped boundary | `docs-audit/SHIPPED-CODE-MAP.md` |
| Coverage matrix | `docs-audit/SOURCE-COVERAGE-MATRIX.md` |
| Gaps | `docs-audit/NEGATIVE-SPACE-GAPS.md` |
| Config NS | `docs-audit/CONFIG-NEGATIVE-SPACE.md` |
| Failure map | `docs-audit/FAILURE-BEHAVIOR-MAP.md` |
| Automatic | `docs-audit/AUTOMATIC-BEHAVIORS.md` |
| Platform | `docs-audit/PLATFORM-DIFFERENCES.md` |
| EFs | `docs-audit/ENGINEERING-FINDINGS.md` (EF-041…057) |
| Normalization | `docs-audit/CAPABILITY-NORMALIZATION.md` |
| Blind reports | `docs-audit/negative-space/{A–H}-*-blind.md` |
| Red-team | `docs-audit/negative-space/P-product-model-redteam.md` |
| Shared instr | `docs-audit/WAVE4-SHARED-INSTRUCTIONS.md` |

## Blind agent IDs

| Owner | Agent |
|-------|-------|
| W4-A | [host runtime](8a59cb39-43a3-43c9-9d01-30f04276ee08) |
| W4-B | [routing backends](0987b056-c673-41e7-98ed-2d9999f7f68b) |
| W4-C | [compile knowledge](8ad0771b-3ae1-48e1-a1ba-c951e15f5629) |
| W4-D | [tools services](dce83117-38b1-4b2f-bbaf-d16a6d46f632) |
| W4-E | [trust state](53d9b5fa-7f96-4457-8e6f-8b2b9d7da898) |
| W4-F | [workers](4b50edc9-c93c-49a2-9fba-4e9c9a146984) |
| W4-G | [scripts operator](40963d24-87f6-4441-8b49-92f3017a0d7d) |
| W4-H | [packaging CI](4acbf6e8-82e7-4dbb-a657-19b8916445a0) |
| W4-P | [product model red-team](235b911e-69d2-4eb9-b6ff-eae67a73d1fd) |
