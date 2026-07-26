# W4-E — Trust + stateful subsystems (blind negative-space audit)

**Owner:** W4-E  
**Scope:** `Receipts/**`, `Playbooks/**`, `Claims/**`, `Attest/**`, `Consensus/**`, `Dataset/**`, `Watch/**`, `Batch/**` (+ signing call sites that live in Tools/Services/Composition when they invoke these).  
**Method:** CODE DISCOVERY FIRST → independent inventory → THEN compare prior `docs-audit/*` → gap classify.  
**SoT:** shipped C# under `src/FFOccamMcp.Core/` only. No product edits.

---

## 1. Blind inventory

### 1.1 Persistence / `~/.occam` write surfaces (in-scope)

| Path (default) | Writer | Eviction / TTL | Atomicity | Cross-process |
|---|---|---|---|---|
| `~/.occam/keys/signing-key.pem` | `ReceiptSigner.LoadOrCreate` | Never | Direct `WriteAllText` | Shared file; first writer wins |
| `~/.occam/playbooks/local/{id}.playbook.json` | `PlaybookSaveService` | Never (overwrite by id) | Direct write after format | No lock |
| `~/.occam/watch/watch.json` | `WatchStore.Persist` | History capped 64/URL; **URLs uncapped**; `Remove` exists but unused | Plain `WriteAllText` (non-atomic) | In-process lock only |
| `~/.occam/jobs/jobs.json` (sibling of `jobs.db` via `ChangeExtension`) | `JsonFileBatchJobStore.Persist` | **None** (no delete API) | tmp + `File.Move(overwrite)` | In-process lock only; load-once |
| In-memory genome cache | `WellKnownGenomeFetcher` | 1h TTL | N/A | Per-process |

Env overrides: `OCCAM_KEYS_ROOT`, `OCCAM_PLAYBOOKS_LOCAL_ROOT`, `OCCAM_WATCH_DB_PATH`, `OCCAM_BATCH_DB_PATH`.

### 1.2 Signing call-site matrix vs `OCCAM_RECEIPTS` / `ReceiptsPolicy`

Master switch: `ReceiptsPolicy.Enabled()` — `OCCAM_RECEIPTS` null → **on**; disable only `off|0|false` (case-insensitive).  
Comment at `ReceiptsPolicy.cs:4-6` claims centralization for “transcode / digest / claim-check / dataset” — **omits watch + consensus**; does not mention playbook_save.

| Call site | Primitive | Honors `ReceiptsPolicy`? | Notes |
|---|---|---|---|
| `OccamTranscodeTool` → `BuildReceipt` / negative | `Sign` | **YES** (`ReceiptsPolicy.Enabled() ? signer : null`) | Optional `TimeAnchorService` same gate; `emit_capsule` packages signed bundle |
| `DigestService` per-item | `Sign` via `BuildReceipt` | **YES** | No time anchor; typically no block leaves |
| `ClaimCheckService` | `Sign` (+ `leafSetComplete`) | **YES** | Merkle math still computed when unsigned |
| `DatasetExportService` rows + manifest | `Sign` + `SignDetached` | **YES** | Manifest `sig` null when off |
| `WatchService` → `WatchHistoryChain.Append` | `SignDetached` | **YES** | Unsigned entries still hash-chain |
| `ConsensusService` vantages | `Sign` / negative | **YES** (but **re-parses env locally**, not `ReceiptsPolicy.Enabled()`) | Verdict string unsigned |
| **`PlaybookSaveService` → `PlaybookSignature.BuildSignedJson`** | `SignDetached` | **NO — unconditional** | Always writes `provenance` + echoes `SignedKeyId` |
| `AttestService` response envelope | — | N/A | Nested claim receipts only; aggregate counts unsigned |
| `BatchJobProcessor` results | — | N/A | **Never constructs receipts** |
| DI `AddOccamCore` | `LoadOrCreate()` | N/A | **Always mints/loads key** even if receipts off |
| CLI `keys export` | `LoadOrCreate` | N/A | Side-effect: may create key |

**Re-verify (independent):** Wave 1 EF-005 holds. `PlaybookSaveService.cs:86-91` calls `BuildSignedJson(..., signer)` with no `ReceiptsPolicy` check. No other unconditional **product** signer found; batch/attest/heal/lint/resolve do not sign envelopes. Keys are still created on host start regardless of the switch.

