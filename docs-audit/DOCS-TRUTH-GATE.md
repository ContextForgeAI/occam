# DOCS TRUTH GATE

**Status:** Phase 6L + **re-run 6.5H** · 2026-07-26 · after FIX_NOW patches + owner decisions OD-1..OD-8 on `fix/phase6-product-hardening`  
**Rule:** GREEN = safe to document as intended. YELLOW = document only with explicit limitation. RED = do not present as supported.  
**Trust:** All 20 Phase-5 forbidden claims remain **IN FORCE**. None removed — code honesty improved (history/Inspect/wrong_key/playbook-sig-v2), but none of the 20 became newly true. Wire-terminology honesty frozen in `HONESTY-SCHEMA-MAP.md`; owner decisions in `OWNER-DECISIONS.md`.

## Family gate (38 live + 1 dead cluster)

| Family | Gate | Notes |
|---|---|---|
| `acquisition-routing` | **GREEN** | Document EF-056 code truth; contract locked |
| `http-acquisition` | **GREEN** | |
| `browser-acquisition` | **YELLOW** | Anonymous clear shipped; do not claim hard isolation / CSP (EF-046) |
| `managed-acquisition` | **YELLOW** | Experimental; no OutboundHttpGuard (EF-003) |
| `network-safety` | **YELLOW** | http/browser/css parity improved (EF-043); probe still masks SSRF as network_error (EF-042); managed gap remains |
| `proxy-egress` | **YELLOW** | Partial reach (CAP-165/166) |
| `session-fetch` | **YELLOW** | Three tiers; default import no longer retains plaintext (EF-054 fixed); still warn filesystem secrets |
| `access-consent` | **YELLOW** | Silent consent dismiss |
| `token-budget` | **GREEN** | |
| `focus-selection` | **GREEN** | Fragment now in cache keys (EF-045) |
| `structured-materialization` | **GREEN** | |
| `differential-materialization` | **GREEN** | |
| `response-cache` | **YELLOW** | Opt-in; EF-001 remaining key omissions |
| `quality-failure-semantics` | **GREEN** | `ok:false` contract |
| `probe-diagnostics` | **YELLOW** | EF-042 SSRF mask |
| `site-mapping` | **GREEN** | |
| `web-search` | **YELLOW** | Fails closed without provider |
| `digest-synthesis` | **GREEN** | |
| `schema-knowledge-extraction` | **YELLOW** | Nuxt disabled (EF-013); css SSRF fixed; extract `receipt` = extraction telemetry, NOT Receipt v1 and not verifiable (EF-006, OD-5); confidence heuristic (EF-014) |
| `canonical-knowledge-ir` | **RED** | Dead cluster — never as feature |
| `playbook-resolution` | **YELLOW** | Genome fetch bounds (EF-048) |
| `playbook-authoring` | **YELLOW** | Always signs (EF-005); v2 signs gate snapshot (OD-4) — still integrity-vs-key, not a quality proof |
| `playbook-healing` | **YELLOW** | Trusted playbook JS (EF-046) |
| `playbook-validation` | **YELLOW** | Lint ≠ save; sanitizer dead (EF-047); signature v2 shipped — `sigVersion` + `unsupported_version` distinct (OD-4/EF-058) |
| `receipts` | **YELLOW** | Integrity vs key only; key always minted (EF-044); forbidden claims |
| `verification` | **YELLOW** | Modes honesty; live verify limits (EF-012); history split fixed; wrong_key shipped |
| `claims-attestation` | **YELLOW** | Strict naming NH-01/02 — never “fact check” / “crypto attest” |
| `dataset-provenance` | **YELLOW** | Manifest verify CLI-only (EF-018) |
| `batch-jobs` | **YELLOW** | Experimental; no Receipt v1 (EF-037) |
| `change-monitoring` | **YELLOW** | Experimental; history honesty fixed; store races remain |
| `consensus-crosscheck` | **RED** as “consensus proof” / **YELLOW** as experimental multi-vantage observe | NH-03 |
| `failure-atlas` | **YELLOW** | Session telemetry |
| `runtime-transports` | **YELLOW** | stdio GREEN core; WS/Remote YELLOW (EF-041 mitigated) |
| `mcp-exposure` | **GREEN** | reader now includes verify (EF-061) |
| `client-context` | **GREEN** | |
| `operator-cli` | **YELLOW** | EF-049 name-wide kill; EF-025 wrapper gaps |
| `install-onboarding` | **YELLOW** | Destructive install (EF-028/029) |
| `host-connectors` | **YELLOW** | Connect scripts may be absent on Level B until connect platform ships on main (EF-035 partial) |
| `packaging-distribution` | **YELLOW** | Docker health fixed; npm non-GA (OD-3/EA-034); cosign honesty-only, no verified-install claim (OD-2/EF-053); marketplace-trust needs external verification (OD-1/EA-052) |

## Counts

| Gate | Count |
|---|---:|
| GREEN | 12 |
| YELLOW | 25 |
| RED | 2 (+ consensus-proof claim) |

## Forbidden trust claims (unchanged — still forbidden)

The 20 claims in `TRUST-MODEL.md` §13 remain forbidden. Phase 6 did **not** make any of them true. Notably still forbidden:

- “Verified provenance of the origin page”
- “Signed quality / gate pass”
- “Consensus proves content”
- “history_verified means unsigned chains are cryptographically verified” — **this specific false success path was fixed**; the *name* remains reserved for the strong case only (NH-04)
- Cosign-verified install
- Master `OCCAM_RECEIPTS` switch

## Docs v3 entry rule

A PUBLIC_CORE / PUBLIC_ADVANCED page may ship only if its family is GREEN or YELLOW with the limitation text taken from this gate + `NAMING-HONESTY-DECISIONS.md` + `HONESTY-SCHEMA-MAP.md`. RED claims must not appear as capabilities.

## Docs v3 gate result (6.5H)

**READY_FOR_DOCS_V3 = YES.** A technical writer can describe the intended product without documenting a bug as a feature, overstating trust, promising unsupported install channels (npm non-GA, cosign honesty-only), implying external release guarantees (marketplace-trust excluded pending EA-052), or using claim/attest/crosscheck language stronger than the code — provided every YELLOW limitation ships with the frozen wording above. Experimental/limited areas are documented honestly as such.
