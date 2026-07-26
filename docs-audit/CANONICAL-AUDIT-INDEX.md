# CANONICAL-AUDIT-INDEX (Phase 5A)

**Purpose:** declare which Wave 1–4 artifacts are authoritative for Phase 5 model synthesis, and where they conflict.
**SoT remains executable code.** Everything below is *evidence*, not truth.

Corpus: 78 files. Canonical IDs are **never renumbered**: CAP-001…CAP-1041, EF-001…EF-057, ART-001…039, FLOW-001…022, GAP-001…044.

## Status legend
`CANONICAL` authoritative for its domain · `SUPPORTING` evidence depth · `AGENT-LOCAL` raw agent output, not authoritative · `SUPERSEDED` retained for history · `CONFLICTING` known contradiction, resolution recorded · `INCOMPLETE` known coverage gap

## Ledgers (authoritative registries)

| File | Purpose | Status | Authoritative for | Supersedes | Known limitations |
|------|---------|--------|-------------------|------------|-------------------|
| `capabilities.json` | 674 CAP entries (wave 1/2/3: 307/183/184) | **CANONICAL** | CAP ID existence, wave/agent/report provenance | `_wave1-cap-extract.json`, `_wave2-cap-extract.json` | Flat; no product hierarchy; no Wave-4 corrections applied → Phase 5B adds `canonical-capabilities.json` beside it |
| `CAPABILITY-INVENTORY.md` | Human CAP index + Wave-4 status header | **CANONICAL** | CAP names/narrative index | — | Wave-1/2/3 sections written before Wave-4 corrections; header lists required corrections |
| `ENGINEERING-FINDINGS.md` | EF-001…057 canonical ledger | **CANONICAL** | All EF IDs, incl. EF-024 WITHDRAWN and Wave-3 agent-local→canonical mappings | all agent-local `EF-0xx` / `EFC-*` numbering | Statuses are OPEN by design (Phase 5 fixes nothing) |
| `ARTIFACT-MAP.md` | ART-001…039 | **CANONICAL** | Artifact identity + lifecycle | — | Pre-Phase-5 categorisation is flat → 5H builds ontology |
| `CODE-DERIVED-WORKFLOWS.md` | FLOW-001…022 | **CANONICAL** | Proven workflows | — | Composition classes added in 5M |
| `capability-graph.json` | 658 nodes / 588 edges, discovery-oriented | **CANONICAL (raw)** | Raw discovery graph — **must not be destroyed** | — | Node explosion + 17 ad-hoc rel names; 5R adds a normalized parallel graph |
| `CAPABILITY-GRAPH.md` | Graph narrative | SUPPORTING | Graph reading guide | — | Mirrors raw graph limitations |
| `CAPABILITY-NORMALIZATION.md` | Merge candidates + Wave-4 second pass | **CANONICAL** | Normalization policy + required CAP corrections | — | Policy only; 5B produces the per-CAP classification |
| `ENVIRONMENT-VARIABLES.md` | Env catalog w/ code sites | **CANONICAL** | Config surface | — | Cross-checked by `CONFIG-NEGATIVE-SPACE.md`; dual-parse of `OCCAM_RECEIPTS` noted there |
| `DEAD-OR-UNREACHABLE.md` | Dead / computed-but-unexposed | **CANONICAL** | Dead-vs-shipped calls | — | Must be read with `SHIPPED-CODE-MAP.md`: dead ≠ unshipped (whole-glob compile) |

## Wave 4 model-correction layer (highest precedence for Phase 5)

| File | Purpose | Status | Authoritative for |
|------|---------|--------|-------------------|
| `WAVE4-REPORT.md` | Adversarial audit envelope + convergence | **CANONICAL** | Final Wave-4 verdicts; corrections list; red-team outcome |
| `SHIPPED-CODE-MAP.md` | Shipped/executable boundary | **CANONICAL** | What actually ships (Docker/tarball/npm/CI) |
| `SOURCE-COVERAGE-MATRIX.md` | 100% partition ownership | **CANONICAL** | Audit coverage claim |
| `NEGATIVE-SPACE-GAPS.md` | GAP-001…044 + verification log | **CANONICAL** | Gap classes; overrides earlier prose where they disagree |
| `FAILURE-BEHAVIOR-MAP.md` | Failure/fallback semantics | **CANONICAL** | Error-path model |
| `AUTOMATIC-BEHAVIORS.md` | 29 silent behaviors | **CANONICAL** | Automation model input (5L) |
| `PLATFORM-DIFFERENCES.md` | OS capability deltas | **CANONICAL** | Platform claims |
| `CONFIG-NEGATIVE-SPACE.md` | Independent config re-derivation | **CANONICAL** | Config gaps vs env catalog |

## Subsystem + tool evidence (deep, pre-Wave-4)