### 1.3 Single-key / many-purposes

One DI singleton `ReceiptSigner` (ECDSA P-256, `signing-key.pem`, unencrypted PKCS8) signs:

1. Receipt envelopes (`Sign`)
2. Playbook content-hash strings (`SignDetached`)
3. Dataset manifest canonical bytes
4. Watch-history entry canonical bytes

No domain-separation tag; separation is only by differing preimage shapes. Windows: `TryHardenPermissions` is a no-op (`ReceiptSigner.cs:86-88`).

### 1.4 MerkleTree

- Leaf preimage: `utf8(text + '\0' + source_selector)` (`MerkleTree.cs:18-21`).
- Odd levels: duplicate last (Bitcoin-style).
- Root / leaf hex / `Proof` / `VerifyProof` / `RootFromLeafHashes`.
- Empty blocks → null root; single leaf → leaf is root.
- `Proof` for OOB index → **empty list** (silent), not error (`MerkleTree.cs:112-114`).
- Leaves carried in receipt/capsule are authentic via signed root, not individually signed.

### 1.5 CapsuleCodec

- Wire: `occam://capsule/` + base64url(JSON).
- Embeds: `cap`, `kind`, **signed envelope**, optional **full markdown `content`**, `blockLeaves`, `timeAnchor`, advisory `verifyRecipe` (alg / merkle description / **keyId string** / CLI one-liner).
- Signature covers envelope only — packaging not signed (`CapsuleCodec.cs:13-14`).
- Round-trip: `Encode` / `TryParse` (never throws).
- Producer: `BuildReceipt(..., emitCapsule:true)` from transcode only.

### 1.6 TimeAnchor (RFC3161)

- Gate: `OCCAM_TIME_ANCHOR` ∈ {1,true,on} **and** `OCCAM_TSA_URL`.
- Timeout: `OCCAM_TSA_TIMEOUT_MS` default 3000 (clamp 500–15000).
- SSRF: `PrivacyClassifier` + `OutboundHttpGuard` on `receipts.timeAnchor` client; allow redirects off.
- Imprint: SHA-256 of raw signature bytes; fail-open → null (receipt still ships).
- Token is unsigned sidecar; consumer verifies bind, **not** TSA chain-to-root (`ReceiptTimeAnchor.cs:34-35`).

### 1.7 WellKnownGenomeFetcher

- URL: `{scheme}://{host}:{port}/.well-known/agent-genome.v1.json`.
- Trigger: tool `fetch_site_genome` **OR** env `OCCAM_SITE_GENOME_FETCH=1|true` (`PlaybookResolveOptions.ShouldFetchSiteGenome`).
- PrivacyClassifier private-host block; HttpClient has `OutboundHttpGuard`.
- Validate: schema_version 1.x + hosts[] match.
- Cache: ConcurrentDictionary, 1h, caches failures too.
- **Body:** `ReadToEnd()` then truncate to 32 KiB — full response buffered first (`WellKnownGenomeFetcher.cs:75-81`).
- **Content-Type:** rejects non-json **only when Content-Type is non-empty**; empty CT skips the check (`:67-69`).

### 1.8 Community hygiene / sanitizer / quality

| Component | What it does | Wired into product? |
|---|---|---|
| `PlaybookCommunityHygiene` | Walk JSON; reject secret-ish **property names** | YES — save reject; community load reject |
| `PlaybookCommunitySanitizer` | Forbidden headers, denylist selectors (`body`/`html`/`*`), agent_notes markers | **NO C# caller** (compiled; mirrored in `workers/.../playbook-publish-sanitize.mjs`) |
| `CommunityManifest` | Unsigned `manifest.json` file→sha256 map; integrity check on load | YES — community tier only |
| `PlaybookLinter` | Schema/structure lint; **no hygiene/sanitizer** | YES — `occam_playbook_lint` |
| `QualityGate` | Score ≥70 and noise ≤0.12; `AssessExtraction` = length + substring noise heuristic | YES — save verify path; score embedded in signed provenance |

Eval/code-exec: playbooks are data (selectors/notes), not executed as code in Core. Risk is **secret leakage** and **selector over-reach**, not RCE via sanitizer.

### 1.9 Claims / Attest / Dataset / Consensus / Watch / Batch (state + trust)

