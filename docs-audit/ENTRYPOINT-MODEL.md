# ENTRYPOINT-MODEL (Phase 5F)

**Agent:** P5-03  
**SoT:** executable registration + scripts. Wave-4 corrections apply (GAP-012/032/033–037, EF-049…053).  
**Docs untrusted.**  
**Date:** 2026-07-26

---

## 0. Core claim

**Product capability ≠ MCP tool count.**  
“Occam = 15 MCP tools” is a **default stdio `tools/list` under `OCCAM_PROFILE=full` with opt-in flags off** — one exposure slice, not the product.

Evidence: `OccamMcpServerRegistration.OccamToolNames` (15) + profile gating + four env opt-ins + offline CLI + operator CLI + install/connect + alternate process modes + packages (`PROFILE-TOOL-MATRIX.md`, `NONCORE-SURFACE-MAP.md`, `RUNTIME-MODES.md`, `SHIPPED-CODE-MAP.md`).

---

## 1. Classification legend (this document)

| Class | Meaning |
|-------|---------|
| `CORE MCP` | Name in `OccamToolNames`; registered when profile exposes it |
| `PROFILE-DEPENDENT MCP` | Same tools; subset via `OCCAM_PROFILE` (not a separate binary) |
| `OPT-IN MCP` | Env-gated; **not** profile-filtered |
| `CLI` | Host binary offline verbs (`OccamCliVerbs`) |
| `INSTALLER` | Bootstrap / Level A–B install paths |
| `CONNECT` | Host auto-connect platform |
| `DOCTOR` | Dependency / worker readiness |
| `BATCH` | BatchServer HTTP mode **and/or** MCP batch tools (related, not identical) |
| `WATCH` | Opt-in MCP watch (+ durable store); no separate Core daemon |
| `OTHER RUNTIME MODES` | stdio / WS / Remote process modes |
| `PACKAGE/API SURFACES` | npm bins / SDK / skill (shipped code; publish status separate) |

---

## 2. Summary counts (defensible)

### 2.1 How many distinct user-reachable entrypoints?

**Answer: 51 named entrypoints** (method below).

| Bucket | Count | What is counted |
|--------|------:|-----------------|
| CORE MCP tools | 15 | Each `OccamToolNames` entry |
| OPT-IN MCP tools | 6 | batch×3 + watch + crosscheck + atlas |
| Host offline CLI verbs | 5 | keys export, verify, install-browser, version-surface, lifecycle |
| Operator `occam <sub>` | 13 | CLI-SURFACE surface B (aliases not double-counted; includes `connect`/`doctor`) |
| Installer / bootstrap (not already a sub) | 4 | `get-ff-occam`, `install`, `verify-install`, `launch-mcp-host` |
| Alternate process modes | 3 | `--mcp-server`, `--remote`, `--batch-server` (stdio = default MCP mode, not extra; BatchServer mode = HTTP batch API) |
| Package/API bins | 3 | `@ff-occam/mcp` bin, skill install bin, agent-sdk package surface |
| Docker image ENTRYPOINT | 1 | `/app/occam` |
| **Total** | **51** | 15+6+5+13+4+3+3+1 |

Connect’s 15 host adapters are **mechanisms under** `occam connect`, not separate entrypoints.

**Method:** count every distinct **named invocation** a user/agent/operator can start from shipped code without writing new code. Do **not** count: 15 connect adapters as separate product entrypoints; worker scripts; gate/bench; help-registry-only rows that are not `occam <sub>` (those are maintainer — listed in §11 as NON-USER or ADVANCED).

### 2.2 What fraction of product capability is reachable via the 15 core MCP tools alone?

**Answer: ~60–70% of product systems; ~29% of named entrypoints; ~71% of MCP tools.**

