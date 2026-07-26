# ARTIFACT-ONTOLOGY (Phase 5H)

**Agent:** P5-04 — Artifact Ontology  
**Question answered:** *What objects actually flow through Occam?*  
**SoT precedence:** executable code → Wave-4 correction layer → `ARTIFACT-MAP.md` (ART-001…039) → subsystem/tool evidence.  
**ID discipline:** ART-001…039 appear **exactly once**. No renumbering. New candidates use `ART-NEW-P504-<n>` only.

Corpus coverage: **39 / 39** ART IDs.

---

## 0. Family taxonomy (evidence verdict)

Candidate families tested: CONTENT · STRUCTURE · KNOWLEDGE · DISCOVERY · SESSION · TRUST · PROVENANCE · VERIFICATION · DATASET · MONITORING · OPERATOR · CONFIGURATION.

| Decision | Family | Justification |
|----------|--------|---------------|
| **KEEP** | CONTENT | Agent-facing page text (compiled markdown, digest combined, translation). |
| **KEEP** | STRUCTURE | Opt-in / forced structured sidecars (`blocks[]`, `tables[]`, `feed`, `chunks[]`). |
| **KEEP** | KNOWLEDGE | Schema/playbook-driven extract + authoring intermediates (facts, heal, resolve, lint, local playbook JSON). |
| **KEEP** | DISCOVERY | Pre-fetch / link / search surfaces that do not materialize full page bodies. |
| **KEEP** | TRUST | Cryptographic commitments + signing material (Receipt v1, capsule, TSA, key PEM, contentHash/mat-key). **Includes** what candidates called PROVENANCE. |
| **MERGE → TRUST** | PROVENANCE | Code uses one `ReceiptSigner` + four hand-rolled canonicalizers (`ReceiptCanonicalizer`, `PlaybookSignature`, `DatasetManifestBuilder`, `WatchHistoryCanonicalizer`). No separate provenance product surface. |
| **KEEP** | VERIFICATION | Consumer-side verdicts / proofs returned by claim_check, attest, verify — distinct *role* from trust *objects*. |
| **KEEP** | DATASET | Multi-row export + detached manifest (one ART). |
| **KEEP** | MONITORING | Watch history chain + watch store. |
| **KEEP** | SESSION | Operator session profiles + retained import cookies (credential-bearing). |
| **SPLIT from CONFIGURATION** | RUNTIME | Process/host ephemeral-or-disk state that is neither packaging nor credentials: ambient budget, batch job snapshot, response cache, temp CSS field-spec. |
| **KEEP (narrowed)** | OPERATOR | Install/connect/skill/release packaging + host IDE config. Batch jobs live in RUNTIME (job state), not OPERATOR. |
| **REJECT as top-level** | CONFIGURATION | Ambient env/config is a *property* of many artifacts; the only configuration-*like* ARTs are better placed under RUNTIME / OPERATOR. |

**Final families (11):** CONTENT · STRUCTURE · KNOWLEDGE · DISCOVERY · TRUST · VERIFICATION · DATASET · MONITORING · SESSION · RUNTIME · OPERATOR.

| Family | Count | ART IDs |
|--------|------:|---------|
| CONTENT | 3 | 001, 010, 039 |
| STRUCTURE | 4 | 002, 003, 004, 005 |
| KNOWLEDGE | 5 | 014, 015, 016, 017, 018 |
| DISCOVERY | 3 | 011, 012, 013 |
| TRUST | 6 | 006, 007, 008, 009, 024, 034 |
| VERIFICATION | 3 | 019, 020, 021 |
| DATASET | 1 | 022 |
| MONITORING | 2 | 025, 028 |
| SESSION | 2 | 026, 037 |
| RUNTIME | 4 | 023, 027, 035, 036 |
| OPERATOR | 6 | 029, 030, 031, 032, 033, 038 |
| **Total** | **39** | 001…039 |

Column legend used below:

| Column | Conservative rule |
|--------|-------------------|
| **SIGNED?** | ECDSA (`ReceiptSigner.Sign` / `SignDetached`) over canonical bytes. `No` unless that path is live. |
| **HASHED?** | Object carries / is identified by SHA-256 (or Merkle root of SHA-256 leaves). |
| **VERIFIABLE?** | A shipped verifier (`occam_verify` / CLI `verify` / `DatasetManifestBuilder.Verify` / `WatchHistoryChain.Verify` / playbook signature inspect) can check integrity **without trusting the producer process**. Telemetry fields named "Receipt" are **not** verifiable. |