- **Claim-check:** forces `json_blocks`; BM25 floor; Merkle proofs; `leafSetComplete` when non-truncated; receipts gated.
- **Attest:** claim-check → semantic classifier (fail-closed) → attach top proof; aggregate `OccamAttestResponse` **unsigned**.
- **Dataset:** per-row receipts + detached manifest over row-leaf Merkle; gated by ReceiptsPolicy.
- **Consensus:** multi-vantage extract; per-vantage receipts gated; **verdict/divergence unsigned**; playbook_policy forced Off.
- **Watch:** last-seen + signed history window; ReceiptsPolicy on history sigs; failure leaves store unchanged; `reset` destroys prior chain; `IWatchStore.Remove` implemented, **no live caller**.
- **Batch:** MCP + `--batch-server` share store types, separate DI; results = markdown DTO **without Receipt v1**; store grows forever; Persist swallows I/O errors (`JsonFileBatchJobStore.cs:367-379`).

### 1.10 Automatic / silent behaviors (trust-relevant)

| Behavior | Trigger | Visible? | Configurable / disable | Trust/privacy effect |
|---|---|---|---|---|
| Key file create | First `LoadOrCreate` (DI / keys export) | stderr only on export | `OCCAM_KEYS_ROOT`; not tied to `OCCAM_RECEIPTS` | Private key on disk even if signing “off” |
| Receipt sign on success paths | Tool success | Response `receipt` | `OCCAM_RECEIPTS` | Default ON |
| Playbook sign on save | Every successful save | File + `SignedKeyId` | **Cannot disable via OCCAM_RECEIPTS** | Always writes provenance |
| Site genome fetch | Param or `OCCAM_SITE_GENOME_FETCH` | Resolve metadata | Param/env | Live network; overlays genome |
| Time anchor POST | Env pair set + signed receipt | Sidecar or absent | Env; fail-open | Third-party sees sig hash |
| Watch full-file rewrite | Every successful watch incl. unchanged | Disk only | Opt-in MCP | LastSeenAt churn |
| Genome failure cache 1h | Failed well-known | CacheHit true | Process restart | Suppresses retries |

### 1.11 Blind behavior count (externally meaningful)

Enumerated ≥ **42** distinct behaviors across signing gates, persistence, Merkle/capsule/TSA, genome fetch, hygiene asymmetry, quality gate, claim/attest/dataset/consensus/watch/batch state machines.

---

## 2. Gap classification

Compare performed against: `CAPABILITY-INVENTORY.md`, `capabilities.json` (spot), `CAPABILITY-GRAPH.md`, `ARTIFACT-MAP.md`, `CODE-DERIVED-WORKFLOWS.md`, `NONCORE-SURFACE-MAP.md`, `ENGINEERING-FINDINGS.md`, `subsystems/trust-receipts.md`, `watch.md`, `batch-batchserver.md`, `consensus-crosscheck.md`, `tools/occam_playbook_save.md`, `occam_playbook_lint.md`, `occam_playbook_resolve.md`, `ENVIRONMENT-VARIABLES.md`.

### Covered exactly (re-confirmed, not re-minted)

| Topic | Prior ID | Evidence re-check |
|---|---|---|
| playbook_save ignores `OCCAM_RECEIPTS` | EF-005, CAP-280 note, CAP-571, ART-015 | `PlaybookSaveService.cs:86-91` |
| Single key many purposes | CAP-289 | DI singleton + SignDetached fan-out |
| Consensus verdict unsigned | CAP-856 | `ConsensusService.cs:50-56` |
| Consensus duplicates ReceiptsPolicy parse | CAP-865 | `ConsensusService.cs:114-122` |
| Watch multi-process race / non-atomic write | EF-019, CAP-845 | `WatchStore.cs:104-115` |
| Watch no unwatch / Remove dead from surfaces | EF-020, CAP-840 | `Remove` at `:60-71`; no `src/` caller |
| Batch no Receipt v1 + no eviction | EF-037, CAP-804 | `BatchJobProcessor` / `IBatchJobStore` |
| Batch cross-process last-writer | EF-038 | load-once + Persist |
| Capsule / Merkle / TSA / key PEM unencrypted | CAP-250..291 | trust-receipts.md |

### Gaps (new or under-modeled)

