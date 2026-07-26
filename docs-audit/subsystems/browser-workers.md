# Browser workers + extract backends (including managed providers) — code-derived capability audit

**Agent:** S18 (Wave 1) · **CAP range:** CAP-200 … CAP-249 · **SoT:** current executable code only (docs untrusted, not read for claims).
**Repo:** `c:\PROJECTS\FFOccamMCP`

All line numbers are as of the inspection commit at audit time; re-verify before citing in a public doc.

---

## 0. Wiring map — which MCP tool reaches which backend

This matters more than any individual capability: several tools that *sound* like they read a page do **not**
go through the browser pool, the HTTP daemon, or the managed-provider escalation at all.

| Tool / service | Path to backends | Reaches `browser`? | Reaches `managed_*`? |
|---|---|---|---|
| `occam_transcode` (`Tools/OccamTranscodeTool.cs:19,163`) | `TranscodePipeline.TranscodeAsync` → `OccamRouter` | Yes (policy-gated) | Yes, only under `http_then_browser` (default) after both http+browser fail |
| `occam_digest` (`Services/DigestService.cs:56,309`) | same `TranscodePipeline`, per URL | Yes | Yes (same conditions) |
| `occam_playbook_save` (verify step, `Playbooks/PlaybookSaveVerifier.cs:5`) | same `TranscodePipeline` | Yes | Yes |
| `occam_watch`, `occam_crosscheck`, `occam_claim_check`, `occam_dataset_export`, batch submit | all construct/consume `TranscodePipeline` (`Watch/WatchService.cs`, `Consensus/ConsensusService.cs`, `Claims/ClaimCheckService.cs`, `Dataset/DatasetExportService.cs`, `Batch/BatchJobProcessor.cs`) | Yes | Yes |
| `occam_playbook_heal` (`Playbooks/PlaybookHealService.cs:5,7`) | `DomSkeletonWorker` → browser pool/daemon **directly** (skeleton capture, not `renderAndExtract`) | Yes (skeleton only) | **No** — never touches `OccamRouter`/managed |
| `occam_extract_knowledge` (`Services/KnowledgeExtractService.cs:10-114`) | `CssExtractWorker` spawns `css-extract.mjs` per call; on 401/403/429 the worker itself opens a **separate, non-pooled** Playwright session (`css-extract.mjs:111-128` → `browser-session.mjs`) | Yes, but bypasses the C# `BrowserExtractBackend`/pool/daemon entirely | **No** |
| `occam_probe` (`Probe/ProbeService.cs`, `HttpProbeFetcher`) | direct HTTP fetch, no `IExtractBackend` | **No** | **No** |
| `occam_map` (`Services/MapService.cs:8`) | `HttpProbeFetcher` only | **No** | **No** |

**Consequence:** `occam_extract_knowledge`'s browser fallback is architecturally isolated from the main
browser subsystem — it gets none of the pool's warm-session reuse, none of the concurrency gate, and none
of the managed-provider escalation. `occam_playbook_heal` shares the pool for skeleton capture but never
reaches a managed provider even when configured.

---

## 1. HTTP extract backend

- **CAP-200 — HTTP extract backend (`node_readability_turndown`).** `Backends/HttpExtractBackend.cs:6-28`. 35s fixed timeout (`DefaultHttpTimeoutMs = 35_000`, line 8). Delegates to `IHttpExtractRunner` → `workers/http-extract/extract.mjs`. Readiness = `workerPaths.IsConfigured` (both scripts exist on disk) — **not** an actual network probe.
- **CAP-201 — HTTP daemon (long-lived, amortized).** `Workers/HttpDaemonHost.cs:10-197`. Default port 39218 (`OCCAM_HTTP_DAEMON_PORT`), idle TTL 120s (`OCCAM_HTTP_DAEMON_IDLE_TTL_MS`), disable with `OCCAM_HTTP_DAEMON=0`. Spawns `workers/http-extract/http-daemon.mjs`. `/health`, `/extract`, `/recycle` endpoints (`HttpDaemonClient`, lines 199-299). Daemon path is skipped when: proxy rotation is configured (`SkipDaemonForRotation`), a playbook overlay path is set, or `HttpExtractRoutingScope.PreferOneShot` (`Workers/HttpExtractRunner.cs:44-46`).
- **CAP-202 — HTTP one-shot fallback.** `Workers/HttpExtractRunner.cs:67-164`. Fresh `node extract.mjs <url>` process per call when daemon disabled/unhealthy/bypassed.
- **CAP-237 — PDF extraction (HTTP path only).** `workers/http-extract/lib/http-extract-run.mjs:463-529`. Binary read capped at 16 MiB default (`OCCAM_MAX_PDF_BYTES`, 64 KiB–128 MiB clamp). Text-layer only — scanned/image PDFs fail honestly (`pdf_no_text_layer`), **no OCR**. Not reachable via the browser backend.
- **JSON feed codec** (opt-in `json_feed` feature) parses RSS/Atom/JSON-Feed bodies directly instead of running Readability (`http-extract-run.mjs:281-302`).
- **Plain-text pass-through**: non-HTML text bodies short-circuit Readability (`shouldPassThroughPlainText`, same file lines 211-230).

