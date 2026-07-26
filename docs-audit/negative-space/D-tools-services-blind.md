# W4-D — Blind negative-space audit (MCP tools / services / digest / search / session / client / agent-hints)

**Owner:** W4-D  
**SoT:** shipped C# under scoped paths (code-first; prior `docs-audit/*` consulted only after §1).  
**Date:** 2026-07-26  

---

## 1. Blind inventory

Externally meaningful behaviors discovered from code **before** opening prior audit artifacts.

### 1.1 MCP tool parameter surfaces (bound params)

| Tool | Env gate | Bound parameters (defaults) |
|------|----------|-----------------------------|
| `occam_transcode` | core | `url`; `backend_policy=http_then_browser`; `max_tokens?`; `fit_markdown=false`; `focus_query?`; `content_selectors?`; `session_profile?`; `playbook_policy=auto`; `if_none_match?`; `semantic_chunking=false`; `capture_screenshot=false`; `json_blocks=false`; `json_tables=false`; `json_feed=false`; `translate_to?`; `diff_against?`; `prefer_llms_txt=false`; `cache_ttl_s?`; `emit_capsule=false`; `rank_blocks=false`; `tag_trust=false`; `delta_only=false` — `Tools/OccamTranscodeTool.cs:46-67` |
| `occam_probe` | core | `url`; `timeout_ms=10000`; `include_social_meta=false`; `session_profile?` — `OccamProbeTool.cs:14-17` |
| `occam_digest` | core | `urls?` (JsonElement); `backend_policy`; `max_urls=8`; `per_url_max_tokens?`; `focus_query?`; `fit_markdown=true`; `include_combined=true`; `session_profile?`; `source_url?`; `max_links=8`; `if_none_match?` — `OccamDigestTool.cs:19-29` |
| `occam_map` | core | `url`; `source=homepage`; `max_links=32`; `same_domain=true`; `filter_nonsense=true`; `focus_query?`; `timeout_ms=15000`; `session_profile?` — `OccamMapTool.cs:14-21` |
| `occam_search` | core (needs env) | `query`; `max_results=8`; `rerank=false` — `OccamSearchTool.cs:18-20` |
| `occam_client_capabilities` | core | `context_tokens?`; `model_id?`; `clear=false` — `OccamClientCapabilitiesTool.cs:23-28` |
| `occam_playbook_resolve` | core | `url`; `schema_version=1.0`; `include_lessons=false`; `fetch_site_genome=false` |
| `occam_playbook_heal` | core | `url`; `failure_reason`; `session_profile?`; `max_skeleton_nodes=600` |
| `occam_playbook_save` | core | `url`; `playbook_json`; `verify=true`; `verify_url?`; `lesson_note?`; `failure_reason?`; `host_id?` |
| `occam_playbook_lint` | core | `playbook_json` |
| `occam_extract_knowledge` | core | `url`; `backend_policy`; `session_profile?` |
| `occam_verify` | core | `receipt`; `markdown?`; `public_key?`; `mode=offline`; `block_index?`; `block_text?`; `block_selector?`; `proof?`; `chunks?` |
| `occam_claim_check` | core | `claim`; `url`; `backend_policy`; `session_profile?`; `max_matches=3` |
| `occam_attest` | core | `claims` (JSON); `backend_policy`; `session_profile?` |
| `occam_dataset_export` | core | `urls` (JSON); `backend_policy`; `session_profile?` |
| `occam_batch_submit/status/results` | `OCCAM_BATCH_MCP=1` | submit: `urls`, `backend_policy`, `focus_query?`, `max_tokens?`, `fit_markdown=true`, `session_profile?`, `playbook_policy=auto`, `idempotency_key?`, `on_oversize=fail`; status: `job_id`; results: `job_id`, `cursor=0`, `limit=50` |
| `occam_watch` | `OCCAM_WATCH_MCP=1` | `url`; `backend_policy`; `focus_query?`; `session_profile?`; `playbook_policy=auto`; `include_diff=true`; `reset=false`; `include_history=false` |
| `occam_crosscheck` | `OCCAM_CONSENSUS_MCP=1` | `url`; `vantages="http,browser"` (tokens `http`\|`browser` only); `session_profile?`; `focus_query?` |
| `occam_failure_atlas` | `OCCAM_ATLAS_MCP=1` | `only_walled=false` |