---

## 1. Ontology table (all 39)

### 1.1 CONTENT

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-001 | Markdown extract (compiled) | `TranscodePipeline` via transcode / digest items / claim / attest / dataset / watch / verify-live | Agents; `contentHash` / receipts; verify live drift | No (response; may ride inside ART-035) | Yes (text) | No (body unsigned; receipt may bind it) | Yes via ART-024 `contentHash` | Indirect: only if paired with ART-007 + markdown | Yes | Yes (string) | ephemeral-in-response (or cache-replay) | → ART-007, ART-024, ART-002 (reconcile), ART-035 | `TranscodePipeline.cs` finish path; `OccamTranscodeTool.cs:273+` | CAP-306..311 |
| ART-010 | Digest combined + items | `occam_digest` / `DigestService` | Agent; `if_none_match` on combined | No | Yes | Per-item reduced Receipt v1 when receipts on | Combined uses content hash for AF-6 | Per-item receipts yes; combined envelope not a separate Receipt schema | Yes | Yes | ephemeral-in-response | items → ART-001/007; discovery via ART-011 engine | `OccamDigestTool.cs:17-30`; DigestService | CAP digest family |
| ART-039 | `translatedMarkdown` | `TranslationService` when `translate_to` + `OCCAM_TRANSLATE_URL` | Agent only | No | Yes | **No** | **No** | **No** — lossy sidecar; warnings on fail | Yes (when produced) | Yes | ephemeral-in-response | sibling of ART-001; never bound into ART-007 | `OccamTranscodeTool.cs:60,198-207,341` | — |

### 1.2 STRUCTURE

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-002 | Structured `blocks[]` | Worker DOM blocks + `BlockReconciler`; forced on claim/attest/dataset/watch | claim_check ranker; Merkle leaves; diff; verify prove/citation | No (unless ART-035) | Partial | No | Leaf hashes → `blockMerkleRoot` | Merkle membership via ART-019/021 when leaves+root present | Transcode yes; claim projects matches | Yes | ephemeral-in-response | → ART-007 root; → ART-019 | `dom-blocks.mjs`; `BlockReconciler.cs`; CAP-078/316 | CAP-078, CAP-252, CAP-316 |
| ART-003 | `tables[]` / records | Transcode `json_tables` | Agent | No | Yes | **No** | **No** dedicated proof | **No** | Transcode only | Yes | ephemeral-in-response | budget-sibling of ART-002 | CAP-318 | CAP-318 |
| ART-004 | `feed` object | Transcode `json_feed` / feed short-circuit | Agent | No | Yes | **No** | **No** | **No** | Transcode | Yes | ephemeral-in-response | — | CAP-319 | CAP-319 |
| ART-005 | `chunks[]` | Transcode `semantic_chunking` (fixed-size chunker despite name) | Agent; verify live chunk-staleness opt-in | No | Yes | **No** | Compared as text in live mode | Soft: staleness eval, not crypto | Transcode | Yes | ephemeral-in-response | consumed by ART-021 live | CAP-320; `OccamVerifyTool` chunks param | CAP-266, CAP-320 |