| Group | Files | Status | Notes |
|-------|-------|--------|-------|
| `subsystems/*.md` (15) | runtime-mcp, materialization, trust-receipts, network-fetch-proxy, browser-workers, config-env, batch-batchserver, watch, consensus-crosscheck, doctor, install-onboard, packaging-distribution, session-lifecycle, verify-cli, failure-atlas | **SUPPORTING** (deep evidence) | Authoritative for *mechanism detail*; **not** for cascade/trust claims corrected by Wave 4 |
| `tools/*.md` (15) | one per core MCP tool | **SUPPORTING** | Same caveat; `occam_transcode.md` holds the CONFLICTING cascade prose |
| `CLI-SURFACE.md`, `CONNECT-PLATFORM.md`, `HOST-CAPABILITY-MATRIX.md`, `RUNTIME-MODES.md`, `PROFILE-TOOL-MATRIX.md`, `NONCORE-SURFACE-MAP.md`, `CODE-MAP.md` | Surface maps | **SUPPORTING** | Catalog-accurate; Wave 4 proved *behavioral* gaps inside them (EF-049/050/051) |

## Agent-local (never auto-canonical)

| Files | Status | Rule |
|-------|--------|------|
| `negative-space/{A…H}-*-blind.md` | **AGENT-LOCAL** | Evidence + `CAP-NEW-*` / `EFC-*` proposals only. Canonicalized via `NEGATIVE-SPACE-GAPS.md` + EF-041…057 |
| `negative-space/P-product-model-redteam.md` | **AGENT-LOCAL** | Verdicts adopted into `WAVE4-REPORT.md`; the file itself is evidence |
| `WAVE{1,2,3,4}-SHARED-INSTRUCTIONS.md`, `WAVE{1,2}-ASSIGNMENT.md`, `WAVE{2,3}-PROGRESS.md`, `WAVE2-COMPLETENESS-GATE.md` | AGENT-LOCAL / process | Historical process records; not product evidence |
| `WAVE{1,2,3}-REPORT.md` | SUPPORTING (historical) | Superseded as *verdicts* by `WAVE4-REPORT.md`; retained for wave provenance |

## Superseded / redundant

| File | Superseded by | Action |
|------|---------------|--------|
| `_wave1-cap-extract.json`, `_wave2-cap-extract.json` | `capabilities.json` | Retain as raw extract; do not read for Phase 5 |
| `SESSION-LIFECYCLE.md` (root, 2 KB) | `subsystems/session-lifecycle.md` (14 KB) | Root file is a Wave-3 summary pointer; treat subsystem file as evidence, root file as index |

## Conflicts and resolutions (Wave 4 wins)

| # | Conflict | Resolution |
|---|----------|------------|
| C1 | `tools/occam_transcode.md` CAP-052/104 cascade prose (density ranking, managed as universal last rung) vs `OccamRouter.cs:145-182` | **Code wins.** 404/410 + `IsPublicReferencePage` short-circuit; `ChooseRawFallback` = `FailureRanking`; managed failure never wins surface. EF-056; correct CAP-052/104 in 5B |
| C2 | S3-04 claim of process-wide `FailureAtlasStore` leak | **Withdrawn** as EF-024; W4-A re-verified per-session DI. Atlas is in-memory per session |
| C3 | CAP-758 lint prose cites `PlaybookCommunitySanitizer` | Sanitizer is **Core-dead** (EF-047); correct the citation |
| C4 | CAP-600 / EF-014 row-mode `base_selector` | Dead *earlier* than modeled — host parsers never set it (W4-C) |
| C5 | CAP-021 "stdio framing" | Content-Length adapter is the **WS** path (GAP-011) |
| C6 | `OCCAM_RECEIPTS` described as single master switch | Incomplete: `playbook_save` always signs (EF-005) and key always minted (EF-044); also parsed twice (ReceiptsPolicy + ConsensusService) |
| C7 | CAP-1029/1031 packaging prose recited but untested | Docker HEALTHCHECK broken (EF-051); marketplace can auto-merge unvalidated (EF-052); cosign unused by install (EF-053) |
| C8 | "dead code" vs "not shipped" | Whole Core glob compiles → dead types **do ship**. `SHIPPED-CODE-MAP.md` governs |
| C9 | `CAP-995`/`CAP-999` duplicate host IDs | Reminted `CAP-1040`/`CAP-1041` (Wave 3 orch); do not resurrect originals |

## Known incompleteness (bounded)

- No runtime repro executed in Wave 4 (source-proven only): EF-041 multi-session pool kill, EF-045 fragment cache collision, EF-051 Docker health.
- Marketplace branch-protection state is outside the repo (EF-052 code path open).
- `packages/*` unit tests not executed.
- Tokenizer error bounds for `heuristic-unicode-v1` unmeasured.

## Phase 5 canonical model layer (added by Phase 5 — highest precedence for *documentation*)

