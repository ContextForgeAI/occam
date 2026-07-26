# Web search

**Slug:** `web-search` · **Product system:** PS-3 Discovery · **CAPs:** 12 · **Public relevance:** HIGH.

## What it is

`occam_search` sends a text query to one configured SearXNG, Brave, or Tavily provider and returns ranked URL hits. Optional reranking runs live probe analysis against each hit and orders by deterministic extractability (CAP-620–631; `OccamSearchTool.cs`).

It discovers URLs; it does not extract page bodies, use `OccamRouter`, or sign results (`PRODUCT-ARCHITECTURE.md:86`).

## Why it exists

- Supply candidate URLs when the caller does not already know a source (CAP-620/622).
- Normalize three provider APIs behind one response shape (CAP-623–626/631).
- Optionally prioritize URLs likely to extract cleanly before a transcode/digest (CAP-627–629).

## User-visible entrypoints

MCP `occam_search` is core and exposed in every profile, including `reader` (`OccamToolProfile.cs`; CAP-620). There is no equivalent host CLI verb.

## Core behavior

1. Validate nonblank `query` and `max_results` 1–20 (CAP-620/621).
2. Resolve the single configured provider from environment; unset, unknown, or incomplete configuration yields `search_unconfigured` (CAP-622).
3. Make one provider HTTP call and map title/URL/snippet records (CAP-623–626).
4. If `rerank=false`, preserve provider order.
5. If `rerank=true`, live-probe every returned URL, score extractability, and stable-sort descending with original rank as tie-break (CAP-627–629).
6. Return provider name, count, hits, and outcome-aware next-step text (CAP-630/631).

## Advanced behavior

| Feature | Behavior | Evidence |
|---|---|---|
| SearXNG | GET `/search?q=…&format=json`; required base URL; optional Bearer token | `SearxngProvider.cs`; CAP-623 |
| Brave | GET `/res/v1/web/search`; required API key in `X-Subscription-Token`; default Brave base | `BraveProvider.cs`; CAP-624 |
| Tavily | POST `/search`; required Bearer key; default Tavily base | `TavilyProvider.cs`; CAP-625 |
| Rerank fan-out | Up to 20 probes, maximum 5 concurrent, fixed 6000 ms each | `OccamSearchTool.RerankAsync`; CAP-627 |
| Score | Fixed 0.0–0.9 decision tree over probe outcome | `SearchExtractabilityScorer.cs`; CAP-628 |
| Annotation | Adds `extractability` and optional `recommendedBackend` only when reranked | CAP-629 |

## Automatic / silent behavior

- Unknown provider names and missing provider requirements share `search_unconfigured`; the typo is not identified (CAP-622).
- SearXNG sends `OCCAM_SEARCH_API_KEY` as Bearer when present even though it does not require a key (CAP-623).
- Provider results with blank URLs are dropped; each provider enforces `.Take(maxResults)` (CAP-621/623–625).
- Rerank performs one additional live HTTP probe per hit, without session context (CAP-627).
- Per-result rerank exceptions are swallowed; the hit remains with score `0.4` and no recommended backend (CAP-627).
- Probe/private-host failures score `0.0` rather than failing the search (CAP-627/628).

## Parameters

| Name | Default/range | Effect | Evidence |
|---|---|---|---|
| `query` | required | Free-text provider query | CAP-620 |
| `max_results` | `8`, 1–20 | Provider/request and returned-hit cap | CAP-621 |
| `rerank` | `false` | Adds live probe fan-out and extractability ordering | CAP-627–629 |

No URL session, backend policy, token budget, cache, receipt, playbook, translation, or result-language parameter exists.

## Configuration

| Variable | Requirement/effect | Evidence |
|---|---|---|
| `OCCAM_SEARCH_PROVIDER` | Required: `searxng`, `brave`, or `tavily`; no default | CAP-622 |
| `OCCAM_SEARCH_URL` | Required for SearXNG; optional base override for Brave/Tavily | CAP-622–625 |
| `OCCAM_SEARCH_API_KEY` | Required for Brave/Tavily; optional SearXNG Bearer | CAP-622–625 |
| `OCCAM_SEARCH_TIMEOUT_MS` | Default 20000; clamp 1000–120000 | CAP-626 |

No other search-specific env variable was found (`occam_search.md:275-281`).

## Backends

