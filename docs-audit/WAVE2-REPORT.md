# WAVE 2 REPORT

**WAVE:** 2  
**STATUS:** COMPLETE

## CORE TOOLS

**15/15** registered in `OccamToolNames` — each has `docs-audit/tools/<name>.md`  
(`occam_transcode` from Wave 1; 14 audited in Wave 2; probe late envelope incorporated)

## CAPABILITIES

| | |
|--|--|
| before | 307 |
| after | **490** |
| new | **183** |
| reused | Heavy cross-reference to Wave-1 IDs (transcode cascade, SSRF, sessions, receipts, profiles, managed, proxy) — preferred over minting duplicates |

## PRODUCT GRAPH

| | |
|--|--|
| nodes | 466 |
| edges | 579 unique |
| largest shared subsystems | `TranscodePipeline`/`OccamRouter`; `Receipts`/`MerkleTree`; `Session`/`FetchPreflight`; `Probe`/`HttpProbeFetcher`; `Playbooks` resolver; Managed backends |
| most connected tools | `occam_transcode`, `occam_digest`, `occam_claim_check`/`occam_attest`, `occam_verify`, `occam_dataset_export`, `occam_search`↔probe, `occam_map`↔digest |

Also: `PROFILE-TOOL-MATRIX.md`, `ARTIFACT-MAP.md` (26 artifacts), `CODE-DERIVED-WORKFLOWS.md` (11 flows).

## ARTIFACTS DISCOVERED

**26** (`ART-001`…`ART-026`) — key: compiled markdown, blocks, capsules, Receipt v1, digest bundles, probe/map/search hits, facts[], playbooks, claim citations, attest statuses, dataset manifest, ambient budget, watch history (consume-only from core verify).

## CODE-DERIVED WORKFLOWS

**11:** FLOW-001…011 — read+verify; probe→read; map/search→digest; search rerank; heal→lint→save→resolve; resolve→extract_knowledge; claim→verify/attest; dataset→CLI manifest verify; session reads; delta/conditional; verify history without watch MCP.

## MOST IMPORTANT HIDDEN CAPABILITIES

1. claim_check / dataset_export — **no token budget**
2. Forced `playbook_policy=auto` + `json_blocks` on claim/attest/dataset
3. digest: playbooks + transcode sidecars unreachable
4. verify live drops session/playbook; bad `mode` → silent offline
5. extract_knowledge: `eval` on `__NUXT__`; session drop on browser fallback
6. playbook_save signs even if `OCCAM_RECEIPTS=off`
7. playbook_lint ≠ save/resolve parsers
8. search `rerank` = up to 20 live probes
9. probe/map/heal: session often headers-only (export-state inert)
10. dataset top-level `ok` always true; manifest verify CLI-only

## MOST SURPRISING PRODUCT BEHAVIORS

1. Opt-in MCP tools ignore `OCCAM_PROFILE` (orthogonal)
2. Managed third-party scrape as last rung of `http_then_browser` only
3. Proxy rotation forces one-shot workers (bypasses daemons)
4. Canonical Knowledge computed then discarded every transcode
5. Always-on internal block collection; flags only serialize
6. Merkle proofs can survive `OCCAM_RECEIPTS=off` on claim_check
7. Attest “grounded” only from semantic `status`, not BM25
8. extract_knowledge bypasses entire TranscodePipeline quality stack
9. Heal/browser skeleton bypasses OccamRouter
10. Cache key omits rank_blocks/tag_trust/emit_capsule (EF-001)

## MOST IMPORTANT CROSS-TOOL COMPOSITIONS

1. Almost all “read” tools → shared TranscodePipeline cascade
2. attest → claim_check → pipeline → Merkle
3. digest ↔ map discovery engine
4. search rerank → ProbeService
5. verify consumes receipts/capsules/citations/watch-history from many producers
6. resolve/save/heal form playbook authoring loop; auto overlay on several tools
7. client_capabilities → ambient max_tokens → cache/materialization identity
8. dataset → CLI manifest verify (MCP gap)
9. probe hints → transcode opt-in params
10. session CLI → session_profile params (uneven storageState support)

## PROFILE SURFACE

Default `full` = 15 tools. `reader`=7, `researcher`=9, `auditor`=12. Playbook heal/resolve/save = **full only**. Opt-ins (batch/watch/crosscheck/atlas) **add** tools regardless of profile. Invalid profile → full. Details: `PROFILE-TOOL-MATRIX.md`.

## ENGINEERING FINDINGS

| ID | Class |
|----|-------|
| EF-001 | BUG-CANDIDATE (cache key) |
| EF-002 | SECURITY-CANDIDATE (cookie bleed) |
| EF-003 | SECURITY-CANDIDATE (managed no OutboundHttpGuard) — **orch confirmed** |
| EF-004 | PERFORMANCE-CANDIDATE (canonical discard) |
| EF-005 | BUG-CANDIDATE (playbook_save vs OCCAM_RECEIPTS) |
| EF-011…012 | verify mode/live semantics |
| EF-013 | SECURITY-CANDIDATE (Nuxt eval) |
| EF-014…015 | extract/lint bugs |
| EF-016…018 | budget/session/dataset design |

## CAPABILITY NORMALIZATION

| Class | Notes |
|-------|-------|
| keep | Surface taxonomy, cascade, managed, proxy rotation, sessions, trust, profile matrix, honesty contracts |
| merge candidates | Session headers-only cluster; no-budget callers; forced playbook auto; reduced receipts; lint drift; fake receipt |
| implementation details | Fine-grained heal/lint/probe/map CAP minutiae |
| needs review | 490 CAP count before public docs; graph edge noise |

## SECOND-PASS TOOLS

**none** (completeness gate + negative-space mini pass: no core-tool BLOCKED gaps)

## FILES CREATED/UPDATED (this consolidation)

- `docs-audit/PROFILE-TOOL-MATRIX.md` *(new)*
- `docs-audit/ARTIFACT-MAP.md` *(new)*
- `docs-audit/CODE-DERIVED-WORKFLOWS.md` *(new)*
- `docs-audit/WAVE2-COMPLETENESS-GATE.md` *(new)*
- `docs-audit/WAVE2-REPORT.md` *(updated)*
- Prior Wave 2: tool reports, inventory, capabilities.json, CAPABILITY-GRAPH, engineering, normalization, dead

## PRODUCT INTERPRETATION

1. Occam is a **local MCP acquisition + materialization host**, not a single “fetch” tool.
2. **`occam_transcode` / TranscodePipeline** is the shared spine for most “read/trust” tools.
3. **Probe/map/search** are discovery/triage fronts into that spine (search can *embed* probe).
4. **Playbooks** are a parallel recipe system (full-profile authoring; silent auto on several callers).
5. **extract_knowledge** is a separate CSS/schema path — weaker trust, different worker.
6. **Receipts/Merkle/capsules** make extracts portable and third-party-checkable via verify/CLI.
7. **claim_check/attest** turn extracts into retrieval+citation (+ limited semantic status).
8. **dataset_export** packages many live extracts into a signed set (manifest verify is CLI).
9. Runtime surface is **profile × opt-in**, not a fixed “15 tools” product.
10. Several powerful behaviors are **env-/param-hidden** (managed, proxy rotation, forced autos, budget gaps).

## RECOMMENDATION

**Wave 3 can start** (opt-in MCP + BatchServer + connect/install/operator CLI), then DOC-GAP.  
**Do not** rewrite public docs until Wave 3 + owner review of this model.

**STOP — Wave 3 not started.**
