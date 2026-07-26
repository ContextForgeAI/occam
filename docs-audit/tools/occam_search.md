# `occam_search` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/Tools/OccamSearchTool.cs`,
`src/FFOccamMcp.Core/Tools/OccamSearchModels.cs`, `src/FFOccamMcp.Core/Tools/SearchExtractabilityScorer.cs`,
`src/FFOccamMcp.Core/Services/SearchService.cs`, `src/FFOccamMcp.Core/Search/*.cs`,
`src/FFOccamMcp.Core/Composition/OccamServiceCollectionExtensions.cs`,
`src/FFOccamMcp.Core/Transport/OccamToolProfile.cs`). Docs (`docs/*.md`, `MCP_API_SPEC.md`) were **not**
used as evidence.

**CAP ID range owned by this audit:** `CAP-620`–`CAP-649` (used: CAP-620…631).

---

## 0. Entry point and schema

`OccamSearchTool.Search` (`Tools/OccamSearchTool.cs`), MCP name `occam_search`. Parameters:

```
query (required, string), max_results (int, default 8, range 1-20), rerank (bool, default false)
```

Constructor-injected: `ISearchService searchService`, `ProbeService probeService` — the tool directly
depends on the probe subsystem for its `rerank` opt-in, not just on the search layer.

Included in **all** tool profiles including the narrowest (`reader`) — confirmed in
`Transport/OccamToolProfile.cs` `ReaderTools` array. `occam_search` is treated as core discovery, not an
advanced/auditor-only capability.

---

## CAP-620 — `query` (required input) + validation

**Trace:** `OccamSearchTool.Search` rejects `null`/whitespace `query` with `invalid_arguments` before
calling the search service. No URL/privacy validation applies here (query is free text, not a URL) — this
tool has no `PrivacyClassifier`/SSRF surface of its own on the query path (see CAP-627 for where privacy
checks re-enter via `rerank`).

## CAP-621 — `max_results` bounds (1–20, default 8)

**Trace:** `DefaultMaxResults = 8`, `MaxResultsCap = 20` (consts in `OccamSearchTool.cs`). Values outside
`[1,20]` return `invalid_arguments` before any provider call. Each provider independently re-applies
`.Take(maxResults)` after deserializing its own response (defense in depth against a provider returning
more hits than requested).

## CAP-622 — Provider selection via `OCCAM_SEARCH_PROVIDER` (off by default)

**Evidence:** `Services/SearchService.cs` `ResolveProvider()`. Reads `OCCAM_SEARCH_PROVIDER` env var,
matches case-insensitively against the three registered `ISearchProvider` names (`searxng`, `brave`,
`tavily`). **Unset or unknown name → tool is fully unconfigured** (`ISearchService.IsConfigured=false`),
and every call fails with `search_unconfigured` — there is no default provider and no keyless
out-of-the-box search; this is the single required env var for the tool to do anything at all.

Per-provider secondary requirement gating (checked in the same `ResolveProvider()` method, not deferred to
the provider itself):
- `RequiresApiKey=true` (Brave, Tavily) → also requires non-blank `OCCAM_SEARCH_API_KEY`, else treated as
  unconfigured (same `search_unconfigured` code, not a distinct "key missing" code).
- `RequiresBaseUrl=true` (SearXNG only) → also requires non-blank `OCCAM_SEARCH_URL`.

**Required env to make `occam_search` do anything:** `OCCAM_SEARCH_PROVIDER` **plus** exactly one of
`OCCAM_SEARCH_URL` (searxng) or `OCCAM_SEARCH_API_KEY` (brave/tavily).

## CAP-623 — SearXNG provider (self-hosted meta-search, keyless)

**Evidence:** `Search/SearxngProvider.cs`. `RequiresApiKey=false`, `RequiresBaseUrl=true`. GET
`{OCCAM_SEARCH_URL}/search?q=<query>&format=json`. Notably the code path **also** attaches an
`Authorization: Bearer {apiKey}` header if `OCCAM_SEARCH_API_KEY` happens to be set even though
`RequiresApiKey=false` — i.e. SearXNG can optionally be run behind an auth-gated proxy using the same key
env var, without that being a hard requirement. Non-2xx response → `search_http_<statuscode>`. Response
mapped from `{results:[{url,title,content}]}`; entries with blank `url` are dropped before `.Take()`.

## CAP-624 — Brave Search provider

