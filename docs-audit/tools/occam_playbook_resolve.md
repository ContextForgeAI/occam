# `occam_playbook_resolve` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`). `docs/`, `MCP_API_SPEC.md`,
`AGENTS.md`, `CLAUDE.md` were **not** used as evidence — every claim below cites a file read directly in
this session.

**CAP ID range owned by this audit:** `CAP-490`–`CAP-509` (new IDs minted: CAP-490…CAP-497; reuses Wave-1
CAP-070…073, CAP-281/282, CAP-374/375 rather than re-minting).

**Entry point:** `OccamPlaybookResolveTool.Resolve` (`src/FFOccamMcp.Core/Tools/OccamPlaybookResolveTool.cs`).
Read-only tool — no MCP parameter path in the tool method ever writes a file.

---

## 0. Schema

```
url (required), schema_version = "1.0", include_lessons = false, fetch_site_genome = false
```

Only `url` is required. `Resolve` builds a `PlaybookResolveOptions(url, schema_version, include_lessons,
fetch_site_genome)` and delegates entirely to `PlaybookSeedResolver.ResolveExtended`.

---

## CAP-490 — `url` accepts bare hostname, not just absolute URL (reused: CAP-050-adjacent, new behavior)

**Evidence:** `PlaybookSeedResolver.ExtractHost` (`Playbooks/PlaybookSeedResolver.cs:274-289`).

Unlike `occam_transcode`'s `url` (CAP-050, strict `Uri.TryCreate(..., UriKind.Absolute)` + http/https scheme
required), this tool's `ExtractHost` accepts **either** a full `http(s)://` URL **or** a bare hostname string
(e.g. `nginx.org`) — if the input contains no `/` or `:` it is treated directly as a hostname. No
`PrivacyClassifier`/SSRF check runs on the plain lookup path at all (no live fetch happens unless
`fetch_site_genome=true`) — this tool never dispatches a network request to the target `url` itself, only
(optionally) to `https://{host}/.well-known/agent-genome.v1.json` (CAP-073). Invalid input (contains `/` or
`:` but fails absolute-URI+scheme parse) → `invalid_arguments`.

## CAP-491 — Four-tier resolution with fallback-per-field, not per-document, override

**Evidence:** `PlaybookSeedResolver.ResolveExtended` lines 58-108; `PlaybookProvenance.TierRank`
(`local`=4, `user`=3, `community`=2, `seed`=1).

All host-matching entries across all four tiers are collected and sorted by tier rank descending
(`matches`). The **winning** entry (`match = matches[0]`) supplies `Id`/`SchemaVersion`/`Provenance`/
`SourcePath` unconditionally, but `PreferredBackend`, `ContentSelectors`, and `AgentNotes` are each resolved
independently via `matches.Select(...).FirstOrDefault(non-empty)` — i.e. **per-field overlay-with-fallback**
across the tier stack. A `local` genome that only sets 2 of 3 fields does not blank out the `seed` tier's
third field; it inherits it. This is a materially different (and more forgiving) merge model than a naive
"highest tier wins, full stop" reading of the tier list would suggest.

**Tier → directory mapping** (confirmed by reading `LoadEntries`):
- `seed` — `{OCCAM_HOME}/profiles/playbooks/seeds/*.seed.json` (repo-shipped, no signature check needed —
 first-party trusted).
- `community` — `{OCCAM_HOME}/profiles/playbooks/community/*.json`, gated by `manifest.json` sha256
 allow-list (CAP-492) — files not listed in the manifest, or with a mismatched hash, are silently skipped.
- `user` (= "WT" tier per the task brief) — `WT_PLAYBOOKS_PATH` env dir, patterns
 `*.json`/`*.playbook.json`/`*.seed.json` — **no hygiene/hash check at all** (trusted because
 operator-provisioned).
