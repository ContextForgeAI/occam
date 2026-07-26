# MAIN-INTEGRATION-PLAN

**Purpose:** Reconcile the unrelated `docs/v3-canonical` history onto the current public runtime.  
**Date:** 2026-07-26  
**Integration branch:** `integrate/docs-v3-main` (based on `public/main` @ `d1f4c31`).  
**Docs v3 source:** `docs/v3-canonical` @ `18c3de5`.  
**Pre-v3 base of the v3 work:** `684b2f1` (occam-private `main`; NOT the public tip).

## Why the histories are unrelated

`public/main` (`d1f4c31`, remote `public` → `github.com/ContextForgeAI/occam`) is a scrubbed public
lineage that already shipped the **connect-platform** runtime (31 files under
`scripts/lib/operator/connect/**` + 15 host adapters). The `docs/v3-canonical` branch was built on the
**private** main (`684b2f1`), which predates connect-platform and instead carries Phase 6/6.5 product
hardening + Docs v3. `git` sees no merge-base, so a normal merge is impossible without
`--allow-unrelated-histories` (forbidden). This plan does a **content/commit reconciliation**:
public runtime wins; Docs v3 wins for documentation; Phase 6/6.5 fixes are ported individually.

## Rule applied

- **Public/main runtime wins** on any unrelated runtime divergence (esp. connect-platform).
- **Docs v3 wins** for documentation where public docs are stale/absent.
- **Phase 6/6.5 fixes evaluated individually** against current public runtime before applying.
- No `--allow-unrelated-histories`, no force-push, no wholesale tree overwrite, `_archive/` untouched.

---

## STEP 2 — Delta classification

The v3 work over its own base `684b2f1..18c3de5` splits into:

| Class | Scope | Handling |
|-------|-------|----------|
| **C. PRODUCT_HARDENING** | 7 code commits (`src/`, `workers/`, `scripts/`, `benchmarks/`, `packages/`, `Dockerfile`, one workflow) | Cherry-pick with `-x`; adapt on conflict |
| **A. DOCS_ONLY** | `docs/**`, `README.md`, `INSTALL.md`, `llms.txt`, `mkdocs.yml`, `MCP_API_SPEC.md`, `SECURITY/VISION/CONTRIBUTING.md`, package/skill README | Path-scoped checkout from v3 (v3 wins) |
| **B. AUDIT_ONLY** | `docs-audit/**` (Phase 5–8 artifacts + this file) | Path-scoped checkout from v3 |
| **D. TEST** | `benchmarks/l0-gate/*UnitTests.cs`, `*.selftest.mjs` | Folded into the hardening cherry-picks |
| **E. CI/DOCS_INFRA** | `scripts/check-docs*.mjs`, `scripts/lib/docs-discoverability-catalog.mjs`, `.github/workflows/docs.yml` | v3 wins (adds Pages + validators) |
| **F. CONFLICT_WITH_PUBLIC_RUNTIME** | `.github/workflows/playbook-marketplace.yml` (notify link only) | Resolved: kept public/canonical URL |
| **G. UNKNOWN** | none | — |

---

## PRODUCT_HARDENING per-commit ledger

