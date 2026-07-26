# COMPOSITION-MODEL (Phase 5M)

**Agent:** P5-08  
**SoT:** executable code + Wave-4 corrections + FLOW-001…022 / ART / CAP / EF ledgers.  
**Docs (`docs/`, README) are untrusted.**  
**Date:** 2026-07-26  

**Purpose:** name the value created when capabilities *compose* — and reject chains the design implies but code does not complete.

---

## 0. Class legend

| Class | Meaning |
|-------|---------|
| `DIRECT_COMPOSITION` | One tool/service calls another internally (same request) |
| `SHARED_SUBSYSTEM` | Independent tools, same engine (pipeline, resolver, ProbeService, …) |
| `ARTIFACT_HANDOFF` | Output of A is a **typed** valid input to B (param/shape proven in code) |
| `OPERATOR_WORKFLOW` | Human/agent sequences tools; Occam does not auto-chain |
| `IMPLICIT_COMPOSITION` | Composition happens automatically without the user asking for the second step |

A row may list a **primary** class plus secondary tags. `ARTIFACT_HANDOFF` is asserted only when B’s parameter contract accepts A’s field/shape.

---

## 1. Candidate battery (include / reject)

| Candidate | Verdict | Primary class | FLOW | One-line evidence |
|-----------|---------|---------------|------|-------------------|
| probe → transcode | **INCLUDE** | OPERATOR_WORKFLOW (+ advisory ARTIFACT_HANDOFF of URL + hint params) | FLOW-002 | `ProbeAgentHints` sets `SuggestedNextTool=occam_transcode`; probe never calls pipeline (`ProbeService.cs`, `ProbeAgentHints.cs:31`) |
| search → probe | **INCLUDE** (rerank only) | DIRECT_COMPOSITION | FLOW-004 | `OccamSearchTool.RerankAsync` → `probeService.AnalyzeAsync` (`OccamSearchTool.cs:48-90`) |
| search → probe (manual) | **INCLUDE** | OPERATOR_WORKFLOW | FLOW-003 | Without `rerank`, search only returns URLs + `suggested` string (`:53-55`) |
| digest ↔ map | **INCLUDE** (asymmetric) | DIRECT + ARTIFACT_HANDOFF + SHARED | FLOW-003 | Digest `source_url`+focus → `mapService.MapAsync` (`DigestService.cs:458-467`); map `SuggestedNext=occam_digest` + `links[].url` (`OccamMapModels.cs:66-68`) — map does **not** call digest |
| playbook_resolve → transcode | **INCLUDE** | SHARED_SUBSYSTEM + OPERATOR (+ IMPLICIT auto) | FLOW-005/006 | Same `PlaybookSeedResolver`; transcode takes **no** resolve blob — re-resolves via `playbook_policy=auto` (`TranscodePipeline.cs:57-104`) |
| transcode → receipt → verify | **INCLUDE** | ARTIFACT_HANDOFF (+ IMPLICIT sign) | FLOW-001 | `receipt` / `occam://capsule/…` → `occam_verify` `receipt` param (`OccamVerifyTool.cs:27-69`, `OccamTranscodeModels.cs:395-411`) |
| claim_check → citations → attest | **SPLIT** | see CMP-007a/b | FLOW-007 | Attest **re-runs** claim-check; does not accept claim_check JSON (`AttestService.cs:67-69`). Citations → `verify` citation/prove **is** handoff |
| dataset_export → manifest → verify | **INCLUDE** | ARTIFACT_HANDOFF (CLI only for manifest) | FLOW-008 | CLI `--mode manifest` deserializes export response (`OccamCliVerbs.cs:240,346-375`); MCP verify has **no** manifest mode (`OccamVerifyTool.cs:30`) |
| session → authenticated acquisition | **INCLUDE** | ARTIFACT_HANDOFF of `session_profile` id | FLOW-009/017 | Tools take `session_profile` string; CLI writes ART-026 (`SessionProfileHeaders`) |
| watch → snapshot → diff | **INCLUDE** | IMPLICIT / DIRECT inside watch | FLOW-013/011 | `WatchService` stores prior hash/blocks, evaluates diff (`WatchService.cs:37-113`); history → verify `mode=history` |
| crosscheck → source comparison | **INCLUDE** | DIRECT_COMPOSITION | FLOW-014 | `ConsensusService` multi-vantage pipeline + unsigned verdict (`ConsensusService.cs:32-55`) |
| map → digest | **INCLUDE** | ARTIFACT_HANDOFF | FLOW-003 | Same as digest↔map outbound leg |
| heal → lint → save → resolve | **INCLUDE** | OPERATOR_WORKFLOW (+ SHARED after save) | FLOW-005 | Heal returns skeleton/candidates **not** `playbook_json` (`OccamPlaybookHealTool.cs:13,129-138`); lint/save both take `playbook_json`; lint not required |
| transcode → extract_knowledge | **REJECT** as handoff | — | — | Extract params: `url` only (+ policy/session); no markdown/receipt input (`OccamExtractKnowledgeTool.cs:16-19`); own spine (`KnowledgeExtractService`) |
| batch → results → dataset | **REJECT** | — | — | Dataset takes URL list only (`DatasetExportService.cs:10-14`); no `job_id` / results param; batch has no Receipt v1 (EF-037) |
| install → doctor → connect | **INCLUDE** | OPERATOR_WORKFLOW | FLOW-015/016 | Operator scripts; not MCP-internal |
| client_capabilities → ambient budget | **INCLUDE** | IMPLICIT_COMPOSITION | FLOW-001 (prefix) / **FLOW-NEW-P508-1** | `ClientCapabilityStore.ResolveMaxTokens` used by transcode (`ClientCapabilityStore.cs:70-78`, `OccamTranscodeTool.cs:78`) |

