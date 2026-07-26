# Subsystem audit: Network / Fetch / Proxy / Cookies / Sessions / SSRF / Headers

**Agent:** Wave 1 subagent S17
**Scope:** CAP-150 .. CAP-199 (this file only)
**Method:** Direct inspection of shipped runtime code (C# `src/FFOccamMcp.Core/**`, Node
`workers/**`, operator scripts `scripts/**`). Docs (`docs/`, `AGENTS.md`, `CLAUDE.md`,
`MCP_API_SPEC.md`) were **not** used as a source of truth — every claim below is cited to a
file+line-range in the current tree. Where doc names diverge from what the code actually does,
the code name is used and the mismatch is called out explicitly.

---

## 0. Files inspected (primary evidence)

C# (`src/FFOccamMcp.Core/`):
- `Session/SessionProfileHeaders.cs`, `Session/OccamFetchDefaults.cs`, `Session/RequestHeadersMerger.cs`,
  `Session/FetchHeadersScope.cs`, `Session/FetchPreflight.cs`
- `Routing/PrivacyClassifier.cs`, `Routing/OutboundHttpGuard.cs`, `Routing/FailureCodeStrings.cs`,
  `Routing/ChallengePageDetector.cs`, `Routing/FailureRanking.cs`
- `Backends/HttpExtractBackend.cs`, `Backends/BrowserExtractBackend.cs`, `Backends/ManagedExtractBackend.cs`,
  `Backends/Managed/*.cs` (Jina/Firecrawl/Scrapfly/Spider providers)
- `Workers/EgressProxyConfig.cs`, `Workers/HttpExtractRunner.cs`, `Workers/BrowserExtractRunner.cs`,
  `Workers/HttpDaemonHost.cs`, `Workers/BrowserPoolManager.cs`, `Workers/CssExtractWorker.cs`,
  `Workers/DomSkeletonWorker.cs`
- `Services/ProxyRotationSettings.cs`, `Services/IProxyRotationService.cs`,
  `Services/RoundRobinProxyRotationService.cs`, `Services/ProxyListParser.cs`,
  `Services/RobotsThrottleService.cs`
- `Access/AccessClassifier.cs`, `Access/AccessModels.cs`, `Access/AccessEvidenceAdapters.cs`
- `PostProcessors/ChallengePagePostProcessor.cs`, `PostProcessors/RequiresLoginPostProcessor.cs`
- `Probe/HttpProbeFetcher.cs`, `Probe/HttpRedirectFollower.cs`, `Probe/ProbeHttpHeaders.cs`
- `Tools/OccamProbeTool.cs` + grep across `Tools/*.cs` for `session_profile` wiring
- `Composition/OccamServiceCollectionExtensions.cs` (DI wiring / `HttpClient` registrations)
- `Portable/OccamUserPaths.cs`

Node (`workers/`):
- `shared/lib/egress-proxy.mjs` + `egress-proxy.selftest.mjs`
- `shared/lib/private-ip.mjs` + `private-ip.selftest.mjs`
- `shared/lib/request-headers.mjs` + `request-headers.selftest.mjs`
- `shared/lib/default-fetch-headers.mjs`, `shared/lib/access-evidence.mjs`
- `http-extract/extract.mjs`, `http-extract/lib/http-extract-run.mjs`
- `browser-extract/lib/cookie-inject.mjs`, `browser-extract/lib/browser-session.mjs`,
  `browser-extract/lib/session-headers.mjs`, `browser-extract/lib/consent.mjs`,
  `browser-extract/lib/browser-launch-options.mjs`, `browser-extract/lib/browser-challenge-detect.selftest.mjs`
- `css-extract/css-extract.mjs`

Scripts:
- `scripts/occam-session.mjs`, `scripts/lib/occam-sessions-lib.mjs`, `scripts/lib/occam-session-export-state.mjs`

**Files inspected count: 46** (41 read in full or in relevant part, 5 confirmed present/absent via grep-only
and cited as such below).

---

## 1. SSRF / private-network blocking

### CAP-150 — Literal pre-check blocks private/local hosts before any dispatch (Core)
`Routing/PrivacyClassifier.cs:1-91`, `Session/FetchPreflight.cs:21-33`. `PrivacyClassifier.Classify(url)`
rejects `localhost`, `*.local`, `*.internal`, and any literal IP matching `IsPrivateIp` (see CAP-155)
before `FetchPreflight.Prepare` proceeds to build headers/session scope. Toggle:
`OCCAM_ALLOW_PRIVATE_URLS=1` (`IsPrivateUrlBlocked()`, `PrivacyClassifier.cs:5-9`).
**Status: PROVEN.**

### CAP-151 — DNS-rebinding-safe SSRF guard, HTTP worker
`workers/shared/lib/private-ip.mjs:117-224`. `resolveAndValidateHost(hostname)` resolves **both**
address families (`dns.lookup(..., { all: true, family: 0 })`), rejects any private answer, and
`createPinnedDispatcher`/`createPinnedLookup` build an undici `Agent` whose `connect.lookup` is
pinned to exactly the validated addresses — closing the classic "resolve-then-refetch" TOCTOU
window. Wired at the call site: `workers/http-extract/lib/http-extract-run.mjs:147-163` (initial
fetch) and again per redirect hop, see CAP-152. **Status: PROVEN**, unit-tested in
`private-ip.selftest.mjs:1-60` (dual-stack ranges, pin behavior, host-aware redirect re-validation).

### CAP-152 — Per-hop SSRF re-validation on `<meta refresh>` redirects (HTTP worker)
`workers/http-extract/lib/http-extract-run.mjs:232-278`. A `<meta refresh>` target parsed from the
fetched HTML is an application-level redirect that bypasses undici's own guarded 3xx path; the code
explicitly re-resolves + re-pins + re-validates that target host via `pinnedDispatcherForUrl` before
following it (max 3 hops), and calls `validateFinalUrl` again on the new final URL.
**Status: PROVEN.**