### 1.3 KNOWLEDGE

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-014 | Knowledge `facts[]` (+ fake `Receipt`) | `occam_extract_knowledge` / css-extract | Agent only (**not** claim_check) | No | Yes | **No** — field named `Receipt` is `{confidence, elapsedMs}` telemetry | **No** | **No** — **not** Receipt v1 | Yes | Yes | ephemeral-in-response | schema from ART-015/017; temp plan ART-036 | `OccamExtractKnowledgeTool.cs:88,111-115,129` | **CAP-287, EF-006** |
| ART-015 | Playbook JSON (local) | `occam_playbook_save` → `PlaybookSignature.BuildSignedJson` | resolve; `playbook_policy=auto`; extract schema | Yes (`OCCAM_PLAYBOOKS_LOCAL_ROOT`) | File | **Yes — always** (ignores `OCCAM_RECEIPTS`) | contentHash inside provenance | Self-key inspect only (TOFU) | Full profile | Yes | persistent-on-disk | signed by ART-034; lint ART-018 advisory | `PlaybookSaveService.cs:86-105` | CAP-280/285, **EF-005** |
| ART-016 | Heal skeleton / candidates | `occam_playbook_heal` | Human/agent → draft → save | No | Yes | **No** | **No** | **No** | Full profile | Yes | ephemeral-in-response | optional → ART-015 | DomSkeletonWorker path | CAP-543 |
| ART-017 | Resolve overlay / genome | `occam_playbook_resolve` (+ optional well-known fetch) | Agent planning; auto-transcode resolver | Optional live genome in memory cache | JSON portable | Inspect local signature only | Community tier uses unsigned sha256 manifest map (not this ART) | Signature inspect ≠ third-party trust | Full profile | Yes | process cache + response | feeds ART-014/001 auto | WellKnownGenomeFetcher; resolve tool | CAP genome family |
| ART-018 | Lint grade/issues | `occam_playbook_lint` | Author before save | No | Yes | **No** | **No** | Advisory only; may disagree with save/resolve | full+auditor | Yes | ephemeral-in-response | advisory vs ART-015 | PlaybookLinter | CAP-758 note: Sanitizer Core-dead EF-047 |

### 1.4 DISCOVERY

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-011 | Map link list | `occam_map` | Digest `source_url` discovery (shared engine); agent | No | Yes | **No** | **No** | **No** | Yes | Yes | ephemeral-in-response | → ART-010 discovery | MapService | CAP-527 |
| ART-012 | Probe diagnosis | `occam_probe` | search rerank; agentHints | No | Yes | **No** | **No** | **No** | Yes | Yes | ephemeral-in-response | → ART-013 scoring | ProbeService | CAP-423/428 |
| ART-013 | Search hits | `occam_search` | Agent → probe/transcode/digest | No | Yes | **No** | **No** | **No** | Yes | Yes | ephemeral-in-response | uses ART-012 scores | search providers | CAP-628 |

### 1.5 TRUST

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-006 | Capsule `occam://capsule/…` | Transcode `emit_capsule` packages signed envelope + content + leaves | `occam_verify` (any mode that parses capsule) | No (in response; **also** inside ART-035 if cached) | Yes (URI string) | Envelope **inside** is signed; **packaging bytes are not** | contentHash + optional Merkle | Offline verify of inner ART-007 | Yes | Yes | ephemeral-in-response | wraps ART-007 + ART-001 + leaves; optional ART-009 | `CapsuleCodec.cs:5-65` | CAP-274 |
| ART-007 | Receipt v1 (positive) | transcode, digest items, claim_check, dataset rows, watch/crosscheck (ReceiptsPolicy) | `occam_verify` offline/live/prove/citation | Ephemeral unless caller stores; replayable via ART-035 | Yes JSON | **Yes** ECDSA when policy on | contentHash + optional blockMerkleRoot | **Yes** (sig ± content) | Yes when receipts on | Yes | ephemeral-in-response / caller-stored | signed by ART-034; may include ART-009; leaves from ART-002 | `ReceiptModels.cs:12-53`; `ReceiptSigner.Sign` | CAP-250..259; C6 / EF-005/044 for policy limits |
| ART-008 | Negative receipt | Failure paths (subset of codes) | verify offline | Ephemeral | Yes | **Yes** when issued | No content hash | **Yes** (sig over negative envelope) | Yes | Yes | ephemeral-in-response | same schema Kind=`negative` | `ReceiptModels.cs:51`; BuildNegativeReceipt call sites | CAP-269 |
| ART-009 | Time-anchor sidecar | Transcode when `OCCAM_TIME_ANCHOR` + `OCCAM_TSA_URL` | verify offline TimeAnchor path | Ephemeral | Yes | Token is TSA-issued; **not** host-ECDSA over sidecar | Imprint = SHA-256(sig bytes) | **Partial** — bind check; chain-to-root cut | Advanced | Yes | ephemeral-in-response | attaches to ART-007 | `TimeAnchorService.cs:16-68`; `ReceiptTimeAnchor.cs` | CAP-261 |
| ART-024 | MaterializationKey / contentHash | Compile after materialize | Client stores pair; `if_none_match` uses **contentHash only**; receipts embed contentHash | No | Yes (hex strings) | **No** | **Yes** SHA-256 | Hash compare only (not a signature) | Advanced | Yes | ephemeral-in-response | contentHash → ART-007; **MaterializationKey is NOT the cache key** (Wave-4/C correction) | `ContentHashToken.cs:13-31`; `MaterializationKey.cs:13-40`; vs `TranscodeCacheKey` | CAP-314; **EF-045** fragment gap |
| ART-034 | Host signing key PEM | `ReceiptSigner.LoadOrCreate` on DI / keys export | All Sign / SignDetached sites | Yes `~/.occam/keys/signing-key.pem` (or `OCCAM_KEYS_ROOT`) | Machine-local private; public half portable | N/A (is the key) | KeyId = SHA-256(SPKI)[..16] | Public half verifies ART-007/015/022/025 | Operator (private); KeyId public | PEM | persistent-on-disk | produces sigs for ART-007/008/015/022/025 | `ReceiptSigner.cs:26-45,80-98`; `OccamServiceCollectionExtensions.cs:23` | CAP-254/255, **EF-044** |