**Evidence:** `Search/BraveProvider.cs`. `RequiresApiKey=true`, `RequiresBaseUrl=false`. Default base
`https://api.search.brave.com` (overridable via `OCCAM_SEARCH_URL` even though Brave doesn't strictly
require a base URL — the override is honored if present). GET `/res/v1/web/search?q=…&count=<max_results>`
with `X-Subscription-Token: <OCCAM_SEARCH_API_KEY>` header (not `Authorization: Bearer`, a
provider-specific header scheme). Maps `{web:{results:[{title,url,description}]}}`.

## CAP-625 — Tavily provider (AI-agent search API)

**Evidence:** `Search/TavilyProvider.cs`. `RequiresApiKey=true`, `RequiresBaseUrl=false`. Default base
`https://api.tavily.com`. **POST** (only provider using POST, not GET) `/search` with JSON body
`{query, max_results}` and `Authorization: Bearer <key>`. Maps `{results:[{title,url,content}]}`.

## CAP-626 — Provider failure taxonomy + timeout

**Evidence:** `Search/SearchModels.cs` (`SearchElapsed`), `Composition/OccamServiceCollectionExtensions.cs`
line 89-90. Failure codes surfaced to the caller (via `DescribeFailure` in the tool):
- `search_unconfigured` — no provider/key/url resolved (CAP-622).
- `search_timeout` — provider call exceeded `OCCAM_SEARCH_TIMEOUT_MS` (default 20000ms, clamped
  1000–120000) or was cancelled; mapped from `TaskCanceledException`/`OperationCanceledException`.
- `search_http_<code>` — provider returned non-2xx; the numeric HTTP status is embedded directly in the
  failure code string (not a separate field), and `DescribeFailure` re-parses that suffix to build a
  human message ("Search backend returned `<code>`. Check the endpoint/key.").
- `search_error` — any other exception (deserialization failure, network error, etc.) — generic catch-all,
  not further subdivided (unlike `occam_transcode`'s `dns_error`/`tls_error`/`network_error` split — this
  tool has a materially coarser failure taxonomy than transcode/probe).

All three providers share `SearchElapsed.Ms`/`FailureFor`/`Trim` helpers for consistent latency
measurement and title/snippet whitespace normalization (collapses embedded `\n`/`\r`, trims, and coerces
empty-after-trim to `null`/`""`).

## CAP-627 — `rerank` (opt-in extractability-based reordering via live probe fan-out)

**Evidence:** `OccamSearchTool.RerankAsync`, `Tools/SearchExtractabilityScorer.cs`. Off by default
(`rerank=false` — a plain search costs one provider HTTP call; `rerank=true` adds up to
`max_results` **additional live HTTP HEAD/GET-class probes**, one per result URL). This is the single most
expensive/least obvious opt-in on the tool — its short description mentions "extra probe latency" but not
that it is a full `ProbeService.AnalyzeAsync` invocation per result (same subsystem `occam_probe` itself
uses), reusing:
- `FetchPreflight.Prepare` → `PrivacyClassifier.Classify` — **CAP-100 (reused from `occam_transcode`
  audit)** — so a search result URL pointing at a private/loopback/RFC1918 host is independently
  re-validated and probed as blocked (`preflight.Ok=false` → `ProbeAnalysis.Ok=false` with a failure code)
  rather than trusted just because it came back from a search provider.
- `HttpProbeFetcher.FetchAsync` (same fetcher `occam_probe` uses) with a **fixed 6000ms per-probe timeout**
  (`RerankProbeTimeoutMs` const) — not configurable via any `occam_search` parameter or env var, unlike
  `occam_probe`'s own tool-level timeout parameter.

**Concurrency:** bounded to `RerankMaxParallel = 5` via `SemaphoreSlim` — a hardcoded fan-out cap, not
env-configurable. All `max_results` probes are still attempted (just throttled to 5-in-flight), so
`rerank=true, max_results=20` issues up to 20 sequential-batched probe calls even though the tool
description implies a lightweight "cheap probe."

**Fault tolerance:** a probe exception for one result (anything other than caller cancellation) is
**swallowed** and that result is kept with a fixed fallback score `0.4` and `RecommendedBackend=null`
rather than being dropped from the result set or failing the whole `occam_search` call — reranking degrades
gracefully per-result, never drops a hit outright.

**Sort semantics:** stable sort by score descending, ties broken by **original provider rank** (not by
title/URL) — `OrderByDescending(Score).ThenBy(OriginalIndex)`, confirmed via the `Rank` field captured
before reordering.