- `local` — `OCCAM_PLAYBOOKS_LOCAL_ROOT` (default `~/.occam/playbooks/local/`, per
 `docs-audit/ENVIRONMENT-VARIABLES.md:119` — not independently re-verified against `OccamUserPaths` in this
 pass), patterns `*.playbook.json`/`*.json` — the heal/save "learn" tier (CAP-070's local override), no
 hygiene check (first-party, produced by `occam_playbook_save` itself).

Entries are cached in-process (`_cache`, `lock`-guarded) after first load; `ClearCacheForTests()` is the only
invalidation path — a playbook file edited on disk after the host process started a call is **not**
re-read within the same process lifetime except via that test-only hook (worth flagging: no file-watch or
mtime check on the hot path).

## CAP-492 — Community tier: manifest-gated sha256 allow-list (distinct from user/local/seed trust model)

**Evidence:** `CommunityManifest.TryLoadFileHashes` / `TryReadVerifiedPlaybook`
(`Playbooks/CommunityManifest.cs`), `PlaybookCommunityHygiene.ContainsForbiddenKeys`
(`Playbooks/PlaybookCommunityHygiene.cs`).

The **only** tier that requires a `manifest.json` (`{playbooks: [{file, sha256}, ...]}`) sitting alongside
the playbook files: a community `*.json` file is loaded **only if** (a) its filename appears in the
manifest, (b) its SHA-256 (after CRLF→LF normalization) matches the manifest's declared hash exactly, and
(c) `PlaybookCommunityHygiene.ContainsForbiddenKeys` returns false. The hygiene walk recursively scans every
JSON property name in the file against a fixed deny-list (`cookie`, `authorization`, `bearer_token`,
`api_key`, `password`, `session_token`, `access_token`, `refresh_token`, etc., case-insensitive) — a
community-sourced playbook that carries any of these keys anywhere in its JSON tree (not just top-level) is
rejected outright, not sanitized/stripped. `manifest.json` itself is explicitly excluded from being treated
as a playbook candidate in every tier's directory scan.

**Not applied to `user`/`local`/`seed` tiers** — those directories have no hash-manifest gate and no
hygiene deny-list scan; trust is purely path-based (operator-provisioned or first-party).

## CAP-493 — Signature inspection is consumer-only at resolve time (reuse: CAP-282)

**Evidence:** `OccamPlaybookResolveTool.InspectSignature` (lines 42-59),
`PlaybookSignature.Inspect` (`Playbooks/PlaybookSignature.cs:97-140`).

After `PlaybookSeedResolver` picks a winning entry, the tool independently runs `PlaybookSignature.Inspect`
against the **raw winning playbook JSON** (`RawWinningPlaybookJson` — carried through the whole merge
pipeline specifically for this purpose) using the **local** signer's key id + public key PEM
(`ReceiptSigner.KeyId`/`ExportPublicKeyPem()` — the same key used for Receipt v1 signing, CAP-090). Four
possible `status` values, all surfaced to the caller as `Signature.status`:

- `unsigned` — no `provenance.signature` present at all (includes malformed/unparsable JSON — caught and
 downgraded to `unsigned` rather than throwing).
- `unknown_key` — a signature **is** present but its claimed `keyId` does not match this host's local key;
 the claim (`score`/`passesGate`) is still echoed but explicitly **not verified** — a real signature this
 host cannot check.
- `invalid` — claimed key **matches** local key but recomputed content-hash/signature no longer verify
 (tamper detection against your own key).
- `verified` — claimed key matches, hash + ECDSA-P256 signature both check out.

This means **only playbooks signed by the same host's own local key can ever show `verified`** — there is
no shared/distributed trust root; a playbook fetched from a teammate's `WT_PLAYBOOKS_PATH` or from the
community tier will read `unknown_key` at best (its claim is shown, but not cryptographically endorsed by
this host), never `verified`. This is the single most important "hidden non-obvious" fact about this tool:
the `Signature` block **looks like** third-party trust verification but is actually **self-trust
verification** (did *I* sign this, and is it still the bytes *I* signed) scoped to a single local keypair.

## CAP-494 — Genome merge (playbook genome ⊕ live site genome), not full document merge

**Evidence:** `PlaybookGenomeMerger.MergeGenome` / `MergeKnowledgeSchema`
(`Playbooks/PlaybookGenomeMerger.cs`).

Only two specific sub-objects — `genome` and `knowledge_schema` — are merge-eligible with the live
`.well-known/agent-genome.v1.json` fetch (CAP-073/CAP-495); everything else about the winning tier document
(`ContentSelectors`, `PreferredBackend`, `AgentNotes`, `Id`) comes exclusively from the local tier stack, the
live site never contributes to those fields. `MergeGenome` is a **shallow key-level union with local-wins**:
every top-level key from the site genome is written first, then every top-level key from the playbook
genome overwrites/adds on top (`playbookGenome` always wins on key collision) — this is not a deep/recursive
merge, so a nested object present in both with different sub-keys does not get unioned at the nested level,
the whole top-level key is replaced. `MergeKnowledgeSchema` is simpler still: if the local tier's
`knowledge_schema` is a non-empty object, the site's is ignored entirely (no merge at all, pure override);
only when the local tier has **no** `knowledge_schema` does the site's get used.

## CAP-495 — Live `.well-known` genome fetch is fully caller-gated here (opposite of CAP-070's transcode finding)

**Evidence:** `PlaybookResolveOptions.ShouldFetchSiteGenome` (`Playbooks/PlaybookResolveOptions.cs:9-19`);
reuses `WellKnownGenomeFetcher` (CAP-073).

`ShouldFetchSiteGenome()` returns true if **either** the tool's own `fetch_site_genome=true` parameter was
passed **or** `OCCAM_SITE_GENOME_FETCH=1|true` is set in the environment — i.e. on `occam_playbook_resolve`
the caller **does** have a direct, per-call knob to trigger the live network fetch (unlike `occam_transcode`,
where CAP-070/073 found `playbook_policy=auto` never triggers it without the separate env var). This is a
meaningful asymmetry between the two tools worth documenting explicitly: the same underlying subsystem
(`WellKnownGenomeFetcher`) is caller-controllable on `occam_playbook_resolve` but operator-only on
`occam_transcode`.

When `fetch_site_genome` is requested but no local tier match exists at all (`match is null`), the tool does
**not** fail with `playbook_not_found` if the site fetch itself succeeded — `BuildSiteOnlyResult` constructs
a full synthetic success response with `Provenance: "site"`, `SourcePath:
".well-known/agent-genome.v1.json"`, `PlaybookId` defaulting to the host name, `SchemaVersion` defaulting to
`"1.0"`, and `PreferredBackend` read from the site JSON's own `routing.preferred_backend` if present — a
genuinely "no local recipe, but the site self-published one" success path distinct from both the normal
merge path and the `playbook_not_found` failure path. `ContentSelectors`/`AgentNotes` are always `null` in
this branch (the site genome schema has no equivalent field consumed here).

Failure states from the fetch itself (`private_url_blocked`, `invalid_host`, `http_4xx/5xx`, `not_json`,
`invalid_manifest`, `timeout`, `network_error`) are surfaced as `GenomeFetch.failureCode` on an otherwise
**still-successful** resolve response when a local match exists — a failed live fetch never turns a
successful local-tier resolve into an overall tool failure; it only means `Genome`/merge did not get the
site's contribution.

## CAP-496 — `page_class` / `knowledge_schema` matching runs, but its own failure codes are swallowed here

**Evidence:** `PlaybookSeedResolver.ResolveExtended` lines 121-147 (`KnowledgeSchemaPlanner.TryMatch(...,
out _)` — the `out string? failureCode` parameter is explicitly discarded with `out _`).

`KnowledgeSchemaPlanner.TryMatch` runs against the **merged** genome+knowledge_schema root (only when the
merged `knowledge_schema` is a non-empty object) to resolve a `page_class` via longest-pattern-match against
`genome.page_classes` (trailing-`*` prefix match via `PageClassMatcher`, falling back to a literal
`"default"` key if no pattern matches) and returns the matched field-schema object. Distinct failure modes
exist inside the planner (`playbook_not_found` — impossible here since it's only called when a root exists;
`knowledge_schema_missing`, `page_class_unmatched`, `knowledge_schema_empty`) **but `occam_playbook_resolve`
discards all of them** — a non-match simply leaves `PageClass`/`KnowledgeSchema` as `null` on an otherwise
`Ok:true` response, never surfaces as a tool failure or even a warning field. `TranscodeAgentDecisions`
(`Agent/TranscodeAgentDecisions.cs:74-83`) has agent hints registered for exactly those three failure codes
(pointing the caller at `occam_transcode` instead) — but that hint code is **dead from this tool's call
path**; it is reachable only from whichever other tool (e.g. `occam_extract_knowledge`, out of this audit's
scope) actually propagates the planner's failure code outward instead of discarding it. Worth flagging as a
documentation/consistency gap: a caller who reads the failure-code catalog and expects
`occam_playbook_resolve` to ever return `knowledge_schema_missing` will never observe it.

## CAP-497 — `include_lessons`: local-tier-only, redacted, capped export

**Evidence:** `PlaybookSeedResolver.ResolveExtended` lines 149-157; `PlaybookLessonExporter.ExportRedactedLessons`
(`Playbooks/PlaybookLessonExporter.cs`); `PlaybookJsonElementWriter.RedactLessonHost` (referenced, not
independently re-read this pass — behavior inferred from call site: takes a `shouldRedactHost` predicate).

Three independent gates must all hold before `Lessons` is populated: `include_lessons=true` **and** the
winning match's `Provenance == PlaybookProvenance.Local` (i.e. lessons from `user`/`community`/`seed`-tier
wins are never exported, even if those files happen to carry a `lessons` array) **and** the raw playbook JSON
actually has a non-empty `lessons` array. Export caps at the **first 10** entries
(`lessons.EnumerateArray().Take(10)` — oldest-first by array order, not most-recent-first) and redacts each
lesson's host field via a token-looking-value heuristic (`LooksLikeToken`: length ≥ 24 chars, or contains
`token`/`secret`/`api_key` case-insensitively) before returning — a heuristic string-shape check, not a
structured secret-classifier. This directly mirrors `PlaybookHealPolicy.MaxLessonsPerFile`'s cap on the
write side (heal/save, out of this audit's scope) but is independently re-implemented as a hard `Take(10)`
literal here rather than referencing that constant — a minor duplication, not a functional bug (both happen
to be 10).

---

## CAP-070/071/072/073 — Playbook resolution/overlay/genome-fetch subsystem (Wave-1 reuse, confirmed identical mechanism)

`occam_playbook_resolve` **is** the standalone entry point into the exact same `PlaybookSeedResolver` /
`PlaybookResolveOptions` / `WellKnownGenomeFetcher` machinery that `occam_transcode`'s `playbook_policy=auto`
(CAP-070) drives internally — confirmed by both call sites constructing a `PlaybookResolveOptions` and
calling `PlaybookSeedResolver.ResolveExtended`. The two tools diverge only in (a) how `PlaybookResolveOptions`
is constructed (`occam_transcode`'s one-arg constructor always leaves `FetchSiteGenome=false` at the call
site — CAP-070's finding; `occam_playbook_resolve` passes the caller's `fetch_site_genome` through directly
— CAP-495 above) and (b) what happens with the result (`occam_transcode` applies it as a soft overlay onto
worker options — CAP-071/072; `occam_playbook_resolve` returns it verbatim as the MCP response, plus runs
the signature inspection CAP-282/493 that transcode's internal path does not perform at all).

## CAP-281/282 — Signature producer (save) / consumer (resolve) split (Wave-1 reuse from `trust-receipts.md`)

Restated for completeness: `CAP-281` (`PlaybookSignature.BuildSignedJson`, at `occam_playbook_save` — out of
this tool's scope) is the producer; `CAP-282` / this audit's `CAP-493` is the consumer half of the same SI-08
signing loop, confirmed exercised specifically inside `OccamPlaybookResolveTool.InspectSignature`.

## CAP-374/375 — Env surface (Wave-1 reuse from `config-env.md`, re-verified against code this pass)

- `OCCAM_PLAYBOOKS_LOCAL_ROOT` — local tier root override (CAP-374 half 1).
- `WT_PLAYBOOKS_PATH` — user/"WT" tier root; **no default** — when unset, `ResolveUserPlaybooksPath()`
 returns `null` and that tier contributes zero entries (confirmed: `LoadDirectory` early-returns on
 null/missing directory via `Directory.Exists` guard).
- `OCCAM_SITE_GENOME_FETCH` — CAP-375, re-confirmed as an **OR**, not an AND, with the per-call
 `fetch_site_genome` parameter (CAP-495).

---

## Cross-cutting categories checked (per shared instructions)

| Category | Finding |
|---|---|
| proxy | Used only transitively — `WellKnownGenomeFetcher` goes through `IHttpClientFactory` (`httpClientFactory.CreateClient("playbook.wellKnownGenome")`); egress-proxy env application to this specific named client was **not** independently re-verified this pass (out of scope; CAP-102 covers workers, not this Core-side `HttpClient`). |
| session | **Not used.** No `session_profile` parameter on this tool; the tool never authenticates to the target site. |
| cookies | Not used — no cookie handling anywhere in the resolve path. |
| headers | Only a hardcoded `User-Agent` on the well-known fetch (`WellKnownGenomeFetcher.cs:53-54`); no caller-supplied headers. |
| http | Only the well-known genome GET (CAP-073/495); the resolve tool never fetches the target `url` itself. |
| browser | **Not used.** No Playwright/browser backend involvement — this is a pure local-file + one narrow HTTP-GET tool. |
| managed | Not used. |
| retry | None observed — well-known fetch is a single attempt per call (subject to the 1h in-process cache, CAP-073). |
| cache | `WellKnownGenomeFetcher`'s 1-hour in-process genome cache (CAP-073) is the only cache; the `_cache` field on `PlaybookSeedResolver` itself is a load-once file-list cache, not a response cache, and has no TTL (only test-clearable). |
| diff | Not used. |
| blocks / tables / chunks | Not used — no markdown/materialization pipeline touched at all. |
| budget | Not used — no `max_tokens`/token budgeting; response size is inherently small (playbook metadata, not page content). |
| receipts | Not produced. `Signature` in the response is a **consumer-side inspection** of a pre-existing signature (CAP-493), not a fresh Receipt v1 envelope — this tool never calls `ReceiptSigner.SignDetached` to sign anything new, only `ReceiptSigner.KeyId`/`ExportPublicKeyPem()` to identify "am I the one who signed this." |
| merkle | Not used. |
| capsules | Not used. |
| playbooks | This tool **is** the playbook subsystem's read surface — see CAP-490-497 above. |
| datasets | Not used. |
| claims | Not used. |
| trust tags | Not used (`tag_trust`/block-level trust tagging is a transcode/materialization concept; this tool's only trust signal is `Signature.status`, CAP-493). |
| screenshots | Not used. |
| translate | Not used. |
| llms.txt | Not used (distinct from the `.well-known/agent-genome.v1.json` mechanism, CAP-073/495, which is playbook-specific, not the generic `prefer_llms_txt` transcode feature). |
| feeds | Not used. |
| profile | **Confirmed full-profile-only.** Per `docs-audit/subsystems/runtime-mcp.md` CAP-009 (re-verified reference, not re-derived from `OccamToolProfile.cs` source this pass): `occam_playbook_resolve` is registered under `OCCAM_PROFILE=full` only — absent from `reader`, `researcher`, and `auditor` profiles, grouped with `occam_playbook_heal`/`occam_playbook_save` as the three profile-gated-out-of-reduced-surfaces tools. |
| env | `OCCAM_PLAYBOOKS_LOCAL_ROOT`, `WT_PLAYBOOKS_PATH`, `OCCAM_SITE_GENOME_FETCH`, plus transitively whatever `OCCAM_HOME` resolution (`WorkerPaths.ResolveOccamHome()`) uses for the seed/community directories. |

---

## Failure code catalog for `occam_playbook_resolve`

| Code | Source | Notes |
|---|---|---|
| `invalid_arguments` | `PlaybookSeedResolver.ExtractHost`/`ResolveExtended` guard clauses | Empty/whitespace url, or a string containing `/`/`:` that doesn't parse as an absolute http(s) URI. |
| `playbook_not_found` | `PlaybookSeedResolver.Fail` when no tier match **and** no successful site-genome fetch | Has a registered agent hint (`continue` → use `occam_transcode` default policy). |

No other failure codes are reachable from this tool — `knowledge_schema_missing`/`page_class_unmatched`/
`knowledge_schema_empty` exist in the shared `KnowledgeSchemaPlanner` but are discarded here (CAP-496); the
well-known-fetch failure codes (`private_url_blocked`, `invalid_host`, `http_*`, `not_json`,
`invalid_manifest`, `timeout`, `network_error`) surface only as a non-fatal `GenomeFetch.failureCode` field
on an `Ok:true` response, never as the top-level tool failure.

---

## Hidden / non-obvious findings (summary)

1. **CAP-493** — `Signature.status=verified` only ever happens for playbooks signed by *this host's own*
 local key. The field reads like third-party provenance verification but is actually a tamper-check against
 your own prior signing, not a trust network. `unknown_key` (a genuinely foreign, unverifiable signature) and
 `unsigned` (no signature at all) are easy to conflate at a glance if a caller doesn't read `status` carefully.
2. **CAP-495** — the live `.well-known/agent-genome.v1.json` fetch is **caller-gated** on this tool
 (`fetch_site_genome=true` works directly) but **not** caller-gated when reached via `occam_transcode`'s
 `playbook_policy=auto` (CAP-070/073) — same subsystem, opposite default-exposure decision depending on
 which of the two tools drives it.
3. **CAP-496** — `page_class`/`knowledge_schema` match failures are silently swallowed into a still-`Ok:true`
 response with null fields, even though the shared planner defines named failure codes and the agent-hints
 table has entries registered for them that this tool's call path never reaches.
4. **CAP-492** — trust model is **tier-asymmetric**: only the `community` tier is hash-manifest-gated and
 hygiene-scanned; `user`/`local`/`seed` tiers are trusted purely by filesystem path with zero content
 inspection — a compromised `WT_PLAYBOOKS_PATH` value (or a malicious file dropped into the local learn
 tier by some other process) has no hygiene backstop at all.
5. **CAP-491** — the tier "override" is a **per-field**, not per-document, fallback — a common
 misunderstanding risk when reading the tier list as a simple precedence stack.
6. Bare-hostname input mode (CAP-490) has zero SSRF/privacy classification — but this is low-risk in
 practice since the tool never dispatches a request to the parsed host itself (only to a derived
 `.well-known` URL that is independently re-classified by `WellKnownGenomeFetcher`, CAP-073).

## Uncertainties

- `OCCAM_PLAYBOOKS_LOCAL_ROOT`'s exact default value (`~/.occam/playbooks/local/`) was taken from
 `docs-audit/ENVIRONMENT-VARIABLES.md` (a Wave-1 artifact, not re-derived from `OccamUserPaths.cs` source in
 this pass) — flagged rather than independently re-verified against that helper's implementation.
- Whether `IHttpClientFactory`'s `"playbook.wellKnownGenome"` named client honors `OCCAM_HTTP_PROXY`/
 `OCCAM_HTTPS_PROXY` (CAP-102/CAP-166 territory) was not independently re-verified against
 `OccamServiceCollectionExtensions.cs`'s named-client registration in this pass.
- `PlaybookJsonElementWriter.RedactLessonHost`'s exact redaction shape (which field(s) it rewrites, and
 whether redaction replaces vs. removes the host value) was not opened this pass — inferred only from the
 `include_lessons`/CAP-497 call site and its `shouldRedactHost: Func<string?, bool>` signature.

## Capability graph edges

```
TOOL:occam_playbook_resolve|USES|CAP-490
TOOL:occam_playbook_resolve|USES|CAP-491
TOOL:occam_playbook_resolve|USES|CAP-492
TOOL:occam_playbook_resolve|USES|CAP-493
TOOL:occam_playbook_resolve|USES|CAP-494
TOOL:occam_playbook_resolve|USES|CAP-495
TOOL:occam_playbook_resolve|USES|CAP-496
TOOL:occam_playbook_resolve|USES|CAP-497
TOOL:occam_playbook_resolve|USES|CAP-070
TOOL:occam_playbook_resolve|USES|CAP-282
TOOL:occam_playbook_resolve|USES|CAP-374
TOOL:occam_playbook_resolve|USES|CAP-375
PARAM:url|ENABLES|CAP-490
PARAM:schema_version|ENABLES|CAP-491
PARAM:include_lessons|ENABLES|CAP-497
PARAM:fetch_site_genome|ENABLES|CAP-495
CAP-491|ROUTES_TO|filesystem:local-tier
CAP-491|ROUTES_TO|filesystem:user-tier(WT_PLAYBOOKS_PATH)
CAP-491|ROUTES_TO|filesystem:community-tier
CAP-491|ROUTES_TO|filesystem:seed-tier
CAP-492|CONSUMES|manifest.json
CAP-492|PRODUCES|hygiene-verdict
CAP-493|CONSUMES|ReceiptSigner.KeyId
CAP-493|CONSUMES|ReceiptSigner.ExportPublicKeyPem
CAP-493|PRODUCES|Signature(status,keyId,score,passesGate)
CAP-494|PRODUCES|Genome(merged)
CAP-494|PRODUCES|KnowledgeSchema(merged)
CAP-495|ROUTES_TO|WellKnownGenomeFetcher
CAP-495|FALLS_BACK_TO|local-tier-only-result
CAP-496|PRODUCES|PageClass
CAP-496|CONSUMES|KnowledgeSchemaPlanner
CAP-497|PRODUCES|Lessons(redacted,max10)
CAP-070|ROUTES_TO|PlaybookSeedResolver
CAP-073|ROUTES_TO|WellKnownGenomeFetcher
CAP-281|PRODUCES|provenance.signature(at save, out of scope)
CAP-282|CONSUMES|provenance.signature(at resolve)
```

## Completeness

`COMPLETENESS: COMPLETE` — every code path inside `OccamPlaybookResolveTool` and its direct dependency graph
(`PlaybookSeedResolver`, `PlaybookResolveOptions`, `PlaybookGenomeMerger`, `KnowledgeSchemaPlanner`,
`PlaybookSignature.Inspect`, `WellKnownGenomeFetcher`, `CommunityManifest`, `PlaybookCommunityHygiene`,
`PlaybookLessonExporter`, `PlaybookProvenance`, `PlaybookPaths`) was opened and read this pass; response
model and JSON serialization context were read; profile-visibility claim was cross-checked against the
Wave-1 `runtime-mcp.md` subsystem report rather than re-deriving `OccamToolProfile.cs` from scratch.