### 1.6 VERIFICATION

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-019 | Claim matches + citation proofs | `occam_claim_check` | `occam_verify` citation/prove; `occam_attest` | No | Yes | Nested ART-007 optional (ReceiptsPolicy); proofs are Merkle | Leaf+proof | Merkle proves **membership**, not claim truth | researcher+ | Yes | ephemeral-in-response | uses ART-002/007; → ART-020 | ClaimCheckService | CAP claim family |
| ART-020 | Attest status batch | `occam_attest` | Agent gate on `status` | No | Yes | **Aggregate unsigned**; nested claim receipts optional | Consumes Merkle proofs | Merkle ≠ semantic truth; classifier is heuristic | auditor+ | Yes | ephemeral-in-response | aggregates ART-019 | `AttestModels.cs:58-73`; `AttestService.cs:24-100` | CAP-728 |
| ART-021 | Verify verdict | `occam_verify` (+ CLI) | Agent / third party | No | Yes | Is the verifier (does not newly sign) | Validates hashes/proofs | **Is** the verification result | researcher+ | Yes | ephemeral-in-response | consumes ART-006/007/008/009/019/025 | `OccamVerifyTool.cs` | CAP-268..273, CAP-650..653 |

### 1.7 DATASET

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-022 | Dataset rows + manifest | `occam_dataset_export` | CLI `verify --mode manifest`; rows via MCP verify | Response only (caller stores) | Yes | Manifest `SignDetached` when receipts on; row ART-007 | Manifest Merkle over row leaves | Manifest CLI-only; rows via verify | auditor+ | Yes | ephemeral-in-response / portable-export if saved | rows embed ART-001/002/007 | `DatasetExportService.cs:45-106`; `DatasetManifest.cs` | CAP-283, CAP-770..779, EF-018 |

### 1.8 MONITORING

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-025 | Watch history chain | `WatchHistoryChain.Append` on first_seen/changed | `occam_verify` mode=history | Inside ART-028 | Yes (JSON array) | Entry `SignDetached` when ReceiptsPolicy on | prevEntryHash chain + contentHash + Merkle root | Chain verify (window-aware) | Opt-in tool | Yes | persistent-on-disk (capped 64) | stored in ART-028; signed by ART-034 | `WatchHistory.cs`; `WatchService.cs:99-165` | CAP-836..844 |
| ART-028 | Watch store | `WatchStore.Persist` | watch Get/Upsert; history verify needs exported history | Yes `~/.occam/watch/watch.json` | Host-local file | Store file unsigned; entries may be | ContentHash + blockHashes | Via exported ART-025 | Disk; response surfaces fields | Yes | persistent-on-disk; **no Remove API** | contains ART-025 | `WatchStore.cs:30-38,112-113` | CAP-840, **EF-020** |

