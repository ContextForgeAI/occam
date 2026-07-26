# PRODUCT-TAXONOMY (Phase 5C)

**Agent:** P5-02
**SoT:** executable code. Wave-4 correction layer overrides older prose (C1–C9).
**Inputs:** `canonical-capabilities.json` (674 CAPs → 9 systems / 39 families / 38 product capabilities), `PRODUCT-ARCHITECTURE.md`, `ENTRYPOINT-MODEL.md`, `TRUST-MODEL.md`, `ACQUISITION-ROUTING-MODEL.md`, `STATE-MODEL.md`, `AUTOMATION-MODEL.md`, `ARTIFACT-ONTOLOGY.md`.
**Public docs (`docs/`, `README.md`, `llms.txt`) were not used as an input and are not an authority here.**
**Date:** 2026-07-26

---

## 0. Verdict on the 9-system / 39-family baseline

**The 9 systems survive. The 39-family layer does not — it becomes 38, with two families re-parented.**

| Change | From | To | Kind | Evidence |
|--------|------|----|------|----------|
| **T-1** | `quality-failure-semantics` in **PS-2 Materialization** | **PS-1 Acquisition** | MOVE (slug unchanged) | The post-processor pipeline is registered as one unit (`OccamServiceCollectionExtensions.cs:34-36`) and runs on the **router result before any materialization** (`TranscodePipeline.cs:152-157`); a failed outcome returns at `:159-174` and never reaches `FinishMaterialize` (`:176-181`). All six members (CAP-094/097/098/105/106/108) are router/post-processor/failure-taxonomy behavior, not compile behavior. |
| **T-2** | `digest-synthesis` in **PS-3 Discovery** | **PS-7 Monitoring and multi-source** | MOVE (slug unchanged) | Digest is not discovery: it fans out to the acquisition spine per URL (`DigestService.cs:309` → `pipeline.TranscodeAsync`) and only *delegates* discovery to `MapService` when `source_url` is given (`DigestService.cs:460-462`). `PRODUCT-ARCHITECTURE.md` §1 classifies `DigestService` in L7 with watch/consensus/batch. Members are fan-out, clamps, combine, per-item receipts, read-order hints (CAP-450…460). |
| **T-3** | `canonical-knowledge-ir` as a **PS-4 family** | **SHIPPED_DEAD evidence cluster** (slug retained, not a product family) | DEMOTE | All four members are `SHIPPED_DEAD` (CAP-328/330/332/333) and the family is the **only one in the corpus with no `PRODUCT_CAPABILITY` parent**. The pipeline forces internal IR features (`TranscodePipeline.cs:44-47`) and the live bundle passes `Canonical: null` (`:259`); AUTOMATION #18 records build-then-discard as CPU waste. There is no user-reachable behavior to name. |
| **T-4** | PS-1…PS-9 flat peers | **PS-1…PS-7 value systems + PS-8/PS-9 enabling systems** | STRATIFY (no renumber, no rename) | See §1.3. Both classes stay top-level; they are not peers in the "what does Occam do for me" sense. |
| **T-5** | six cross-cutting properties | **seven** — adds *agent-facing response contract / honesty signals* | ADD LENS | §7.2. The only genuine hole found; it cannot become a family without breaking the one-family-one-system rule, because its members already live in five different systems. |

**Resulting invariant (a strong argument for T-3):** after the demotion, **every family has exactly one canonical product capability — 38 families, 38 product capabilities.** Before T-3 the counts were 39/38 with one unexplained orphan.

**Counts after this file**

| Quantity | Before (5B) | After (5C) |
|----------|------------:|-----------:|
| Product systems | 9 | 9 (7 value + 2 enabling) |
| Capability families | 39 | **38** |
| Canonical product capabilities | 38 | 38 (unchanged) |
| Raw CAP records | 674 | 674 (unchanged — nothing renumbered or deleted) |
| Shipped-dead evidence clusters | 0 named | 1 (`canonical-knowledge-ir`, 4 CAPs) |

**Migration table (downstream files)**

| Old reference | New reference |
|---------------|---------------|
| `quality-failure-semantics` → PS-2 | `quality-failure-semantics` → **PS-1** |
| `digest-synthesis` → PS-3 | `digest-synthesis` → **PS-7** |
| `canonical-knowledge-ir` → PS-4 family | `canonical-knowledge-ir` → **PS-4 dead-evidence cluster** (do not list as a product area) |
| "39 families" | **"38 families"** |
| PS-3 = probe/map/search/digest | PS-3 = **probe/map/search** |
| PS-7 = batch/watch/consensus/atlas | PS-7 = **digest/batch/watch/consensus/atlas** |

No family slug was renamed. No CAP was renumbered. `canonical-capabilities.json` is owned by the orchestrator; the reassignments above are stated here as the canonical structural verdict for it to apply.

---

## 1. Stress test of the hypothesis — every challenge, with its verdict

### 1.1 "Are all nine really peer-level product systems?"

| Challenge | Verdict | Evidence |
|-----------|---------|----------|
| **Is Discovery (PS-3) a peer of Acquisition (PS-1), or a mode of it?** | **PEER — challenge failed** (after T-2 removes digest) | Probe, map and search never enter the acquisition spine: they bypass `TranscodePipeline` and `OccamRouter` entirely (`PRODUCT-ARCHITECTURE.md` §3; `ProbeService`/`MapService` → `HttpProbeFetcher`, `SearchService` → provider HTTP). They are structurally HTTP-only and **never escalate** (CAP-420 "confirmed HTTP-only / backend-isolated"; CAP-510 "HTTP-only design, no backend escalation ever"). They emit non-content artifacts (ART-011/012/013), not page bodies. They answer a different question — *which URL, and is it worth fetching* — and `occam_search`/`occam_map` have standalone value with zero extraction. A "mode of acquisition" would share the router; none of them do. |
| **Is Operator surface (PS-9) a product system or just an exposure surface?** | **PRODUCT SYSTEM, but of the *enabling* class** | It is not exposure: exposure is PS-8. PS-9 *changes the machine* — it installs runtime assets, mutates ≤15 third-party host config files with backups (ART-031, EF-021), writes onboarding env that is silently injected into every later launch (ART-029, EF-050), kills processes machine-wide by binary name (EF-049), authors credential-bearing session profiles (ART-026/037), and builds/distributes release artifacts (ART-032/038). It owns 159 CAPs, four artifact groups, and fifteen EFs of its own. That is product behavior with its own failure surface, not a view onto other systems. It is *enabling* because none of it produces content, knowledge or proofs. |
| **Is Runtime and exposure (PS-8) a product system?** | **ENABLING SYSTEM — kept, reclassified** | Its families decide *what is reachable*, not *what is done*: transports (CAP-002…006), the 15-name registry plus profile filtering and four env gates (CAP-007…015), and the ambient client budget (CAP-400…404). Two of its five client-context members are inert (CAP-402 `model_id` "stored, echoed, never consumed"; CAP-403 `suggestedProfile` "zero automated linkage"). Kept top-level because `OCCAM_PROFILE` and the opt-in gates genuinely remove capability from a deployment (EFC-P5-05-4: `reader` can produce receipts but cannot verify them) — that is a product decision, not a rendering detail. |
| **Is Knowledge extraction (PS-4) big enough to be a system?** | **PEER — kept, flagged as the thinnest system** | After T-3 it is a single live family (13 CAPs). It survives because it is a **separate spine**: `occam_extract_knowledge` bypasses `TranscodePipeline`/`OccamRouter` entirely (CAP-591), uses its own worker (`css-extract.mjs`, CAP-592), its own temp artifact (ART-036), its own output artifact (ART-014) and its own narrower failure taxonomy (CAP-601). It is a *consumer* of PS-5 (Recipe D: resolve → schema-match → extract, CAP-590), not a part of it. |
| **Is Playbooks (PS-5) a system or an overlay on PS-1?** | **PEER — kept** | Wave-4 STATEMENT_7 correctly says playbooks are *in-band overlays* on the transcode spine at **runtime**. But resolution, authoring, healing and validation form an independently operated lifecycle with three of their own MCP tools, their own on-disk artifact (ART-015, always signed — EF-005), their own four-tier precedence engine (CAP-491), their own trust boundary (community integrity ≠ authenticity, G-E-03) and their own supply chain (EF-052). Overlay-at-runtime and system-in-the-product are not in conflict. |

