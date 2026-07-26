# Playbook resolution

**Slug:** `playbook-resolution` · **Product system:** PS-5 Playbooks · **CAPs:** 11 · **Public relevance:** HIGH.

## What it is

Playbook resolution selects and merges site recipes from local, user, community, seed, and optionally live well-known genome sources. `occam_playbook_resolve` is the read-only inspection surface; the same resolver is embedded in transcode auto-policy, claim checking, dataset export, and knowledge extraction (CAP-070–072, CAP-490–497).

## Why it exists

- Apply site-specific selectors, preferred backend, notes, page classes, and knowledge schemas (CAP-070/071/491).
- Let local/operator recipes override shipped/community defaults without replacing every field (CAP-491).
- Optionally combine site-published genome/schema data with local recipes (CAP-494/495).
- Inspect whether the winning local file still verifies against this host's own key (CAP-493; trust limits below).

## User-visible entrypoints

| Surface | Behavior | Evidence |
|---|---|---|
| MCP `occam_playbook_resolve` | Read-only resolve/inspection; full profile only | `OccamPlaybookResolveTool.cs`; CAP-490 |
| `occam_transcode(playbook_policy=auto)` | Soft overlay; site fetch only if env enables | CAP-070–073 |
| `occam_claim_check` / `occam_attest` | Auto policy forced, no opt-out | CAP-693 |
| `occam_dataset_export` | Auto policy forced per row | CAP-772 |
| `occam_extract_knowledge` | Requires resolved knowledge schema | CAP-590 |

## Core behavior

1. Extract host from absolute HTTP(S) URL or bare hostname (CAP-490).
2. Load matching files from local, user, community, and seed tiers; cache them in process (CAP-491).
3. Sort by tier: local > user > community > seed (CAP-491).
4. Winner supplies id/version/provenance/source, while preferred backend, selectors, and notes each fall back independently through lower tiers (CAP-491).
5. Optionally fetch `https://host/.well-known/agent-genome.v1.json` and merge genome/schema with local-wins rules (CAP-494/495).
6. Attempt page-class/schema match; resolve tool swallows match failure into null fields (CAP-496).
7. Optionally expose up to ten redacted local lessons (CAP-497).
8. Inspect winning raw playbook signature against this host's one local key (CAP-493).

## Advanced behavior

| Mechanism | Semantics | Evidence |
|---|---|---|
| Community integrity | Manifest filename + normalized SHA-256 + forbidden-key hygiene | `CommunityManifest.cs`; CAP-492 |
| User/local/seed trust | Path/tier based; no community manifest/hygiene gate | CAP-491/492 |
| Genome merge | Shallow top-level union, playbook wins | `PlaybookGenomeMerger.cs`; CAP-494 |
| Knowledge schema merge | Nonempty local schema replaces site schema wholly | CAP-494 |
| Site-only result | Successful well-known fetch can produce result without local playbook | CAP-495 |
| Page class | Longest matching path pattern, then `default` | `KnowledgeSchemaPlanner.cs`; CAP-496 |
| Lessons | Local winner only, first 10, heuristic host redaction | CAP-497 |

## Automatic / silent behavior

- Tier file list is cached for process lifetime; external disk edits are not re-read (CAP-491).
- Field fallback is per-field, not whole-document (CAP-491).
- Community files absent/mismatched in manifest or with forbidden keys are silently skipped (CAP-492).
- `signature.status=unknown_key` is chosen by unsigned `provenance.keyId` before cryptographic verify; editing it can relabel tamper as foreign author (TRUST-MODEL EFC-P5-05-1).
- Schema match failure codes are discarded; result remains `ok:true` with null pageClass/schema (CAP-496).
- Failed well-known fetch is a nonfatal sidecar when local resolution succeeds (CAP-495).

## Parameters

| Name | Default | Effect | Evidence |
|---|---|---|---|
| `url` | required | Absolute URL or bare hostname | CAP-490 |
| `schema_version` | `"1.0"` | Version compatibility request | `OccamPlaybookResolveTool.cs`; CAP-491 |
| `include_lessons` | `false` | Returns capped/redacted lessons for local winner | CAP-497 |
| `fetch_site_genome` | `false` | Per-call well-known fetch; OR with env | CAP-495 |

No session, headers, backend policy, token, cache, receipt, or write parameter exists.

## Configuration

| Variable | Effect | Evidence |
|---|---|---|
| `OCCAM_PLAYBOOKS_LOCAL_ROOT` | Local highest-priority tier | CAP-374/491 |
| `WT_PLAYBOOKS_PATH` | Optional user/org tier | CAP-374/491 |
| `OCCAM_HOME` | Community/seed roots | `PlaybookSeedResolver.cs:334-338` |
| `OCCAM_SITE_GENOME_FETCH` | Enables well-known fetch as OR with call parameter | CAP-375/495 |
| `OCCAM_KEYS_ROOT` | Local key used only for self-key signature inspection | CAP-493/ART-034 |