### 1.9 SESSION

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-026 | Session profile files | `occam-session` CLI | Tools with `session_profile` (tiered — CAP-880) | Yes under sessions root | Operator-local (secrets) | **No** | **No** | N/A | Operator | Yes JSON | persistent-on-disk | may reference storageState; imports → ART-037 | `occam-sessions-lib.mjs`; `SessionProfileHeaders.cs` | CAP-880..885 |
| ART-037 | Session import raw cookies | `occam-session` import (`--no-keep-import` to skip) | Human/operator (not auto-consumed by host) | Yes `sessions/_imports/` | **Sensitive copy** | **No** | **No** | **No** | Operator filesystem | Yes (plaintext) | persistent-on-disk | sibling of ART-026 | `occam-sessions-lib.mjs:105,159-162`; `occam-session.mjs:32` | **EF-054**, GAP-038 |

### 1.10 RUNTIME

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-023 | Client ambient budget | `occam_client_capabilities` / `OCCAM_CLIENT_CONTEXT_TOKENS` | transcode/digest omitted `max_tokens` | Process memory | N/A | **No** | **No** | **No** | All profiles (tool response) | Yes | process-lifetime | shifts ART-024/cache identity indirectly | `ClientCapabilityStore.cs:39-78` | CAP-304 |
| ART-027 | Batch job snapshot | `occam_batch_*` / `--batch-server` | status/results / HTTP clients | Yes (`jobs.json` beside DB path) | Host-local | **No Receipt v1** | **No** | **No** | Opt-in | Yes | persistent-on-disk; **no eviction** | markdown retained; ≠ ART-007 | `JsonFileBatchJobStore.cs:29,367+`; BatchSettings | CAP-804, **EF-037/038** |
| ART-035 | Transcode response cache entry | Eligible transcode success (`cache_ttl_s`) | Later transcode lookup | Yes (`OCCAM_CACHE_DIR` / temp) | Host-local | Replays prior signed envelope **as opaque JSON** | Key via `TranscodeCacheKey` (≠ ART-024) | Replay ≠ re-verify | `cached:true` flag | Yes | persistent-on-disk until TTL read | may contain ART-001/002/006/007/039 | `TranscodeResponseCache.cs:24-145`; `OccamCacheEntry` | CAP-321, **EF-045**, CAP-315 |
| ART-036 | Temp CSS field-spec JSON | `CssExtractWorker` | css-extract worker argv | Temp file | Host temp only | **No** | **No** | **No** | No (internal) | Yes | temp-file (best-effort cleanup) | carries plan for ART-014 | `CssExtractWorker.cs:23-47` | CAP-NEW-C missing-art prior |

### 1.11 OPERATOR

| ART | Name | PRODUCER | CONSUMER | PERSISTED? | PORTABLE? | SIGNED? | HASHED? | VERIFIABLE? | USER_VISIBLE? | MACHINE_READABLE? | LIFETIME | RELATIONSHIPS | Evidence | CAP / EF |
|-----|------|----------|----------|------------|-----------|---------|---------|-------------|----------------|-------------------|----------|---------------|----------|----------|
| ART-029 | Onboard state | `occam-onboard.mjs` | launcher / connect env injection | Yes `~/.occam/onboard.json` | Host-local | **No** | **No** | **No** | Operator | Yes | persistent-on-disk | written **before** verify | `onboard-schema.mjs:21` | **EF-029** |
| ART-030 | Connect last-run | `occam connect` | installer snippet-skip | Yes `connect-last.json` | Host-local | **No** | **No** | **No** | Operator | Yes | persistent-on-disk | — | `get-ff-occam.ps1:215` | — |
| ART-031 | Host MCP config + bak | Connect CONFIG_FILE adapters | Host IDEs/CLIs | Yes | Host-local | Host-side | **No** Occam crypto | Host-dependent | Operator | Yes | host-config | rollback often dead | connect adapters | **EF-021** |
| ART-032 | Level B tarball + manifest | `build-release` / GH Releases | install Level B / get-ff-occam | Yes (release assets) | Yes | Manifest is **sha256 map**, not ECDSA Receipt | **Yes** sha256 | Install verifies sha256; **not** cosign by default | Users/operators | Yes | portable-export | real ship path; npm unpublished | packaging subsystem | CAP-1026 |
| ART-033 | Skill card | `skills/occam` / skill install | Agents reading skill | Yes (harness dirs) | Yes | **No** | **No** | **No** | Agents | Yes (markdown) | portable-export | stale version/tool-count | `install-occam-skill.mjs` | **EF-036** |
| ART-038 | Cosign release `.bundle` | `sign-release.yml` | **Nothing in shipped install path** (manual only) | Yes (GH Release asset) | Yes | Cosign keyless | Bundle | Manual `cosign verify-blob` only | Release page | Yes | portable-export (orphaned) | **dead consumer** vs ART-032 | `sign-release.yml:83-115`; EF-053 | CAP-1028, **EF-053** |