### 1.2 "Do any families belong to a different system?"

Two do — T-1 and T-2 above. Three more were challenged and **kept**:

| Family | Challenge | Verdict |
|--------|-----------|---------|
| `response-cache` (PS-2) | It skips acquisition entirely, so is it PS-1? | **Keep in PS-2.** It caches the *materialized* post-sign envelope keyed by materialization inputs (ART-035; `TranscodeResponseCache`), not a fetch. `ACQUISITION-ROUTING-MODEL.md` lists it as "Rung 0 — acquisition-adjacent gate", i.e. a PS-2 object read before PS-1 runs. |
| `client-context` (PS-8) | Its only live effect is the default `max_tokens`, i.e. PS-2 token budgeting. | **Keep in PS-8**, with a declared cross-system contract. It is a session-runtime store (`ClientCapabilityStore`, process-scoped) exposed as its own tool; PS-2 *consumes* its output (CAP-404 "ambient budget mutates transcode/digest cache & materialization identity"). Ownership follows the store, not the consumer. |
| `session-fetch` (PS-1) vs `operator-cli` session verbs (PS-9) | Same word, two systems. | **Keep split.** PS-9 *authors* session profiles (`occam session import/export-state` → ART-026/ART-037, EF-054); PS-1 *consumes* them under a three-tier reach model (Tier 1 headers+storageState, Tier 2 HTTP-only, Tier 3 headers-only — `session-lifecycle`). Producer and consumer are genuinely different behaviors with different failure modes. |

### 1.3 The stratification (T-4)

| Class | Systems | Test that separates them |
|-------|---------|--------------------------|
| **Value systems** | PS-1 Acquisition · PS-2 Materialization · PS-3 Discovery · PS-4 Knowledge extraction · PS-5 Playbooks · PS-6 Trust and provenance · PS-7 Monitoring and multi-source | Removing the system removes an outcome the caller wanted (content, a diagnosis, typed facts, a recipe, a proof, a comparison). |
| **Enabling systems** | PS-8 Runtime and exposure · PS-9 Operator surface | Removing the system removes *access to* or *operability of* the value systems; no caller outcome disappears from the model. |

This matters for documentation: a handbook that gives PS-9's 159 CAPs the same billing as PS-1's 122 will read as an installer manual with a web reader attached.

### 1.4 "Are any families the same family under two names?"

No family-level duplicates survived. Four **CAP-level** duplicates/misfilings were found and are proposed to the orchestrator (family membership is a CAP field this file does not own):

| # | CAP | Currently | Should be | Evidence |
|---|-----|-----------|-----------|----------|
| R-1 | CAP-088 `tag_trust` | `acquisition-routing` (PS-1) | `focus-selection` (PS-2), **alias of CAP-317** | CAP-317 is "`rank_blocks`/`tag_trust`: post-hoc block annotation, requires `json_blocks=true`". Same feature, two families. |
| R-2 | CAP-178 "Generic (non-per-site) consent/cookie-banner dismissal" | `session-fetch` | **alias of CAP-211** in `access-consent` | CAP-211 is "Generic consent/cookie-banner auto-dismiss." Identical mechanism. |
| R-3 | CAP-084 `prefer_llms_txt` | `structured-materialization` (PS-2) | `acquisition-routing` (PS-1) | It substitutes the fetched URL before any compile (`OccamTranscodeTool.cs:147-164`); `ACQUISITION-ROUTING-MODEL.md` models it as Rung 0b. |
| R-4 | CAP-086 `emit_capsule` | `structured-materialization` (PS-2) | `receipts` (PS-6) | The capsule is a trust container wrapping the signed envelope (ART-006, `CapsuleCodec.cs`); it materializes nothing. |
| R-5 | CAP-326 "`occam_watch`: stateful wrapper reusing the same transcode options/diff plumbing" | `differential-materialization` (PS-2) | `change-monitoring` (PS-7) | It describes the watch tool, not the diff codec. |
| R-6 | CAP-308 `FitMarkdown.Apply` | `differential-materialization` (PS-2) | `token-budget` (PS-2) — same system, wrong family | `fit_markdown` is CAP-063 in `token-budget`; CAP-308 is its implementation. |

R-1…R-6 are **classification corrections only**; no CAP is deleted and no ID moves system except R-3/R-4/R-5.

### 1.5 "Is any family a bug or dead code masquerading as a product area?"

| Family | Verdict |
|--------|---------|
| `canonical-knowledge-ir` | **Yes — demoted (T-3).** Four dead CAPs, zero product capability, live cost with no live output. |
| `response-cache` | **No, but half-buggy.** CAP-085/321 are real opt-in behavior; CAP-315 is `BUGGY` (key omits `rank_blocks`/`tag_trust`/`emit_capsule` — EF-001) and CAP-322 documents TTL-checked-only-on-read. Keep the family; never document CAP-315 as a feature. |
| `failure-atlas` | **No.** Real per-session aggregation; the "process-wide leak" claim was **withdrawn** (EF-024, C2). CAP-874 confirms non-persistence is total and intended. |
| `managed-acquisition` | **No, but off by default and privacy-loaded.** Real third-party escalation; EF-003 (no SSRF guard on the managed client) and the unset-`OCCAM_MANAGED_DOMAINS`-means-all-hosts default are limitations, not the whole family. |
| `packaging-distribution` | **No, but partly ceremonial.** ART-038 cosign bundle has no shipped consumer (EF-053) and the Docker HEALTHCHECK is broken (EF-051). The family is real; two of its outputs prove nothing. |

Dead members inside otherwise-live families (CAP-331/334/335 in `structured-materialization`, CAP-303 in `token-budget`, CAP-264/279/286/287 in `receipts`, CAP-324 in `differential-materialization`, CAP-436, CAP-496, CAP-552/553, CAP-595/600, CAP-840, CAP-864, CAP-1003, CAP-165/166/188, CAP-248a/248b) stay where they are, marked `SHIPPED_DEAD`. Per C8 they **do ship** in the binary; they are simply not product behavior.

### 1.6 "Does any real product area have no family?"

Four candidates were tested. One is a genuine hole.

| Candidate | Verdict |
|-----------|---------|
| **Agent-facing response contract / honesty signals** — `agentMeta.decisions`, `recovery[]`, `quality.verdict`, `confidence`, probe `agentHints`, digest `suggestedReadOrder`, profile-aware MCP `instructions`, heal hints | **HOLE — recorded as cross-cutting lens #7 (T-5).** The pieces exist (CAP-106, CAP-098, CAP-311, CAP-460, CAP-010, CAP-428-family) but they live in five different systems, so no single family can own them without breaking one-family-one-system. This is the product's differentiating surface and it currently has no name anywhere in the model. |
| **Host self-diagnostics / telemetry** — banner, stderr contract, cost display, `version-surface` | **Not a hole — scattered but owned.** CAP-019/028 (`mcp-exposure`, PS-8) and CAP-391/392/920/927 (`operator-cli`, PS-9). |
| **Cost / token economics** | **Not a hole.** `token-budget` (PS-2) owns the mechanism; `client-context` (PS-8) owns the ambient input. |
| **Media, PDF, feeds** | **Not a hole.** CAP-109 media refs and CAP-080/319 feed in `structured-materialization`; CAP-059 transparent PDF in `http-acquisition`. |

---

