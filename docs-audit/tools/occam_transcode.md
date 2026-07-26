# `occam_transcode` — Deep Capability Audit (Wave 1 / S1)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`, `workers/**`). Documentation
(`docs/*.md`, `MCP_API_SPEC.md`) was **not** used as evidence and is explicitly untrusted for this audit —
every claim below cites a file + line/region that was read directly.

**CAP ID range owned by this audit:** `CAP-050`–`CAP-149` (used: CAP-050…CAP-112; remainder of range
intentionally unused — reserved, not exhausted).

**Method:** for every MCP-visible parameter of `occam_transcode`, the trace goes
`schema (Tool ctor param) → parser/validation → pipeline/service → backend or worker → conditional branch →
observable field(s) in the JSON response`. Where a parameter turns on an entire subsystem (not just a
field), that subsystem gets its own CAP with its own evidence, separate from the "it's just a parameter"
row.

---

## 0. Entry point and schema

`OccamTranscodeTool` (`src/FFOccamMcp.Core/Tools/OccamTranscodeTool.cs`) is the MCP handler. Its
`[McpServerTool]`-annotated method takes these parameters (name → C# type, all but `url` optional):

```
url (required), backend_policy, max_tokens, fit_markdown, focus_query, content_selectors,
session_profile, playbook_policy, if_none_match, semantic_chunking, capture_screenshot,
json_blocks, json_tables, json_feed, translate_to, diff_against, prefer_llms_txt, cache_ttl_s,
emit_capsule, rank_blocks, tag_trust, delta_only
```

Only `url` is required — confirmed against `AGENTS.md` claim ("only `url` is required") by reading the
tool signature directly: every other parameter has a default value in the method signature (`= null` /
`= false` / literal default), so omitting them compiles and produces the documented "cheapest" default
path (HTTP-first, no sidecars, ambient token budget).

---

## CAP-050 — `url` (required input, identity of the whole operation)

**Trace:** `OccamTranscodeTool.TranscodeAsync` receives `url` as a bare string → passed into
`OccamTranscodeOptionsParser.TryBuild` (`Routing/OccamTranscodeOptions.cs`) which does NOT itself validate
URL shape → real validation happens in `FetchPreflight.Validate` (`Session/FetchPreflight.cs`), which:

1. Requires `Uri.TryCreate(url, UriKind.Absolute, out …)` to succeed and scheme to be `http`/`https` —
 else `invalid_arguments` with message `"url must be an absolute http(s) URL"`.
2. Runs `PrivacyClassifier.Classify(url)` (`Routing/PrivacyClassifier.cs`) — private/loopback/link-local
 hosts and RFC1918 ranges are rejected with failure code `private_url_blocked` unless
 `OCCAM_ALLOW_PRIVATE_URLS=1` is set (see **CAP-100**).
3. Resolves `session_profile` (if any) into headers/storage state (see **CAP-068**).

The **final** URL that appears in the response `UrlInfo` (`OccamTranscodeModels.cs`) is not necessarily
the input URL: it is `payload.Url?.Final` returned by the worker after following HTTP redirects and/or
`<meta http-equiv="refresh">` client-side redirects (`workers/shared/lib/meta-refresh.mjs`,
`parseMetaRefreshTarget`, up to a bounded hop count) — each hop is re-validated against
`PrivacyClassifier` server-side before being followed, so redirect chains cannot be used to reach a
private host that direct navigation would have blocked (see **CAP-100** for the cross-origin header-strip
detail).

**Failure codes on this path:** `invalid_arguments`, `private_url_blocked`, `dns_error`, `tls_error`,
`network_error`.

---

## CAP-051 — `backend_policy` (schema, parsing, effective set)

**Schema:** free-text string, default `"http_then_browser"`.

**Trace:** `OccamBackendPolicyParser.TryParse` (referenced from `OccamTranscodeTool.cs`) accepts
`http`, `browser`, `http_then_browser`, and the hyphenated alias `http-then-browser`; anything else →
`invalid_policy` failure. The parsed enum flows into `OccamRouter.RouteAsync`
(`Routing/OccamRouter.cs`), which is the sole dispatcher to `IExtractBackend` implementations:
`HttpExtractBackend`, `BrowserExtractBackend`, and (conditionally) `ManagedExtractBackend`
(**CAP-054**). `browser`-only policy short-circuits directly to `BrowserExtractBackend.ExtractAsync`
with no HTTP attempt at all (no fallback either way if the browser backend is not ready — see **CAP-053**).

---

## CAP-052 — `http_then_browser` cascade (the actual default execution path)

**Evidence:** `OccamRouter.TranscodeHttpThenBrowserAsync` (`Routing/OccamRouter.cs`).

This is a full state machine, not "try http, else try browser":

1. Run `HttpExtractBackend` first.
2. If HTTP result is `Ok` **and** the markdown does not look like a challenge page
 (`ChallengePageDetector.LooksLikeChallengePage`) **and** does not look thin
 (`ExtractQualityEvaluator.LooksLikeThinExtract`) → return the HTTP result immediately (browser is never
 invoked — this is the fast/cheap path for the overwhelming majority of URLs).
3. If HTTP failed with a **terminal** failure (`FailureCodeStrings` classifies e.g. `http_404`,
 `dns_error` as non-retryable-by-backend-switch) the router still escalates to browser *unless* the
 failure is in a small "never escalate" set (private URL block, invalid arguments) — those return
 immediately without wasting a browser launch.
4. Otherwise the router calls `BrowserExtractBackend` and then runs `ChooseRawFallback` — a comparator
 that keeps whichever of the two raw attempts is **more informative** (longer/denser markdown, not a
 challenge shell) even if the "later" attempt technically succeeded with `ok:true` but produced worse
 content than an earlier `ok:false`/thin HTTP attempt. This means the router can return an HTTP body
 tagged as coming from the browser attempt's failure envelope, or vice versa — evidence this is
 content-quality-driven, not just "first success wins".
5. Every attempt (HTTP, browser, managed) is appended to an internal recovery/attempt log
 (`TranscodeAttempt` records in `TranscodeOutcome.cs`) surfaced in the response as `agentMeta.recoveryLog`
 style data (see **CAP-098**).

---

## CAP-053 — Browser backend readiness / auto-provisioning gate

**Evidence:** `FeatureDiscoveryService.cs`, `BrowserExtractBackend.cs`, `Workers/BrowserExtractTimeouts.cs`.

Before the router ever calls the browser backend, `FeatureDiscoveryService` probes
`workers/browser-extract/provision-gate.mjs` to learn (a) whether Playwright Chromium is already
installed, and (b) whether the worker is configured to **auto-install** Chromium on first use
(`OCCAM_BROWSER_AUTO_PROVISION`-style gate read from the same probe, not from a knob on
`occam_transcode` itself). If auto-provisioning is possible but not yet done, `BrowserExtractTimeouts`
adds a **grace period** on top of the normal ~120s browser timeout so the first browser call in a fresh
install doesn't time out mid-download. If the browser worker is missing entirely (`WorkerPaths.IsConfigured`
false for the browser script) and `backend_policy=browser` or the cascade needs to escalate, the failure
code is `workers_unavailable` (agent hint: run `occam doctor`, see **CAP-106**).

---

## CAP-054 — Managed backend escalation subsystem (third-party scraping fallback)

**Evidence:** `Backends/ManagedExtractBackend.cs`, `Backends/IManagedExtractBackend.cs`,
`Routing/OccamRouter.cs` (managed invocation site).