---

## 2. Composition records (code-supported)

### CMP-001 — Probe triage then read

| Field | Value |
|-------|--------|
| NAME | Probe → transcode |
| CLASS | OPERATOR_WORKFLOW (primary); advisory ARTIFACT_HANDOFF |
| STEPS | `occam_probe(url[, session_profile])` → agent applies hints → `occam_transcode(url, …)` |
| WHAT FLOWS | **URL** (and optional `finalUrl`); advisory `agentHints.suggestedNextTool`, `decisions[]` (`Parameter` e.g. `session_profile`, `backend_policy`, `json_tables`, `max_tokens`) — **not** a typed options object |
| ACHIEVES | Cheap HTTP diagnosis + backend/opt-in recommendations before expensive extract |
| WHO SEQUENCES | Agent |
| TRUST EFFECT | **Neutral** — probe unsigned; does not create/degrade Receipt v1 |
| FAILURE | Probe `ok:false` → hints may say `suggestedNextTool=none`; agent must not invent content |
| FLOW | FLOW-002 |
| EVIDENCE | `ProbeAgentHints.cs:27-103`; FLOW-002 |

### CMP-002 — Search with extractability rerank

| Field | Value |
|-------|--------|
| NAME | Search → ProbeService (rerank) |
| CLASS | DIRECT_COMPOSITION |
| STEPS | `occam_search(…, rerank=true)` → per-hit `ProbeService.AnalyzeAsync` → reorder by extractability |
| WHAT FLOWS | Hit **URL** → probe analysis → `extractability` + `recommendedBackend` on each result |
| ACHIEVES | Discovery ordered by fetchability, not provider rank alone |
| WHO SEQUENCES | Occam itself (when rerank on) |
| TRUST EFFECT | **Neutral** (no receipts on search/probe) |
| FAILURE | Probe exception → hit kept mid-low score (`OccamSearchTool.cs:98-100`); search provider fail aborts whole tool |
| FLOW | FLOW-004 |
| EVIDENCE | `OccamSearchTool.cs:48-90` |

### CMP-003 — Map discover then digest (and digest→map engine)

| Field | Value |
|-------|--------|
| NAME | Map ↔ digest discovery |
| CLASS | ARTIFACT_HANDOFF (map→digest URLs); DIRECT (digest→MapService); SHARED (`MapLinkRanker` / sitemap) |
| STEPS | A: `occam_map` → agent copies `links[].url` into `occam_digest(urls)` **or** B: `occam_digest(source_url=…)` auto-discovers |
| WHAT FLOWS | **URL list** (max 8 into digest); map hint `suggestedNext=occam_digest` |
| ACHIEVES | Multi-page research without N× transcode; auto-discovery when agent has only a hub URL |
| WHO SEQUENCES | Agent (A); Occam (B) |
| TRUST EFFECT | Digest items may carry receipts when enabled; **no** playbook / transcode sidecars on digest (FLOW-003 limit) |
| FAILURE | Empty discovery → digest failure; map partial → warning, still handoffable links |
| FLOW | FLOW-003 |
| EVIDENCE | `OccamMapModels.cs:66-68`; `DigestService.cs:78-92,458-467`; `OccamDigestTool.cs:17-28` |