### CAP-153 — SSRF guard, browser worker (every navigation, not just the first)
`workers/browser-extract/lib/browser-session.mjs:172-186` (per-request `page.route("**/*")`
interceptor calling `resolveAndValidateHost` on every `isNavigationRequest()`, aborting with
`blockedbyclient` on failure) + `browser-session.mjs:469-488` (pre-goto check) +
`browser-session.mjs:644-651` (post-render final-URL check via `validateFinalUrlInBrowser`).
Comment at line 173 explicitly states the reason: "Chromium resolves and follows redirects itself,
so the pre-navigation host check … can't stop a redirect/JS navigation to an internal host via a
DNS-resolving name" — i.e. this is deliberate defense-in-depth against exactly the gap a naive
implementation would have. **Status: PROVEN.**

### CAP-154 — SSRF guard on Core's own direct `HttpClient` egress (non-worker fetches)
`Routing/OutboundHttpGuard.cs:1-89`, wired as `SocketsHttpHandler.ConnectCallback` in
`Composition/OccamServiceCollectionExtensions.cs:50-63` (probe's two clients:
`RedirectTrackingClientName`, `AutoRedirectClientName`), `:64-70` (`playbook.wellKnownGenome`),
`:73-79` (`receipts.timeAnchor`), `:95-101` (`occam.robots`). Resolves the target host (both
families via `Dns.GetHostAddressesAsync`), rejects private answers unless
`OCCAM_ALLOW_PRIVATE_URLS=1`, and connects a raw `Socket` pinned to the validated addresses
(`ConnectAsync`, lines 62-80). This is a **separate implementation** from the Node-side guard (C#
vs JS) but enforces the identical policy — confirms probe/genome/robots/time-anchor fetches (which
do **not** go through a Node worker) are not an SSRF hole. **Status: PROVEN.**

### CAP-155 — Matching dual-stack private-range definitions (C# vs JS)
`Routing/PrivacyClassifier.cs:55-91` and `workers/shared/lib/private-ip.mjs:11-85` independently
implement the same range set: IPv4 `0.0.0.0/8` ("this host", routes to localhost on Linux —
explicitly called an SSRF vector in both comments), `10/8`, `172.16/12`, `192.168/16`, `127/8`,
`169.254/16`; IPv6 `::1`, `fe80::/10`, `fec0::/10` (deprecated site-local), `fc00::/7`
(unique-local, first byte 0xFC/0xFD), plus IPv4-mapped-IPv6 folding for both the dotted
(`::ffff:a.b.c.d`) and hex (`::ffff:AABB:CCDD`) forms so a mapped loopback/link-local address can't
slip past the v6-only checks. **Status: PROVEN** — unit-tested for every branch in
`private-ip.selftest.mjs:15-36`.

### CAP-156 — `OCCAM_ALLOW_PRIVATE_URLS` only relaxes rejection, never disables resolution/pinning
`workers/shared/lib/private-ip.mjs:140-151` (comment + `allowPrivate` param flows into
`resolveAndValidateHost` but the `for` loop that throws is the *only* thing skipped — resolution and
pinning still run) and `browser-session.mjs:178,473` (`shouldSkipPrivateIpCheck()` still calls
`resolveAndValidateHost`, just doesn't throw on a private hit... actually for the browser route
guard, when the flag is set the check is skipped entirely at that call site — see nuance below).
**Nuance:** in the *browser* worker the flag actually skips the per-navigation route-guard call
entirely (`if (!shouldSkipPrivateIpCheck() && ...)`, line 178), whereas in the *HTTP* worker the
flag only relaxes the private-IP throw inside `resolveAndValidateHost` but resolution+pinning always
run. So the "always resolves, only relaxes rejection" guarantee is proven for the HTTP path and the
Core `OutboundHttpGuard` path, but for the **browser** path the flag skips the whole guard function
call, not just the rejection. **Status: PROVEN with a documented asymmetry between HTTP-worker and
browser-worker behavior under the escape hatch** (only matters for local/dev testing since the flag
defaults off).

---

## 2. HTTP / SOCKS proxy support

### CAP-157 — Static HTTP/HTTPS/SOCKS5 proxy for Node worker egress
`workers/shared/lib/egress-proxy.mjs:1-27,105-129,189-217`. Env: `OCCAM_HTTP_PROXY`,
`OCCAM_HTTPS_PROXY` (falls back to `OCCAM_HTTP_PROXY` if unset), `OCCAM_NO_PROXY`. `egressFetch()`
resolves the effective proxy for the target URL and dispatches via `undici`'s `ProxyAgent`; when no
proxy applies it falls through to plain `fetch()`. Allowed proxy protocols: `http:`, `https:`,
`socks5:` (`ALLOWED_PROXY_PROTOCOLS`, line 5). Used by `http-extract-run.mjs` (all HTML/PDF
fetches) and `css-extract.mjs:3,39` (confirmed via grep — CSS worker fetch also proxy-aware).
**Status: PROVEN**, unit-tested (`egress-proxy.selftest.mjs`).

### CAP-158 — Proxy URL validation + typed failure + connect-failure reclassification
`egress-proxy.mjs:29-49` (`validateProxyUrl` — rejects non-http/https/socks5 schemes and missing
host, returns `EgressProxyError("invalid_proxy_url")`) and `:189-217` (`egressFetch` catch block
reclassifies connect failures matching
`/ECONNREFUSED|ENOTFOUND|ETIMEDOUT|proxy|fetch failed|502|Bad Gateway/i` as
`EgressProxyError("proxy_unreachable")` instead of leaking a raw network error). Mirrored in C#:
`Workers/EgressProxyConfig.cs:56-69` (`IsValidProxyUrl`). **Status: PROVEN.**