**Helpers (not MCP tools):** `BlockDiff` (`Tools/BlockDiff.cs`) — block hash delta for `diff_against`/`delta_only`; `SearchExtractabilityScorer` — shared probe→[0,1] score for probe response + search rerank.

### 1.2 `[Description]` vs behavior (notable)

- **Transcode:** Descriptions generally match; ambient `max_tokens` via `ClientCapabilityStore` is disclosed. Silent policy downgrade when browser missing + no auto-provision (`playwright_browser_missing_downgrading_to_http`) — `OccamTranscodeTool.cs:139-145`. `delta_only` ignored with warning if missing `diff_against`/`json_blocks` — `:230-242`.
- **Digest:** Description claims per-entry `focus_query` override; preferred JSON **array** path rejects objects (`DigestInputNormalizer.cs:74-77`) — per-entry focus only via legacy string-encoded JSON array (`DigestUrlParser.cs:43-47`).
- **Search:** Tool description ends `Returns { title, url, snippet }` (`OccamSearchTool.cs:16`) but success also returns `provider`, `count`, `agentHints`, and with `rerank` also `extractability`/`recommendedBackend`.
- **Probe:** Claims extractability 0–1 — implemented via same scorer (`OccamProbeModels.cs` mapper + `SearchExtractabilityScorer`).
- **Crosscheck:** `vantages` cannot express `http_then_browser`; session doubles axis to anon+authed per backend.

### 1.3 Search providers

- **Count:** 3 — `SearxngProvider`, `BraveProvider`, `TavilyProvider` registered in `OccamServiceCollectionExtensions.cs:91-93`.
- **Env:** `OCCAM_SEARCH_PROVIDER` (required name); `OCCAM_SEARCH_URL` (required for searxng; optional base override for brave/tavily); `OCCAM_SEARCH_API_KEY` (required brave/tavily; optional Bearer for searxng); `OCCAM_SEARCH_TIMEOUT_MS` default 20s (`:89-90`).
- **Rerank cost:** `rerank=true` → up to `max_results` live `ProbeService.AnalyzeAsync` calls, timeout **6000ms** fixed, parallelism **5** (`OccamSearchTool.cs:13-14,71-122`). Probe failures → score `0.4`, keep hit. No session on rerank probes.

### 1.4 TranslationService (LibreTranslate)

- **Reachable when:** `translate_to` set on transcode **and** `OCCAM_TRANSLATE_URL` non-empty (`TranslationService.cs:29-34`, used from `OccamTranscodeTool.cs:202`).
- **Endpoint:** `POST {OCCAM_TRANSLATE_URL}/translate` with `{q,source=auto,target,format=text,api_key?}` — `:62-64`.
- **Env:** `OCCAM_TRANSLATE_URL`, `OCCAM_TRANSLATE_API_KEY`, `OCCAM_TRANSLATE_TIMEOUT_MS` (DI `:80-81`).
- **Semantics:** Non-fatal warnings (`translate_endpoint_unconfigured`, `translate_http_*`, `translate_empty_response`, `translate_timeout`, `translate_error`). **Sync-over-async:** `PostAsync(...).GetAwaiter().GetResult()` — `:64`.

### 1.5 Proxy rotation