### CMP-004 — Playbook resolve then acquire / extract

| Field | Value |
|-------|--------|
| NAME | Resolve → transcode / extract_knowledge |
| CLASS | SHARED_SUBSYSTEM + OPERATOR_WORKFLOW; IMPLICIT on `playbook_policy=auto` |
| STEPS | Optional `occam_playbook_resolve(url)` for planning → `occam_transcode` **or** `occam_extract_knowledge(url)` |
| WHAT FLOWS | **Host/URL** identity; resolve exposes `playbookId` / schema presence for agent gating — **not** passed as a param into transcode/extract (both re-resolve) |
| ACHIEVES | Agent knows schema/tier before spend; extract requires resolvable `knowledge_schema` |
| WHO SEQUENCES | Agent (explicit resolve); Occam (auto overlay) |
| TRUST EFFECT | Transcode may seal playbook id into receipt; extract “Receipt” is **telemetry** (CAP-287 / EF-006) |
| FAILURE | Resolve miss → agent skips extract; auto soft-fail overlay inside pipeline |
| FLOW | FLOW-005 (authoring adjacency), FLOW-006 (extract) |
| EVIDENCE | `KnowledgeExtractService.cs:37-56`; `TranscodePipeline.cs:57-104`; `OccamExtractKnowledgeTool.cs:16-19` |

### CMP-005 — Read with proof

| Field | Value |
|-------|--------|
| NAME | Transcode → receipt/capsule → verify |
| CLASS | ARTIFACT_HANDOFF; IMPLICIT signing when `OCCAM_RECEIPTS` on |
| STEPS | `occam_transcode` → store `receipt` / `receipt.capsule` / markdown → `occam_verify` or CLI `occam verify` |
| WHAT FLOWS | ART-007 signed envelope; optional ART-006 capsule string; optional `blockLeaves`; `contentHash`; optional ART-009 time-anchor |
| ACHIEVES | Offline / live / prove / citation verification neither tool alone provides end-to-end |
| WHO SEQUENCES | Agent (verify call); Occam (sign) |
| TRUST EFFECT | **Creates** verifiability (C3/C4); live mode **degrades** fidelity (drops session/playbook/budget — TRUST-MODEL C11) |
| FAILURE | Mid-chain: no receipt if receipts off / failure path; verify `invalid_receipt`; unknown mode → silent offline (EF-011) |
| FLOW | FLOW-001 |
| EVIDENCE | `OccamTranscodeModels.cs:369-411`; `OccamVerifyTool.cs:25-77`; TRUST-MODEL |

### CMP-006 — Claim citations to verify

| Field | Value |
|-------|--------|
| NAME | Claim_check → citation package → verify |
| CLASS | ARTIFACT_HANDOFF |
| STEPS | `occam_claim_check` → take match `leaf`+`proof`+receipt → `occam_verify mode=citation|prove` |
| WHAT FLOWS | `block_text`, `block_selector`, `proof[]`, signed `blockMerkleRoot` / envelope |
| ACHIEVES | Third-party check that a cited block was in the signed extract (existence ≠ truth) |
| WHO SEQUENCES | Agent |
| TRUST EFFECT | **Preserves** Merkle membership; does **not** create claim-truth |
| FAILURE | `found:false` → no citation; prove needs `blockLeaves` |
| FLOW | FLOW-007 |
| EVIDENCE | `OccamClaimCheckTool.cs:18`; `OccamVerifyTool.cs:15-16,235-288`; `ClaimCheckService.cs:20-42` |

### CMP-007a — Attest (internal claim_check)

| Field | Value |
|-------|--------|
| NAME | Attest → ClaimCheckService → pipeline |
| CLASS | DIRECT_COMPOSITION |
| STEPS | `occam_attest(claims[{claim,sourceUrl}])` → per claim `claimCheckService.CheckAsync` → classifier |
| WHAT FLOWS | **claim text + sourceUrl** (not prior claim_check JSON) |
| ACHIEVES | Semantic status + retrieval + optional Merkle attach in one call |
| WHO SEQUENCES | Occam |
| TRUST EFFECT | Aggregate **unsigned** (GAP-028); Merkle still existence-only |
| FAILURE | Claim-check failure → per-claim `unknown` (fail-closed) |
| FLOW | FLOW-007 |
| EVIDENCE | `AttestService.cs:52-100` |

