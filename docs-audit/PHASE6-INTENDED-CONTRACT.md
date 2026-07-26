# PHASE 6 — INTENDED CONTRACT

**Status:** Phase 6A · Orchestrator freeze · 2026-07-26  
**Branch:** `fix/phase6-product-hardening` (from `main` @ 684b2f1)  
**Preserved:** `docs/site-overhaul` @ 23d6453 · stash `wip-before-docs-overhaul` · untracked `docs-audit/`  
**Rule:** Semantics first. Code follows this file. Public docs remain FROZEN.

## Locked fundamental (trust)

A cryptographic Occam signature may prove **integrity of bytes relative to a supplied key**.  
It MUST NOT prove: truth, origin authenticity, factual accuracy, external identity, trustworthy timestamp, or semantic correctness.  
Source: `TRUST-MODEL.md` §0 / Phase 5 forbidden claims (all 20 remain in force until code+tests justify removal).

---

## Blockers & naming issues

### EF-043 — css-extract SSRF / body-cap parity

| Field | Value |
|-------|-------|
| CURRENT BEHAVIOR | css-extract uses `egressFetch` without DNS pin / private-IP reject and unbounded `response.text()` |
| WHY PROBLEM | Network-safety presented as host-wide; extract_knowledge can SSRF / DoS |
| USER IMPACT | Untrusted URL can hit private network or exhaust memory |
| SECURITY IMPACT | CRITICAL |
| TRUST IMPACT | Forbids “all workers enforce SSRF” claim |
| CURRENT PUBLIC IMPLICATION | Docs/trust model already warn; still NEEDS_FIX before parity claims |
| POSSIBLE INTENDED | (A) parity with http-extract (B) document css as weaker forever |
| **RECOMMENDED** | **A — FIX_NOW** mirror `resolveAndValidateHost` + `OCCAM_MAX_RESPONSE_BYTES` |
| BREAKING? | No (stricter; private URLs that previously worked now fail closed) |
| COMPATIBILITY | Callers using private URLs via extract_knowledge will start getting blocks |
| ACCEPTANCE | Private/loopback URL → fail with policy error; body > max → capped/rejected; selftest |
| OWNER DECISION? | No |

### EF-013 — Nuxt `eval`

| Field | Value |
|-------|-------|
| CURRENT | `(0,eval)(__NUXT__)` / page-controlled JS in Node during css-extract |
| WHY | Arbitrary code execution from page content |
| **RECOMMENDED** | **FIX_NOW — disable Nuxt attr path** (fail with typed error / skip). Safe parser may follow later. |
| BREAKING? | Yes for schemas using `attr=nuxt` |
| ACCEPTANCE | Nuxt attr never calls `eval`; regression selftest |
| OWNER DECISION? | Soft — orchestrator chooses disable over sandboxed parser for speed to honest docs |

### EF-002 / EF-040 — anonymous BrowserContext bleed

| Field | Value |
|-------|-------|
| CURRENT | Warm pool reuses BrowserContext for anonymous→anonymous |
| **RECOMMENDED** | **FIX_NOW — clear cookies + storage between anonymous extracts** (OD-1: clear, not always-fresh context) |
| BREAKING? | Slight latency; no API break |
| ACCEPTANCE | Cookie set on host A not visible on subsequent anonymous extract of host B |
| OWNER DECISION? | No (isolation required; clear is sufficient contract) |

### EF-054 — plaintext `_imports/` retention

| Field | Value |
|-------|-------|
| CURRENT | Session import keeps raw cookies under `_imports/` by default |
| **RECOMMENDED** | **FIX_NOW — default do not retain**; require explicit `--keep-import` |
| BREAKING? | Yes for operators relying on retained raw files |
| ACCEPTANCE | Default import leaves no plaintext raw cookie file; flag restores old behavior |
| OWNER DECISION? | No (security default) |

### EF-041 — InstallShared StopAll