- **Ingest:** `OCCAM_PROXY_LIST_FILE` if path set **and** `File.Exists` → parse file only (no fallback to inline even if empty) — `ProxyListParser.cs:10-17`. Else `OCCAM_PROXY_LIST` split on `,;/\n/\r`. File: URL lines or CSV `ip,port,protocol(s)`; `socks4` rejected; `#` comments — `:20-170`.
- **Round-robin:** `Interlocked.Increment` — `RoundRobinProxyRotationService.cs:30-32`.
- **One-shot side effect:** `EgressProxyConfig.ApplyForSpawn` acquires next proxy per worker spawn; runners skip daemon/pool when rotation configured (call sites in Http/Browser extract runners — observed via `ApplyForSpawn` / interface docs).

### 1.6 RobotsThrottleService

- **Default OFF:** both `OCCAM_RESPECT_ROBOTS=false` and `OCCAM_HOST_THROTTLE_MS=0` → immediate null, **no** robots.txt fetch — `RobotsThrottleService.cs:31-36`.
- **When on:** `User-agent: *` group only; **Allow not modeled** (any Disallow prefix blocks) — `:143-146`; fail-open on robots fetch error — `:107-112`; Crawl-delay max with throttle; per-host queue via `_nextAllowedTicks` — `:55-88`. Failure code `robots_disallowed`. Wired from `TranscodePipeline`.

### 1.7 FeatureDiscoveryService

- **Not an MCP surface.** Agents do not call it. Effects: `IsBrowserAvailable()` + `WillAutoProvisionBrowser()` (lazy Node spawn of `provision-gate.mjs`, 10s, **fail-open assume true**) gate silent HTTP downgrade in transcode and browser-backend provision expectation — `FeatureDiscoveryService.cs:12-141`, `OccamTranscodeTool.cs:139-145`.

### 1.8 ClientCapabilityStore

- Process singleton; env bootstrap `OCCAM_CLIENT_CONTEXT_TOKENS` + `OCCAM_CLIENT_MODEL_ID` — `ClientCapabilityStore.cs:93-108`.
- Output budget = 20% context clamped 512–16384 — `:11-17,81-85`.
- `suggestedProfile` advisory (`reader`/`researcher`/`full`) — no auto `OCCAM_PROFILE` link — `:87-91`.
- **Cache/materialization identity side effect:** `ResolveMaxTokens` feeds options → `MaterializationKey` / cache key include `max_tokens` — ambient configure mid-session changes identity of later omit-`max_tokens` calls (`MaterializationKey.cs:34`, `OccamTranscodeTool.cs:78`).

### 1.9 Session

- `SessionProfileHeaders`: `OCCAM_SESSIONS_ROOT` or user-data `sessions/`; id sanitization; blocked header names; `storageState` containment — `SessionProfileHeaders.cs`.
- `RequestHeadersMerger`: `OCCAM_REQUEST_HEADERS_FILE`; session wins — `RequestHeadersMerger.cs`.
- `FetchPreflight` + `FetchHeadersScope` (AsyncLocal temp headers file) — privacy block, merge, worker handoff.
- `OccamFetchDefaults`: loads `profiles/occam-fetch-defaults.json` UA/Accept.

### 1.10 Digest / Map services (scope)

- Digest: AF-5 `source_url` ignores `urls`; parallel via `DigestParallelism` (`OCCAM_DIGEST_PARALLEL=0` → 1; `OCCAM_DIGEST_MAX_PARALLEL`; else HTTP≤4 / browser via browser concurrency); whole-batch preflight gate; AF-6 `if_none_match` on **combined**; `FocusNotFound` honesty flag.
- Map: homepage/sitemap/robots; Soft404 heuristic; link filter/ranker; hub expansion; shared by digest discovery.

### 1.11 Agent hints honesty