### CMP-007b — Agent claim_check then attest (anti-efficient)

| Field | Value |
|-------|--------|
| NAME | claim_check → attest (two MCP calls) |
| CLASS | OPERATOR_WORKFLOW — **not** ARTIFACT_HANDOFF |
| STEPS | Agent calls both with same `{claim,url}` |
| WHAT FLOWS | Nothing typed — attest **re-fetches** |
| ACHIEVES | Redundant double extract; no composition benefit |
| WHO SEQUENCES | Agent (should prefer attest alone or claim_check alone) |
| TRUST EFFECT | Neutral / waste; risks divergent receipts across fetches |
| FAILURE | Either step fails independently |
| FLOW | FLOW-007 (limitation) |
| EVIDENCE | `AttestService.cs:67-69` vs `OccamAttestTool` input shape |

### CMP-008 — Dataset export to manifest verify

| Field | Value |
|-------|--------|
| NAME | Dataset_export → CLI manifest verify (+ per-row verify) |
| CLASS | ARTIFACT_HANDOFF |
| STEPS | `occam_dataset_export(urls)` → persist response → `occam verify --mode manifest` and/or MCP `occam_verify` per row receipt |
| WHAT FLOWS | Full export JSON (`manifest` + `rows[]` with `rowLeaf`, optional `receipt`); **MCP cannot verify manifest** |
| ACHIEVES | Set-level tamper-evidence + per-row receipts |
| WHO SEQUENCES | Agent/operator (must leave MCP for set verify) |
| TRUST EFFECT | **Creates** set binding when receipts on; unsigned if `OCCAM_RECEIPTS=off` (`OccamCliVerbs.cs:362-364`) |
| FAILURE | Row failures still in set (`ok` top-level always true — FLOW-008); unsigned → CLI `unsigned` |
| FLOW | FLOW-008 |
| EVIDENCE | `DatasetExportService.cs:17-65`; `OccamCliVerbs.cs:240,346-375` |

### CMP-009 — Session profile into fetch tools

| Field | Value |
|-------|--------|
| NAME | Session CLI → `session_profile` on acquisition |
| CLASS | ARTIFACT_HANDOFF (profile **name** string) |
| STEPS | `occam-session` init/export-state → pass `session_profile=<id>` to Tier-capable tools |
| WHAT FLOWS | Profile **id** → headers ± `storageState` path (ART-026 / ART-NEW-P504-5) |
| ACHIEVES | Authenticated / cookied fetch neither CLI nor tool alone completes |
| WHO SEQUENCES | Operator (CLI) + agent (param) |
| TRUST EFFECT | **Can degrade** — Tier-2/3 drop storageState (ACQUISITION-ROUTING-MODEL); live verify drops session |
| FAILURE | Bad id → `invalid_session_profile` / `session_profile_not_found`; silent weaker auth on Tier-3 |
| FLOW | FLOW-009, FLOW-017 |
| EVIDENCE | Tool params `session_profile`; ACQUISITION-ROUTING-MODEL tiers |

### CMP-010 — Watch change chain

| Field | Value |
|-------|--------|
| NAME | Watch poll → stored snapshot/diff → history verify |
| CLASS | IMPLICIT (diff vs prior); ARTIFACT_HANDOFF (history→verify) |
| STEPS | `occam_watch` (opt-in) persists prior contentHash/blocks → next call evaluates change/diff → `occam_verify mode=history` |
| WHAT FLOWS | Watch **id**/URL key; ART-025 history entries; contentHash; blockHashes; optional signed chain |
| ACHIEVES | Temporal change detection + chain integrity |
| WHO SEQUENCES | Agent cadence; Occam stores prior |
| TRUST EFFECT | **Creates** chain verifiability when signed; unsigned entries skip sig check (TRUST-MODEL) |
| FAILURE | First seen = no diff; multi-process race EF-019/020; creating needs `OCCAM_WATCH_MCP`, verifying does not (FLOW-011) |
| FLOW | FLOW-013, FLOW-011 |
| EVIDENCE | `WatchService.cs:37-141`; `OccamVerifyTool.cs:42-46` |

### CMP-011 — Crosscheck multi-vantage