## 2. The hierarchy at a glance

```
OCCAM (host process + Node workers)
│
├── VALUE SYSTEMS
│   ├── PS-1 ACQUISITION ...................... 9 families / 122 CAPs
│   │   ├── acquisition-routing ............... CAP-052  http_then_browser cascade (real default path)
│   │   ├── http-acquisition .................. CAP-200  HTTP extract backend (readability+turndown)
│   │   ├── browser-acquisition ............... CAP-203  Browser extract backend (Playwright Chromium)
│   │   ├── managed-acquisition ............... CAP-054  Managed third-party escalation (env-gated)
│   │   ├── network-safety .................... CAP-151  DNS-rebinding-safe SSRF guard
│   │   ├── proxy-egress ...................... CAP-157  Static proxy for worker egress
│   │   ├── session-fetch ..................... CAP-167  Local session-profile files
│   │   ├── access-consent .................... CAP-095  Challenge-page detection
│   │   └── quality-failure-semantics [T-1] ... CAP-094  PostProcessor pipeline ordering
│   ├── PS-2 MATERIALIZATION .................. 5 families / 58 CAPs
│   │   ├── token-budget ...................... CAP-061  Two-layer budget split (BudgetOwnership)
│   │   ├── focus-selection ................... CAP-064  focus_query (+ honesty signal)
│   │   ├── structured-materialization ........ CAP-081  translate_to (host-side codec)
│   │   ├── differential-materialization ...... CAP-074  if_none_match (conditional response)
│   │   └── response-cache .................... CAP-085  cache_ttl_s (opt-in on-disk cache)
│   ├── PS-3 DISCOVERY ........................ 3 families / 50 CAPs
│   │   ├── probe-diagnostics ................. CAP-420  Cheap pre-fetch diagnosis, HTTP-only
│   │   ├── site-mapping ...................... CAP-510  HTTP-only mapping, never escalates
│   │   └── web-search ........................ CAP-620  query input + validation (provider-gated)
│   ├── PS-4 KNOWLEDGE EXTRACTION ............. 1 family / 13 CAPs (+4 dead)
│   │   ├── schema-knowledge-extraction ....... CAP-590  Recipe D: resolve → schema-match → CSS-extract
│   │   └── [canonical-knowledge-ir] .......... (dead evidence cluster — no product capability) [T-3]
│   ├── PS-5 PLAYBOOKS ........................ 4 families / 68 CAPs
│   │   ├── playbook-resolution ............... CAP-491  Four-tier resolution, per-field fallback
│   │   ├── playbook-authoring ................ CAP-562  verify=true dry-run quality gate
│   │   ├── playbook-healing .................. CAP-530  url + failure_reason heal attempt
│   │   └── playbook-validation ............... CAP-750  Pure, network-free lint contract
│   ├── PS-6 TRUST AND PROVENANCE ............. 4 families / 85 CAPs
│   │   ├── receipts .......................... CAP-090  Receipt v1 positive signing
│   │   ├── verification ...................... CAP-268  occam_verify mode dispatch
│   │   ├── claims-attestation ................ CAP-690  claim_check as fact-grounding primitive
│   │   └── dataset-provenance ................ CAP-770  dataset_export entry point
│   └── PS-7 MONITORING AND MULTI-SOURCE ...... 5 families / 78 CAPs
│       ├── digest-synthesis [T-2] ............ CAP-450  urls / source_url input contract
│       ├── batch-jobs ........................ CAP-800  Batch execution core
│       ├── change-monitoring ................. CAP-830  occam_watch forced block collection
│       ├── consensus-crosscheck .............. CAP-850  occam_crosscheck surface
│       └── failure-atlas ..................... CAP-870  occam_failure_atlas contract
│
└── ENABLING SYSTEMS
    ├── PS-8 RUNTIME AND EXPOSURE ............. 3 families / 37 CAPs
    │   ├── runtime-transports ................ CAP-003  stdio (default transport)
    │   ├── mcp-exposure ...................... CAP-007  15 always-registered core tool names
    │   └── client-context .................... CAP-400  Idempotent inspect-only read
    └── PS-9 OPERATOR SURFACE ................. 4 families / 159 CAPs
        ├── operator-cli ...................... CAP-920  version-surface deployment diagnostic
        ├── install-onboarding ................ CAP-940  occam-doctor unified preflight
        ├── host-connectors ................... CAP-980  occam connect plan/apply/verify
        └── packaging-distribution ............ CAP-1020 @ff-occam/mcp npm bin launcher
```

`122+58+50+13+68+85+78+37+159 = 670` reachable-classified CAPs `+ 4` dead-cluster CAPs `= 674`.

---

## 3. Value systems

### PS-1 — Acquisition