Exactly one configured search provider is called; there is no provider fallback, retry, browser search, or managed extraction cascade (CAP-622/626).

Rerank uses `ProbeService`/`HttpProbeFetcher`, not `occam_transcode`; it reads recommendation strings but never launches a browser (CAP-627).

## Sessions / state

No session parameter, cookies, storageState, or search-result cache exists. Every provider call and rerank probe is live. Rerank explicitly uses no session (CAP-627; `occam_search.md:244-246`).

## Network behavior

- Base search costs one provider request (GET for SearXNG/Brave, POST for Tavily).
- Rerank adds N target-site probes, bounded to five concurrent and six seconds each (CAP-627).
- Provider clients are not wired to worker proxy env (CAP-166; `occam_search.md:236-243`).
- Search API keys are transmitted using provider-specific headers (CAP-623–625).
- No retry/backoff or cache is present (CAP-626).

## Artifacts produced

ART-013 search hits: `{title,url,snippet}` plus optional extractability/recommended backend, provider name, count, and hints (`ARTIFACT-ONTOLOGY.md:94`; CAP-629/631).

Results are ephemeral, unsigned, unhashed, and not independently verifiable.

## Trust / provenance properties

Provider name identifies which configured service returned the candidates; it does not authenticate individual results or prove page content (CAP-631).

Extractability is a heuristic probe score, not content confidence or provenance (CAP-628; TRUST-MODEL §2). No Receipt v1, Merkle data, capsule, or trust tag exists.

## Failure / fallback behavior

| Code | Trigger | Evidence |
|---|---|---|
| `invalid_arguments` | Empty query or result count outside 1–20 | CAP-620/621 |
| `search_unconfigured` | Missing/unknown provider or missing key/base | CAP-622 |
| `search_timeout` | Provider timeout/cancellation | CAP-626 |
| `search_http_<nnn>` | Non-success provider status | CAP-626 |
| `search_error` | Network/deserialization/other exception | CAP-626 |

There is no provider fallback and no retry. Rerank failure is per-hit and never changes a successful provider search to tool failure (CAP-627).

## Platform differences

None declared for search semantics. Provider HTTP behavior is managed .NET `HttpClient`; probe rerank inherits probe scanner throughput differences only (`PLATFORM-DIFFERENCES.md`; CAP-627).

## Composition with other capabilities

- Uses `probe-diagnostics` for reranking (CAP-627/628).
- Hints suggest `occam_transcode` for one hit or `occam_digest` for several (CAP-630).
- Search does not itself call transcode, digest, map, playbooks, claims, or dataset export.

## Known limitations

- Off by default until operator configuration is complete (CAP-622).
- One provider only; no federation or fallback.
- Coarse error taxonomy collapses DNS/TLS/network/deserialization to `search_error` (CAP-626).
- Rerank can add 20 live requests and ignores sessions (CAP-627).
- Score is a fixed heuristic, not learned relevance or truth (CAP-628).
- No query/result cache, token budget, receipts, page bodies, browser, translation, or filtering by provenance.

## Engineering findings

- Shared `OCCAM_SEARCH_API_KEY` namespace can unintentionally attach a Bearer token to SearXNG (CAP-623).
- Unknown provider spelling is indistinguishable from absent configuration (CAP-622).
- Search client proxy behavior is absent under the explicit DI registration (CAP-166; report uncertainty notes no global handler was found).
- Failure taxonomy is materially coarser than probe/transcode (CAP-626).

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamSearchTool.cs`
- `src/FFOccamMcp.Core/Services/SearchService.cs`
- `src/FFOccamMcp.Core/Search/SearxngProvider.cs`
- `src/FFOccamMcp.Core/Search/BraveProvider.cs`
- `src/FFOccamMcp.Core/Search/TavilyProvider.cs`
- `src/FFOccamMcp.Core/Tools/SearchExtractabilityScorer.cs`
- CAP-620–631; ART-013.

## Public-doc relevance

High. Document the no-default-provider requirement, exact provider variables and HTTP methods, result limits, rerank network amplification, no-session rerank, score semantics, and coarse failures. Never imply search results or scores are signed, factual, or extracted.

## Handbook relevance

Use as the starting point when no source URL is known. Include provider setup cards, a “rerank costs N probes” warning, and recipes that hand one result to transcode or several to digest.