| Field | Value |
|-------|--------|
| NAME | Crosscheck consensus |
| CLASS | DIRECT_COMPOSITION |
| STEPS | `occam_crosscheck` → N pipeline fetches → compare fingerprints → unsigned verdict |
| WHAT FLOWS | Per-vantage `contentHash` / `blockMerkleRoot` / optional ART-007; **verdict string not signed** |
| ACHIEVES | Same-host multi-backend agreement signal |
| WHO SEQUENCES | Occam (tool call); agent enables env |
| TRUST EFFECT | **Half-creates** — vantage receipts may be signed; **verdict not re-derivable by any shipped verifier** (EF-032, CAP-863) |
| FAILURE | Per-vantage failure recorded; verdict still computed from available observations |
| FLOW | FLOW-014 |
| EVIDENCE | `ConsensusService.cs:19-55,92-115`; EF-032 |

### CMP-012 — Playbook authoring loop

| Field | Value |
|-------|--------|
| NAME | Heal → (draft) → lint? → save → resolve/auto |
| CLASS | OPERATOR_WORKFLOW; DIRECT dry-run on save; SHARED resolver after save |
| STEPS | `occam_playbook_heal` → **human/agent writes JSON** → optional `occam_playbook_lint(playbook_json)` → `occam_playbook_save(url, playbook_json)` → later resolve/transcode/extract |
| WHAT FLOWS | Heal: DOM skeleton + candidates (ART-016) — **not** save input; lint/save: **`playbook_json` string**; save → disk ART-015 + `ClearCacheForTests`; resolve reads tiers |
| ACHIEVES | Site recipe authoring with quality gate dry-run |
| WHO SEQUENCES | Agent/human (draft is outside code) |
| TRUST EFFECT | Save **always signs** (EF-005); lint grade does **not** gate save |
| FAILURE | Heal fail → no draft materials; save verify fail → reject; lint broken ≠ save blocked |
| FLOW | FLOW-005 |
| EVIDENCE | `OccamPlaybookHealTool.cs:13`; `OccamPlaybookLintTool.cs:18-20`; `PlaybookSaveService.cs:49-94`; `PlaybookSaveVerifier.cs:24` |

### CMP-013 — Ambient client token budget

| Field | Value |
|-------|--------|
| NAME | Client_capabilities → later max_tokens default |
| CLASS | IMPLICIT_COMPOSITION |
| STEPS | `occam_client_capabilities(context_tokens=…)` once → later `occam_transcode`/`digest` omit `max_tokens` → store supplies budget |
| WHAT FLOWS | Process-memory ART-023 (`OutputBudgetTokens`); also env `OCCAM_CLIENT_CONTEXT_TOKENS` bootstrap |
| ACHIEVES | Session-wide sizing without per-call token math |
| WHO SEQUENCES | Occam (after configure); agent must call once |
| TRUST EFFECT | **Degrades hash stability** if budget changes between comparable reads (affects contentHash / cache identity) |
| FAILURE | Unconfigured → full payload; explicit `max_tokens` wins |
| FLOW | FLOW-001 prefix; **FLOW-NEW-P508-1** |
| EVIDENCE | `ClientCapabilityStore.cs:70-78`; `OccamTranscodeTool.cs:48,78` |

### CMP-014 — Operator install/doctor/connect

| Field | Value |
|-------|--------|
| NAME | Install → doctor → onboard/verify → connect |
| CLASS | OPERATOR_WORKFLOW |
| STEPS | Bootstrap install → `occam doctor` → verify-install / onboard → `occam connect` |
| WHAT FLOWS | ART-029/030/031 host config; ART-032 tarball; `OCCAM_HOME` |
| ACHIEVES | Runnable MCP host wired into an IDE — no MCP tool does this |
| WHO SEQUENCES | Operator |
| TRUST EFFECT | Supply-chain / host-config trust (sha256 tarball; cosign unused EF-053); onboard-before-verify EF-029 |
| FAILURE | Doctor fail blocks readiness; connect rollback gaps EF-021 |
| FLOW | FLOW-015, FLOW-016 |
| EVIDENCE | FLOW ledger; ENTRYPOINT-MODEL; SHIPPED-CODE-MAP |

### CMP-015 — Batch job lifecycle (no dataset bridge)

