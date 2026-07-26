# `occam_digest` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`). Documentation
(`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md`) was **not** used as evidence.

**CAP ID range owned by this audit:** `CAP-450`–`CAP-489` (used: CAP-450…CAP-460; remainder
reserved, not exhausted). Where `occam_digest` merely re-invokes `TranscodePipeline`/`OccamRouter`
machinery already documented in Wave 1, this report **references** the Wave-1 CAP ID
(`CAP-050`…`CAP-112`) instead of re-documenting the mechanism, per assignment instructions.

**Files inspected:**
`Tools/OccamDigestTool.cs`, `Tools/OccamDigestModels.cs`, `Digest/DigestInputContract.cs`,
`Digest/DigestInputNormalizer.cs`, `Digest/DigestUrlParser.cs`, `Agent/DigestAgentHints.cs`,
`Services/DigestService.cs`, `Services/DigestParallelism.cs`, `Workers/HttpExtractRoutingScope.cs`,
`Workers/BrowserConcurrencyLimiter.cs`, `Routing/OccamTranscodeOptions.cs`,
`Routing/TranscodePipeline.cs`, `Session/FetchPreflight.cs`, `Compile/FocusMatcher.cs`
(`MatchesForDigest`/`EvaluateForDigest`), `Tools/OccamTranscodeModels.cs`
(`OccamTranscodeResponseBuilder.BuildReceipt`), `Transport/OccamMcpServerRegistration.cs`
(digest `urls` schema-union patch site), `Transport/OccamToolProfile.cs`.

---

## 0. Entry point and schema

`OccamDigestTool.Digest` (`Tools/OccamDigestTool.cs`) — `[McpServerTool(Name = "occam_digest")]`.
Parameters (all optional at the C# signature level, but see **CAP-450** for the real
"at-least-one" contract):

```
urls, backend_policy, max_urls, per_url_max_tokens, focus_query, fit_markdown,
include_combined, session_profile, source_url, max_links, if_none_match
```

Unlike `occam_transcode`, `occam_digest` is registered **manually** in
`OccamMcpServerRegistration.AddOccamMcpServer` (not via `builder.WithTools<T>()`) specifically so
its generated `InputSchema` can be patched post-hoc — see **CAP-450**.

---

## CAP-450 — `urls` schema union + "urls and/or source_url" input contract

**Evidence:** `Transport/OccamMcpServerRegistration.cs` (`WithDigestUrlsUnion`,
`DigestUrlsUnionSchema` constant), `Digest/DigestInputContract.cs` (`TryValidate`).

The reflection-generated JSON Schema for a `JsonElement? urls` parameter would normally be an
unconstrained object/any-type. `WithDigestUrlsUnion` walks the tool's serialized `InputSchema` at
registration time and replaces the `urls` property definition in-place with a hand-written
`oneOf`: **(a)** an array of URI strings (`minItems:1, maxItems:256`) — the schema calls this
"Preferred" — or **(b)** a bare string (`minLength:1`) for "deprecated compatibility" (JSON-array
string or newline/comma-separated list). This is the only tool in the audited set whose MCP
schema is edited outside the standard `[Description]`-attribute pipeline; every other digest
parameter uses the normal source-generated path.

`DigestInputContract.TryValidate` is the actual gating logic, independent of schema shape: at
least one of `urls` / `source_url` must be present, else `invalid_arguments` with message
`"Provide urls and/or source_url (at least one). When source_url is set, urls is ignored."` When
`source_url` is non-blank, `useSourceUrl=true` and **`urls` is unconditionally ignored** even if
both were supplied — there is no error or warning surfaced when a caller passes both (silent
override, not a conflict failure).

## CAP-451 — `urls` normalization: array path vs. legacy string path have **different** capabilities

**Evidence:** `Digest/DigestInputNormalizer.cs`, `Digest/DigestUrlParser.cs`.

Two structurally different parsers exist for the same public field, and they are **not**
equivalent in what they accept:

