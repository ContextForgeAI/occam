# W4-B — Blind negative-space audit: routing / backends / post-processors / probe / access / text / semantics

**Owner:** W4-B  
**SoT:** shipped C# under `src/FFOccamMcp.Core/{Routing,Backends,PostProcessors,Probe,Access,Text,Semantics}` (+ DI wiring in `Composition/OccamServiceCollectionExtensions.cs` for HttpClient/SSRF).  
**Method:** code discovery first; only then compare to Wave 1–3 model artifacts.  
**Date:** 2026-07-26

---

## 1. Blind inventory

Externally meaningful behaviors observed in code (shipped unless marked DEAD).

### 1.1 Escalation ladder (`OccamRouter`)

| # | Behavior | Evidence | Trigger / notes |
|---|----------|----------|-----------------|
| B1 | Policies: `http` \| `browser` \| `http_then_browser` (+ hyphen alias) | `OccamBackendPolicyParser` | Default parse empty → `HttpThenBrowser` |
| B2 | Single-backend policies never touch managed | `OccamRouter.TranscodeAsync` switch | Managed is **not** a policy |
| B3 | `http_then_browser`: HTTP → (gates) → browser → (optional) managed | `TranscodeHttpThenBrowserAsync` | Only path that can call managed |
| B4 | Success = `Ok` + non-empty MD + **not** EQM-thin + (len>2000 **or** not challenge) | `IsSuccessfulExtract` | Router pre-escalates raw thin/challenge before post-processors |
| B5 | Terminal HTTP short-circuit: status **404/410** or failure `http_404`/`http_410` → **no browser** | `IsTerminalHttpFailure` | Does **not** short-circuit `dns_error`, `timeout`, `http_403`, etc. |
| B6 | `DomainTierRegistry.IsPublicReferencePage(url)` → **no browser** after failed HTTP | `OccamRouter:149-152` | Wikipedia `/wiki/`, rfc-editor `/rfc/`, tier_a_docs + `/docs/` (not login path) |
| B7 | Managed last rung iff `_managed != null` && `ShouldAttempt(url)` | `OccamRouter:163-175` | Env-gated; see §1.3 |
| B8 | Final dual-fail choice: `ChooseRawFallback(http, browser)` by `FailureRanking` only — **managed attempt never selected as surface winner** | `OccamRouter:182` | Managed success returns early; managed failure only stays in `recovery[]` |
| B9 | `AreBackendsReady(HttpThenBrowser)` requires **both** http+browser ready | `AreBackendsReady` | Managed cannot salvage `workers_unavailable` if browser missing |
| B10 | Recovery log records every attempt + `EscalationReason` | `TranscodeAttempt` | Surfaced via tool mapping |

### 1.2 Pipeline / silent overlays (`TranscodePipeline`)

| # | Behavior | Evidence |
|---|----------|----------|
| B11 | Always injects internal `json_blocks,json_tables` into `OccamFeaturesScope` (public opt-in still gates response projection) | `TranscodePipeline:44-47` |
| B12 | `playbook_policy` apply → resolve seed → **preferred backend overrides only when requested policy is `HttpThenBrowser`** | `ResolveEffectiveBackendPolicy` |
| B13 | Soft playbook overlay (`PlaybookVerifyScope` strict:false); stamps `PlaybookId` only if `OverlayApplied` | `TranscodePipeline:63-78` |
| B14 | `FetchPreflight` (privacy literal + session) then optional `robotsThrottle` | `TranscodeCoreAsync` |
| B15 | Post-processors ordered by `Order` then `FinishMaterialize` (compile can emit `content_selectors_miss` / `thin_extract`) | Orders 100/150/200 |
| B16 | Success attaches EQM `Quality` + AF-1 `Confidence` (browser +0.05, truncation −0.05) | `FinishMaterialize` + `ExtractQualityEvaluator` |

### 1.3 Managed providers