## 2. Browser extract backend — core

- **CAP-203 — Browser extract backend (Playwright Chromium).** `Backends/BrowserExtractBackend.cs:6-35`. Timeout resolved by `BrowserExtractTimeouts.ResolvePerExtractTimeoutMs` — default 60s (`OCCAM_BROWSER_TIMEOUT_MS`, 15s–180s clamp), raised to a 240s floor when a cold auto-provision is predicted (`BrowserExtractTimeouts.cs:8-25`).
- **CAP-204 — Browser daemon pool (multi-slot).** `Workers/BrowserPoolManager.cs`. Pool size 1–8 (`OCCAM_BROWSER_POOL_SIZE`), base port 39217 (`OCCAM_BROWSER_POOL_BASE_PORT`, single-slot pools honor legacy `OCCAM_BROWSER_DAEMON_PORT`), idle TTL 120s, `OCCAM_BROWSER_MAX_PARALLEL` (default 2, fallback env `WT_BROWSER_MAX_PARALLEL`). Round-robin slot pick with per-slot health probe + per-slot spawn serialization to avoid an `EADDRINUSE` race on cold start (`BrowserPoolManager.cs:249-275`). Enable/disable: `OCCAM_BROWSER_DAEMON=0` or `OCCAM_BROWSER_PROFILE=isolated|parallel|throughput` forces one-shot mode (`Workers/BrowserExecutionProfile.cs`).
- **CAP-205 — Browser one-shot fallback.** `Workers/BrowserExtractRunner.cs:110-202`. Used when pool disabled, proxy rotation configured (forces one-shot always), or the pool attempt returns `null` (exception).
- **CAP-227 — Pool session recycle policy.** `workers/browser-extract/lib/browser-pool.mjs:9-10,192-198`. Recycles after 10 consecutive runs OR heap usage > 400 MB. Also recycles on `headersFile`/`storageStateFile` change (`ensureSession`, lines 26-58) and on any extract failure (`#doExtractOnce`, line 187-190) or daemon-enforced timeout (line 138-148, `hostTimeoutMs` default 115s, `daemonTimeoutMs = hostTimeoutMs - 2000`).
- **CAP-249 — Session/state isolation caveat.** The pool holds exactly **one** warm `BrowserContext` per slot, reused sequentially across unrelated URLs/hosts (queued via `#extractQueue`, `browser-pool.mjs:84-91`). Cookies added via `applySessionCookies`/`injectRecipeCookies` are NOT cleared between calls unless a recycle condition fires — a session_profile's cookies (or a recipe's injected cookies) can persist into a subsequent, unrelated extract on a different host within the same pool lifetime if none of run-count/memory/headers-change/failure triggers a recycle first. There is no per-host isolation boundary.
- **CAP-230 — Browser concurrency limiting.** `Workers/BrowserConcurrencyLimiter.cs`. Combines a global gate (`min(MaxParallel, PoolSize)`) and a per-pool-slot gate; idempotent releaser (safe double-dispose on cancellation).
- **CAP-231 — Round-robin slot selection + serialized spawn.** `BrowserPoolManager.cs:204-275` — see above.
- **CAP-232 — Idle-TTL auto-stop.** Both `HttpDaemonHost` (`OnIdleTimerTick`, lines 133-157) and `BrowserPoolManager` (`OnIdleTimerTick`, lines 355-380) poll every 15s and kill idle daemon processes past TTL.
- **CAP-228 — Node worker lifecycle recycle.** `Workers/NodeWorkerLifecycle.cs:10-51`. Per-backend-name singleton (`For("http")`/`For("browser")`) tracks consecutive successful runs; forces a recycle (kill tree + reset) after 10 consecutive runs, or **immediately** on any timeout/crash. Shared by daemon and one-shot code paths.
- **CAP-229 — Process-tree cleanup.** `Workers/WorkerProcessGroup.cs`. Windows: Job Object with `KILL_ON_JOB_CLOSE`. POSIX: `setpgid` + `kill(-pgid)` (SIGTERM, 2s grace, then SIGKILL). Global `ProcessExit`/`Ctrl+C` hook kills all tracked PIDs/PGIDs even outside the normal recycle path. Also drains long-lived daemon stdout in the background (`DrainStandardOutput`, lines 54-77) to prevent a full-pipe-buffer deadlock.