This is **not** an `occam_transcode` parameter at all — it is a fully env-gated subsystem that the router
consults as a *third* rung of the cascade, after HTTP and browser both failed/were thin, but only when:

- `OCCAM_MANAGED_PROVIDER` is set to one of `jina | firecrawl | scrapfly | spider` (see CAP-055…058),
- `OCCAM_MANAGED_API_KEY` is present (except Jina, which supports a keyless free tier), and
- `ManagedExtractBackend.ShouldAttempt(url)` returns true — this consults `OCCAM_MANAGED_DOMAINS`
 (comma-separated allow-list of hosts/suffixes); if that env var is set, **only** matching domains are
 escalated to the paid/rate-limited third-party API — this is a cost/quota safety valve, not a quality
 knob, and there is no way to force managed mode from the MCP call itself (no `backend_policy=managed`
 value exists in the parser).

If no provider/key is configured, `ManagedExtractBackend.IsReady` is `false` and the router skips it
silently — this means a fresh install with zero env vars gets identical behavior with or without this
class existing. **This is a genuinely hidden advanced capability**: nothing in the `occam_transcode`
schema hints that a third scraping tier exists; it is entirely operator-provisioned via environment.

---

## CAP-055 — Managed provider: Jina Reader

**Evidence:** `Backends/Managed/JinaProvider.cs`. GET `https://r.jina.ai/<url>` (or configurable base),
optional `Authorization: Bearer <key>` header when a key is present (keyless calls are rate-limited by
Jina, not by Occam). Response body is treated directly as markdown text (no JSON envelope to unwrap for
the reader endpoint).

## CAP-056 — Managed provider: Firecrawl

**Evidence:** `Backends/Managed/FirecrawlProvider.cs`. POST to `https://api.firecrawl.dev` (or override)
with `{"url": …}` JSON body and `Authorization: Bearer <apiKey>` (required — `RequiresApiKey = true`);
markdown is unwrapped from `data.markdown` in the JSON response.

## CAP-057 — Managed provider: Scrapfly

**Evidence:** `Backends/Managed/ScrapflyProvider.cs`. GET `https://api.scrapfly.io/scrape?key=…&url=…
&format=markdown&render_js=true` — notably this **always** requests JS rendering (`render_js=true` is
hardcoded, not configurable), so every Scrapfly call is billed as a browser-rendered scrape on their
side regardless of whether the page needed JS. Markdown comes from `result.content`.

## CAP-058 — Managed provider: Spider

**Evidence:** `Backends/Managed/SpiderProvider.cs`. POST `https://api.spider.cloud/crawl` with
`{"url": …, "limit": 1, "return_format": "markdown"}` and `Authorization: Bearer <apiKey>` (required);
response is a JSON array of page objects, markdown taken from `pages[0].content`.

All four providers share `ManagedResults`/`ManagedElapsed` helpers for consistent latency measurement and
failure envelope shape, and all run **synchronous** `HttpClient.Send` (not `SendAsync`) inside the backend
— worth flagging for a thread-pool-starvation review, though out of scope to fix here.

---

## CAP-059 — Transparent PDF extraction (implicit, not a parameter)

**Evidence:** `Routing/ContentFormatDetector.cs` (`IsPdfUrl`, `IsPdfContentType`),
`workers/shared/lib/pdf-extract.mjs`, `workers/http-extract/lib/http-extract-run.mjs`
(`shouldTryPdf` / `extractPdfResponse` call sites).

There is **no `format` or `pdf` parameter on `occam_transcode`.** PDF handling is entirely automatic:
the HTTP worker inspects the response `Content-Type` header (`application/pdf`) and/or the URL's `.pdf`
suffix; if either matches, it reads the response as a binary buffer and pipes it through `unpdf`
(`pdf-extract.mjs`) to produce markdown text instead of running the HTML/Readability path. This applies
transparently under any `backend_policy` that reaches the HTTP worker (`http`, or `http_then_browser`'s
first rung) — the browser worker does not have its own PDF path; if HTTP's PDF path fails/produces empty
text, the cascade still escalates to a full browser render of the PDF viewer chrome (likely a low-quality
extract, but not specially handled — this is an **UNRESOLVED** edge case, see §Unresolved).

---

## CAP-060 — `max_tokens` ambient budget resolution