| # | Behavior | Evidence |
|---|----------|----------|
| B17 | Enable: `OCCAM_MANAGED_PROVIDER` ∈ {jina,firecrawl,spider,scrapfly}; key required except Jina | `ManagedExtractBackend.ResolveProvider` |
| B18 | Domain gate: `OCCAM_MANAGED_DOMAINS` unset → **any host**; else suffix allowlist | `IsHostOptedIn` |
| B19 | Env: `OCCAM_MANAGED_API_KEY`, `OCCAM_MANAGED_BASE_URL`, `OCCAM_MANAGED_TIMEOUT_MS` (default 60s) | DI + backend |
| B20 | Firecrawl: POST `{url,formats:[markdown]}` + Bearer → third party | `FirecrawlProvider` |
| B21 | Jina: GET `{base}/{fullUrl}` + optional Bearer; URL embedded in path | `JinaProvider` |
| B22 | Spider: POST `{url,limit:1,return_format:markdown}` + Bearer | `SpiderProvider` |
| B23 | Scrapfly: GET query `key`+`url`+`format=markdown`+**`render_js=true` hardcoded** | `ScrapflyProvider` |
| B24 | Normalize: backend `managed_<name>`; empty→`extraction_failed`; HTTP→`http_N`; else `timeout`/`managed_error` | `ManagedResults` |
| B25 | `ExtractAsync` wraps **sync** `HttpClient.Send` in `ValueTask.FromResult` (blocks thread) | `ManagedExtractBackend:30-45` |
| B26 | Named client `occam.managed`: timeout only — **no** `OutboundHttpGuard.ConnectCallback` | `OccamServiceCollectionExtensions:82-83` |

### 1.4 SSRF / privacy / HttpClient matrix

| Client name | ConnectCallback? | User-influenced URL? | Notes |
|-------------|------------------|----------------------|-------|
| `probe.redirectTracking` | **YES** | Yes | Manual redirect follow; each hop reconnects → re-validate |
| `probe.autoRedirect` | YES | Yes | **DEAD selection** — no caller passes `trackRedirects:false` |
| `playbook.wellKnownGenome` | YES | Yes | |
| `receipts.timeAnchor` | YES | Operator TSA URL | |
| robots (`RobotsThrottleService`) | YES | Origin of user URL | |
| `occam.managed` | **NO** | Target URL sent to 3P API | EF-003 class |
| Search `HttpClient` | **NO** | Query/provider URLs | |
| Translation `HttpClient` | **NO** | Operator endpoint | |
| `BrowserDaemonClient` raw `HttpClient` | **NO** | Local daemon | |

| # | Behavior | Evidence |
|---|----------|----------|
| B27 | `PrivacyClassifier` preflight: literal IP / localhost / `.local` / `.internal` only — **no DNS** | `PrivacyClassifier.IsPrivateHost` |
| B28 | `OutboundHttpGuard`: DNS resolve both families, reject private, **pin connect** to validated addrs | `OutboundHttpGuard` |
| B29 | `OCCAM_ALLOW_PRIVATE_URLS=1` disables private rejection | `IsPrivateUrlBlocked` |
| B30 | Probe path: `OutboundUrlBlockedException` **not** caught specially → often surfaces as `network_error` | `HttpProbeFetcher` catch blocks |
| B31 | **No C# meta-refresh follower** in Probe/Routing (worker owns meta-refresh) | grep scope |

### 1.5 Failure codes (routing/backends/PP-relevant)

Emitted or normalized in this scope (non-exhaustive of whole product):

| Code | Primary trigger(s) |
|------|-------------------|
| `workers_unavailable` | Backends not ready; infra worker failures folded |
| `invalid_policy` | Unknown policy enum arm |
| `timeout` | TimedOut extract / managed cancel |
| `http_401`…`http_N` / `http_error` | Status or `FromHttpStatus` |
| `dns_error` / `tls_error` / `network_error` | Normalize families |
| `private_url_blocked` | Preflight / guard / worker alias |
| `thin_extract` | Router success gate, Thin PP, empty compile |
| `captcha_or_challenge` | Router gate + Challenge PP (≤2000 chars) |
| `requires_login` | RequiresLogin PP via AccessClassifier Restricted |
| `content_selectors_miss` | Compile path |
| `robots_disallowed` | Robots throttle (pipeline) |
| `managed_error` / `managed_disabled` | ManagedResults / disabled provider |
| `unusable_extract` | Attempt log only when Ok+body but neither thin nor challenge | **Internal recovery dimension** |
| `redirect_loop` | Probe follower >10 hops |
| `unsupported_content_type` / `invalid_url` | Probe |