## 3. Browser launch, provisioning, and system-browser support

- **CAP-208 — System-browser / channel support.** `workers/browser-extract/lib/browser-launch-options.mjs:46-65`. `OCCAM_BROWSER_EXECUTABLE_PATH` / `OCCAM_CHROME_PATH` (explicit binary) or `OCCAM_BROWSER_CHANNEL=chrome|msedge|chrome-beta|msedge-beta|chromium`. `usesSystemBrowser()` (lines 97-108) is the single predicate gating auto-install.
- **CAP-206 — Auto-provision (branch 2): user-level chromium install.** `workers/browser-extract/lib/browser-provision.mjs:1-61`. Triggers only when `chromium.launch()` fails with a "binary missing" pattern AND `autoInstallEnabled()` (`OCCAM_BROWSER_AUTOINSTALL` != `"0"`, default on) AND NOT `usesSystemBrowser()`. Runs `npx playwright install chromium` (Windows: via `cmd /c npx`), coalesces concurrent callers into one in-flight install, logs installer chatter to `%TMP%/occam-browser-provision.log` (never to the worker's stdout/stderr — avoids corrupting the JSON contract or deadlocking on an undrained daemon stderr pipe). C# side extends the per-extract timeout to a 240s floor when this branch is predicted (`BrowserExtractBackend.cs:29-32`).
- **CAP-206b — Provision-gate single-source-of-truth probe.** `workers/browser-extract/lib/provision-gate.mjs`. A separate, pure (no-Playwright-import) Node process the C# host spawns (`Services/FeatureDiscoveryService.cs:74-142`) to ask "will the worker auto-provision?" without duplicating the JS predicate in C#. 10s timeout; on any probe failure the host **assumes `true`** (i.e., does not downgrade to HTTP) so a real failure surfaces the worker's own typed `playwright_missing` fix instead of silently changing behavior.
- **CAP-207 — `occam install-browser` CLI.** `src/FFOccamMcp.Core/Cli/OccamCliVerbs.cs:44-166`. Downloads the per-user Playwright chromium (no root). No-ops with `already_present` if a system browser/channel/executable is configured. Exit codes: 0 ready, 1 install failed, 2 worker tree not found. Emits one JSON line (`CliInstallBrowserResult`) on stdout; playwright's own progress goes to stderr. This is the exact command referenced by the worker's own `playwright_missing` failure `fix.command` (`browser-launch-options.mjs:91`).
- **CAP-246 — Playwright browser-cache path resolution.** `src/FFOccamMcp.Core/Workers/PlaywrightEnvironment.cs`. Precedence: existing `PLAYWRIGHT_BROWSERS_PATH` env (if it already has a chromium dir) → `OCCAM_PLAYWRIGHT_BROWSERS_PATH` override → OS default (`%LOCALAPPDATA%\ms-playwright` on Windows, `~/Library/Caches/ms-playwright` on macOS, `~/.cache/ms-playwright` on Linux). Applied to every spawned Node child (`ApplyTo`).
- **CAP-245 — Node executable resolution.** `src/FFOccamMcp.Core/Workers/NodeRuntime.cs`. `OCCAM_NODE_BIN` override → `<OCCAM_HOME>/bin/node` bundled binary → bare `"node"` on PATH.
- **CAP-247 — Silent-exit-13 guard.** `workers/shared/lib/worker-exit-guard.mjs`. Both `http-extract/extract.mjs` and `browser-extract/browser-extract.mjs` install this guard: if the top-level extract promise never settles and Node exits (code 13, "Unfinished Top-Level Await"), an `exit` handler synchronously emits a typed `{ok:false, failure:"timeout"}` JSON line instead of the process going silent — otherwise the host misreports a worker bug as `workers_unavailable`/"run doctor". Documented real-world trigger: an unread `undici` response body on an early-return path (e.g., a plain 404) hanging `Agent.close()`.
- **CAP-225 — Oversized-HTML guard (browser path).** `workers/browser-extract/lib/browser-session.mjs:38-61`. Caps at 900,000 chars (`BROWSER_HTML_MAX_CHARS`) to avoid feeding an oversized DOM snapshot into the markdown extractor; checked both on the initial snapshot and after the SPA "settle" re-extract.

## 4. Anti-bot, consent, and challenge handling

- **CAP-209 — "Lean-A" stealth baseline.** `browser-launch-options.mjs:27-41`. `--disable-blink-features=AutomationControlled` launch arg + an `addInitScript` that overrides `navigator.webdriver` to `false` before any page script runs. Explicitly scoped in code comments as NOT impersonation/CAPTCHA-solving/proxy-chaining.
- **CAP-210 — Challenge/CAPTCHA fail-fast wall detection ("Q-019").** `browser-session.mjs:76-105,544-570`. After initial navigation + settle, probes DOM for Cloudflare/Turnstile/hCaptcha/reCAPTCHA markers or known interstitial phrases (incl. one Russian phrase, "ддос"); fires only when combined with near-zero readable text (<200 chars) — a real page merely embedding a captcha widget is not short-circuited. Returns typed `captcha_or_challenge` immediately instead of burning the full extract budget. The router (`Routing/OccamRouter.cs:185-199`) applies a parallel/independent 2000-char-content threshold check for the same phrase list on already-extracted markdown.
- **CAP-211 — Generic consent/cookie-banner auto-dismiss.** `workers/browser-extract/lib/consent.mjs`. Site-agnostic selector list (OneTrust, Cookiebot, TrustArc, generic "accept all" patterns) tried across the main frame + all iframes, iframes prioritized by URL hints (consent/cookie/gdpr/privacy/sp_message). Non-aggressive and aggressive (extra wait + retry) modes.
- **CAP-212 — CSS-hide fallback for unresolved consent overlays.** Same file, `hideConsentOverlays`/`OVERLAY_HIDE_CSS` — injects a style tag hiding known CMP container selectors when the click-based dismiss did not fully work; also always applied to the JSDOM-parsed snapshot for the HTTP-style variant paths (`html-preprocess.mjs:49-52` `stripConsentOnly`).
- **CAP-213 — Recipe cookie injection (opt-in).** `workers/browser-extract/lib/cookie-inject.mjs`. Gated by `WT_COOKIE_INJECT=1/true/yes` AND a matching recipe with a `cookies` array; documented in code as "privacy-reviewed per domain."
- **CAP-222 — Per-host recipes.** `workers/browser-extract/lib/recipes/registry.mjs` + `developer.mozilla.org.mjs`, `nuxt.com.mjs`, `postgresql.org.mjs`, `kubernetes.io.mjs`, `docs.docker.com.mjs`, `nginx.org.mjs`. Recipes can set: `consentAggressive`, `consentSelectors`, `contentSelectors`, `domStripSelectors`, `waitSelectors`/`selectorTimeoutMs`, `articleSelectors`/`articleSelectorTimeoutMs`/`articleWaitBudgetMs`, `gotoTimeoutMs`, `waitUntil`, `postLoadWaitMs`, `contentPrefix`, `extractVariant`, `virtualScroll`, `cookies`. Rule enforced in `.cursor/rules/node-workers.mdc`: new per-host logic must go through this registry, not `if (host === …)` branches in shared code.

## 5. Rendering-quality features

- **CAP-223 — Extract variants.** `browser-session.mjs:63-70`: `baseline` (no clone/strip), `reextract` (clone, no strip), `css-hide` (default), `strip-consent` (clone, strip consent only), `strip-chrome` (clone, strip nav/footer/aside/sidebar + consent). Selected via `--extract-variant=`, a recipe's `extractVariant`, or `WT_BROWSER_EXTRACT_VARIANT`.
- **CAP-216/217/218 — Virtual scroll.** `workers/browser-extract/lib/virtual-scroll.mjs`. Modes: `append` (scroll-to-plateau, default `DEFAULT_MAX_ROUNDS=12`/`DEFAULT_STEP_PX=600`/`DEFAULT_WAIT_MS=350`), `replace` (detects a fixed-size virtualized list container, scrolls + merges unique items by key/text across rounds, re-injects a synthesized `<section id="wt-virtual-scroll-merge">` for extraction), and `auto` (heuristically decides replace-vs-append by comparing item-slot count and height stability before/after one scroll). Disable entirely via `WT_VIRTUAL_SCROLL=0`.
- **CAP-219 — Shadow DOM flattening.** `workers/browser-extract/lib/shadow-dom-flatten.mjs`. Open shadow roots only (capped at 128 hosts) are cloned into visible light-DOM siblings tagged `data-occam-shadow-flat`, both in-page (`flattenOpenShadowRoots`) and for static HTML snapshots (`flattenOpenShadowRootsInDocument`). Closed shadow roots are explicitly out of scope (code comment references a "P10-C4a heal guard").
- **CAP-220 — Playbook-driven browser interaction plan.** `workers/browser-extract/lib/interaction-steps.mjs`. `js_before_wait` (arbitrary JS eval) → `wait_for` (selector or JS predicate, 12s default timeout) → `interaction_steps[]` (`scroll`/`click`/`wait`/`type`, each independently timeout-capped, e.g. `wait` clamped to 50–60,000ms). Read from a JSON plan file (`readBrowserPlanFile`) passed via `--browser-plan-file=` or inline through the daemon `/extract` body (`browser_plan_file`).
- **CAP-224 — Playbook overlay application in the browser worker.** `browser-session.mjs:382-395` (`extractOptionsForVariant`) + `../../shared/lib/playbook-seed.mjs` (`getContentSelectorsForUrl`, `getDomStripSelectorsForUrl`, `applySeedPostMarkdown`, `isStrictPlaybookOverlay`). Strict overlays force content-selector-only extraction (no Readability fallback); soft overlays keep selectors as a preference with Readability as fallback. Delivered to the daemon per-request via `AsyncLocalStorage` (`runWithOverlay`/`wasOverlayApplied` in `browser-pool.mjs:1-2,81-91,181-185`) so the warm pool doesn't need a cold one-shot per overlay request — a one-shot-only limitation that used to force overlay requests off the fast path.
- **Consent-aware re-extract loop**: `renderAndExtract` (`browser-session.mjs:572-586`) re-extracts after a consent click settles, or applies CSS-hide + a second wait if the click failed — both before the main content-wait/virtual-scroll/DOM-strip pipeline runs.
- **Short-response settle retry**: if the first extract yields <800 chars, the worker waits for `networkidle` (8s), re-waits for article content, flattens shadow DOM again, and re-extracts once (`browser-session.mjs:613-629`) — re-applying the oversize guard (CAP-225) before the second pass.

## 6. Screenshots

- **CAP-221 — Screenshot capture (opt-in).** Only exposed on `occam_transcode`'s `capture_screenshot` parameter (`Tools/OccamTranscodeTool.cs:56`), which maps to the pipeline's `screenshot` feature flag. Captured as JPEG (`quality: 80`) and base64-encoded in `browser-session.mjs:659-672`; only fires when the browser backend actually ran (there is no HTTP-path screenshot). Failure to capture is swallowed (`console.error`, screenshot omitted) — does not fail the overall extract. `occam_digest` and other `TranscodePipeline` callers do not expose a screenshot toggle at the tool-parameter level (`DigestService`/`OccamDigestModels` were not found to pass a screenshot option through).

## 7. SSRF / network-safety on the browser path

- **CAP-226 — Browser-side SSRF guard.** Two layers in `browser-session.mjs`:
  1. Pre-navigation: `resolveAndValidateHost` on the target hostname before `page.goto` (lines 469-488), skippable via the same private-IP allowlist the HTTP worker uses.
  2. Per-request route interception (`context.route("**/*", …)`, lines 154-186): every navigation request (initial nav, 3xx redirect, meta-refresh, JS `location`, iframe nav) is re-resolved and validated at the network layer, aborting with `blockedbyclient` on a private target — because Chromium performs its own DNS resolution/redirect-following, the single pre-nav check cannot catch a redirect to an internal host.
  A `net::ERR_BLOCKED_BY_CLIENT` from this guard is mapped to the honest `private_url_blocked` failure code rather than a generic `extraction_failed` (`browser-session.mjs:704-714`).
- Proxy: browser launch reads `resolvePlaywrightProxy()` (`workers/shared/lib/egress-proxy.mjs:132-162`), an **independent implementation** of the same `OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY`/`OCCAM_NO_PROXY` contract the HTTP worker's `egressFetch` uses (lines 189-217) — two separate code paths honoring the same env vars, not shared logic. (Deep proxy-specific findings are S17's scope; noted here only for the browser-launch touchpoint.)
- **CAP-234 — Proxy rotation forces one-shot.** `HttpExtractRunner.cs:24` / `BrowserExtractRunner.cs:27` (`SkipDaemonForRotation => _proxyRotation.IsConfigured`) — when `IProxyRotationService` is configured, BOTH the HTTP daemon and the browser pool are bypassed entirely in favor of one-shot processes, presumably so each extract can carry a distinct outbound proxy/identity. This is a full-throughput/latency trade-off that is easy to miss when diagnosing "why did my browser pool stop being used."

