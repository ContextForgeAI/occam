# `occam_map` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`). Documentation
(`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md`) was **not** used as evidence — every claim below cites
a file read directly this session.

**CAP ID range owned by this audit:** `CAP-510`–`CAP-529` (used: CAP-510…529, full range).
Reused Wave-1 IDs are cited inline (mostly `CAP-150…199` network/session family from
`docs-audit/subsystems/network-fetch-proxy.md`).

**Files inspected:**
`Tools/OccamMapTool.cs`, `Tools/OccamMapModels.cs`, `Services/MapService.cs`,
`Services/MapLinkRanker.cs`, `Services/MapLinkFilter.cs`, `Services/MapSoft404Filter.cs`,
`Services/SitemapDiscovery.cs`, `Probe/HtmlLinkExtractor.cs` (+ `MapLinkTitleSanitizer`),
`Probe/HttpProbeFetcher.cs`, `Probe/HttpRedirectFollower.cs`, `Probe/MappedLink.cs`,
`Routing/PrivacyClassifier.cs`, `Routing/OutboundHttpGuard.cs`, `Session/FetchPreflight.cs`,
`Session/SessionProfileHeaders.cs`, `Compile/FocusQueryDecomposition.cs`,
`Composition/OccamServiceCollectionExtensions.cs` (HttpClient wiring), `Services/DigestService.cs`
(cross-reference for shared discovery engine), `Agent/TranscodeAgentDecisions.cs`,
`Agent/FailureAgentHints.cs`.

---

## 0. Entry point and schema

`OccamMapTool.Map` (`Tools/OccamMapTool.cs`) — MCP name `occam_map`. Parameters:

```
url (required), source = "homepage", max_links = 32 (1-64), same_domain = true,
filter_nonsense = true, focus_query = null, timeout_ms = 15000 (3000-30000),
session_profile = null
```

No `backend_policy` parameter exists at all — see **CAP-510**. `max_links` and `timeout_ms` are
validated and hard-rejected (`invalid_arguments`) **in the tool method itself**, before
`MapService.MapAsync` is ever called — a stricter gate than the service layer's own internal
`Math.Clamp` safety net (**CAP-525**).

---

## CAP-510 — HTTP-only design, no backend escalation ever

**Evidence:** `Tools/OccamMapTool.cs` (no `backend_policy` param), `Services/MapService.cs` (only
`HttpProbeFetcher` is injected/used — no `IExtractBackend`, no browser reference anywhere in the
file). Confirmed against the tool's own MCP description: *"HTTP-only, up to 64 URLs"*. Unlike
`occam_transcode`/`occam_digest`, there is **no cascade, no challenge-page escalation, no
Playwright fallback** — a JS-rendered SPA homepage with no server-rendered `<a>` tags will simply
return few/zero links (surfaced as `thin_extract`) with no automatic browser retry offered by the
tool itself, only recorded in the failure taxonomy (**CAP-522**).

## CAP-511 — Homepage link extraction (`source=homepage`, default)

**Evidence:** `Probe/HtmlLinkExtractor.Extract`, called from `MapService.MapCoreAsync`. Streams the
fetched HTML (`HtmlStreamScanner.EnumerateAnchors`), resolves each `href` against the base URL,
strips `#`/`javascript:`/`mailto:`/`tel:` pseudo-links, canonicalizes to `scheme://host/path[?query]`
(fragment always dropped), dedupes case-insensitively, and stops at an internal `extractCap`
(**CAP-518**) — a hard DOM-order cutoff distinct from the caller's `max_links`. Anchor inner text
becomes `Title` after `MapLinkTitleSanitizer` cleanup (**CAP-526**).

## CAP-512 — Sitemap / robots discovery (`source=sitemap` / `source=robots`)

**Evidence:** `Services/SitemapDiscovery.DiscoverAsync`. Always fetches `/robots.txt` first
(128 KB cap) and extracts `Sitemap:` directive URLs; if `source=sitemap` and robots declared none,
falls back to a bare `/sitemap.xml` guess. Follows **sitemap-index** (`<sitemapindex>`) nesting by
queuing discovered child sitemap URLs, bounded to **`MaxSitemapFetches = 4`** total XML fetches
(root sitemap + index children combined) and a 2 MB per-sitemap byte cap (absolute worker/fetcher
ceiling is 4 MB, see **CAP-523**). `source=robots` restricts the walk to robots-declared sitemaps
only (`robotsOnly: true`) — no blind `/sitemap.xml` guess in that mode. XML parsing prefers a real
`XmlReader` (DTD/external-entity processing explicitly disabled) and falls back to a regex `<loc>`
scan on any parse exception, so malformed-but-recognizable sitemap XML still yields links.