---

## 2. Flow diagram (artifact handoff graph)

```
DISCOVERY                ACQUISITION HUB                 MATERIALIZE
ART-012 probe ─┐         TranscodePipeline /             ART-001 markdown
ART-011 map ───┼────────► OccamRouter ─────────────────► ART-002 blocks ──┐
ART-013 search ┘         (claim/attest/dataset/watch/     ART-003 tables   │
                         digest/verify-live also)         ART-004 feed     │
                                                          ART-005 chunks   │
                                                          ART-039 translate│
                                                          ART-024 hashes   │
                                                                 │         │
KNOWLEDGE (bypass hub)                                           ▼         │
ART-017 resolve ─► ART-015 playbook ─► ART-014 facts                      │
ART-016 heal ─────► (human) ─────────► ART-015                            │
ART-018 lint ─────► (advisory)                                            │
ART-036 temp field-spec ─► css-extract ─► ART-014                         │
                                                                          │
TRUST LAYER ◄─────────────────────────────────────────────────────────────┘
ART-034 signing-key ─► Sign ─► ART-007/008 receipt
                    ├─► SignDetached ─► ART-015 provenance
                    ├─► SignDetached ─► ART-022 manifest
                    └─► SignDetached ─► ART-025 history entries
ART-007 (+ leaves) ─► ART-006 capsule
ART-007 ─► ART-009 time-anchor (opt-in TSA)
ART-035 cache ◄── full success envelope (may include 001/002/006/007/039)

VERIFICATION
ART-019 claim ◄── ART-002 + ART-007
ART-020 attest ◄── ART-019
ART-021 verify ◄── ART-006/007/008/009/019/025

MONITORING
occam_watch ─► ART-028 store ◄── ART-025 chain ─► ART-021 history

SESSION / RUNTIME / OPERATOR (side channels)
ART-026 profile ─► fetch headers/storageState (tiered)
ART-037 raw cookies (import retain; not host-consumed)
ART-023 ambient budget ─► default max_tokens
ART-027 batch jobs (markdown; no Receipt v1)
ART-029/030/031 onboard+connect+host config
ART-032 tarball ◄── build-release; ART-038 cosign orphan
ART-033 skill card ─► agent harnesses
```

### 2.1 Produced but never consumed inside Occam (dead / orphan outputs)

| ART | Status | Evidence |
|-----|--------|----------|
| ART-038 | **Dead consumer** — produced on GH Release; install uses sha256 manifest of ART-032, not cosign | EF-053 |
| ART-037 | Retained on disk; **no Core reader** of `_imports/` | EF-054 |
| ART-018 | Advisory only — save/resolve do not require lint pass | playbook lint audit |
| ART-033 | Consumed by external harnesses, not by Core runtime | skill install |
| ART-039 | Agent-only; never enters receipt canonical bytes | `OccamTranscodeTool.cs:198-207` |
| ART-003 / ART-004 | Agent-only structured; no Merkle / claim path | CAP-318/319 |
| ART-027 | Consumed by batch status/results only; **no** trust verifier | EF-037 |

### 2.2 Consumers with no producer inside Occam (imports)

| Import | Consumed as | Notes |
|--------|-------------|-------|
| Operator-placed `signing-key.pem` / public PEM | ART-034 / verify `public_key` | TOFU; CLI may mint if absent |
| Community / seed playbook files + unsigned `manifest.json` sha map | ART-017 resolve | Integrity ≠ ECDSA |
| Site `/.well-known/agent-genome.v1.json` | ART-017 overlay | Network import |
| LibreTranslate HTTP | ART-039 | External service |
| Search provider HTTP | ART-013 | External |
| Cosign/Sigstore (CI) | ART-038 | Not consumed by install |
| Host IDE MCP config templates | ART-031 | Connect writes; hosts read |
| Cookies.txt / storageState on import | ART-026 / ART-037 | Operator supply |

---

## 3. Boundary crossings