`FailureRanking` informativeness drives dual-fail winner (`http_401/403/requires_login`=100 … generic=10). Rank entry `anti_bot_blocked` exists but is **not emitted** by these post-processors.

### 1.6 Post-processors / access / EQM

| # | Behavior | Evidence |
|---|----------|----------|
| B32 | Order: Challenge(100) → RequiresLogin(150) → Thin(200) | DI + Order props |
| B33 | Challenge/Thin: only mutate `Ok` outcomes; clear MD → fail codes | PP classes |
| B34 | RequiresLogin: skipped if `session_profile` set; else always attaches `AccessAssessment`; Restricted → `requires_login` | PP + `AccessClassifier` |
| B35 | Access Restricted iff 401 / WWW-Auth / login-redirect / blocking identity UI without usable content | `AccessEvidence.HasBlockingIdentityUi` |
| B36 | EQM multi-signal BE vs SQD (`short_quality`); length never sole reject | `ExtractQualityEvaluator` |
| B37 | **DEAD:** `LoginWallDetector.LooksLikeLoginWall` — zero call sites | shipped symbol, unused |

### 1.7 Domain tiers

| # | Behavior | Evidence |
|---|----------|----------|
| B38 | Load `profiles/tiers/domain-tier.v1.json` + `OCCAM_DOMAIN_TIERS_PATH` (pathsep list); cache forever; invalid JSON skipped | `DomainTierRegistry.LoadEntries` |
| B39 | Tier `http_only` drives **probe** `PreferHttpOnlyRoute` / signal overlays — **does not** gate router browser skip | contrast B6 |
| B40 | Silent probe overlays: `news_consent`, `anti_bot_blogs` challenge heuristic, login-path segment match | `ApplyTierHints`, `IsLoginPath` |
| B41 | Social challenge suppress: Instagram/LinkedIn + prose≥350 | `ShouldSuppressProbeChallengeStop` |

### 1.8 Probe / text / semantics

| # | Behavior | Evidence |
|---|----------|----------|
| B42 | Two DI clients; live path always `trackRedirects=true` | `HttpProbeFetcher` |
| B43 | Manual redirects max 10; Location resolve absolute/relative; **no** PrivacyClassifier re-check of Location string (rely on ConnectCallback) | `HttpRedirectFollower` |
| B44 | Byte clamp 1…4MiB (default 256KiB); encoding via charset/BOM | fetcher |
| B45 | Classifier: SPA/challenge/login/paywall/consent + tier merge + Access overlay | `HtmlProbeClassifier` |
| B46 | Link extract via `HtmlStreamScanner` + SIMD tag scan; social via `HtmlHeadScanner` | Probe+Text |
| B47 | `VectorizedHtmlScanner`: AVX2 / AdvSimd / SSE2 / scalar fallbacks via `Vector*.IsHardwareAccelerated` | platform-dependent **perf**, same semantics |
| B48 | `SemanticOutcomeMapper`: maps access/focus/completeness to public snake fields; digest focus aliases `focusMatched` | Semantics |

### 1.9 Local backends

| # | Behavior | Evidence |
|---|----------|----------|
| B49 | HTTP worker timeout 35s; session headers/storage + overlay via ambient scopes | `HttpExtractBackend` |
| B50 | Browser: same scopes + inline overlay JSON; timeout via provision-aware resolver | `BrowserExtractBackend` |

---

## 2. Gap classification

Compared to `CAPABILITY-INVENTORY.md`, `capabilities.json`, `tools/occam_transcode.md`, `tools/occam_probe.md`, `subsystems/browser-workers.md`, `subsystems/network-fetch-proxy.md`, `ENGINEERING-FINDINGS.md` (EF-003), `ENVIRONMENT-VARIABLES.md`.

