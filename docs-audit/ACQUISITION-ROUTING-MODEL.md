# ACQUISITION-ROUTING-MODEL (Phase 5J / P5-06)

**Product system:** PS-1 Acquisition (accepted as named; see Taxonomy verdict below).  
**SoT:** executable code under `src/FFOccamMcp.Core/Routing/`, `Backends/`, `PostProcessors/`, `Session/`, `Workers/`, plus `workers/{http,browser,css}-extract/` and `workers/shared/lib/`.  
**Wave-4 override:** Conflict **C1** / **EF-056** / **GAP-001** — prior CAP-052/104 cascade prose is **wrong**. This file reconstructs the real ladder.

**Answers this file settles:**

1. **What determines how Occam obtains a resource?**
2. **What changes when acquisition becomes difficult?**

---

## Taxonomy verdict (PS-1)

**Accept PS-1 Acquisition** as the product system for this model. Scope matches the provisional hypothesis: `OccamRouter` + backend policy + http/browser/managed + escalation + proxy/rotation + egress/SSRF + robots/throttle + sessions-for-fetch + domain tiers.

**Boundary note (not a rename):** `prefer_llms_txt`, `if_none_match`, and `cache_ttl_s` are *acquisition-adjacent gates* on the `occam_transcode` surface (they decide whether/which URL is fetched, or whether a live fetch is skipped). Post-fetch compile (`FinishMaterialize`, fit/focus/codecs) is **PS-2 Materialization** and is only referenced where it late-fails an otherwise “successful” extract (`thin_extract` / `content_selectors_miss`).

---

## Two definitive answers

### 1. What determines how Occam obtains a resource?

Ordered determinants (first decisive wins):

| # | Determinant | Effect | Evidence |
|---|-------------|--------|----------|
| A | Tool / entry surface | Which spine runs (TranscodePipeline vs probe/map HttpClient vs css-extract vs heal skeleton) | session tiers CAP-880 family; `subsystems/session-lifecycle.md` |
| B | Opt-in cache hit (`cache_ttl_s`) | Skip live acquisition entirely | `OccamTranscodeTool.cs:117-130`; FLOW-019 |
| C | `prefer_llms_txt` | May replace target URL with `{origin}/llms.txt` via **HTTP-only** pipeline first | `OccamTranscodeTool.cs:147-164` |
| D | `backend_policy` (`http` \| `browser` \| `http_then_browser`) | Selects single backend or cascade | `OccamBackendPolicyParser` `OccamRouter.cs:14-39,81-90` |
| E | Playbook preferred backend | Overrides policy **only when** requested policy is `HttpThenBrowser` | `TranscodePipeline.cs:87-104` |
| F | Browser availability downgrade | Tool may force `Http` if browser missing and auto-provision off | `OccamTranscodeTool.cs:135-145` |
| G | `FetchPreflight` | Blocks private/literal hosts; loads `session_profile` + env headers | `FetchPreflight.cs:21-68` |
| H | Robots/throttle (opt-in) | May return `robots_disallowed` before any backend | `TranscodePipeline.cs:130-143`; `RobotsThrottleService.cs:29-64` |
| I | Cascade gates inside router | Success / 404-410 stop / public-ref stop / browser / managed | `OccamRouter.cs:134-182` |
| J | Env-gated managed provider | Last rung only on `http_then_browser` after both locals fail | `OccamRouter.cs:163-175`; `ManagedExtractBackend.cs:28-95` |
| K | Dual-fail surface pick | `ChooseRawFallback` by `FailureRanking` (http vs browser only) | `OccamRouter.cs:182,206-213`; `FailureRanking.cs:10-21` |

**Not determinants of router escalation:** domain-tier `http_only` flag (probe-advisory only — `PreferHttpOnlyRoute`); markdown length/density for dual-fail winner; managed failure informativeness.

### 2. What changes when acquisition becomes difficult?

Difficulty is handled as a **gated ladder**, not a universal “try everything”:

- **Usable HTTP** (ok + body + not EQM-thin + not short challenge) → **stop** (cheap path). `OccamRouter.cs:134-137,194-199`
- **HTTP thin / short challenge / non-terminal fail** → escalate to **browser** (unless public-reference short-circuit). `:139-161`
- **HTTP 404/410** → **terminate** (no browser, no managed). `:144-147,216-218`
- **Public reference URL + failed HTTP** → **terminate** (no browser). `:149-152`; `DomainTierRegistry.cs:98-124`
- **Both locals fail** → optional **managed** if configured+host opted-in; on managed **success** surface managed; on managed **fail** surface = ranked(http, browser) only. `:163-182`
- **Post-processors** may still downgrade a routed “success” to `captcha_or_challenge` / `requires_login` / `thin_extract`. Orders 100/150/200.
- **Operator levers** (not automatic): `session_profile`, static/rotating proxy, managed env, `backend_policy=browser`, raise timeouts/size caps, disable private-URL block for local-dev.
- **Explicit non-goals:** no CAPTCHA solving; no identity/fingerprint rotation; managed is not a public `backend_policy` value.

---

## Decision model (hand-executable ladder)

Applies to callers that reach `TranscodePipeline.TranscodeCoreAsync` → `OccamRouter` (transcode, digest, claim_check, attest, dataset_export, batch, watch, crosscheck session vantage). Probe/map/css/heal diverge — see session tiers and failure map.

```
0. Tool gates (transcode only)
   0a. workers configured? else workers_unavailable
   0b. cacheable + TTL hit? → return cached (no fetch)          [FLOW-019]
   0c. browser policy + no browser + no auto-provision?
       → effectivePolicy = Http + warning
   0d. prefer_llms_txt?
       → Transcode(llmsTxtUrl, Http); if ok && len≥Min → DONE
       else fall through to requested URL

1. Playbook overlay (if playbook_policy applies)
   1a. Resolve seed; if preferredBackend set AND policy==HttpThenBrowser
       → policy := preferredBackend
   1b. Soft overlay Push(strict:false) around core

2. Preflight (TranscodeCoreAsync)
   2a. FocusIntent strip fragment → fetchUrl
   2b. FetchPreflight.Prepare(url, session_profile)
       - invalid scheme/host → invalid_url
       - literal private host + !OCCAM_ALLOW_PRIVATE_URLS → private_url_blocked
       - bad session id/file → invalid_session_profile / session_profile_not_found
       - merge OCCAM_REQUEST_HEADERS_FILE ⊕ session headers; optional storageState path
   2c. robotsThrottle.CheckAndThrottle
       - OCCAM_RESPECT_ROBOTS Disallow → robots_disallowed
       - else optional delay (throttle / crawl-delay); robots fetch fail-open

3. Router.TranscodeAsync(policy)
   3a. AreBackendsReady(policy)? else workers_unavailable
       HttpThenBrowser requires BOTH http and browser ready
   3b. policy == Http     → HTTP backend only → goto 4
   3c. policy == Browser  → Browser backend only → goto 4
   3d. policy == HttpThenBrowser → cascade below

4. Cascade HttpThenBrowser (OccamRouter.TranscodeHttpThenBrowserAsync)
   4.1 HTTP extract; Record(attempt)
   4.2 IsSuccessfulExtract(http)? → Finish(http)          [TERMINATE success]
   4.3 IsTerminalHttpFailure(http)?  // 404|410
       → Finish(http)                                    [TERMINATE — no browser/managed]
   4.4 IsPublicReferencePage(url)?
       → Finish(http)                                    [TERMINATE — no browser]
   4.5 Browser extract; Record(attempt, escalationReason)
   4.6 IsSuccessfulExtract(browser)? → Finish(browser)   [TERMINATE success]
   4.7 managed != null && ShouldAttempt(url)?
       → managed extract; Record
       → success? Finish(managed)                        [TERMINATE success]
       → fail? (do NOT use managed as surface winner)
   4.8 Finish(ChooseRawFallback(http, browser))          [TERMINATE dual-fail]

5. Post-processors (ok outcomes only mutate)
   Challenge(100) → RequiresLogin(150) → Thin(200)

6. If still ok → FinishMaterialize (PS-2; may emit thin_extract / content_selectors_miss)
```