| Denominator | Fraction | Method |
|-------------|----------|--------|
| **Product systems (PS-1…9)** | **6/9 ≈ 67%** fully or mostly reachable via core 15; **PS-7 Monitoring** needs opt-ins; **PS-9 Operator** needs CLI/install/connect; **PS-8** transports beyond stdio need binary flags | Map systems → entry classes |
| **Named entrypoints (51)** | **15/51 ≈ 29%** | Core tools / all named entrypoints |
| **MCP tool names at max surface** | **15/21 ≈ 71%** | Core / (core+opt-in) |
| **Capability families (coarse)** | Acquisition+materialization+discovery+knowledge+playbooks+trust **yes**; watch/batch/consensus/atlas **no**; doctor/install/connect/session/refresh/packaging **no** | Qualitative family cover |

**Honest product statement:** An agent with only the default 15 tools can **read pages, probe/map/search, extract knowledge, author playbooks, and do core trust flows** — and **cannot** run watch/batch/crosscheck/atlas, install/connect hosts, or start WS/Remote/BatchServer via the canonical launcher.

---

## 3. CORE MCP (15)

**Registry:** `Transport/OccamMcpServerRegistration.cs:15-32`.  
**WHO:** MCP clients (Cursor, Claude, Hermes, …) after connect/launch.  
**WHAT EXPOSES:** tools/list names below (+ profile-filtered subset).  
**WHAT DOES NOT:** opt-in tools; operator install; offline `keys`/`verify` CLI (different surface); BatchServer HTTP.

| Tool | Capability families | Stateful? | Networked? | Trust-relevant? | Public? | Advanced? | Operator? | Evidence |
|------|---------------------|-----------|------------|-----------------|---------|-----------|-----------|----------|
| `occam_client_capabilities` | Runtime budget (PS-8) | in-proc store | no | no | yes | no | no | registration `:79-80` |
| `occam_transcode` | Acquisition + materialization | optional cache | yes | receipts opt-in | yes | no | no | `:81-82` |
| `occam_probe` | Discovery | no | yes | access signals | yes | no | no | `:83-84` |
| `occam_digest` | Acquisition multi-URL | no | yes | receipts opt-in | yes | no | no | `:85-96` |
| `occam_playbook_resolve` | Playbooks | read FS/net genome | maybe | signature inspect | yes | yes | no | `:97-98` |
| `occam_map` | Discovery | no | yes | no | yes | no | no | `:99-100` |
| `occam_playbook_heal` | Playbooks | no | yes (browser) | no | yes | yes | no | `:101-102` |
| `occam_playbook_save` | Playbooks | writes FS | maybe verify | **always signs** (EF-005) | yes | yes | no | `:103-104` |
| `occam_extract_knowledge` | Knowledge | no | yes | fake receipt (EF-006) | yes | yes | no | `:105-106` |
| `occam_search` | Discovery | no | yes (provider) | no | yes | yes* | no | `:107-108` (*needs provider env) |
| `occam_verify` | Trust | no | live mode yes | **yes** | yes | yes | no | `:109-110` |
| `occam_claim_check` | Trust | no | yes | **yes** | yes | yes | no | `:111-112` |
| `occam_attest` | Trust | no | yes | partial (counts) | yes | yes | no | `:113-114` |
| `occam_playbook_lint` | Playbooks | no | no | lint only | yes | yes | no | `:115-116` |
| `occam_dataset_export` | Trust + multi | writes export | yes | **yes** | yes | yes | no | `:117-118` |

\*Search fails closed without `OCCAM_SEARCH_PROVIDER` (provider wiring in DI).

---

## 4. PROFILE-DEPENDENT MCP

**WHO:** Operators setting `OCCAM_PROFILE`.  
**WHAT EXPOSES:** Subset of the 15 (registration-time).  
**WHAT DOES NOT:** Change handler semantics; hide playbook overlay / managed / key mint / cache behavior for remaining tools; filter opt-ins.