| Boundary | Artifacts that cross | Notes |
|----------|----------------------|-------|
| **(a) Response → agent** | 001–022 (except 015 disk, 028 disk, 034), 023–025 (history in response), 027 results, 033 (via skill), 035 (`cached`), 039 | Primary product surface |
| **(b) Disk** | 015, 026, 027, 028, 029, 030, 031, 032, 033, 034, 035, 036 (temp), 037, 038 | Plus playbook community trees |
| **(c) Network** | 001–005, 010–014, 017 (genome), 019–022, 025/028 (via fetch), 032/038 (release CDN), 039 (translate) | Acquisition + distribution |
| **(d) Trust (signed/verifiable)** | **Fully:** 007, 008, 006 (inner), 015 (self-key), 022 (when on), 025 (when on), 034 (key) · **Partial:** 009 (TSA bind), 019 (Merkle≠truth), 021 (verdict), 024 (hash-only), 032 (sha256) · **Named-but-not:** **014** | Conservative: do not equate Merkle membership with claim truth |
| **(e) Machine / user portable** | 001–014, 016–022, 024–025, 032, 033, 038, 039 (as JSON/text) · **Not portable safely:** 026, 034 private, 037 · **Host-bound:** 027–031, 035 | Portability ≠ secrecy |

---

## 4. Sensitive artifacts

| ART | Sensitivity | Detail |
|-----|-------------|--------|
| **ART-034** | **Critical** — unencrypted PKCS8 private key | `signing-key.pem`; Windows ACL harden no-op (`ReceiptSigner.cs:84-88`); minted even if `OCCAM_RECEIPTS=off` (EF-044) |
| **ART-037** | **High** — raw cookies plaintext | Default retain under `_imports/` (EF-054) |
| **ART-026** | **High** — Cookie headers / storageState paths | Session profiles on disk; list claims no secret values but files contain them |
| ART-031 | Medium — may embed paths/env for MCP hosts | Connect bak/rollback issues (EF-021) |
| ART-029 | Medium — onboard env keys | Written before verify (EF-029) |
| ART-035 | Medium-if-session — cache eligibility excludes `session_profile`, but still stores full public-page envelopes + receipts | CAP-321 |
| ART-027 | Medium retention — full markdown, no eviction | EF-037 |
| ART-036 | Low — field selectors in temp JSON | Not credentials |
| ART-001/002… | Page content may include private URL bodies if SSRF guards bypassed — gated by fetch policy | PrivacyClassifier |

Private / RFC1918 URLs are blocked on fetch paths; they are not a separate ART but constrain what CONTENT may legally contain.

---

## 5. Lifetime classes

| Class | ART IDs |
|-------|---------|
| **ephemeral-in-response** | 001–014, 016–022, 024–025 (also persisted), 039 |
| **temp-file** | 036 (also headers temp files — see Gaps) |
| **process-lifetime** | 023 (in-memory store); genome fetch cache (not numbered) |
| **persistent-on-disk** | 015, 026, 027, 028, 029, 030, 031, 034, 035, 037 |
| **portable-export** | 006, 007, 008, 009, 022 (caller-saved), 032, 033, 038 |
| **host-config** | 029, 030, 031 |

ART-025 is dual-class: returned in-response and stored inside ART-028.

---

## 6. Gaps — implied by code, missing from ART-001…039

Do **not** allocate real ART numbers. Proposals:

| ID | Proposed name | Why missing | Evidence |
|----|---------------|-------------|----------|
| ART-NEW-P504-1 | Fetch headers temp JSON | Host→worker handoff every preflight; drives pool recycle | `FetchHeadersScope.cs` (CAP-881) |
| ART-NEW-P504-2 | Community `manifest.json` (sha256 map) | Unsigned integrity artifact for community playbooks | E-trust-state §1.8; PlaybookCommunityHygiene load |
| ART-NEW-P504-3 | Consensus response (verdict + divergence) | Unsigned aggregate; per-vantage receipts reuse ART-007 | `ConsensusService.cs:49-55,114` |
| ART-NEW-P504-4 | Failure atlas in-memory session store | Stateful; not on ART map (EF-024 withdrawn as leak) | FailureAtlas DI |
| ART-NEW-P504-5 | Playwright `storageState` file | Referenced by ART-026; distinct credential blob | session export-state |
| ART-NEW-P504-6 | Well-known genome JSON body | Network-imported overlay cached 1h | `WellKnownGenomeFetcher` |
| ART-NEW-P504-7 | Playbook publish package (`artifacts/playbook-publish/`) | CLI output; no automated consumer | CLI-SURFACE.md |
| ART-NEW-P504-8 | Public key PEM export | Consumer half of ART-034; CLI `keys export` | `ReceiptSigner.ExportPublicKeyPem` |