### Escalation vs termination (the interesting short-circuits)

| Condition | Escalates? | Terminates ladder at | Evidence |
|-----------|------------|----------------------|----------|
| HTTP usable success | no | HTTP | `:134-137` |
| HTTP 404/410 | **no** | HTTP failure | `:144-147,216-218` |
| HTTP fail + public reference URL | **no** | HTTP failure | `:149-152` |
| HTTP thin / challenge / other fail | **yes → browser** | — | `:154-161` |
| Browser usable success | no | Browser | `:158-160` |
| Both fail + managed success | (managed run) | Managed | `:165-174` |
| Both fail + managed fail / skipped | no further rung | Ranked(http,browser) | `:182` |
| `backend_policy=http` or `browser` | never reaches managed | Single backend | `:81-86` |

---

## Per-rung cards

### Rung 0 — Response cache (opt-in, pre-ladder)

| Field | Content |
|-------|---------|
| Trigger | `cache_ttl_s > 0` and eligibility (not private URL, no `session_profile`, no `if_none_match`, no `diff_against`, no `prefer_llms_txt`) |
| Cost | Disk read; no network |
| Defeats | Repeat identical materialization within TTL |
| Cannot defeat | Auth walls, challenges, first fetch, any ineligible request |
| Config | `cache_ttl_s` param; `OCCAM_CACHE_DIR` |
| Failure codes | None (miss → live path) |
| Evidence | `OccamTranscodeTool.cs:117-130`; `Caching/TranscodeCacheEligibility.cs`; FLOW-019 |

### Rung 0b — `prefer_llms_txt` (opt-in URL substitute)

| Field | Content |
|-------|---------|
| Trigger | `prefer_llms_txt=true` and origin llms.txt URL buildable |
| Cost | Extra HTTP-only pipeline attempt |
| Defeats | Sites that publish sanctioned LLM markdown |
| Cannot defeat | Missing/short llms.txt (falls back to normal URL) |
| Config | Tool param only |
| Failure codes | Inherited from HTTP pipeline on fallback |
| Evidence | `OccamTranscodeTool.cs:147-164` |

### Rung 1 — Direct HTTP worker (`HttpExtractBackend`)

| Field | Content |
|-------|---------|
| Trigger | Policy `http`, or first step of `http_then_browser`, or llms probe |
| Cost | ~Node one-shot or HTTP daemon; **35s** host timeout |
| Defeats | Static HTML, many docs, PDF (worker auto-path), feeds when features set |
| Cannot defeat | SPA shells, many anti-bot walls, storageState-only cookies (headers only unless Cookie header present) |
| Config | `OCCAM_HOME`, `OCCAM_HTTP_*` proxy, `OCCAM_MAX_RESPONSE_BYTES` (default 8 MiB), `OCCAM_REQUEST_HEADERS_FILE`, session headers, playbook overlay ambient |
| Failure codes | `timeout`, `http_*`, `dns_error`, `tls_error`, `network_error`, `private_url_blocked` (worker alias), `response_too_large`, `extraction_failed`, … |
| Evidence | `HttpExtractBackend.cs:6-28`; `workers/http-extract/`; `response-body-cap.mjs:9-10` |

### Rung 1 auxiliaries — headers / UA / cookies / session

| Mechanism | Behavior | Evidence |
|-----------|----------|----------|
| Default UA/Accept | From `profiles/occam-fetch-defaults.json` or Chrome-like fallback | `OccamFetchDefaults.cs:9-47` |
| Env headers | `OCCAM_REQUEST_HEADERS_FILE` JSON merged into every preflight | `RequestHeadersMerger.cs:35+` |
| `session_profile` | Loads `OCCAM_SESSIONS_ROOT/<id>.json` headers; optional `storageState` path confined under sessions root | `SessionProfileHeaders.cs:54-113,195-238` |
| Ambient scope | Temp headers file + storageState path pushed for backends | `FetchHeadersScope`; backends `:22-23` |
| **Tier 1 full** | Headers + storageState on TranscodePipeline callers | session-lifecycle Tier 1 |
| **Tier 2 HTTP-only** | Probe/map: headers via probe path; **no** storageState / browser | Tier 2 |
| **Tier 3 headers-only** | Heal / extract_knowledge: headers forwarded; **storageState dropped** | Tier 3; `CssExtractWorker` has no storageState param |