| Profile | Tools | Evidence |
|---------|------:|----------|
| `full` (default) | 15 | `OccamToolProfile`; `PROFILE-TOOL-MATRIX.md` |
| `reader` | 7 | same |
| `researcher` | 9 | same |
| `auditor` | 12 | same |
| invalid | → `full` + stderr warn | CAP-008 |

**CAPABILITY FAMILIES:** same as core, narrower exposure.  
**STATEFUL/NETWORKED/TRUST:** inherited per tool.  
**PUBLIC?** yes (env). **ADVANCED?** yes. **OPERATOR?** yes (config).

**EF/GAP:** Server instructions can still mention `occam_watch` without gate (GAP-012) — profile text vs opt-in mismatch.

---

## 5. OPT-IN MCP (6 tools / 4 flags)

| Flag (default off) | Tools | Class notes | Stateful? | Networked? | Trust? | Evidence |
|--------------------|-------|-------------|-----------|------------|--------|----------|
| `OCCAM_BATCH_MCP=1` | `occam_batch_submit/status/results` | Also starts `BatchJobProcessor` | yes (job store) | yes | **no Receipt v1** (EF-037) | `OccamMcpServerRegistration.cs:122-130` |
| `OCCAM_WATCH_MCP=1` | `occam_watch` | Durable history | yes | yes | receipts opt-in; history verify | `:134-138` |
| `OCCAM_CONSENSUS_MCP=1` | `occam_crosscheck` | Multi-backend | no store | yes | verdict unsigned | `:142-145` |
| `OCCAM_ATLAS_MCP=1` | `occam_failure_atlas` | Telemetry sink swap | in-memory | no | no | `:149-156` |

**WHO:** Operators enabling experimental/SI surfaces.  
**NOT EXPOSED by default tools/list.**  
**NOT profile-filtered** (CAP-011).  
**PUBLIC?** advanced/opt-in. **OPERATOR?** env.  

Related EFs: EF-019/020 (watch store), EF-037/038 (batch), EF-024 WITHDRAWN (atlas leak claim).

---

## 6. CLI (host offline verbs)

**WHO:** Humans / scripts invoking the host binary **without** MCP.  
**Entry:** `Program.cs:12-15` → `OccamCliVerbs.TryRun`.

| Verb | Exposes | Does not | Families | Stateful | Net | Trust | Public | Adv | Op | Evidence |
|------|---------|----------|----------|----------|-----|-------|--------|-----|----|----------|
| `keys export` | Public key PEM | MCP tools | Trust key mgmt | reads key | no | yes | yes | yes | no | CLI-SURFACE §2 |
| `verify` | Offline receipt/citation/manifest/history verify | Live extract | Trust | no | no* | **yes** | yes | yes | no | same (*no workers) |
| `install-browser` | Playwright chromium install | MCP | Runtime deps | cache dir | yes | no | yes | no | **yes** | same |
| `version-surface` | Host version JSON | Full contract | Runtime | no | no | no | yes | yes | no | EF-023 name collision w/ `occam contract` |
| `lifecycle` | Identity / peer diagnose | Process kill | Runtime | no | no | no | no | yes | internal | INV-10 comment |

**EF-025:** `occam` wrapper does **not** route these verbs to the host binary.

---

## 7. INSTALLER

| Entrypoint | WHO | Exposes | Does not | Families | State | Net | Trust | Public | Adv | Op | Evidence |
|------------|-----|---------|----------|----------|-------|-----|-------|--------|-----|----|----------|
| `scripts/get-ff-occam.*` | New users | Fetch/install + may auto-connect | Full MCP itself | Operator | writes home | yes | supply-chain | yes | no | yes | NONCORE §G; CONNECT §1 |
| `scripts/install.ps1/.sh` | Operators | Level A/B layout | Guaranteed connect on all platforms | Operator | disk | yes | sha256 vs unsigned (EF-053) | yes | no | yes | SHIPPED-CODE-MAP |
| `scripts/verify-install.*` | Post-install | Smoke checks | Product MCP surface | Operator | no | maybe | no | yes | no | yes | NONCORE §G |
| `scripts/launch-mcp-host.mjs` | Hosts/connect | Spawns host **stdio only** | WS/Remote/Batch (CAP-1001) | Runtime | injects onboard env (**EF-050**) | no* | config integrity | yes | no | yes | RUNTIME-MODES CAP-1001 |