### CAP-159 — `NO_PROXY` bypass matching (exact / suffix / wildcard / global)
`egress-proxy.mjs:55-84` (`shouldBypassProxy`) supports exact host match, `.suffix` (dot-prefixed
rule matching suffix or exact-minus-dot), `*.suffix` wildcard, and bare `*` (bypass everything).
**Status: PROVEN**, unit-tested (`egress-proxy.selftest.mjs:16-18`).

### CAP-160 — Playwright (browser backend) proxy support, incl. embedded credentials
`egress-proxy.mjs:131-162` (`resolvePlaywrightProxy`) maps the same `OCCAM_HTTPS_PROXY` /
`OCCAM_HTTP_PROXY` env to a Playwright `LaunchOptions.proxy` object, including decoded
`username`/`password` extracted from the proxy URL's userinfo, and a `bypass` string built from
`OCCAM_NO_PROXY`. Wired at `workers/browser-extract/lib/browser-session.mjs:113-117`
(`createBrowserSession` sets `launchOptions.proxy = proxy` when configured). **Status: PROVEN**,
unit-tested (`egress-proxy.selftest.mjs:41-44`).

### CAP-161 — Proxy credential redaction (both languages)
`egress-proxy.mjs:167-183` (`redactProxyUrl`, JS) and `Workers/EgressProxyConfig.cs:71-95`
(`RedactCredentials`, C#) both mask `user:pass@` as `***:***@` before a proxy URL could reach a log
line. **Status: PROVEN** (JS side unit-tested at `egress-proxy.selftest.mjs:28-29`; C# side has no
dedicated selftest found — **UNRESOLVED: no gate coverage located for `RedactCredentials`**, though
the implementation is straightforward `UriBuilder` masking).

### CAP-162 — Proxy rotation pool (round-robin, per-spawn)
`Services/ProxyRotationSettings.cs` (env keys `OCCAM_PROXY_LIST`, `OCCAM_PROXY_LIST_FILE`),
`Services/RoundRobinProxyRotationService.cs:1-34` (interlocked round-robin index over a static
array), registered as the singleton `IProxyRotationService`
(`Composition/OccamServiceCollectionExtensions.cs:115`). `Workers/EgressProxyConfig.ApplyForSpawn`
(lines 35-54) acquires the next proxy and sets **both** `HTTP_PROXY`/`HTTPS_PROXY` process-env vars
for that one worker spawn to the same rotated URL, falling back to the static env-based settings
when the pool is empty. **Status: PROVEN** — this is a genuine capability beyond a single static
proxy, i.e. **HIDDEN_ADVANCED** relative to what a "proxy support: yes/no" doc line would suggest.

### CAP-163 — Proxy list ingestion: URL-per-line **and** scraper-CSV export format
`Services/ProxyListParser.cs:1-199`. Accepts either newline/comma/semicolon-delimited proxy URLs
(inline `OCCAM_PROXY_LIST`) or a file (`OCCAM_PROXY_LIST_FILE`) that can be plain URL-per-line
(`#`-comments skipped) **or** a CSV with an `ip,port,protocols` header row (auto-detected via
`LooksLikeCsvHeader`, line 85-96) — the kind of export common free-proxy-scraper tools produce.
`BuildProxyUrl` (lines 145-170) maps `http`/`https`/`socks5` protocol column values to a URL and
explicitly rejects `socks4` (`prefix = null` for that case, line 160). Every candidate URL is
re-validated through `EgressProxyConfig.IsValidProxyUrl` regardless of source. **Status: PROVEN.**

### CAP-164 — Proxy rotation forces one-shot spawns, bypassing the daemon/pool
`Workers/HttpExtractRunner.cs:24,44-47` (`SkipDaemonForRotation => _proxyRotation.IsConfigured`;
daemon path skipped when true) and `Workers/BrowserExtractRunner.cs:27,50-51` (same pattern for the
browser pool). This is a real, deliberate trade-off documented only in code comments
(`BrowserExtractRunner.cs:49`: "Proxy rotation still forces one-shot") — every extract runs a fresh
Node process when a proxy pool is configured, so each call can legitimately get a distinct egress
IP, at the cost of the daemon's/pool's warm-process speed. **Status: PROVEN**, and this is exactly
the kind of "known historical blind spot" this audit was asked to surface —
**HIDDEN_ADVANCED**.

### CAP-165 — ABSENT: rotation does not reach the persistent daemon / pool / CSS / dom-skeleton spawns
Grep across `src/` for `EgressProxyConfig.` shows four call sites using the **non-rotating**
`ApplyTo(psi)` overload instead of `ApplyForSpawn`: `Workers/HttpDaemonHost.cs:78`,
`Workers/BrowserPoolManager.cs:313`, `Workers/CssExtractWorker.cs:64`,
`Workers/DomSkeletonWorker.cs:95`. Only `HttpExtractRunner.cs:88` and
`BrowserExtractRunner.cs:132` call `ApplyForSpawn` (rotation-aware). Net effect: if the HTTP daemon
or browser pool is already warm and a proxy pool is *also* configured, `SkipDaemonForRotation`
(CAP-164) ensures the daemon/pool path is never taken for `occam_transcode`/`occam_digest` calls —
so in practice CAP-165 is not reachable via the primary extract tools today. It **is** reachable for
the CSS-extract and DOM-skeleton workers (used by `occam_playbook_heal`/genome tooling), which spawn
independently of the rotation-skip logic and always get the **static** (non-rotated) proxy env only.
**Status: PROVEN ABSENT for CssExtractWorker/DomSkeletonWorker/daemon/pool spawn paths specifically.**

### CAP-166 — ABSENT: Core's own C# `HttpClient`s never honor `OCCAM_HTTP_PROXY`/`HTTPS_PROXY`
Every `SocketsHttpHandler` registered in `Composition/OccamServiceCollectionExtensions.cs:50-101`
sets `ConnectCallback` (SSRF guard) but **none** sets `.Proxy` or reads any `OCCAM_*PROXY*` var.
`Workers/EgressProxyConfig.cs` is explicitly scoped ("Compiles OCCAM_* proxy env for worker spawns
only — Core never performs proxied HTTP.", line 7) — this is a comment in the code itself, not a
doc claim, confirming the boundary is intentional. Consequence: `occam_probe`, robots.txt fetches,
`/.well-known` genome fetches, the receipts time-anchor POST, and the managed-provider backends
(Jina/Firecrawl/Scrapfly/Spider — see CAP-194) **all bypass any configured egress proxy** and go out
on the host's direct network path. **Status: PROVEN ABSENT** — this is the single most
audit-relevant gap for anyone assuming "proxy support" is global.

---

## 3. Cookies / session profiles / custom headers

### CAP-167 — Local session-profile files (`OCCAM_SESSIONS_ROOT/<id>.json`)
`Session/SessionProfileHeaders.cs:1-239`. Default root: `OccamUserPaths.ResolveUserDataRoot()` +
`"sessions"` (line 128) unless `OCCAM_SESSIONS_ROOT` is set (line 122-126). `id` is sanitized to
`[A-Za-z0-9._-]` with explicit `..`/`/`/`\\` rejection (`ContainsPathTraversal`, `IsAllowedId`, lines
131-149) before ever touching the filesystem — `invalid_session_profile` / `session_profile_not_found`
are the two typed failures (`SessionProfileStatus`, lines 6-11, `FailureCode`, lines 24-29). Header
parsing (`ParseHeadersObject`, lines 159-191) drops non-string values and two classes of dangerous
names: `ReservedKeys` (`storageState`, `_occam` — metadata, not real headers) and `BlockedNames`
(`Host`, `Content-Length`, `Content-Type`, `Transfer-Encoding`, `Connection`, `Expect`, `Upgrade` —
headers a profile must not be able to forge). **Status: PROVEN.**

### CAP-168 — `storageState` path resolution with containment check
`SessionProfileHeaders.cs:193-238`. A profile's `storageState` value (relative, absolute, or
`~`-prefixed) is resolved via `ResolveStorageStatePath` and then required to be **inside** the
sessions root (`full.StartsWith(sessionsRoot, ...)`, lines 229-235) before being trusted — an
absolute-path or `..`-escape attempt in a profile's `storageState` field is rejected (function
returns `null`), which `TryResolveStorageStatePath` turns into `SessionProfileStatus.NotFound`
(lines 195-217) rather than silently reading an arbitrary file. **Status: PROVEN.**