Waves 1–4 are the evidence corpus. Phase 5 is the model built from it. For any future documentation
work, these files are authoritative over the raw corpus, and code remains authoritative over these.

| File | Purpose | Status | Authoritative for |
|------|---------|--------|-------------------|
| `canonical-capabilities.json` | 674 CAPs → 9 systems / 39 families / 38 product capabilities, machine-readable | **CANONICAL** | Capability hierarchy, family membership, classification, exposure relevance. **Structure of record** — overrides prose |
| `CANONICAL-CAPABILITIES.md` | Human narrative + merge/correction tables | **CANONICAL** | Normalization reasoning (see its reconciliation header) |
| `PRODUCT-TAXONOMY.md` | 9 product systems, stress-tested | **CANONICAL** | Product-system definitions, boundaries, reading order |
| `PRODUCT-DEFINITION.md` | Definition in 3 lengths + "what Occam is NOT" | **CANONICAL** | Product framing; forbidden mischaracterisations |
| `PRODUCT-ARCHITECTURE.md` | Layer model, spine vs bypass paths, topology | **CANONICAL** | Runtime flow. Corrects "TranscodePipeline is the central spine" |
| `ENTRYPOINT-MODEL.md` | 51 entrypoints classified | **CANONICAL** | Exposure surfaces. Product capability ≠ 15 MCP tools |
| `canonical/*.md` (39) | Per-family capability cards | **CANONICAL** | The writer-facing bridge to public docs |
| `ARTIFACT-ONTOLOGY.md` | ART-001…039 in 11 families | **CANONICAL** | What objects flow through Occam |
| `TRUST-MODEL.md` | 12 primitives, conservative | **CANONICAL** | **Binding for every trust claim anywhere.** Contains the forbidden-claims list |
| `ACQUISITION-ROUTING-MODEL.md` | The corrected ladder | **CANONICAL** | Acquisition/routing. Supersedes the CAP-052/104 cascade prose (EF-056) |
| `STATE-MODEL.md` | 29 state items (ST-01…29) | **CANONICAL** | Persistence, footprint, privacy |
| `AUTOMATION-MODEL.md` | 29 behaviors in 7 classes | **CANONICAL** | Automatic/silent behavior + disclosure duty |
| `COMPOSITION-MODEL.md` | CMP-001…015 + 8 rejected chains | **CANONICAL** | What composes, what only appears to |
| `USE-CASE-MODEL.md` | 8 derived user modes, ranked | **CANONICAL** | Audience model |
| `DOCUMENTATION-EXPOSURE-MATRIX.md` | Exposure class per family | **CANONICAL** | What is public / advanced / operator / never-a-feature |
| `DISCOVERABILITY-GATE.md` | Rules R1–R10 + automation design | **CANONICAL** | Future docs quality gate (design only) |
| `PRODUCT-VS-ENGINEERING.md` | EF-001…062 classified for docs | **CANONICAL** | Bug-vs-feature boundary; `NEEDS_FIX_BEFORE_DOC` list |
| `CANONICAL-PRODUCT-GRAPH.md` + `.json` | 155 nodes / 280 edges, 12 relations | **CANONICAL** | Normalized conceptual graph. Raw `capability-graph.json` preserved untouched |
| `HANDBOOK-OUTLINE.md` | 27 chapters, 7 parts, reading paths | **CANONICAL** | Handbook design |
| `DOCS-V3-PLAN.md` | 80 v2 pages → delta actions + target IA | **CANONICAL** | Docs v2→v3 migration |
| `PHASE5-SHARED-INSTRUCTIONS.md` | Agent contract for Phase 5 | SUPPORTING | Process record |
| `PHASE5-REPORT.md` | Phase 5 envelope + the 28 product answers | **CANONICAL** | Phase outcome |

**EF ledger extended:** EF-058…EF-062 added (Phase 5 trust re-read, orchestrator-verified). Agent-local
`EFC-P5-05-1…5` and `EFC-P5-G2-1` are canonicalized there; `EFC-P5-G2-1` is a duplicate of `EFC-P5-05-1`.

## Phase 5 precedence order

1. Executable code
2. Wave-4 correction layer (`WAVE4-REPORT`, `NEGATIVE-SPACE-GAPS`, `SHIPPED-CODE-MAP`, `FAILURE-BEHAVIOR-MAP`, `AUTOMATIC-BEHAVIORS`, `CONFIG-NEGATIVE-SPACE`, `PLATFORM-DIFFERENCES`)
3. Canonical ledgers (`capabilities.json`, `ENGINEERING-FINDINGS`, `ARTIFACT-MAP`, `CODE-DERIVED-WORKFLOWS`, `ENVIRONMENT-VARIABLES`, `DEAD-OR-UNREACHABLE`)
4. Subsystem/tool evidence
5. Agent-local blind reports
6. Public docs — **untrusted, frozen**
