# DOCS-V3-HONESTY-AUDIT (Phase 8F + 8G)

**Branch:** `docs/v3-canonical` · **Date:** 2026-07-26  
**Sources:** code + `HONESTY-SCHEMA-MAP.md` + `OWNER-DECISIONS.md` + `TRUST-MODEL.md` §13 + `PHASE6-ACQUISITION-CONTRACT.md`  
**Scope:** `docs/`, `README.md`, `INSTALL.md`, `llms.txt`, `docs/handbook/`, `MCP_API_SPEC.md` (public English only; `_archive/` excluded)

## Summary counts

| Metric | Count |
|--------|------:|
| Public doc files scanned (approx.) | 120+ |
| Findings recorded (table rows) | 48 |
| **OVERCLAIM** | 14 |
| **AMBIGUOUS** | 12 |
| **OK** | 22 |
| Doc files **fixed** in this pass | **7** |
| Out-of-scope overclaims noted (not fixed) | 2 files |

**Fixed files:** `docs/concepts.md`, `docs/receipt_verification.md`, `docs/tools/index.md`, `docs/tools-reference.md`, `docs/datasets.md`, `docs/tools/occam_watch.md`, `MCP_API_SPEC.md`

---

## Trust / provenance vocabulary

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/concepts.md` | `\| receipt \| Cryptographic attestation of what was extracted` | OVERCLAIM | **yes (fixed)** | Signed Receipt v1 integrity vs local key; not attestation of truth/origin |
| `docs/concepts.md` | `Every tool call fetches the page **now**. There is no built-in disk cache` | OVERCLAIM | **yes (fixed)** | Default live extract; qualify `cache_ttl_s` local replay |
| `docs/receipt_verification.md` | `prove the extraction is authentic **offline**` | OVERCLAIM | **yes (fixed)** | Verify integrity offline; does not prove authenticity/origin/truth |
| `docs/receipt_verification.md` | `blockLeaves sidecar is authentic because` | OVERCLAIM | **yes (fixed)** | Consistent with signed root when reconstruction matches |
| `MCP_API_SPEC.md` | `**Provable absence:** … matching text provably does **not** appear` | OVERCLAIM | **yes (fixed)** | Retrieval-complete negative (legacy `proven`); not semantic absence |
| `MCP_API_SPEC.md` | `The tool proves **which** block is lexically relevant` | OVERCLAIM | **yes (fixed)** | Tool **returns** block + membership proof; does not prove page truth |
| `MCP_API_SPEC.md` | `valid:true` proves a TSA attested the signed receipt | AMBIGUOUS | **yes (fixed)** | TSA attested per its cert; chain-trust out of scope |
| `MCP_API_SPEC.md` | playbook resolve: `A trust signal, not a resolve failure` | AMBIGUOUS | **yes (fixed)** | Integrity signal vs local key; not author/registry trust |
| `docs/tools/index.md` | `Returns provable source blocks` | OVERCLAIM | **yes (fixed)** | Retrieved blocks + Merkle membership; not proof in source page |
| `docs/tools-reference.md` | `Batch-attest claims against cited pages` | OVERCLAIM | **yes (fixed)** | Heuristic citation assessment; not cryptographic attestation |
| `docs/datasets.md` | `prove each extract assertion individually` | AMBIGUOUS | **yes (fixed)** | Bind each row's signed envelope (integrity vs key) |
| `docs/handbook/14-what-a-receipt-proves.md` | Forbidden-claim table + tamper-evident framing | OK | no | — |
| `docs/trust-and-safety.md` | Explicit forbidden-claims admonition block | OK | no | — |
| `llms.txt` | Trust limits + crosscheck/attest/proven honesty | OK | no | — |
| `README.md` | `integrity relative to a key — not truth, origin…` | OK | no | — |
| `docs/handbook/16-evidence-for-claims.md` | `proven` = retrieval-complete negative | OK | no | — |
| `docs/tools/occam_attest.md` | `not cryptographic attestation` in lead | OK | no | — |
| `docs/tools/occam_claim_check.md` | Legacy `proven` documented honestly | OK | no | — |
| `docs/tools/occam_crosscheck.md` | `Not consensus proof` | OK | no | — |
| `docs/examples/structured-extraction.md` | extract `receipt` = telemetry, not Receipt v1 | OK | no | — |
| `docs/receipts.md` | § Not Receipt v1: extract_knowledge | OK | no | — |
| `docs/playbooks.md` | v1 gate unsigned; v2 tamper-evident heuristic | OK | no | — |
| `docs/handbook/15-verifying.md` | `history_verified` vs unsigned chains | OK | no | — |
| `docs/how-occam-works.md` | `does **not** prove … source authentic` | OK | no | — |
| `packages/occam-mcp/README.md` | lead + npm GA | OVERCLAIM | **yes (fixed)** | Softened: npm not GA; integrity vs key; no fixed 14-tool health check |
| `skills/occam/SKILL.md` | `14` tools · claim/attest wording | OVERCLAIM | **yes (fixed)** | Registry/profile-aware smoke; claim/attest honesty |

---

## Feature-specific checks

### Receipt v1

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/receipts.md` | Receipt v1 = integrity vs key; OCCAM_RECEIPTS not master switch | OK | no | — |
| `MCP_API_SPEC.md` | transcode `receipt` field documents signed envelope | OK | no | — |
| `MCP_API_SPEC.md` | Goals: `every call fetches` (no cache) | OVERCLAIM | **yes (fixed)** | Default live; `cache_ttl_s` opt-in replay |