### Rung 1b — Proxy / rotation (worker egress)

| Field | Content |
|-------|---------|
| Static proxy | `OCCAM_HTTP_PROXY` / `HTTPS_PROXY` / `NO_PROXY` → Node `egressFetch` + Playwright launch proxy | CAP-157/160; `egress-proxy.mjs` |
| Rotation | `OCCAM_PROXY_LIST` / `_FILE` → round-robin per **one-shot** spawn; **forces SkipDaemon** | CAP-162/164 |
| **Does NOT honor rotation** | Persistent HTTP daemon, browser pool/daemon, css-extract spawn, dom-skeleton spawn (static env only) | **CAP-165** |
| **Does NOT honor proxy at all** | Core C# `HttpClient`s (probe, map, managed, search, robots client uses SSRF guard but not OCCAM_* proxy) | **CAP-166** |
| Fail-open | Playwright proxy resolve failure → null proxy | GAP-030 |

### Rung 1c — Robots / throttle (pre-backend gate)

| Field | Content |
|-------|---------|
| Trigger | `OCCAM_RESPECT_ROBOTS` and/or `OCCAM_HOST_THROTTLE_MS>0` |
| Cost | robots.txt fetch (SSRF-guarded client) + possible sleep |
| Defeats | Accidental crawl of Disallow paths when opted in |
| Cannot defeat | Default-off; robots fetch errors **fail-open allow** | GAP-018 |
| Failure | `robots_disallowed` |
| Evidence | `RobotsThrottleService.cs:29-64`; `TranscodePipeline.cs:130-143` |

### Rung 2 — Browser worker (`BrowserExtractBackend`)

| Field | Content |
|-------|---------|
| Trigger | Policy `browser`, or cascade after non-terminal HTTP fail (and not public-ref) |
| Cost | Playwright Chromium; default **60s** per extract (`OCCAM_BROWSER_TIMEOUT_MS`, clamp 15–180s); +240s grace if auto-provision expected; daemon queue wait up to 900s |
| Defeats | Many SPAs, JS-rendered docs, some social public pages; consent dismiss / stealth / bypassCSP |
| Cannot defeat | Hard CAPTCHAs (no solver), true login without cookies/storageState, terminal 404 (never reached), public-ref HTTP fails (never reached) |
| Config | `OCCAM_BROWSER_*` pool/daemon/profile/channel/autoinstall; session storageState (Tier 1) |
| Failure codes | `timeout`, `captcha_or_challenge` (router/PP), `thin_extract`, `extraction_failed`, `workers_unavailable`, … |
| Evidence | `BrowserExtractBackend.cs:15-33`; `BrowserExtractTimeouts.cs:8-36`; AUTOMATIC-BEHAVIORS #7–8 |

### Rung 2b — Browser daemon / pool reuse

| Field | Content |
|-------|---------|
| Default | Shared daemon/pool when profile allows and rotation inactive |
| Side effects | New WS/Remote `InstallShared` → `StopAll` prior pool (latency spike) | AUTOMATIC #3; GAP-002 |
| Session churn | Per-call GUID headers temp file forces pool session recycle even for identical headers | CAP-881 |
| Rotation | Disables daemon path for primary extract tools | CAP-164 |

### Rung 3 — Managed providers (env-gated last resort)