## CAP-628 — `SearchExtractabilityScorer` (deterministic probe→score mapping)

**Evidence:** `Tools/SearchExtractabilityScorer.cs`. Pure function, no network, consumes a `ProbeAnalysis`
(from CAP-627's live probe) and returns a score in `[0,1]` via a fixed decision tree (not a learned model):

| Score | Condition |
|---|---|
| `0.0` | probe failed / HTTP ≥400 / any `FailureCode` set (includes private-URL-blocked results — CAP-100 reuse) |
| `0.05` | challenge/anti-bot page detected (`classification.Challenge is not null`) |
| `0.15` | login/paywall likely (`Signals.LikelyLoginRequired`) |
| `0.3` | non-HTML/binary (`RecommendedBackend == "none"`) |
| `0.45` | JS-heavy stub: `VisibleTextRatio` in `(0, 0.08)` **and** `ScriptDensity > 0.5` |
| `0.55` | recommender says `browser` (JS-heavy but not stub-level) |
| `0.9` | clean HTTP-extractable page classified as `docs`/`article`/`reference`/`blog` |
| `0.7` | clean HTTP-extractable page, any other page class (default "good" bucket) |

This directly reuses `occam_probe`'s own classification fields (`ProbeAnalysis.Classification`,
`PageClass`, `Signals.LikelyLoginRequired`, `VisibleTextRatio`, `ScriptDensity`, `RecommendedBackend`) —
the scorer is a thin policy layer on top of probe's existing signal set, minting no new detection logic of
its own. Rounded to 2 decimal places before being placed in the response (`Math.Round(score, 2)`).

## CAP-629 — Rerank output annotation (`extractability` + `recommendedBackend` fields)

**Evidence:** `Tools/OccamSearchModels.cs` (`OccamSearchResultInfo`). Both fields are declared
`JsonIgnore(Condition = WhenWritingNull)` — meaning a **plain (non-rerank) search response never has these
keys present at all** (not `null`-valued, absent entirely), so a caller cannot distinguish "rerank was off"
from "rerank ran but a probe hard-failed" purely by checking for key presence on an individual result vs.
the top-level `agentHints.suggestedNext` text (CAP-630) — the per-result absence is the same in both cases
only for results where an exception path forced `RecommendedBackend=null` (CAP-627's fallback), which is
distinguishable from the null-omission because `extractability` (0.4) is present but `recommendedBackend`
is dropped.

## CAP-630 — `agentHints.suggestedNext` (result-set-aware next-step text)

**Evidence:** `OccamSearchTool.Search` response construction. Three distinct hint strings depending on
outcome, not a single static string:
- Zero results → `"refine query or try another provider"`.
- Results present, `rerank=false` → `"occam_transcode (fetch a result URL) or occam_digest (compare
  several)"`.
- Results present, `rerank=true` → `"results reranked by extractability — prefer the top (highest
  extractability) URLs for transcode"` (overrides the plain-search hint even when results exist).

This is a lightweight but genuine per-response agent-routing signal — distinct from a generic "next steps"
doc string, computed from the actual outcome of this specific call.

## CAP-631 — Response shape (`{ok, query, provider, count, results[], agentHints}`)

**Evidence:** `OccamSearchSuccessResponse`/`OccamSearchFailureResponse` (camelCase via
`JsonSourceGenerationOptions`). Success includes the **resolved provider name** (`outcome.Provider`, e.g.
`"searxng"`) even though the caller never specified which provider ran — this is the only place a caller
can learn which backend actually served the query (useful when an operator has `OCCAM_SEARCH_PROVIDER` set
to something the caller doesn't know about). `count` is `results.Length` post-any-truncation, i.e. it
reflects what's actually in the array, not a raw provider-reported total-hits number (none of the three
wire models expose a total-count field upstream in the first place).

---

## Failure code catalog for `occam_search` (consolidated)

| Code | Source | Retryable? |
|---|---|---|
| `invalid_arguments` | tool-level (empty query / `max_results` out of `[1,20]`) — CAP-620/621 | No |
| `search_unconfigured` | `SearchService.ResolveProvider` — missing/unknown provider or missing required key/url — CAP-622 | No (fix config) |
| `search_timeout` | provider HTTP client timeout (`OCCAM_SEARCH_TIMEOUT_MS`) or cancellation — CAP-626 | Yes |
| `search_http_<nnn>` | provider returned non-2xx — CAP-626 | Depends on `<nnn>` |
| `search_error` | any other provider-call exception (deserialize/network/etc.) — CAP-626 | Sometimes |

Note: `rerank=true` **never** turns a successful search into a failure — probe failures during reranking
are absorbed per-result (CAP-627), not surfaced as a tool-level error.

---

## Capability graph edges

```
TOOL|USES|CAP-620
TOOL|USES|CAP-621
TOOL|USES|CAP-622
TOOL|USES|CAP-626
TOOL|USES|CAP-630
TOOL|USES|CAP-631
PARAM:query|ENABLES|CAP-620
PARAM:max_results|ENABLES|CAP-621
PARAM:rerank|ENABLES|CAP-627
CAP-622|ROUTES_TO|CAP-623
CAP-622|ROUTES_TO|CAP-624
CAP-622|ROUTES_TO|CAP-625
CAP-623|CONSUMES|env:OCCAM_SEARCH_URL
CAP-624|CONSUMES|env:OCCAM_SEARCH_API_KEY
CAP-625|CONSUMES|env:OCCAM_SEARCH_API_KEY
CAP-626|CONSUMES|env:OCCAM_SEARCH_TIMEOUT_MS
CAP-627|USES|CAP-100
CAP-627|ROUTES_TO|ProbeService.AnalyzeAsync
CAP-627|PRODUCES|CAP-628
CAP-628|CONSUMES|ProbeAnalysis
CAP-628|PRODUCES|CAP-629
CAP-629|PRODUCES|artifact:extractability_score
TOOL|FALLS_BACK_TO|none (no backend cascade; single provider call, no retry-on-same-provider)
CAP-627|ROUTES_TO|occam_probe-subsystem
```

---

## Cross-cutting categories checked

- **proxy** — not used directly by `occam_search`/providers (no `OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY`
  reference in `Search/*.cs` or `SearchService.cs`); the shared `HttpClientFactory`-created client
  (`occam.search` named client) does not configure a proxy handler in
  `OccamServiceCollectionExtensions.cs` the way the egress-proxy-aware workers do. **Not used** for the
  search HTTP calls themselves. The `rerank` probe path reuses `HttpProbeFetcher`, whose own proxy
  behavior is out of this tool's scope (belongs to the probe subsystem's own audit).