\*Child may network after start.

---

## 8. CONNECT

| Entrypoint | WHO | Exposes | Does not | Families | State | Net | Trust | Public | Adv | Op | Evidence |
|------------|-----|---------|----------|----------|-------|-----|-------|--------|-----|----|----------|
| `occam connect` / `occam-connect.mjs` | Operators / post-install | Detect + mutate ≤15 host MCP configs | Host tool-call proof for CONFIG_FILE (max verify L1) | Operator / PS-9 | **mutates host configs** | spawn verify | ownership heuristic | yes | no | **yes** | CONNECT-PLATFORM.md |

**15 adapters** = mechanisms under one entrypoint (HOST-CAPABILITY-MATRIX).  
**EFs:** EF-021 (rollback dead for some CONFIG_FILE+restart); desktop default mutate (CONNECT §5); EF-035 tarball may omit connect script.

---

## 9. DOCTOR

| Entrypoint | WHO | Exposes | Does not | Families | State | Net | Trust | Public | Adv | Op | Evidence |
|------------|-----|---------|----------|----------|-------|-----|-------|--------|-----|----|----------|
| `occam doctor` → `occam-doctor.ps1/.sh` | Operators | npm/playwright/dotnet publish readiness | MCP tools | Operator / runtime deps | may install | yes | no | yes | no | **yes** | CLI-SURFACE; NONCORE §G |

Also invoked from `occam refresh` path (after kill — EF-049).

---

## 10. BATCH

Two related but **distinct** entrypoints:

| Entrypoint | Class | WHO | Exposes | Does not | State | Net | Trust | Evidence |
|------------|-------|-----|---------|----------|-------|-----|-------|----------|
| MCP `occam_batch_*` | OPT-IN MCP | Agents | Job submit/status/results via MCP | Receipts (EF-037) | file store | yes | low | registration `:122-130` |
| `--batch-server` → `BatchServerHost` | OTHER + BATCH | Operators / local clients | HTTP `/v1/health`, `/v1/batch/*` | MCP registration; **no auth** (loopback only) | file store | loopback | low | `Program.cs:37-42`; CAP-006 |

**WATCH:** MCP-only opt-in (`occam_watch`); agent-driven cadence; no Core watch daemon (`WatchService` comment). Class `WATCH` + `OPT-IN MCP`.

---

## 11. OTHER RUNTIME MODES

| Mode | How | WHO | Exposes | Does not | State | Net | Trust | Public | Adv | Op | Evidence |
|------|-----|-----|---------|----------|-------|-----|-------|--------|-----|----|----------|
| stdio (default) | no flags / launcher `[]` | IDE agents | Profile+opt-in tools | — | session DI | MCP stdio | per tools | **yes** | no | no | CAP-003, CAP-1001 |
| WebSocket | `--mcp-server` | Local multi-client | Same tools over WS | Auth; launcher path | **per-socket DI** (EF-041) | loopback | banner may lie stdio (GAP-032) | advanced | yes | yes | CAP-004, CAP-1000 |
| Remote WSS+JWT | `--remote` | Remote agents | Same + JWT | Query tokens | per-socket DI + session cap | public bind possible | TLS+JWT | advanced | yes | yes | CAP-005 |
| BatchServer | `--batch-server` | Local automation | HTTP batch API | MCP tools | job store | loopback | none | advanced | yes | yes | CAP-006 |

**npm `@ff-occam/mcp`:** independently forwards WS subset (CAP-1002); unpublished / DOA-if-published (EF-034).

---