| Field | Value |
|-------|--------|
| NAME | Batch submit → status → results |
| CLASS | DIRECT / OPERATOR within batch surface only |
| STEPS | `occam_batch_submit` → poll status → results **or** BatchServer HTTP |
| WHAT FLOWS | **job_id**; markdown result JSON — **not** Receipt v1; **not** dataset rows |
| ACHIEVES | Async multi-URL extract |
| WHO SEQUENCES | Agent (opt-in env) |
| TRUST EFFECT | **No** Receipt v1 (EF-037) — trust dead-end |
| FAILURE | Job/item failures in store; cross-process clobber EF-038 |
| FLOW | FLOW-012 |
| EVIDENCE | `BatchJobService.cs:40-76`; EF-037 |

---

## 3. Rejected candidates (explicit)

| Candidate | Why rejected | Do instead |
|-----------|--------------|------------|
| **transcode markdown → extract_knowledge** | Extract accepts only `url` (+ policy/session); css-extract spine; no markdown/receipt param (`OccamExtractKnowledgeTool.cs:16-19`) | `playbook_resolve` (gate schema) → `extract_knowledge(url)` (FLOW-006) |
| **batch results → dataset_export** | Dataset API is URL list only (`DatasetExportService.cs:10-14`); no job_id; batch lacks receipts | Pass original URLs to `dataset_export`; or N× transcode with receipts |
| **claim_check JSON → attest input** | Attest input is claims+URLs; internally re-runs claim-check (`AttestService.cs:67-69`) | Call `occam_attest` alone |
| **facts[] → claim_check** | No code bridge (CODE-DERIVED-WORKFLOWS “NOT proven”) | Re-state claim against URL via claim_check/attest |
| **heal response → save playbook_json** | Heal returns skeleton/candidates; save requires author-drafted JSON | Agent drafts JSON from heal evidence → lint → save |
| **MCP verify of dataset manifest** | Manifest mode is CLI-only (`OccamCliVerbs.cs:240` vs `OccamVerifyTool.cs:30`) | CLI `occam verify --mode manifest`; MCP only per-row receipts |
| **crosscheck verdict → occam_verify** | No verify mode re-derives consensus (EF-032) | Manually inspect per-vantage receipts; do not treat verdict as proof |
| **extract_knowledge.Receipt → occam_verify** | Field is `{confidence, elapsedMs}` not Receipt v1 (CAP-287, EF-006, `OccamExtractKnowledgeReceiptInfo`) | Use transcode/claim/dataset receipts |

---

## 4. Required analyses

### 4.1 Composition graph — connective tissue

Identifiers that **cross a tool/service boundary**:

| ID / artifact | Produced by | Consumed by | Notes |
|---------------|-------------|-------------|-------|
| **URL** / finalUrl | Almost all discovery + fetch tools | All acquisition tools | Primary join key |
| **session_profile name** | `occam-session` CLI (ART-026) | Tools with `session_profile` param | String id, not cookie blob |
| **playbook id / hosts** | save / resolve / seeds | resolve, auto-transcode, extract schema match | Disk + in-memory cache |
| **playbook_json** | Agent draft (+ lint echo) | lint, save | Heal does **not** emit this |
| **koId** (`meta.koId`) | extract_knowledge | Agent only | No downstream Occam consumer |
| **contentHash** / `sha256:…` | transcode/digest/watch/dataset | `if_none_match`, watch compare, verify | Hash of **compiled** markdown (TRUST C2) |
| **materializationKey** / blockHashes | transcode diff sidecars | `diff_against`, watch | Per-materialization, not URL alone |
| **receipt / signed envelope** | transcode, digest items, claim, dataset rows, watch (opt) | `occam_verify`, CLI verify | ART-007 |
| **capsule `occam://…`** | transcode `emit_capsule` | verify (parses as receipt) | ART-006 |
| **block leaf + Merkle proof** | claim_check / verify prove | verify citation | ART-019 → ART-021 |
| **dataset manifest + rowLeaf** | dataset_export | CLI manifest verify; rows→MCP verify | ART-022 |
| **watch history[] / watch store key** | occam_watch | verify history | ART-025/028 |
| **job_id** | batch submit | batch status/results | ART-027 — **dead-end for trust/dataset** |
| **ambient budget (ART-023)** | client_capabilities / env | transcode/digest omitted max_tokens | Process memory |
| **map links[].url** | occam_map | digest urls / agent | ART-011 |
| **search result urls** | occam_search | probe/transcode/digest | ART-013 |
| **probe extractability** | search rerank | sort order only | Not a separate tool param |