## Backends

Ordinary resolve is filesystem-only. Optional site genome uses one guarded Core HTTP GET; no `OccamRouter`, browser, managed provider, or target-page extraction occurs (CAP-490/495; `PRODUCT-ARCHITECTURE.md:89`).

## Sessions / state

No session_profile/cookies. Resolver entries are cached in memory until production save calls the misleadingly named `ClearCacheForTests`, which also clears genome cache (CAP-575).

Well-known responses have a one-hour in-process cache (CAP-073; ST-10). Playbook files persist in tier roots (ST-07–09).

## Network behavior

Only optional well-known HTTPS fetch. It is caller-enabled here or env-enabled, has SSRF guard and typed failures, no browser, and no authenticated session (CAP-073/495).

This differs from transcode auto-policy, where no per-call site-fetch parameter exists (CAP-070/495).

## Artifacts produced

ART-017 resolve overlay/genome response: winner metadata, merged genome/schema, page class, signature inspection, and optional lessons (`ARTIFACT-ONTOLOGY.md:85`).

It may consume ART-015 local playbook files. It does not create a new receipt or signature.

## Trust / provenance properties

Community SHA-256 manifest is integrity checking, not authentication: an actor controlling file and manifest can replace both (TRUST-MODEL D10; G-E-03).

Playbook signature covers recipe body but excludes the entire top-level `provenance` object. Therefore `verify.score`, `passesGate`, `noiseLeakage`, `keyId`, `alg`, and `signedAt` are unsigned and editable (TRUST-MODEL §12 X1/X2; `PlaybookSignature.cs:36,45-46,63-84`).

`verified` means this host's local self-signed key validates the recipe body. It does not identify an author, guarantee recipe quality, or establish third-party provenance (CAP-493; TRUST-MODEL §13 forbidden claims 1/5/12).

## Failure / fallback behavior

- Invalid host input: top-level `invalid_arguments` (CAP-490).
- No tier/site result: `playbook_not_found` (CAP-495).
- Well-known failures (`private_url_blocked`, `http_*`, `not_json`, `invalid_manifest`, `timeout`, `network_error`) are nonfatal `GenomeFetch.failureCode` when local data exists (CAP-495).
- Schema match failures are swallowed into null fields (CAP-496).
- Site data fills only specific genome/schema gaps; local recipe fields otherwise remain authoritative (CAP-494).

## Platform differences

Filesystem paths and case behavior follow platform conventions. Local signing-key permission hardening is no-op on Windows and best-effort `0600` on POSIX, affecting inspection trust context (TRUST-MODEL §10.2; `ReceiptSigner.cs:84-99`).

Resolution semantics and tier order have no declared OS delta.

## Composition with other capabilities

- Feeds playbook overlays into acquisition/materialization (CAP-070–072).
- Feeds schema/page class into `schema-knowledge-extraction` (CAP-590).
- Consumes files written by `playbook-authoring`; save invalidates cache (CAP-575).
- Healing provides evidence for a draft but does not itself resolve existing recipes (CAP-549).
- Validation is advisory and not a prerequisite to resolve (CAP-759/760).

## Known limitations

- Full-profile-only direct tool.
- External file changes remain stale until restart/save cache bust (CAP-491).
- Community tier is unauthenticated (CAP-492).
- Foreign keys cannot be trusted/pinned; only `unknown_key` (CAP-493).
- Provenance/quality fields are unsigned (TRUST-MODEL X1).
- Schema failures are hidden (CAP-496).
- Merge is shallow and schema replacement is whole-object (CAP-494).
- No authenticated well-known fetch or browser.

## Engineering findings

- EFC-P5-05-1: unsigned `provenance.keyId` controls `unknown_key` short-circuit.
- EF-047: `PlaybookCommunitySanitizer` is Core-dead; do not cite it as active resolve hygiene. Active community check is `PlaybookCommunityHygiene`/manifest (C3).
- EF-048: well-known fetch has Content-Type/read-before-truncate weaknesses.
- CAP-496: schema failures swallowed.
- Prior prose claiming signed quality metrics is contradicted by binding TRUST-MODEL.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamPlaybookResolveTool.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookSeedResolver.cs`
- `src/FFOccamMcp.Core/Playbooks/CommunityManifest.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookCommunityHygiene.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookGenomeMerger.cs`
- `src/FFOccamMcp.Core/Playbooks/WellKnownGenomeFetcher.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookSignature.cs`
- CAP-070–073, CAP-490–497; ART-015/017; EF-047/048.

## Public-doc relevance

High. Explain tier and field precedence, cache lifetime, optional site fetch, shallow merge, schema-null behavior, community integrity limits, and exact signature meaning. Never describe quality fields as signed or a verified playbook as trusted third-party provenance.

## Handbook relevance

Use as the authoritative “which recipe applies?” card. Include tier examples, site-genome opt-in, reading signature states conservatively, and handoffs to transcode, knowledge extraction, lint, heal, and save.