| Module | Behavior |
|--------|----------|
| `ProbeAgentHints` | Proactive nudges: tables→`json_tables`, llms link→`prefer_llms_txt`, feed content-type→`json_feed`, large HTML≥750KB→budget, paywall/login/challenge — `ProbeAgentHints.cs:63-91`. Anti-bot blog tier → `suggestedNext=none`. |
| `TranscodeAgentDecisions` | Failure→action map; **`ThinExtractBrowserExhausted`** overrides retry-browser when backend already browser/playwright — `TranscodeAgentDecisions.cs:7-13`, wired `OccamTranscodeTool.cs:569-578` (also clears heal offer). |
| `DigestAgentHints` | `suggestedReadOrder`, `focus_not_found`, hub-excerpt heuristic (≥8 `[` + Guide/TOC phrases) — `DigestAgentHints.cs`. |
| `FailureAgentHints` | Thin wrapper over `TranscodeAgentDecisions.ForFailure` for multi-tool failures. |

### 1.12 Config reverse list (scope)

`OCCAM_SEARCH_*`, `OCCAM_TRANSLATE_*`, `OCCAM_ROBOTS_TIMEOUT_MS`, `OCCAM_RESPECT_ROBOTS`, `OCCAM_HOST_THROTTLE_MS`, `OCCAM_PROXY_LIST`, `OCCAM_PROXY_LIST_FILE`, `OCCAM_DIGEST_PARALLEL`, `OCCAM_DIGEST_MAX_PARALLEL`, `OCCAM_CLIENT_CONTEXT_TOKENS`, `OCCAM_CLIENT_MODEL_ID`, `OCCAM_SESSIONS_ROOT`, `OCCAM_REQUEST_HEADERS_FILE`, `OCCAM_HOME` (worker check), `PLAYWRIGHT_BROWSERS_PATH` (FeatureDiscovery), batch/watch/consensus/atlas gates (tool registration outside this folder but tools live here).

### 1.13 Automatic / silent

| Behavior | Trigger | Visible? | Configurable? | Disableable? |
|----------|---------|----------|---------------|--------------|
| Ambient max_tokens | omit max_tokens + store configured | via budget / smaller md | tool/env | clear / explicit max_tokens |
| MaterializationKey shift | ambient budget change | only if client tracks keys | yes | explicit max_tokens |
| Browser→HTTP downgrade | no browser & no provision | warning string | provision-gate / install | install browser |
| Provision-gate assume-on | probe fail/timeout | stderr only | n/a | — |
| Robots/throttle | env on | `robots_disallowed` / latency | env | leave unset |
| Proxy one-shot | pool configured | perf (no daemon) | unset pool | unset pool |
| Translate | `translate_to` | sidecar or warning | URL env | omit param |
| Search rerank probes | `rerank=true` | latency + fields | param | default off |
| Digest parallel | multi-URL | latency | env | `PARALLEL=0` |
| Atlas/watch/batch/consensus | host env | tool presence | env | off default |

---

## 2. Gap classification

Compared against `CAPABILITY-INVENTORY.md`, `capabilities.json`, `ARTIFACT-MAP.md`, `CODE-DERIVED-WORKFLOWS.md`, `ENVIRONMENT-VARIABLES.md`, relevant `tools/*.md` / `subsystems/*.md`.