| Field | Content |
|-------|---------|
| **PURPOSE** | Turn a URL into raw extracted content through a gated ladder of HTTP, browser and optional third-party backends, under explicit network-safety, proxy, session and access policy — and classify the outcome honestly when it fails. |
| **USER VALUE** | The caller gets real current bytes from the real URL, or a **typed refusal**. The ladder means an SPA or a thin HTTP shell is retried in a browser without the caller asking; the short-circuits mean a 404 does not cost a browser launch. |
| **PRIMARY ENTRYPOINTS** | `occam_transcode` (reference path); indirectly every pipeline consumer: `occam_digest`, `occam_claim_check`, `occam_attest`, `occam_dataset_export`, `occam_watch`, `occam_crosscheck`, `occam_batch_*`, `occam_verify mode=live`. Not `occam_probe`/`occam_map`/`occam_search`/`occam_extract_knowledge`/`occam_playbook_heal` — those have their own fetchers. |
| **CORE SUBSYSTEMS** | `OccamRouter` (escalation) · `TranscodePipeline` preflight half (playbook overlay, focus intent, `FetchPreflight`, robots/throttle) · `HttpExtractBackend` / `BrowserExtractBackend` / `ManagedExtractBackend` · post-processor pipeline (challenge → requires_login → thin) · `BrowserPoolManager` + daemons · `FetchHeadersScope` / `SessionProfileHeaders` · `OutboundHttpGuard` + worker DNS pinning · `ProxyRotation` · `DomainTierRegistry` · `FailureRanking`. |
| **CAPABILITY FAMILIES** | `acquisition-routing`, `http-acquisition`, `browser-acquisition`, `managed-acquisition`, `network-safety`, `proxy-egress`, `session-fetch`, `access-consent`, **`quality-failure-semantics` (T-1)**. |
| **ARTIFACTS** | Produces no artifact family of its own (`ARTIFACT-ONTOLOGY.md` §10) — it feeds ART-001/002. Consumes ART-026 (session profile), ART-NEW-P504-1 (fetch-headers temp JSON), ART-NEW-P504-5 (storageState). |
| **CONFIGURATION** | `backend_policy` (default `http_then_browser`), `session_profile`, `prefer_llms_txt`; `OCCAM_ALLOW_PRIVATE_URLS`, `OCCAM_RESPECT_ROBOTS`, `OCCAM_HOST_THROTTLE_MS`, `OCCAM_HTTP(S)_PROXY`/`NO_PROXY`, `OCCAM_PROXY_LIST(_FILE)`, `OCCAM_MANAGED_PROVIDER`/`_API_KEY`/`_BASE_URL`/`_DOMAINS`/`_TIMEOUT_MS`, `OCCAM_MAX_RESPONSE_BYTES` (8 MiB), `OCCAM_BROWSER_TIMEOUT_MS` (**60 s default**, not 120), `OCCAM_BROWSER_AUTOINSTALL`, `OCCAM_SESSIONS_ROOT`, `OCCAM_REQUEST_HEADERS_FILE`, `OCCAM_DOMAIN_TIERS_PATH`. |
| **TRUST CHARACTERISTICS** | This is where the trust model is weakest and nothing here is proven. TLS terminates inside the worker; no transcript, cert pin or origin signature is ever retained. Browser contexts run `bypassCSP:true` **unconditionally** (EF-046) and playbook plans can reach `page.evaluate`. A configured managed provider is a third party that sees the URL and returns content Occam will later sign (EF-003). Cloaking, personalization and prompt-injected text are hashed and signed like genuine content. |
| **KNOWN LIMITATIONS** | No CAPTCHA solving, ever. No fingerprint/identity rotation. No automatic retry/backoff (CAP-188 absent). Managed never becomes the surfaced result on failure and is not a `backend_policy` value (EF-056). 404/410 and public-reference URLs terminate the ladder silently. Proxy rotation does not reach the daemon, pool, css-extract or dom-skeleton spawns (CAP-165); Core's own C# `HttpClient`s ignore `OCCAM_*` proxy entirely (CAP-166). css-extract lacks DNS-pin and body cap (EF-043). Robots is off by default and fails open (GAP-018). |
| **ENGINEERING FINDINGS** | EF-002, EF-003, EF-007, EF-017, EF-039, EF-040, EF-041, EF-042, EF-043, EF-046, EF-054, EF-056, EF-057; GAP-001/002/003/004/014/018/030. |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Routing/`, `Backends/`, `PostProcessors/`, `Session/`, `Workers/`, `Net/`; `workers/http-extract/`, `workers/browser-extract/`, `workers/shared/lib/`. |

### PS-2 — Materialization

| Field | Content |
|-------|---------|
| **PURPOSE** | Turn extracted content into a **bounded** response that fits a model's context: budget it, focus it, add opt-in structured sidecars, express it as a delta when asked, and optionally replay it from disk. |
| **USER VALUE** | The reason to use Occam rather than an HTTP client: a 300 KB page arrives as a few thousand tokens of relevant markdown, with a machine-readable record of what was dropped (`OmittedManifest`, CAP-067/310) instead of a silent truncation. |
| **PRIMARY ENTRYPOINTS** | `occam_transcode` (all `[tokens]`/`[structured]` params); `occam_digest` (`per_url_max_tokens`); every pipeline consumer inherits the compile stage. |
| **CORE SUBSYSTEMS** | `TranscodePipeline.FinishMaterialize` · `Compile/` (`BudgetOwnership`, `ResponseBudgetPlanner`, `TokenBudget`, `FitMarkdown`, `SectionIndex`/`SectionRanker`, `AnswerUnitSelector`, `OmittedManifest`) · `Knowledge/MaterializationPlanner` · `Codecs/` (only `MarkdownPassthroughCodec` ever runs live — CAP-329) · `BlockReconciler` · `TranslationService` · `TranscodeResponseCache` + `TranscodeCacheKey` · `ContentHashToken` / `MaterializationKey`. |
| **CAPABILITY FAMILIES** | `token-budget`, `focus-selection`, `structured-materialization`, `differential-materialization`, `response-cache`. (`quality-failure-semantics` left for PS-1 — T-1.) |
| **ARTIFACTS** | ART-001 markdown · ART-002 blocks · ART-003 tables · ART-004 feed · ART-005 chunks · ART-024 contentHash/MaterializationKey · ART-035 response-cache entry · ART-039 translatedMarkdown. |
| **CONFIGURATION** | `max_tokens`, `fit_markdown`, `focus_query`, `content_selectors`, `json_blocks`, `json_tables`, `json_feed`, `semantic_chunking`, `rank_blocks`, `tag_trust`, `translate_to`, `if_none_match`, `diff_against`, `delta_only`, `cache_ttl_s`; `OCCAM_CLIENT_CONTEXT_TOKENS`, `OCCAM_TRANSLATE_URL`, `OCCAM_CACHE_DIR`, `OCCAM_CHUNK_SIZE` (chars, not tokens — CAP-337). |
| **TRUST CHARACTERISTICS** | `contentHash` covers the **compiled** markdown, not the page: two receipts for one page under different budgets legitimately differ. `translatedMarkdown` never enters the signed bytes (ART-039). `tag_trust` is an off-by-default heuristic carried outside the signature. Cache replay reissues the original signed envelope with its original `ts`; `cached:true` sits outside the signature. |
| **KNOWN LIMITATIONS** | `semantic_chunking` is a fixed-size line accumulator, not semantic (CAP-320). `content_selectors` are heading anchors, not CSS (CAP-307). The codec registry has no live selection surface (CAP-327). `ResponseBudgetDiagnostics` is computed and never surfaced (CAP-303). Cache TTL is evaluated **only on read** — no sweep (CAP-322); the key omits `rank_blocks`/`tag_trust`/`emit_capsule` (CAP-315/EF-001) and the URL fragment (EF-045). Tokenizer is `heuristic-unicode-v1` with unmeasured error bounds. |
| **ENGINEERING FINDINGS** | EF-001, EF-004, EF-010, EF-045, EF-055. |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Compile/`, `Codecs/`, `Knowledge/`, `Caching/`, `Routing/TranscodePipeline.cs` (materialize half). |

### PS-3 — Discovery

| Field | Content |
|-------|---------|
| **PURPOSE** | Answer *which URL* and *is it worth fetching* without paying for extraction: diagnose a URL cheaply, enumerate a site's links, and search a configured provider. |
| **USER VALUE** | Spend one cheap HTTP request instead of a browser launch to learn that a page is a login wall, a redirect, an SPA, or unextractable — and get a link set or search hits to feed the acquisition path. |
| **PRIMARY ENTRYPOINTS** | `occam_probe`, `occam_map`, `occam_search`. Also reached internally by `occam_digest` (`source_url` → `MapService`) and by search reranking (probe fan-out). |
| **CORE SUBSYSTEMS** | `ProbeService` + `HttpProbeFetcher` + `VectorizedHtmlScanner` · `MapService` (sitemap/robots/homepage link harvest, ranking, clamps) · `SearchService` + provider adapters + `SearchExtractabilityScorer` · `AccessClassifier` (shared with PS-1) · `DomainTierRegistry` hints. |
| **CAPABILITY FAMILIES** | `probe-diagnostics`, `site-mapping`, `web-search`. (`digest-synthesis` left for PS-7 — T-2.) |
| **ARTIFACTS** | ART-011 map link list · ART-012 probe diagnosis · ART-013 search hits. All unsigned, unhashed, non-verifiable by design. |
| **CONFIGURATION** | `occam_map`: `source`, `max_links`, timeouts. `occam_search`: `OCCAM_SEARCH_PROVIDER` (**off by default — the tool fails closed without it**), `rerank` (live probe fan-out). Domain tiers via `OCCAM_DOMAIN_TIERS_PATH`. |
| **TRUST CHARACTERISTICS** | Nothing in PS-3 is signed, hashed or verifiable. `extractability` is a **prediction before fetch** and must never be presented on the same scale as `confidence`/`quality` (measurement after fetch) or a playbook `verify.score` (0–100 gate). |
| **KNOWN LIMITATIONS** | Probe is structurally HTTP-only and never escalates (CAP-420); a browser-only page can be mis-predicted. Map never escalates either (CAP-510) — a JS-rendered nav yields nothing. Probe masks SSRF blocks as `network_error` (GAP-003, `HttpProbeFetcher.cs:164-175`). `probe.autoRedirect` HttpClient is registered but never selected (CAP-436, dead). Search results reflect the third-party provider's index, and rerank orders by *extractability*, not relevance. |
| **ENGINEERING FINDINGS** | No canonical EF is attached to PS-3 families. Behavioral gap GAP-003 is recorded in `ACQUISITION-ROUTING-MODEL.md`. |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Services/ProbeService.cs`, `MapService.cs`, `SearchService.cs`, `Probe/`, `Search/`, `Tools/OccamProbeTool.cs`, `OccamMapTool.cs`, `OccamSearchTool.cs`. |

### PS-4 — Knowledge extraction

| Field | Content |
|-------|---------|
| **PURPOSE** | Map a page into caller-supplied typed fields using a resolved playbook schema and a dedicated CSS/regex worker. |
| **USER VALUE** | Structured `facts[]` for a known site shape instead of markdown the agent must re-parse — when a schema exists. |
| **PRIMARY ENTRYPOINTS** | `occam_extract_knowledge` (schema is **required**; there is no schema-free mode). |
| **CORE SUBSYSTEMS** | `KnowledgeExtractService` · `CssExtractWorker` (+ temp field-spec file) · `workers/css-extract/css-extract.mjs` · `PlaybookSeedResolver` for the schema (PS-5 dependency). |
| **CAPABILITY FAMILIES** | `schema-knowledge-extraction`. Dead cluster: `canonical-knowledge-ir` (T-3). |
| **ARTIFACTS** | ART-014 `facts[]` (+ the misleadingly named `Receipt`) · ART-036 temp CSS field-spec JSON. |
| **CONFIGURATION** | Tool params (`url`, schema/`knowledge_schema`, playbook inputs); `session_profile` is accepted but **silently does not reach the browser-fallback leg** (CAP-594). Worker timeout is hardcoded 45 s (CAP-592). |
| **TRUST CHARACTERISTICS** | **The `Receipt` field on this tool is not a Receipt v1** — it is `{confidence, elapsedMs}` telemetry (CAP-287/596, EF-006). Nothing this tool returns is signed, hashed or verifiable. `confidence` is always `0.0` (CAP-595, dead field). |
| **KNOWN LIMITATIONS** | Bypasses the entire acquisition/materialization spine (CAP-591), so no token budget, no post-processors, no receipts, no pool reuse (per-call throwaway Playwright for the browser leg, CAP-593). Row-mode `base_selector` is unreachable — host parsers never set it (CAP-600, EF-014, C4). `readNuxtPath` runs `eval()` over page-controlled Nuxt state (CAP-598, EF-013). Narrower failure taxonomy than transcode (CAP-601). |
| **ENGINEERING FINDINGS** | EF-006, EF-013, EF-014, EF-055. |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Knowledge/`, `Tools/OccamExtractKnowledgeTool.cs`, `Workers/CssExtractWorker.cs`; `workers/css-extract/`. Dead cluster lives in `Knowledge/Canonical*`. |