#### G-E-01 — `PlaybookCommunitySanitizer` is Core-dead but audit prose treats it as live
- **Class:** `DEAD_CODE_MISTAKEN_AS_PRODUCT` + `COVERED_WRONG`
- **Evidence:** zero references outside its own file in `src/` / `benchmarks/`; `occam_playbook_lint.md` CAP-758 cites `PlaybookCommunitySanitizer.cs` as if live-consumed for `agent_notes`.
- **Semantics:** Save/lint never run header/selector/notes sanitizer; only Hygiene property-name walk + (community) sha256.

#### G-E-02 — Three-tier trust asymmetry on playbook JSON (hygiene ≠ sanitizer ≠ lint)
- **Class:** `MISSING_SECURITY_SEMANTIC` + `MISSING_EDGE`
- **Evidence:** Save uses Hygiene only (`PlaybookSaveService.cs:23-26`); Linter has no Hygiene/Sanitizer calls; Community load = Hygiene + unsigned manifest hashes (`CommunityManifest.cs:83-114`).
- **Missing model:** edge “local save can persist Cookie headers / `body` selectors / bearer notes that community publish JS would reject.”

#### G-E-03 — Community `manifest.json` is integrity-only (no authenticity)
- **Class:** `MISSING_SECURITY_SEMANTIC` + `MISSING_ARTIFACT` (trust properties of ART community files)
- **Evidence:** `CommunityManifest` only compares sha256; no signature on manifest or playbook at community load (contrast local save which always ECDSA-signs).
- **Threat:** writer who controls both `manifest.json` and `.playbook.json` passes the gate.

#### G-E-04 — Genome fetch: empty Content-Type bypasses `not_json`
- **Class:** `MISSING_EDGE` + `MISSING_FAILURE_SEMANTIC`
- **Evidence:** `WellKnownGenomeFetcher.cs:67-69` — `!string.IsNullOrEmpty(contentType)` short-circuit.
- **Effect:** HTML/error bodies with missing CT proceed to truncate + schema validate (may fail later as `invalid_manifest`, but not as `not_json`).

#### G-E-05 — Genome fetch buffers full response before MaxBytes truncate
- **Class:** `MISSING_SECURITY_SEMANTIC` (DoS / memory)
- **Evidence:** `ReadToEnd()` then `body[..MaxBytes]` (`WellKnownGenomeFetcher.cs:75-81`). MaxBytes=32KiB is post-read.

#### G-E-06 — `ReceiptsPolicy` comment / capability framing understates signing surface
- **Class:** `COVERED_WRONG` (doc-comment + inventory framing) + `MISSING_EDGE`
- **Evidence:** Policy comment lists 4 paths; code also gates watch + consensus; playbook_save is intentionally outside. Inventory often says “single kill-switch” without the save exception in the same sentence as watch/consensus.

#### G-E-07 — Host start always materializes signing key (receipts-off still creates PEM)
- **Class:** `MISSING_ARTIFACT` + `AUTOMATIC` under-modeled relative to ARTIFACT-MAP
- **Evidence:** `OccamServiceCollectionExtensions.cs:23` → `LoadOrCreate()`; ARTIFACT-MAP has ART-007..028 but **no first-class ART for `signing-key.pem`**.
- **Effect:** `OCCAM_RECEIPTS=off` does not mean “no key material on disk.”

#### G-E-08 — Attest aggregate envelope unsigned (parallel to consensus verdict)
- **Class:** `MISSING_ARTIFACT` / `MISSING_EDGE`
- **Evidence:** `AttestService.cs:38-49` returns counts + perClaim; only nested `Receipt` objects may be signed. ART-020 notes Merkle≠truth but not “unsigned aggregate.”

#### G-E-09 — QualityGate heuristic scores are embedded in **signed** playbook provenance
- **Class:** `MISSING_SECURITY_SEMANTIC` (over-trust of `passesGate`)
- **Evidence:** `QualityGate.AssessExtraction` substring/length heuristic (`QualityGate.cs:50-86`) → `PlaybookSignature.BuildSignedJson` verify block (`PlaybookSignature.cs:70-81`).
- **Risk:** consumers may treat signed `passesGate:true` as strong quality proof; it is a local heuristic attestation.

#### G-E-10 — Batch Persist fail-open (I/O errors swallowed; memory authoritative)
- **Class:** `MISSING_FAILURE_SEMANTIC` (partial vs EF-038)
- **Evidence:** `JsonFileBatchJobStore.Persist` bare `catch` (`:367-379`). Durability can silently diverge from disk without surfacing to MCP/HTTP clients.