| ID | Finding | Classification | Evidence |
|----|---------|----------------|----------|
| G-D1 | Most tool params, search trio, rerank fan-out, client budget, digest AF-5/6, map filters, proxy CSV/round-robin/one-shot, robots default-off, FeatureDiscovery provision-gate, Probe/Digest agent hints, CAP-400–404 ambient identity | COVERED_EXACTLY | Wave1–2 CAPs (e.g. 081, 103, 162–164, 370–382, 400–404, 425, 434, 451–460, 620–631, 206b) |
| G-D2 | CAP-106 failure hints omit **browser-exhausted thin_extract** special case (stop + no heal); failure table still “Sometimes (try browser)” | COVERED_WRONG / MISSING_EDGE | `OccamTranscodeTool.cs:569-593`; `TranscodeAgentDecisions.ThinExtractBrowserExhausted`; `occam_transcode.md` CAP-106 + failure table ~862 |
| G-D3 | `SearchExtractabilityScorer` ignores `LikelyPaywall` (only login/challenge/JS/class); probe hints warn paywall but rerank may still rank paywall pages ~0.7–0.9 | MISSING_EDGE | `SearchExtractabilityScorer.cs:12-61` vs `ProbeAgentHints.cs:88-91` / classifier paywall strings |
| G-D4 | Robots parser: Allow unmodeled + fail-open on robots fetch — not called out in CAP-103/370 beyond “disallow can block” | MISSING_SECURITY_SEMANTIC | `RobotsThrottleService.cs:107-112,143-146` |
| G-D5 | `OCCAM_PROXY_LIST_FILE` existing empty/invalid file suppresses `OCCAM_PROXY_LIST` (no fallback) | MISSING_CONFIG | `ProxyListParser.cs:10-17` vs CAP-381 “file wins” |
| G-D6 | No ARTIFACT-MAP row for `translatedMarkdown` / translate warning sidecar | MISSING_ARTIFACT | `TranslationService` + transcode response; `ARTIFACT-MAP.md` has no translate ART |
| G-D7 | `TranslationService.Translate` blocks thread via `GetAwaiter().GetResult()` — not in CAP-081/379 | MISSING_FAILURE_SEMANTIC | `TranslationService.cs:64` |
| G-D8 | Search MCP `[Description]` understates response shape / rerank cost (partially noted as “hidden” in tool audit, not as description↔schema wrongness) | COVERED_PARTIALLY | `OccamSearchTool.cs:16` vs CAP-627/631 |
| G-D9 | Digest preferred-array vs per-entry focus | COVERED_EXACTLY | CAP-451 |
| G-D10 | Proxy one-shot / Core HttpClient no proxy | COVERED_EXACTLY | CAP-164/166 |
| G-D11 | FeatureDiscovery not agent-callable (operator/internal) | COVERED_EXACTLY | CAP-206b / transcode downgrade |
| G-D12 | Client suggestedProfile / model_id decorative / clear precedence | COVERED_EXACTLY | CAP-401–403 |
| G-D13 | Crosscheck vantages parsing quirks | COVERED_EXACTLY | CAP-866 etc. |
| G-D14 | FLOW for search rerank / client→transcode | COVERED_EXACTLY | FLOW-001/004 |
| G-D15 | No dedicated FLOW for translate_to / robots-polite crawl / ambient-budget identity shift as first-class workflow | MISSING_WORKFLOW | `CODE-DERIVED-WORKFLOWS.md` lacks translate/robots/ambient-shift flows (CAP-404 documents shift as CAP not FLOW) |

**Proposed new CAPs (orchestrator allocates):**
- `CAP-NEW-D-1` — Browser-exhausted `thin_extract` agent-decision override (stop; suppress heal/retry).
- `CAP-NEW-D-2` — Paywall signal asymmetry: probe warn vs search scorer omission.
- `CAP-NEW-D-3` — Robots Allow-omission + robots-fetch fail-open posture.

**Proposed edges:**
- `TOOL:occam_transcode|OVERRIDES|CAP-106` when thin_extract+browser → `CAP-NEW-D-1`
- `CAP-425/628|OMITS|signal:LikelyPaywall` → honesty gap vs `CAP-434`
- `env:OCCAM_PROXY_LIST_FILE|BLOCKS|env:OCCAM_PROXY_LIST` when file exists empty
- `PARAM:translate_to|PRODUCES|ART-NEW-translate-sidecar`
- `ClientCapabilityStore|MUTATES|MaterializationKey` (edge already in wave2 extract; reinforce as FLOW)

**EFC proposals:**
- `EFC-D-1` PERFORMANCE — sync `GetAwaiter().GetResult()` on LibreTranslate HTTP (`TranslationService.cs:64`). Confidence: PROVEN.
- `EFC-D-2` BUG-CANDIDATE — empty `OCCAM_PROXY_LIST_FILE` disables inline list without diagnostic (`ProxyListParser.cs:10-17`). Confidence: PROVEN.
- `EFC-D-3` DESIGN-QUESTION — `SearchExtractabilityScorer` vs `LikelyPaywall` honesty for `rerank`. Confidence: PROVEN.