### PS-5 — Playbooks

| Field | Content |
|-------|---------|
| **PURPOSE** | Resolve, author, repair, validate and sign per-site extraction recipes, and merge them from local, org, community, seed and live-genome tiers. |
| **USER VALUE** | A site that extracts badly can be made to extract well, once, and stay fixed — and the fix is a portable JSON file rather than a code change. |
| **PRIMARY ENTRYPOINTS** | `occam_playbook_resolve`, `occam_playbook_heal`, `occam_playbook_save`, `occam_playbook_lint`; in-band via `playbook_policy=auto` on `occam_transcode`; forced `auto` inside `claim_check`/`attest`/`dataset_export` (CAP-693, no parameter exists). |
| **CORE SUBSYSTEMS** | `PlaybookSeedResolver` (four-tier, per-field precedence) · `PlaybookGenomeMerger` + `WellKnownGenomeFetcher` · `PlaybookHealService` + `DomSkeletonWorker` · `PlaybookSaveService` + `PlaybookSignature` + `PlaybookSaveVerifier` · `PlaybookLinter` · `PlaybookVerifyScope` overlay in `TranscodePipeline`. |
| **CAPABILITY FAMILIES** | `playbook-resolution`, `playbook-authoring`, `playbook-healing`, `playbook-validation`. |
| **ARTIFACTS** | ART-015 signed local playbook · ART-016 heal skeleton/candidates · ART-017 resolve overlay/genome · ART-018 lint grade; plus ART-NEW-P504-2 community sha256 manifest, ART-NEW-P504-6 well-known genome body, ART-NEW-P504-7 publish package. |
| **CONFIGURATION** | `playbook_policy` (`off`/`auto`), `verify`, `genome`/`genomeFetch`/`fetch_site_genome`, `include_lessons`, `page_class`, `knowledge_schema`, `schema_version`; `OCCAM_PLAYBOOKS_LOCAL_ROOT`, `WT_PLAYBOOKS_PATH`, `OCCAM_SITE_GENOME_FETCH`. |
| **TRUST CHARACTERISTICS** | `occam_playbook_save` **always signs**, ignoring `OCCAM_RECEIPTS` (EF-005) — one of the two reasons that variable must never be called a master switch. The signature covers the recipe body **only**: `keyId`, `alg`, `signedAt` and the whole `verify{score,passesGate,noiseLeakage}` block sit inside the excluded `provenance` key and are freely editable without invalidating `Verify` (TRUST-MODEL X1). Community playbooks are sha256-integrity-checked, not authenticated (G-E-03). |
| **KNOWN LIMITATIONS** | `PlaybookCommunitySanitizer` is Core-dead — lint and local save never publish-sanitize (EF-047, C3). Lint is advisory: save and resolve do not require a passing grade. Heal's `--consent-aggressive` worker flag is unreachable from MCP (CAP-553). `page_class`/`knowledge_schema` match failures are swallowed on the resolve path (CAP-496). Live genome fetch has an empty-Content-Type bypass and reads before truncating (EF-048). Marketplace CI can auto-merge community playbooks (EF-052). |
| **ENGINEERING FINDINGS** | EF-005, EF-015, EF-044, EF-047, EF-048, EF-052; EFC-P5-05-1 (tamper reported as foreign author). |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Playbooks/`, `Tools/OccamPlaybook*Tool.cs`, `Workers/DomSkeletonWorker.cs`; `profiles/playbooks/{seeds,community}/`; `workers/browser-extract/dom-skeleton-capture.mjs`. |

### PS-6 — Trust and provenance

| Field | Content |
|-------|---------|
| **PURPOSE** | Commit to what was extracted, so it can later be checked for tampering: sign receipts, build Merkle commitments over blocks, package capsules, verify offline, retrieve evidence for claims, and bind dataset row sets. |
| **USER VALUE** | An extraction can be handed to someone else with a receipt they can check without trusting the sender's prose — and a quoted sentence can be proven to have been in the extraction it claims to come from. |
| **PRIMARY ENTRYPOINTS** | `occam_verify`, `occam_claim_check`, `occam_attest`, `occam_dataset_export`; receipt production rides on `occam_transcode`/`occam_digest`/`watch`/`crosscheck`; offline CLI `occam verify` and `keys export`. |
| **CORE SUBSYSTEMS** | `ReceiptSigner` (ECDSA P-256) · `ReceiptCanonicalizer` · `ReceiptVerifier` · `MerkleTree` · `CapsuleCodec` · `TimeAnchorService` + `ReceiptTimeAnchor` · `ClaimCheckService` + `ClaimBlockRanker` (BM25) · `AttestService` · `DatasetExportService` + `DatasetManifestBuilder` · `WatchHistoryChain` (verification half) · `ReceiptsPolicy`. |
| **CAPABILITY FAMILIES** | `receipts`, `verification`, `claims-attestation`, `dataset-provenance`. |
| **ARTIFACTS** | ART-006 capsule · ART-007 positive receipt · ART-008 negative receipt · ART-009 time anchor · ART-019 claim matches + proofs · ART-020 attest batch · ART-021 verify verdict · ART-022 dataset rows + manifest · ART-034 signing key. |
| **CONFIGURATION** | `OCCAM_RECEIPTS` (**not a master switch** — C6), `OCCAM_KEYS_ROOT`, `OCCAM_TIME_ANCHOR` + `OCCAM_TSA_URL`; tool params `emit_capsule`, `public_key`, `mode`, `chunks`. |
| **TRUST CHARACTERISTICS** | Exactly one thing is proven: *the holder of this key asserted these bytes and they are unaltered*. Not who the holder is (no PKI, no registry, no rotation, no revocation — TOFU over an out-of-band PEM), not that the origin served it, not that a fetch happened, not when (the `ts` is the signer's clock; the optional TSA certificate is never chained to a root). Merkle proves membership in the signer's block set, never truth. The capsule wrapper is unsigned; only the nested envelope is signed. `claim_check` performs **no stance evaluation** (`Verdict` hardcoded `not_evaluated`). `occam_attest`'s aggregate is **unsigned** and its classifier recognises two English claim shapes. |
| **KNOWN LIMITATIONS** | Unknown verify modes silently downgrade to `offline` (EF-011). `live` re-fetch drops session/playbook/budget context, so `drifted` often means "my re-fetch lacked context" (EF-012); all re-fetch failures collapse to `refetch_failed`. Manifest verification is **CLI-only** (EF-018), and the CLI is unreachable through the `occam` wrapper (EF-025). MCP `public_key` defaults to the running host's own key, so a foreign receipt "fails" indistinguishably from tampering (EFC-P5-05-5). A fully unsigned watch chain returns `history_verified` (EFC-P5-05-2). Negative receipts prove a wall was hit from this egress, nothing more; the `"paywall"` branch is dead (EF-008). |
| **ENGINEERING FINDINGS** | EF-005, EF-008, EF-011, EF-012, EF-016, EF-018, EF-044, EF-053; EFC-P5-05-1…5. EF-024 is **WITHDRAWN** and must not be revived. |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Receipts/`, `Claims/`, `Attest/`, `Dataset/`, `Tools/OccamVerifyTool.cs`, `OccamClaimCheckTool.cs`, `OccamAttestTool.cs`, `OccamDatasetExportTool.cs`, `Cli/OccamCliVerbs.cs` (verify/keys). |