- **session** — no `session_profile` parameter on `occam_search` at all; `rerank`'s internal
  `ProbeService.AnalyzeAsync` call passes `sessionProfile: null` explicitly (default), so reranking never
  uses an authenticated session even if one exists elsewhere. **Not used.**
- **cookies/headers** — SearXNG optionally sends a Bearer token (CAP-623); Brave sends
  `X-Subscription-Token`; Tavily sends `Authorization: Bearer`. No cookie handling anywhere in the search
  layer. **Auth headers only, no cookies.**
- **http/browser backend split** — `occam_search` itself makes only plain `HttpClient` calls (no browser
  worker involvement at all for the search step); `rerank` indirectly references `RecommendedBackend`
  strings (`http`/`browser`/`none`) as **data** from probe classification but never launches a browser
  itself.
- **managed** — no interaction with `ManagedExtractBackend`/third-party scraping providers. **Not used.**
- **retry** — no same-provider retry-on-failure logic found in any of the three providers or
  `SearchService`; a failed search returns the failure immediately. **Not used** (contrast with
  `occam_transcode`'s backend cascade).
- **cache** — no caching layer for search results (no `TranscodeResponseCache`-equivalent). Every call is
  a live provider hit. **Not used.**
- **diff** — no `diff_against`/`if_none_match`-style parameter. **Not used.**
- **blocks/tables/chunks** — search results are flat `{title,url,snippet}` triples; no block/table/chunk
  materialization. **Not used.**
- **budget** — no `max_tokens`/token-budget integration; `max_results` is a **count** cap, not a token
  budget, and there's no `ClientCapabilityStore` consultation anywhere in `OccamSearchTool`. **Not used.**
- **receipts/merkle/capsules** — no receipt signing on search responses. **Not used.**
- **playbooks** — no playbook resolution touches the search path. **Not used.**
- **datasets/claims** — no `occam_dataset_export`/`occam_claim_check` linkage in code (only mentioned as
  a *suggested next tool* in agent hints text elsewhere, e.g. `OccamServerInstructions.cs`, not a code
  dependency). **Not used** (edge only, see below).
- **trust tags** — no `tag_trust`-equivalent on search results. **Not used.**
- **screenshots** — not applicable; no browser rendering. **Not used.**
- **translate** — no `translate_to`-equivalent. **Not used.**
- **llms.txt** — no `prefer_llms_txt`-equivalent. **Not used.**
- **feeds** — no RSS/Atom handling. **Not used.**
- **profile (`OCCAM_PROFILE`)** — `occam_search` is included in **every** tool profile including `reader`
  (the narrowest) — see `Transport/OccamToolProfile.cs` `ReaderTools`. Confirms it's treated as a baseline
  discovery primitive, not gated behind researcher/auditor.
- **env** — four env vars total: `OCCAM_SEARCH_PROVIDER`, `OCCAM_SEARCH_URL`, `OCCAM_SEARCH_API_KEY`,
  `OCCAM_SEARCH_TIMEOUT_MS` — all four already catalogued in `docs-audit/ENVIRONMENT-VARIABLES.md`
  (Wave 1, lines 128-131); this audit confirms no additional undocumented env var exists in the search
  code path.

---

## Hidden / non-obvious capabilities

Capabilities a user would **never** discover from the tool's one-line MCP description
(`"Open-web search (query -> result URLs) via a configured backend (SearXNG/Brave/Tavily)... Requires
OCCAM_SEARCH_PROVIDER. Returns { title, url, snippet }."`):

1. **CAP-627/628** — `rerank=true` is not a cheap re-sort of already-known data; it launches up to
   `max_results` (≤20) **full live HTTP probes** against the result URLs themselves, reusing the entire
   `occam_probe` classification pipeline (challenge/login/JS-density detection), bounded to 5 concurrent
   with a hardcoded 6s-per-probe timeout that cannot be tuned from the `occam_search` call.
2. **CAP-627** — those rerank probes independently re-run SSRF/private-URL protection (CAP-100) against
   every result URL, so a malicious or misconfigured search backend returning an internal/private URL gets
   silently scored `0.0` and sorted to the bottom rather than trusted.
3. **CAP-623** — the "keyless" SearXNG provider will still send a Bearer auth header if
   `OCCAM_SEARCH_API_KEY` happens to be set for an unrelated reason (e.g. an operator also configured
   `OCCAM_SEARCH_API_KEY` for a different purpose) — there's no provider-scoped key namespace, all three
   providers read the exact same single env var.
4. **CAP-629** — `extractability`/`recommendedBackend` fields are entirely absent (not null) from JSON
   when `rerank=false` — an agent parsing the schema without testing both modes could reasonably assume
   these fields always exist.
5. **CAP-622** — a wrong/misspelled `OCCAM_SEARCH_PROVIDER` value produces the *same* generic
   `search_unconfigured` failure as having no provider configured at all — no "unknown provider name: X,
   expected one of searxng|brave|tavily" diagnostic distinguishes a typo from total non-configuration.
6. **CAP-626** — coarser failure taxonomy than sibling tools: no `dns_error`/`tls_error`/`network_error`
   split — everything that isn't a timeout or an HTTP status collapses into one generic `search_error`.

---

## Unresolved items

- Whether `OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY`/`OCCAM_NO_PROXY` (egress proxy, CAP-102 from the
  `occam_transcode` audit) apply to the `occam.search` named `HttpClient` — the DI registration
  (`OccamServiceCollectionExtensions.cs` line 89) sets only a `Timeout`, with no
  `ConfigurePrimaryHttpMessageHandler` proxy wiring visible at that registration site (contrast with the
  robots-throttle client immediately below it, which does call `ConfigurePrimaryHttpMessageHandler`) —
  read as **not proxy-aware**, but the full `IHttpClientFactory` default-handler configuration chain
  (possible global `HttpClient` defaults elsewhere in the composition root) was not exhaustively traced to
  rule out an ambient proxy applying framework-wide.
- Whether `rerank`'s underlying `ProbeService.AnalyzeAsync` / `HttpProbeFetcher` calls are subject to
  `OCCAM_RESPECT_ROBOTS`/`OCCAM_HOST_THROTTLE_MS` (CAP-103) — not verified from `Tools/OccamSearchTool.cs`
  alone; belongs properly to the `occam_probe` subsystem audit.
- Exact behavior when `OCCAM_SEARCH_PROVIDER=searxng` but the instance's `/search?format=json` is disabled
  server-side (a common SearXNG hardening default) — expected to surface as `search_http_403` or a
  non-JSON body causing `search_error`, but not independently confirmed against a live instance in this
  code-only audit.