### CAP-169 — Global custom headers file merged under session-profile precedence
`Session/RequestHeadersMerger.cs:1-64`. `OCCAM_REQUEST_HEADERS_FILE` (JSON object) is read once per
call (`ReadEnvHeaders`) and merged with the active `session_profile`'s headers — on key collision the
**session profile wins** (`Merge`, lines 10-33, loop at 27-30 overwrites env values). Malformed/
missing file → empty dict, never throws (lines 43-56). **Status: PROVEN.**

### CAP-170 — `FetchHeadersScope`: ephemeral per-call header/storageState handoff to workers
`Session/FetchHeadersScope.cs:1-125`. Merged headers are written to a **temp JSON file**
(`Path.GetTempPath()/occam-headers-{guid}.json`) referenced through two `AsyncLocal<string?>`
fields (`CurrentPath`, `CurrentStorageStatePath`) so the ambient scope flows through the async
call chain to whichever backend runs (`Backends/HttpExtractBackend.cs:22-23`,
`Backends/BrowserExtractBackend.cs:22-23` both read `FetchHeadersScope.ActivePath` /
`ActiveStorageStatePath`). On `Dispose()` the temp file is deleted with a 3-attempt retry
(`TryDeleteTempFile`, lines 62-94) and, if still locked, a fire-and-forget background retry loop for
up to ~2s (`ScheduleBackgroundDelete`, lines 96-120). The class comment and the cleanup-failure log
line (line 6, lines 50-53) explicitly guarantee header **values** are never logged — only the temp
filename is. **Status: PROVEN.**

### CAP-171 — Cookie-header → Playwright cookie injection (session profile path)
`workers/shared/lib/request-headers.mjs:102-138` (`parseCookieHeader` — splits a `Cookie:` header
into individual `{name, value, domain, path, secure, sameSite:"Lax"}` objects derived from the
target page URL) and `workers/browser-extract/lib/session-headers.mjs:21-40`
(`applySessionCookies` calls `context.addCookies(cookies)` before navigation, reporting
`cookiesAdded`). Invoked from `browser-session.mjs:459-463` inside `renderAndExtract`.
**Status: PROVEN.**

### CAP-172 — Cross-origin credential stripping on redirect (HTTP worker)
`workers/shared/lib/request-headers.mjs:35-69` (`stripCrossOriginSensitiveHeaders`) strips
`Cookie`, `Authorization`, `Proxy-Authorization` (case-insensitively) whenever the redirect target's
**origin** (scheme+host+port) differs from the source — including a scheme-downgrade
(`https→http` same host) or port-only change, and fails safe (strips) on an unparseable target URL.
Applied specifically on the `<meta refresh>` hop in `http-extract-run.mjs:255-262`. **Status:
PROVEN**, unit-tested exhaustively (`request-headers.selftest.mjs:17-54`: same-origin keep,
cross-host strip, scheme-downgrade strip, port-change strip, case-insensitive strip, unparseable
fail-safe).

