# CANONICAL-PRODUCT-GRAPH (Phase 5R)

**Agent:** P5-R  
**SoT:** executable code + Phase 5 canonical models. Public docs untrusted.  
**Machine graph:** [`canonical-product-graph.json`](canonical-product-graph.json)  
**Preserved raw discovery graph:** [`capability-graph.json`](capability-graph.json) (**658 nodes / 588 edges**, 17 ad-hoc relation names) — **untouched**.  
**Date:** 2026-07-26

---

## 0. How to read this graph

This is a **normalized product graph** for human reasoning, not a CAP inventory.

| | Discovery graph (`capability-graph.json`) | Product graph (this file) |
|--|------------------------------------------|---------------------------|
| Nodes | **658** (mostly CAP / tool / file atoms) | **155** (systems, families, load-bearing surfaces) |
| Edges | **588** | **280** |
| Relation vocabulary | 17 ad-hoc names | **12** closed relations (see §1) |
| Purpose | Trace every CAP | Explain product structure, hubs, orphans, degradation |

**Node types:** `PRODUCT_SYSTEM` · `CAPABILITY_FAMILY` · `ENTRYPOINT` · `ARTIFACT` · `BACKEND` · `STATE` · `TRUST_PRIMITIVE` · `WORKFLOW`.

**Not one node per CAP.** Families carry representative CAP IDs; full membership stays in `canonical-capabilities.json` (674 CAPs → 9 systems / 39 families / 38 product capabilities + 1 `DEAD_CLUSTER`).

### Inclusion rule (what was cut for readability)

| Layer | Included | Intentionally omitted |
|-------|----------|------------------------|
| **Entrypoints** | **28 / 51** named: 15 core MCP + 4 opt-in MCP surfaces (`batch_*`, `watch`, `crosscheck`, `failure_atlas`) + BatchServer + CLI `verify`/`keys` + `doctor`/`onboard`/`connect`/`session` + `launch-mcp-host` + Level B install | Connect’s **15 host adapters** (mechanisms under `EP:occam_connect`); maintainer gate/bench/help-registry-only scripts |
| **Artifacts** | **34 / 39** ART-001…039 as nodes | **ART-003, ART-004, ART-030, ART-033, ART-039** — see §6 |
| **State** | **12 / 29** ST items that mutate secrets, trust identity, caches/stores, browser pool, or host config | Temp headers/CSS temps; RAM caches (genome/seeds/tiers); stderr; skill trees; process-kill identity; portable response-only items already modeled as ART |
| **Workflows** | **14** load-bearing FLOW/CMP nodes | Remaining FLOW-011/016–018/020–022 (documented elsewhere; not required for spine slices) |

---

## 1. Relation dictionary (closed set of 12)

| Relation | From → To | Exact meaning (single sense) |
|----------|-----------|------------------------------|
| **CONTAINS** | `PRODUCT_SYSTEM` → `CAPABILITY_FAMILY` | Ownership: the family belongs to exactly one system. |
| **EXPOSES** | `ENTRYPOINT` → `CAPABILITY_FAMILY` | Primary caller surface for that family. |
| **USES** | `CAPABILITY_FAMILY`\|`WORKFLOW` → `BACKEND`\|`CAPABILITY_FAMILY` | Runtime dependency (not ownership). |
| **PRODUCES** | `CAPABILITY_FAMILY`\|`BACKEND` → `ARTIFACT` | Primary issuer of the artifact identity. |
| **CONSUMES** | `CAPABILITY_FAMILY`\|`WORKFLOW`\|`TRUST_PRIMITIVE` → `ARTIFACT` | Required input (not mere adjacency). |
| **READS_WRITES** | `CAPABILITY_FAMILY` → `STATE` | Contractual read/mutate of durable or process state. |
| **ESCALATES_TO** | `BACKEND` → `BACKEND` | Router may advance rungs on the **corrected** cascade ladder (`OccamRouter.cs:134-182`; EF-056). Not dual-fail ranking. |
| **PROVES** | `TRUST_PRIMITIVE` → `ARTIFACT` | What the primitive can bind about that artifact class — scope limited by `TRUST-MODEL.md` (never “page truth”). |
| **VERIFIES** | `CAPABILITY_FAMILY`\|`ENTRYPOINT` → `TRUST_PRIMITIVE` | Checker that exercises the primitive. |
| **COMPOSES_INTO** | `CAPABILITY_FAMILY`\|`ENTRYPOINT` → `WORKFLOW` | Load-bearing step in a proven FLOW/CMP. |
| **GATED_BY** | `CAPABILITY_FAMILY`\|`ENTRYPOINT` → `STATE`\|`ENTRYPOINT` | Reachability requires opt-in env/profile/ambient state; absence removes the surface. |
| **DEGRADES_TO** | `CAPABILITY_FAMILY`\|`BACKEND`\|`ARTIFACT` → `CAPABILITY_FAMILY`\|`ARTIFACT`\|`TRUST_PRIMITIVE` | Failure/honesty collapse: typed failure, negative receipt, or weaker trust — **never invent content**. |