## CAP-513 — Discovery deadline is a shared TOTAL budget, not per-fetch

**Evidence:** `SitemapDiscovery.DiscoverAsync`, code comment lines 139-144: a single
`CancellationTokenSource.CancelAfter(timeoutMs)` wraps the entire robots→sitemap-index→sitemap walk,
and each subsequent fetch's own timeout argument is the **remaining** budget (`RemainingMs()`), not
the caller's full `timeout_ms` again. This is an explicit fix noted in-code against a prior bug where
"robots + up to 4 sitemaps ran sequentially at the full timeout each" (i.e. a slow host could
previously stall map for ~5× the caller's `timeout_ms`). A timeout mid-walk returns whatever links
were already collected with `Partial: true` (**CAP-524**) rather than failing the whole call, unless
zero links were found (then `timeout` failure).

## CAP-514 — `filter_nonsense` composite noise filter

**Evidence:** `Services/MapLinkFilter.IsNonsense` (+ `MapSoft404Filter.LooksLikeSoft404`). A single
opt-out boolean gates **four independent heuristics** stacked together, not one check:
1. Path-substring blocklist (`/_next/static`, `/webpack`, `/chunk`, `/hot-update`, `/assets/`,
 `/static/js|css/`, `/node_modules/`).
2. Extension blocklist (`.js/.css/.map/.woff*/.png/.jpg/...svg/.pdf/.zip/.gz`, plus a
 `ext + "?"` check for query-stringed asset URLs).
3. **Soft-404 detection** — title text matching `404`/`not found`/`error`/`oops`, or path containing
 `/404`, `/not-found`, `/error` (root `/` explicitly exempted from the path check).
4. **Site-specific carve-out**: `nginx.org` sitemap entries under a `/changes`-prefixed path segment
 are dropped as version-changelog noise — the **only** hardcoded per-host rule found in `occam_map`
 (contrasts with `AGENTS.md`'s general anti-pattern guidance against per-host branches; this one
 predates/sits outside the seeds/playbooks mechanism).

Filtered-out links are counted (`Filtered`) and reported in the response, not silently dropped.

## CAP-515 — `focus_query` entity-first link ranking

**Evidence:** `Services/MapLinkRanker` (+ `Compile/FocusQueryDecomposition`). This is a bespoke
ranker, not a generic BM25 rerank: the query is first decomposed into **primary anchors** (quoted
phrases, path-like/dotted identifiers, snake_case/PascalCase identifiers, and any token not in a
curated ~90-word "supporting lexicon" of generic verbs/prepositions/doc-jargon) vs **supporting
terms**. Scoring then heavily favors primary-anchor hits in the URL path segment (+12) or title
(+8) over generic term overlap (BM25 contribution is down-weighted ×0.35 whenever any primary anchor
exists, and semantic-overlap is capped at 2.0) — so a query like `"asyncio create_task"` will rank a
page whose path segment literally contains `create_task` far above a page that merely mentions
"asyncio" and "task" frequently in prose. A **missing-primary penalty** (-8) and a **version-root /
changelog penalty** (-6 or -3, +2 extra when primaries exist) actively demote version-index pages
(e.g. `/3.12/`) and "what's new"/changelog pages unless the query itself looks version-related
(**`QueryLooksLikeVersion`**). Without `focus_query`, links are returned in raw discovery order
(homepage DOM order / sitemap document order) truncated to `max_links` — no ranking at all.

## CAP-516 — Second-level hub expansion (auto-crawl on weak focus hit)

**Evidence:** `MapService.ExpandSecondLevelAsync` + `SelectExpansionHubs`/`LooksLikeHub`/`HubPriority`.
When `source=homepage`, `focus_query` is set, and the initial ranked pool has **no strong hit**
(`MapLinkRanker.HasStrongHit`, score ≥ `StrongHitThreshold = 4.0`) and there is still time left in
`timeout_ms`, the service auto-selects up to **`MaxSecondLevelHubs = 3`** likely "hub" pages from the
already-discovered links (paths ending in `/`, or containing `/library`, `/reference`, `/docs`,
`/api`, `/guide`, or titles containing "library"/"reference"/"documentation"/"index" — scored by
`HubPriority`), fetches each within a divided remaining-budget window (clamped 1.5s–8s per hub), and
merges their extracted links (plus a primary-anchor enrichment pass, **CAP-517**) back into the
candidate pool before re-ranking. This is a **genuine second network round-trip the caller did not
explicitly ask for** — `focus_query` alone silently triggers 1-3 extra page fetches beyond the seed
URL. Surfaced honestly via `expanded: true` + a warning string in the response, not hidden.

## CAP-517 — Primary-anchor enrichment scan (unbounded by DOM-order cap)

**Evidence:** `HtmlLinkExtractor.ExtractPrimaryMatches`, called from `MapService.ExtractPrimaryEnrichment`
on both the seed page and every expansion hub (**CAP-516**) whenever `focus_query` decomposes to at
least one primary anchor. Unlike the main `Extract` pass (which stops at `extractCap` in DOM
encounter order — **CAP-518**), this second pass scans the **entire** HTML document for any anchor
whose path or title contains a primary anchor term, capped only at `maxMatches = 48`. Rationale
(from code comment): a rare, highly-relevant entity link appearing late in a large page's DOM would
otherwise be silently dropped by the sequential cap before ranking ever sees it.

## CAP-518 — Focus-aware over-fetch candidate pool sizing

**Evidence:** `MapService.MapCoreAsync`, `extractCap` computation (line ~89). Without `focus_query`,
the extraction cap is `max(maxLinks*2, maxLinks)` (a modest safety margin over what will actually be
returned). With `focus_query` set, the cap jumps to `min(200, max(maxLinks*8, 64))` — i.e. asking for
a focused map over-fetches up to **200 raw candidates** (regardless of the caller's `max_links`) so
the ranker has a real pool to compete over instead of just the first N in DOM order. This is
invisible in the response shape (only the final ranked/truncated `links[]` is returned) but directly
explains why a focused `occam_map` call can be measurably slower/more memory-active than an
unfocused one on a link-dense homepage.

## CAP-519 — `same_domain` off-origin exclusion toggle

**Evidence:** `HtmlLinkExtractor.IsSameHost` / `SitemapDiscovery.TryNormalizePageUrl` both gate on
`sameDomainOnly` (default `true` from the tool's `same_domain` param). Host comparison is **exact
hostname match** (`Uri.Host.Equals(..., OrdinalIgnoreCase)`) — no `www.`-prefix normalization and no
subdomain/eTLD+1 awareness, so `same_domain=true` on `https://docs.example.com` will exclude links to
`https://example.com` or `https://blog.example.com` even though a human would call those "the same
site." Setting `same_domain=false` removes this filter entirely for both homepage and
sitemap/robots sources, letting genuinely off-origin links (e.g. a homepage linking out to partner
sites) through — combined with `filter_nonsense`, still subject to the asset/soft-404 filters.

## CAP-520 — Neighbor-text context extraction (soft ranking signal, not in response)

**Evidence:** `HtmlLinkExtractor.ExtractNeighborContext`. For every extracted homepage anchor, the
extractor re-locates the raw `href` string in the original HTML and captures a ±100-character
plain-text window around it (tag-stripped, sanitized, capped at 240 chars) as `MappedLink.Context`.
This field is **never exposed in the MCP response** (`OccamMapLinkInfo` only carries `Url`, `Title`,
`Path`) — its only consumer is `MapLinkRanker`'s scoring (`contextNorm` contributes to `anchorPhrase`
and `supporting` scores at reduced weight vs. path/title). A completely internal, hidden ranking
input.

## CAP-521 — Hardcoded `agentHints.suggestedNext = occam_digest` (always emitted)

**Evidence:** `OccamMapResponseMapper.MapSuccess` — `AgentHints: new(SuggestedNext: "occam_digest",
MaxDigestUrls: DigestService.MaxUrlsCap, Warnings: ...)`. This is **unconditional** on every
successful `occam_map` response regardless of `source`, result size, or `focus_query` — there is no
branch that ever suggests a different next tool (e.g. `occam_transcode` for a single standout link).
`MaxDigestUrls` is read live from `DigestService.MaxUrlsCap`, so if that cap ever changes the hint
stays numerically correct without a code change here — a small but real cross-tool coupling
(**CROSS_SUBSYSTEM_EDGES**: `occam_map` → `occam_digest`).

## CAP-522 — Map-specific failure taxonomy

**Evidence:** `OccamMapResponseMapper.FormatMapMessage`, `MapService` failure sites. Distinct failure
codes not shared verbatim with `occam_transcode`'s taxonomy: `sitemap_not_found` (empty
sitemap/robots walk), `unsupported_content_type` (non-HTML homepage response, or a PDF URL rejected
pre-fetch via `ContentFormatDetector.IsPdfUrl` — reusing the transcode-audit's **CAP-059** detector at
the map layer, confirming it is a shared utility, not transcode-only), `invalid_url` (malformed input,
returned **before** the generic `invalid_arguments` path — distinct code, not reused), and
`thin_extract` scoped specifically to "homepage HTML had no extractable same-domain links after
filtering" (a materially different meaning from `occam_transcode`'s prose-quality `thin_extract`).
`TranscodeAgentDecisions.ForFailure` is reused for hint generation (**CAP-106** family) and contains a
map-aware branch: `sitemap_not_found` → hints `retry_map(occam_map, source=homepage)`; generic
`thin_extract` → hints `retry_transcode(occam_transcode, backend_policy=browser)`, which is a
**cross-tool recovery suggestion out of `occam_map` entirely** since map itself never escalates to a
browser (**CAP-510**).

## CAP-523 — Response byte caps per fetch kind (map-tuned, not the transcode defaults)

**Evidence:** `MapService.MapCoreAsync`/`ExpandSecondLevelAsync` (homepage + hub fetches:
512 KB `maxBytes`), `SitemapDiscovery.DiscoverAsync` (robots: 128 KB; each sitemap body:
`MaxSitemapBytes = 2 MB`). `HttpProbeFetcher.FetchAsync` clamps any caller-supplied `maxBytes` into
`[1, AbsoluteMaxBytes = 4 MB]` — the fetcher's own code comment explicitly calls out that this
absolute ceiling exists **because** sitemap discovery legitimately needs more than the fetcher's own
256 KB default probe read, so a naive clamp-to-default would have silently truncated large sitemaps.
Oversized bodies are read up to the cap and processed truncated (no fail-fast/partial-mode toggle
here unlike `occam_transcode`'s **CAP-101** — map always takes the "partial" behavior implicitly).

## CAP-524 — `Partial` honesty flag (sitemap walk truncated by budget)

**Evidence:** `SitemapDiscovery.DiscoverAsync` returns `TimedOut`; `MapService.MapCoreAsync` maps this
to `MapAnalysis.Partial`, surfaced in the response as `partial: true` plus an explicit warning string
("Sitemap discovery reached timeout_ms after finding some links; links[] is partial."). This only
applies to `source=sitemap`/`robots` — homepage-source discovery has no equivalent partial flag
(a slow/large homepage fetch either completes within `HttpProbeFetcher`'s own timeout or fails
outright with `timeout`, it cannot "partially" extract mid-document).

## CAP-525 — Redundant double bounds-checking (tool-level reject vs service-level clamp)

**Evidence:** `Tools/OccamMapTool.Map` rejects out-of-range `max_links`/`timeout_ms` with
`invalid_arguments` **before** calling the service; `MapService.MapCoreAsync` independently
`Math.Clamp`s both values again internally. Since the tool is the only MCP entry point and always
validates first, the service-level clamp is dead code from the MCP surface's perspective — it only
matters for any other in-process caller of `MapService.MapAsync` that skips the tool's validation
(e.g. `DigestService`'s auto-discovery path, **CAP-529**, which passes fixed constants and cannot
trigger either branch anyway). Not a bug, but worth flagging as belt-and-suspenders rather than a
single source of truth for validation.

## CAP-526 — Anchor title sanitization (SVG/CSS garbage rejection)

**Evidence:** `MapLinkTitleSanitizer.Sanitize` (`Probe/HtmlLinkExtractor.cs`). Anchor inner text is
rejected outright (title becomes `null`, not just trimmed) if it contains `xmlns`, `viewBox`, `{`, or
`display:` — heuristics targeting inline-SVG icon markup or raw CSS that leaks into `<a>` inner-text
scraping on sites with inlined icon sprites inside links. Titles are also hard-truncated to 120 chars.
This is defensive noise-filtering with no MCP-visible toggle — always on.

## CAP-527 — `session_profile` on `occam_map` is headers-only; `storageState` is resolved but unused

**Evidence:** `Session/FetchPreflight.Prepare` (shared with `occam_transcode`) resolves **both** a
merged-headers dictionary **and** a Playwright `storageStatePath` for any given `session_profile`
(the same code path documented in the transcode audit's **CAP-068**). `MapService.MapAsync` only
ever reads `preflight.MergedHeaders` and passes it to `HttpProbeFetcher.FetchAsync` — the resolved
`storageStatePath` / `FetchHeadersScope.ActiveStorageStatePath` is computed and scoped but **never
consumed**, because `MapService` has no browser backend to hand it to (**CAP-510**). Practical
consequence: a `session_profile` authored primarily via a browser-login export (which typically
populates Playwright `storageState` — cookies/localStorage — and may carry **no** explicit `headers`
object at all, per `Session/SessionProfileHeaders.cs`'s JSON shape) can be a **silent no-op** on
`occam_map` even though the same profile fully authenticates `occam_transcode`'s browser backend.
Only a session profile with an explicit `headers.Cookie` (or other header) entry actually changes
`occam_map`'s HTTP requests. This is a genuinely non-obvious gap — the parameter name and its
presence on `occam_map` implies parity with `occam_transcode`'s handling that does not fully exist.

## CAP-528 — PDF short-circuit before any network fetch

**Evidence:** `MapService.MapCoreAsync`, first check in `MapCoreAsync`:
`ContentFormatDetector.IsPdfUrl(url)` → immediate `unsupported_content_type` failure, no HTTP call
made at all. This reuses the transcode-audit's **CAP-059** URL-suffix PDF detector (`.pdf` extension
heuristic only — content-type sniffing is not possible here since no request has been sent yet) at a
layer transcode doesn't need it (transcode extracts PDFs; map has no concept of extracting links from
a PDF's own content, so it refuses up front rather than fetching and then failing).

## CAP-529 — Shared discovery engine: `occam_digest` reuses `MapService`/`MapLinkRanker` directly

**Evidence:** `Services/DigestService.cs` (`~line 391-462, 571`) calls `MapService.MapAsync` (using
`MapService.MaxLinksCap`/`DefaultTimeoutMs` as its own internal constants) and
`MapLinkRanker.Rank(...)` directly as part of its own **auto-discovery** feature (when `occam_digest`
is given a seed/hub URL instead of an explicit URL list — out of full scope for this audit per the
Wave 2 assignment, but the coupling is direct evidence `occam_map`'s ranking/discovery internals are
not `occam_map`-exclusive). This means a change to `MapLinkRanker`'s scoring or `MapService`'s
timeout/cap constants silently changes `occam_digest`'s auto-discovery behavior too — the two tools
are not independently versioned in this subsystem.

---

## Cross-cutting categories checklist (per shared instructions)

| Category | Finding |
|---|---|
| Proxy | **Not used.** `occam_map`'s `HttpProbeFetcher` clients (`probe.redirectTracking`/`probe.autoRedirect`) have no `OCCAM_HTTP_PROXY`/`HTTPS_PROXY` wiring in `OccamServiceCollectionExtensions.cs` (proxy config there is scoped to Node workers + a few other named clients — see Wave-1 `CAP-166`, ABSENT for Core `HttpClient`s generally, confirmed applicable here). |
| Session | **Headers only** — `session_profile` resolved via shared `FetchPreflight`/`SessionProfileHeaders` (Wave-1 **CAP-167/169/170/191**), but only the headers half is consumed (**CAP-527**, new finding this audit). |
| Cookies | Only via a session profile's explicit `headers.Cookie` string (reuses Wave-1 **CAP-171**'s header-merge mechanism at the plain-HTTP layer, not the cookie-jar/Playwright-injection half of CAP-171 which is browser-only). |
| Headers | Session-profile headers + env-level default headers merged (`RequestHeadersMerger`, Wave-1 **CAP-169**); default `User-Agent` from `OccamFetchDefaults.UserAgent` (Wave-1 **CAP-189**). |
| HTTP | Exclusive transport — two `SocketsHttpHandler`-based clients (`probe.redirectTracking` no-auto-redirect + manual loop, `probe.autoRedirect` auto-redirect) both SSRF-guarded (Wave-1 **CAP-184**, confirmed directly this audit — see SSRF section below). |
| Browser | **Not used at all** — confirmed absent from every file inspected (**CAP-510**). |
| Managed (3rd-party scraping) | **Not used** — no reference to `ManagedExtractBackend`/Jina/Firecrawl/Scrapfly/Spider anywhere in the map code path. |
| Retry | **None** — no retry-on-failure logic found in `MapService`/`HttpProbeFetcher`/`SitemapDiscovery` beyond the sitemap-index child-URL queue (which is new-URL traversal, not a retry of a failed fetch); consistent with Wave-1 **CAP-188** (absent network-layer retry, repo-wide). |
| Cache | **Not used** — no cache lookup/store anywhere in `MapService`; `occam_transcode`'s `cache_ttl_s`-gated cache (Wave-1/transcode **CAP-085**) is a transcode-only feature, not reachable from `occam_map`. |
| Diff | Not applicable — no `if_none_match`/`diff_against`-equivalent parameter on `occam_map`. |
| Blocks / Tables / Chunks | Not applicable — `occam_map` has no markdown/materialization output; it returns a link array only, no `json_blocks`/`json_tables`/`semantic_chunking` concept exists here. |
| Budget (tokens) | Not applicable — no `max_tokens`/`ClientCapabilityStore` interaction; the only "budget" concept is `max_links` (result count) and `timeout_ms` (wall clock), unrelated to token economy. |
| Receipts / Merkle / Capsules | **Not used** — no `Receipts/*` reference anywhere in the map code path; `occam_map` responses carry no `receipt` field. |
| Playbooks | **Not used** — no `Playbooks/*` reference; `occam_map` does not consult site playbooks/genome for link discovery (a possible future extension point, not present today). |
| Datasets / Claims / Trust tags | **Not used** — no `occam_dataset_export`/`occam_claim_check`/`tag_trust` touchpoints found. |
| Screenshots | Not applicable — HTTP-only tool, no browser, no screenshot concept. |
| Translate | **Not used** — no `TranslationService` reference. |
| llms.txt | **Not used** — no `prefer_llms_txt`-equivalent or well-known-file check in `MapService`. |
| Feeds | **Not used** — no RSS/Atom detection path; a feed URL passed to `occam_map` would be parsed as generic HTML/XML by whichever source path applies (sitemap XML parsing might accidentally partially work on an RSS/Atom XML doc since both use generic `<loc>`-less XML, but this was not verified as intentional — see Unresolved). |
| Profile (session profile) | Covered under Session/Cookies above (**CAP-527**). |
| Env | `OCCAM_ALLOW_PRIVATE_URLS` (SSRF escape hatch, Wave-1 **CAP-156**) is the only environment variable found to affect `occam_map` behavior directly; no map-specific env vars exist (no `OCCAM_MAP_*` knobs). |

---

## SSRF / security summary

- **URL-shape pre-check:** `FetchPreflight.Prepare` → `PrivacyClassifier.Classify` rejects
 `localhost`/`.local`/`.internal`/literal private IPs before any fetch (Wave-1 **CAP-150/155**),
 overridable only via the global `OCCAM_ALLOW_PRIVATE_URLS=1` (Wave-1 **CAP-156**).
- **Connection-level guard (the layer that actually matters for redirects/DNS-rebinding):** both
 `HttpProbeFetcher` clients (`probe.redirectTracking`, `probe.autoRedirect`) are registered with
 `SocketsHttpHandler.ConnectCallback = OutboundHttpGuard.ConnectAsync`
 (`Composition/OccamServiceCollectionExtensions.cs`), which resolves DNS itself, rejects any private
 IPv4/IPv6 answer, and pins the socket connection to the validated address — this fires on **every
 TCP connection**, meaning every redirect hop (whether followed by the manual `HttpRedirectFollower`
 loop or the handler's own `AllowAutoRedirect=true` path) and every sitemap/robots/hub fetch is
 independently SSRF-validated at connect time, not just the original seed URL. This matches Wave-1
 **CAP-154/184**'s finding for the Core probe client family and is confirmed here to cover
 `occam_map`'s call sites specifically (`HttpProbeFetcher.FetchAsync`, used by `MapService` for
 homepage/hub/robots/sitemap fetches alike).
- **No app-level per-hop `PrivacyClassifier` re-check** exists in `HttpRedirectFollower.FollowAsync`
 itself (unlike the transcode audit's claim for the Node HTTP worker's `<meta refresh>` path,
 Wave-1 **CAP-152**) — but this is **immaterial** here because the connect-level guard
 (`OutboundHttpGuard`) independently re-validates every hop's actual TCP connection regardless of
 app-level awareness, making a hostname-based bypass via redirect equally closed.
- **Path traversal:** `session_profile` IDs go through the same `SessionProfileHeaders` sanitizer as
 `occam_transcode` (Wave-1/transcode **CAP-069**) — no map-specific weakening found.
- **Resource exhaustion:** per-fetch-kind byte caps (**CAP-523**) + shared discovery deadline
 (**CAP-513**) bound both memory and wall-clock cost of a single `occam_map` call; `MaxSitemapFetches
 = 4` bounds sitemap-index fan-out (no unbounded recursive sitemap-of-sitemaps crawl).

---

## Failure code catalog for `occam_map`

| Code | Source | Notes |
|---|---|---|
| `invalid_arguments` | `OccamMapTool` (max_links/timeout_ms range) or `MapService.NormalizeSource` (bad `source`) | Tool-level reject happens before service call for the numeric params |
| `invalid_url` | `MapService.MapAsync` (`Uri.TryCreate` fails) | Distinct from `invalid_arguments`, not reused |
| `private_url_blocked` | `FetchPreflight`/`PrivacyClassifier` (**CAP-150/156**) | Global-only override |
| `unsupported_content_type` | PDF URL pre-check (**CAP-528**) or non-HTML homepage response | |
| `sitemap_not_found` | `SitemapDiscovery` returned zero links, not a timeout | Agent hint: retry with `source=homepage` |
| `thin_extract` | Homepage HTML yielded zero same-domain links after filtering | Agent hint (generic, cross-tool): retry via `occam_transcode` browser backend |
| `timeout` | `HttpProbeFetcher`/`SitemapDiscovery` deadline exceeded with zero usable links | |
| `http_4xx`/`http_5xx` (via `FailureCodeStrings.Normalize`) | Homepage fetch non-success status | |
| session profile failure codes (invalid id / not found) | Shared `SessionProfileHeaders` (Wave-1 **CAP-167**) | Same codes as `occam_transcode` |

---

## Capability graph edges

```
TOOL:occam_map|USES|CAP-510
TOOL:occam_map|USES|CAP-511
TOOL:occam_map|USES|CAP-512
TOOL:occam_map|USES|CAP-513
TOOL:occam_map|USES|CAP-514
TOOL:occam_map|USES|CAP-515
TOOL:occam_map|USES|CAP-516
TOOL:occam_map|USES|CAP-517
TOOL:occam_map|USES|CAP-518
TOOL:occam_map|USES|CAP-519
TOOL:occam_map|USES|CAP-520
TOOL:occam_map|USES|CAP-521
TOOL:occam_map|USES|CAP-522
TOOL:occam_map|USES|CAP-523
TOOL:occam_map|USES|CAP-524
TOOL:occam_map|USES|CAP-525
TOOL:occam_map|USES|CAP-526
TOOL:occam_map|USES|CAP-527
TOOL:occam_map|USES|CAP-528
TOOL:occam_map|USES|CAP-150
TOOL:occam_map|USES|CAP-154
TOOL:occam_map|USES|CAP-156
TOOL:occam_map|USES|CAP-167
TOOL:occam_map|USES|CAP-169
TOOL:occam_map|USES|CAP-170
TOOL:occam_map|USES|CAP-184
TOOL:occam_map|USES|CAP-188
TOOL:occam_map|USES|CAP-189
TOOL:occam_map|USES|CAP-191
PARAM:source|ENABLES|CAP-511
PARAM:source|ENABLES|CAP-512
PARAM:focus_query|ENABLES|CAP-515
PARAM:focus_query|ENABLES|CAP-516
PARAM:focus_query|ENABLES|CAP-517
PARAM:focus_query|ENABLES|CAP-518
PARAM:same_domain|ENABLES|CAP-519
PARAM:filter_nonsense|ENABLES|CAP-514
PARAM:session_profile|ENABLES|CAP-527
PARAM:timeout_ms|ENABLES|CAP-513
CAP-510|ROUTES_TO|HttpProbeFetcher
CAP-511|CONSUMES|html_response
CAP-512|ROUTES_TO|HttpProbeFetcher
CAP-512|PRODUCES|MappedLink[]
CAP-513|FALLS_BACK_TO|partial_results
CAP-515|CONSUMES|FocusQueryDecomposition
CAP-516|ROUTES_TO|HttpProbeFetcher
CAP-521|ROUTES_TO|occam_digest
CAP-522|ROUTES_TO|occam_transcode
CAP-522|ROUTES_TO|occam_map
CAP-527|CONSUMES|session
CAP-528|CONSUMES|CAP-059
CAP-529|ROUTES_TO|occam_digest
CAP-184|PRODUCES|ssrf_guard
CAP-154|PRODUCES|ssrf_guard
```

---

## HIDDEN / NON-OBVIOUS CAPABILITIES

Which capabilities would a user **never** discover from the short MCP description
("Discover a site's same-domain links from its homepage, sitemap, or robots.txt (HTTP-only, up to 64
URLs)…")?

1. **CAP-516/517/518** — `focus_query` silently triggers up to 3 *extra* page fetches (hub expansion)
 and over-fetches up to 200 raw candidates internally when the seed page doesn't score a strong hit
 — a caller reading only the parameter description ("Optional focus keywords…") would not expect
 network-fetch amplification from this one string parameter.
2. **CAP-527** — `session_profile` is real but **headers-only** on this tool; a session profile that
 works perfectly for `occam_transcode`'s browser backend (storageState-based) can be a complete
 no-op here. Nothing in the parameter description ("loads headers from …") signals this narrower
 scope versus the fuller session support implied by the same parameter name on other tools.
3. **CAP-521** — every successful response nudges the caller toward `occam_digest` regardless of
 result quality or size; this is a fixed recommendation, not a computed one.
4. **CAP-514** — `filter_nonsense=true` (the default) silently drops nginx.org changelog pages via a
 hardcoded per-host rule — the only site-specific carve-out in the tool, invisible unless reading
 `MapLinkFilter.IsNonsense` directly.
5. **CAP-515** — the ranker is not "search relevance" in the generic sense; it actively **penalizes**
 version/changelog-shaped URLs unless the query itself looks version-related, which can surprise a
 caller who genuinely wants "what changed in 3.12" style results without quoting version numbers.
6. **CAP-522** — a `thin_extract` failure from `occam_map` recommends retrying a **different tool**
 (`occam_transcode` with `backend_policy=browser`) rather than anything `occam_map` itself can do,
 since map has no browser escalation path of its own (**CAP-510**).

---

## Unresolved items

- Whether an RSS/Atom feed URL passed as `source=sitemap`'s seed would partially "work" by accident
 (both are generic XML and the sitemap parser's regex fallback only looks for `<loc>`, which feeds
 don't have) — likely a clean `sitemap_not_found`/empty result, but not explicitly tested this audit.
- Exact interaction between `filter_nonsense=false` and `MapSoft404Filter`/nginx-changelog carve-outs
 when combined with `source=sitemap` vs `source=homepage` — code path is shared (`IsNonsense` gates
 all of it identically for both sources), but no live corpus run was performed to confirm real-world
 sitemap soft-404 rates.
- Whether `same_domain=false` combined with `source=sitemap` could be used to enumerate arbitrary
 external hosts referenced in a sitemap at scale (SSRF is still guarded per-connection via
 `OutboundHttpGuard`, so this is a discovery-scope question, not a security hole) — not exercised
 against a real multi-host sitemap this audit.
