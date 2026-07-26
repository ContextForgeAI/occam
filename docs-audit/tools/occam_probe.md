# `occam_probe` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`). Documentation
(`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md`) was **not** used as evidence and is untrusted here —
every claim below cites a file read directly in this session.

**CAP ID range owned by this audit:** `CAP-420`–`CAP-449` (used: CAP-420…437; remainder reserved,
not exhausted). Wave-1 IDs are reused wherever probe activates existing infrastructure rather than
introducing new behavior.

**Method:** schema (`OccamProbeTool` param) → `ProbeService.AnalyzeAsync` → `HttpProbeFetcher` /
`HtmlProbeClassifier` / `AccessClassifier` / `DomainTierRegistry` → response mapper
(`OccamProbeResponseMapper`) → observable JSON field. Special focus per assignment: **does probe
ever reach a managed/browser backend?** — traced explicitly, answer below.

---

## 0. Entry point and schema

`OccamProbeTool.Probe` (`src/FFOccamMcp.Core/Tools/OccamProbeTool.cs`) is the sole MCP handler.
Parameters (only `url` required):

```
url (required), timeout_ms = 10_000, include_social_meta = false, session_profile = null
```

This is a **much smaller** surface than `occam_transcode` (4 params vs. ~20) — consistent with its
stated purpose ("cheaply diagnose a URL... before paying for a full fetch").

---

## CAP-420 — Core capability: cheap pre-fetch diagnosis, confirmed HTTP-only / backend-isolated

**Evidence:** `Tools/OccamProbeTool.cs` → `Services/ProbeService.cs` → `Probe/HttpProbeFetcher.cs`.

**Verification of the mandatory constraint ("probe must NOT reach managed backends"):** confirmed
by tracing every dependency of the probe call chain. `OccamProbeTool` depends only on
`ProbeService`; `ProbeService` depends only on `HttpProbeFetcher`; `HttpProbeFetcher` depends only
on `IHttpClientFactory` (two named clients, both plain `SocketsHttpHandler`-based, registered in
`Composition/OccamServiceCollectionExtensions.cs`). Grepping the entire probe call path for
`Backends`/`Worker`/`Managed`/`Browser` namespace references returns **zero matches**
(`Services/ProbeService.cs` has none). There is:

