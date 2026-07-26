# Digest synthesis

**Slug:** `digest-synthesis` · **Product system:** PS-3 Discovery · **CAPs:** 11 · **Public relevance:** HIGH.

## What it is

`occam_digest` discovers and/or extracts up to eight URLs, returns per-item results, optionally combines successful markdown, and emits batch-level read-order/focus hints (CAP-450–460; `OccamDigestTool.cs`; ART-010).

It composes `TranscodePipeline` per URL but exposes a reduced option surface; it is not equivalent to N full `occam_transcode` calls (CAP-455; `PRODUCT-ARCHITECTURE.md:83`).

## Why it exists

- Replace multiple independent page reads with one bounded research request (CAP-453/455).
- Discover relevant pages from one hub URL through map/sitemap infrastructure (CAP-459).
- Preserve per-item failures while synthesizing successful material (CAP-456).
- Tell agents when focus failed or combined output is unsafe to treat as focused evidence (CAP-460).

## User-visible entrypoints

MCP `occam_digest` is core and present in all nonempty profiles (`OccamToolProfile.cs`; CAP-450). `occam_map` points callers to it via `agentHints` (CAP-521).

## Core behavior

1. Require `urls` and/or `source_url`; when both exist, `source_url` silently wins (CAP-450).
2. Normalize array or legacy string inputs, deduplicate, cap/truncate to `max_urls` (CAP-451/453).
3. Optionally discover links from `source_url` using map/sitemap engines (CAP-459).
4. Preflight every resolved URL before extraction; one invalid/private URL aborts the whole digest (CAP-452).
5. Extract entries through `TranscodePipeline`, with bounded parallelism and reduced options (CAP-455).
6. Return `items[]`, stats, deterministic `digestId`, optional `combined`, receipts per successful item, and hints (CAP-456/457/460).

## Advanced behavior

| Feature | Behavior | Evidence |
|---|---|---|
| Input schema patch | `urls` is array-of-URI strings or deprecated string form | `OccamMcpServerRegistration.WithDigestUrlsUnion`; CAP-450 |
| Per-URL focus | Reachable only through string-encoded JSON array objects | `DigestUrlParser.cs`; CAP-451 |
| Ambient budget | Explicit `per_url_max_tokens` wins; otherwise client ambient budget; floor 128 | CAP-454 |
| Parallelism | HTTP default 4; browser/cascade uses browser limiter default 2; env-overridable | `DigestParallelism.cs`; CAP-455 |
| Parallel HTTP routing | Parallel tasks force one-shot Node workers, bypassing shared HTTP daemon | `HttpExtractRoutingScope.PushOneShot`; CAP-455 |
| Combined conditional | `if_none_match` hashes combined text and blanks only `combined`; item excerpts remain | CAP-458 |
| Source discovery | Unfocused sitemap-first; focused homepage-map + sitemap pool + rank | CAP-459 |
| Focus honesty | Read order can downgrade `combined` → `items_by_focusMatched` → `items_only` | `DigestAgentHints.cs`; CAP-460 |

## Automatic / silent behavior

- `source_url` ignores supplied `urls` without warning (CAP-450).
- Exact URL dedup and `max_urls` truncation are silent (CAP-451/453).
- Preferred array input cannot express per-entry focus; deprecated string JSON can (CAP-451).
- One bad preflight URL prevents every valid URL from running (CAP-452).
- Internal DOM block/table work still occurs even though digest never exposes those sidecars (CAP-455/CAP-078).
- Parallel batches bypass the persistent HTTP daemon (CAP-455).
- Failed items are omitted from `combined`; they remain in `items[]` and stats (CAP-456).

## Parameters

| Name | Default | Effect | Evidence |
|---|---|---|---|
| `urls` | optional union | Explicit URL inputs; max raw items 256/chars 65536 | CAP-450/451 |
| `source_url` | optional | Auto-discovery; overrides `urls` | CAP-450/459 |
| `backend_policy` | `http_then_browser` | Uniform per-item route | CAP-455 |
| `max_urls` | service cap 8 | Silent clamp 1–8 | CAP-453 |
| `per_url_max_tokens` | ambient | Per-item token budget; minimum 128 | CAP-454 |
| `focus_query` | `null` | Batch/default focus and discovery strategy switch | CAP-451/459/460 |
| `fit_markdown` | `false` | Per-item compile fitting | CAP-455 |
| `include_combined` | tool default | Controls joined success body | CAP-456 |
| `session_profile` | `null` | Uniform session on item transcodes | CAP-452/455 |
| `max_links` | service-clamped 1–8 | Discovery result cap | CAP-453/459 |
| `if_none_match` | `null` | Conditional over combined only | CAP-458 |

No per-item playbook, blocks/tables/chunks, screenshot, translation, cache, capsule, selector, diff, llms.txt, or trust-tag option exists (CAP-455).

## Configuration

`OCCAM_DIGEST_PARALLEL`, `OCCAM_DIGEST_MAX_PARALLEL`, and `OCCAM_BROWSER_MAX_PARALLEL` set fan-out (`DigestParallelism.cs`; CAP-455). `OCCAM_RECEIPTS` gates per-item signatures (CAP-457/CAP-280). Pipeline proxy, robots, session, managed, and browser variables apply per extracted item; discovery's Core HTTP path does not inherit worker proxy (CAP-166).

