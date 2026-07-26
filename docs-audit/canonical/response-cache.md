# Response cache

**Slug:** `response-cache` · **Product system:** PS-2 Materialization · **CAPs:** 4 · **Public relevance:** HIGH

**Member CAPs:** CAP-085, CAP-315, CAP-321, CAP-322  
**Product capability:** CAP-085  
**Artifacts:** ART-035 (on-disk cache entries)  
**Engineering findings:** EF-001, EF-045

## What it is

**Opt-in on-disk replay** of successful transcode JSON envelopes within a caller-supplied TTL. Default Occam behavior remains **live extract** — cache is off unless `cache_ttl_s > 0`.

## Why it exists

Avoid repeat network+compile cost for identical public, session-free requests during agent loops — without implying a global CDN or always-on page cache.

## User-visible entrypoints

| Surface | Notes | Evidence |
|---------|-------|----------|
| `cache_ttl_s` on `occam_transcode` | Omit or ≤0 = off | CAP-085; `OccamTranscodeTool.cs:63,117-130` |
| Hit marker | `cached:true` on replay | tool cache path |
| Env `OCCAM_CACHE_DIR` | Storage root | CAP-321; `TranscodeResponseCache.cs:37-43` |

No separate cache MCP tool.

## Core behavior

### Eligibility (CAP-321 + tool)

`TranscodeCacheEligibility.IsCacheable` requires:

- `cacheTtlS > 0`
- no `session_profile`
- no `if_none_match`
- URL not private / invalid per `PrivacyClassifier`

Tool additionally requires **no** `diff_against` and **no** `prefer_llms_txt` (`OccamTranscodeTool.cs:119-121`).

### Key (CAP-315 / EF-001)

`TranscodeCacheKey.Compute` folds URL (normalized: lower scheme/host, drop fragment, keep path/query), `backend_policy`, and many options (`max_tokens`, fit, focus, selectors, playbook_policy, semantic_chunking, screenshot, json_* , translate_to) — **`TranscodeCacheKey.cs:19-48`**.

**Omitted from key but output-affecting:** `rank_blocks`, `tag_trust`, `emit_capsule` → cache hit may replay stale annotations/capsule — **EF-001** / CAP-315.

**Fragment:** dropped in `NormalizeUrl` while `FocusIntent` uses fragment → cross-fragment collision — **EF-045**.

### Storage (CAP-321)

- One JSON file per key under `OCCAM_CACHE_DIR` (default `%TEMP%/occam-cache`).
- Atomic write via temp + `File.Move(overwrite:true)`.
- Schema version gate (`CurrentSchemaVersion=1`); corrupt/locked → miss fail-closed.
- Stores **full success envelope** including signed receipt when present (AUTOMATION #16) — trust/privacy implication.

### TTL (CAP-322)

Age checked **only on read**; expired entries deleted on read. No background sweep, size cap, or LRU — stale files accumulate until re-read.

## Advanced behavior

| Topic | Notes |
|-------|-------|
| Write path | Eligible successes written after materialize/sign |
| Miss | Transparent live path |
| Private URL | Never eligible |

## Automatic / silent behavior

| Behavior | Automation |
|----------|------------|
| Opt-in cache write of signed envelopes | #16 |
| Fragment focus without key identity | EF-045 |
| Ambient max_tokens changes key | Client capabilities affect identity |

## Parameters

| Name | Default | Effect |
|------|---------|--------|
| `cache_ttl_s` | null/≤0 | Enable TTL seconds; hit returns prior JSON |

## Configuration

| Knob | Default | Effect |
|------|---------|--------|
| `OCCAM_CACHE_DIR` | OS temp `occam-cache` | Directory for ART-035 files |

## Backends

None — skips acquisition on hit (FLOW-019).

## Sessions / state

Disk files only. Not shared memory. Multi-process hosts sharing `OCCAM_CACHE_DIR` share entries.

## Network behavior

Hit: **no** live fetch. Miss/ineligible: normal acquisition.

## Artifacts produced

| Artifact | ID |
|----------|-----|
| Per-key JSON envelope files | ART-035 |

## Trust / provenance properties

Replay returns a **prior** signed envelope if receipts were on — verifies prior packaging, not “fresh now.” Per `ARTIFACT-ONTOLOGY.md` / `STATE-MODEL.md`: ART-035 may embed ART-001/002/006/007/039; **MaterializationKey (ART-024) is not the cache key** — server uses `TranscodeCacheKey`. Stale rank_blocks/tag_trust/capsule possible (EF-001). Do not cache authenticated content (eligibility enforces session ban). “No file cache by design” = default live extract, **not** “Occam is stateless.”

## Failure / fallback behavior

| Condition | Behavior |
|-----------|----------|
| TTL expired | Delete on read → live |
| Corrupt file | Miss |
| Ineligible request | No read/write, no error |
| Key collision (EF-001/045) | Wrong envelope possible |

## Platform differences

Default dir uses OS temp. Path separators OS-native. No semantic delta.

## Composition with other capabilities

- Pre-ladder gate before `acquisition-routing` (FLOW-019).
- Disjoint from differential params.
- Key must stay aligned with materialization options — currently incomplete (EF-001).
- Couples to ambient token budget via key field.

## Known limitations

- Not a general web cache; opt-in only.
- No proactive eviction.
- Key gaps EF-001 / EF-045.
- Full envelope on disk may retain receipts + page text until TTL read-delete.

## Engineering findings

| ID | Finding |
|----|---------|
| **EF-001** | Cache key omits rank_blocks/tag_trust/emit_capsule |
| **EF-045** | Fragment drives focus but omitted from cache key |

## Code evidence

- `Caching/TranscodeCacheEligibility.cs:13-41`
- `Caching/TranscodeCacheKey.cs:19-72`
- `Caching/TranscodeResponseCache.cs:24-121`
- `Tools/OccamTranscodeTool.cs:117-130`
- `docs-audit/subsystems/materialization.md` §4
- FLOW-019

## Public-doc relevance

**HIGH.** State default live extract; eligibility exclusions; disk location; do not promise annotation-safe keys until EF-001 fixed.

## Handbook relevance

Short “optional TTL cache” note under performance; warn about authenticated content and key caveats.