## 8. CSS-extract worker (structured field extraction)

- **CAP-236 — `css-extract.mjs`.** `workers/css-extract/css-extract.mjs`. HTTP-only by default (`egressFetch`, 45s timeout via `AbortSignal.timeout(45_000)`). On `--browser-fallback` AND a 401/403/429 response, calls `fetchHtmlViaBrowser` (lines 111-128) which does `import("../browser-extract/lib/browser-session.mjs")` and opens its **own** `createBrowserSession()` — a fresh, non-pooled Playwright session per call, immediately closed after. This bypasses `BrowserPoolManager`, `BrowserConcurrencyLimiter`, and the daemon entirely.
- C# orchestration: `Workers/CssExtractWorker.cs` spawns a fresh `node css-extract.mjs` **process per call** (no daemon variant exists for CSS extract) and writes a temp JSON field-spec file (`occam-fields-<guid>.json`) which is best-effort-deleted in a `finally`.
- Only `occam_extract_knowledge` sets `browserFallback: true` conditionally, and only after an initial HTTP attempt returns `http_401`/`http_403`/`http_429`/`timeout`/`extraction_failed` AND the effective backend policy allows browser (`KnowledgeExtractService.cs:96-114,168-172`).

## 9. Managed extraction backends (Package 3) — CRITICAL section