| ID | Finding | Label | Evidence |
|----|---------|-------|----------|
| G1 | Escalation ladder + managed-only-on-`http_then_browser` | COVERED_PARTIALLY | CAP-052/054; model incomplete on B8 managed discard |
| G2 | CAP-052 step 3 claims terminal set / implies dns etc. may not escalate; code only short-circuits **404/410** (+ public-reference skip) | COVERED_WRONG | `OccamRouter:216-218` vs `occam_transcode.md:88-91` |
| G3 | CAP-052 claims `ChooseRawFallback` picks longer/denser markdown; code ranks **failure-code informativeness** | COVERED_WRONG | `FailureRanking` + `ChooseRawFallback` vs md:92-97 |
| G4 | CAP-104 claims tier `http_only` “skip browser escalation entirely”; router ignores `HttpOnly` — only `IsPublicReferencePage` skips | COVERED_WRONG | `DomainTierRegistry.PreferHttpOnlyRoute` used in `OccamProbeModels` only; router:149 uses `IsPublicReferencePage` |
| G5 | Managed no ConnectCallback / URL egress to 3P | COVERED_EXACTLY | EF-003, CAP-194/238 |
| G6 | Scrapfly `render_js=true` hardcoded | COVERED_EXACTLY | CAP-057/242 |
| G7 | Sync managed `Send` | COVERED_EXACTLY | browser-workers CAP notes |
| G8 | `probe.autoRedirect` dead | COVERED_EXACTLY | CAP-436 |
| G9 | Post-processor order + challenge/login/thin | COVERED_EXACTLY | CAP-094–097 |
| G10 | Managed failure never wins `ChooseRawFallback`; only http vs browser | MISSING_EDGE | `OccamRouter:182` |
| G11 | `IsPublicReferencePage` silent **no-browser** on failed HTTP (wikipedia/rfc/tier_a docs) | MISSING_EDGE / MISSING_WORKFLOW | `OccamRouter:149-152`; CAP-104 mentions allow-list for login FP, not cascade skip |
| G12 | `OutboundUrlBlockedException` → probe often `network_error` (masks `private_url_blocked`/`dns_error`) | MISSING_FAILURE_SEMANTIC / MISSING_SECURITY_SEMANTIC | `HttpProbeFetcher:160-175` vs guard throw |
| G13 | Search/translate clients lack ConnectCallback | COVERED_PARTIALLY | network-fetch-proxy notes managed/search; translate less crisp as security story |
| G14 | No C# meta-refresh; worker has it — probe/map C# path has **no** meta-refresh re-validation | COVERED_PARTIALLY | CAP-152 is worker; probe audit notes manual 3xx only |
| G15 | `LoginWallDetector` dead shipped code | DEAD_CODE_MISTAKEN_AS_PRODUCT risk if docs cite it | zero refs; Access path replaced it |
| G16 | `unusable_extract` recovery-only code | MISSING_FAILURE_SEMANTIC | `ResolveAttemptFailure` |
| G17 | `managed_error` / `managed_disabled` taxonomy | COVERED_PARTIALLY | browser-workers mentions; FailureCodeStrings lacks dedicated agent messages for `managed_*` |
| G18 | Always-on internal `json_blocks/json_tables` feature injection | COVERED_PARTIALLY | CAP-078/083 family; silent trigger understated in routing surface |
| G19 | Preferred-backend override only under `HttpThenBrowser` | COVERED_EXACTLY | CAP-072 / ResolveEffectiveBackendPolicy |
| G20 | VectorizedHtmlScanner SIMD platform branches | MISSING_RUNTIME_SURFACE | `VectorizedHtmlScanner.cs:37-77,92-196` |
| G21 | `HttpThenBrowser` readiness requires browser even when only HTTP would run first | MISSING_EDGE | `AreBackendsReady` `_` arm |
| G22 | Domain tier JSON extensibility + silent invalid-file skip | COVERED_PARTIALLY | ENV catalog; silent skip under-modeled as failure mode |
| G23 | SemanticOutcomeMapper INV-9 public fields | COVERED_EXACTLY | CAP-107 |
| G24 | Challenge threshold 2000 shared router/PP | COVERED_EXACTLY | CAP-095 |

### Proposed NEW caps (orchestrator allocates)

- `CAP-NEW-B-1` — Managed attempt excluded from dual-fail surface selection (recovery-only).
- `CAP-NEW-B-2` — `IsPublicReferencePage` cascade short-circuit (no browser on HTTP fail).
- `CAP-NEW-B-3` — Probe SSRF exception → failure-code masking (`network_error`).
- `CAP-NEW-B-4` — Tier-A SIMD HTML scanners (VectorizedHtmlScanner + consumers) as runtime/perf surface.

### Proposed edges