- no `IExtractBackend` / `OccamRouter` reference (that's the transcode-only dispatcher),
- no `NodeWorkerProcessSpawner` / `WorkerPaths` reference (no Node.js process is ever spawned),
- no `ManagedExtractBackend` / `IManagedProvider` reference (Jina/Firecrawl/Scrapfly/Spider are
  unreachable from probe, confirmed distinct from CAP-054's transcode-only third-party escalation).

`occam_probe` is a pure in-process `HttpClient` GET (one request, or a short manual redirect
chain) against the target URL, classified with regex/string heuristics over the first
≤256 KiB of the response body. This matches the tool's own MCP description ("Before paying for a
full fetch, cheaply diagnose a URL") and is architecturally enforced, not just a documented
intention — there is no code path from `OccamProbeTool` into `Routing/OccamRouter.cs` or any
`Backends/*` class at all.

---

## CAP-421 — `timeout_ms` (probe fetch timeout)

**Evidence:** `Tools/OccamProbeTool.cs`, `Probe/HttpProbeFetcher.cs`.

`int`, default `10_000`. Clamped server-side to `[1, 120_000]` ms
(`Math.Clamp(timeoutMs, 1, 120_000)` in `HttpProbeFetcher.FetchAsync`) — a caller passing `0` or a
negative value is silently raised to `1`ms rather than rejected with `invalid_arguments`; a caller
passing `999_999_999` is silently capped to 120s. This is looser than `occam_transcode`'s backend
timeouts (fixed 35s/120s, not caller-controlled at all per Wave-1 CAP-187) — `occam_probe` is the
**only** core tool where the caller directly controls a numeric backend timeout.

---

## CAP-422 — `include_social_meta` (OpenGraph/Twitter head-only extraction)

**Evidence:** `Probe/HtmlSocialMetaExtractor.cs`, `Services/ProbeService.cs` (guarded by
`includeSocialMeta && fetch.HtmlSample is not null`).

`bool`, default `false`. When true, scans only the `<head>` region (`Text.HtmlHeadScanner.Scan`)
for `og:title`/`og:description`/`og:image`/`twitter:card`/site name/`lang` — never touches
`<body>`. Relative `og:image` URLs are resolved to absolute against the page's final URL. This is
metadata-only and does not affect classification, recommendation, or failure codes — a pure
additive sidecar (`socialMeta` field, `JsonIgnore` when null).

---

## CAP-423 — `session_profile` on probe: headers-only forwarding (narrower than transcode)

**Evidence:** `Session/FetchPreflight.cs` (`Prepare`, used by probe — **distinct method** from the
`Validate` method Wave-1 documented for transcode, CAP-050), `Probe/ProbeHttpHeaders.cs`,
`Session/RequestHeadersMerger.cs`.

`session_profile` is resolved the same way as for transcode (Wave-1 CAP-068/069, reused
infrastructure: `SessionProfileHeaders.Resolve`, path-traversal/allow-list hardening,
`OCCAM_SESSIONS_ROOT/<id>.json`, merged with `OCCAM_REQUEST_HEADERS_FILE` env defaults, session
wins on key conflict). The merged flat header dictionary is applied to probe's plain `HttpClient`
via `ProbeHttpHeaders.Apply` (custom `User-Agent` handling + `TryAddWithoutValidation` for
everything else). **This is the entire effect** — see CAP-424 for what is silently dropped.

## CAP-424 — HIDDEN: `export-state`-created session profiles are inert for `occam_probe`

**Evidence:** `Session/FetchPreflight.cs` (`Prepare` resolves `session.StorageStatePath` into
`storageStatePath` and passes it into `FetchHeadersScope.Create(merged, storageStatePath)`),
`Probe/HttpProbeFetcher.cs` (`FetchAsync` signature takes only `requestHeaders`, never reads
`FetchHeadersScope.ActiveStorageStatePath` at all), `scripts/lib/occam-session-export-state.mjs`
(line ~89-93: `writeSessionProfile({ headers: { "User-Agent": userAgent }, storageState: … })` —
**no** `Cookie` header is ever written by `export-state`, only by the separate `import`
subcommand which converts a Netscape `cookies.txt` into a flat `Cookie` header).

This is a genuine, non-obvious gap: the two session-profile creation paths documented in
`AGENTS.md`'s own task table produce **structurally different** artifacts —

- `occam-session.mjs import --from cookies.txt` → writes a `Cookie:` header → **works** for
  `occam_probe` (headers are forwarded).
- `occam-session.mjs export-state` (the path the code's own inline warnings recommend for
  Cloudflare-protected sites: `"cf_clearance present — may still get http_403 on HTTP worker; use
  export-state + browser"`) → writes **only** `storageState` (a Playwright cookie-jar JSON file) +
  a bare `User-Agent` header → **does nothing** for `occam_probe`, because `HttpProbeFetcher` has
  no code path that reads a `storageState` file at all (that field only exists for the browser
  worker, consumed elsewhere in the transcode path, Wave-1 CAP-068/176).

Practical consequence: an agent that dutifully sets up a `session_profile` via `export-state` for
a login-walled or Cloudflare-gated site and then calls `occam_probe(url, session_profile=...)` to
"cheaply check" the page before transcoding will see the **exact same** (logged-out / challenged)
result as with no session profile at all — probe can wrongly report `likely_login_required` or
`likely_challenge` even though the corresponding `occam_transcode(..., backend_policy=browser,
session_profile=...)` call would succeed. Nothing in the tool's response distinguishes "no session
applied" from "session applied but had no effect."

---

## CAP-425 — Shared extractability scorer (0–1) — also backs `occam_search` reranking

**Evidence:** `Tools/SearchExtractabilityScorer.cs`, `Tools/OccamProbeModels.cs`
(`OccamProbeRecommendationInfo.Extractability`), `Tools/OccamSearchTool.cs` (line ~87-92).

`SearchExtractabilityScorer.Score(ProbeAnalysis)` is a pure, deterministic function (no network)
mapping the just-computed `ProbeAnalysis` to a single float: `0.0` for dead/HTTP-error/failed,
`0.05` for anti-bot/challenge, `0.15` for login-required, `0.3` for non-HTML (recommender says
`none`), `0.45` for JS-heavy stub (low visible-text-ratio + high script density), `0.55` for
generic browser-recommended pages, `0.9` for clean `docs`/`article`/`reference`/`blog` page
classes, `0.7` as the generic fallback. This exact scorer is reused verbatim by `occam_search`
(`OccamSearchTool.cs`) to rerank search-engine hits by "worth fetching" — **`occam_probe` is not an
island tool**: every `occam_search` call internally runs one `ProbeService.AnalyzeAsync` per
result URL (own timeout constant `RerankProbeTimeoutMs`, best-effort/non-fatal on exception) purely
to compute this same field for ranking, without ever surfacing a nested "probe response" to the
caller.

---

## CAP-426 — Backend recommendation heuristic (advisory only, not enforced)

**Evidence:** `Services/ProbeService.cs` (`Recommend` static method).

Returns `(Backend, EstimatedLatencyMs)` from a fixed decision table over `ProbeSignals` + resolved
`DomainTierMatch`:

1. `LikelyLoginRequired` → `("none", 0)`.
2. Domain tier `anti_bot_blogs` **and** `LikelyChallenge` → `("none", 0)`.
3. Domain tier `HttpOnly == true` **and not** `LikelyChallenge` → `("http", 800)`.
4. `LikelyCookieConsent` OR `SpaShell` OR `RequiresJavascript` OR `VisibleTextRatio < 0.03` →
   `("http_then_browser", 5000)`.
5. Otherwise → `("http", 1200)`.

This is **purely advisory text in the response** (`recommendation.backend`,
`recommendation.estimatedLatencyMs`) — `occam_probe` has no side effect on any subsequent
`occam_transcode` call; nothing is cached or passed by reference. A caller must read the
recommendation and manually set `backend_policy` on a separate `occam_transcode` call — the two
tools are not coupled at the code level beyond sharing the `DomainTierRegistry` and
`ProbeSignals` types.

## CAP-427 — `httpOnlyRoute` advisory flag (`DomainTierRegistry.PreferHttpOnlyRoute`)

**Evidence:** `Tools/OccamProbeModels.cs` (`HttpOnlyRoute` field, `JsonIgnore` when default/false),
`Routing/DomainTierRegistry.cs` (`PreferHttpOnlyRoute`).

A curated, second, narrower signal alongside `recommendation.backend`: true only when the domain
tier is `HttpOnly`, the page is not challenge/login-flagged, is not an SPA stub
(`SpaShellDetector.IsStub`), and — for `pageClass=documentation` — clears a tier-specific
visible-text-ratio floor (0.06 for `*.learn.microsoft.com`, 0.03 otherwise). Distinct from
`recommendation.backend` in that it is a curated allow-list bypass signal (skip browser
escalation entirely on known-safe sites) rather than a general heuristic.

## CAP-428 — Tri-state Access assessment surfaced on probe (`access` field)

**Evidence:** `Access/AccessClassifier.cs`, `Access/AccessEvidenceAdapters.FromProbeFetch`,
`Semantics/SemanticOutcomeMapper.MapAccess`, `Tools/OccamProbeModels.cs`
(`OccamProbeSuccessResponse.Access`).

`HtmlProbeClassifier.Classify` runs `AccessClassifier.Classify` (Wave-1 CAP-096, shared with
transcode) against DOM-stage evidence collected purely from the probe's own HTML sample (no
worker involved — evidence collection at `AccessEvidenceStage.Dom`, distinct from transcode's
`Combined`/`Extracted` stages). This produces `Open`/`Restricted`/`Unknown` with a confidence and
recommended action (`continue`/`use_session`/`retry_or_inspect`), independently surfaced in the
probe response's `access` object — this is the **same tri-axis honesty design** Wave-1 documented
for transcode (CAP-107) but computed a full extraction cycle earlier, from less evidence (probe
only ever reads ≤256 KiB of raw HTML, never a compiled/Readability-processed markdown).

## CAP-429 — Challenge-kind taxonomy on probe (independent detection instance)

**Evidence:** `Routing/ChallengeKindClassifier.cs` (shared class, Wave-1 does not have a
transcode-side CAP number dedicated to it beyond CAP-095's detector reference), `Probe/
HtmlProbeClassifier.cs` (`ClassifyLegacy` calls `ChallengeKindClassifier.DetectHint`).

Detects a specific challenge **kind** (`rate_limit`, `turnstile`, `hcaptcha`, `datadome`,
`js_challenge`, `generic_challenge`) with a `RecommendedAction` (`retry_later`, `skip_url`, `stop`,
`session_cookies`) — surfaced in the probe response as `classification.challenge`
(`{kind, healEligible, recommendedAction}`, always `healEligible: false` on probe since
playbook-heal is a transcode-only workflow). `DomainTierRegistry.ShouldSuppressProbeChallengeStop`
can null out an `hcaptcha`/`generic_challenge` hint specifically for "browser-friendly social
hosts" (Instagram/LinkedIn) with ≥350 chars of visible prose — a curated false-positive suppressor
distinct from the generic public-reference-page suppressor used elsewhere in the same classifier.

## CAP-430 — Manual redirect-chain capture with bounded hop count (probe-specific implementation)

**Evidence:** `Probe/HttpRedirectFollower.cs`.

Unlike the worker-side redirect handling Wave-1 documented for transcode (native HTTP client
auto-redirect inside the Node process, plus separate meta-refresh following, CAP-110/185/186),
`occam_probe`'s redirect chain is followed **manually** in C#: `AllowAutoRedirect = false` on the
probe's registered `HttpClient` (`Composition/OccamServiceCollectionExtensions.cs`), with
`HttpRedirectFollower.FollowAsync` doing its own loop (max 10 hops, `MaxRedirects = 10`), recording
every intermediate URL into `chain`, and failing with a probe-specific `redirect_loop` failure code
if the bound is exceeded — a failure code that does not exist anywhere in transcode's taxonomy
(Wave-1 CAP-105's consolidated list). Every hop still goes through the same physical `HttpClient`
whose `SocketsHttpHandler.ConnectCallback = OutboundHttpGuard.ConnectAsync` (Wave-1 CAP-154), so
SSRF protection on redirect hops is enforced at the socket-connect layer transparently, not by an
explicit re-classification call per hop (a different mechanism from transcode's explicit
per-hop `PrivacyClassifier` re-validation, Wave-1 CAP-100, but an equivalent guarantee).

## CAP-431 — PDF short-circuit classification (metadata-only, no real analysis)

**Evidence:** `Services/ProbeService.cs` (`BuildPdfAnalysis`), `Probe/HttpProbeFetcher.cs`
(`IsPdf` flag set purely from `Content-Type`/URL-suffix sniffing, reusing
`Routing/ContentFormatDetector` — Wave-1 CAP-059/237).

When the fetch is detected as PDF, probe does **not** read/parse any PDF bytes (no `unpdf`
invocation — that only exists in the HTTP extract *worker*, not in probe's C# path). It fabricates
a fixed classification: `pageClass: "pdf"`, `visibleTextRatio: 1.0` (hardcoded, not measured),
`riskFlags: []`, `recommendedBackend: "http"`, `estimatedLatencyMs: 0`. This means probe's PDF
"extractability" signal is a constant, not a real per-document measurement — a caller cannot use
`occam_probe` to distinguish a 2-page PDF from a 2000-page scanned-image PDF with no extractable
text; both report identically.

## CAP-432 — Byte-capped HTML sampling, shared ceiling across 4 call sites

**Evidence:** `Probe/HttpProbeFetcher.cs` (`DefaultMaxBytes = 256 KiB`,
`AbsoluteMaxBytes = 4 MiB`, inline comment explaining the ceiling exists specifically because
sitemap discovery needs up to 2 MiB and must not be silently truncated to the probe default).

`occam_probe` itself always reads at most 256 KiB of body (a hard classification-budget decision:
enough for `<head>` + above-the-fold content, not enough to be an extraction substitute). This same
fetcher/cap is reused with a **larger** `maxBytes` override by `Services/SitemapDiscovery.cs` (2
MiB robots.txt/sitemap XML reads) and `Services/MapService.cs`/`Services/DigestService.cs`
(homepage/hub page fetches) — see CAP-435 for the full cross-tool reuse picture.

## CAP-433 — `unsupported_content_type` short-circuit (non-HTML, non-PDF refusal)

**Evidence:** `Probe/HttpProbeFetcher.cs` (`IsHtml` gate — accepts `html`, `xml`, or
`text/plain` content-types only; anything else, once PDF is ruled out, returns
`FailureCode: "unsupported_content_type"` without reading the body at all).

This is a probe-only failure code (not present in transcode's taxonomy at all — transcode's HTTP
worker has dedicated non-HTML paths for feed/plain-text/PDF via CAP-059/080/111, so a URL that
probe refuses with `unsupported_content_type` may still transcode successfully through one of
those worker-side format branches). This is a **meaningful cross-tool inconsistency**: `occam_probe`
answering "not worth fetching" for a `.json`/binary/octet-stream URL does not necessarily mean
`occam_transcode` would fail on the same URL — the tools apply different content-type policies.

## CAP-434 — Probe-specific proactive agent hints (distinct nudge set from transcode's)

**Evidence:** `Agent/ProbeAgentHints.cs` (`ForProbe`).

A dedicated hint generator, separate from `Agent/TranscodeAgentDecisions.cs` (Wave-1 CAP-106),
that inspects probe-only signals to proactively suggest **transcode parameters the caller hasn't
set yet**, using only evidence the cheap probe fetch actually has:

- `HasTables` → suggests `json_tables=true` on the follow-up transcode call.
- `HasLlmsTxtLink` → suggests `prefer_llms_txt=true`.
- `ContentType` containing `rss`/`atom` → suggests `json_feed=true`.
- `HtmlBytes >= 750_000` ("large_page") → suggests `max_tokens` or `fit_markdown` + `focus_query`.
- `LikelyPaywall` → warns `thin_extract` is likely, suggests a `session_profile` if the caller has
  access.
- Anti-bot-blog tier + challenge → forces `suggestedNextTool: "none"` (do not bother
  transcoding at all).

This is the **most concrete "hidden capability" finding for this tool's short description**: an
agent reading only the one-line MCP tool description ("diagnose a URL... recommended backend")
would not expect `occam_probe` to also proactively recommend specific *sidecar parameters*
(`json_tables`, `prefer_llms_txt`, `json_feed`, `max_tokens`/`fit_markdown`) for a **different**
tool's call — this is cross-tool parameter advice baked into probe's response, not just a
page-class verdict.

## CAP-435 — `HttpProbeFetcher` as shared cross-tool infrastructure (indirect probe reuse)

**Evidence:** `Services/MapService.cs` (constructor takes `HttpProbeFetcher`, used for homepage +
per-hub fetches), `Services/DigestService.cs` (constructor takes `HttpProbeFetcher`, used for
resolving a digest "link source" URL), `Services/SitemapDiscovery.cs` (`DiscoverAsync(HttpProbeFetcher
fetcher, ...)`, used by `occam_map(source="sitemap")`), `Composition/OccamServiceCollectionExtensions.cs`
(single `AddSingleton<HttpProbeFetcher>()` registration shared by all consumers).

The exact same fetcher class instance (DI singleton) that powers `occam_probe` is silently reused
by three other tools' internal machinery — `occam_map`, `occam_digest`, and (via `occam_search`'s
internal `ProbeService` calls, CAP-425) `occam_search`. None of these call `occam_probe` as an MCP
tool; they hold a direct reference to the same underlying fetch primitive. Practical implication:
`occam_probe`'s SSRF guard, byte cap, and redirect-following behavior (CAP-430/432/433) are **not
scoped to the `occam_probe` tool** — they are load-bearing plumbing for at least four of the
fifteen core MCP tools. A change to `HttpProbeFetcher` (e.g. a byte-cap regression) would silently
affect `occam_map`/`occam_digest`/`occam_search` behavior with no `occam_probe`-specific test
necessarily catching it.

## CAP-436 — DEAD/UNREACHABLE: `probe.autoRedirect` HttpClient is registered but never selected

**Evidence:** `Probe/HttpProbeFetcher.cs` (`FetchAsync(..., bool trackRedirects = true, ...)`),
grep across `src/FFOccamMcp.Core/**` for `trackRedirects` — the only occurrences are the parameter
declaration and its two internal branches; no caller (`ProbeService`, `MapService`,
`DigestService`, `SitemapDiscovery`) ever passes `trackRedirects: false`.

`Composition/OccamServiceCollectionExtensions.cs` registers a second named `HttpClient` —
`HttpProbeFetcher.AutoRedirectClientName` ("probe.autoRedirect", `AllowAutoRedirect = true`) —
specifically for this branch, but with **zero live call sites** selecting it. This mirrors the
dead-code pattern Wave-1 flagged elsewhere (`materialization.md` CAP-324/328): a fully-wired,
DI-registered code path with no way to reach it from any current tool. Confirmed
implementation-detail-only (not worth a product-capability framing), noted for completeness per
the audit brief's dead-code-hunting mandate.

## CAP-437 — `domainTier` provenance field (read-only curated-tier stamp)

**Evidence:** `Tools/OccamProbeModels.cs` (`OccamProbeClassificationInfo.DomainTier`, `JsonIgnore`
when null), `Services/ProbeService.cs` (`tier?.TierId`).

Independent of whether `DomainTierRegistry.ApplyTierHints` (Wave-1 CAP-104) actually changed any
signal, the resolved tier ID (e.g. `tier_a_docs`, `news_consent`, `anti_bot_blogs`) is stamped onto
the response verbatim when a match exists — letting a caller distinguish "this page's
classification was influenced by curated per-site knowledge" from "purely generic heuristics" even
when the tier's hints happened not to change anything observable (e.g. a non-`HttpOnly` tier with
no `PageClassHint` is a no-op in `ApplyTierHints` but still surfaces `domainTier`).

---

## Failure code catalog for `occam_probe` (consolidated, code-confirmed)

| Code | Source | Notes |
|---|---|---|
| `invalid_arguments` | `OccamProbeTool.Probe` inline URL-shape check | Before `ProbeService` is even called |
| `invalid_url` | `Session/FetchPreflight.Prepare` / `PrivacyClassifier` | Distinct string from transcode's `invalid_arguments` for the same root cause |
| `private_url_blocked` | `PrivacyClassifier` (Wave-1 CAP-100), enforced in `FetchPreflight.Prepare` | Same global `OCCAM_ALLOW_PRIVATE_URLS` escape hatch as transcode |
| `session_profile_not_found` / `invalid_session_profile` | `Session/FetchPreflight.Prepare` → `SessionProfileHeaders.Resolve` | Same as transcode (CAP-069) |
| `timeout` | `HttpProbeFetcher.FetchAsync` catch clause | `OperationCanceledException` when not caller-cancelled |
| `http_404` | `HttpProbeFetcher` catch `HttpRequestException` w/ 404 | Also independently derivable via `FailureCodeStrings.FromHttpStatus` for other 4xx/5xx |
| `network_error` | `HttpProbeFetcher` generic catch-all | Broadest bucket |
| `invalid_url` (again) | `HttpProbeFetcher` catch `UriFormatException` | Overlaps with preflight's own check |
| `redirect_loop` | `Probe/HttpRedirectFollower.FollowAsync` | **Probe-only** code, absent from transcode's taxonomy |
| `unsupported_content_type` | `HttpProbeFetcher.FetchAsync` (`IsHtml` gate) | **Probe-only** code (CAP-433) |
| `http_401` / `http_403` / other `http_4xx`/`http_5xx` | `FailureCodeStrings.FromHttpStatus`, applied post-classification in `ProbeService.AnalyzeCoreAsync` | Only raised *after* a successful classification pass, not pre-empting it |

`ProbeAgentHints.ForFailure` maps a subset of these to `suggestedNextTool: "none"`
(`workers_unavailable`, `invalid_url`, `invalid_arguments`, `http_404`/`http_410`) — but note
`occam_probe` itself never produces `workers_unavailable` (no workers exist on this path); that
mapping is dead weight inherited from a shared hint helper, evidence it is written generically
for reuse rather than probe-specific.

---

## Security summary

- SSRF: same `PrivacyClassifier` global gate as transcode (Wave-1 CAP-100), plus the probe
  `HttpClient`'s own `OutboundHttpGuard.ConnectAsync` socket-level guard (Wave-1 CAP-154) covering
  every redirect hop transparently (CAP-430).
- Path traversal: `session_profile` resolution reuses the exact same hardened resolver as transcode
  (Wave-1 CAP-069) — no probe-specific weakening.
- Credential handling: session headers are forwarded to probe's `HttpClient` in-process (no temp
  file/argv exposure, since there is no child process on this path at all) — the temp-file/
  `AsyncLocal` scope machinery (Wave-1 CAP-068) is constructed by `FetchPreflight.Prepare` but its
  file-based handoff exists for the *worker* consumers; probe reads headers directly from the
  in-memory `MergedHeaders` dictionary instead.
- Resource exhaustion: hard byte cap (256 KiB default / 4 MiB ceiling, CAP-432), bounded redirect
  hops (10, CAP-430), caller-clamped timeout (1–120 000 ms, CAP-421) — smaller blast radius than
  transcode by construction (no worker process, no browser page).
- No managed/third-party escalation of any kind is reachable from this tool (CAP-420) — this
  narrows probe's operator-configured-secret exposure to zero (no API keys are ever read on this
  path).

---

## Hidden / advanced findings (summary)

1. **CAP-424** — `session_profile` created via `occam-session.mjs export-state` (the
   Cloudflare/browser-recommended path) is **silently inert** for `occam_probe` — only
   `import`-created (flat `Cookie:` header) profiles have any effect, because `HttpProbeFetcher`
   never reads a `storageState` file. No error, no warning field — the caller cannot tell from the
   response that their session was ignored.
2. **CAP-425/435** — `occam_probe`'s scoring/fetch machinery is silently embedded inside
   `occam_search` (per-result rerank) and `occam_map`/`occam_digest` (link/hub resolution) —
   calling those tools invokes the same code without the caller ever seeing a probe response or
   knowing probe-level guards (byte cap, SSRF guard, redirect bound) are what's actually running.
3. **CAP-434** — probe proactively recommends specific `occam_transcode` sidecar *parameters*
   (`json_tables`, `prefer_llms_txt`, `json_feed`, `max_tokens`/`fit_markdown`), not just a
   page-class verdict — undiscoverable from the one-line tool description.
4. **CAP-433** — `unsupported_content_type` on probe does not imply `occam_transcode` would fail on
   the same URL (transcode has worker-side non-HTML branches probe doesn't know about).
5. **CAP-436** — `probe.autoRedirect` HttpClient + the `trackRedirects=false` branch are fully
   wired but dead: no call site in the current codebase ever disables redirect tracking.
6. **CAP-431** — PDF probing is a fixed, non-measured stub (`visibleTextRatio: 1.0` always) — it
   cannot distinguish a good PDF from an unextractable scanned one.

## Cross-cutting checklist (per WAVE2-SHARED-INSTRUCTIONS)

| Category | Status on `occam_probe` |
|---|---|
| proxy | **Not used.** Probe's `HttpClient`s have no `OCCAM_HTTP_PROXY`/`HTTPS_PROXY` wiring (Wave-1 CAP-166 already flags Core's own `HttpClient`s as never honoring proxy env vars — probe is one of those clients, confirmed here). |
| session | Used, narrowly — headers-only (CAP-423), storageState silently dropped (CAP-424). |
| cookies | Only via a flat `Cookie:` header if present in the resolved session profile JSON (from `import`, not `export-state`). No cookie jar / `Set-Cookie` persistence across probe calls. |
| headers | Used — session headers + `OCCAM_REQUEST_HEADERS_FILE` merge (CAP-423), applied via `ProbeHttpHeaders.Apply`. |
| http | Used — probe *is* an HTTP-only tool by construction (CAP-420). |
| browser | **Not used, confirmed absent** — no code path reaches `BrowserExtractBackend` or any Playwright/worker code from this tool. |
| managed | **Not used, confirmed absent** — no code path reaches `ManagedExtractBackend`/Jina/Firecrawl/Scrapfly/Spider. |
| retry | **Not used.** No same-request retry-on-failure; a single fetch attempt (plus the manual redirect chain, which is following redirects, not retrying). |
| cache | **Not used.** No `TranscodeResponseCache`/`cache_ttl_s` equivalent for probe; every call re-fetches live. |
| diff | **Not used.** No `if_none_match`/`diff_against` concept on this tool. |
| blocks | **Not used.** No `json_blocks`/DOM block walk on probe. |
| tables | Detected as a boolean signal only (`HasTables`, drives an agent hint, CAP-434) — never extracted/returned as structured data by probe itself. |
| chunks | **Not used.** |
| budget | **Not used.** No `max_tokens`/token-budget concept — probe's own body-read cap (CAP-432) is a byte, not token, limit and is not caller-configurable. |
| receipts | **Not used.** No `Receipts/*` reference anywhere in the probe call path — probe responses are never signed. |
| merkle | **Not used.** |
| capsules | **Not used.** |
| playbooks | **Not used.** No `Playbooks/*` reference — probe does not consult or resolve playbooks (unlike transcode's `playbook_policy=auto`, CAP-070). |
| datasets | **Not used.** |
| claims | **Not used.** |
| trust tags | **Not used.** No `tag_trust`/`BlockTrust` equivalent. |
| screenshots | **Not used.** No `capture_screenshot` parameter or browser capability at all. |
| translate | **Not used.** |
| llms.txt | Detected as a boolean signal only (`HasLlmsTxtLink`, drives an agent hint, CAP-434) — probe does not itself fetch or prefer `/llms.txt` content (unlike transcode's `prefer_llms_txt`, CAP-084). |
| feeds | Detected via `ContentType` string match only (drives an agent hint, CAP-434) — probe does not parse feed items (unlike transcode's `json_feed`, CAP-080). |
| profile | Included in all four MCP tool-surface profiles per `CODE-MAP.md` (`full`/`reader`/`researcher`/`auditor`) — no profile excludes `occam_probe`. |
| env | `OCCAM_ALLOW_PRIVATE_URLS` (SSRF override, shared), `OCCAM_SESSIONS_ROOT` (shared), `OCCAM_REQUEST_HEADERS_FILE` (shared). No probe-specific env var was found (no `OCCAM_PROBE_*` prefix exists in the inspected files). |

## Answer: hidden / non-obvious capabilities a user would never discover from the short MCP description

The tool description reads: *"Before paying for a full fetch, cheaply diagnose a URL: page class,
risks, redirect chain, an extractability score (0-1...), and the recommended backend for
occam_transcode."* A user reading only this would **not** discover:

- That probe proactively recommends specific *sidecar parameters* for a follow-up transcode call
  (`json_tables`, `prefer_llms_txt`, `json_feed`, `max_tokens`/`fit_markdown`) — CAP-434.
- That `session_profile` support is real but silently degrades to "no effect" for the
  browser-export session type — CAP-424.
- That the same extractability score also silently reranks `occam_search` results — CAP-425.
- That probe's fetch primitive is shared, load-bearing infrastructure for `occam_map` and
  `occam_digest`, not an isolated diagnostic path — CAP-435.
- That a `Restricted`/`Open`/`Unknown` **access** tri-state (distinct from the simple boolean
  `likelyLoginRequired` risk flag) is computed and returned — CAP-428.
- That a curated `domainTier` provenance stamp is returned even when it changed nothing
  observable — CAP-437.
- That `occam_probe` explicitly refuses to classify non-HTML/non-PDF content
  (`unsupported_content_type`) in a way that does **not** predict `occam_transcode`'s own success
  on the same URL — CAP-433.

## Unresolved items

- Whether `OCCAM_REQUEST_HEADERS_FILE`-only (no `session_profile`) requests to `occam_probe` are
  exercised by any gate test — evidence found the merge logic (`RequestHeadersMerger`) but not a
  probe-specific gate assertion for the env-only path.
- Exact behavior when `include_social_meta=true` is requested on a non-HTML (PDF) or failed fetch —
  traced the guard (`includeSocialMeta && fetch.HtmlSample is not null`) confirming it is silently
  skipped, but did not find a response field indicating "social meta was requested but
  unavailable" (same style of silent no-op as CAP-424, lower severity since it's an explicit opt-in
  with an obviously-absent-then field rather than a false negative).

## Capability graph edges

```
TOOL:occam_probe|USES|CAP-420
PARAM:timeout_ms|ENABLES|CAP-421
PARAM:include_social_meta|ENABLES|CAP-422
PARAM:session_profile|ENABLES|CAP-423
CAP-423|FALLS_SHORT_OF|CAP-424
CAP-424|CONSUMES|session
TOOL:occam_probe|PRODUCES|CAP-425
CAP-425|CONSUMED_BY|TOOL:occam_search
TOOL:occam_probe|USES|CAP-426
TOOL:occam_probe|USES|CAP-427
CAP-427|USES|CAP-104
TOOL:occam_probe|PRODUCES|CAP-428
CAP-428|USES|CAP-096
TOOL:occam_probe|USES|CAP-429
CAP-429|USES|CAP-104
TOOL:occam_probe|USES|CAP-430
CAP-430|ROUTES_TO|http
CAP-430|FALLS_BACK_TO|(none — no browser/managed rung on probe)
TOOL:occam_probe|USES|CAP-431
CAP-431|ROUTES_TO|http
TOOL:occam_probe|USES|CAP-432
CAP-432|CONSUMED_BY|TOOL:occam_map
CAP-432|CONSUMED_BY|TOOL:occam_digest
TOOL:occam_probe|USES|CAP-433
TOOL:occam_probe|PRODUCES|CAP-434
CAP-434|ENABLES|PARAM:json_tables
CAP-434|ENABLES|PARAM:prefer_llms_txt
CAP-434|ENABLES|PARAM:json_feed
CAP-434|ENABLES|PARAM:max_tokens
TOOL:occam_probe|USES|CAP-435
CAP-435|CONSUMED_BY|TOOL:occam_map
CAP-435|CONSUMED_BY|TOOL:occam_digest
CAP-435|CONSUMED_BY|TOOL:occam_search
TOOL:occam_probe|USES|CAP-436
CAP-436|ABSENT_FROM|live_traffic
TOOL:occam_probe|PRODUCES|CAP-437
CAP-437|USES|CAP-104
TOOL:occam_probe|USES|CAP-100
TOOL:occam_probe|USES|CAP-154
TOOL:occam_probe|USES|CAP-068
TOOL:occam_probe|USES|CAP-069
TOOL:occam_probe|USES|CAP-059
TOOL:occam_probe|USES|CAP-105
TOOL:occam_probe|ROUTES_TO|http
TOOL:occam_probe|FALLS_BACK_TO|(none — browser and managed backends are unreachable from this tool)
```
