# USE-CASE-MODEL (Phase 5N)

**Agent:** P5-09  
**SoT:** `canonical-capabilities.json` (39 family slugs), `ENTRYPOINT-MODEL.md`, `PRODUCT-ARCHITECTURE.md`, `TRUST-MODEL.md`, `ACQUISITION-ROUTING-MODEL.md`, `STATE-MODEL.md`, `AUTOMATION-MODEL.md`, `ARTIFACT-ONTOLOGY.md`.  
**Docs (`docs/`, README) are untrusted.**  
**Date:** 2026-07-26

---

## 0. Method

User modes are **derived from reachable capability families and entrypoints**, not marketing personas.

| Candidate mode | Verdict | Reason |
|----------------|---------|--------|
| **AI agent** | **KEEP** | Default product: 15 core MCP tools + server instructions; live extract honesty contract (`ENTRYPOINT-MODEL` §2–3; PS-1…6 via core). |
| **Operator** | **KEEP** | Install, doctor, connect, refresh, session CLI, packaging consume (`operator-cli`, `install-onboarding`, `host-connectors`; 51 named entrypoints). |
| **Research workflow** | **KEEP** | Probe → map/search → digest/transcode is a first-class multi-tool spine (`probe-diagnostics`, `site-mapping`, `web-search`, `digest-synthesis`). |
| **Auditor** | **KEEP (narrow)** | `claim_check` / `attest` / `verify` / `dataset_export` exist, but prove **bytes/keys/heuristics**, not truth (`TRUST-MODEL` §13). |
| **Integration author** | **KEEP** | Connect adapters (≤15 hosts), skill install, MCP profile/env wiring, agent-sdk tree (`host-connectors`, `mcp-exposure`, ART-031/033). |
| **Developer** | **KEEP (narrow)** | Host flags (WS/Remote/BatchServer), env surface, offline CLI verbs, packaging internals — **not** a general-purpose library API for app embedding. |
| **Data pipeline** | **KEEP (weak)** | Batch MCP + `--batch-server` exist; no Receipt v1, no eviction, opt-in only (EF-037/038). |
| End-user browser reader / consumer UI | **REJECT** | No shipped UI; MCP/CLI/operator only (`ENTRYPOINT-MODEL` §0). |
| Multi-tenant SaaS / hosted Occam cloud | **REJECT** | Single-host TOFU key; Remote mode is self-hosted process, not a cloud product (`TRUST-MODEL` §1; CAP-023). |
| CAPTCHA-bypass / anti-bot red team | **REJECT** | Explicit non-goal; typed failures only (`PRODUCT-ARCHITECTURE` §8; `ACQUISITION-ROUTING-MODEL` §2). |
| Cryptographic notary / PKI issuer | **REJECT** | Local ECDSA TOFU only; no identity binding (`TRUST-MODEL` §1, forbidden claims 1–5). |

**Added mode (not in the seed list):** **Playbook author** — heal → lint → save → resolve/auto overlay is an independently operable lifecycle (PS-5) distinct from “developer” or “integration author.”

---

## 1. Ranking (documentation priority input)