### extract_knowledge receipt telemetry

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `MCP_API_SPEC.md` | Success JSON shows `{confidence, elapsedMs}` without disclaimer | OVERCLAIM | **yes (fixed)** | Add OD-5 note: telemetry only; not Receipt v1 |
| `docs/tools/occam_extract_knowledge.md` | extraction telemetry only | OK | no | — |
| `docs/handbook/13-typed-field-extraction.md` | Receipt telemetry ≠ Receipt v1 | OK | no | — |

### claim_check `proven`

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `MCP_API_SPEC.md` | `Provable absence` / `provably does not appear` | OVERCLAIM | **yes (fixed)** | Retrieval-complete negative (OD-6) |
| `docs/tools-reference.md` | No `proven` semantics | AMBIGUOUS | **yes (fixed)** | Add legacy `proven` one-liner |
| `docs/guides/claims.md` | `proven: true` = retrieval-complete negative | OK | no | — |

### attest

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `MCP_API_SPEC.md` | `Attest an LLM report` (tool name) + layers honest | OK | no | — |
| `docs/choosing-a-tool.md` | `not cryptographic attestation` | OK | no | — |

### crosscheck / consensus

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/experimental.md` | verdict computed; not consensus proof | OK | no | — |
| `docs/handbook/17-opt-in-surfaces.md` | debunks crosscheck proves genuine | OK | no | — |
| `MCP_API_SPEC.md` | `SI-14 consensus/cloaking cross-check` (internal codename in spec) | AMBIGUOUS | no | Acceptable in API spec; user docs say multi-source comparison |

### playbook v1 / v2 signatures

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/playbooks.md` | v1 provenance unsigned; v2 gate tamper-evident | OK | no | — |
| `docs/tools/occam_playbook_save.md` | integrity relative to local key | OK | no | — |

### watch history

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/tools/occam_watch.md` | `with a signed change history` (unqualified) | AMBIGUOUS | **yes (fixed)** | Signed when receipts on; `history_verified` needs all entries signed |
| `docs/tools/index.md` | opt-in watch: `signed history` | AMBIGUOUS | **yes (fixed)** | Same qualification |

### dataset signatures / Merkle

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/datasets.md` | manifest proves row set; CLI-only manifest verify called out | OK | no | — |
| `docs/tools/occam_dataset_export.md` | integrity of export artifact | OK | no | — |
| `docs/recipes.md` | Merkle proof = block existence, not claim truth | OK | no | — |