### CAP-173 — Browser-path credential isolation for `extraHTTPHeaders`
`request-headers.mjs:71-100` (`pickExtraHttpHeaders` / `BLOCKED_EXTRA`) explicitly excludes
`cookie`, `authorization`, `proxy-authorization` (plus the usual hop-by-hop/forbidden header names
and `user-agent`, which is set separately) from what gets passed into Playwright's
`extraHTTPHeaders`. Comment (lines 73-79) explains why: Playwright's `extraHTTPHeaders` are static
per-**context** and attach to every request including cross-origin subresources/redirects with no
origin filter, so putting `Authorization`/`Cookie` there would leak a `session_profile`'s
credentials to third-party hosts; `Cookie` is instead re-injected origin-scoped via
`context.addCookies` (CAP-171). **Status: PROVEN**, unit-tested (`request-headers.selftest.mjs`
tail, F1 section — confirmed present via file read, lines 56-60+).

### CAP-174 — `occam-session.mjs` operator CLI (init / list / import / export-state)
`scripts/occam-session.mjs:1-240`. **Not an MCP tool** — a standalone Node CLI for local session
management. `init` scaffolds `~/.occam/sessions/` (or `OCCAM_SESSIONS_ROOT`) with `_imports/`,
`states/`, a README, and a `.gitignore` (see CAP-193). `list` enumerates profile ids + header
**key names only** (never values, `cmdList`, lines 74-94). **Status: PROVEN.**

### CAP-175 — Netscape `cookies.txt` import with risk warnings
`occam-session.mjs:96-194` (`cmdImport`) + `scripts/lib/occam-sessions-lib.mjs:29-72`
(`parseNetscapeCookies`, filters by `--host` or takes `--all`, skips expired entries, computes
byte size). Emits explicit operator-facing warnings (lines 144-157): Cookie header >8KB may exceed
server limits (suggests `export-state` instead), `--all` produces a multi-site Cookie sent on
*every* request (workers don't domain-filter cookies the way real browsers do), and presence of
`cf_clearance=` may still 403 on the HTTP backend (suggests `export-state` + browser). **Status:
PROVEN.**

### CAP-176 — `export-state`: headed-browser login capture → Playwright storageState
`scripts/lib/occam-session-export-state.mjs:1-116`. Launches a **headed** (non-headless) Chromium
via the same `resolveBrowserLaunchOptions()` used in production, navigates to the operator-given
URL, waits for a manual Enter keypress (operator logs in / passes Cloudflare / accepts cookies by
hand), then calls `context.storageState({ path })` and writes a companion session-profile JSON
pointing `storageState` at the saved file. This is the documented-in-code (not just in docs) answer
for CAPTCHA/Cloudflare-gated sites — Occam **does not** solve challenges itself; a human does, once,
and the resulting state is replayed. **Status: PROVEN.**

### CAP-177 — Recipe-based cookie injection (separate from `session_profile`)
`workers/browser-extract/lib/cookie-inject.mjs:1-18`. Gated by `WT_COOKIE_INJECT=1|true|yes`
(off by default), sources cookies from a per-site **playbook recipe**'s `cookies` array (not from
`session_profile`), injected via `context.addCookies` before navigation
(`browser-session.mjs:456-458`). Distinct mechanism from CAP-171 — two independent cookie-injection
paths exist in the browser worker. **Status: PROVEN.**

---

## 4. Consent / anti-bot / challenge / access classification

### CAP-178 — Generic (non-per-site) consent/cookie-banner dismissal
`workers/browser-extract/lib/consent.mjs:1-142`. A prioritized, **site-agnostic** CSS-selector list
(OneTrust, Cookiebot, TrustArc, generic `accept`/`agree`/`allow all` button patterns) tried across
the main page and all iframes (frames whose URL hints at `consent|cookie|gdpr|privacy|sp_message`
are tried first, `prioritizeFrames`, lines 132-141), with a role-based `getByRole("button", {name:
/accept|agree|allow all|got it/i})` fallback, and a CSS "hide the known CMP containers" fallback
layer (`hideConsentOverlays`) when click-based dismissal doesn't fully work. **Status: PROVEN.**

### CAP-179 — Two independent anti-bot/challenge detectors
1. **Live in-browser fail-fast** (`browser-session.mjs:88-105,540-570`, `isChallengeWall`): a pure
   function over a lightweight DOM probe (title, first-400-char lowercase body sample, a boolean
   "known challenge-widget node present" check for Cloudflare Turnstile / hCaptcha / reCAPTCHA
   selectors) — flags a **wall** only when a marker is present **and** `textLen < 200`, so a real
   page that merely embeds a captcha widget in a signup form (lots of prose) is not false-flagged.
   Unit-tested exhaustively (`browser-challenge-detect.selftest.mjs:1-40`: CF interstitial, phrase-only
   wall, real-content-with-widget non-wall, normal-article non-wall, exact-boundary non-wall, null/empty
   probe fail-open).
2. **Post-hoc, backend-agnostic** (`PostProcessors/ChallengePagePostProcessor.cs:1-40`,
   `Routing/ChallengePageDetector.cs`): runs on the **compiled markdown** from either backend, but
   only when that markdown is ≤2000 chars (`ChallengeMaxMarkdownChars`) — explicitly so an article
   that merely *mentions* Cloudflare/captcha/rate-limiting isn't false-flagged (Q-026 comment).
**Status: PROVEN**, and the dual live+post-hoc design plus the exhaustive false-positive guarding is
a meaningfully more sophisticated approach than a single keyword check — **HIDDEN_ADVANCED**.