### PS-7 — Monitoring and multi-source

**Scope restated (T-2):** composition **across multiple fetches** — across sources (digest), across vantages (crosscheck), across time (watch), across jobs (batch), and across failures (atlas). Not "the opt-in bucket": `occam_digest` is a core always-on tool and now lives here.

| Field | Content |
|-------|---------|
| **PURPOSE** | Run and combine more than one acquisition, then report the combination: a bounded multi-URL digest, an async job queue, a change history, a multi-vantage comparison, a session failure aggregate. |
| **USER VALUE** | One call instead of N: read five pages under one token budget; learn what changed since last time; see whether a page looks different to a browser than to an HTTP client; see which hosts are walling this session. |
| **PRIMARY ENTRYPOINTS** | `occam_digest` (core). Opt-in: `occam_batch_submit/status/results` (`OCCAM_BATCH_MCP`), `occam_watch` (`OCCAM_WATCH_MCP`), `occam_crosscheck` (`OCCAM_CONSENSUS_MCP`), `occam_failure_atlas` (`OCCAM_ATLAS_MCP`). Also `--batch-server` HTTP mode. |
| **CORE SUBSYSTEMS** | `DigestService` (fan-out + combine + read-order hints) · `BatchServerHost` + `BatchJobProcessor` + `JsonFileBatchJobStore` · `WatchService` + `WatchStore` + `WatchHistoryChain` · `ConsensusService` + `ConsensusEvaluator` · `FailureAtlasStore` + `FailureAtlasClassifier`. |
| **CAPABILITY FAMILIES** | **`digest-synthesis` (T-2)**, `batch-jobs`, `change-monitoring`, `consensus-crosscheck`, `failure-atlas`. |
| **ARTIFACTS** | ART-010 digest combined + items · ART-025 watch history chain · ART-027 batch job snapshot · ART-028 watch store · ART-NEW-P504-3 consensus response · ART-NEW-P504-4 atlas in-memory store. |
| **CONFIGURATION** | `urls`/`source_url`, `max_urls`, `max_links`, `per_url_max_tokens`, `combine`; `OCCAM_BATCH_MCP`, `OCCAM_BATCH_DB_PATH`, `OCCAM_WATCH_MCP`, `OCCAM_WATCH_DB_PATH`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP`; crosscheck `vantages`. |
| **TRUST CHARACTERISTICS** | Digest items carry **reduced** Receipt v1 (content-hash only, no block leaves, no time anchor — CAP-457). Batch results carry **no Receipt v1 at all** (EF-037). The consensus verdict is **unsigned** and no shipped tool re-derives it from the per-vantage receipts (CAP-856, EF-032); all vantages leave one process, one egress IP, one proxy config (CAP-859), so agreement excludes one cloaking axis and proves nothing about accuracy. Watch entries are signed only when receipts are on, yet an entirely unsigned chain still verifies (EFC-P5-05-2). Atlas is unsigned aggregate telemetry. |
| **KNOWN LIMITATIONS** | Batch retains full markdown forever with no delete API (EF-037) and is last-writer-wins across processes (EF-038). Watch has **no un-watch**: `IWatchStore.Remove` has no product caller (CAP-840, EF-020); history is capped at 64/URL but the URL set is uncapped; a corrupt store silently resets to empty (CAP-832). Crosscheck is exempt from `OCCAM_PROFILE` and absent from server instructions (EF-031, CAP-861). Atlas is per-session memory only and enabling it **replaces** the host telemetry sink (CAP-875). Digest applies whole-batch SSRF/session preflight, not per item (CAP-452), and truncates silently at the clamps (CAP-453). |
| **ENGINEERING FINDINGS** | EF-019, EF-020, EF-031, EF-032, EF-037, EF-038. EF-024 WITHDRAWN. |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Services/DigestService.cs`, `Batch/`, `Watch/`, `Consensus/`, `Telemetry/FailureAtlas*`, `Tools/OccamDigestTool.cs`, `OccamWatchTool.cs`, `OccamCrosscheckTool.cs`, `OccamFailureAtlasTool.cs`, `OccamBatch*Tool.cs`. |

---

## 4. Enabling systems

### PS-8 — Runtime and exposure

| Field | Content |
|-------|---------|
| **PURPOSE** | Decide how a caller reaches the host and how much of the product that caller can see: transport mode, tool registry, profile filtering, env gates, server instructions, and the ambient client budget. |
| **USER VALUE** | The product appears inside an agent host as a tool list sized to the deployment; declaring a context window once makes every later read fit that window automatically. |
| **PRIMARY ENTRYPOINTS** | Default stdio host; `--mcp-server` (WS), `--remote` (WSS+JWT), `--batch-server`; `occam_client_capabilities`; `OCCAM_PROFILE`; the four opt-in flags. |
| **CORE SUBSYSTEMS** | `Program.cs` · `StdioMcpTransport` / `WebSocketMcpTransport` / remote twin · `OccamMcpServerRegistration` (`OccamToolNames`) · `OccamToolProfile` · `OccamServerInstructions` · `ClientCapabilityStore` · `OccamServiceCollectionExtensions` (DI) · banner + stderr contract. |
| **CAPABILITY FAMILIES** | `runtime-transports`, `mcp-exposure`, `client-context`. |
| **ARTIFACTS** | ART-023 client ambient budget. |
| **CONFIGURATION** | `OCCAM_PROFILE` (`full`/`reader`/`researcher`/`auditor`; invalid → `full` + stderr warning), `OCCAM_BATCH_MCP`, `OCCAM_WATCH_MCP`, `OCCAM_CONSENSUS_MCP`, `OCCAM_ATLAS_MCP`, `OCCAM_CLIENT_CONTEXT_TOKENS`, `OCCAM_CLIENT_MODEL_ID`, `OCCAM_REMOTE_MAX_SESSIONS`, `OCCAM_BANNER`, `OCCAM_LOG`. |
| **TRUST CHARACTERISTICS** | Profiles change *exposure*, never handler semantics — a `reader` deployment still mints a key, still signs, still applies playbook overlays, still may use a managed provider. `reader` exposes the receipt producer and hides the verifier (EFC-P5-05-4). Opt-in tools are **not** profile-filtered (CAP-011), so `reader` + `OCCAM_CONSENSUS_MCP=1` is legal. |
| **KNOWN LIMITATIONS** | The canonical launcher is stdio-only and never forwards args (CAP-1001) — WS/Remote/BatchServer are unreachable through it. Local WS has no session semaphore; each socket builds a DI container and reinstalls the shared browser pool, killing the previous one (EF-041). The banner can claim stdio on WS/Remote (GAP-032). Server instructions can mention `occam_watch` without its gate (GAP-012). BatchServer has no auth (loopback only). `model_id` and `suggestedProfile` are inert (CAP-402/403). |
| **ENGINEERING FINDINGS** | EF-033, EF-041; GAP-011 (Content-Length adapter is the WS path, not stdio framing — C5), GAP-012, GAP-032. |
| **CODE OWNERSHIP** | `src/FFOccamMcp.Core/Program.cs`, `Transport/`, `Client/`, `Composition/`, `Configuration/`. |