## 12. PACKAGE / API SURFACES

| Surface | WHO | Exposes | Does not | Families | State | Net | Trust | Public | Adv | Op | Evidence |
|---------|-----|---------|----------|----------|-------|-----|-------|--------|-----|----|----------|
| `packages/occam-mcp` bin | npm users | Host launch (WS subset) | In-repo clone path; Remote/Batch | Packaging | may download | yes | EF-034 files set | intended public | yes | yes | SHIPPED-CODE-MAP; CAP-1002 |
| `packages/occam-agent-sdk` | Integrators | TS client API | Host binary itself | Packaging/API | no | to host | n/a | intended | yes | no | SHIPPED-CODE-MAP |
| `packages/occam-skill` / `occam skill` | Operators | Skill card install | Accurate version/tool count (EF-036) | Packaging | overwrites skill files | maybe | no | yes | no | yes | EF-036 |
| Docker `ENTRYPOINT /app/occam` | Container ops | Host binary | Healthy probe today (**EF-051**) | Packaging | container FS | yes | HEALTHCHECK broken | yes | yes | yes | Dockerfile; EF-051 |

CI marketplace / cosign: build-time entrypoints — **not** end-user product entrypoints; trust theater EF-052/053.

---

## 13. Operator CLI remainder (inside the 13)

Already counted in §2; attributes:

| Sub | Class overlap | Stateful | Net | Trust | Op | Notable EF |
|-----|---------------|----------|-----|-------|----|------------|
| `onboard`/`settings` | INSTALLER | writes `~/.occam` | no | config | yes | feeds EF-050 |
| `refresh`/`restart` | OTHER | kills processes | no | **EF-049** name-wide kill | yes | EF-049 |
| `session` | OTHER | cookie profiles | no | **EF-054** plaintext imports | yes | EF-054 |
| `smoke` | DOCTOR-adjacent | no | yes | no | yes | — |
| `update` | OTHER | no | yes (releases API) | URL allow | yes | — |
| `snippet`/`help`/`status`/`control`/`contract`/`skill` | OPERATOR | varies | varies | contract≠version-surface (EF-023) | yes | EF-022 stale “9 tools” in refresh |

---

## 14. EF impact map (entrypoints affected — no fixes)

| EF | Entrypoints affected |
|----|----------------------|
| EF-049 | `occam refresh` / `stop-occam-processes` |
| EF-050 | `launch-mcp-host` (all connect/doctor launches using it) |
| EF-051 | Docker ENTRYPOINT health path |
| EF-052 / EF-053 | CI marketplace / install trust (not runtime MCP tools) |
| EF-034 | npm `occam-mcp` package entry |
| EF-035 | Level B tarball vs advertised `connect`/`contract` |
| EF-025 | Operator CLI vs host `install-browser`/`verify`/`keys` |
| EF-041 | WS / Remote session entry (pool) |
| EF-005 / EF-044 | Any path calling `playbook_save` / any host start (key mint) |
| EF-037 | Batch MCP + BatchServer results |

---

## 15. Corrections to prior model

1. Reject “15 tools = product.” Use layered entrypoint model (§0–§2).  
2. Profiles and opt-ins are **orthogonal** (reader + batch is legal).  
3. BatchServer ≠ MCP batch tools (shared store/processor family, different exposure).  
4. Canonical launcher ≠ full transport matrix (CAP-1001).  
5. Connect’s 15 adapters ≠ 15 extra entrypoints.

---

## 16. Uncertainty

| Item | Status |
|------|--------|
| Whether to count help-registry-only maintainer scripts (build-release, gates) as user entrypoints | **Excluded** from 51 by method; would raise count ~8–12 if included |
| Exact “% capability” if weighted by CAP count (674) | Not used — CAP inventory is flat/pre-hierarchy; PS-system method preferred |
| Unpublished npm reachability | Code ships; registry 404 — still an entrypoint **in tree** |