### CAP-180 — Browser stealth baseline (explicitly scoped, not full anti-detect)
`workers/browser-extract/lib/browser-launch-options.mjs:1-65`. `navigator.webdriver` is overridden
to `false` via an `addInitScript` run before any page script (`STEALTH_INIT_SCRIPT`, lines 36-41),
plus the Chromium launch arg `--disable-blink-features=AutomationControlled` (`STEALTH_ARGS`, line
27-29). The file's own header comment (lines 6-11) is explicit about the boundary: "✓ hide the
navigator.webdriver flag … ✗ no CAPTCHA solving, no identity rotation, no proxy chaining (opt-in
escalation)" — i.e. there is **no** fingerprint/canvas/WebGL randomization or multi-identity
rotation capability in the shipped runtime; stealth is limited to these two measures.
**Status: PROVEN, with an explicit code-documented capability boundary** (useful to cite verbatim
when answering "does it evade fingerprinting?").

### CAP-181 — Shared login/access-wall classifier (probe + transcode)
`Access/AccessClassifier.cs:1-72`. Pure decision function over an `AccessEvidence` record combining
network+DOM signals: `StatusCode==401`, `HasAuthenticationChallenge` (⇐ `WWW-Authenticate` header
presence, see `HttpProbeFetcher.cs:96,112,147` — `response.Headers.WwwAuthenticate.Count > 0`),
`RedirectedToLogin` (final URL's path matches a login-route regex **and** differs from the requested
URL), `HasBlockingIdentityUi` (password field inside a modal/dialog). Produces one of three
dispositions — `Restricted` (0.95 confidence, suggests `use_session`), `Open` (0.85, `continue`), or
`Unknown` (0.25, `retry_or_inspect`) — each with a machine-readable evidence-code list (e.g.
`authentication_challenge`, `usable_public_content`, `insufficient_access_evidence`).
**Status: PROVEN.**

### CAP-182 — Automatic `requires_login` conversion when no session was supplied
`PostProcessors/RequiresLoginPostProcessor.cs:1-46`. Runs only when `session_profile` was **not**
supplied for that call (line 13-16: if a session was used, this post-processor is a no-op even if
access evidence still looks restricted — i.e. it does not retry/upgrade the session automatically,
it only fires the *first* time a wall is hit anonymously). Converts an otherwise `ok:true` result
into `ok:false, failureCode:"requires_login"` when `AccessClassifier` returns
`RequiresLogin==true`. **Status: PROVEN.**

### CAP-183 — Access evidence collection is deliberately leak-resistant
`workers/shared/lib/access-evidence.mjs:1-62`. `collectAccessEvidence` returns **only booleans**
(`password_field`, `identity_field`, `login_form_action`, `login_heading`, `blocking_overlay`,
`has_usable_content`, `authentication_terminology`, `redirected_to_login`) — the file's own top
comment states the design intent: "No form values, text, headers, or selectors are returned, so
worker diagnostics cannot leak credentials or page content." Even the "terminology" check
(`AUTH_TERMS_RE`) only returns a boolean match result, never the matched substring.
**Status: PROVEN.**

---

## 5. Redirects / timeouts / retry / User-Agent

### CAP-184 — Redirect handling, Core probe (two client modes, both SSRF-guarded)
`Probe/HttpRedirectFollower.cs:1-114` (manual loop, `MaxRedirects=10`, full chain captured,
`redirect_loop` typed failure past the cap) used by the `RedirectTrackingClientName` client
(`AllowAutoRedirect=false` at the `SocketsHttpHandler` level, so redirects are followed *manually*
to build the chain) vs. the `AutoRedirectClientName` client (`AllowAutoRedirect=true`, native
following, no chain). Both clients share the same `ConnectCallback = OutboundHttpGuard.ConnectAsync`
(CAP-154), so every hop of either mode is SSRF-validated. **Status: PROVEN.**

### CAP-185 — Redirect handling, HTTP extract worker
Initial 3xx following is undici's own (already SSRF-safe because it rides the pinned dispatcher from
CAP-151); on top of that, a bespoke **`<meta refresh>` loop** (up to 3 hops,
`http-extract-run.mjs:232-278`) independently re-resolves/re-pins/re-validates each target and
strips cross-origin credentials (CAP-172) before each refetch — i.e. redirect-safety is enforced at
**two layers** (HTTP-header 3xx via undici, and HTML-level meta-refresh via bespoke code) rather than
assuming one covers the other. **Status: PROVEN.**

### CAP-186 — Redirect handling, browser worker (every navigation type)
`browser-session.mjs:154-186` (persistent `page.route` interceptor validates **every** navigation
request — initial, 3xx, meta-refresh, JS `location`, iframe loads — because this is the *only* place
a redirect to an internal host can be caught once Chromium owns DNS/redirect-following) plus a
second, independent final-URL check after full render (`validateFinalUrlInBrowser`, called at lines
490-519 for the initial nav and again at 644-651 after all interaction/scroll/consent steps).
**Status: PROVEN.**

### CAP-187 — Fixed, non-negotiable-per-call backend timeouts
`Backends/HttpExtractBackend.cs:8,27` — `DefaultHttpTimeoutMs = 35_000` process budget, with an
independent inner `AbortSignal.timeout(30_000)` on the actual `fetch()` call
(`http-extract-run.mjs:173`) — i.e. the process-level and fetch-level timeouts are two different
numbers (35s outer, 30s inner) with no MCP-tool parameter to change either per call.
`Backends/BrowserExtractBackend.cs:29-33` uses a provisioning-aware variable timeout
(`BrowserExtractTimeouts.ResolvePerExtractTimeoutMs`), and internally the browser worker layers its
own budgets on top: `page.setDefaultTimeout(gotoTimeout)` (45s/60s depending on
`consentAggressive`, `resolveGotoTimeoutMs`, lines 410-419, capped lower under `OCCAM_TIER_B=1`),
a 12s `waitForSelector` for basic content, up to an 8s `networkidle` re-settle pass when the first
extract looks too short (line 615). **Status: PROVEN — no per-call timeout override exists in any
tool signature inspected; timeouts are host/env-level only** (confirmed absent from
`Tools/OccamTranscodeTool.cs`'s parameter list, which the earlier grep for `session_profile` also
surfaced in full).