```
URL ──► probe ──hints──► agent ──► transcode ──receipt──► verify
 │         ▲                         │
 │         └── rerank ── search      ├── blocks ──► claim_check ──leaf+proof──► verify
 │                                   │                 ▲
 │                                   │                 └── DIRECT ── attest
 │                                   ├── capsule ──► verify
 │                                   └── contentHash ──► if_none_match / watch
 │
URL ──► map.links ──► digest(urls)     digest(source_url) ──DIRECT──► MapService
URL ──► resolve (plan) ──► extract_knowledge(url)   [no markdown bridge]
URL ──► dataset_export ──manifest──► CLI verify     [MCP: row receipts only]
session_id ──► session_profile param ──► Tiered fetch
job_id ──► batch results ── ✗ ──► dataset
heal.skeleton ── ✗ ──► save;  agent draft playbook_json ──► lint?/save ──► disk ──► resolve
client_capabilities ──ART-023──► ambient max_tokens
crosscheck ──vantage receipts──► (manual); verdict ── ✗ ──► verify
```

### 4.2 Broken or half-wired compositions

| Gap | Implied by design/name | Code reality | IDs |
|-----|------------------------|--------------|-----|
| Crosscheck receipt re-derive | “Verdict re-derivable from receipts” (ConsensusService comment) | No tool/CLI mode reconstructs verdict | EF-032, CAP-856/867, FLOW-014 |
| extract_knowledge `Receipt` | Looks like Receipt v1 | `{confidence, elapsedMs}` telemetry | CAP-287, EF-006, ART-014 |
| Batch → auditable corpus | Natural “results then dataset” | No bridge; batch unsigned | EF-037, REJECT above |
| Heal → save | Tool description “then save” | Missing automated JSON emitter | FLOW-005 limitation |
| Lint → save gate | “Lint before save” | Advisory only; save has own schema+verify | ART-018 orphan consumer |
| Dataset set verify in MCP | Export advertises set verify | Manifest = CLI only | FLOW-008 |
| facts[] → claim_check | Knowledge→claims pipeline | No bridge | CODE-DERIVED-WORKFLOWS |
| Live verify of sessioned read | Prove same authenticated page | Live drops session/playbook/budget | TRUST C11, FLOW-001 limit |
| Attest / consensus aggregates | Cryptographic attestation/consensus | Unsigned aggregates | GAP-028 |
| Cosign release → install | Signed releases | Install uses sha256 map; cosign orphan | EF-053, ART-038 |

### 4.3 Compositions that silently degrade trust

| Chain | Degradation | Evidence |
|-------|-------------|----------|
| Materialize then hash/sign | `contentHash` / Merkle cover **compiled** markdown after budget/fit/focus/translate — not raw page | TRUST-MODEL C2; `ReceiptCanonicalizer.cs:17-18` |
| Cache replay (FLOW-019) | Full envelope incl. receipt replayed; fragment omitted from cache key → wrong-section collision | EF-045, FLOW-019/020 |
| Session Tier-2/3 | Same `session_profile` id; storageState dropped on probe/map/heal/extract | ACQUISITION-ROUTING-MODEL Tier table |
| claim_check then attest | Double live fetch → divergent leaves/receipts possible | `AttestService` re-fetch |
| Crosscheck “agreement” | Unsigned verdict; same egress IP | CAP-856, EF-032 |
| Reader profile | Can obtain receipts; `occam_verify` hidden | TRUST D9 / profile matrix |
| OCCAM_RECEIPTS=off + playbook_save | Save still signs; key still minted | EF-005, EF-044 |
| Watch history verify of unsigned entries | Sig check skipped | TRUST-MODEL history mode note |
| Ambient budget change mid-session | Alters compiled bytes → breaks hash continuity for `if_none_match`/receipt compare | CMP-013 |

### 4.4 Canonical top workflows (learn-order)

Ranked for a **real user/agent** (core path first; opt-in last):

1. **CMP-013** — Session start: `client_capabilities` (ambient budget)  
2. **CMP-005** — Read one page + optional verify (`transcode` → receipt)  
3. **CMP-001** — Probe then transcode when URL quality unknown  
4. **CMP-003** — Map/search → digest for multi-URL research  
5. **CMP-002** — Search with `rerank=true` when discovering from query  
6. **CMP-009** — Session profile for login-walled pages  
7. **CMP-004 / FLOW-006** — Resolve → extract_knowledge for typed fields  
8. **CMP-006 / CMP-007a** — Claim_check or attest for citation grounding  
9. **CMP-008** — Dataset export + CLI manifest verify for corpus handoff  
10. **CMP-012** — Heal → draft → lint → save when authoring playbooks  
11. **CMP-010** — Watch + history verify (needs `OCCAM_WATCH_MCP`)  
12. **CMP-011 / CMP-015** — Crosscheck / batch only when opt-in env justified  