| Provider | Implemented | Key required | Call shape | Evidence |
|----------|-------------|--------------|------------|----------|
| `jina` | yes | no (optional) | GET `{base}/{fullUrl}` | `JinaProvider.cs` |
| `firecrawl` | yes | yes | POST `/v1/scrape` markdown | `FirecrawlProvider.cs` |
| `spider` | yes | yes | POST `/crawl` limit:1 markdown | `SpiderProvider.cs` |
| `scrapfly` | yes | yes | GET scrape; **`render_js=true` hardcoded**; key in query | `ScrapflyProvider.cs:23-25` |

| Field | Content |
|-------|---------|
| Trigger | Cascade only; `_managed.ShouldAttempt(url)` = provider resolved + host allowlist |
| Cost | Third-party API; default client timeout 60s (`OCCAM_MANAGED_TIMEOUT_MS`); **sync** `HttpClient.Send` |
| Defeats | Some anti-bot / geo / JS pages the locals cannot (provider-dependent) |
| Cannot defeat | Unconfigured installs (silent skip); hosts outside `OCCAM_MANAGED_DOMAINS` when set; **never becomes dual-fail surface winner** |
| Config | `OCCAM_MANAGED_PROVIDER`, `_API_KEY`, `_BASE_URL`, `_DOMAINS` (unset = **all hosts**), `_TIMEOUT_MS` |
| Failure codes | `http_N`, `timeout`, `managed_error`, `managed_disabled`, `extraction_failed` (empty MD) |
| SSRF | `occam.managed` client has **no** `OutboundHttpGuard` — URL egresses to 3P | EF-003; DI `:82-83` |
| Evidence | `ManagedExtractBackend.cs:28-95`; `OccamRouter.cs:163-175` |

### Post-rung — Post-processors (quality / access gates)

| PP | Order | Action | Evidence |
|----|-------|--------|----------|
| ChallengePage | 100 | ok + MD≤2000 + challenge keywords → `captcha_or_challenge` | `ChallengePagePostProcessor.cs` |
| RequiresLogin | 150 | skipped if `session_profile` set; else Restricted → `requires_login` | `RequiresLoginPostProcessor.cs` |
| ThinExtract | 200 | EQM thin → `thin_extract` | `ThinExtractPostProcessor.cs` |

Router already treats thin/challenge as **non-success for escalation** (`IsSuccessfulExtract`); PPs catch residual ok outcomes after a single-backend policy or after compile-adjacent paths.

---

## Result-selection logic (where the old model was wrong)

**Prior claim (WRONG):** after dual failure, pick longer/denser markdown; managed is universal last rung that can “win” the failure surface.

**Actual code:**

```206:213:src/FFOccamMcp.Core/Routing/OccamRouter.cs
    private static ExtractRunResult ChooseRawFallback(ExtractRunResult http, ExtractRunResult browser) =>
        RawRank(browser) >= RawRank(http) ? browser : http;

    private static int RawRank(ExtractRunResult result) =>
        result.Ok && !string.IsNullOrWhiteSpace(result.Markdown)
            ? FailureRanking.Informativeness("thin_extract")
            : FailureRanking.Informativeness(
                FailureCodeStrings.ResolveTranscodeFailure(result.Failure, result.StatusCode));
```

`FailureRanking.Informativeness` (`FailureRanking.cs:10-21`):

| Rank | Codes |
|------|-------|
| 100 | `http_401`, `http_403`, `requires_login` |
| 90 | `captcha_or_challenge`, `anti_bot_blocked` (rank exists; not emitted by these PPs) |
| 85 | `tls_error` |
| 80 | other `http_4*` |
| 70 | `http_5*` |
| 60 | `thin_extract` (also used for raw ok-but-nonempty dual-fail candidates) |
| 50 | `timeout`, `network_error`, `dns_error` |
| 40 | `content_selectors_miss` |
| 10 | everything else |

Tie → **browser** (`RawRank(browser) >= RawRank(http)`).

**Managed:** success returns early (`:171-174`). Failure is recorded in `recovery[]` only; **never** passed to `ChooseRawFallback` (`:182`).

---

## Difficult acquisition playbook