**Ships in code, off by default, undocumented in `AGENTS.md`'s "always-on core MCP tools" list** (which only
names 15 tools; the managed backend is not a tool, it is a hidden router-level escalation reachable through
several of those 15 tools). This is the single biggest "local-first" claim qualifier in this subsystem.

- **CAP-238 — Managed extract backend framework.** `Backends/ManagedExtractBackend.cs:11-96`. Registered unconditionally in DI (`Composition/OccamServiceCollectionExtensions.cs:82-88`), but functionally a no-op (`IsReady=false`) unless `OCCAM_MANAGED_PROVIDER` names a registered provider AND (if that provider requires a key) `OCCAM_MANAGED_API_KEY` is set.
  - Env surface: `OCCAM_MANAGED_PROVIDER` (selects provider by name, case-insensitive), `OCCAM_MANAGED_API_KEY` (shared across whatever provider is selected — not provider-namespaced), `OCCAM_MANAGED_BASE_URL` (shared override for whichever provider is active), `OCCAM_MANAGED_DOMAINS` (comma-separated allowlist; suffix-matches subdomains; when unset, ANY host is eligible once a provider+key are configured), `OCCAM_MANAGED_TIMEOUT_MS` (default 60,000ms, clamped 1,000–180,000ms, applied to the shared `HttpClient`).
  - **CAP-243 — Per-domain opt-in.** `ManagedExtractBackend.cs:70-95` (`IsHostOptedIn`). Host must equal or be a subdomain of a listed `OCCAM_MANAGED_DOMAINS` entry; malformed/unparseable URLs are rejected (fail-closed).
  - **CAP-244 — Reachability scope.** Only invoked from `OccamRouter.TranscodeHttpThenBrowserAsync` (`Routing/OccamRouter.cs:163-175`), as the LAST step after both http and browser attempts failed to produce usable content, and only for `http_then_browser` policy — never reachable under plain `http` or `browser` policy, and never reachable from `occam_probe`, `occam_map`, `occam_extract_knowledge`, or `occam_playbook_heal` (see §0 wiring map).
  - Fetch calls are **synchronous** (`HttpClient.Send`, not `SendAsync`) inside each provider (`FirecrawlProvider.cs:34`, `JinaProvider.cs:27`, `SpiderProvider.cs:35`, `ScrapflyProvider.cs:28`) — the `ManagedExtractBackend.ExtractAsync` interface is `async` in signature only (`ValueTask.FromResult(...)`, `ManagedExtractBackend.cs:33-45`); a slow managed API blocks the calling thread for its duration. Code comment explicitly acknowledges this as an accepted trade-off given the off-by-default/last-resort/opted-in-host scope.
  - Result normalization: `Backends/Managed/ManagedResults.cs`. Backend name in the response is `managed_<provider>` (e.g. `managed_firecrawl`). Empty/whitespace markdown → `extraction_failed`; non-2xx → `http_<status>`; timeout/cancel → `timeout`; other exception → `managed_error`.