Do **not** reuse `USES` for ownership, `PROVES` for verification, or `ESCALATES_TO` for dual-fail surface ranking (`ChooseRawFallback` / `FailureRanking`).

---

## 2. Spine slice — agent → entrypoint → acquisition → materialization → response

Reference narrative is **`occam_transcode(url)` defaults** (`PRODUCT-ARCHITECTURE.md` §2). It is **not** universal (probe/map/search/heal/resolve/lint/client_capabilities/atlas bypass the pipeline).

```
AGENT
  └─ EXPOSES ─ EP:occam_transcode
        ├─ FAM:response-cache          (Rung 0 — only if cache_ttl_s > 0)
        ├─ FAM:playbook-resolution     (playbook_policy=auto overlay)
        ├─ FAM:network-safety          (FetchPreflight / SSRF)
        ├─ FAM:session-fetch           (optional session_profile)
        ├─ FAM:acquisition-routing     ── USES ──► BE:http ─ESCALATES_TO► BE:browser ─ESCALATES_TO► BE:managed
        │                                      (404/410 + public-ref TERMINATE; managed fail never surfaces — EF-056)
        ├─ FAM:quality-failure-semantics  (post-processors BEFORE materialize — TranscodePipeline.cs:152-174)
        ├─ FAM:token-budget / focus-selection / structured-materialization / differential-materialization
        └─ FAM:receipts                (opt-in ReceiptsPolicy; key always minted — EF-044)
              └─ PRODUCES ART-001 (+ ART-002/024; optional ART-007/006)
```

**Parallel spines (same host, different L2):**

| Entrypoint | Spine | Skips |
|------------|-------|-------|
| `EP:occam_probe` / `map` | `BE:probe-fetcher` only | Router, post-processors, materialize, receipts |
| `EP:occam_search` | `BE:search-provider` | Extract / materialize |
| `EP:occam_extract_knowledge` | resolve → `BE:css-extract` | `TranscodePipeline` / Router; fake Receipt (EF-006) |
| `EP:occam_digest` / claim / attest / dataset / watch / crosscheck / batch | Compose into spine A via `pipeline.TranscodeAsync` | Sync MCP shape differs |

Hub on this slice: **`EP:occam_transcode` (degree 20)** — highest-degree node in the product graph.

---

## 3. Trust slice

```
FAM:receipts ─PRODUCES► ART-007 / ART-008 / ART-006 / ART-034
FAM:claims-attestation ─PRODUCES► ART-019 / ART-020
FAM:verification ─VERIFIES► TP:offline-verify | TP:live-verify | TP:receipt | TP:merkle | TP:capsule | TP:citation
TP:* ─PROVES► corresponding ART (see TRUST-MODEL §2 — C1…C12)
```

| Primitive | Proves (honest bound) | Does **not** prove |
|-----------|----------------------|--------------------|
| `TP:content-hash` | Byte identity of compiled markdown | URL/origin/time |
| `TP:receipt` / `TP:signature` | Key holder signed listed fields | Third-party identity of key holder; page truth |
| `TP:merkle` / `TP:citation` | Leaf membership under signed root | Semantic claim stance |
| `TP:claim-check` | BM25 floor + membership; `Verdict=not_evaluated` | Entailment |
| `TP:attestation` | Heuristic status only; **aggregate unsigned** | Cryptographic batch proof |
| `TP:consensus` | Same-host multi-vantage fingerprint equality | Independent observers; verdict unsigned (EF-032) |
| `TP:live-verify` | Drift vs re-fetch **now** | New signed artifact |
| `TP:offline-verify` | Crypto over supplied bytes + PEM | Network acquisition fact |

**DEGRADES_TO (trust honesty):** `ART-014` → `TP:acquisition-fact` (EF-006 fake Receipt); `FAM:claims-attestation` → `TP:attestation`; `FAM:consensus-crosscheck` → `TP:consensus`.