- **Array path** (the schema's "Preferred" shape — `JsonValueKind.Array`): every element must be a
 bare JSON string; `DigestInputNormalizer` immediately errors (`"urls array entries must all be
 URL strings."`) on any non-string element (e.g. an object). Entries built this way always have
 `FocusQuery = null`.
- **Legacy string path** (`JsonValueKind.String`, the "deprecated compatibility" shape):
 `DigestUrlParser.TryParse` first tries `JsonDocument.Parse` if the trimmed string starts with
 `[`. If it parses as a JSON array, **each element may be a string OR an object
 `{"url": ..., "focus_query": ...}`** — the object form is what actually lets a caller set a
 **per-URL `focus_query` override** (consumed later in `DigestService.TranscodeEntryAsync` as
 `entry.FocusQuery ?? focusQuery`). If the string doesn't start with `[`, it falls back to
 splitting on `\n`, `\r`, `,`, `;` (no per-entry focus in that sub-mode either).

**HIDDEN finding:** the tool description's "Preferred: array of URL strings" path can **never**
set a per-entry `focus_query` — that capability is reachable only through the "Deprecated
compatibility" string-encoded-JSON-array form. A caller following the schema's own preference
guidance loses access to a real, tested capability (`DigestUrlEntry.FocusQuery`) that the
tool description doesn't mention exists at all.

Both paths converge on shared limits: `MaxInputCharacters = 65_536`, `MaxInputItems = 256`, and
case-insensitive dedup by exact URL string (`DigestUrlParser.DeduplicateEntries`) — silent, not
reported back to the caller (no "N duplicates removed" signal).

## CAP-452 — Per-URL SSRF/session preflight is a **whole-batch** gate, not per-item

**Evidence:** `Services/DigestService.cs` `DigestAsync`, `Session/FetchPreflight.Prepare`.

Before any transcode runs, `DigestAsync` loops every resolved entry and calls
`FetchPreflight.Prepare(entry.Url, sessionProfile)` (same SSRF/`PrivacyClassifier` check as
`occam_transcode`'s **CAP-100**, and the same `session_profile` resolution as **CAP-068**/
**CAP-069**). **If any single URL in the batch fails this preflight** (invalid URL shape, private/
loopback host, or a broken `session_profile`), `DigestAsync` returns immediately with a **whole-
digest** failure (`invalid_url` / `private_url_blocked` / session failure code) — **none** of the
other, valid URLs in the batch are attempted. This is a materially different failure isolation
model from runtime extraction failures (timeout, 404, thin extract, etc.), which **are** isolated
per-item in `items[]` while the digest as a whole still returns `ok:true` for the surviving URLs
(see **CAP-455**). A caller mixing one malformed/private URL into an otherwise-good 8-URL research
batch gets **zero** successful transcodes back, with no partial-success path for this specific
class of failure.

## CAP-453 — `max_urls` / `max_links` clamps and silent truncation

**Evidence:** `Services/DigestService.cs`, `DigestService.MaxUrlsCap = 8`.

`max_urls` is clamped to `[1, 8]` regardless of caller input (values above 8 are silently capped,
not rejected); if the resolved entry count exceeds the clamped `max_urls`, entries are truncated
with `.Take(maxUrls)` — **no warning field indicates URLs were dropped** (the tool description says
"Extra URLs are dropped" but the response carries no count of how many, unlike `OmittedManifest`
for token budget on `occam_transcode`, **CAP-067**). `max_links` (AF-5 discovery cap) is separately
clamped to `[1, 8]` inside `DiscoverLinksFromSourceAsync`.

## CAP-454 — `per_url_max_tokens` ambient-budget resolution + floor

**Evidence:** `Tools/OccamDigestTool.cs` (`clientCapabilities.ResolveMaxTokens(per_url_max_tokens)`),
`Services/DigestService.cs` (`MinTokenBudget = 128`).

Same ambient-budget mechanism as `occam_transcode`'s `max_tokens` — reuses
`ClientCapabilityStore.ResolveMaxTokens` (**CAP-060**/**CAP-304**): an explicit
`per_url_max_tokens` always wins; otherwise the per-session budget from
`occam_client_capabilities`/`OCCAM_CLIENT_CONTEXT_TOKENS` is used, with the same ~20%-of-context
derivation. The budget applies **per URL**, not to the whole digest — an 8-URL digest at the
default ambient budget can therefore consume up to 8× that budget in total (visible in
`stats.totalTokensEstimated`, **CAP-456**). A supplied value below the 128-token floor is rejected
with `invalid_arguments` for the **whole digest** (checked once, after budget resolution, before
any URL is touched) — same floor value as `occam_transcode`'s min-token floor (**CAP-305**), not a
digest-specific number.

## CAP-455 — Digest fan-out: reduced per-URL `OccamTranscodeOptions` + parallel HTTP one-shot routing

**Evidence:** `Services/DigestService.cs` `TranscodeEntryAsync`/`TranscodeAllEntriesAsync`,
`Routing/OccamTranscodeOptions.cs`, `Workers/HttpExtractRoutingScope.cs`.

Each URL is transcoded via the **same** `TranscodePipeline.TranscodeAsync` used by
`occam_transcode` (references **CAP-052** `http_then_browser` cascade, **CAP-094**-**CAP-098**
post-processor/quality/recovery-log machinery, **CAP-100** SSRF, **CAP-102** proxy, **CAP-103**
robots/throttle, **CAP-104** domain tiers — all apply identically per URL), but the
`OccamTranscodeOptions` record built for each entry (`Services/DigestService.cs:301-307`)
populates **only** `MaxTokens`, `FitMarkdown`, `FocusQuery`, `SessionProfile`. Every other
transcode-level knob is left at its record default:

- `PlaybookPolicy = "off"` — **digest never triggers playbook/genome-aware resolution**
 (**CAP-070**), even though `occam_transcode`'s default cascade can. A site with a saved/curated
 playbook gets the **same** generic extraction from `occam_digest` as from a bare
 `occam_transcode(playbook_policy=off)` call — there is no way to opt a digest batch into
 playbook-aware extraction.
- `ContentSelectors = []`, `IfNoneMatch = null` (per-item; see **CAP-458** for the separate
 digest-level `if_none_match`), `SemanticChunking = false`, `CaptureScreenshot = false`,
 `JsonBlocks = false`, `JsonTables = false`, `JsonFeed = false`, `TranslateTo = null`,
 `DiffAgainst = null` — **none** of `occam_transcode`'s sidecar/codec features (**CAP-075**,
 **CAP-076**, **CAP-079**, **CAP-080**, **CAP-081**, **CAP-082**) are reachable through
 `occam_digest` at all; there is no parameter on the digest schema to request any of them per URL
 or for the batch.
- `TranscodePipeline` still unconditionally adds internal `json_blocks`/`json_tables` feature
 flags for the worker regardless of these options (**CAP-078**, confirmed identical code path) —
 so the DOM block/table walk cost is still paid per URL in a digest even though the digest
 response never exposes blocks/tables.

**Parallelism (CAP-459):** `DigestParallelism.ResolveMaxParallel` picks a max-concurrency value
(1 for a single URL; env-overridable; otherwise 4 for `http`-only policy or the shared browser
concurrency-gate value `BrowserConcurrencyLimiter.ResolveMaxParallel()` — default 2, env
`OCCAM_BROWSER_MAX_PARALLEL` — for `browser`/`http_then_browser` policy). When `maxParallel > 1`,
`TranscodeAllEntriesAsync` fans out with `Task.Run` + a `SemaphoreSlim` gate (not
`Parallel.ForEachAsync`), and — **non-obvious plumbing detail worth flagging** — each parallel
task pushes `HttpExtractRoutingScope.PushOneShot()` around its own transcode call. This
`AsyncLocal` scope forces that specific async flow's HTTP backend to spawn a **fresh one-shot**
Node process instead of routing through the shared persistent HTTP daemon (referenced from Wave 1
`CAP-201`/browser-workers subsystem report) — i.e. **a parallel digest batch does not share the
single HTTP daemon process across its concurrent URLs**; each concurrent slot gets its own
one-shot worker spawn. Sequential digests (`maxParallel<=1`, e.g. a 1-URL digest, or
`OCCAM_DIGEST_PARALLEL=0`) do **not** push this scope and so can still use the shared daemon like
a normal `occam_transcode` call.

## CAP-456 — Response shape: `items[]`, `stats`, `combined`, `digestId`

**Evidence:** `Tools/OccamDigestModels.cs`.

- `digestId` — a 16-hex-char SHA-256 prefix over the **sorted, trailing-slash-trimmed, case-
 insensitive** URL set (`DigestService.ComputeDigestId`) — deterministic per URL **set**, order-
 independent, and does not depend on any of the compile-time options (focus query, token budget,
 etc.), unlike `occam_transcode`'s `MaterializationKey` (**CAP-093**) which does. Two digests over
 the same URLs but different `focus_query`/`per_url_max_tokens` get the **same** `digestId`.
- `items[]` — per-URL `OccamDigestItemInfo`: `url`, `ok`, `title` (first `#`-heading line found in
 the compiled markdown — a naive first-line-starting-with-`#` scan, not the worker's own title
 extraction), `excerpt` (= the full per-URL compiled markdown, not actually a truncated
 "excerpt" — the field name undersells its content), `backend`, `tokensEstimated`, `failure`,
 `focusQuery` (the effective, possibly per-entry-overridden query for that item), `focusMatched`,
 `mediaRefs` (reuses **CAP-109**'s always-on media-ref mapping), `confidence`, `receipt` (see
 **CAP-457**), `access`/`focus`/`completeness` (reuses **CAP-107** `SemanticOutcomeMapper` — same
 three-axis honesty fields as `occam_transcode`, computed per item).
- `stats` — `requested` (= entry count before per-item transcode, i.e. after truncation/dedup),
 `succeeded`, `failed`, `totalTokensEstimated` (sum over **successful** items only).
- `combined` — only built `if (includeCombined && okCount > 0)`: `string.Join("\n\n", …)` of
 `## {title|url}\n\n{excerpt}` for every ok item with a non-empty excerpt — failed items are
 silently excluded from `combined` with no placeholder marker.
- Whole-digest failure (`okCount == 0`): returns `ok:false`, `failureCode:"digest_failed"`,
 `message:"All URLs failed to transcode."`, but **still includes** `items[]`/`stats` (all failed)
 so the caller can see each individual failure code even though the digest itself is `ok:false` —
 confirmed by `OccamDigestResponseMapper.MapFailure` explicitly serializing `Items`/`Stats` when
 present, unlike a pure `invalid_arguments`-class failure which has neither.

## CAP-457 — Per-item Receipt v1 signing (reduced: content-hash-only, no block leaves, no time anchor)

**Evidence:** `Services/DigestService.cs` (`TranscodeEntryAsync` receipt-build call),
`Tools/OccamTranscodeModels.cs` (`OccamTranscodeResponseBuilder.BuildReceipt`).

Each **successful** item independently calls the same shared `BuildReceipt` builder used by
`occam_transcode` (Wave 1 **CAP-090**/**CAP-278**), gated by the same global `OCCAM_RECEIPTS`
kill-switch (`ReceiptsPolicy.Enabled()`, **CAP-280**) — there is no separate digest-specific
receipts flag. Two digest-specific reductions versus a standalone `occam_transcode` receipt:

1. **No block-level Merkle leaves.** Because the per-URL `OccamTranscodeOptions` never sets
 `JsonBlocks=true` (**CAP-455**), the pipeline's `ExposePublicBlocks` gate is false and
 `TranscodeOutcome.Blocks` comes back `null` to `DigestService` — `BuildReceipt` then computes its
 signature over an **empty** block array, i.e. the signed envelope is a single whole-document
 content-hash commitment, not a per-block Merkle tree a caller could later request a membership
 proof (**CAP-262**) against.
2. **No time anchor.** `BuildReceipt(..., timeAnchor: null)` — explicit in code, with a comment
 explaining the reason (avoiding N RFC3161 calls, one per digest URL) — so digest items can never
 carry the optional TSA-strengthened receipt (**CAP-092**) that a standalone `occam_transcode`
 call can request.

There is **no digest-level receipt** over the `combined` text or the URL set as a whole — only
per-item receipts exist; `emit_capsule` (**CAP-086**) is not reachable from `occam_digest` at all
(no such parameter).

## CAP-458 — `if_none_match` (digest-level, whole-`combined`-body conditional)

**Evidence:** `Services/DigestService.cs` (`ContentHashToken.Matches(combined, ifNoneMatch)`).

Distinct code path from `occam_transcode`'s `if_none_match` (**CAP-074**, which hashes one page's
compiled markdown): here the hash is computed over the **entire joined `combined` string** (all
ok items concatenated), only evaluated when `includeCombined` produced a non-null combined **and**
at least one item succeeded. On a match, the response sets `unchanged:true` and **blanks the
`combined` field to an empty string** — but, unlike `occam_transcode`'s `delta_only`/`if_none_match`
interaction, `items[]` (full per-item excerpts) are **still returned in full** even when
`unchanged:true` — so `if_none_match` on `occam_digest` only ever saves the `combined` field's
bytes, never the per-item bodies. There is no digest equivalent of `occam_transcode`'s
`delta_only` parameter to also suppress `items[].excerpt`.

