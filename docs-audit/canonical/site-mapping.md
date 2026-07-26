# Site mapping

**Slug:** `site-mapping` · **Product system:** PS-3 Discovery · **CAPs:** 20 · **Public relevance:** HIGH.

## What it is

`occam_map` discovers and ranks URLs from a homepage, robots declarations, or sitemaps. It is an HTTP-only discovery service returning ART-011 link lists, not page content (CAP-510–529; `OccamMapTool.cs`; `ARTIFACT-ONTOLOGY.md:92`).

## Why it exists

- Turn a site seed into candidate URLs for an agent or digest (CAP-511/512/521/529).
- Bound sitemap traversal and result count while preserving partial-result honesty (CAP-513/523/524).
- Rank links around a concrete entity or topic without materializing every page (CAP-515–518).

## User-visible entrypoints

| Surface | Role | Evidence |
|---|---|---|
| MCP `occam_map` | Direct map response | `OccamMapTool.cs`; CAP-510 |
| `occam_digest(source_url=...)` | Reuses MapService/ranker and sitemap discovery | `DigestService.cs`; CAP-529/CAP-459 |

The successful response always suggests `occam_digest` as the next tool (CAP-521).

## Core behavior

1. Validate `url`, `source`, `max_links`, and `timeout_ms` (CAP-525).
2. Resolve headers/session through preflight (CAP-527).
3. For `homepage`, fetch HTML and stream anchors; for `sitemap`/`robots`, inspect robots and bounded sitemap XML (CAP-511/512).
4. Normalize, deduplicate, same-host filter, and optionally remove noise (CAP-514/519/526).
5. If focused, over-fetch candidates, enrich primary matches, optionally expand likely hubs, then rank (CAP-515–518).
6. Return at most `max_links`, filtered count, partial/expanded flags, warnings, and fixed digest handoff (CAP-521/524).

## Advanced behavior

| Mechanism | Behavior | Evidence |
|---|---|---|
| Sitemap traversal | Robots first; `/sitemap.xml` guess only for `source=sitemap`; max 4 XML fetches | `SitemapDiscovery.cs`; CAP-512 |
| XML safety | DTD/external entities disabled; regex `<loc>` fallback on parse failure | `SitemapDiscovery.cs`; CAP-512 |
| Shared deadline | One total timeout across robots and sitemap children | CAP-513 |
| Focus ranking | Entity-first path/title weighting, BM25 secondary, penalties for version/changelog roots | `MapLinkRanker.cs`; CAP-515 |
| Hub expansion | Up to 3 extra homepage-linked hubs when no score ≥4.0 | `MapService.ExpandSecondLevelAsync`; CAP-516 |
| Primary enrichment | Full-document anchor scan capped at 48 to escape DOM-order cutoff | CAP-517 |
| Focus pool | Up to 200 candidates (`max(max_links*8,64)`) | CAP-518 |
| Neighbor context | ±100-character hidden ranking context, never returned | CAP-520 |

## Automatic / silent behavior

- Homepage links lose fragments, are case-insensitively deduplicated, and stop at an internal DOM-order cap (CAP-511/518).
- `filter_nonsense=true` also removes soft-404s and hardcoded `nginx.org/changes` noise (CAP-514).
- `focus_query` can trigger 1–3 unrequested network fetches and a much larger candidate pool; `expanded:true` and a warning disclose expansion (CAP-516/518).
- Exact-host “same domain” excludes sibling subdomains and `www` variants (CAP-519).
- Anchor titles are silently rejected when they resemble SVG/CSS garbage and truncated to 120 characters (CAP-526).
- Service clamps bounds again even though MCP validation already rejected out-of-range values (CAP-525).

## Parameters

| Name | Default/range | Effect | Evidence |
|---|---|---|---|
| `url` | required | Seed homepage/site | CAP-510 |
| `source` | `homepage` | `homepage`, `sitemap`, or `robots` | CAP-511/512 |
| `max_links` | `32`, 1–64 | Returned result cap | `OccamMapTool.cs`; CAP-525 |
| `same_domain` | `true` | Exact-host-only filtering | CAP-519 |
| `filter_nonsense` | `true` | Asset, soft-404, and site-noise filtering | CAP-514 |
| `focus_query` | `null` | Entity-first ranking and possible hub expansion | CAP-515–518 |
| `timeout_ms` | `15000`, 3000–30000 | Whole discovery deadline | CAP-513/525 |
| `session_profile` | `null` | Header-only session application | CAP-527 |

There is no backend, browser, managed, token, cache, receipt, playbook, translation, or diff parameter (CAP-510).

## Configuration

`OCCAM_ALLOW_PRIVATE_URLS`, `OCCAM_SESSIONS_ROOT`, and `OCCAM_REQUEST_HEADERS_FILE` affect shared preflight/header behavior (CAP-150/156/167/169). No `OCCAM_MAP_*` variable exists (`occam_map.md:275-299`).