| Obstacle | Automatic | Operator can add | Occam does **not** |
|----------|-----------|------------------|--------------------|
| Cloudflare / challenge interstitial | Router treats short challenge MD as fail → escalate browser; PP may emit `captcha_or_challenge`; consent dismiss / stealth / bypassCSP in browser worker | `session_profile` with storageState (Tier 1); managed provider; `backend_policy=browser` | **No CAPTCHA solving**; no fingerprint rotation (network-fetch-proxy CAP-180) |
| Login wall | RequiresLogin PP → `requires_login` if no session; AccessClassifier Restricted | Export cookies/`storageState` via session CLI; pass `session_profile`; prefer browser policy | Does not invent credentials; Tier 3 tools drop storageState |
| SPA / JS-rendered | Thin/challenge HTTP → browser; playbook may prefer browser under HttpThenBrowser | Force `backend_policy=browser`; playbook `preferredBackend` | HTTP-only policy never escalates |
| Paywall | Probe may signal `likelyPaywall`; extract may return thin/login-like | Session cookies if user has access | No paywall bypass product |
| HTTP 403 | Escalates to browser (not terminal); dual-fail prefers informative `http_403` (rank 100) | Session / proxy / managed | Does not claim 403 is terminal |
| Rate limit / 429 | May look like challenge keywords if body short; else `http_429` rank 80 | Throttle env; proxy rotation | No automatic backoff beyond robots crawl-delay when opted in |
| Robots Disallow | Only if `OCCAM_RESPECT_ROBOTS=1` → `robots_disallowed` | Leave unset (default ignore); or fix URL | Default is **not** polite; robots fetch fail-open |
| Geo / IP block | May surface as thin / challenge / http fail → browser → managed | Proxy / rotation / managed Scrapfly etc. | Core HttpClients ignore OCCAM proxy (CAP-166) |
| Oversized page | HTTP/browser workers: `OCCAM_MAX_RESPONSE_BYTES` (default 8 MiB) → `response_too_large` or partial per oversize mode | Raise cap (max 16 MiB) | **css-extract unbounded** `response.text()` (EF-043 / GAP-004) |
| Slow origin | HTTP 35s / browser timeout env / managed 60s | Raise `OCCAM_BROWSER_TIMEOUT_MS` / managed timeout | No adaptive retry loop beyond cascade |
| DNS / TLS failure | Escalates to browser under cascade (not in terminal set); dual-fail ranks 50 | Fix network; allow-private only for local | Probe may mask SSRF as `network_error` (below) |
| Private / SSRF target | Preflight literal private → `private_url_blocked`; worker DNS-pin (http/browser); C# OutboundHttpGuard on probe/robots/genome/TSA | `OCCAM_ALLOW_PRIVATE_URLS=1` for local-dev | **css-extract lacks private-ip import**; managed/search clients ungarded |

---

## Failure code map (acquisition layer)

| Condition | Code | Notes |
|-----------|------|-------|
| Workers / paths missing | `workers_unavailable` | Also HttpThenBrowser if either backend not ready |
| Unknown policy | `invalid_policy` | Router default arm |
| Preflight bad URL | `invalid_url` | `FetchPreflight` |
| Literal private host blocked | `private_url_blocked` | Preflight; worker `private_ip_blocked` normalized |
| Bad/missing session | `invalid_session_profile` / `session_profile_not_found` | |
| Robots Disallow | `robots_disallowed` | Opt-in only |
| HTTP status | `http_401`… / `http_404` / `http_410` / … | 404/410 **terminate** cascade |
| Timeout | `timeout` | |
| DNS / TLS / generic net | `dns_error` / `tls_error` / `network_error` | |
| Body oversize | `response_too_large` | HTTP/browser workers |
| Thin / challenge / login (PP or router gate) | `thin_extract` / `captcha_or_challenge` / `requires_login` | |
| Managed | `managed_error` / `managed_disabled` / http_* | Failures do not win surface |
| Recovery-only | `unusable_extract` | Attempt log when ok+body but not thin/challenge |
| **Dishonest: probe SSRF** | Often `network_error` | `OutboundUrlBlockedException` caught by broad `HttpRequestException` / catch-all in `HttpProbeFetcher.cs:164-175` — masks `private_url_blocked` / `dns_error` from guard (GAP-003 / EF via EFC-B-2) |