## Backends

Each item uses the requested `http`, `browser`, or corrected `http_then_browser` router semantics. The real cascade has terminal 404/410 and public-reference short-circuits, failure ranking, and managed-success-only surfacing (EF-056; `ACQUISITION-ROUTING-MODEL.md:96-110`).

Source discovery bypasses router and uses map/probe HTTP services (CAP-459).

## Sessions / state

One `session_profile` applies to all item transcodes. Source discovery hardcodes no session, so an authenticated hub may fail discovery even though resulting item fetches could authenticate (CAP-459 report uncertainty resolved by call site).

No digest response cache exists. The deterministic `digestId` is a URL-set identifier, not persisted state and not sensitive to focus/budget options (CAP-456).

## Network behavior

- Up to eight extraction flows, bounded parallelism (CAP-453/455).
- `source_url` adds sitemap/homepage/hub discovery requests before extraction (CAP-459).
- Whole-batch preflight happens before any item fetch (CAP-452).
- Managed providers may be reached transitively under cascade if configured, subject to corrected router rules (CAP-455; EF-056).
- No digest-level retry or cache (CAP-455).

## Artifacts produced

ART-010: `digestId`, `items[]`, stats, optional combined markdown, discovered links, and agent hints (`ARTIFACT-ONTOLOGY.md:66`; CAP-456/459/460).

Successful items may carry reduced Receipt v1 envelopes; there is no digest-level receipt binding the combined text or URL set (CAP-457).

## Trust / provenance properties

Per-item receipts, when enabled, commit to each compiled markdown content hash. They have no block leaves/root usable for citations and no time anchor; the combined digest is unsigned (CAP-457; TRUST-MODEL §3 C3/C4).

Signatures prove only assertion by the holder of the local self-signed key, not origin, truth, identity, or freshness (TRUST-MODEL §1/§13). Failed items must never be filled from memory (CAP-460).

## Failure / fallback behavior

| Scope | Behavior | Evidence |
|---|---|---|
| Argument/preflight | Any invalid/private URL or session failure aborts entire digest | CAP-450/452/454 |
| Discovery | Zero links gives `invalid_urls` | CAP-459 |
| Runtime item | Failures remain per-item while successful items continue | CAP-455/456 |
| All items fail | Top-level `digest_failed`, but full failed `items[]`/stats retained | CAP-456 |
| Partial | `ok:true`; hints include `partial_digest` and `skip_failed` | CAP-460 |

Underlying item failures follow the pipeline taxonomy and corrected cascade; there is no same-backend retry.

## Platform differences

No digest-specific semantic OS differences. It inherits worker/process and browser-cache platform differences from acquisition (`PLATFORM-DIFFERENCES.md`). Fan-out logic is managed `Task`/`SemaphoreSlim` and is platform-neutral.

## Composition with other capabilities

- Reuses map/site discovery (CAP-459/CAP-529).
- Reuses acquisition/materialization per item with a deliberately reduced option set (CAP-455).
- Consumes ambient client budget (CAP-454/ART-023).
- Produces reduced receipts from PS-6 (CAP-457).
- Does not consume playbook resolution: options leave `PlaybookPolicy=off` (CAP-455).

## Known limitations

- Maximum eight effective URLs; silent truncation (CAP-453).
- One invalid/private URL aborts all work (CAP-452).
- No playbook-aware extraction, structured sidecars, capsules, time anchors, cache, translation, or screenshots (CAP-455/457).
- Combined is not separately signed and excludes failed placeholders (CAP-456/457).
- `if_none_match` does not suppress per-item bodies (CAP-458).
- Discovery ignores session profile (CAP-459).

## Engineering findings

- CAP-451: useful per-entry focus is hidden behind deprecated input shape.
- CAP-452: preflight isolation differs from runtime item isolation.
- CAP-455: shared-daemon bypass and unreachable playbooks are non-obvious.
- CAP-457: receipt capability is reduced versus direct transcode.
- EF-056 corrections govern all cascade prose; older CAP-052/104 descriptions must not be repeated.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamDigestTool.cs`
- `src/FFOccamMcp.Core/Services/DigestService.cs`
- `src/FFOccamMcp.Core/Digest/DigestInputContract.cs`
- `src/FFOccamMcp.Core/Digest/DigestInputNormalizer.cs`
- `src/FFOccamMcp.Core/Digest/DigestUrlParser.cs`
- `src/FFOccamMcp.Core/Services/DigestParallelism.cs`
- `src/FFOccamMcp.Core/Agent/DigestAgentHints.cs`
- CAP-450–460; ART-010; EF-056.

## Public-doc relevance

High. Document both input forms honestly, the eight-URL cap, source override, whole-batch preflight, reduced transcode surface, parallel one-shot behavior, discovery/session mismatch, combined semantics, and reduced per-item receipts.

## Handbook relevance

Use as the canonical “several pages, one call” workflow. Include explicit-URL and source-discovery recipes, focus/read-order interpretation, partial-failure handling, and a decision point for when full per-page transcode features require separate calls.
