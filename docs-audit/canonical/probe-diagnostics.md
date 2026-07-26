# Probe diagnostics

**Slug:** `probe-diagnostics` · **Product system:** PS-3 Discovery · **CAPs:** 18 · **Public relevance:** HIGH.

## What it is

`occam_probe` is a bounded, HTTP-only pre-fetch diagnostic that classifies a URL before a full extraction. It returns page/access signals, redirect history, extractability, a backend recommendation, optional social metadata, and agent hints (CAP-420–437; `docs-audit/tools/occam_probe.md:18-28`).

It is not a lightweight invocation of `occam_transcode`: it bypasses `TranscodePipeline`, `OccamRouter`, Node workers, browser workers, managed providers, post-processors, materialization, and receipts (`ProbeService.cs:7`; `HttpProbeFetcher.cs`; CAP-420; `PRODUCT-ARCHITECTURE.md:84`).

## Why it exists

- Avoid paying browser/extraction cost before basic access and page-shape diagnosis (CAP-420).
- Recommend a follow-up `backend_policy` and selected transcode sidecars without coupling the calls (CAP-426, CAP-434).
- Supply the same deterministic extractability score used by optional search reranking (CAP-425).
- Preserve typed uncertainty: probe signals are heuristics over a capped HTML sample, not proof that extraction will succeed (CAP-428–433).

## User-visible entrypoints

| Surface | Availability | Evidence |
|---|---|---|
| MCP `occam_probe` | Core; exposed in all profiles | `OccamProbeTool.cs`; `OccamToolProfile.cs`; CAP-420 |
| Indirect search rerank | `occam_search(rerank=true)` invokes `ProbeService` per result | `OccamSearchTool.cs:87-92`; CAP-425 |
| Shared fetch primitive | Map, digest discovery, and sitemap discovery use `HttpProbeFetcher` directly | `MapService.cs`; `DigestService.cs`; `SitemapDiscovery.cs`; CAP-435 |

No CLI verb exposes the full probe response (CAP-420).

## Core behavior

1. Validate `url`; resolve request/session headers through `FetchPreflight.Prepare` (CAP-423).
2. Perform one HTTP request or a manually followed redirect chain; never invoke browser or managed extraction (CAP-420, CAP-430).
3. Read at most 256 KiB for ordinary probe analysis; classify HTML/XML/text or short-circuit PDF/unsupported types (CAP-431–433).
4. Derive page class, risks, access tri-state, challenge kind, domain tier, recommendation, latency estimate, and extractability (CAP-425–429, CAP-437).
5. Optionally parse social metadata from `<head>` only (CAP-422).
6. Emit proactive hints for the next tool/parameters (CAP-434).

## Advanced behavior

| Behavior | Detail | Evidence |
|---|---|---|
| Redirect capture | Manual, maximum 10 hops; overflow gives `redirect_loop` | `HttpRedirectFollower.cs`; CAP-430 |
| PDF classification | Metadata stub; fixed `visibleTextRatio=1.0`, no PDF byte parsing | `ProbeService.BuildPdfAnalysis`; CAP-431 |
| Extractability score | Fixed decision tree: 0.0 failure through 0.9 clean docs/article/reference/blog | `SearchExtractabilityScorer.cs`; CAP-425 |
| Backend recommendation | Fixed heuristic; advisory only and not persisted into later calls | `ProbeService.Recommend`; CAP-426 |
| `httpOnlyRoute` | Curated advisory signal; does not control router escalation | `DomainTierRegistry.PreferHttpOnlyRoute`; CAP-427; EF-056 |
| Access | `Open` / `Restricted` / `Unknown`, with confidence and action | `AccessClassifier.cs`; CAP-428 |
| Challenge taxonomy | `rate_limit`, `turnstile`, `hcaptcha`, `datadome`, `js_challenge`, `generic_challenge` | `ChallengeKindClassifier.cs`; CAP-429 |
| Domain-tier stamp | Returns tier id even when no observable hint changed | `ProbeService.cs`; CAP-437 |

## Automatic / silent behavior