Core map clients do not honor worker proxy variables (CAP-166).

## Backends

Map uses only `HttpProbeFetcher`; there is no `backend_policy`, `OccamRouter`, browser, managed provider, or Node extraction worker (CAP-510; `PRODUCT-ARCHITECTURE.md:85`).

## Sessions / state

Every call is live and ephemeral; there is no map cache or persisted map state (CAP-510).

Session headers, including explicit Cookie, are applied. Playwright storageState/localStorage are resolved but unused, making storage-state-only profiles silent no-ops (CAP-527; ART-026).

## Network behavior

- Homepage/hub reads cap at 512 KiB; robots at 128 KiB; each sitemap at 2 MiB; fetcher ceiling 4 MiB (CAP-523).
- Sitemap walk has at most four XML fetches and one total timeout (CAP-512/513).
- Focused homepage maps may add up to three hub fetches (CAP-516).
- URL preflight and connect-time DNS/IP guard apply to every connection/redirect (CAP-154/184).
- No retry, browser, managed provider, cache, or worker proxy support (CAP-188/510/166).

## Artifacts produced

ART-011 is an ephemeral, machine-readable array of `{url,title,path}` links plus counts and hints (`ARTIFACT-ONTOLOGY.md:92`). Hidden neighbor context and raw candidates are not returned (CAP-520).

No content, receipt, hash, signature, Merkle root, or capsule is produced.

## Trust / provenance properties

Map output is an unsigned observation of links found in fetched HTML/XML. It proves neither that the links are current nor that they were authored by the origin. SSRF guarding constrains fetch destinations; it does not establish provenance (CAP-154/184; TRUST-MODEL §1).

## Failure / fallback behavior

| Code | Meaning | Evidence |
|---|---|---|
| `invalid_arguments` | Bounds or source invalid | CAP-525 |
| `invalid_url` / `private_url_blocked` | Seed/preflight refusal | CAP-522; CAP-150 |
| `unsupported_content_type` | PDF precheck or non-HTML homepage | CAP-522/528 |
| `sitemap_not_found` | No sitemap links; hint recommends homepage source | CAP-522 |
| `thin_extract` | Homepage yielded no usable links; hint recommends browser transcode | CAP-522 |
| `timeout` | No links before deadline | CAP-513/522 |
| `http_*` / session errors | Shared fetch/preflight failure | CAP-522/527 |

Sitemap timeout after some links yields `ok:true`, `partial:true`, and a warning rather than discarding results (CAP-524). There is no browser fallback.

## Platform differences

Session path containment uses case-insensitive comparison on Windows and ordinal comparison on Unix (`SessionProfileHeaders.cs:230-232`). `OCCAM_DOMAIN_TIERS_PATH` is not part of map. Discovery semantics otherwise have no declared OS delta (`PLATFORM-DIFFERENCES.md`).

## Composition with other capabilities

- Produces candidates for `digest-synthesis`; successful responses always suggest it (CAP-521).
- Digest directly reuses map service and ranker, so ranking changes affect both tools (CAP-529).
- Uses the same fetch primitive as probe (CAP-435).
- On `thin_extract`, it suggests `occam_transcode(backend_policy=browser)` because map cannot render JS (CAP-510/522).

## Known limitations

- HTTP-only; JS-generated links can be absent (CAP-510).
- “Same domain” is exact host, not registrable domain (CAP-519).
- Focus changes network cost and algorithm, not just ordering (CAP-516/518).
- StorageState sessions are ignored (CAP-527).
- PDF links are not mapped (CAP-528).
- No tokens, bodies, structured content, receipts, cache, retries, or playbook-aware discovery.

## Engineering findings

- CAP-514: hardcoded nginx host carve-out conflicts with playbook-oriented extensibility guidance; it remains shipped behavior.
- CAP-525: duplicate validation/clamp sources.
- CAP-527: silent session capability downgrade.
- CAP-529: tight hidden coupling to digest.

No canonical EF is assigned to these map-local observations.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamMapTool.cs`
- `src/FFOccamMcp.Core/Services/MapService.cs`
- `src/FFOccamMcp.Core/Services/SitemapDiscovery.cs`
- `src/FFOccamMcp.Core/Services/MapLinkRanker.cs`
- `src/FFOccamMcp.Core/Services/MapLinkFilter.cs`
- `src/FFOccamMcp.Core/Probe/HtmlLinkExtractor.cs`
- CAP-510–529; ART-011.

## Public-doc relevance

High. Explain source modes, exact limits/defaults, focused network amplification, exact-host semantics, partial sitemap behavior, HTTP-only boundary, and storageState limitation. Do not describe map as a browser crawler or signed site inventory.

## Handbook relevance

Use for “discover candidate pages before reading.” Provide recipes for homepage vs sitemap vs robots, focused mapping, interpreting `partial`/`expanded`, and handing selected URLs to one digest.