Hubs: **`FAM:verification` (19)**, **`FAM:receipts` (13)**, **`FAM:claims-attestation` (12)**.

---

## 4. State slice

Load-bearing `READS_WRITES` (12 ST nodes):

| State | Family | Why load-bearing |
|-------|--------|------------------|
| `ST-01` | `session-fetch` | HIGH secrets for fetch (ART-026) |
| `ST-06` | `response-cache` | Opt-in full-envelope disk cache (EF-001/045) |
| `ST-07` | playbook authoring/resolution | Always-signed local recipes (EF-005) |
| `ST-13` | `change-monitoring` | Durable watch; no un-watch API (EF-020) |
| `ST-15` | `receipts` | Key always minted (EF-044); incomplete master switch |
| `ST-17` | `batch-jobs` | No eviction; no Receipt v1 (EF-037) |
| `ST-18` | `failure-atlas` | In-memory per DI session only (C2; EF-024 withdrawn) |
| `ST-19` | `client-context` | Ambient `max_tokens` (CMP-013) |
| `ST-20` | `browser-acquisition` | Process-wide pool; InstallShared kills prior (EF-041) |
| `ST-22` / `ST-24` / `ST-26` | onboard / connect / packaging | Host mutation + install tree |

---

## 5. Operator slice

```
EP:install_level_b ─EXPOSES► FAM:packaging-distribution / install-onboarding
EP:occam_doctor     ─EXPOSES► FAM:operator-cli
EP:occam_onboard    ─EXPOSES► FAM:install-onboarding ─READS_WRITES► ST-22 (EF-029/050)
EP:occam_connect    ─EXPOSES► FAM:host-connectors    ─READS_WRITES► ST-24 (EF-021)
EP:occam_session    ─EXPOSES► FAM:session-fetch + operator-cli ─PRODUCES► ART-026/037
EP:launch_mcp_host  ─EXPOSES► FAM:runtime-transports + mcp-exposure
FLOW-015 (doctor → onboard → connect) ← COMPOSES_INTO from those entrypoints
```

**Packaging honesty:** `ART-038` (cosign bundle) **DEGRADES_TO** `ART-032` trust path — install verifies **sha256 of tarball**, not cosign (EF-053). Docker HEALTHCHECK broken (EF-051) — packaging family is real; some outputs prove nothing.

---

## 6. Failure / degradation slice

Corrected ladder (not density-ranked managed last-rung marketing):

```
BE:http ─ESCALATES_TO► BE:browser ─ESCALATES_TO► BE:managed
   │                      │                      │
   └─ on 404/410 or public-ref: TERMINATE (no escalate) — EF-056
   └─ DEGRADES_TO FAM:quality-failure-semantics (post-processors)
Managed fail: surface = ranked(http, browser) only — never managed failure as winner
FAM:quality-failure-semantics ─DEGRADES_TO► ART-008 (negative receipt when issued)
FAM:canonical-knowledge-ir ─DEGRADES_TO► FAM:structured-materialization
   (IR forced then discarded; Canonical:null — DEAD_CLUSTER)
```

`ok:false` ⇒ content **UNKNOWN**. `thin_extract` ⇒ bad extraction, not “short quality page”.

---

## 7. Structural findings the flat inventory hid

### 7.1 Hubs (highest undirected degree)

| Rank | Node | Degree | Why |
|------|------|-------:|----|
| 1 | `EP:occam_transcode` | 20 | Flagship exposure into PS-1+PS-2+receipts+playbook overlay |
| 2 | `FAM:verification` | 19 | Consumes many trust artifacts; verifies many primitives |
| 3 | `FAM:acquisition-routing` | 17 | Shared by digest/claim/dataset/watch/crosscheck/batch/live-verify |
| 4 | `FAM:receipts` | 13 | Key + positive/negative/capsule production |
| 5 | `FAM:claims-attestation` | 12 | Claim + attest composition hub |

### 7.2 Orphans

| Kind | Nodes | Reading |
|------|-------|---------|
| **Family with no `EXPOSES`** | **`FAM:canonical-knowledge-ir` only** | Expected: `DEAD_CLUSTER`, zero product capabilities, retained for CAP traceability |
| **Artifact with no `CONSUMES` edge** (agent- or operator-terminal / orphaned) | `ART-005,010,013,014,016,018,020,027,028,029,031,032,034,036,037,038` | Many are **intentional terminals** (agent-facing digest/facts/search; operator host configs). True structural orphans: **`ART-038`** (no install consumer — EF-053), **`ART-037`** (no Core reader of `_imports/` — EF-054), **`ART-018`** (lint never required by save/resolve) |