---

## Configuration surface (acquisition)

| Knob | Default | When flipped |
|------|---------|--------------|
| `backend_policy` | `http_then_browser` | `http`/`browser` = no cascade / no managed |
| `session_profile` | unset | Loads headers±storageState; skips RequiresLogin PP; cache ineligible |
| `prefer_llms_txt` | false | HTTP llms.txt attempt first |
| `if_none_match` | unset | AF-6 unchanged envelope after fetch; cache ineligible |
| `cache_ttl_s` | unset/≤0 | Opt-in disk cache (FLOW-019) |
| `playbook_policy` | off unless set | Soft overlay + preferredBackend override under cascade |
| `OCCAM_ALLOW_PRIVATE_URLS` | off | Allows private/localhost fetches |
| `OCCAM_RESPECT_ROBOTS` | off | Enforce Disallow |
| `OCCAM_HOST_THROTTLE_MS` | 0 | Per-host delay |
| `OCCAM_HTTP(S)_PROXY` / `NO_PROXY` | unset | Worker egress proxy (not Core HttpClients) |
| `OCCAM_PROXY_LIST`/`_FILE` | unset | Rotation + one-shot spawns |
| `OCCAM_MANAGED_*` | unset/disabled | Enables last rung |
| `OCCAM_MAX_RESPONSE_BYTES` | 8 MiB | Cap/raise body limit (http/browser) |
| `OCCAM_BROWSER_TIMEOUT_MS` | 60000 | Per-page browser budget |
| `OCCAM_BROWSER_DAEMON` / `PROFILE` / pool size | shared/on | Pool vs isolated |
| `OCCAM_BROWSER_AUTOINSTALL` | on | Auto chromium vs typed miss |
| `OCCAM_REQUEST_HEADERS_FILE` | unset | Ambient headers |
| `OCCAM_SESSIONS_ROOT` | user data `sessions` | Session JSON root |
| `OCCAM_DOMAIN_TIERS_PATH` | built-in JSON | Extends tier hints; **does not** set router http_only skip |

---

## Automatic / silent acquisition behavior

Cross-ref `AUTOMATIC-BEHAVIORS.md`:

| # | Behavior | Acquisition impact |
|---|----------|-------------------|
| 2 | HTTP daemon prewarm | Cold-start latency |
| 3 | InstallShared kills prior browser pool | Cascade browser rung spikes |
| 4 | Post-processors always | May flip ok→fail after rung success |
| 7–8 | Stealth, bypassCSP, consent | Browser rung mutates page; no CAPTCHA solve |
| 17 | URL fragment → FocusIntent | Changes fetch URL / focus |
| 21 | Proxy list → rotation + one-shot | Disables warm daemon for extracts |
| (B-blind) | Internal always-on `json_blocks,json_tables` features | Worker feature flags, not public sidecars unless opted |
| (router) | Public-reference no-browser | Silent termination of cascade |
| (router) | Thin/challenge HTTP auto-escalate | Silent second rung |

---

## Platform differences (acquisition-relevant)

Cross-ref `PLATFORM-DIFFERENCES.md`:

| Site | Difference |
|------|------------|
| Playwright cache paths | Windows `%LOCALAPPDATA%\ms-playwright` vs macOS/Linux cache dirs — install location only |
| `WorkerProcessGroup` | Win32 Job Object vs POSIX process-group kill — cleanup mechanism |
| Domain tier path list | OS `Path.PathSeparator` for `OCCAM_DOMAIN_TIERS_PATH` |
| `VectorizedHtmlScanner` (probe HTML) | AVX2 / AdvSimd / SSE2 / scalar — **throughput**, same semantics |
| Session storageState path check | Windows ordinal-ignore-case prefix vs Unix ordinal | `SessionProfileHeaders.cs:230-232` |

No platform-specific change to cascade short-circuits or `FailureRanking`.