### CAP-188 — ABSENT: no automatic retry/backoff on transient network failures
Every fetch call site inspected (`http-extract-run.mjs`, `browser-session.mjs`, `HttpProbeFetcher.cs`)
performs a **single attempt**; there is no loop, no backoff, no re-issue-on-`ECONNRESET` anywhere in
the files read. `Routing/FailureCodeStrings.IsRetryable(code)` (lines 141-144) exists, but it is a
pure classifier ("is this the *kind* of failure a caller could usefully retry": `timeout`,
`network_error`, `dns_error`, `thin_extract`, `http_429`, `http_5xx`) — grepping its only call sites
confirms it feeds *decision/telemetry* surfaces (e.g. `Agent/TranscodeAgentDecisions.cs`,
`Playbooks/PlaybookHealPolicy.cs`), not an internal retry loop. The only thing that resembles a
"retry" at the orchestration level is `backend_policy=http_then_browser`, which is a full escalation
to a **different backend entirely**, not a retry of the same fetch. **Status: PROVEN ABSENT** as a
network-layer retry/backoff mechanism.

### CAP-189 — Single shared default identity (User-Agent + Accept) across all three fetch paths
`Session/OccamFetchDefaults.cs:1-51` (C#) and `workers/shared/lib/default-fetch-headers.mjs:1-48`
(JS) both load `profiles/occam-fetch-defaults.json` (`userAgent`, `accept` keys) with an **identical
hardcoded fallback** (`Mozilla/5.0 (Windows NT 10.0; Win64; x64) … Chrome/120.0.0.0 Safari/537.36`,
`text/html,application/xhtml+xml`) if the file is missing/unparseable — the comment atop each file
cross-references the other ("Keep in sync with…"). Used by the probe `HttpClient`
(`HttpProbeFetcher.cs:36`), the HTTP worker (`http-extract-run.mjs:166-170`), and the browser
worker's default context UA (`session-headers.mjs:4,12`). The **only** way to get a different UA
per call is a `session_profile` or `OCCAM_REQUEST_HEADERS_FILE` entry supplying `User-Agent`
explicitly (`mergeFetchHeaders`, `request-headers.mjs:24-33` — keeps the caller's UA if present,
else falls back to the shared default). **Status: PROVEN — no per-request UA randomization/rotation
capability exists.**

### CAP-190 — Robots.txt / crawl-politeness layer (opt-in, off by default)
`Services/RobotsThrottleService.cs:1-135`. Fully gated by `OCCAM_RESPECT_ROBOTS` (robots.txt
`Disallow` enforcement → typed `robots_disallowed` failure) and/or `OCCAM_HOST_THROTTLE_MS`
(flat per-host minimum interval, queued via a lock + "reserve the next slot" pattern so concurrent
same-host calls serialize politely, lines 67-80). When both are unset/zero the service is a
documented no-op that **never fetches robots.txt** (line 33-36 + class doc comment: "Occam is
user-directed (not a crawler), so this is off by default"). The robots.txt fetch itself uses the
SSRF-guarded `occam.robots` `HttpClient` (CAP-154). **Status: PROVEN.**

---

## 6. Cross-cutting / secret hygiene / tool-surface wiring

### CAP-191 — `session_profile` is plumbed through nearly every URL-touching MCP tool, not just transcode
Confirmed via full-repo grep of `Tools/*.cs` for `session_profile`: `occam_probe`
(`OccamProbeTool.cs:17`), `occam_transcode` (`OccamTranscodeTool.cs:52`), `occam_digest`
(`OccamDigestTool.cs:26`), `occam_map` (`OccamMapTool.cs:21`), `occam_extract_knowledge`
(`OccamExtractKnowledgeTool.cs:19`), `occam_claim_check` (`OccamClaimCheckTool.cs:23`),
`occam_attest` (`OccamAttestTool.cs:26`), `occam_dataset_export` (`OccamDatasetExportTool.cs:25`),
`occam_playbook_heal` (`OccamPlaybookHealTool.cs:17`), the opt-in `occam_batch_submit`
(`OccamBatchTools.cs:69`) and `occam_watch` (`OccamWatchTool.cs:23`), and the opt-in
`occam_crosscheck` (`OccamCrosscheckTool.cs:22`) — whose description explicitly frames
`session_profile` as adding "an authenticated vantage per backend (anon-vs-authed axis)", i.e. a
deliberate compare-authenticated-vs-anonymous capability, not just a login workaround.
**Status: PROVEN — HIDDEN_ADVANCED** relative to a doc that only mentions session profiles under
`occam_transcode`.

### CAP-192 — Secret-hygiene defaults for local session storage
`scripts/lib/occam-sessions-lib.mjs:113-121` (`ensureSessionsLayout` writes a `.gitignore` containing
`*\n!README.md\n!.gitignore\n` into the sessions root on first `init` — i.e. every session profile
and exported storageState file is excluded from version control **by default**, not as an opt-in).
Combined with CAP-170's "never log header values, only the temp filename" guarantee and CAP-161's
proxy-credential redaction, this forms a consistent "credentials touch disk/temp-files but never
logs/VCS" posture across the subsystem. **Status: PROVEN.**

### CAP-193 — CSS-extract worker also proxy-aware (egressFetch), confirming proxy plumbing is not HTML-only
`workers/css-extract/css-extract.mjs:3,39` imports and calls `egressFetch` (same function as the
HTML worker). Combined with CAP-165, this means: CSS-extract *fetches* respect the **static**
`OCCAM_HTTP_PROXY`/`HTTPS_PROXY` env (via `egressFetch`'s own env read, independent of how the
process was spawned) but never receive a **rotated** proxy, since `Workers/CssExtractWorker.cs:64`
spawns it with the non-rotating `EgressProxyConfig.ApplyTo`. **Status: PROVEN.**

### CAP-194 — Managed-backend escalation: API-key authenticated fetch to third-party extraction services
`Backends/ManagedExtractBackend.cs:1-64` + `Backends/Managed/{Jina,Firecrawl,Scrapfly,Spider}Provider.cs`.
Off by default; requires `OCCAM_MANAGED_PROVIDER` to name a registered provider **and**
(if `provider.RequiresApiKey`) `OCCAM_MANAGED_API_KEY` to be present (`ResolveProvider`, lines
47-64), with optional `OCCAM_MANAGED_BASE_URL` override and a comment stating "Credentials live only
in the environment, never in the repo" (line 9). Uses a dedicated `occam.managed` `HttpClient`
(`Composition/OccamServiceCollectionExtensions.cs:82-83`) — **note:** unlike the probe/robots/genome
clients, this `HttpClient` registration has **no** `ConnectCallback`/SSRF guard configured, though
the risk profile differs (the outbound target is the managed-provider's own API host, not an
arbitrary user URL — the user URL is passed as a request *parameter* to that API, not dialed
directly). **Status: PROVEN present; SSRF-guard absence on this specific HttpClient is a genuine gap
worth flagging even though its exploitability is lower than a direct-dial client (UNRESOLVED how the
providers pass the target URL — not read in full, see below).**

---

## 7. Explicitly out of reach / not verified (UNRESOLVED)

- **`Backends/Managed/*Provider.cs` request construction** — confirmed these classes exist and are
  registered (`JinaProvider`, `FirecrawlProvider`, `ScrapflyProvider`, `SpiderProvider`), and that
  `ManagedExtractBackend` resolves an API key + optional base URL for them, but the exact HTTP
  request shape (whether/how each provider forwards session headers, whether any of them could be
  tricked into SSRF via a crafted `OCCAM_MANAGED_BASE_URL`) was **not** read line-by-line — flagged
  as UNRESOLVED rather than guessed.
- **`Search/*Provider.cs`** (Searxng/Brave/Tavily) and their `HttpClient` — only confirmed via the DI
  registration (`OccamServiceCollectionExtensions.cs:89-94`, no `ConnectCallback`) that
  `occam_search` egress is also not SSRF-guarded and not proxy-aware; provider internals not read.
  Out of the explicit "Inspect at minimum" list for this ticket, so not pursued further.
- **`EgressProxyConfig.RedactCredentials` (C#) gate coverage** — implementation read and looks
  correct, but no dedicated unit/selftest was located for it in the areas inspected (unlike its JS
  twin `redactProxyUrl`, which is unit-tested). Not confirmed dead vs. tested-elsewhere.
- **`BrowserPoolManager.cs` / `HttpDaemonHost.cs` internals beyond the single `EgressProxyConfig.ApplyTo`
  call site** — confirmed via grep + the one call site each, but the surrounding daemon lifecycle
  code was not read in full; no further proxy/session-related behavior expected there but not
  exhaustively ruled out.

---

## 8. Summary table

| Area | Present | Notably absent / gapped |
|---|---|---|
| Proxy (static) | Node workers only (CAP-157/159/160/163) | Core's own `HttpClient`s (CAP-166); non-rotating daemon/pool/CSS/dom-skeleton spawns get static-only (CAP-165) |
| Proxy rotation | Yes, round-robin, per one-shot spawn (CAP-162/164) | Never reaches daemon/pool paths (CAP-165); no weighted/sticky/health-checked rotation — round-robin only |
| Browser proxy | Yes, incl. credentials + bypass (CAP-160) | — |
| Cookies | session_profile Cookie→Playwright injection (CAP-171), recipe cookie injection (CAP-177), cookies.txt import (CAP-175), storageState export (CAP-176) | No first-class Set-Cookie persistence back into a session profile after a call (session profiles are static input, not updated post-fetch) — not found in any file read |
| Session profiles | Local JSON + storageState, path-safe (CAP-167/168), plumbed into 12 tools (CAP-191) | No remote/shared session store — filesystem-local only |
| Custom headers | Global file + per-call profile, session wins (CAP-169) | No per-call inline header parameter on MCP tools (only `session_profile` indirection) found in the tool files inspected |
| Authenticated fetch | session_profile headers/cookies; managed-provider API keys (CAP-194) | Managed-provider `HttpClient` has no SSRF guard (gap) |
| Persisted state | Sessions dir + `states/` storageState files, gitignored by default (CAP-192) | — |
| Isolation | Per-request pinned dispatcher + per-request browser context, cross-origin credential stripping (CAP-172/173) | — |
| Secret handling | Temp header file never logged by content (CAP-170), proxy URL redaction (CAP-161), sessions `.gitignore` (CAP-192) | C# `RedactCredentials` gate coverage unresolved |
| Login detection | Shared `AccessClassifier` combining HTTP+DOM signals (CAP-181), auto `requires_login` (CAP-182), leak-resistant evidence (CAP-183) | Does not auto-upgrade a request that already has a session but still looks restricted |
| Challenge behavior | Dual live+post-hoc detection (CAP-179), generic consent dismissal (CAP-178), stealth baseline with explicit scope limits (CAP-180) | No CAPTCHA solving, no fingerprint rotation (explicitly out of scope per code comment) |
| SSRF | Three independent, dual-stack, DNS-rebinding-safe guards covering worker HTTP, worker browser, and Core's direct HttpClient (CAP-150-156) | Browser-path escape hatch (`OCCAM_ALLOW_PRIVATE_URLS`) skips the guard call outright rather than just relaxing rejection (asymmetry noted in CAP-156) |
| Redirects | Guarded at every layer/hop across all three fetch paths (CAP-184-186) | — |
| Timeouts | Fixed, layered, host/env-configurable only (CAP-187) | No per-call timeout MCP parameter |
| Retry | None at network layer (CAP-188, proven absent) | — |