## CAP-459 — AF-5: `source_url` link auto-discovery (focused vs. unfocused strategies)

**Evidence:** `Services/DigestService.cs` `DiscoverLinksFromSourceAsync` /
`DiscoverFocusedLinksAsync` / `DiscoverViaHtmlAsync`, referencing `Services/MapService.cs`,
`Services/SitemapDiscovery.cs`, `Services/MapLinkFilter.cs`, `Services/MapLinkRanker.cs`,
`Probe/HtmlLinkExtractor.cs` — these classes are shared with `occam_map` and are **not**
re-documented here (per assignment: reference, don't duplicate; full audit belongs to the
`occam_map` Wave 2 report).

When `source_url` is set, `urls` is ignored entirely (**CAP-450**) and the tool runs one of two
different discovery strategies depending on whether `focus_query` is also set:

- **No `focus_query`:** cheaper path — try `SitemapDiscovery` first (`sameDomainOnly:true`,
 15s timeout, `robotsOnly:false`), filter obvious junk links (`MapLinkFilter.IsNonsense`), rank,
 cap to `max_links`; only falls through to a raw HTML `<a>` link scrape
 (`HtmlLinkExtractor.Extract`) if the sitemap path yields **zero** usable links.
- **With `focus_query`:** more expensive path — runs `MapService.MapAsync` against the homepage
 (`source:"homepage"`, requests the **full** `MapService.MaxLinksCap`, not the caller's smaller
 `max_links`, specifically so ranking isn't pre-truncated) **and** a sitemap discovery pass
 (candidate cap `Math.Min(120, Math.Max(maxLinks*8, 32))`), pools and deduplicates both sources by
 exact URL, then ranks the **combined** pool with `MapLinkRanker.Rank(…, focusQuery, maxLinks)`
 before capping. Falls back to HTML scraping only if the combined pool is empty.

If discovery yields zero links (either strategy), the whole digest fails with `invalid_urls` /
`"source_url did not yield any discoverable links."` — a **distinct failure code** from the
`urls`-path's `invalid_arguments` (**CAP-450**), worth noting for callers building generic
digest-failure handling around a single code. Discovered URLs are surfaced back to the caller in
`discoveredLinks[]` (bare `{url}` records) alongside the normal `items[]`, so a caller can see
*which* URLs were auto-selected — this is the only tool-level visibility into what "auto-discovery"
actually picked.

## CAP-460 — Digest-level agent hints: `suggestedReadOrder` + focus-honesty warnings/decisions

**Evidence:** `Agent/DigestAgentHints.cs`.

This is a **digest-specific** hint layer, structurally different from `occam_transcode`'s failure-
only `agentMeta.decisions` (**CAP-106**) — it fires on **success** responses too, not just
failures, and reasons over the **whole batch**, not one page:

- `suggestedReadOrder` defaults to `"combined"`, downgrades to `"items_by_focusMatched"` when some
 (but not all) ok items have `focusMatched:false`, and downgrades further to `"items_only"` when
 **every** ok item missed the focus query (`FocusNotFound` — computed as "focus_query was set and
 every successful item's `focusMatched` is false", **DigestService.FocusNotFound**) — an explicit
 honesty guard telling the agent not to cite `combined` as a focused answer when nothing actually
 matched.
- `warnings[]` — free-text machine-readable strings: `check_items_before_combined`,
 `hub_in_digest` (mixed strong+weak focus matches — index/TOC pages may mislead), `focus_not_found`,
 `hub_excerpt:<url>` (heuristic hub/TOC detection: excerpt ≥80 chars, ≥8 markdown links, and
 contains "Guide"/"table of contents"/"In this article" — **DigestAgentHints.LooksLikeHubExcerpt**),
 `partial_digest:<n> URL(s) failed`.
- `decisions[]` (`DigestDecision{Action, Reason, Url?}`) — `focus_not_found` + `iterate_items` when
 the whole batch missed the focus; `skip_failed` ("Do not invent content for failed digest items")
 whenever `analysis.Failed > 0`.

None of this heuristic layer (hub-excerpt detection, focus-honesty downgrade) exists on
`occam_transcode`'s single-page response — it is a genuinely new, digest-only capability aimed at
preventing an agent from over-trusting a multi-page `combined` block.

---

## Failure codes specific to (or notably shaped by) `occam_digest`

| Code | Source | Scope |
|---|---|---|
| `invalid_arguments` | `DigestInputContract`/`DigestInputNormalizer` (bad shape, empty array, >256 items, >65536 chars, missing urls+source_url, `per_url_max_tokens` below floor) | whole digest |
| `invalid_urls` | `DigestService` — `source_url` discovery yielded zero links (**CAP-459**) | whole digest |
| `invalid_url` | `FetchPreflight.Prepare` — malformed URL found during the whole-batch preflight (**CAP-452**) | whole digest (not per-item) |
| `private_url_blocked` | `PrivacyClassifier` via preflight, whole-batch (**CAP-452**, reuses **CAP-100**) | whole digest |
| session failure codes | `SessionProfileHeaders.Resolve` (invalid id / file not found) | whole digest |
| `workers_unavailable` | `WorkerPaths.IsConfigured` false | whole digest |
| `digest_failed` | every URL individually failed transcode (`okCount==0`) | whole digest, but `items[]`/`stats` still populated |
| per-item transcode codes | any `occam_transcode` failure code (**CAP-105** taxonomy), normalized; `content_extraction_failed`→`extraction_failed` remap specific to this mapper | per item only, does not fail the digest as a whole |
| `invalid_policy` | `OccamBackendPolicyParser` on `backend_policy` | whole digest |

---

## Capability graph edges

```
TOOL:occam_digest|USES|CAP-450
TOOL:occam_digest|USES|CAP-451
TOOL:occam_digest|USES|CAP-452
TOOL:occam_digest|USES|CAP-453
TOOL:occam_digest|USES|CAP-454
TOOL:occam_digest|USES|CAP-455
TOOL:occam_digest|USES|CAP-456
TOOL:occam_digest|USES|CAP-457
TOOL:occam_digest|USES|CAP-458
TOOL:occam_digest|USES|CAP-459
TOOL:occam_digest|USES|CAP-460
TOOL:occam_digest|USES|CAP-052
TOOL:occam_digest|USES|CAP-060
TOOL:occam_digest|USES|CAP-064
TOOL:occam_digest|USES|CAP-068
TOOL:occam_digest|USES|CAP-069
TOOL:occam_digest|USES|CAP-078
TOOL:occam_digest|USES|CAP-094
TOOL:occam_digest|USES|CAP-095
TOOL:occam_digest|USES|CAP-096
TOOL:occam_digest|USES|CAP-097
TOOL:occam_digest|USES|CAP-098
TOOL:occam_digest|USES|CAP-100
TOOL:occam_digest|USES|CAP-102
TOOL:occam_digest|USES|CAP-103
TOOL:occam_digest|USES|CAP-104
TOOL:occam_digest|USES|CAP-105
TOOL:occam_digest|USES|CAP-107
TOOL:occam_digest|USES|CAP-109
TOOL:occam_digest|USES|CAP-280
TOOL:occam_digest|DOES_NOT_USE|CAP-070
TOOL:occam_digest|DOES_NOT_USE|CAP-086
PARAM:urls|ENABLES|CAP-450
PARAM:urls|ENABLES|CAP-451
PARAM:source_url|ENABLES|CAP-459
PARAM:max_links|ENABLES|CAP-459
PARAM:focus_query|ENABLES|CAP-064
PARAM:per_url_max_tokens|ENABLES|CAP-454
PARAM:if_none_match|ENABLES|CAP-458
PARAM:session_profile|ENABLES|CAP-068
PARAM:backend_policy|ROUTES_TO|CAP-052
CAP-450|CONSUMES|source_url
CAP-451|PRODUCES|DigestUrlEntry[]
CAP-452|CONSUMES|session
CAP-452|ROUTES_TO|FetchPreflight
CAP-455|ROUTES_TO|TranscodePipeline
CAP-455|FALLS_BACK_TO|http_daemon_bypass_one_shot
CAP-455|CONSUMES|OCCAM_DIGEST_PARALLEL
CAP-455|CONSUMES|OCCAM_DIGEST_MAX_PARALLEL
CAP-455|CONSUMES|OCCAM_BROWSER_MAX_PARALLEL
CAP-456|PRODUCES|digestId
CAP-456|PRODUCES|combined
CAP-457|PRODUCES|receipt
CAP-457|CONSUMES|OCCAM_RECEIPTS
CAP-458|PRODUCES|unchanged
CAP-459|ROUTES_TO|MapService
CAP-459|ROUTES_TO|SitemapDiscovery
CAP-459|ROUTES_TO|HtmlLinkExtractor
CAP-459|PRODUCES|discoveredLinks
CAP-460|PRODUCES|agentHints
```

---

## Cross-cutting category checklist

- **proxy** — not a digest parameter; inherited transparently per-URL via `TranscodePipeline` →
 `OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY` (**CAP-102**). Not used for `MapService`'s discovery fetch
 path independently confirmed here — out of scope (belongs to `occam_map` audit).
- **session** — `session_profile` is a first-class digest parameter, applied identically to every
 URL in the batch (**CAP-068**/**CAP-452**); no per-URL session override exists.
- **cookies/headers** — not used directly by digest code; flows through `session_profile` only
 (same as transcode).
- **http/browser** — both reachable via `backend_policy`; digest adds its own parallelism/one-shot
 layer on top (**CAP-455**).
- **managed** — reachable transitively (any per-URL `TranscodePipeline` call can escalate to a
 managed provider per **CAP-054** if the operator has it configured) but **digest cannot force it
 and has no visibility knob for it** — same env-gated invisibility as transcode.
- **retry** — none at the digest level; per-URL retry-like behavior is the inherited
 `http_then_browser` cascade (**CAP-052**) only.
- **cache** — `occam_transcode`'s `cache_ttl_s` on-disk cache (**CAP-085**) is **not reachable**
 from `occam_digest` — no parameter, and the per-URL `OccamTranscodeOptions` never sets it.
- **diff** — digest has its own, coarser `if_none_match` (**CAP-458**); `diff_against`/
 `delta_only` (**CAP-082**/**CAP-089**) are **not used/not reachable**.
- **blocks/tables** — internal-only per **CAP-078**/**CAP-455**; never exposed in the digest
 response (no `json_blocks`/`json_tables` parameter exists on `occam_digest`).
- **chunks** — `semantic_chunking` not reachable; not used.
- **budget** — `per_url_max_tokens` (**CAP-454**), reuses `ClientCapabilityStore` ambient default
 (**CAP-060**), but no `OmittedManifest`-equivalent structured record is surfaced per item.
- **receipts** — per-item only, reduced shape (**CAP-457**); no digest-level receipt, no capsule.
- **merkle/capsules** — Merkle leaves not populated per item (empty block set); `emit_capsule` not
 reachable at all.
- **playbooks** — **not used** — `playbook_policy` is not a digest parameter and the internal
 `OccamTranscodeOptions.PlaybookPolicy` is left at `"off"` for every item (**CAP-455** finding 1).
- **datasets** — not used.
- **claims** — not used.
- **trust tags** — `tag_trust`/`rank_blocks` not reachable (require `json_blocks`, which digest
 never sets).
- **screenshots** — `capture_screenshot` not reachable; no parameter.
- **translate** — `translate_to` not reachable; no parameter.
- **llms.txt** — `prefer_llms_txt` not reachable; no parameter.
- **feeds** — `json_feed` not reachable; no parameter (though the underlying HTTP worker's feed
 auto-detection, **CAP-080**'s worker-side branch, still runs transparently per URL since it is
 content-type-driven, not flag-driven — a feed URL in a digest batch is still parsed as a feed).
- **profile** — `occam_digest` is included in every non-empty `OccamToolProfile` tier seen
 (`reader`/`researcher`/`auditor` all list it — `Transport/OccamToolProfile.cs`), unlike some
 other tools that are profile-restricted.
- **env** — `OCCAM_DIGEST_PARALLEL`, `OCCAM_DIGEST_MAX_PARALLEL` (digest-specific, **CAP-382**),
 `OCCAM_BROWSER_MAX_PARALLEL` (shared with browser backend concurrency, **CAP-364**),
 `OCCAM_RECEIPTS` (**CAP-280**/**CAP-372**), plus every env var that
 `TranscodePipeline`/`OccamRouter` read per URL (proxy, robots, domain tiers, SSRF override —
 all Wave-1 `CAP-1xx`/`CAP-3xx`).

---

## HIDDEN / NON-OBVIOUS CAPABILITIES

A user reading only the tool's short MCP description ("Research several pages at once…") would
**never** discover:

1. **CAP-451** — per-URL `focus_query` override exists, but only through the "deprecated
 compatibility" string-encoded-JSON-array input form, not the schema's own "Preferred" array
 shape.
2. **CAP-452** — one malformed/private URL anywhere in the batch fails the **entire** digest
 before any URL is fetched — no partial-success path for preflight-class failures, unlike
 runtime extraction failures which are isolated per item.
3. **CAP-455** — playbook/genome-aware resolution (`playbook_policy=auto`) is **structurally
 unreachable** from `occam_digest` — every URL is always extracted as if `playbook_policy=off`,
 even for sites with a saved, high-quality playbook.
4. **CAP-455** — a parallel digest batch bypasses the shared HTTP daemon entirely (one-shot Node
 spawn per concurrent slot) — meaningfully different resource/latency profile from N sequential
 `occam_transcode` calls, which would reuse the daemon.
5. **CAP-457** — per-item receipts are a reduced Receipt v1 (no block-level Merkle leaves, no
 time anchor) compared to what the same URL would get from a direct `occam_transcode` call with
 `json_blocks=true`/a time anchor enabled.
6. **CAP-458** — `if_none_match` on digest only ever shrinks the `combined` field; `items[]`
 (the bulk of the response bytes) is always returned in full regardless of match.
7. **CAP-459** — `source_url` discovery silently runs one of two entirely different algorithms
 (sitemap-first vs. homepage-map+sitemap-pool-then-rank) depending solely on whether
 `focus_query` happens to be set — a caller toggling `focus_query` on/off changes not just
 relevance filtering but the whole discovery strategy and candidate pool size.
8. **CAP-460** — digest emits its own heuristic "hub/TOC excerpt" detector and a
 read-order downgrade ladder (`combined` → `items_by_focusMatched` → `items_only`) that does not
 exist anywhere in `occam_transcode`.
9. `json_blocks`/`json_tables`/`semantic_chunking`/`capture_screenshot`/`translate_to`/
 `diff_against`/`delta_only`/`prefer_llms_txt`/`cache_ttl_s`/`emit_capsule`/`rank_blocks`/
 `tag_trust`/`content_selectors` — the **entire** `occam_transcode` sidecar/codec surface is
 unreachable per-URL from `occam_digest`; a caller who wants any of these must fall back to N
 individual `occam_transcode` calls, defeating the tool's own stated purpose ("Prefer ONE digest
 over N separate occam_transcode calls").

## Uncertainties

- Whether `MapService.MapAsync`'s own internal fetches (used only in the focused **CAP-459** path)
 honor `session_profile` — traced the call site (`sessionProfile: null` is hardcoded in
 `DiscoverFocusedLinksAsync`'s call to `mapService.MapAsync`) — **confirmed absent**: discovery via
 `source_url` never uses the caller's `session_profile` even though the resulting per-URL
 transcodes do. Worth flagging as a real gap: a login-walled site's sitemap/homepage cannot be
 discovered with credentials even though the discovered pages could then be fetched with them.
- Full internal mechanics of `SitemapDiscovery`/`MapLinkRanker`/`MapLinkFilter`/`HtmlLinkExtractor`
 are intentionally not re-derived here (owned by the pending `occam_map` Wave 2 report); only the
 call shape and control flow as seen from `DigestService` is documented.
- Exact interaction between `OCCAM_DIGEST_PARALLEL=0` and an in-flight `source_url` discovery
 (discovery itself is always sequential/awaited before the transcode fan-out; not re-verified
 against a live env-var toggle).

## COMPLETENESS: COMPLETE