UNCERTAIN whether ART-NEW-P504-3 should stay separate vs. documented as "unsigned sidecar on ART-007 fan-out" — resolve in trust-model agent (P5-05).

---

## 7. Naming honesty

| Name / field | Suggests | Actually delivers | IDs |
|--------------|----------|-------------------|-----|
| `Receipt` on extract_knowledge | Signed Receipt v1 | `{confidence, elapsedMs}` only | ART-014, **CAP-287**, EF-006 |
| `semantic_chunking` | Semantic segmenter | Fixed-length accumulator with heading breadcrumbs | ART-005, CAP-320 |
| Capsule "proof-carrying" packaging | Whole URI signed | Only inner envelope signed; wrapper unsigned | ART-006, CapsuleCodec doc |
| `occam_attest` / `status` | Cryptographic truth of claims | Heuristic classifier + optional Merkle membership | ART-020 |
| `MaterializationKey` as cache/`if_none_match` driver (older map prose) | Server uses mat-key | Server: `TranscodeCacheKey` + `ContentHashToken`; mat-key is **client metadata** | ART-024, C-blind COVERED_WRONG |
| Receipts "not cached" (older map) | Receipts ephemeral-only | Full post-sign envelope including receipt/capsule **is** cached when `cache_ttl_s` | ART-007/006 ↔ ART-035 |
| `OCCAM_RECEIPTS` master switch | Disables all signing | Does **not** stop ART-015 signing or ART-034 mint | EF-005, EF-044, C6 |
| Cosign `.bundle` | Install verifies release | Install does not consume ART-038 | ART-038, EF-053 |
| Watch "signed history" when receipts off | Always signed | Unsigned entries still hash-chain | ART-025 |
| Batch "results" | Same trust as transcode | Markdown DTO, **no** Receipt v1 | ART-027, EF-037 |
| Skill "occam" card | Current tool surface | Stale version / tool count | ART-033, EF-036 |
| Playbook "signature" | Registry / third-party trust | Self-key TOFU only | ART-015 |
| Time anchor | Full TSA chain trust | Self-consistent bind; chain cut | ART-009 |

---

## 8. Corrections to prior flat map (`ARTIFACT-MAP.md`)

1. **ART-024 consumption** — do not claim MaterializationKey drives cache or `if_none_match` (code uses `TranscodeCacheKey` / `ContentHashToken`).
2. **ART-006/007 cacheability** — eligible success envelopes **do** persist receipts/capsules inside ART-035.
3. **PROVENANCE** is not a separate family — same key as TRUST (four preimages).
4. **ART-014** must be loud-flagged as non-crypto (already in map; reinforced for trust-model feed).
5. **ART-027** classified RUNTIME (job state), not OPERATOR packaging.
6. Family counts replace Wave-2/3 flat "category" absence.

---

## 9. Coverage check

```
ART-001..039 present: 39
Duplicates: 0
Missing: 0
Families: 11
```

---

## 10. Relation to provisional product systems (PS-1…9)

| PS | Artifact families primarily flowing |
|----|-------------------------------------|
| PS-2 Materialization | CONTENT, STRUCTURE, ART-024, ART-035, ART-039 |
| PS-3 Discovery | DISCOVERY |
| PS-4 Knowledge | KNOWLEDGE, ART-036 |
| PS-5 Playbooks | ART-015…018 |
| PS-6 Trust & provenance | TRUST, VERIFICATION, DATASET |
| PS-7 Monitoring & multi-source | MONITORING, ART-027; consensus → ART-NEW-P504-3 |
| PS-8 Runtime | RUNTIME ART-023/035; SESSION for fetch |
| PS-9 Operator | OPERATOR, SESSION, ART-034 |

PS-1 Acquisition produces CONTENT/STRUCTURE but is not itself an artifact family.