---

## Engineering findings affecting acquisition (no fixes)

| ID | Relevance |
|----|-----------|
| **EF-056** | Cascade model correction (this file) — not a runtime bug |
| EF-003 | Managed HttpClient lacks OutboundHttpGuard |
| EF-043 | css-extract SSRF / body-cap parity gap vs http/browser |
| EF-041 / GAP-002 | Multi-session pool kill via InstallShared |
| EF-045 | Fragment / cache key collision risk with FLOW-019 |
| GAP-001 | Cascade prose wrong (404/410, public-ref, FailureRanking, managed fail) |
| GAP-003 | Probe SSRF → `network_error` mask |
| GAP-014 | Managed fail invisible as surface winner |
| GAP-018 | Robots fail-open |
| GAP-030 | Playwright proxy fail-open |
| CAP-165/166 | Proxy rotation / Core HttpClient holes |
| CAP-881 | Session headers path churn kills pool reuse |

---

## UNCERTAIN

| Item | What would resolve |
|------|--------------------|
| Exact exception wrapping of `OutboundUrlBlockedException` inside `SocketsHttpHandler` (InnerException vs outer type only) | Runtime repro or unit that asserts probe failure code for private DNS target |
| Whether any `#if OCCAM_GATE`-only caller selects `trackRedirects:false` | Grep gate project (prod callers: none) |
| PDF URL that fails HTTP PDF path then browser-renders viewer chrome quality | Live corpus case + visual artifact |
| Managed provider semantic fidelity vs local markdown (Scrapfly always JS bill) | Side-by-side fixture per provider |
| Default browser timeout prose “~120s” vs code default **60s** | Doc scrub only (code is SoT: `BrowserExtractTimeouts.cs:8,23`) |

---

## Corrections to prior model (C1 checklist)

Every point where reality ≠ CAP-052/104 / `tools/occam_transcode.md` cascade prose:

1. **Terminal set is 404/410 only** — not a broad “non-retryable” set including `dns_error`. Code: `OccamRouter.cs:144-147,216-218`. Wrong prose: `docs-audit/tools/occam_transcode.md:88-91`.
2. **`IsPublicReferencePage` silently skips browser** after failed HTTP (wikipedia `/wiki/`, rfc-editor `/rfc/`, tier_a_docs + `/docs/`, non-login). Code: `OccamRouter.cs:149-152`; `DomainTierRegistry.cs:98-124`. Missing from CAP-052 ladder; CAP-104 mis-attributed skip to `http_only`.
3. **Tier `http_only` does not skip router browser escalation** — only probe `PreferHttpOnlyRoute`. Code: `DomainTierRegistry.cs:204-231` vs router `:149`. Wrong: CAP-104 “skip browser escalation entirely”.
4. **`ChooseRawFallback` ranks by `FailureRanking.Informativeness`**, not longer/denser markdown. Code: `OccamRouter.cs:206-213`; `FailureRanking.cs:10-21`. Wrong: `occam_transcode.md:92-97`.
5. **Raw ok-but-unusable body ranks as `thin_extract` (60)**, not by content density. Code: `OccamRouter.cs:209-211`.
6. **Managed runs only on `http_then_browser` after both locals fail**, never as a `backend_policy` value. Code: `OccamRouter.cs:81-86,163-175`.
7. **Managed success can surface; managed failure never enters `ChooseRawFallback`** — surface = http vs browser only. Code: `OccamRouter.cs:171-182`.
8. **`HttpThenBrowser` readiness requires both backends** — managed cannot salvage missing browser workers. Code: `OccamRouter.cs:96-101`.
9. **Router success gate pre-escalates thin/challenge** (threshold 2000) before PPs. Code: `OccamRouter.cs:188-199`.
10. **Probe masks SSRF blocks as `network_error`**. Code: `HttpProbeFetcher.cs:164-175` + `OutboundHttpGuard.cs:38-53`.

Canonical IDs: correct CAP-052/104 in Phase 5B; EF-056; GAP-001/003/014.