#### G-E-11 — Watch vs Batch durability asymmetry not edged in graph
- **Class:** `MISSING_EDGE`
- **Evidence:** Batch uses tmp+Move; Watch uses plain WriteAllText. CAP-845 notes non-atomic watch but ARTIFACT-MAP/graph do not contrast store durability classes.

#### G-E-12 — Merkle `Proof` OOB → empty proof (silent)
- **Class:** `MISSING_EDGE`
- **Evidence:** `MerkleTree.Proof` `:112-114`. Callers must validate index; empty proof verifies only for degenerate trees.

### Explicitly not new CAPs (prefer edges)

- EF-005 / playbook_save: **reconfirmed**, propose engineering follow-up only if orch wants closure tracking → `EFC-E-1`.
- Unsigned consensus verdict / CAP-865 / watch Remove / batch eviction: already modeled.

### New CAP candidates (only if orch insists on mint)

- `CAP-NEW-E-1` — Playbook trust pipeline asymmetry (Hygiene vs Sanitizer vs Lint vs Community integrity).
- `CAP-NEW-E-2` — Genome fetch Content-Type empty bypass + unbounded read-before-truncate.
- Else: prefer edges ART/`signing-key.pem`, ART attest-envelope-unsigned, workflow “community publish sanitize not enforced on MCP save.”

### Proposed engineering findings (orchestrator allocates EF-041+)

| ID | Class | Confidence | One-liner |
|---|---|---|---|
| EFC-E-1 | BUG-CANDIDATE | PROVEN | Reconfirm EF-005: `PlaybookSaveService` still unconditional-signs; only non-`ReceiptsPolicy` product signer |
| EFC-E-2 | OBSERVATION | PROVEN | `PlaybookCommunitySanitizer` ships in Core but is unreachable; lint audit text wrongly cites it as live |
| EFC-E-3 | SECURITY-SEMANTIC | PROVEN | Local save path does not apply Sanitizer denylist/header/notes checks that publish JS claims |
| EFC-E-4 | SECURITY / PERF | PROVEN | `WellKnownGenomeFetcher` ReadToEnd-before-truncate + empty Content-Type skips `not_json` |
| EFC-E-5 | OBSERVATION | PROVEN | `AddOccamCore` always `LoadOrCreate` — receipts-off still creates `signing-key.pem` |
| EFC-E-6 | DESIGN-QUESTION | PROVEN | Signed playbook `verify.passesGate` binds QualityGate heuristic, not independent measurement |
| EFC-E-7 | BUG-CANDIDATE | PROVEN | Batch `Persist` swallows all exceptions — silent durability loss (extends EF-038 failure mode) |

---

## 3. Cross-cutting lens notes

### Config reverse
- Effective: `OCCAM_RECEIPTS`, `OCCAM_KEYS_ROOT`, `OCCAM_TIME_ANCHOR`, `OCCAM_TSA_URL`, `OCCAM_TSA_TIMEOUT_MS`, `OCCAM_SITE_GENOME_FETCH`, `OCCAM_PLAYBOOKS_LOCAL_ROOT`, `WT_PLAYBOOKS_PATH`, `OCCAM_WATCH_*`, `OCCAM_BATCH_*`, `OCCAM_WATCH_MCP` / `OCCAM_BATCH_MCP` / `OCCAM_CONSENSUS_MCP`.
- Surprising: receipts master switch **does not** cover playbook_save; key creation **not** gated by receipts; genome env OR-gated with tool param (operator can force network without agent param).

### Failure / fallback
- TSA / genome network / batch Persist / watch corrupt load → fail-open or silent empty.
- Claim/attest fail-closed on semantics; negative receipts only for provable wall codes.

### Platform
- Key chmod only non-Windows; path joins use `Path.Combine` (OK). Batch/Watch path overrides are raw strings (operator responsibility).

### Dead vs shipped
- `PlaybookCommunitySanitizer`: **ships compiled, dead at runtime in Core**.
- `IWatchStore.Remove`: ships, dead from product surfaces (gate unit may call).

---

## 4. Convergence