- `CAP-052` —**SHORT_CIRCUITS_TO**→ `IsPublicReferencePage` (not `http_only`).
- `CAP-052` —**FALLS_BACK_VIA**→ `FailureRanking` (not markdown density).
- `CAP-054` —**RECOVERY_ONLY_ON_FAIL**→ managed (no ChooseRawFallback inclusion).
- `OutboundHttpGuard` —**WIRED_TO**→ {probe×2, genome, TSA, robots}; —**NOT_WIRED_TO**→ {managed, search, translate, browser-daemon}.

### Proposed artifacts

- `recovery[]` / `TranscodeAttempt` (incl. managed) — already modeled CAP-098; **edge** that managed fail does not become `Backend`/`FailureCode` of final outcome needs explicit relation.
- Third-party request bodies/query (URL + API key) — privacy artifact under CAP-054; Scrapfly key-in-query deserves MISSING_SECURITY_SEMANTIC emphasis vs Bearer-in-header peers.

### Proposed workflows

- WF: `http_then_browser` + public-reference HTTP fail → **stop** (no browser, no managed).
- WF: both local fail + managed fail → surface = ranked(http,browser) **ignoring** managed failure informativeness.

### Automatic / silent (top)

| Trigger | Visible? | Configurable? | Disableable? |
|---------|----------|---------------|--------------|
| Internal json_blocks/tables features | Indirect (sidecars only if opted) | Via public flags for exposure | Exposure yes; fetch always |
| EQM thin / challenge escalate | Via recovery + backend change | No | Policy `http`/`browser` only |
| Public-reference no-browser | Failure stays HTTP | Tier JSON / hardwired hosts | Partial via tiers path |
| Managed 3P call | `backend=managed_*` | Env opt-in | Yes (unset provider) |
| Tier probe overlays | domainTier / signals | `OCCAM_DOMAIN_TIERS_PATH` | Replace/empty extra files |
| RequiresLogin PP downgrade | `ok:false` + code | session_profile bypass | Session only |

### Failure / fallback (top)

1. Thin/challenge HTTP → browser → maybe managed → rank http vs browser.  
2. 404/410 HTTP → stop.  
3. Public reference HTTP fail → stop.  
4. ConnectCallback block on probe → often `network_error`.  
5. Compile empty / selector miss after “success” extract → late `thin_extract` / `content_selectors_miss`.

### Config gaps (top)

- Model overstates `http_only` as cascade control.  
- `OCCAM_MANAGED_DOMAINS` unset = all hosts (powerful; documented but easy to miss).  
- Domain tier invalid JSON silently ignored (no operator signal).

### Platform diffs (top)

- `VectorizedHtmlScanner`: AVX2 (x64), AdvSimd (ARM), SSE2, else scalar — **behavior identical**, throughput differs.  
- Domain tier pathsep is OS `Path.PathSeparator`.

### EFC proposals (do not mint EF-NNN)

| ID | Class | Confidence | Summary |
|----|-------|------------|---------|
| EFC-B-1 | BUG-CANDIDATE | PROVEN | CAP-052/104 prose wrong vs router short-circuit + ChooseRawFallback ranking |
| EFC-B-2 | SECURITY-CANDIDATE | PROVEN | Probe masks `OutboundUrlBlockedException` as `network_error` |
| EFC-B-3 | DESIGN-QUESTION | PROVEN | Managed fail excluded from final failure ranking |
| EFC-B-4 | OBSERVATION | PROVEN | `LoginWallDetector` dead; AccessClassifier is live SoT |
| EFC-B-5 | OBSERVATION | PROVEN | `HttpThenBrowser` blocked when browser workers missing even for HTTP-only successes path start |

---

## 3. Convergence

Independent discovery still found **major** unmodeled / wrong-modeled cascade semantics (G2–G4, G10–G12). Core managed/SSRF/post-processor inventory is largely present. **CONVERGENCE_IN_SCOPE: NO** for cascade truthfulness; **YES** for provider list and PP ordering.

## 4. Uncertainties

- Exact wrapping of `OutboundUrlBlockedException` by `SocketsHttpHandler` (InnerException vs message-only) — outcome still non-`private_url_blocked` in fetcher.  
- Whether any `#if OCCAM_GATE` test path selects `trackRedirects:false` outside Core prod callers (prod callers: none).  
- Worker-side SSRF for extract URLs is out of this C# scope (referenced only by contrast).