| Rank | Use case | Fit | Justification |
|------|----------|-----|---------------|
| 1 | AI agent | **STRONG** | Default stdio MCP; `occam_transcode(url)` alone delivers value; client_capabilities + failure honesty are designed for agents (`ENTRYPOINT` §2.2 ~60–70% of PS via core 15). |
| 2 | Operator | **STRONG** | Full PS-9 surface: install Level A/B, doctor, connect, refresh, session profiles — required before agents work. |
| 3 | Research workflow | **STRONG** | Probe/map/search/digest are always-on core tools; clear end-to-end path without opt-in flags. |
| 4 | Playbook author | **PARTIAL** | Tools exist and are core; heal needs browser; save always signs (EF-005); community sanitizer Core-dead (EF-047); quality score unsigned (TRUST X1). |
| 5 | Auditor | **PARTIAL** | Tools exist under researcher/auditor profiles; names overstate (`TRUST-MODEL` §13 #7–9); manifest verify CLI-only (EF-018/025); `reader` can produce receipts but not verify (EFC-P5-05-4). |
| 6 | Integration author | **PARTIAL** | Connect + skill work; adapter rollback gaps (EF-021); skill card stale (EF-036); launcher hides WS/Remote (CAP-1001). |
| 7 | Developer | **PARTIAL** | Alternate transports and CLI verbs exist; discoverability weak (EF-025 host verbs vs wrapper); Docker HEALTHCHECK broken (EF-051). |
| 8 | Data pipeline | **WEAK** | Batch/watch opt-in; batch retains markdown forever without Receipt v1 (EF-037/038); watch has no Remove API (EF-020); not in default tools/list. |

---

## 2. Use cases (detail)

### UC-1 — AI agent (STRONG)

| Field | Content |
|-------|---------|
| **GOAL** | Obtain real page text (or typed refusal) inside an LLM tool loop without inventing content on failure. |
| **MINIMUM ENTRYPOINTS** | (1) Install/launch MCP host · (2) `occam_client_capabilities` once · (3) `occam_transcode` with `url` only. |
| **CAPABILITY FAMILIES** | `client-context`, `mcp-exposure`, `runtime-transports`, `acquisition-routing`, `http-acquisition`, `browser-acquisition`, `token-budget`, `quality-failure-semantics`, `focus-selection` |
| **ARTIFACTS** | ART-001, ART-023, ART-012 (optional probe), ART-007 when receipts on |
| **TRUST NEEDS** | Treat `ok:false` as unknown content; do not treat receipts as origin proof (`TRUST-MODEL` C1–C4). |
| **ADVANCED OPTIONS** | `max_tokens` / `fit_markdown`+`focus_query`; `json_blocks`/`json_tables`/`json_feed`; `session_profile`; `backend_policy`; `playbook_policy=auto`; `prefer_llms_txt`; `emit_capsule`. |
| **LIMITATIONS** | No CAPTCHA solve; login walls need operator session; managed providers (if configured) are third-party egress (EF-003); ambient budget changes cache identity; cascade is **not** universal http→browser→managed (EF-056). |
| **SCENARIO** | Operator ran `occam connect` → agent calls `occam_client_capabilities(context_tokens=200000)` → `occam_transcode({url:"https://developer.mozilla.org/"})` → on `ok:true` read `markdown`; on `thin_extract`/`captcha_or_challenge` follow `failure.code` / `agentMeta.decisions`, do **not** summarize from memory. |

---

### UC-2 — Operator (STRONG)

| Field | Content |
|-------|---------|
| **GOAL** | Get a working host on a machine, wire an MCP client, keep workers/browsers healthy, manage sessions and process lifecycle. |
| **MINIMUM ENTRYPOINTS** | `get-ff-occam` / `install` · `occam doctor` · `occam connect` · `launch-mcp-host` (or host-managed launch). |
| **CAPABILITY FAMILIES** | `install-onboarding`, `operator-cli`, `host-connectors`, `packaging-distribution`, `runtime-transports`, `session-fetch`, `proxy-egress` (when needed) |
| **ARTIFACTS** | ART-029, ART-030, ART-031, ART-032, ART-026, ART-034 (always minted — EF-044) |
| **TRUST NEEDS** | Know key is minted on first start; Level B install verifies sha256 map, **not** cosign by default (EF-053); refresh kills **all** `OccamMcp.Core` by name (EF-049). |
| **ADVANCED OPTIONS** | Level B tarball; `OCCAM_PROFILE`; opt-in `OCCAM_*_MCP` flags; WS/`--remote`/`--batch-server` (not via canonical launcher); proxy envs; `occam session` import/export-state. |
| **LIMITATIONS** | Onboard env silently merges into every launch (EF-050); install `rm -rf` with no rollback (EF-028); connect rollback often dead when host requires restart (EF-021); Docker health broken (EF-051). |
| **SCENARIO** | `.\scripts\occam-doctor.ps1` → `node scripts/occam.mjs connect --host cursor` → set `OCCAM_HOME` → reload MCP → `occam session export-state` for a login wall → pass `session_profile` into tools. |

---

### UC-3 — Research workflow (STRONG)

| Field | Content |
|-------|---------|
| **GOAL** | Decide what is worth fetching, discover site structure, then read many URLs under one budget. |
| **MINIMUM ENTRYPOINTS** | MCP host + `occam_probe` + (`occam_map` **or** `occam_search`) + `occam_digest` (or N× `occam_transcode`). |
| **CAPABILITY FAMILIES** | `probe-diagnostics`, `site-mapping`, `web-search`, `digest-synthesis`, `acquisition-routing`, `http-acquisition`, `browser-acquisition`, `token-budget`, `quality-failure-semantics` |
| **ARTIFACTS** | ART-012, ART-011, ART-013, ART-010, ART-001 |
| **TRUST NEEDS** | Probe/map/search are **unsigned discovery**; extractability scores are heuristics, not guarantees. |
| **ADVANCED OPTIONS** | Search provider env (`OCCAM_SEARCH_PROVIDER`); digest `focus_query` / `source_url`; map `source=sitemap|homepage`; probe before search rerank. |
| **LIMITATIONS** | Search fails closed without provider; map/probe bypass Router (no playbook overlay); PDF map short-circuits; search may fan-out probes (cost). |
| **SCENARIO** | `occam_probe({url})` → if extractable, `occam_map({url, source:"sitemap"})` → `occam_digest({urls:[…], focus_query:"API limits", backend_policy:"http_then_browser"})` → read combined markdown + per-item failures. |

---

### UC-4 — Playbook author (PARTIAL) — **added mode**

| Field | Content |
|-------|---------|
| **GOAL** | Draft, validate, and persist site-specific extraction recipes; optionally drive schema extract. |
| **MINIMUM ENTRYPOINTS** | `occam_playbook_heal` → `occam_playbook_lint` → `occam_playbook_save` → verify with `occam_playbook_resolve` / `occam_transcode(playbook_policy=auto)`. |
| **CAPABILITY FAMILIES** | `playbook-healing`, `playbook-validation`, `playbook-authoring`, `playbook-resolution`, `schema-knowledge-extraction`, `browser-acquisition` |
| **ARTIFACTS** | ART-016, ART-018, ART-015, ART-017, ART-014 |
| **TRUST NEEDS** | Save **always signs** regardless of `OCCAM_RECEIPTS` (EF-005); `verify.score` / `passesGate` are **unsigned** provenance fields (TRUST X1); signature ≠ quality. |
| **ADVANCED OPTIONS** | Genome fetch (`fetch_site_genome` / `OCCAM_SITE_GENOME_FETCH`); community/seed tiers; `occam_extract_knowledge` after schema present. |
| **LIMITATIONS** | Heal needs browser/skeleton; `--consent-aggressive` unreachable from MCP (CAP-553); `PlaybookCommunitySanitizer` Core-dead (EF-047); marketplace auto-merge supply-chain risk (EF-052). |
| **SCENARIO** | After `thin_extract` on a SPA: `occam_playbook_heal({url})` → edit candidates → `occam_playbook_lint({playbook})` → `occam_playbook_save({…})` → `occam_transcode({url, playbook_policy:"auto"})`. |

---

### UC-5 — Auditor (PARTIAL)

| Field | Content |
|-------|---------|
| **GOAL** | Bind extracts to self-signed receipts; check claim strings against blocks; batch-status reports; export auditable URL sets. |
| **MINIMUM ENTRYPOINTS** | Profile `auditor` or `full` + `occam_transcode`/`occam_claim_check` + `occam_verify` (offline) + optional `occam_attest` / `occam_dataset_export`. Offline: host `verify` / `keys export` (not via `occam` wrapper — EF-025). |
| **CAPABILITY FAMILIES** | `receipts`, `verification`, `claims-attestation`, `dataset-provenance`, `structured-materialization` |
| **ARTIFACTS** | ART-007, ART-008, ART-006, ART-019, ART-020, ART-021, ART-022, ART-034 |
| **TRUST NEEDS** | Only TOFU integrity; forbidden claims in `TRUST-MODEL` §13; MCP `public_key` defaults to **this host's** key; CLI forces `--pubkey`. |
| **ADVANCED OPTIONS** | `emit_capsule`; `OCCAM_TIME_ANCHOR`+`OCCAM_TSA_URL` (partial); `mode=live|prove|citation|history`; dataset manifest CLI verify. |
| **LIMITATIONS** | `attest` is regex entailment, aggregate **unsigned** (forbidden #7); `claim_check` `proven:true` is retrieval-complete not semantic (forbidden #8); live verify drops session/playbook (EF-012); history_verified can mean unsigned chain (EFC-P5-05-2). |
| **SCENARIO** | `occam_transcode` with receipts on → store `receipt.signed` → `occam_claim_check({url, claim:"X is Y"})` → `occam_verify({mode:"offline", receipt, public_key})` → for a report, `occam_attest({claims:[…]})` reading **status as heuristic**, not crypto. |

---

### UC-6 — Integration author (PARTIAL)

| Field | Content |
|-------|---------|
| **GOAL** | Wire Occam into a specific MCP host / agent harness with correct command, env, and skill/card. |
| **MINIMUM ENTRYPOINTS** | `occam connect` (or manual snippet) + skill install bin + optional `@ff-occam/agent-sdk` / `@ff-occam/mcp` package surface. |
| **CAPABILITY FAMILIES** | `host-connectors`, `install-onboarding`, `mcp-exposure`, `client-context`, `packaging-distribution` |
| **ARTIFACTS** | ART-031, ART-033, ART-029, ART-030 |
| **TRUST NEEDS** | Do not claim marketplace/cosign identity for community playbooks (EF-052/053). |
| **ADVANCED OPTIONS** | Per-host adapters; `OCCAM_PROFILE=reader|researcher|auditor`; opt-in tool flags; Remote auth. |
| **LIMITATIONS** | Skill card may show stale tool count/version (EF-036); npm publish status separate from tree; connect may mutate ≤15 configs without confirm; launcher cannot start WS/Remote. |
| **SCENARIO** | `occam connect --host claude` → install skill → set `OCCAM_CLIENT_CONTEXT_TOKENS` or rely on `occam_client_capabilities` → smoke `occam_transcode`. |

---

### UC-7 — Developer (PARTIAL)

| Field | Content |
|-------|---------|
| **GOAL** | Run alternate process modes, inspect CLI verbs, tune env, package/distribute builds. |
| **MINIMUM ENTRYPOINTS** | Host binary with `--mcp-server` / `--remote` / `--batch-server` · `OccamCliVerbs` (`keys`, `verify`, `install-browser`, …) · packaging scripts. |
| **CAPABILITY FAMILIES** | `runtime-transports`, `operator-cli`, `packaging-distribution`, `mcp-exposure`, `batch-jobs` |
| **ARTIFACTS** | ART-027, ART-032, ART-038 (orphaned cosign), ART-034 |
| **TRUST NEEDS** | EF-041 pool kill on multi-session DI; EF-051 Docker; do not document dead codecs as features. |
| **ADVANCED OPTIONS** | Profile matrix; daemon/pool envs; managed provider envs; gate/bench (maintainer — out of user product). |
| **LIMITATIONS** | Host trust verbs poorly discoverable via `occam` wrapper (EF-025); many packaging claims untested (C7); Canonical IR discarded (dead product path). |
| **SCENARIO** | `dotnet run --project src/FFOccamMcp.Core -- --remote` with auth env → open WS session → observe per-connection DI + browser pool behavior; use `occam verify --pubkey …` for offline checks. |

---

### UC-8 — Data pipeline (WEAK)

| Field | Content |
|-------|---------|
| **GOAL** | Queue many URLs asynchronously and poll results; optionally watch URLs for change. |
| **MINIMUM ENTRYPOINTS** | `OCCAM_BATCH_MCP=1` + `occam_batch_submit/status/results` **or** `--batch-server`; for change: `OCCAM_WATCH_MCP=1` + `occam_watch`. |
| **CAPABILITY FAMILIES** | `batch-jobs`, `change-monitoring`, `acquisition-routing`, `http-acquisition`, `browser-acquisition` |
| **ARTIFACTS** | ART-027, ART-025, ART-028, ART-001 (retained in jobs) |
| **TRUST NEEDS** | Batch produces **no Receipt v1** (EF-037); watch history signing incomplete; no un-watch (EF-020). |
| **ADVANCED OPTIONS** | `OCCAM_BATCH_DB_PATH` / `OCCAM_WATCH_DB_PATH`; BatchServer HTTP clients; crosscheck (`OCCAM_CONSENSUS_MCP`) — still same egress (forbidden #9–10). |
| **LIMITATIONS** | Opt-in invisible by default; disk grows without eviction; not a streaming ETL bus; crosscheck is not multi-node; failure atlas is in-memory session telemetry only. |
| **SCENARIO** | Export `OCCAM_BATCH_MCP=1` → restart host → `occam_batch_submit({urls:[…]})` → poll `occam_batch_status` → `occam_batch_results`; accept markdown-only provenance. |

---

## 3. Cross-cut: which product systems each mode needs

| Use case | PS-1 | PS-2 | PS-3 | PS-4 | PS-5 | PS-6 | PS-7 | PS-8 | PS-9 |
|----------|:----:|:----:|:----:|:----:|:----:|:----:|:----:|:----:|:----:|
| AI agent | ● | ● | ○ | ○ | ○ | ○ | — | ● | ○ |
| Operator | ○ | — | — | — | — | ○ | — | ● | ● |
| Research | ● | ● | ● | — | — | — | — | ● | ○ |
| Playbook author | ● | — | — | ● | ● | ○ | — | ● | ○ |
| Auditor | ● | ● | — | — | — | ● | ○ | ● | ○ |
| Integration author | — | — | — | — | — | — | — | ● | ● |
| Developer | ○ | ○ | — | — | — | ○ | ○ | ● | ● |
| Data pipeline | ● | ○ | — | — | — | — | ● | ● | ○ |

● primary · ○ secondary/supporting · — not required for the minimum path

---

## 4. Documentation priority implication

Write **task guides first** for UC-1, UC-2, UC-3. Treat UC-4/5 as **advanced honest** guides (limits in-band). Treat UC-6 as operator+integration handbook. Treat UC-7/8 as reference/experimental — never lead marketing with batch/consensus/attest-as-crypto.

---

## 5. Uncertainty

| Item | Status |
|------|--------|
| Whether agent-sdk constitutes a separate “SDK developer” use case | **PARTIAL overlap with UC-6/7**; package publish status outside this model |
| Exact fraction of agents that need playbooks on day one | UNCERTAIN — capability exists; default path is playbook_policy=auto soft overlay |
