# PHASE 6 — REGRESSION CONTRACT

**Status:** Phase 6J · 2026-07-26 · branch `fix/phase6-product-hardening`  
**Purpose:** Map product contracts → tests → code → capability family.

| PRODUCT CONTRACT | TEST | CODE PATH | FAMILY |
|---|---|---|---|
| css-extract enforces private-IP + body cap | `workers/css-extract/css-extract.selftest.mjs` | `css-extract.mjs` + shared private-ip / response-body-cap | `network-safety`, `schema-knowledge-extraction` |
| Nuxt attr never `eval`s page JS | same selftest (hostile `__NUXT__`) | `css-schema-extract.mjs` → `nuxt_attr_disabled` | `schema-knowledge-extraction` |
| Anonymous browser extracts clear cookie/storage | `browser-pool-clear.selftest.mjs` | `browser-pool.mjs` | `browser-acquisition`, `session-fetch` |
| Session import does not retain plaintext by default | `occam-session-import.selftest.mjs` | `scripts/occam-session.mjs` | `session-fetch`, `operator-cli` |
| InstallShared does not StopAll when shared exists | `L0InfraUnitTests` (OCCAM_GATE) | `BrowserPoolManager.InstallShared` | `browser-acquisition`, `runtime-transports` |
| Unsigned watch chain ≠ `history_verified` | `ReceiptUnitTests` / verify unit paths | `WatchHistory.Verify` → `signatureStatus` | `change-monitoring`, `verification` |
| `history_verified` only when all entries signed+verified | same | `OccamVerifyTool`, `OccamCliVerbs` | `verification` |
| Playbook Inspect verify-before-classify; wrong_key | playbook/receipt unit tests | `PlaybookSignature.Inspect` | `playbook-validation`, `receipts` |
| Playbook signature v2 signs trust fields; v1 preserved; version-distinguishable | `ReceiptUnitTests` T1–T11 (v1/v2/mutation/wrong-key/unsupported_version) | `PlaybookSignature` (BuildSignedJson/Verify/Inspect) | `playbook-validation`, `receipts` |
| Reader profile exposes `occam_verify` | profile / workflow frozen unit tests | `OccamToolProfile` | `mcp-exposure` |
| Fragment URLs have distinct cache/materialization keys | `L0InfraUnitTests`, `ConditionalEconomyUnitTests`, repro EF-045 | `TranscodeCacheKey`, `MaterializationKey` | `response-cache`, `focus-selection` |
| Docker healthcheck exits without stdio block | CLI `version-surface` smoke; Dockerfile review | `Dockerfile` HEALTHCHECK | `packaging-distribution` |
| Level B ships `check-public-mcp-contract.mjs` | build-release scriptFiles list + exists | `scripts/lib/build-release.mjs` | `packaging-distribution` |
| npm pack includes vendored host-install-gate | `npm pack --dry-run` | `packages/occam-mcp/lib/host-install-gate.mjs` | `packaging-distribution` |
| Marketplace auto-merge requires L4 `passed` | workflow review (CI) | `.github/workflows/playbook-marketplace.yml` | `packaging-distribution` |
| Acquisition ladder EF-056 intentional behaviors | `PHASE6-ACQUISITION-CONTRACT.md` + existing router/gate coverage | `OccamRouter`, `TranscodePipeline` | `acquisition-routing` |
| `ok:false` means unknown content | existing failure taxonomy / post-processor tests | post-processors + tools | `quality-failure-semantics` |

## Intentionally not covered by new tests this phase

| Contract | Reason |
|---|---|
| EF-002 live cross-host cookie bleed under Playwright | Runtime BLOCKED in Phase 6; unit clear-state test covers the mitigation |
| EF-060 Merkle algorithm change | DOCUMENT_LIMITATION only |
| EF-053 Cosign-required install | OWNER EA-053 (OD-2 honesty-only) |
| EF-052 branch protection | EXTERNAL EA-052 (OD-1) |

## How to re-run

```powershell
$env:OCCAM_HOME = (Get-Location).Path
node workers/css-extract/css-extract.selftest.mjs
node workers/browser-extract/lib/browser-pool-clear.selftest.mjs
node scripts/lib/occam-session-import.selftest.mjs
dotnet run --project benchmarks/l0-gate -- --unit-only
# optional: --fast for live smoke
```