### PS-9 — Operator surface

| Field | Content |
|-------|---------|
| **PURPOSE** | Get Occam onto a machine, wired into a host, kept healthy, credentialed, refreshed and distributed. |
| **USER VALUE** | One command installs prerequisites and Chromium, detects up to fifteen agent hosts and writes their MCP config with a backup, verifies the install, and prints a working snippet. Session verbs turn a human login into cookies the fetch path can use. |
| **PRIMARY ENTRYPOINTS** | `occam <sub>` wrapper (13 subcommands incl. `doctor`, `connect`, `session`, `refresh`, `onboard`, `smoke`, `update`, `skill`, `status`, `contract`); host offline verbs (`keys export`, `verify`, `install-browser`, `version-surface`, `lifecycle`); `get-ff-occam`, `install.ps1/.sh`, `verify-install`, `launch-mcp-host.mjs`; npm bin, skill install, Docker ENTRYPOINT. |
| **CORE SUBSYSTEMS** | `scripts/lib/operator/**` (control loop, subcommands, snippet, update check) · `occam-doctor.ps1/.sh` · `occam-connect.mjs` + 15 host adapters + `config-engine` · `occam-sessions-lib.mjs` · `onboard-schema/onboard-config` · `build-release` + release workflows · `install-occam-skill.mjs` · `stop-occam-processes.mjs`. |
| **CAPABILITY FAMILIES** | `operator-cli`, `install-onboarding`, `host-connectors`, `packaging-distribution`. |
| **ARTIFACTS** | ART-026 session profiles · ART-029 onboard state · ART-030 connect last-run · ART-031 host MCP config + `.occam-bak` · ART-032 Level B tarball + sha256 manifest · ART-033 skill card · ART-037 retained raw cookies · ART-038 cosign bundle. Also operates on ART-034 (key export). |
| **CONFIGURATION** | `OCCAM_HOME`, `OCCAM_CONFIG`, `OCCAM_SESSIONS_ROOT`, `OCCAM_KEYS_ROOT`, `OCCAM_RELEASE_BASE_URL`, `OCCAM_REPO_URL`, `PLAYWRIGHT_BROWSERS_PATH`/`OCCAM_PLAYWRIGHT_BROWSERS_PATH`, `OCCAM_FORCE_DOTNET_RUN`. |
| **TRUST CHARACTERISTICS** | This is the supply-chain surface and it is the weakest part of the trust story. Level B install verifies a **sha256 manifest**, not a signature; the cosign bundle is consumed by no shipped install path (EF-053, "trust theater"). `occam keys export` against an empty key store **mints a new key and exports it** — a consumer can pin a key that never signed anything. Session import retains raw cookies in plaintext by default (EF-054). Connect mutates third-party config files and its rollback is dead for some `requiresRestart` adapters (EF-021). |
| **KNOWN LIMITATIONS** | `occam refresh` kills every `OccamMcp.Core[.exe]` on the machine with no scope flag (EF-049). `launch-mcp-host` injects `~/.occam/onboard.json` env into every launch (EF-050). The wrapper does not route to the host's `verify`/`keys`/`install-browser` verbs (EF-025). `version-surface` and `contract` collide by name (EF-023); refresh output still says "9 tools" (EF-022). Skill install `rmSync`s the destination and ships a stale version/tool count (EF-036). npm package is unpublished and would be DOA as packaged (EF-034); the tarball may omit `connect` (EF-035); Docker HEALTHCHECK invokes an unsupported `--version` and can hang in stdio (EF-051); marketplace CI can auto-merge unvalidated playbooks (EF-052). Uninstalling the install tree leaves `~/.occam`, host configs, skills and the Playwright cache behind. |
| **ENGINEERING FINDINGS** | EF-021, EF-022, EF-023, EF-025, EF-028, EF-029, EF-030, EF-034, EF-035, EF-036, EF-049, EF-050, EF-051, EF-052, EF-053, EF-054. |
| **CODE OWNERSHIP** | `scripts/**` (operator, install, connect, session, release), `packages/occam-mcp`, `packages/occam-skill`, `packages/occam-agent-sdk`, `Dockerfile`, `.github/workflows/**`, `src/FFOccamMcp.Core/Cli/OccamCliVerbs.cs`. |

---

## 5. System interaction map

```
                 PS-3 DISCOVERY                 PS-5 PLAYBOOKS
              probe · map · search        resolve · heal · save · lint
                     │                              │
                     │ which URL?                   │ overlay (in-band, soft)
                     ▼                              ▼
   caller ─────► PS-1 ACQUISITION ────────► PS-2 MATERIALIZATION ────► response
                 router · backends           budget · focus · sidecars
                 post-processors                     │
                     │                               │ contentHash / block leaves
                     │ raw HTML/DOM                  ▼
                     │                        PS-6 TRUST
                     ▼                        sign · Merkle · verify · claims
                 PS-4 KNOWLEDGE  (separate spine: css-extract, no router,
                 typed facts      no budget, no receipts — needs PS-5 schema)

   PS-7 MULTI-FETCH  digest · batch · watch · crosscheck · atlas
        └─ composes PS-1+PS-2 N times, adds its own store/verdict

   PS-8 RUNTIME & EXPOSURE  decides which of the above is callable at all
   PS-9 OPERATOR SURFACE    puts the host on the machine and keeps it running
```

Hard dependencies worth stating once: **PS-4 requires PS-5** (a schema must resolve before extraction). **PS-6 depends on PS-1+PS-2** for anything it signs (`claim_check`, `attest`, `dataset_export`, `verify mode=live` all enter the pipeline). **PS-7 depends on PS-1+PS-2** for every item it composes. **PS-3 depends on nothing** — it is the only value system that can run with the workers absent for extraction.

---

## 6. Reading order for someone learning the product

Ordered so that each step only assumes the previous ones. Wave-4 corrections are load-bearing at steps 3 and 8 — read them there, not as an appendix.