**CONVERGENCE_IN_SCOPE: YES (with residual edges).**  
Major trust/state machines (ReceiptsPolicy exception for save, watch/batch races, unsigned consensus, single-key fan-out, TSA fail-open, capsule contents) are already in Wave 1–3 model. Independent discovery still found **actionable under-models**: dead Sanitizer vs live Hygiene asymmetry, genome CT/DoS edges, key file as missing ART, attest aggregate unsigned, QualityGate-in-provenance over-trust, batch Persist swallow.

---

## 5. Uncertainties

1. Whether any **external** Node-only publish path is considered “product” for Sanitizer (out of C# scope; mirror exists).
2. Whether `OutboundHttpGuard` + PrivacyClassifier fully cover DNS rebinding for genome/TSA (not re-proved beyond ConnectCallback registration).
3. Gate coverage depth for `ConsensusService` end-to-end receipts (prior CAP-863/EF-030 noted absence; not re-run gate).
4. Exact size of production `watch.json` / `jobs.json` in the wild (unbounded growth risk is code-proven, ops impact not measured).

---

## Envelope (compact)

```
OWNER: W4-E
SCOPE_FILES_READ: ~55 Core files (Receipts 11, Playbooks 25, Claims 3, Attest 3, Consensus 3, Dataset 3, Watch 4, Batch 9 + Tools/Composition/CLI signing sites)
BLIND_BEHAVIORS: 42
GAPS: covered_exact=11 partial=3 wrong=2 missing_cap=2 missing_edge=6 missing_artifact=3 missing_workflow=1 missing_config=1 missing_failure=2 missing_security=4 dead_as_product=1 product_as_internal=0
TOP_MISSED:
  1. PlaybookCommunitySanitizer Core-dead; lint CAP-758 wrongly cites it (Sanitizer.cs / lint.md)
  2. Save≠Sanitizer: Cookie headers/body selectors survivable on local save (SaveService:23 vs Sanitizer:8-21)
  3. CommunityManifest integrity-only / unsigned (CommunityManifest.cs:83-114)
  4. Genome empty Content-Type skips not_json (WellKnownGenomeFetcher.cs:67-69)
  5. Genome ReadToEnd before 32KiB truncate (WellKnownGenomeFetcher.cs:75-81)
  6. DI always LoadOrCreate key; ARTIFACT-MAP lacks signing-key.pem (OccamServiceCollectionExtensions.cs:23)
  7. Attest aggregate unsigned (AttestService.cs:38-49)
  8. QualityGate heuristic sealed into signed provenance (QualityGate.cs:50-86 → PlaybookSignature.cs:70-81)
NEW_CAP_CANDIDATES: CAP-NEW-E-1 (playbook trust asymmetry); CAP-NEW-E-2 (genome CT/DoS) — prefer edges
NEW_EDGES: Hygiene≠Sanitizer≠Lint; watch non-atomic vs batch tmp+Move; ReceiptsPolicy omits save but covers watch/consensus; attest-aggregate||consensus-verdict unsigned pattern
NEW_ARTIFACTS: ~/.occam/keys/signing-key.pem (missing ART); unsigned attest summary; community manifest integrity-only
NEW_WORKFLOWS: “MCP save local without publish-sanitize” vs “JS publish sanitize”
AUTOMATIC_SILENT: key mint on host start; OCCAM_SITE_GENOME_FETCH network; playbook sign always; TSA fail-open omit
FAILURE_FALLBACK: batch Persist swallow; watch corrupt→empty; genome/TSA fail-open; Merkle Proof OOB→[]
CONFIG_GAPS: OCCAM_RECEIPTS incomplete master (save); receipts-off≠no-key; genome env OR tool param
PLATFORM_DIFFS: TryHardenPermissions no-op on Windows (ReceiptSigner.cs:86-88)
EFC: EFC-E-1 BUG EF-005 reconfirm; EFC-E-2 Sanitizer dead; EFC-E-3 save skips Sanitizer; EFC-E-4 genome CT/DoS; EFC-E-5 key mint vs receipts-off; EFC-E-6 signed heuristic passesGate; EFC-E-7 batch Persist swallow
CONVERGENCE_IN_SCOPE: YES — major trust/state already modeled; residual = hygiene asymmetry, genome edges, key ART, attest aggregate, Persist swallow
UNCERTAINTIES: JS publish as product?; SSRF rebinding depth; consensus E2E gate; field store sizes
```