| Field | Value |
|-------|-------|
| CURRENT | Every WS/Remote DI rebuild calls `StopAll` then replaces shared pool |
| **RECOMMENDED** | **FIX_NOW — InstallShared idempotent**: if shared already installed, do not StopAll |
| BREAKING? | No |
| ACCEPTANCE | Second DI build leaves leased daemon alive; unit test with fake daemon |
| OWNER DECISION? | No |

### EF-051 — Docker HEALTHCHECK

| Field | Value |
|-------|-------|
| CURRENT | `CMD /app/occam --version` → unknown arg → stdio blocks → unhealthy |
| **RECOMMENDED** | **FIX_NOW — HEALTHCHECK uses a non-blocking verb** (`version-surface` or equivalent that exits) |
| BREAKING? | No |
| ACCEPTANCE | Healthcheck command exits 0 quickly without waiting on stdin |
| OWNER DECISION? | No |

### EF-034 — npm package DOA if published

| Field | Value |
|-------|-------|
| CURRENT | Launcher imports outside `files` set |
| **RECOMMENDED** | **FIX_BEFORE_PUBLIC_DOCS — vendor required helpers into `packages/occam-mcp/lib/`** |
| BREAKING? | No |
| ACCEPTANCE | `npm pack` dry-run; packed tarball runs `--help` / host resolve without missing modules |
| OWNER DECISION? | EA-034: whether npm is a 1.0 channel (else REMOVE from advertised surface) |

### EF-035 — Level B tarball missing connect/contract scripts

| Field | Value |
|-------|-------|
| CURRENT | Help advertises scripts not in tarball |
| **RECOMMENDED** | **FIX_NOW — include scripts in `scriptFiles`** |
| BREAKING? | No |
| ACCEPTANCE | Built tarball contains advertised scripts; help paths exist on disk |
| OWNER DECISION? | No |

### EF-052 — Marketplace auto-merge without validation

| Field | Value |
|-------|-------|
| CURRENT | Skipped L4 can count as success; auto-merge path open |
| **RECOMMENDED** | **FIX_BEFORE_PUBLIC_DOCS** in-repo workflow harden + **EXTERNAL** branch protection |
| BREAKING? | CI behavior may reject more PRs |
| ACCEPTANCE | Empty/skipped playbook set cannot auto-merge; required check documented |
| OWNER DECISION? | Yes — EA-052 branch protection |

### EF-053 — Cosign theater

| Field | Value |
|-------|-------|
| CURRENT | Misconfigured cosign; install never verifies `.bundle` |
| **RECOMMENDED** | **DOCUMENT_LIMITATION / honesty-only** until EA-053 chooses Cosign-required |
| BREAKING? | N/A if honesty-only |
| ACCEPTANCE | No public claim of cosign-verified install; install path documents sha256-manifest only |
| OWNER DECISION? | Yes — EA-053 H vs C |

### EF-045 — fragment omitted from cache keys

| Field | Value |
|-------|-------|
| CURRENT | `#frag` drives focus but not TranscodeCacheKey / MaterializationKey |
| **RECOMMENDED** | **FIX_NOW — include fragment (or derived FocusIntent) in both keys** |
| BREAKING? | Cache misses increase; old cache entries for fragment URLs may be orphaned |
| ACCEPTANCE | `#a` and `#b` produce distinct keys; flipped unit test |
| OWNER DECISION? | No |

### EF-058 — playbook provenance unsigned

| Field | Value |
|-------|-------|
| CURRENT | Whole `provenance` excluded from signed preimage; Inspect trusts claimed keyId first |
| POSSIBLE | A unsigned-metadata honesty · B playbook-sig v2 covering trust fields |
| **RECOMMENDED** | **Interim FIX_NOW:** Inspect must verify before classifying; stop treating score/passesGate as signed. **Follow-up:** playbook-sig v2 (OWNER for ship timing) |
| BREAKING? | Interim soft; v2 needs dual-read |
| ACCEPTANCE | Tampered score still Verify under v1 but Inspect never calls it “gate-backed”; keyId tamper cannot soften to unknown_key without crypto attempt |
| OWNER DECISION? | Yes for v2 ship; No for Inspect honesty |