### 4.5 Anti-compositions

| User tries | Fails because | Instead |
|------------|---------------|---------|
| Feed transcode markdown into extract_knowledge | No such param | extract_knowledge(url) after resolve |
| Feed batch results into dataset_export | URLs-only API; no receipts on batch | dataset_export(urls) live |
| Pass claim_check response into attest | Wrong shape; would double-fetch anyway | attest alone |
| Pass extract `Receipt` to verify | Not Receipt v1 | transcode/claim/dataset receipts |
| Expect MCP verify of dataset manifest | Mode missing | CLI `--mode manifest` |
| Expect crosscheck verdict to verify | No mode | Treat as advisory; use vantage receipts manually |
| Heal JSON straight into save | Heal ≠ playbook_json | Draft JSON; lint; save |
| Rely on lint `ready` ⇒ save success | Independent parsers/gates | Always run save verify |
| Live-verify a sessioned extract as identical | Session dropped on live | Offline/capsule verify; or re-run with same session_profile manually |
| facts[] into claim_check | No bridge | claim_check(claim, url) |
| Watch without env, then expect tool | Tool not registered | Set `OCCAM_WATCH_MCP=1`; history verify still works on stored ART-025 |
| Search without provider → probe | Search fails closed | Configure `OCCAM_SEARCH_PROVIDER` or start from known URLs |

---

## 5. Counts and FLOW mapping

| Class (primary tag on INCLUDE rows) | Count |
|-------------------------------------|------:|
| DIRECT_COMPOSITION | 4 (CMP-002, CMP-003-direct leg, CMP-007a, CMP-011) |
| SHARED_SUBSYSTEM | 2 (CMP-004, CMP-012 post-save) — often secondary |
| ARTIFACT_HANDOFF | 7 (CMP-001 advisory, CMP-003 map→digest, CMP-005, CMP-006, CMP-008, CMP-009, CMP-010 history) |
| OPERATOR_WORKFLOW | 5 (CMP-001, CMP-007b, CMP-012, CMP-014, search-without-rerank) |
| IMPLICIT_COMPOSITION | 3 (CMP-005 sign, CMP-010 diff, CMP-013 budget) |
| **Rejected candidates** | **8** (table §3) |

Composition records CMP-001…015: **15 INCLUDE** (007 split a/b).  
Map to existing FLOW-001…022: **14** compositions touch existing FLOWs.  
New workflow id allocated: **FLOW-NEW-P508-1** (ambient budget as first-class composition; also noted under FLOW-001).  
No additional FLOW-NEW required for rejected chains (documented as anti-compositions).

---

## 6. Taxonomy verdict (PS hypothesis)

**Accept PS-1…9** as exposure buckets. Composition evidence **reinforces** PRODUCT-ARCHITECTURE: value lives in **L7 higher workflows** and **cross-spine handoffs**, not in “15 tools” alone. Correction: treat **ARTIFACT_HANDOFF vs DIRECT** as documentation-critical — several named “pipelines” are agent-only or half-wired (EF-032, CAP-287, batch→dataset).

---

## 7. EFC / UNCERTAIN

| ID | Note |
|----|------|
| EFC | none new — EF-005/006/011/032/037/044/045/053 already cover composition trust holes |
| UNCERTAIN | Whether any out-of-repo host adapter auto-sequences install→doctor→connect beyond scripts (operator surface varies by host) — does not affect MCP composition graph |

---

## 8. Corrections to prior model

1. **claim_check → attest** is not an artifact handoff; attest is DIRECT over ClaimCheckService.  
2. **transcode → extract_knowledge** is not a markdown pipeline.  
3. **batch → dataset** is not supported.  
4. **digest ↔ map** is asymmetric (DIRECT only digest→map).  
5. **Crosscheck** creates vantage receipts but **not** a verifiable consensus artifact (EF-032).  
6. **extract_knowledge.Receipt** must never appear in trust composition diagrams as ART-007.