| Source commit | Subject | Key files | Public has subsystem? | Public newer/different? | Applies cleanly? | Action |
|---------------|---------|-----------|-----------------------|-------------------------|------------------|--------|
| `2c6a1d6` | fix(security): css SSRF, Nuxt, pool, session import | `workers/css-extract/*`, `workers/browser-extract/lib/browser-pool.mjs`, `src/.../BrowserPoolManager.cs`, `Composition/*`, `scripts/occam-session.mjs` + selftests | Yes (same workers/pool) | No | Yes (1 test auto-merge) | **APPLY** |
| `ec2615a` | fix(trust): honest history verify + Inspect verdicts | `Watch/WatchHistory.cs`, `Tools/OccamVerify*`, `Cli/OccamCliVerbs.cs`, `Receipts/*`, `Playbooks/PlaybookSignature.cs`, `Transport/OccamToolProfile.cs` + tests | Yes | No | Yes | **APPLY** |
| `0418b1a` | fix(packaging): healthcheck, npm pack boundary, marketplace gate | `Dockerfile`, `packages/occam-mcp/bin/occam-mcp.js`, `packages/occam-mcp/lib/host-install-gate.mjs` (new), `scripts/lib/build-release.mjs`, `.github/workflows/playbook-marketplace.yml` | Partially (public already had EF-052 gate + public URLs) | Yes (marketplace workflow) | Conflict on 1 link | **ADAPT** (kept public canonical `ContextForgeAI/occam` links; applied Docker/npm/gate hardening) |
| `7c2c6c4` | fix(runtime): URL fragment in cache keys | `Caching/TranscodeCacheKey.cs`, `Compile/MaterializationKey.cs` + test | Yes | No | Yes | **APPLY** |
| `5123bbc` | fix(trust): playbook signature v2 (v1 preserved) | `Playbooks/PlaybookSignature.cs`, `Tools/OccamPlaybookResolve*` | Yes (had only v1) | No | Yes | **APPLY** |
| `2a70807` | fix(semantics): crosscheck/extract telemetry comments | `Tools/OccamCrosscheckTool.cs`, `Tools/OccamExtractKnowledgeTool.cs` | Yes | No | Yes | **APPLY** |
| `5a4214a` | test(trust): playbook v1/v2 compatibility fixtures | `benchmarks/l0-gate/ReceiptUnitTests.cs` | Yes | No | Yes | **APPLY** |

Audit-doc commits `c4898e9`, `0c27910`, `a4437e2`, `f9c9a95` are `docs-audit/**` → folded into the AUDIT_ONLY checkout (SKIP_ALREADY_PRESENT for runtime; brought as docs).

**Preserved product semantics (verified post-integration):** css-extract SSRF/private-IP guards + body cap;
Nuxt fail-closed; browser-pool clear idempotency/isolation; session import no-plaintext-retain default;
cache fragment identity; Docker healthcheck; npm pack boundary/host-install-gate; honest watch
`history_verified`; reader profile exposes `occam_verify`; playbook signature **v2** with **v1**
compatibility; `wrong_key`/`unsupported_version`/`key_mismatch` distinctions; crosscheck/extract honesty
comments. **Acquisition EF-056 behavior unchanged** (no acquisition files in the hardening set).

---

## STEP 3 — Connect-platform preservation

| Inventory | Before (public/main) | After (integration) |
|-----------|---------------------:|--------------------:|
| `scripts/lib/operator/connect/**` | 31 | 31 |
| `scripts/lib/operator/connect/adapters/**` | 15 | 15 |
| `connect.selftest.mjs` | present, **OK** | present, **OK** |

**Verdict:** NO accidental deletions. Connect runtime + host adapters fully preserved. Docs v3 additionally
contributes the `docs/connect/*` documentation (6 pages) that public/main lacked.

---

## STEP 7 — Runtime diff vs public/main (working tree)

| Status | Count |
|--------|------:|
| ADDED | 316 (docs, handbook, docs-audit, `docs.yml`, doc validators, `host-install-gate.mjs`, selftests) |
| MODIFIED | 65 (7 hardening runtime files + doc-infra `check-docs.mjs` + package/skill README + governance docs + 1 workflow link) |
| DELETED | **0** |

**Explicitly verified:** connect-platform present · 15 host adapters present · 15 core tools in
`OccamToolNames` · Phase 6/6.5 fixes present · no downgraded runtime files · installer/doctor/connect/
launch entrypoints present.

---

## Gates (Step 8) — all green

`mkdocs --strict` · `check-docs` · `check-docs-brand` · `check-docs-honesty` ·
`check-docs-discoverability` (links/orphans folded into check-docs) · `connect.selftest.mjs` ·
`css-extract.selftest` · `browser-pool-clear.selftest` · `occam-session-import.selftest` ·
`dotnet run benchmarks/l0-gate --unit-only` → **L0_GATE_OK** (+ L1A/L1/L2/L3/L4/L8) · AOT `dotnet publish win-x64` OK.

## Known public limitations preserved

npm non-GA · Cosign not enforced · marketplace trust requires EA-052 · experimental surfaces labeled EXPERIMENTAL.