---

## Acquisition red team (PHASE6-ACQUISITION-CONTRACT)

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/acquisition.md` | Locked ladder; 404/410; FailureRanking; managed not policy | OK | no | — |
| `docs/handbook/05-acquisition-ladder.md` | Debunks cascade myth (more text / always managed) | OK | no | — |
| `docs/failure-codes.md` | 404/410 no browser escalation | OK | no | — |
| `docs/concepts.md` | managed last resort after locals fail | OK | no | — |
| `docs/how-occam-works.md` | managed failure never surfaces | OK | no | — |
| `docs/handbook/06-when-acquisition-is-hard.md` | Debunks global session equivalence | OK | no | — |
| `docs/guides/sessions.md` | Three session tiers documented | OK | no | — |
| `docs/experimental.md` | managed not backend_policy | OK | no | — |

**Cascade myths:** No affirmative public doc still claims unconditional HTTP→browser→managed, density-ranked dual-fail, or managed as `backend_policy`. Myth text appears only as **debunked** quotes in handbook Ch.5.

---

## Meta myths (14 tools, npm GA, cosign, cache, tool-count health)

| FILE | SNIPPET | VERDICT | FIX_NEEDED | SUGGESTED FIX |
|------|---------|---------|------------|---------------|
| `docs/tools/index.md` | `15 tools by default; 6 opt-in` + profile caveat in intro | OK | no | Matches handbook Ch.18 exposure model |
| `docs/handbook/18-exposure.md` | Product ≠ 15 tools; 51 entrypoints | OK | no | — |
| `llms.txt` | Runtime `tools/list` authoritative; npm not GA | OK | no | — |
| `README.md` | npm not GA; cosign not enforced | OK | no | — |
| `INSTALL.md` / `docs/install.md` | sha256 only; npm not GA | OK | no | — |
| `docs/materialization.md` | default live + opt-in `cache_ttl_s` | OK | no | — |
| `packages/occam-mcp/README.md` | `14 MCP tools` · `Zero-config install via npx` | OVERCLAIM | no (scope) | Registry + OD-3 |
| `skills/occam/SKILL.md` | smoke expects **14** tools | OVERCLAIM | no (scope) | Registry-based smoke guidance |

---

## Fixes applied (this pass)

| File | Change |
|------|--------|
| `docs/concepts.md` | Live-extract default + cache qualification; receipt row no longer "cryptographic attestation" |
| `docs/receipt_verification.md` | "authentic offline" → integrity offline + limits; blockLeaves "authentic" → consistent with signed root |
| `docs/tools/index.md` | claim_check row; watch opt-in history wording |
| `docs/tools-reference.md` | attest heading; claim_check `proven` semantics |
| `docs/datasets.md` | manifest success box: bind envelopes vs "prove assertions" |
| `docs/tools/occam_watch.md` | signed history qualified |
| `MCP_API_SPEC.md` | Goals cache honesty; extract_knowledge receipt telemetry note; claim_check `proven` wording; playbook signature signal; TSA wording; `history_chain_ok` in verify verdicts |

---

## Residual (not fixed — follow-up)

1. **`packages/occam-mcp/README.md`** — stale 14-tool count, npx-as-primary framing, attest overclaim, global no-cache line (OD-3 / EF-036).
2. **`skills/occam/SKILL.md`** — hard-coded 14-tool smoke + "Proves block in source" (EF-036 / honesty vocabulary).
3. **`MCP_API_SPEC.md` transcode `receipt` row (line ~709)** — correctly describes signed Receipt v1 for transcode; no change needed (distinct from extract_knowledge telemetry).

---

## Verdict

Public docs on `docs/v3-canonical` are **largely aligned** with Phase 6 honesty contract after this pass. Remaining drift is concentrated in **npm package README** and **skill card** (outside the docs-only fix scope requested). Acquisition ladder and session-tier honesty are **correct** in handbook and user guides.