| # | Read | Why here |
|---|------|----------|
| 1 | **The trust rule**: `ok:false` means the content is UNKNOWN; `thin_extract` means bad extraction, not a short page; a short good page is `ok:true` + `quality.verdict=short_quality` | Every other behavior is downstream of this contract. An agent that ignores it turns the whole product into decoration (TRUST-MODEL §9.5). |
| 2 | **PS-1 the reference path** — `occam_transcode(url)` with defaults: preflight → router → post-processors → materialize | The one narrative that explains 80 % of usage (`PRODUCT-ARCHITECTURE.md` §2). |
| 3 | **PS-1 the ladder, correctly** — `http_then_browser`, the 404/410 and public-reference short-circuits, `FailureRanking` for dual failure, managed as an env-gated rung that never wins a failure surface | The single most-miswritten area in the corpus (C1 / EF-056 / GAP-001). Learn it right the first time. |
| 4 | **PS-2 token economics** — `max_tokens`, ambient budget, `fit_markdown`, `focus_query`, `OmittedManifest` | This is why a caller uses Occam instead of `fetch`. |
| 5 | **PS-3 cheap-first** — probe before transcode; map/search to find URLs; `extractability` is a *prediction*, not a measurement | Teaches spending discipline before teaching more tools. |
| 6 | **PS-7 digest** — several URLs in one bounded call rather than N transcodes | The natural next step after "read one page". |
| 7 | **Failure taxonomy and agent guidance** — typed failure codes, `agentMeta.decisions`, `recovery[]`, heal hints | Lens #7; the caller now has enough context for the codes to mean something. |
| 8 | **PS-6 what a receipt does and does not prove** — signature = "this key asserted these bytes"; Merkle = membership, not truth; no PKI; `OCCAM_RECEIPTS` is not a master switch | Must be read **before** anything that sounds like verification, so the names are never over-read (C6, TRUST-MODEL §13). |
| 9 | **PS-5 playbooks** — resolve → heal → lint → save, and the in-band `playbook_policy=auto` overlay | Only useful once the reader has seen a page extract badly. |
| 10 | **PS-4 typed extraction** — schema required, separate spine, `Receipt` field is telemetry (EF-006) | Depends on PS-5. |
| 11 | **PS-7 stateful/multi-vantage** — watch, crosscheck, batch, atlas, and their exposure gates | Opt-in surfaces last. |
| 12 | **PS-8 exposure** — profiles, transports, opt-in flags, `occam_client_capabilities` | Deployment-shaping, once the capabilities are known. |
| 13 | **PS-9 operate it** — doctor, connect, sessions, refresh, packaging, and the uninstall footprint | Operator concerns; needed to run any of the above, but not to understand it. |
| 14 | **Cross-cutting properties** (§7) — state, automation, config, platform, failure, security, response contract | The lenses only make sense once the systems have names. |

---

## 7. Cross-cutting properties — deliberately NOT systems

### 7.1 The six inherited lenses

Each is a *property of many systems*, so promoting it would break one-family-one-system and duplicate evidence.

| Lens | Why it is not a system | Where it is modelled | Heaviest systems |
|------|------------------------|----------------------|------------------|
| **Configuration** | 110 CAPs classified `CONFIG_BEHAVIOR` spread across every family; env vars are inputs to behavior, not behavior | `ENVIRONMENT-VARIABLES.md`, `CONFIG-NEGATIVE-SPACE.md` | PS-1, PS-8, PS-9 |
| **Platform differences** | Same semantics, different mechanism (Job Object vs process group, cache paths, SIMD tier, path separators) | `PLATFORM-DIFFERENCES.md` | PS-1, PS-9 |
| **Automatic behavior** | 29 silent decisions attach as class tags (A-ROUT/A-PROV/A-SHAPE/A-TRUST/A-HYGIENE/A-NET/A-HOST) to behaviors that already belong to a system | `AUTOMATION-MODEL.md` | PS-1, PS-5, PS-6, PS-9 |
| **Failure semantics** | Every system fails; the *lens* is universal even though the *implementation* (post-processor pipeline + `FailureCodeStrings` + `FailureRanking`) is now a PS-1 family (T-1) | `FAILURE-BEHAVIOR-MAP.md` | PS-1 (impl), all (surface) |
| **State and persistence** | 29 state items across seven systems; "no file cache by design" means live-extract-by-default, **not** stateless | `STATE-MODEL.md` | PS-6 (key), PS-5, PS-7, PS-9, PS-2 |
| **Security and privacy** | SSRF guards, private-URL policy, credential-bearing files, third-party egress, code-execution surfaces — each owned by its host system | `TRUST-MODEL.md` §10, `STATE-MODEL.md` §7 | PS-1, PS-6, PS-9 |

### 7.2 Lens #7 — agent-facing response contract and honesty signals (new, T-5)

The product's differentiator has no owning family because its parts are distributed across five systems. Naming it as a lens keeps it visible without faking a home for it.

| Element | Lives in | Evidence |
|---------|----------|----------|
| `ok:false` = content UNKNOWN; typed `failure.code` | PS-1 `quality-failure-semantics` | CAP-105 |
| `agentMeta.decisions` — what the host decided and why | PS-1 | CAP-106 |
| `recovery[]` — the router attempt log | PS-1 | CAP-098 |
| `quality.verdict` (`short_quality` vs `thin_extract`), `confidence` | PS-1 / PS-2 | CAP-097, CAP-311 |
| `focus`/`completeness` assessment + focus-honesty warnings | PS-2 `focus-selection` | CAP-311 |
| `compile.omitted` — machine-readable "what was cut" | PS-2 `token-budget` | CAP-067/310 |
| `agentHints` on probe | PS-3 `probe-diagnostics` | CAP-428-family |
| `suggestedReadOrder` + digest honesty warnings | PS-7 `digest-synthesis` | CAP-460 |
| Profile-aware MCP `instructions` on initialize | PS-8 `mcp-exposure` | CAP-010 |
| Heal-hint policy on typed failures | PS-5 `playbook-healing` | `PlaybookHealPolicy` |

**Known coverage gap in the lens itself:** `occam_crosscheck` has no server-instruction or agentHints coverage (CAP-861), so a capability that exists is undiscoverable in-band.

---

## 8. Corrections to prior model

1. **39 families → 38.** `canonical-knowledge-ir` is a shipped-dead evidence cluster, not a product area (T-3). After this, every family has exactly one product capability.
2. **`quality-failure-semantics` is PS-1, not PS-2** (T-1). Post-processors gate the acquisition outcome and run before materialization; a failed outcome never materializes.
3. **`digest-synthesis` is PS-7, not PS-3** (T-2). Digest composes the acquisition spine N times; it delegates discovery, it does not perform it. PS-3 is exactly probe + map + search.
4. **PS-7 is not "the opt-in bucket."** It is multi-fetch composition, and one of its five families (`digest-synthesis`) is a core always-on tool. Taxonomy must not track exposure gating (that is PS-8's job).
5. **PS-8 and PS-9 are enabling systems**, not peers of PS-1…PS-7 in user-value terms (T-4).
6. **Six CAP-level reassignments proposed** (R-1…R-6) — `tag_trust`, generic consent dismissal, `prefer_llms_txt`, `emit_capsule`, the watch wrapper, `FitMarkdown.Apply`.
7. **A seventh cross-cutting lens exists** — the agent-facing response contract (T-5). It was absent from the provisional list and is the product's most distinctive surface.
8. Browser default timeout is **60 s** (`BrowserExtractTimeouts.cs:8,23`), not the 120 s repeated in older prose. Recorded here so the taxonomy's PS-1 configuration row is not another copy of the wrong number.

---

## 9. UNCERTAIN

| Item | What would resolve it |
|------|----------------------|
| Whether `client-context` should ultimately live in PS-2 rather than PS-8 | A decision on whether ownership follows the *store* (PS-8, chosen here) or the *effect* (PS-2). Both are defensible; the choice is editorial, and the cross-system contract is documented either way. |
| Whether PS-4 should eventually merge into PS-5 if `schema-knowledge-extraction` stays a single family | A second live family in PS-4, or a decision that "typed extraction" is a playbook application rather than its own outcome. Kept separate here on spine-separation evidence (CAP-591). |
| Whether the six CAP reassignments (R-1…R-6) change any downstream count | Orchestrator applies them to `canonical-capabilities.json`; family membership counts shift by a few CAPs, family and system counts do not. |
| Whether `access-consent` and `quality-failure-semantics` should be one family | They are two halves of one registered post-processor pipeline (`OccamServiceCollectionExtensions.cs:34-36`). Kept separate to avoid deleting a slug downstream files reference; recorded here as an alias relationship, not a merge. |