### EF-059 — `history_verified` on unsigned chains

| Field | Value |
|-------|-------|
| CURRENT | Null Sig skipped; wholly unsigned chain → `history_verified` / exit 0 |
| **RECOMMENDED** | **FIX_NOW — split `chainIntegrity` vs `signatureStatus`**; `history_verified` only if all entries signed+verified; unsigned → `history_chain_ok` or equivalent, non-success for “verified” |
| BREAKING? | Yes — intentional honesty break |
| ACCEPTANCE | Unsigned chain never reports `history_verified`; CLI exit ≠ 0 for “verify” of unsigned when expecting signatures |
| OWNER DECISION? | Soft — orchestrator freezes honesty fix |

### EF-060 — Merkle duplicate-last ambiguity

| Field | Value |
|-------|-------|
| CURRENT | Duplicate-last leaf promotion → CVE-2012-2459 shape |
| **RECOMMENDED** | **DOCUMENT_LIMITATION** for Docs v3; algorithm change DEFER (compat) |
| OWNER DECISION? | Yes before algorithm change |

### EF-061 — reader hides verify

| Field | Value |
|-------|-------|
| CURRENT | reader emits receipts, omits `occam_verify` |
| **RECOMMENDED** | **FIX_NOW — expose `occam_verify` on reader** (option A) |
| BREAKING? | Tool list grows for reader profile |
| ACCEPTANCE | `OCCAM_PROFILE=reader` tools/list includes `occam_verify` |
| OWNER DECISION? | No |

### EF-062 — no wrong_key verdict

| Field | Value |
|-------|-------|
| CURRENT | Wrong key indistinguishable from tamper |
| **RECOMMENDED** | **FIX_NOW where feasible** — emit `wrong_key` / `key_mismatch` when claimed keyId ≠ verifier key and/or when public key fingerprint mismatches |
| BREAKING? | Soft — new verdict string |
| OWNER DECISION? | No |

---

## Naming honesty (freeze — no MCP ID renames)

| Term | User inference | Code truth | Decision |
|------|----------------|------------|----------|
| `occam_claim_check` | Fact check | BM25 + regex span retrieval with proofs | **KEEP_WITH_STRICT_DEFINITION** — “claim evidence lookup” |
| `occam_attest` | Cryptographic attestation | Unsigned heuristic citation status after re-fetch | **KEEP_WITH_STRICT_DEFINITION** — “heuristic citation assessment” |
| `occam_crosscheck` / consensus | Multi-party consensus proof | Local multi-source agreement observation | **KEEP_WITH_STRICT_DEFINITION** — “multi-source comparison”; never “consensus proof” |
| `history_verified` | Signed history | Currently link-only possible | **RENAME_DISPLAY_CONCEPT** — reserve for full signature verify; add `chain_integrity` |
| extract `Receipt` | Receipt v1 | Telemetry | **KEEP technical field if needed + STRICT LABEL** “extraction telemetry, not Receipt v1” |
| `verify.score` / `passesGate` | Signed quality | Unsigned in provenance | **KEEP_WITH_STRICT_DEFINITION** as unsigned claims until v2 |

Full table: `docs-audit/NAMING-HONESTY-DECISIONS.md`.

---

## Acquisition contract (intentional — not “bugs”)

Current code-derived ladder (EF-056) is **intended**. Lock with regression tests; do not change to match old docs.

Locked behaviors: HTTP success · thin/challenge downgrade · browser escalation · 404/410 short-circuit · public-reference short-circuit · dual-fail `FailureRanking` · managed only after both local fails · managed fail never wins surface · `ok:false` = unknown · private-IP reject on http/browser · session tiers 1/2/3.

Details: `PHASE6-ACQUISITION-CONTRACT.md` (6E).