- `timeout_ms` is silently clamped to 1–120,000 ms rather than rejected (CAP-421).
- Manual redirects are always tracked in production; the auto-redirect client/branch is wired but unreachable (CAP-436; `DEAD-OR-UNREACHABLE.md:38`).
- A storage-state-only session profile is resolved but has no effect because probe consumes headers only; no warning reports this downgrade (CAP-424).
- PDF quality is not measured; all PDFs receive the same fixed classification (CAP-431).
- Search rerank reuses probe with a separate fixed 6-second timeout and no session (CAP-425, CAP-627).
- Agent hints may recommend `json_tables`, `prefer_llms_txt`, `json_feed`, or token/focus parameters for a different tool (CAP-434).

## Parameters

| Name | Required/default | Effect | Evidence |
|---|---|---|---|
| `url` | required | HTTP(S) target; invalid shape fails before fetch | `OccamProbeTool.cs`; CAP-420 |
| `timeout_ms` | `10000` | Fetch/redirect budget; clamp 1–120000 ms | `HttpProbeFetcher.cs`; CAP-421 |
| `include_social_meta` | `false` | Adds head-only OpenGraph/Twitter metadata; no classification effect | `HtmlSocialMetaExtractor.cs`; CAP-422 |
| `session_profile` | `null` | Applies merged headers only; storageState is inert | `FetchPreflight.cs`; `ProbeHttpHeaders.cs`; CAP-423/424 |

There is no `backend_policy`, browser, managed-provider, token-budget, cache, receipt, playbook, or diff parameter (CAP-420; `occam_probe.md:404-434`).

## Configuration

| Setting | Default/effect | Evidence |
|---|---|---|
| `OCCAM_ALLOW_PRIVATE_URLS` | Off; permits otherwise-blocked private targets when enabled | `PrivacyClassifier.cs`; CAP-100 |
| `OCCAM_SESSIONS_ROOT` | User session root; resolves `session_profile` | `SessionProfileHeaders.cs`; CAP-423 |
| `OCCAM_REQUEST_HEADERS_FILE` | Unset; merges ambient HTTP headers | `RequestHeadersMerger.cs`; CAP-423 |

No `OCCAM_PROBE_*` setting exists (CAP-420–437). Core C# probe clients do not honor worker proxy variables (CAP-166; `occam_probe.md:406-434`).

## Backends

Only `HttpProbeFetcher` and C# `HttpClient` are reachable. There is no `IExtractBackend`, browser, managed provider, Node worker, or backend cascade (CAP-420; `PRODUCT-ARCHITECTURE.md:84`).

The named redirect-tracking client disables automatic redirects and uses `OutboundHttpGuard.ConnectAsync`; the separately registered auto-redirect client is dead from product call sites (CAP-430, CAP-436).

## Sessions / state

Probe is live and stateless per call; there is no response cache or persistent probe state (CAP-420).

`session_profile` contributes flat headers, including an explicit `Cookie` header. Playwright `storageState` and localStorage are not consumed, so browser-exported sessions can be silently ineffective (CAP-423/424; ART-026).

## Network behavior

- One target HTTP flow plus up to 10 redirects; no retries (CAP-420, CAP-430).
- Body sample: 256 KiB for probe; shared fetcher absolute ceiling 4 MiB for other consumers (CAP-432).
- URL preflight plus connect-time DNS/IP guarding applies to redirects (CAP-430; CAP-154).
- Worker proxy env is not applied to this Core `HttpClient` family (CAP-166).
- No managed third party receives the URL on this path (CAP-420).

## Artifacts produced

- ART-012 probe diagnosis: page class, risks, access, redirect chain, recommendation, extractability, optional social metadata (`ARTIFACT-ONTOLOGY.md:92-94`).
- Agent hints for a follow-up tool call (CAP-434).

The response is unsigned, unhashed, ephemeral, and not verifiable as a trust artifact (ART-012; `ARTIFACT-ONTOLOGY.md:88-95`).

## Trust / provenance properties

Probe proves nothing about origin content, extraction success, or truth. It is a current HTTP observation plus deterministic heuristics over a capped sample (CAP-420, CAP-432).

No Receipt v1, Merkle root, capsule, signature, or time anchor is produced (`occam_probe.md:422-425`). A domain-tier field identifies curated policy input, not cryptographic provenance (CAP-437).

## Failure / fallback behavior