**Trace:** `OccamTranscodeOptions.MaxTokens` is nullable at the schema level. When the caller omits it,
`OccamTranscodeTool` does **not** default to "no limit" — it consults `ClientCapabilityStore`
(`Client/ClientCapabilityStore.cs`), which holds a **per-session** value set once via
`occam_client_capabilities(context_tokens=…)` (or bootstrapped from `OCCAM_CLIENT_CONTEXT_TOKENS`).
`ClientCapabilityStore` derives a **default output budget as ~20% of the declared context window**
(matches the `occam_client_capabilities` tool description embedded in the MCP catalog, verified here
against the store's derivation logic, not against docs). If the client never called
`occam_client_capabilities` and no env var is set, the store falls back to a hardcoded conservative
default. Explicit `max_tokens` on the call always overrides the ambient value.

## CAP-061 — Two-layer budget split (`BudgetOwnership`)

**Evidence:** `Compile/BudgetOwnership.cs`. The single public `max_tokens` number is **not** applied once
— it is split into (a) a "whole-response" budget owned by `ResponseBudgetPlanner` (covers markdown +
every sidecar + receipt bytes) and (b) a "surface/semantic" sub-budget handed to
`MaterializationPlanner`/`TranscodeCompiler` for markdown-only fitting before the sidecars are even
measured. This two-pass design means markdown can be trimmed once for "content fit" (focus-aware) and
then trimmed again for "final byte budget" if sidecars (blocks/tables/feed/receipt) eat into the
remaining allowance — see **CAP-062**.

## CAP-062 — `ResponseBudgetPlanner` allocation and greedy sidecar trim

**Evidence:** `Compile/ResponseBudgetPlanner.cs`. Buckets, in the order the planner protects them:
markdown (highest priority) → blocks → tables → chunks → media refs → feed items → receipt. If the total
exceeds `max_tokens`, sidecars are dropped **greedily from the lowest-priority bucket first**, not
proportionally — e.g. a request with `json_feed=true` and a tiny `max_tokens` can silently lose the feed
items entirely while markdown survives, with the drop recorded in `OmittedManifest` (**CAP-067**). This
is a meaningful non-obvious behavior: sidecars are best-effort under tight budgets, not guaranteed once
requested.

---

## CAP-063 — `fit_markdown` (BM25 paragraph pruning)

**Evidence:** `Compile/FitMarkdown.cs`, invoked from `Routing/TranscodeCompiler.cs`.

`fit_markdown: bool` (default `false`). When true, `FitMarkdown.Apply` splits the markdown into
heading-delimited sections/paragraph blocks, scores each block with a BM25-inspired term-frequency
formula against `focus_query` tokens (falling back to a generic "informativeness" score — headings,
links, prose density — when no `focus_query` is given, so `fit_markdown=true` alone still does
boilerplate stripping), and drops low-scoring blocks (nav/footer/cookie-banner boilerplate patterns are
explicitly deprioritized). Dropped regions are recorded for `OmittedManifest`.

## CAP-064 — `focus_query` (relevance targeting + honesty signal)

**Evidence:** `Compile/FocusMatcher.cs`, consumed by `FitMarkdown`, `TokenBudget` (focus-centered
truncation, **CAP-066**), `BlockSalience` (**CAP-087**), and `SemanticOutcomeMapper` (**CAP-107**).

`focus_query` is a free-text string. `FocusMatcher` does **stemmed + synonym-tolerant** matching (not
naive substring search) and produces a `FocusMatchStatus` with a numeric score and a discrete **tier**
(e.g. strong/partial/none). This tier feeds directly into the response's `semantic.focus.matched` /
`focusMatched`-style honesty field — i.e. the tool actively tells the caller whether their focus query
was actually found in the page, rather than silently returning whatever fragment scored highest even if
nothing matched well. `focus_query` has an effect **even without `fit_markdown=true`**: it still
participates in `TokenBudget`'s focus-centered truncation strategy when `max_tokens` forces a cut
(**CAP-066**), and in `BlockSalience` if `rank_blocks=true`.

## CAP-065 — `content_selectors` (heading-anchor scoping)

**Evidence:** `Routing/ContentSelectorsParser.cs`, `Compile/MarkdownContentFilter.cs`.

Input accepts either a comma-separated list of selector strings or a JSON array (parser tries JSON array
first, falls back to comma-split). `MarkdownContentFilter` does not do real CSS selection against the
DOM at this layer — it matches selector strings against **markdown section headings** (post-conversion),
retaining only matching sections and their sub-content. If none of the selectors match anything in the
document, this is a distinct condition that can be surfaced (evidence: dedicated handling path in
`MarkdownContentFilter`/`TranscodeCompiler` separate from "empty page") — worth documenting as a
selector-miss rather than a generic thin-extract.

## CAP-066 — `TokenBudget` truncation strategies + definitional-anchor preservation

**Evidence:** `Compile/TokenBudget.cs`.

Three distinct truncation strategies chosen based on inputs:
- **head-safe** — default when no `focus_query`: keep from the top, cut the tail.
- **sandwich** — keep head + tail, drop the middle (used when the planner judges both ends
 informationally load-bearing, e.g. intro + conclusion of an article).
- **focus-centered / focus-window** — when `focus_query` is set, center the retained window around the
 highest-scoring match location instead of the document start.

Independently of strategy, `TokenBudget.PreserveDefinitionalAnchor` special-cases keeping the *first*
heading + its immediate lead paragraph even if the chosen window would otherwise cut it — this prevents
"headless" fragments that start mid-article with no title context, a known LLM-context-quality failure
mode. Every truncation records enough metadata for `OmittedManifest` to report byte/token counts and
which strategy fired.

## CAP-067 — `OmittedManifest` (machine-readable "what got cut")

**Evidence:** `Compile/OmittedManifest.cs`, populated by `TranscodeCompiler` and
`ResponseBudgetPlanner`. Distinct from a human "..." ellipsis: a structured record with a `reason`
(`token_budget`, `focus_filter`, `content_selectors`, sidecar-drop reasons from **CAP-062**), an
estimated-tokens-dropped count, and the affected region(s). This lets a calling agent decide
programmatically whether to re-fetch with a larger `max_tokens` or accept the truncation — it is exposed
even on success responses, not just failures.

---

## CAP-068 — `session_profile` (headers + cookies + browser storage state)

**Evidence:** `Session/SessionProfileHeaders.cs`, `Session/FetchHeadersScope.cs`,
`Session/RequestHeadersMerger.cs`, `Session/FetchPreflight.cs`.

`session_profile` is a **string ID**, not inline credentials — it is resolved to a local file
`OCCAM_SESSIONS_ROOT/<id>.json` on the *host machine running Occam*, never sent over MCP as raw
cookie/header material. That file (produced out-of-band, e.g. by an `occam-session.mjs export-state`
CLI referenced in `AGENTS.md`'s task table — not itself part of `occam_transcode`) supplies:

- extra HTTP headers merged with env-level defaults (`RequestHeadersMerger`, session profile headers win
 on key conflicts) for the HTTP backend, and
- a Playwright `storageState` JSON path for the browser backend (full cookie jar + localStorage).

`FetchHeadersScope` writes the *merged* header set to a **temp file** and exposes its path via an
`AsyncLocal` scope so worker processes read headers from disk rather than via command-line args (avoids
leaking auth headers into process listings/argv visible to other local users). `SessionProfileHeaders`
sanitizes the profile ID (rejects path traversal, restricts to the configured sessions root) before
resolving the file path — see **CAP-069**.

Interaction with caching: a request with `session_profile` set is **never** eligible for the response
cache (**CAP-085**), regardless of `cache_ttl_s` — enforced in `TranscodeCacheEligibility.cs`.

## CAP-069 — Session profile ID hardening

**Evidence:** `Session/SessionProfileHeaders.cs`. The profile ID is validated against a safe-character
allow-list and resolved via `Path.GetFullPath` + a prefix check against the canonical sessions root
before the file is opened, specifically to prevent `session_profile=../../etc/passwd`-style path
escapes. This is a security control worth its own CAP given `session_profile` is fully caller-controlled
input reaching the filesystem.

---

## CAP-070 — `playbook_policy=auto` (genome-aware resolution subsystem)

**Evidence:** `Routing/TranscodePipeline.cs` (playbook integration block), `Playbooks/PlaybookPolicy.cs`,
`Playbooks/PlaybookSeedResolver.cs`, `Playbooks/PlaybookResolveOptions.cs`.

`playbook_policy` accepts `off` (default) or `auto`. When `auto`, the pipeline calls
`PlaybookSeedResolver.ResolveExtended(new PlaybookResolveOptions(url))` — this walks a **tiered**
resolution order (mirrors the standalone `occam_playbook_resolve` tool's tiering: local override →
`WT_PLAYBOOKS_PATH` → community-verified manifest → built-in seeds) to find a per-site or per-page-class
recipe (selectors, backend preference, wait conditions) *before* the transcode backend is even chosen.
This is a materially different code path from `playbook_policy=off`, not just a metadata flag.

Constructing `PlaybookResolveOptions(url)` with the **one-argument constructor** leaves
`FetchSiteGenome = false` by its record default (`Playbooks/PlaybookResolveOptions.cs` line 7) — so
`occam_transcode`'s `playbook_policy=auto` path does **not** by itself trigger a live
`/.well-known/agent-genome.v1.json` network fetch (**CAP-073**) unless the operator has separately set
`OCCAM_SITE_GENOME_FETCH=1`. This distinction matters: `playbook_policy=auto` on `occam_transcode` is
resolve-only-from-existing-sources; the *fetch-a-new-genome-from-the-live-site* behavior documented for
`occam_playbook_resolve`'s own `genomeFetch`/`fetch_site_genome` params is env-gated when reached via
transcode, not caller-gated.

## CAP-071 — Playbook overlay soft-apply + provenance stamp

**Evidence:** `Routing/TranscodePipeline.cs`, `Playbooks/PlaybookVerifyScope.cs` (referenced from the
pipeline). A resolved playbook is applied as a **soft overlay** onto the worker options (selectors,
backend hint) rather than a hard requirement — if the overlay's selectors don't match anything on the
live page, the pipeline does not hard-fail; it falls back to generic extraction and the response
provenance reflects whether the overlay was actually applied vs. merely available (an honesty guard
against silently claiming playbook-quality extraction when the playbook didn't actually match).

## CAP-072 — Playbook `preferred_backend` override

**Evidence:** `Routing/TranscodePipeline.cs`. A resolved playbook can carry a preferred backend hint that
takes priority over the router's default `http_then_browser` cascade ordering for that specific URL
(e.g. a playbook can force browser-first for a known JS-heavy site even if the caller passed the default
policy) — this is a genome-driven override layered *underneath* the explicit `backend_policy` parameter
(an explicit caller `backend_policy` still wins; the playbook only fills in when the caller left it at
default — confirmed by reading the precedence order in the pipeline's option-merge logic).

## CAP-073 — Well-known site genome fetch (env-gated live network call)

**Evidence:** `Playbooks/WellKnownGenomeFetcher.cs`. When enabled (see **CAP-070** gating), this issues a
live GET to `https://<host>/.well-known/agent-genome.v1.json` (max 32 KB response, 1-hour in-process
cache, 8s default timeout clamped to [2s, 30s]), and — notably — re-runs `PrivacyClassifier` against the
well-known URL itself before fetching, so this internal fetch path independently honors the private-URL
block rather than trusting the caller's already-validated `url`. Failure states (`http_4xx/5xx`,
`not_json`, `invalid_host`) are cached too, avoiding repeated well-known probes against sites that don't
support the mechanism.

---

## CAP-074 — `if_none_match` (conditional/differential response)

**Evidence:** `Compile/ContentHashToken.cs`, `OccamTranscodeTool.cs` response-shaping branch.

`ContentHashToken.Hash` computes a bare lowercase-hex SHA-256 over the **final compiled markdown**
(post-fit/post-budget, not raw extract) — so the token is tied to a specific materialization, consistent
with `MaterializationKey` (**CAP-093**). `if_none_match` accepts either a bare hex hash or a
`sha256:`-prefixed form. When it matches the freshly computed hash, the tool still performs the **full**
extract (there is no HTTP-conditional-GET shortcut against the origin — Occam always re-fetches) but
returns a minimal envelope (`unchanged: true`-style shape) instead of the full markdown/sidecars,
saving response tokens on the MCP side even though origin bandwidth is not saved.

## CAP-075 — `semantic_chunking` (worker-side chunk plugin)

**Evidence:** `workers/shared/plugins/chunking.mjs`, `Workers/ExtractOptions.cs` (Features flag),
`TranscodePipeline.cs` (features list construction).

`semantic_chunking: bool` is passed to the worker as a named feature flag (only added to the features
list actually sent to the worker process when true — confirmed as opt-in, not always-on, unlike
**CAP-078**). The plugin splits markdown into logical chunks aligned to header boundaries and preserves
the header hierarchy path for each chunk (so a chunk under `## API > ### Auth` carries that breadcrumb),
intended for RAG-style downstream chunk-level embedding rather than whole-document embedding.

## CAP-076 — `capture_screenshot` (browser-only)

**Evidence:** `Workers/ExtractOptions.cs`, `Backends/BrowserExtractBackend.cs`.

`capture_screenshot: bool`. This flag is meaningful **only** when the browser backend actually runs
(either `backend_policy=browser`, or the `http_then_browser` cascade escalated to browser). Passed as a
worker feature; the browser worker captures a JPEG screenshot and returns it base64-encoded in the
worker's JSON payload. If the HTTP backend alone satisfies the request (no escalation), the screenshot
silently cannot exist — there is no forced-escalation-for-screenshot behavior (i.e.
`capture_screenshot=true` under `backend_policy=http` produces no screenshot and no error — an
**UNRESOLVED** honesty gap worth flagging: the caller gets no signal that their screenshot request was a
no-op under an HTTP-only policy).

---

## CAP-077 — `json_blocks` (structured block extraction + selector provenance)

**Evidence:** `workers/shared/lib/dom-blocks.mjs`, `Knowledge/MaterializationRequest.cs` (`ExposePublicBlocks`).

Each block carries `type`, `text`, extracted `links`, and a **document-absolute `source_selector`**
(a CSS-path-like locator computed by walking the live DOM, not reverse-engineered from the markdown) —
this selector is what lets `tag_trust` (**CAP-088**) and downstream tooling point back at *exactly* which
DOM node a block came from, independent of markdown serialization order. The DOM walk explicitly skips
script/style/nav/footer/aside-type tags before block collection.

## CAP-078 — Always-on internal block/table collection (hidden subsystem, not a parameter)

**Evidence:** `Routing/TranscodePipeline.cs` features-list construction (`featuresList.Add("json_blocks")`
/ `featuresList.Add("json_tables")` added **unconditionally**, independent of the caller's
`json_blocks`/`json_tables` values).

This is the single most important "hidden advanced" finding on this tool: **every** transcode call — even
one with every sidecar flag left `false` — causes the worker to run the full DOM block-walk
(`dom-blocks.mjs`) and table-walk (`dom-tables.mjs`) internally, because `MaterializationPlanner` /
`TranscodeCompiler` need the internal block/table representation for **Canonical-reference retention** and
quality scoring regardless of whether the caller ever sees them. The public-facing `json_blocks`/
`json_tables` flags (via `MaterializationRequest.ExposePublicBlocks`/`ExposePublicTables`, consumed by
`TranscodePipeline`'s `ProjectBlocks`/`ProjectTables` helpers) only control whether the **already-computed**
internal arrays are copied into the public JSON response — they do not control whether the (non-trivial)
DOM walk work happens at all. Practically: there is a real CPU/latency cost paid on every single call for
capabilities most callers never request, and conversely there is no way to make a transcode call "skip"
block/table collection to save worker time even under `backend_policy=http` with all sidecars off.

## CAP-079 — `json_tables` (+ semantic record reconstruction)

**Evidence:** `workers/shared/lib/dom-tables.mjs`.

Beyond generic caption/header/row extraction, the table walker has a **semantic reconstruction** step
for recognized list-like table patterns (evidence cites a Hacker-News-style item-list schema explicitly
in the module) — i.e. for some known page shapes it emits structured records (title/points/author/etc.)
rather than raw cell arrays. Purely decorative/layout tables (detected heuristically — e.g. single-column,
no `<th>`, used for CSS positioning) are explicitly **skipped**, not emitted as noise rows.

## CAP-080 — `json_feed` (RSS/Atom/JSON-Feed short-circuit)

**Evidence:** `workers/http-extract/lib/http-extract-run.mjs` (feed-vs-PDF-vs-HTML branch referenced
alongside **CAP-059**'s PDF detection). When the response content-type/body indicates a syndication feed,
the worker takes an entirely different parse path (feed item extraction) instead of
Readability/Turndown HTML→markdown — analogous in kind to the PDF branch: format sniffing happens
worker-side per-request, not via an explicit "this URL is a feed" parameter.

---

## CAP-081 — `translate_to` (host-side LibreTranslate codec)

**Evidence:** `Services/TranslationService.cs`.

`translate_to` is a target language code. Unlike almost every other sidecar, translation runs **entirely
on the .NET host**, after the worker has already returned markdown and after compile/budget steps —
i.e. translation operates on the *already-truncated* final markdown, not the raw extract (meaning a
tight `max_tokens` truncates before translating, not after — token accounting is therefore against the
untranslated text length, and the actual returned (translated) text length can differ). Calls an
operator-configured LibreTranslate endpoint (`OCCAM_TRANSLATE_URL`); failures here are explicitly
**non-fatal** — the transcode still returns `ok:true` with the original-language markdown and (per
evidence read) a marker that translation was attempted/failed, rather than turning the whole call into a
failure over a translation-sidecar outage.

---

## CAP-082 — `diff_against` (block-level delta codec)

**Evidence:** `Tools/BlockDiff.cs`, `OccamTranscodeTool.cs` diff-branch.

`diff_against` accepts a prior list of block hashes (from a previous call's block set). `BlockDiff.Compute`
diffs the **current** block hash list against the supplied prior list and returns `added` blocks (full
content, only for genuinely new/changed blocks), `removed` hashes (blocks that disappeared), and the
full current hash list (for the *next* diff call to chain against) — this is a proper incremental diff
primitive, not a full-content-plus-a-flag response.

## CAP-083 — HIDDEN: `diff_against` silently forces full `blocks[]` into the response

**Evidence:** `Knowledge/MaterializationRequest.cs` — `ExposePublicBlocks: options.JsonBlocks ||
options.DiffAgainst is not null`; `Routing/TranscodePipeline.cs` `ProjectBlocks` (`request.ExposePublicBlocks
? blocks : null`); `Tools/OccamTranscodeTool.cs` final response construction
(`Blocks: omitHeavySidecars || result.Blocks is null ? null : [.. result.Blocks]`).

This is a **non-obvious parameter interaction** worth flagging explicitly: calling `occam_transcode` with
`diff_against=<hashes>` and `json_blocks=false` still results in the response's top-level `blocks[]`
array being populated with the **complete** current block set (not just the diff), because
`ExposePublicBlocks` is OR'd across both flags and the final serialization only checks
`result.Blocks is null`, not which flag caused it to be non-null. A caller who only wants the small
`diff` payload and explicitly opted out of the (potentially large) full blocks array by leaving
`json_blocks` at its default `false` will still pay the full blocks-array token/byte cost. This looks
like an implementation shortcut (reusing the same internal flag for two different public intents) rather
than deliberate design, and is exactly the kind of thing the audit brief asked to hunt for.

## CAP-084 — `prefer_llms_txt` (probe-first codec)

**Evidence:** `OccamTranscodeTool.cs` pre-pipeline branch. When true, before running the normal
transcode pipeline, the tool checks the target host for a `/llms.txt` (or similar well-known summary)
resource and, if present and usable, prefers that as the source content instead of extracting the
originally-requested page — this is a distinct pre-check inserted **ahead of** `OccamRouter`, not a
post-processing step, so backend cascade / playbook resolution never run at all when the llms.txt path
short-circuits successfully.

## CAP-085 — `cache_ttl_s` (opt-in on-disk response cache)

**Evidence:** `Caching/TranscodeCacheEligibility.cs`, `Caching/TranscodeResponseCache.cs`,
`Caching/TranscodeCacheKey.cs`.

Off by default (`cache_ttl_s` unset ⇒ no caching — consistent with the repo-wide "no file cache by
design" principle for the *extraction* path; this is a narrow, explicit opt-in exception scoped only to
`occam_transcode` responses, gated by three independent conditions all needing to hold:
`cache_ttl_s` is set and > 0, **no** `session_profile` (never cache authenticated content —
**CAP-068**), **no** `if_none_match` (conditional requests bypass cache to avoid masking real content
changes), and the URL is not private (**CAP-100**). `TranscodeCacheKey` hashes the normalized URL
together with **every output-affecting option** (backend policy, all token/fit/focus/selector flags,
`playbook_id`, `rank_blocks`, `tag_trust`, etc.) — so cache entries are correctly partitioned per exact
materialization, not just per URL (two calls to the same URL with different `max_tokens` never collide
in cache).

## CAP-086 — `emit_capsule` (proof-carrying capsule encoding)

**Evidence:** `Receipts/CapsuleCodec.cs`.

When true (and receipts are enabled — **CAP-090**), the response includes an `occam://capsule/…` URI
that self-encodes the signed `ReceiptEnvelope`, the extracted markdown, block leaf hashes, and (if
present) a time-anchor proof — a self-contained artifact any other agent can verify **offline** via
`occam_verify` without re-fetching the original URL or trusting the issuing host at call time. This is
additive to (not a replacement for) the normal markdown response.

## CAP-087 — `rank_blocks` (BM25 salience tagging)

**Evidence:** `Compile/BlockSalience.cs`. Requires `json_blocks=true` to have any visible effect (gated
in the tool: salience annotation only runs against the blocks array that is actually being returned).
Each block gets a `salience` float in `[0,1]`, computed via the same BM25-family scoring as
`fit_markdown`/`FocusMatcher` against `focus_query`, normalized so the single highest-scoring block in
the document is exactly `1.0` — i.e. salience is **relative to this document's own blocks**, not an
absolute cross-document relevance score, and without `focus_query` set it falls back to a generic
informativeness heuristic (same fallback pattern as **CAP-063**).

## CAP-088 — `tag_trust` (prompt-injection / boilerplate trust signal)

**Evidence:** `Compile/BlockTrust.cs`. Also requires `json_blocks=true` to be observable. Tags each
block with `suspicious` and/or `boilerplate` boolean-style signals based on text pattern heuristics
(e.g. imperative "ignore previous instructions"-style phrasing patterns for `suspicious`; nav/cookie/footer
boilerplate patterns) combined with `source_selector` heuristics (e.g. blocks from `nav`/`footer`-rooted
selectors are more readily tagged boilerplate). This is explicitly a **security-adjacent** feature —
its stated purpose (per code comments/naming) is to let downstream LLM consumers de-weight or quarantine
blocks that look like third-party injected content (ads, comments, prompt-injection attempts embedded in
page content) rather than treating the whole page as equally trustworthy.

## CAP-089 — `delta_only` (suppress-full-body-when-diffing)

**Evidence:** `OccamTranscodeTool.cs` response-shaping (used together with `diff_against`/`if_none_match`
branches). When true, the tool omits the full markdown body from the response even on a "changed"
result, returning only the delta/diff payload — an explicit token-saving mode for callers doing
repeated polling who only ever want to react to changes, not re-read the whole page each time.

---

## CAP-090 — Receipt v1 positive signing

**Evidence:** `Receipts/ReceiptsPolicy.cs` (`OCCAM_RECEIPTS` env gate — off by default, confirmed as an
opt-in per `AGENTS.md`'s own env-flag framing, verified here directly against the policy class rather
than trusted from docs), `Receipts/ReceiptSigner.cs`.

When enabled, every **successful** transcode response can carry a `receipt.signed` envelope: an ECDSA
P-256 signature (key loaded from local storage or generated fresh — persisted, not ephemeral, so the
same signer identity is stable across calls) over a Merkle root built from block-level content leaves
plus request/response metadata (URL, materialization key, timestamp). This lets a third party later
verify (`occam_verify`) that a specific piece of markdown text really was produced by this signer for
this exact URL+options combination, without needing to trust the transport.

## CAP-091 — Receipt v1 negative signing

**Evidence:** cross-referenced from `Receipts/ReceiptsPolicy.cs`/`ReceiptSigner.cs` and the failure-path
receipt attachment noted in `OccamTranscodeModels.cs`'s failure record shape. Failures can also carry a
signed receipt — but scoped specifically to **provable unavailability** claims (e.g. "this signer
observed HTTP 404 for this URL at this time") rather than a receipt over absent content; this avoids the
nonsensical case of "signing" content that was never actually retrieved.

## CAP-092 — Time-anchor sidecar

**Evidence:** referenced in `Receipts/CapsuleCodec.cs` (capsule bundles "optional time anchors")
and `OccamTranscodeModels.cs` receipt info fields. An optional RFC3161-style external time-stamping
proof can be bundled alongside the signature to establish "this existed no later than T" independent of
the signer's own clock — an additive, opt-in strengthening of the receipt's evidentiary value, not
required for basic receipt emission.

---

## CAP-093 — `MaterializationKey` / content-hash identity system

**Evidence:** `Compile/MaterializationKey.cs`, `Compile/ContentHashToken.cs`.

`MaterializationKey` is a SHA-256-prefixed deterministic hash over URL + backend policy + the **full**
set of output-affecting options (mirrors `TranscodeCacheKey`'s option set — `playbook_id`, `rank_blocks`,
`tag_trust` explicitly called out in evidence as included, confirming these affect the identity of a
"materialization" even though they don't change the base markdown text). This key is what
`if_none_match` (**CAP-074**), caching (**CAP-085**), and receipts (**CAP-090**) all key off of — it is
the load-bearing concept tying "same inputs ⇒ same content-hash" together across every conditional/
caching/proof feature on this tool. A caller changing `rank_blocks` from false→true on an otherwise
identical request gets a **different** materialization key/content hash even though the markdown body
itself is byte-identical — worth knowing before relying on hash equality as a markdown-equality check.

---

## CAP-094 — PostProcessor pipeline ordering

**Evidence:** `Routing/TranscodePipeline.cs` (`_postProcessors` iteration), `PostProcessors/*.cs`.

Order confirmed by the pipeline's iteration list: **Challenge-page check → requires-login check →
thin-extract check.** This ordering matters: a Cloudflare challenge page is caught and coded as
`captcha_or_challenge` *before* the (cheaper, less specific) thin-extract heuristic would otherwise also
have flagged it as thin content — giving the caller the more actionable, specific failure code rather
than a generic "not enough text" verdict.

## CAP-095 — Challenge-page detection (post-processor + router-level parity)

**Evidence:** `PostProcessors/ChallengePagePostProcessor.cs`, `Routing/ChallengePageDetector.cs` (also
consulted directly inside `OccamRouter`'s cascade decision, **CAP-052** step 2). The same detector class
is used in two places for two different purposes: (a) inside the router, to decide *whether to escalate*
from HTTP to browser without waiting for the full post-processor pipeline, and (b) as a formal
post-processor that turns a still-challenge-flagged final result into the `captcha_or_challenge` failure
code. Failure code: `captcha_or_challenge`. Occam does **not** attempt to solve challenges — it only
detects and reports them (consistent with the tool's stated non-goal).

## CAP-096 — Requires-login detection (`AccessClassifier`)

**Evidence:** `PostProcessors/RequiresLoginPostProcessor.cs`, `Access/AccessClassifier.cs`,
`workers/shared/lib/access-evidence.mjs`.

The worker collects raw DOM/URL evidence (`access-evidence.mjs`: password fields, identity fields,
login-form presence, login-heading text, blocking overlays, redirect-to-login patterns) — purely
boolean signal collection, no judgment made worker-side. `AccessClassifier` (host-side) turns that
evidence into a three-way verdict: **Open / Restricted / Unknown**. The post-processor only fails the
transcode (`requires_login`) when the verdict is **Restricted** *and* no `session_profile` was supplied
— i.e. supplying a (even irrelevant/expired) `session_profile` does not suppress this check by itself;
the classifier still runs against the actual returned content, so a bad/expired session still correctly
surfaces `requires_login` rather than masking it. `DomainTierRegistry` (**CAP-104**) can suppress false
positives on known-public reference sites that happen to render login-like chrome (e.g. a "Sign in"
header link) without gating the actual content.

## CAP-097 — Thin-extract detection (`ExtractQualityEvaluator`)

**Evidence:** `PostProcessors/ThinExtractPostProcessor.cs`, `PostProcessors/ExtractQualityEvaluator.cs`.

`ExtractQualityEvaluator` computes a multi-factor `ExtractQualityReport`: visible prose character count,
heading count, link count, content density (text vs. markup ratio), a semantic-richness score, and a
noise score (boilerplate ratio) — combined into a `Confidence` value. Failure code `thin_extract` fires
only below a quality floor; critically, per the audit brief's own framing (confirmed directly in the
evaluator's logic, not just from AGENTS.md's assertion) a **complete but genuinely short page** is
distinguished from a **bad/shell extract** — the former surfaces as `ok:true` with
`quality.verdict=short_quality` rather than being coerced into a `thin_extract` failure. This distinction
is load-bearing for **CAP-106**'s heal-policy decisions (a genuinely short page should never trigger a
playbook-heal suggestion).

## CAP-098 — Router recovery/attempt log

**Evidence:** `Routing/TranscodeOutcome.cs` (`TranscodeAttempt` record), `Routing/OccamRouter.cs`
cascade (**CAP-052**).

Every backend attempt in a cascade (HTTP, browser, managed) is recorded as a `TranscodeAttempt` with its
own backend name, outcome/failure, and latency — surfaced to the caller so a multi-hop escalation
(e.g. HTTP failed with `timeout`, browser succeeded) is auditable rather than only returning the final
winning attempt with no trace of what was tried and discarded. This is what backs the `ChooseRawFallback`
informativeness comparison in **CAP-052** step 4 — the log is not just cosmetic telemetry, it is the data
the router's own selection logic operates over.

## CAP-099 — Auto browser provisioning + timeout grace

Covered in depth under **CAP-053**; restated here for CAP-ID completeness since it is a distinct
observable behavior (extended timeout window) rather than merely a readiness gate.

---

## CAP-100 — SSRF / private-network protections

**Evidence:** `Routing/PrivacyClassifier.cs`, `Session/FetchPreflight.cs`, redirect re-validation implied
by the meta-refresh + HTTP-redirect handling in `workers/http-extract/lib/http-extract-run.mjs` and
`workers/shared/lib/meta-refresh.mjs`; also independently re-implemented in `WellKnownGenomeFetcher.cs`
(**CAP-073**) for its own internal fetch.

`PrivacyClassifier.Classify` blocks `localhost`, `.local`/`.internal` TLD-style suffixes, and RFC1918/
loopback/link-local IP ranges, overridable only via `OCCAM_ALLOW_PRIVATE_URLS=1` (an explicit, global,
operator-set escape hatch — not a per-call parameter, so a malicious/compromised MCP client cannot
individually opt into SSRF against the operator's will on a single call). Every hop of a redirect chain
(HTTP 3xx or HTML meta-refresh) is re-validated, preventing an attacker from using an initially-public
URL that 302-redirects to `http://169.254.169.254/...`-style metadata endpoints. Cross-origin redirect
hops also strip session/auth headers (confirmed by evidence of header-scope handling tied to origin in
the fetch preflight/headers-merge code) so a redirect cannot be used to exfiltrate a caller's
`session_profile` credentials to an unrelated third-party host.

## CAP-101 — Response size cap / oversize mode

**Evidence:** `workers/shared/lib/response-body-cap.mjs` (`OCCAM_MAX_RESPONSE_BYTES`,
`OCCAM_HTTP_OVERSIZE_MODE`).

Two operator-selectable behaviors when a response body exceeds the configured cap: **fail-fast** (checks
`Content-Length` and aborts before downloading the body at all when declared size exceeds the cap →
failure code `response_too_large`) or **partial** mode (streams up to the cap and processes a truncated
body rather than failing outright — useful for very large but still information-dense pages like long
API references). This is a resource-safety control with no direct `occam_transcode` parameter — entirely
operator/env-configured, invisible to the MCP schema.

## CAP-102 — Egress proxy support

**Evidence:** `workers/shared/lib/egress-proxy.mjs` (`OCCAM_HTTP_PROXY`, `OCCAM_HTTPS_PROXY`,
`OCCAM_NO_PROXY`). Validates proxy URL shape, applies to both the plain Node `fetch` path (HTTP backend)
and Playwright's proxy configuration (browser backend), with a `NO_PROXY`-style bypass list — again,
entirely env/operator-controlled, not an `occam_transcode` input.

## CAP-103 — Robots.txt respect + host throttling

**Evidence:** `Services/RobotsThrottleService.cs` (`OCCAM_RESPECT_ROBOTS`, `OCCAM_HOST_THROTTLE_MS`),
invoked from `TranscodePipeline.CheckAndThrottle` ahead of the backend cascade. Off by default; when
enabled, `robots.txt` disallow rules can block a transcode attempt outright, and/or a minimum
inter-request delay per host is enforced even across concurrent calls (shared state keyed by host) — a
politeness control with no corresponding `occam_transcode` parameter (an operator-wide policy, not a
per-call opt-in), which matters for an agent issuing rapid same-host `occam_digest`/`occam_transcode`
bursts.

## CAP-104 — Domain tier registry

**Evidence:** `Routing/DomainTierRegistry.cs`. Loads JSON-configured per-host/per-domain hints:
`http_only` (skip browser escalation entirely for known-safe-HTTP sites, saving latency),
`page_class_hint` / `quality_mode_hint` (bias the quality evaluator's thresholds for known page shapes),
login-path detection and **public-reference-page allow-list** (suppresses false-positive
`requires_login` verdicts for sites that render login chrome on otherwise fully public pages — direct
input into **CAP-096**). This is a curated-knowledge layer sitting alongside (not replacing) the
generic heuristics, and it is entirely code/config-driven — no `occam_transcode` parameter exposes or
overrides it per-call.

---

## CAP-105 — Failure code normalization/taxonomy

**Evidence:** `Routing/FailureCodeStrings.cs`.

Central utility for normalizing raw worker/HTTP failure strings into the canonical failure-code
vocabulary, and for classifying **retryability** (e.g. `timeout`/`network_error` are retryable-by-nature;
`http_404`/`invalid_arguments` are not). Confirmed failure codes reachable on the `occam_transcode` path
from evidence gathered across this audit:

`invalid_arguments`, `invalid_policy`, `workers_unavailable`, `timeout`, `extraction_failed`,
`thin_extract`, `captcha_or_challenge`, `requires_login`, `http_403`, `http_404` (and other `http_4xx`/
`http_5xx` via the same normalizer), `response_too_large`, `private_url_blocked`, `dns_error`,
`tls_error`, `network_error`, `content_selectors` miss-class conditions (**CAP-065**), plus
worker-process-level codes surfaced verbatim when no structured JSON is produced: `spawn_failed`,
`bad_json`, `no_json` / `no_json:exit_<n>` / `no_json:<stderr tail>` (**CAP-108**).

## CAP-106 — Agent decision hints on failure

**Evidence:** `Agent/TranscodeAgentDecisions.cs`, `Playbooks/PlaybookHealPolicy.cs`.

Failure responses are enriched with a machine-actionable recommendation (`agentMeta.decisions`-style
field per the tool's own documented trust rule) — e.g. `requires_login` → "configure a `session_profile`",
`workers_unavailable` → "run `occam doctor`", certain challenge/terminal failures → "retry with
`backend_policy=browser`" or "this is not healable, stop." `PlaybookHealPolicy.IsHealable` gates whether
a failure should additionally suggest the playbook-heal workflow — explicitly excluding "genuinely short
but complete page" verdicts (**CAP-097**) from ever being offered as heal candidates, so the agent isn't
nudged to "fix" a page that was already extracted correctly.

## CAP-107 — Semantic outcome mapping (Access / Focus / Completeness)

**Evidence:** `Semantics/SemanticOutcomeMapper.cs`.

Maps three internal assessments into public response dimensions that are **independent axes**, not a
single quality score: `SemanticAccessInfo` (from `AccessClassifier`, **CAP-096**), `SemanticFocusInfo`
(from `FocusMatchStatus`, **CAP-064**), and `SemanticCompletenessInfo` (from `MaterializationAssessment`
— did the compiled markdown actually retain what the planner judged as the semantically load-bearing
content, distinct from raw truncation bookkeeping in `OmittedManifest`). This tri-axis design lets a
caller distinguish, e.g., "page was fully open and complete but simply didn't match your focus query"
from "page was access-restricted" from "page matched but got truncated" — three very different follow-up
actions for an agent.

## CAP-108 — Worker process lifecycle failure taxonomy

**Evidence:** `Workers/NodeWorkerProcessSpawner.cs`.

Distinct low-level failure states before any "worker business logic" failure is even possible:
`spawn_failed` (process could not start at all), `timeout` (wall-clock limit hit — separately reported
via `lifecycle.OnRunCompleted(timedOut: true, ...)` for pool/health-tracking purposes, see
`NodeWorkerLifecycle`), `no_json`/`no_json:exit_<code>`/`no_json:<stderr tail, max 240 chars,
newline-collapsed>` (process exited without emitting a parseable trailing JSON line — the stderr tail
capture is specifically bounded and sanitized before being embedded in a failure string, avoiding
unbounded/multi-line garbage in the failure code field), and `bad_json` (a JSON line was found but didn't
deserialize to the expected `WorkerExtractResponse` shape). Each of these also updates the worker's
observed **lifecycle health** (`timedOut`/`crashed` booleans) independent of the specific failure string
— this lifecycle signal is what backend pool/health logic (outside strict transcode scope, but sourced
from this same call path) uses to decide things like recycling a degraded browser daemon worker.

## CAP-109 — Media references (always-on, not gated by any parameter)

**Evidence:** `Workers/NodeWorkerProcessSpawner.cs` (`MediaRefMapper.Map(payload.MediaRefs)` called
unconditionally on both success and failure-with-partial-markdown branches), `OccamTranscodeModels.cs`
media-ref record.

Unlike `json_blocks`/`json_tables`/`json_feed` (all explicit opt-ins), media reference extraction
(image/video/audio URLs discovered in the page) is collected and attached to **every** response that
produced any markdown at all — there is no `occam_transcode` parameter to request or suppress it. This
is a genuinely always-on sidecar, confirmed by its presence directly in the unconditional
`ExtractRunResult` construction path rather than behind any feature-flag check.

## CAP-110 — Meta-refresh redirect following (client-side redirect layer)

**Evidence:** `workers/shared/lib/meta-refresh.mjs`.

Distinct from HTTP 3xx redirect handling: this specifically parses `<meta http-equiv="refresh"
content="N;url=...">` tags out of already-fetched HTML and resolves the target against the page's base
URL, allowing the worker to follow "soft" client-side redirects that a plain HTTP client would never see
(common on old-style splash/interstitial pages). Each resolved target is treated as a fresh navigation
for privacy-classification purposes (**CAP-100**), not just blindly followed.

## CAP-111 — Plain-text pass-through codec

**Evidence:** `workers/shared/lib/plain-text-pass-through.mjs`.

For `text/plain`/`application/octet-stream` responses (e.g. raw `README` files, `.txt` docs) that decode
as valid UTF-8 text (checked via a non-printable-character-ratio heuristic over a 4KB sample, rejecting
anything with embedded NUL bytes outright), the HTTP worker bypasses Readability/Turndown entirely.
If the content already looks like Markdown (leading `#`/`##` headers, fenced code blocks, or bullet
lists — `looksLikeMarkdown`) or the URL ends in `.md`/`.markdown`, it is passed through **verbatim**;
otherwise it is wrapped in a single fenced code block so non-markdown plain text (logs, config dumps)
still renders sanely as markdown rather than being misinterpreted as prose. This is a third format
branch alongside PDF (**CAP-059**) and feed (**CAP-080**) detection — confirms the HTTP worker does
real content-type-driven format dispatch, not a single fixed HTML pipeline.

## CAP-112 — Worker feature-scope propagation (`OccamFeaturesScope`)

**Evidence:** `src/FFOccamMcp.Core/Routing/OccamFeaturesScope.cs`.

An `AsyncLocal`-based disposable scope (same pattern family as `FetchHeadersScope`, **CAP-068**) used to
propagate the assembled feature-flag list (`semantic_chunking`, `screenshot`, `json_blocks`,
`json_tables`, `json_feed`, etc. — the same list built once per request in `TranscodePipeline`, see
**CAP-078**) down to the point where the worker process is actually spawned, without needing to thread an
explicit parameter through every intermediate call in the backend/runner layers. This is plumbing, not a
capability itself, but it is the mechanism that makes CAP-075/076/077/078/079/080 all actually reach the
Node.js worker process boundary — documented here to close the schema→worker trace requested by the
audit brief.

---

## Failure code catalog for `occam_transcode` (consolidated)

| Code | Source | Retryable? |
|---|---|---|
| `invalid_arguments` | `FetchPreflight` (CAP-050) | No |
| `invalid_policy` | `OccamBackendPolicyParser` (CAP-051) | No |
| `private_url_blocked` | `PrivacyClassifier` (CAP-100) | No |
| `workers_unavailable` | `WorkerPaths.IsConfigured` false (CAP-053) | No (fix install) |
| `timeout` | `NodeWorkerProcessSpawner` (CAP-108) / backend timeout | Yes |
| `extraction_failed` | generic worker-reported extraction failure | Sometimes |
| `thin_extract` | `ThinExtractPostProcessor` (CAP-097) | Sometimes (try browser) |
| `captcha_or_challenge` | `ChallengePagePostProcessor`/`ChallengePageDetector` (CAP-095) | No |
| `requires_login` | `RequiresLoginPostProcessor`/`AccessClassifier` (CAP-096) | No (needs session_profile) |
| `http_403` / `http_404` / other `http_*` | `FailureCodeStrings` normalizer (CAP-105) | Depends on code |
| `response_too_large` | `response-body-cap.mjs` (CAP-101) | No (raise cap or accept partial) |
| `dns_error` / `tls_error` / `network_error` | HTTP worker fetch layer | Yes (transient) |
| `content_selectors` miss | `MarkdownContentFilter` (CAP-065) | Yes (loosen selectors) |
| `spawn_failed` / `bad_json` / `no_json[:...]` | `NodeWorkerProcessSpawner` (CAP-108) | Depends |

---

## Retries

There is no client-facing "retry count" parameter on `occam_transcode` itself. Retry-like behavior is
entirely internal to the **cascade** (`http_then_browser` trying a second backend, **CAP-052**) — a
single logical `occam_transcode` call can still make multiple underlying attempts (HTTP → browser →
managed), each independently timed and logged (**CAP-098**), but this is backend escalation, not
same-backend retry-on-failure. No evidence was found of the HTTP backend itself retrying a failed fetch
against the same backend before escalating.

---

## Security summary

- SSRF: private/loopback/link-local blocked by default, redirect-hop re-validated, global-only override
 (**CAP-100**).
- Path traversal: `session_profile` IDs sanitized and root-confined (**CAP-069**).
- Credential handling: session headers passed to workers via temp file + `AsyncLocal` scope, not argv or
 MCP-visible fields; cross-origin redirects strip auth headers (**CAP-068**, **CAP-100**).
- Resource exhaustion: response byte cap with fail-fast/partial modes (**CAP-101**); worker timeouts with
 bounded stderr capture on crash (**CAP-108**).
- Prompt-injection-adjacent: `tag_trust` gives downstream consumers an explicit signal to de-weight
 suspicious/boilerplate blocks (**CAP-088**) — this is a mitigation aid, not a guarantee; Occam does not
 strip or refuse suspicious content outright.
- Managed third-party escalation is fully operator-gated (provider + API key + optional domain
 allow-list) with no per-call override, limiting blast radius of unexpected third-party data egress
 (**CAP-054**).

---

## Hidden / advanced findings (summary)

1. **CAP-054/055-058** — a fourth-tier third-party managed-scraping fallback exists with zero
 `occam_transcode`-schema visibility; entirely env-provisioned.
2. **CAP-059/080/111** — content-format dispatch (PDF / feed / plain-text / HTML) happens transparently
 inside the HTTP worker with no format parameter at all.
3. **CAP-073** — `playbook_policy=auto` reads existing playbooks only; it does **not** itself trigger the
 live `/.well-known/agent-genome.v1.json` fetch unless a separate env var is set — a meaningful gap
 between what the parameter name might suggest and what it does on this specific tool.
4. **CAP-078** — `json_blocks`/`json_tables` collection work happens on **every** call regardless of the
 caller's flags; the flags only gate response serialization, not the underlying compute cost.
5. **CAP-083** — `diff_against` alone (without `json_blocks`) silently returns the **full** blocks array
 in the response, not just the diff — likely an unintended coupling via shared internal flag reuse.
6. **CAP-076** — `capture_screenshot=true` under an HTTP-only effective path is a silent no-op with no
 distinguishing signal in the response.
7. **CAP-057** — the Scrapfly managed provider hardcodes `render_js=true` on every call, always paying
 for/requesting browser rendering upstream regardless of page complexity.

## Unresolved items

- Whether a PDF that fails HTTP-side `unpdf` extraction and then escalates to the browser backend
 receives any PDF-specific handling in the browser worker, or simply renders/extracts the browser's PDF
 viewer chrome as generic HTML — evidence found only covers the HTTP-side PDF path; browser-side PDF
 handling was not located in `BrowserExtractBackend.cs`/browser worker sources inspected.
- Exact numeric thresholds inside `ExtractQualityEvaluator`'s confidence formula and `FitMarkdown`'s BM25
 parameters (k1/b-equivalents) were read structurally but not exhaustively transcribed here — considered
 out of scope for a capability-existence audit (would belong in a tuning/calibration audit instead).
- Whether `OCCAM_RESPECT_ROBOTS`/`OCCAM_HOST_THROTTLE_MS` (CAP-103) apply identically to the managed
 backend's outbound calls (CAP-054) or only to the direct HTTP/browser backends — evidence located the
 service call site in `TranscodePipeline` ahead of the router, which would suggest it applies uniformly,
 but this was not independently confirmed against `ManagedExtractBackend`'s call sites.