- **CAP-239 — Firecrawl provider.** `Backends/Managed/FirecrawlProvider.cs`. `POST {base}/v1/scrape` (default base `https://api.firecrawl.dev`), `Authorization: Bearer <key>`, body `{url, formats:["markdown"]}`, reads `data.markdown`. Requires API key.
- **CAP-240 — Jina Reader provider.** `Backends/Managed/JinaProvider.cs`. `GET {base}/<url>` (default base `https://r.jina.ai`), `Accept: text/markdown, text/plain`, optional Bearer key (raises rate limits only — works without a key). Response body IS the markdown (no JSON envelope).
- **CAP-241 — Spider provider.** `Backends/Managed/SpiderProvider.cs`. `POST {base}/crawl` (default `https://api.spider.cloud`), body `{url, limit:1, return_format:"markdown"}`, reads `pages[0].content`. Requires API key.
- **CAP-242 — Scrapfly provider.** `Backends/Managed/ScrapflyProvider.cs`. `GET {base}/scrape?key=…&url=…&format=markdown&render_js=true` (default base `https://api.scrapfly.io`), reads `result.content`. Requires API key. `render_js=true` is hardcoded (not configurable) — this provider is effectively "managed headless browser," so Scrapfly is the closest managed equivalent to the local browser backend, silently included in the SAME last-resort cascade slot as the non-JS providers.
- **Privacy/network implications (code-derived, not doc claims):** when configured, the full requested URL (and, for Firecrawl/Spider/Scrapfly, the URL as a request body/query parameter) is sent to a third-party API over the public internet, together with an API key. None of the four providers pass through the local egress proxy / SSRF guard (`private-ip.mjs`) that the HTTP and browser backends use — they are plain outbound `HttpClient` calls via a dedicated `occam.managed` named client (`OccamServiceCollectionExtensions.cs:82-83`) with no `SocketsHttpHandler`/`ConnectCallback` SSRF pinning applied (contrast with `HttpProbeFetcher`'s clients at lines 50-63, which do get `OutboundHttpGuard.ConnectAsync`). A private/internal URL passed to a managed provider is NOT blocked host-side before being forwarded to the third party (the provider decides what it will fetch).
- **Classification:** internal/advanced, opt-in, no MCP-tool-level documentation surface, not part of the 15-tool core registry (`AGENTS.md` §1/§7), reachable only as a hidden last-resort inside `http_then_browser`. The "no file cache by design, always live extract, local-first" framing in `AGENTS.md` is accurate for the DEFAULT configuration but requires this qualifier once `OCCAM_MANAGED_PROVIDER` is set: at that point, `occam_transcode`/`occam_digest`/etc. can silently egress to a named third party as a final fallback with no MCP-response field that names "managed provider used" beyond the ordinary `backend: "managed_<provider>"` string (there is no separate boolean flag equivalent to `browserProvisioned` for "managed backend used").

## 10. Architecture-hygiene findings (dead/orphaned code)

- **CAP-248a — `IWorkerProcessSpawner`/`NodeWorkerProcessSpawner` is registered in DI (`OccamServiceCollectionExtensions.cs:30`) but never injected or called anywhere else in the codebase** (confirmed via full-repo grep — only its own file, its interface file, and the DI registration reference the type). `HttpExtractRunner`/`BrowserExtractRunner` reimplement the identical spawn+capture+map logic inline instead of using this abstraction. Dead code / duplicated logic risk (a bugfix applied to one of the three copies — `NodeWorkerProcessSpawner`, `HttpExtractRunner`, `BrowserExtractRunner` — will not propagate to the other two).
- **CAP-248b — `BrowserConcurrencyGate` (`Workers/BrowserConcurrencyGate.cs`) is a second, independent concurrency-gate implementation.** Only its `.MaxParallel` static property is read (`BrowserExtractTimeouts.cs:33`); its actual `Run<T>(...)` gating method is never called anywhere in the codebase (confirmed via grep) — the real gating happens through `BrowserConcurrencyLimiter` (CAP-230) instead. Both read the same `OCCAM_BROWSER_MAX_PARALLEL` env var independently, so they cannot drift in the value, but the `Run<T>` method itself is inert dead code.

---

## Summary table (CAP ID → one-line capability)

| CAP | Capability |
|---|---|
| 200 | HTTP extract backend (35s timeout) |
| 201 | HTTP daemon (long-lived, port 39218 default) |
| 202 | HTTP one-shot fallback |
| 203 | Browser extract backend (Playwright) |
| 204 | Browser daemon pool (1–8 slots) |
| 205 | Browser one-shot fallback |
| 206 | Auto-provision user-level chromium (branch 2) + provision-gate probe |
| 207 | `occam install-browser` CLI |
| 208 | System-browser/channel support (chrome/msedge/executablePath) |
| 209 | Lean-A stealth (navigator.webdriver mask + AutomationControlled) |
| 210 | Challenge/CAPTCHA fail-fast wall detection |
| 211 | Generic consent/cookie-banner auto-dismiss |
| 212 | CSS-hide fallback for unresolved consent |
| 213 | Recipe cookie injection (opt-in `WT_COOKIE_INJECT`) |
| 214 | Session cookie/header injection into browser context |
| 215 | Storage-state (full session) load into browser context |
| 216 | Virtual scroll — append |
| 217 | Virtual scroll — replace/merge |
| 218 | Virtual scroll — auto-detect |
| 219 | Shadow DOM flattening (open roots) |
| 220 | Playbook browser interaction plan (js/wait/click/scroll/type) |
| 221 | Screenshot capture (transcode-only opt-in) |
| 222 | Per-host recipes registry |
| 223 | Extract variants (baseline/reextract/css-hide/strip-consent/strip-chrome) |
| 224 | Playbook overlay in browser worker (strict/soft) + ALS per-request delivery |
| 225 | Oversized-HTML guard (900K chars) |
| 226 | Browser-side SSRF guard (pre-nav + per-navigation-request) |
| 227 | Pool session recycle (10 runs / 400MB / header change / failure) |
| 228 | Node worker lifecycle recycle (10 runs or immediate on timeout/crash) |
| 229 | Process-tree cleanup (Job Object / setpgid+kill) |
| 230 | Browser concurrency limiting (global + pool gate) |
| 231 | Round-robin slot selection + serialized spawn |
| 232 | Idle-TTL auto-stop (http + browser daemons) |
| 233 | Dual independent proxy implementations (http egress vs Playwright) |
| 234 | Proxy rotation forces one-shot (bypasses both daemons) |
| 235 | DOM skeleton capture (playbook heal, pool-aware) |
| 236 | CSS-extract worker + isolated non-pooled browser fallback |
| 237 | PDF extraction (HTTP path, text-layer only, no OCR) |
| 238 | Managed extract backend framework (Package 3, off by default) |
| 239 | Firecrawl managed provider |
| 240 | Jina Reader managed provider |
| 241 | Spider managed provider |
| 242 | Scrapfly managed provider (render_js hardcoded true) |
| 243 | Managed per-domain opt-in allowlist |
| 244 | Managed backend reachability scope (http_then_browser last resort only) |
| 245 | Node executable resolution |
| 246 | Playwright browser-cache path resolution |
| 247 | Silent-exit-13 guard (both http + browser one-shot workers) |
| 248 | Dead/orphaned abstractions: `IWorkerProcessSpawner`, `BrowserConcurrencyGate.Run<T>` |
| 249 | Session/state isolation caveat (single warm context, no per-host boundary) |

**Total capabilities documented: 50 (CAP-200 … CAP-249).**

---

## Files inspected (primary evidence)

C#: `Backends/HttpExtractBackend.cs`, `Backends/BrowserExtractBackend.cs`, `Backends/ManagedExtractBackend.cs`,
`Backends/IManagedExtractBackend.cs`, `Backends/Managed/{IManagedProvider,ManagedResults,FirecrawlProvider,JinaProvider,SpiderProvider,ScrapflyProvider}.cs`,
`Routing/{OccamRouter,TranscodePipeline}.cs`, `Composition/OccamServiceCollectionExtensions.cs`,
`Workers/{WorkerPaths,HttpExtractRunner,BrowserExtractRunner,HttpDaemonHost,BrowserDaemonHost,BrowserPoolManager,
BrowserPoolSettings,BrowserExecutionProfile,BrowserExtractTimeouts,BrowserConcurrencyLimiter,BrowserConcurrencyGate,
NodeWorkerLifecycle,NodeWorkerProcessSpawner,WorkerProcessGroup,NodeRuntime,PlaywrightEnvironment,CssExtractWorker,
DomSkeletonWorker}.cs`, `Services/{DigestService,MapService,KnowledgeExtractService,FeatureDiscoveryService}.cs`,
`Playbooks/{PlaybookHealService,PlaybookSaveVerifier}.cs`, `Tools/OccamTranscodeTool.cs`, `Cli/OccamCliVerbs.cs`.

JS: `workers/http-extract/{extract.mjs,http-daemon.mjs,lib/http-extract-run.mjs}`,
`workers/browser-extract/{browser-extract.mjs,browser-daemon.mjs,dom-skeleton-capture.mjs,
lib/{browser-extract-run,browser-session,browser-launch-options,browser-provision,provision-gate,
browser-pool,consent,cookie-inject,session-headers,virtual-scroll,shadow-dom-flatten,interaction-steps,
extract-html,html-preprocess,ensure-chromium-usable,verify-browser-launch,dom-skeleton}.mjs,
lib/recipes/registry.mjs}`, `workers/css-extract/css-extract.mjs`,
`workers/shared/lib/{egress-proxy,worker-exit-guard,playbook-seed,request-headers,default-fetch-headers,
private-ip}.mjs` (private-ip read for SSRF cross-reference only — deep network/SSRF audit is S17 scope).

**Files inspected count: 47** (28 C#, 19 JS/mjs).

## Unresolved / needs second pass

- Did not exhaustively trace `Watch/WatchService.cs`, `Consensus/ConsensusService.cs`, `Claims/ClaimCheckService.cs`,
  `Dataset/DatasetExportService.cs`, `Batch/BatchJobProcessor.cs` line-by-line — confirmed only that they construct/consume
  `TranscodePipeline` (grep-level), not their individual backend-policy defaults or whether any override to `http`-only.
- `OccamDigestTool`/`OccamDigestModels` were not opened to confirm the absence of a screenshot toggle beyond a targeted
  grep; stated as "not found" rather than "confirmed absent."
- Did not open `dom-skeleton.mjs`, `dom-skeleton-capture.mjs` bodies in full (only referenced via `DomSkeletonWorker.cs`
  wiring) — sufficient for the wiring-map claim in §0 but not for a full feature inventory of skeleton capture itself.
- Did not verify runtime behavior (no gate run) — this is a static code-reading audit only, per task scope.