| Failure | Meaning / fallback | Evidence |
|---|---|---|
| `invalid_arguments` | Tool-level missing/bad URL | `OccamProbeTool.cs`; CAP-420 |
| `invalid_url` / `private_url_blocked` | Preflight refusal | `FetchPreflight.cs`; CAP-100 |
| session profile errors | Missing/invalid profile | `SessionProfileHeaders.cs`; CAP-423 |
| `timeout` | Probe deadline exceeded | `HttpProbeFetcher.cs`; CAP-421 |
| `redirect_loop` | More than 10 redirects | `HttpRedirectFollower.cs`; CAP-430 |
| `unsupported_content_type` | Non-HTML/XML/plain and non-PDF; transcode may still succeed | CAP-433 |
| `http_*` / `network_error` | HTTP/network failure; SSRF guard may be coarsened to `network_error` | GAP-003; `FAILURE-BEHAVIOR-MAP.md` |

There is no browser or managed fallback. Failure means probe did not establish content; it must not be replaced with model memory (TRUST-MODEL §9.5).

## Platform differences

`VectorizedHtmlScanner` selects AVX2, AdvSimd, SSE2, or scalar implementations by platform/CPU; this changes throughput, not semantics (`PLATFORM-DIFFERENCES.md`; `ACQUISITION-ROUTING-MODEL.md:383-389`).

Session path containment is case-insensitive on Windows and ordinal on Unix (`SessionProfileHeaders.cs:230-232`). Network and classification semantics otherwise have no declared OS delta.

## Composition with other capabilities

- `probe-diagnostics` → `web-search`: shared extractability scorer reranks hits (CAP-425/627).
- `probe-diagnostics` → `site-mapping` / `digest-synthesis`: shared `HttpProbeFetcher` supplies homepage, hub, robots, and sitemap reads (CAP-435).
- Probe hints → acquisition/materialization: caller may set backend and sidecars manually (CAP-426/434).
- Probe does not mutate or seed a future transcode; composition is explicit at the caller boundary (CAP-426).

## Known limitations

- HTML sample is capped and may miss late body signals (CAP-432).
- PDF analysis is a fixed stub, not extractability measurement (CAP-431).
- Unsupported probe type does not imply transcode failure (CAP-433).
- StorageState sessions are silently ignored (CAP-424).
- Recommendations are heuristic and advisory (CAP-426).
- No browser/managed escalation, retries, cache, playbooks, receipts, blocks, token budget, or CAPTCHA solving (CAP-420; `occam_probe.md:404-434`).

## Engineering findings

- GAP-003: connect-time SSRF refusals can collapse to `network_error` in probe (`HttpProbeFetcher.cs:164-175`).
- CAP-424: browser-export session state is silently inert.
- CAP-436: `probe.autoRedirect` and `trackRedirects=false` are dead product paths (`DEAD-OR-UNREACHABLE.md:38`).
- CAP-431: PDF fields overstate measurement by using fixed values.

These are findings/limitations, not advertised capabilities.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamProbeTool.cs`
- `src/FFOccamMcp.Core/Services/ProbeService.cs`
- `src/FFOccamMcp.Core/Probe/HttpProbeFetcher.cs`
- `src/FFOccamMcp.Core/Probe/HttpRedirectFollower.cs`
- `src/FFOccamMcp.Core/Probe/HtmlProbeClassifier.cs`
- `src/FFOccamMcp.Core/Probe/HtmlSocialMetaExtractor.cs`
- `src/FFOccamMcp.Core/Tools/SearchExtractabilityScorer.cs`
- `src/FFOccamMcp.Core/Routing/DomainTierRegistry.cs`
- `src/FFOccamMcp.Core/Access/AccessClassifier.cs`
- `src/FFOccamMcp.Core/Agent/ProbeAgentHints.cs`
- CAP-420–437; ART-012; GAP-003.

## Public-doc relevance

High. Public documentation should define probe as HTTP-only, unsigned, heuristic, sample-capped, and advisory; explain the session headers-only boundary; list real parameters/defaults; and state that `unsupported_content_type` does not predict transcode failure (CAP-420–434).

It must not imply browser/managed escalation, receipt-backed diagnosis, PDF quality measurement, or automatic coupling to transcode.

## Handbook relevance

Use as the first decision card for “is this URL worth a full fetch?” and “which extraction path should I try?” Include a compact decision table for access/challenge/backend recommendations, a warning that storageState is ignored, and explicit handoff recipes to `occam_transcode`, `occam_map`, and `occam_search`.
