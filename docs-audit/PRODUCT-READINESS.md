# PRODUCT READINESS

**Status:** Phase 6M + **6.5G reconcile** · 2026-07-26 · `fix/phase6-product-hardening`  
**Note:** Docs-ready ≠ product-ready. This matrix is the exposure basis for Docs v3.

| Area | Class | Rationale |
|---|---|---|
| CORE READ PATH (transcode/probe/digest defaults) | **STABLE** | Ladder intentional; quality/failure honesty; unit+fast gates green |
| ADVANCED ACQUISITION (browser escalation, sessions) | **USABLE_WITH_LIMITATIONS** | Anonymous clear shipped; CSP bypass + playbook JS trusted; session tiers |
| SESSIONS | **USABLE_WITH_LIMITATIONS** | Three tiers; import default no plaintext retain; operator must still protect profiles |
| TRUST / RECEIPTS | **USABLE_WITH_LIMITATIONS** | Integrity-vs-key only; history honesty fixed; Inspect honesty fixed; **playbook-sig v2 shipped (OD-4)**; key always minted |
| PLAYBOOKS | **USABLE_WITH_LIMITATIONS** | Heal/save/resolve work; Nuxt disabled; always-sign; **v2 signs gate snapshot (tamper-evident, still integrity-vs-key)**; `sigVersion` reported |
| DISCOVERY (map/search) | **USABLE_WITH_LIMITATIONS** | Search needs provider; probe SSRF mask remains |
| CLAIMS / ATTEST | **USABLE_WITH_LIMITATIONS** | Tools work; names overclaim — docs must use NH glosses |
| DATASETS | **USABLE_WITH_LIMITATIONS** | Export works; manifest verify CLI-only |
| WATCH | **EXPERIMENTAL** | Env-gated; history honesty fixed; store races / no unwatch |
| CROSSCHECK | **EXPERIMENTAL** | Env-gated; unsigned verdict; never “consensus proof” |
| BATCH | **EXPERIMENTAL** | Env-gated / BatchServer; no Receipt v1; store races |
| INSTALL | **USABLE_WITH_LIMITATIONS** | Destructive; doctor works |
| CONNECT | **USABLE_WITH_LIMITATIONS** | Platform code under `scripts/lib/operator/connect/`; top-level `occam-connect.mjs` absent on `main` lineage — Level B may skip |
| DOCTOR | **STABLE** | |
| PACKAGING (Level B tarball) | **USABLE_WITH_LIMITATIONS** | Contract script included; connect script absent on this branch |
| DOCKER | **USABLE_WITH_LIMITATIONS** | HEALTHCHECK fixed; image not necessarily published |
| NPM `@ff-occam/mcp` | **INTERNAL** — not a public 1.0 channel (OD-3/EA-034) | Pack boundary fixed & verified (`lib/host-install-gate.mjs` in tarball); unpublished; non-GA until end-to-end install contract passes |

## Summary counts

| Class | Areas |
|---|---|
| STABLE | Core read path, Doctor |
| USABLE_WITH_LIMITATIONS | Advanced acquisition, Sessions, Trust, Playbooks, Discovery, Claims/Attest, Datasets, Install, Connect, Packaging, Docker |
| EXPERIMENTAL | Watch, Crosscheck, Batch |
| BROKEN | *(none after FIX_NOW — prior broken healthcheck/npm-pack/history-lie mitigated)* |
| INTERNAL | npm publish channel (OD-3: not GA) |