### 7.3 Dead ends

- **`FAM:canonical-knowledge-ir`**: shipped dead compute (AUTOMATION #18); no entrypoint.
- **`ART-038` → ART-032`**: cosign ceremony without product consumer.
- **Batch (`ART-027`)**: durable markdown store **without** Receipt v1 (EF-037) — multi-source without provenance.
- **Attest aggregate (`ART-020`)**: unsigned status batch (GAP-028 / TRUST-MODEL C9).

### 7.4 Cycles

Directed DFS finds **gating/composition cycles**, not cascade loops. Examples:

- `FAM:managed-acquisition` ←`GATED_BY`← `EP:occam_transcode` ←`EXPOSES`← … → managed (opt-in exposure cycle).
- `FAM:response-cache` ↔ `EP:occam_transcode` (cache gated by the same tool that exposes it).

**No cycle on `ESCALATES_TO` alone** (`http → browser → managed` is a DAG). Treat gating cycles as “reachability mutual dependence,” not infinite product loops.

### 7.5 Reconciled taxonomy moves (visible in CONTAINS)

| Family | System | Note |
|--------|--------|------|
| `quality-failure-semantics` | **PS-1** | Post-processors on router result **before** materialization |
| `digest-synthesis` | **PS-7** | Multi-fetch composition (not discovery) |
| `canonical-knowledge-ir` | **PS-4** as `DEAD_CLUSTER` | Zero product capabilities |

---

## 8. Traceability rule

Every node/edge claim must carry at least one of:

1. **CAP ID** from `canonical-capabilities.json` / `capabilities.json`
2. **ART / FLOW / EF / GAP / CMP** ID from canonical ledgers
3. **`path:line`** into executable code

Phase 5 precedence: code → Wave-4 correction layer → ledgers → subsystems → agent-local → public docs (untrusted).

Wave-4 conflicts that reshape edges here: **C1/EF-056** (cascade), **C2** (atlas not process-wide), **C6/EF-005/044** (receipts switch incomplete), **C8** (dead code still ships).

---

## 9. Intentionally excluded artifacts (ART coverage assertion)

| ART | Reason excluded from graph nodes |
|-----|----------------------------------|
| **ART-003** | Agent-only `tables[]`; no Merkle/claim consumer — readability |
| **ART-004** | Agent-only `feed`; no trust path — readability |
| **ART-030** | Connect last-run bookkeeping; dominated by ART-031 |
| **ART-033** | Skill card for external harnesses, not Core runtime |
| **ART-039** | `translatedMarkdown` lossy sidecar; never in receipt canonical bytes |

All other **ART-001…039** appear as `ARTIFACT` nodes in `canonical-product-graph.json`.

---

## 10. Self-verification

Scripted assertions against `canonical-product-graph.json` + `canonical-capabilities.json`:

| Assertion | Result |
|-----------|--------|
| JSON parses | **PASS** (155 nodes, 280 edges) |
| Every edge endpoint ∈ `nodes` | **PASS** |
| Every `rel` ∈ declared `relations` | **PASS** (12 relations) |
| Every family slug in `canonical-capabilities.json` has exactly one `CAPABILITY_FAMILY` node | **PASS** (39/39) |
| Every ART-001…039 is a node **or** listed in §9 | **PASS** (34 nodes + 5 excluded) |

---

## 11. Counts

| Quantity | Value |
|----------|------:|
| Product systems | 9 |
| Capability families (incl. dead cluster) | 39 |
| Entrypoints (graph) | 28 |
| Artifacts (graph) | 34 |
| Backends | 7 |
| State items (graph) | 12 |
| Trust primitives | 12 |
| Workflows | 14 |
| **Total nodes** | **155** |
| **Total edges** | **280** |
| Discovery graph (preserved) | 658 / 588 |

**Relation histogram:** CONTAINS 39 · EXPOSES 49 · USES 30 · PRODUCES 38 · CONSUMES 29 · READS_WRITES 13 · ESCALATES_TO 2 · PROVES 12 · VERIFIES 14 · COMPOSES_INTO 32 · GATED_BY 12 · DEGRADES_TO 10.