---

## 3. Compact envelope (copy for orchestrator)

```
OWNER: W4-D
SCOPE_FILES_READ: ~64 (Tools/**, Services/**, Digest/**, Search/**, Session/**, Client/ClientCapabilityStore.cs, Agent/**; plus post-blind CAPABILITY-INVENTORY, capabilities.json hits, ARTIFACT-MAP, CODE-DERIVED-WORKFLOWS, tools/occam_{transcode,search,digest,probe,client_capabilities}.md, subsystems/{config-env,network-fetch-proxy,browser-workers,consensus-crosscheck}.md, ENGINEERING-FINDINGS.md)
BLIND_BEHAVIORS: 48
GAPS: covered_exact=18 partial=2 wrong=1 missing_cap=3 missing_edge=3 missing_artifact=1 missing_workflow=1 missing_config=1 missing_failure=1 missing_security=1 dead_as_product=0 product_as_internal=0
TOP_MISSED:
1. ThinExtractBrowserExhausted not in CAP-106 — OccamTranscodeTool.cs:569-578
2. Scorer ignores LikelyPaywall — SearchExtractabilityScorer.cs:12-61
3. Robots Allow omitted + fail-open — RobotsThrottleService.cs:107-146
4. Empty PROXY_LIST_FILE suppresses inline — ProxyListParser.cs:10-17
5. No ART for translatedMarkdown — TranslationService + transcode
6. Translate sync-block GetResult — TranslationService.cs:64
7. Search Description understates shape/cost — OccamSearchTool.cs:16
8. No FLOW for translate / robots-polite / ambient Key-shift — CODE-DERIVED-WORKFLOWS.md
NEW_CAP_CANDIDATES: CAP-NEW-D-1, CAP-NEW-D-2, CAP-NEW-D-3
NEW_EDGES: thin_extract+browser→stop/no-heal; scorer⊖LikelyPaywall; PROXY_LIST_FILE∃empty⊣PROXY_LIST; translate_to→ART-translate; ClientStore→MaterializationKey (reinforce)
NEW_ARTIFACTS: translatedMarkdown + translate warning codes (propose ART-NEW-D-translate)
NEW_WORKFLOWS: FLOW-NEW-D-translate; FLOW-NEW-D-robots-polite; FLOW-NEW-D-ambient-budget-identity (or extend FLOW-001)
AUTOMATIC_SILENT: ambient max_tokens→MaterializationKey/cache; browser downgrade+provision fail-open; robots/throttle env; proxy one-shot; digest parallel; search rerank probe fan-out
FAILURE_FALLBACK: translate non-fatal; robots fetch fail-open allow-all; rerank probe→0.4; FeatureDiscovery assume provision=true; proxy empty pool→static OCCAM_HTTP_PROXY
CONFIG_GAPS: empty PROXY_LIST_FILE; shared OCCAM_SEARCH_API_KEY across providers (already CAP); RerankProbeTimeoutMs not env-tunable
PLATFORM_DIFFS: none material in scope (FetchHeadersScope AsyncLocal; Node path for provision-gate)
EFC: EFC-D-1 PERFORMANCE sync LibreTranslate; EFC-D-2 empty proxy-file swallows inline; EFC-D-3 paywall vs rerank scorer
CONVERGENCE_IN_SCOPE: YES — Wave1–2 already modeled the bulk of tools/services/search/client/proxy/robots; residual gaps are edge honesty/config/artifact/workflow, not whole missing subsystems
UNCERTAINTIES: whether managed backends invoke RobotsThrottle identically; exact worker daemon skip lines (runners outside Services/ but interface-proven); gate coverage for ThinExtractBrowserExhausted
```
