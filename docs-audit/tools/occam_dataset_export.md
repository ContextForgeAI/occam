# `occam_dataset_export` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/**`). Documentation
(`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md`) was **not** used as evidence.

**CAP ID range owned by this audit:** `CAP-770`–`CAP-799` (used: CAP-770…779; remainder reserved).
**Wave-1 CAP IDs reused (not re-minted):** CAP-050, CAP-052, CAP-053, CAP-054, CAP-068, CAP-069,
CAP-070, CAP-071, CAP-072, CAP-073, CAP-078, CAP-090, CAP-091, CAP-093 (absence noted), CAP-100,
CAP-101, CAP-102, CAP-103, CAP-104, CAP-105, CAP-108, CAP-252, CAP-254, CAP-257, CAP-280, CAP-283,
CAP-007/008/009 (profile gating).

**Files inspected:**
`Tools/OccamDatasetExportTool.cs`, `Dataset/DatasetModels.cs`, `Dataset/DatasetExportService.cs`,
`Dataset/DatasetManifest.cs`, `Tools/OccamTranscodeTool.cs` (for parameter-surface diff),
`Routing/OccamTranscodeOptions.cs`, `Routing/TranscodePipeline.cs`, `Knowledge/MaterializationPlanner.cs`,
`Receipts/MerkleTree.cs`, `Receipts/ReceiptCanonicalizer.cs`, `Receipts/ReceiptsPolicy.cs`,
`Compile/ContentHashToken.cs`, `Cli/OccamCliVerbs.cs` (manifest verify verb),
`Tools/OccamVerifyTool.cs` (mode list), `Transport/OccamMcpServerRegistration.cs`,
`Transport/OccamToolProfile.cs`, `docs-audit/subsystems/trust-receipts.md` (CAP-283 origin, S19),
`docs-audit/CAPABILITY-INVENTORY.md`.

---

## 0. Entry point and schema

`OccamDatasetExportTool.Export` (`Tools/OccamDatasetExportTool.cs`) — one `[McpServerTool]` method,
three parameters:

```
urls (required, JSON array string), backend_policy = "http_then_browser", session_profile = null
```

No `max_tokens`, `fit_markdown`, `focus_query`, `content_selectors`, `playbook_policy`,
`if_none_match`, `semantic_chunking`, `capture_screenshot`, `json_blocks/tables/feed`,
`translate_to`, `diff_against`, `prefer_llms_txt`, `cache_ttl_s`, `emit_capsule`, `rank_blocks`,
`tag_trust`, or `delta_only` parameter exists on this tool at all — see **CAP-771**.

---

## CAP-770 — Tool entry point: schema, validation, dispatch

**Trace:** `OccamDatasetExportTool.Export` validates in this fixed order:
1. `urls` non-empty/non-whitespace string → else `invalid_arguments`.
2. `backend_policy` parses via the shared `OccamBackendPolicyParser.TryParse` (CAP-051 reuse — same
   parser occam_transcode uses: `http` / `browser` / `http_then_browser` / `http-then-browser`).
3. `urls` deserializes as a JSON string array (`OccamDatasetJsonContext.Default.StringArray`) → a
   `JsonException` becomes `invalid_arguments` with the exception message embedded.
4. Elements are trimmed and empty/whitespace entries are dropped (`cleaned` array) — an input like
   `["", "  ", "https://a.example"]` silently collapses to 1 effective URL, not an error.
5. Zero remaining URLs after cleaning → `invalid_arguments`.
6. **`MaxUrls = 20`** hard cap (`cleaned.Length > MaxUrls`) → `invalid_arguments` naming the actual
   count and the limit. There is **no** lower-bound-other-than-zero check beyond step 5 — 1 URL is a
   valid (if wasteful) call.

Individual URL shape (`http`/`https`, not private/loopback) is **not** validated at this layer —
that happens per-row, later, inside the shared `TranscodePipeline`/`FetchPreflight` path (CAP-050,
CAP-100 reuse), so a malformed or private URL in the array does not fail the whole call; it produces
one failed row (see **CAP-771**).

On success, `Export` delegates to `IDatasetExportService.ExportAsync(cleaned, policy, session_profile,
cancellationToken)` and serializes the typed response directly (no post-processing in the tool layer
itself — everything interesting happens in `DatasetExportService`).

---

## CAP-771 — HIDDEN: per-row transcode bypasses almost the entire `occam_transcode` parameter surface

**Evidence:** `Dataset/DatasetExportService.cs` `ExportOneAsync` builds its own
`OccamTranscodeOptions` and calls `TranscodePipeline.TranscodeAsync` **directly** — it does **not**
go through `OccamTranscodeTool`. This matters because `OccamTranscodeTool.Transcode` (the MCP tool)
is where most of transcode's opt-in machinery actually lives (confirmed by reading
`OccamTranscodeTool.cs` line-by-line): `clientCapabilities.ResolveMaxTokens`, `rank_blocks`/
`tag_trust` block annotation, `translate_to` (LibreTranslate), `emit_capsule`, `cache_ttl_s`,
`prefer_llms_txt`, `diff_against`/`delta_only`, `if_none_match` (AF-6), `MaterializationKey`
computation, and the `Access`/`Focus`/`Completeness` semantic mapping are **all implemented in the
tool method itself**, not in `TranscodePipeline`. None of that code runs for a dataset-export row.

Concretely, for every URL in a dataset export:

- **`MaxTokens` is `null` and is never resolved against the ambient client budget.**
  `OccamTranscodeOptions.MaxTokens` defaults to `null`; `DatasetExportService` never calls
  `ClientCapabilityStore.ResolveMaxTokens` (confirmed: `IDatasetExportService`/`DatasetExportService`
  take no `ClientCapabilityStore` dependency at all — grep across the file found none). Downstream,
  `MaterializationPlanner.Plan` only budgets `if (request.MaxTokens is int budget)` — with `null` it
  skips budget-aware trimming entirely. **Net effect: every dataset-export row returns full,
  untruncated compiled markdown, with no ~20%-context-window sizing that a plain `occam_transcode`
  call gets by default.** For a 20-URL export of long pages this can produce a very large response
  with no way to cap it from this tool's own parameters.
- No `fit_markdown`/`focus_query`/`content_selectors` — no BM25 pruning or heading-anchor scoping is
  possible per row.
- No `rank_blocks`/`tag_trust` — even though `JsonBlocks=true` is forced on internally (see
  **CAP-772**), blocks are never annotated with salience or trust tags (those annotators live in
  `OccamTranscodeTool.Transcode`, never invoked here).
- No `translate_to`, `emit_capsule`, `cache_ttl_s`, `prefer_llms_txt`, `diff_against`, `delta_only`,
  `if_none_match`, `semantic_chunking`, `capture_screenshot`, `json_feed` — none of these are
  reachable; a dataset row is always plain markdown + blocks + a signed receipt, nothing else.
- `Access`/`Focus`/`Completeness` semantic verdicts and `agentHints`/warnings are **never computed**
  for dataset rows — `OccamDatasetRowInfo` has no fields for them.
- **`MaterializationKey` is never computed for rows** — an intentional-looking gap since it is the
  load-bearing identity `occam_transcode`'s own caching/conditional/receipt features key off of
  (CAP-093), but dataset export's row identity is instead the manifest leaf hash (CAP-283), which is
  a coarser, receipt-content-only identity.

None of this is stated in the tool's MCP description (`"Params: urls (JSON array), backend_policy,
session_profile."`) — a caller cannot discover from the schema that dataset rows are a strictly
reduced transcode compared to calling `occam_transcode` directly on the same URL.

---

## CAP-772 — Two hardcoded per-row options: forced `json_blocks` + forced `playbook_policy=auto`

**Evidence:** `DatasetExportService.ExportOneAsync`:
```
JsonBlocks = true,             // so each row carries a block-Merkle root
SessionProfile = sessionProfile,
PlaybookPolicy = PlaybookPolicy.Auto,
```
`JsonBlocks=true` is hardcoded (not caller-controlled) purely so `MerkleTree.Root(blocks)` has data
to hash into `BlockMerkleRoot` — the caller never sees a "did you want blocks" choice, and the
resulting `blocks[]` array is **not exposed** in `OccamDatasetRowInfo` (only the row's Merkle root
is) — see **CAP-283**'s design note that manifest rows deliberately omit content. So the DOM
block-walk cost (CAP-078) is paid per row but the actual block content is discarded after hashing.

`PlaybookPolicy.Auto` is likewise hardcoded — every dataset-export URL gets genome-aware playbook
resolution (CAP-070 reuse: local → `WT_PLAYBOOKS_PATH` → community → seeds, soft-overlay apply,
CAP-071/072) with **no way to opt out** (`playbook_policy=off` is not an available parameter on this
tool, unlike `occam_transcode`). Same env-gating caveat as CAP-070/073 applies unchanged: this does
**not** trigger a live `.well-known/agent-genome.v1.json` fetch unless `OCCAM_SITE_GENOME_FETCH=1` is
set (the one-argument `PlaybookResolveOptions(url)` constructor leaves `FetchSiteGenome=false`,
confirmed identical construction site pattern as `TranscodePipeline.TranscodeAsync`, which this
service calls through unmodified).

`backend_policy` is the **one** dispatch-affecting knob exposed, and it is a **single value applied
uniformly to every URL in the batch** — there is no per-URL backend override in the array (a mixed
array where URL A needs `browser` and URL B is fine on `http` cannot be expressed in one call; the
caller must either accept `http_then_browser`'s cascade for all URLs or split into multiple exports).

---

## CAP-773 — HIDDEN: top-level `ok` is always `true` once argument validation passes, independent of row outcomes

**Evidence:** `DatasetExportService.ExportAsync` — `return new OccamDatasetExportResponse(true,
manifest, rowInfos, createdAt);` — the boolean literal `true` is **hardcoded**, not derived from
`rowInfos.All(r => r.Ok)` or any aggregate. A dataset export where **every single URL fails**
(all `rows[].ok == false`) still returns an envelope with `ok:true` at the top level. The manifest is
still built and signed over the (all-failure) row leaves — a caller must inspect `rows[].ok`
per-element; there is no shortcut aggregate field (`allOk`, `successCount`, etc.) anywhere in
`OccamDatasetExportResponse`/`OccamDatasetManifestInfo`. This is a genuine trust-model asymmetry
versus `occam_transcode`, where `ok:false` is the tool's central "content is unknown" signal
(AGENTS.md's own stated trust rule) — that signal does not exist at the dataset-export envelope
level at all.

---

## CAP-774 — Manifest signs identity + outcome, not success; failed rows are still bound into the root

**Evidence:** `Dataset/DatasetManifest.cs` `DatasetRow(Url, FinalUrl, Ok, ContentHash, BlockMerkleRoot,
FailureCode)` — `Ok=false` rows are first-class leaf members (their leaf hashes
`Url|FinalUrl|"0"|""|""|FailureCode`), included in `ManifestRoot` exactly like successful rows. The
manifest signature is therefore a claim of the form "this exact ordered set of N attempted
extractions (successes AND failures, with these exact outcomes) was produced together by this key at
this time" — not "these N pages were successfully captured." Consumers relying on the manifest
signature alone (without reading each row's `ok`/`failureCode`) would incorrectly treat a
mostly-failed export as a verified successful corpus.

---

## CAP-775 — Per-row receipts only exist for a narrow "provable failure" allow-list; most failures get `receipt: null`

**Evidence:** `DatasetExportService.ExportOneAsync` failure branch calls
`OccamTranscodeResponseBuilder.BuildNegativeReceipt(url, finalUrl, backend, code, outcome.StatusCode,
effectiveSigner)`, which (per Wave-1 CAP-091, re-verified here directly in
`OccamTranscodeModels.cs:419-432`) returns **`null`** unless `code` is one of
`captcha_or_challenge` / `requires_login` / `paywall`, or `statusCode` is `401`/`403`/`404`/`410`.
Concretely: a row that failed with `timeout`, `network_error`, `dns_error`, `tls_error`,
`extraction_failed`, `thin_extract`, or `workers_unavailable` gets `Receipt: null` in
`OccamDatasetRowInfo` — the row is still bound into the manifest root (its `FailureCode` is part of
the leaf preimage, CAP-774), but there is **no signed evidence at all** for *why* that specific row
failed; only the manifest signature over the bare failure-code string vouches for it, which is weaker
than a per-row signed receipt.

---

## CAP-776 — Success-row receipts are capsule-less and time-anchor-less by construction (no opt-in path)

**Evidence:** `DatasetExportService.ExportOneAsync` success branch:
`OccamTranscodeResponseBuilder.BuildReceipt(outcome, url, effectiveSigner)?.Signed` — this calls the
**3-argument** overload of `BuildReceipt` (`OccamTranscodeModels.cs:347-349`), which defaults
`timeAnchor: null` and `emitCapsule: false`. Compare `OccamTranscodeTool.Transcode`, which always
passes its `TimeAnchorService` (when receipts are enabled) and the caller's `emit_capsule` flag.
**Result:** even when `OCCAM_RECEIPTS` is on and an RFC3161 time-anchor service is configured
operator-side, dataset-export row receipts never carry a time anchor and never carry a
`occam://capsule/…` proof bundle (CAP-086/CAP-092 features are simply unreachable from this tool) —
and there is no parameter on `occam_dataset_export` to request either.

---

## CAP-777 — `contentHash` field format diverges from `occam_transcode`'s own `contentHash` field

**Evidence:** `DatasetExportService.ExportOneAsync`: `var contentHash =
ReceiptCanonicalizer.ContentHash(outcome.Markdown);` — `ReceiptCanonicalizer.ContentHash`
(`Receipts/ReceiptCanonicalizer.cs:17-18`) returns `"sha256:" + hex(SHA256(content))` (prefixed).
`occam_transcode`'s own top-level `contentHash` response field is built via
`Compile.ContentHashToken.BareHex` (`Compile/ContentHashToken.cs:18-19`) — **bare hex, no prefix**.
Both hash the identical bytes (raw UTF-8 markdown, SHA-256), so they are semantically equivalent and
`ContentHashToken.Matches` (used by `if_none_match`) explicitly accepts either form — but a consumer
comparing `occam_dataset_export`'s `rows[].contentHash` against `occam_transcode`'s `contentHash` for
the same URL byte-for-byte will see two differently-formatted strings for the same hash unless they
know to strip the `sha256:` prefix first.

---

## CAP-778 — Sequential (non-parallel) row processing — latency is linear in URL count

**Evidence:** `DatasetExportService.ExportAsync` — a plain `for` loop with `await
ExportOneAsync(...)` per iteration; no `Task.WhenAll`/`Parallel.ForEachAsync`, no concurrency
limiter needed because there is no concurrency at all. Contrast with `occam_digest`'s per-URL
parallelism (Wave-1 territory, not re-audited here, but structurally different). **Worst case:** 20
URLs each escalating through the full `http_then_browser` cascade (HTTP timeout ~35s + browser
timeout ~120s per URL, CAP-052/CAP-053) could take on the order of tens of minutes for one
`occam_dataset_export` call — there is no partial-results/streaming mechanism; the whole call blocks
until every row (success or failure) has been attempted, in array order.

---

## CAP-779 — Profile-gated tool: only in `full` and `auditor`, not `reader`/`researcher`

**Evidence:** `Transport/OccamMcpServerRegistration.cs` line 117-118 (`OccamToolProfile.IsExposed`
gate) and `docs-audit/subsystems/runtime-mcp.md` CAP-009's profile table
(`auditor = researcher + attest, dataset_export, playbook_lint`, 12 tools). A caller running under
`OCCAM_PROFILE=reader` or `=researcher` (CAP-008/009 reuse) will not see `occam_dataset_export` in
`tools/list` at all — it is scoped to the two role profiles most associated with provenance/audit
work (`full`, `auditor`).

---

## Reused subsystem capabilities (not re-described in full — see cited reports)

- **CAP-050 / CAP-100** — per-URL SSRF/private-host validation and `invalid_arguments` /
  `private_url_blocked` classification happen inside the shared `TranscodePipeline` → `FetchPreflight`
  path exactly as for `occam_transcode`; a private/malformed URL in the array fails only that row, not
  the whole export (confirmed: `TranscodePipeline.TranscodeAsync` is the same method
  `occam_transcode`'s router calls; `DatasetExportService` does not pre-filter URLs beyond
  whitespace/empty trimming).
- **CAP-052 / CAP-053 / CAP-054** — the `http_then_browser` cascade (or `http`/`browser` alone),
  browser auto-provisioning gate, and managed third-party escalation subsystem are all reachable per
  row exactly as in `occam_transcode` (single shared `OccamRouter`).
- **CAP-068 / CAP-069** — `session_profile` resolution and path-traversal hardening apply identically;
  the same session is applied to **every** row in the batch (no per-row session override).
- **CAP-070 / CAP-071 / CAP-072 / CAP-073** — playbook auto-resolution, soft-overlay, preferred-backend
  override, and env-gated well-known genome fetch — see **CAP-772** for the "always-on, not opt-out"
  framing specific to this tool.
- **CAP-078** — the always-on internal block/table DOM walk runs per row regardless (json_blocks is
  explicitly forced true here anyway, CAP-772).
- **CAP-090 / CAP-091** — Receipt v1 positive/negative signing primitives — see **CAP-775/776** for
  this tool's specific (narrower) call pattern into the shared builders.
- **CAP-093** — `MaterializationKey` exists as a concept but is **never computed** for dataset rows
  (noted as an absence in CAP-771).
- **CAP-101 / CAP-102 / CAP-103 / CAP-104** — response-size cap, egress proxy, robots/throttle, and
  domain-tier registry all apply per row identically to `occam_transcode` (same worker/backend layer).
- **CAP-105 / CAP-108** — failure-code normalization and worker-process-lifecycle failure taxonomy are
  identical; any `occam_transcode`-reachable failure code can appear in `rows[].failureCode`.
- **CAP-252 / CAP-254 / CAP-257** — `MerkleTree`, the local ECDSA P-256 `ReceiptSigner`, and the
  generic `SignDetached`/`VerifyDetached` primitive are reused as-is; `DatasetManifestBuilder` is the
  **4th** independent hand-written canonicalizer sharing this signer (per CAP-257's cross-subsystem
  note), with its **own** `CanonicalBytes` layout (`v`, `createdAt`, `rowCount`, `manifestRoot`,
  `keyId`, `alg` — `sig` excluded) distinct from `ReceiptCanonicalizer`'s receipt-envelope layout.
- **CAP-280** — `ReceiptsPolicy.Enabled()` (`OCCAM_RECEIPTS` global kill-switch) gates the **entire**
  dataset-export trust layer at once: when off, `effectiveSigner` is `null`, every row's `Receipt` is
  `null`, and the manifest's `KeyId`/`Sig` are both `null` (root/`createdAt`/`rowCount` still present)
  — one flag disables both per-row and per-set proof simultaneously, consistent with the kill-switch's
  documented scope in CAP-280's own report.
- **CAP-283** — `DatasetManifestBuilder` itself (row-leaf canonicalization, Merkle root, detached
  signature, `Verify`) is the Wave-1-documented core of this tool; this Wave-2 audit adds the
  tool-level integration findings (CAP-770…779) around it. CAP-283's own finding — **the manifest has
  no MCP-side verify mode; `DatasetManifestBuilder.Verify` is reachable only via `occam verify --mode
  manifest` (CLI)** — is reconfirmed here directly against `OccamVerifyTool.cs`'s mode list
  (`offline | live | prove | citation | history` — no `manifest`) and `OccamCliVerbs.cs:240,356-375`
  (`VerifyManifest`, CLI-only). A dataset-export **row's own receipt** IS verifiable via the
  `occam_verify` MCP tool (it's a normal `ReceiptEnvelope`); the **manifest signature over the set** is
  not, from inside an MCP session — a caller who wants to prove the whole set (not just one row) must
  shell out to the CLI with a pinned public key.
- **CAP-007 / CAP-008 / CAP-009** — see **CAP-779** for this tool's specific profile placement.

---

## Cross-cutting category checklist (per shared instructions)

| Category | Status |
|---|---|
| proxy | Used — inherited from shared `TranscodePipeline`/worker layer (CAP-102); no dataset-export-specific proxy behavior. |
| session | Used — single `session_profile` string applied to every row (CAP-068/069); not per-row. |
| cookies | Used transitively via `session_profile` → Playwright storageState / header cookies (CAP-068); no direct param here. |
| headers | Used transitively via session profile merge (CAP-068); no direct param. |
| http | Used — `backend_policy=http` is one of three valid values, applied to all rows. |
| browser | Used — `backend_policy=browser`/`http_then_browser` reach the browser backend per row (CAP-052/053). |
| managed | Used transitively — the managed third-party escalation tier (CAP-054) is reachable per row if operator-configured; not disableable/selectable from this tool. |
| retry | Not used — no retry parameter; cascade escalation (HTTP→browser) is the only retry-like behavior, inherited (CAP-052). |
| cache | **Not used** — `cache_ttl_s` does not exist on this tool; every export is a live, uncached run of every row (no `TranscodeResponseCache` interaction anywhere in `DatasetExportService`). |
| diff | Not used — no `diff_against`/`if_none_match`/`delta_only`; not reachable (CAP-771). |
| blocks | Used internally only — `JsonBlocks=true` forced (CAP-772) but blocks are hashed into `blockMerkleRoot` and then **discarded**, never returned to the caller. |
| tables | Not used — `JsonTables` is never set true in `OccamTranscodeOptions` here (default `false`); table data plays no role in dataset rows. |
| chunks | Not used — `SemanticChunking` defaults false and is not settable. |
| budget | **Not used / bypassed** — `MaxTokens` stays `null` for every row; no token budgeting applies at all (CAP-771, the tool's single biggest hidden finding). |
| receipts | Used — per-row Receipt v1 (narrowed, CAP-775/776) + a separate manifest-level detached signature (CAP-283/774). |
| merkle | Used — two distinct Merkle usages per successful row: (a) `MerkleTree.Root(blocks)` → `blockMerkleRoot` (per-page content tree, same primitive as `occam_transcode`'s internal block hashing), and (b) `MerkleTree.RootFromLeafHashes` over the **row leaves** → `manifestRoot` (per-set tree, CAP-283). |
| capsules | **Not used / unreachable** — `emit_capsule` does not exist on this tool and the internal `BuildReceipt` call never requests one (CAP-776). |
| playbooks | Used — forced `playbook_policy=auto` for every row, no opt-out (CAP-772). |
| datasets | This **is** the datasets capability — the tool's entire purpose (CAP-770/283). |
| claims | Not used — no interaction with `occam_claim_check`/Canonical claim extraction. |
| trust tags | **Not used / unreachable** — `tag_trust`/`rank_blocks` block annotation code lives only in `OccamTranscodeTool.Transcode`, never invoked here (CAP-771). |
| screenshots | Not used — `capture_screenshot` does not exist on this tool. |
| translate | **Not used / unreachable** — `translate_to`/`TranslationService` never invoked (CAP-771). |
| llms.txt | Not used — `prefer_llms_txt` does not exist on this tool. |
| feeds | Not used — `JsonFeed` stays false; feed parsing never engaged for dataset rows. |
| profile | Used — tool itself is profile-gated (`full`, `auditor` only, CAP-779); no per-call profile parameter. |
| env | Used transitively — every env var governing the shared pipeline (`OCCAM_RECEIPTS`, `OCCAM_ALLOW_PRIVATE_URLS`, `OCCAM_SESSIONS_ROOT`, `OCCAM_HTTP_PROXY`/`HTTPS_PROXY`, `OCCAM_RESPECT_ROBOTS`, `OCCAM_MANAGED_*`, `OCCAM_SITE_GENOME_FETCH`, browser-provisioning vars) applies identically per row; no dataset-export-specific env var was found. |

---

## Failure codes reachable on this tool

- **Argument-validation layer** (tool method itself, before any URL is touched):
  `invalid_arguments` — empty/whitespace `urls`, bad JSON, all-empty array after cleaning, more than
  20 URLs, or an invalid `backend_policy` string.
- **Per-row layer** (via shared `TranscodePipeline`): any `occam_transcode`-reachable failure code can
  appear in `rows[i].failureCode` — `workers_unavailable`, `timeout`, `extraction_failed`,
  `thin_extract`, `captcha_or_challenge`, `requires_login`, `http_403`/`http_404`/other `http_*`,
  `response_too_large`, `private_url_blocked`, `dns_error`, `tls_error`, `network_error` (full catalog:
  Wave-1 `docs-audit/tools/occam_transcode.md` §"Failure code catalog"). These **never** fail the
  overall MCP call — they only populate that row's `ok:false`/`failureCode` (see **CAP-773**).
- There is **no top-level failure path once argument validation passes** — `DatasetExportService`
  has no `try/catch`-to-failure-response mapping of its own; any unexpected exception during a row's
  transcode would propagate as an unhandled exception rather than a typed `OccamDatasetExportFailureResponse`
  (UNCERTAIN — see below).

---

## Capability graph edges

```
TOOL:occam_dataset_export|USES|CAP-770
TOOL:occam_dataset_export|USES|CAP-771
TOOL:occam_dataset_export|USES|CAP-772
TOOL:occam_dataset_export|USES|CAP-773
TOOL:occam_dataset_export|USES|CAP-774
TOOL:occam_dataset_export|USES|CAP-775
TOOL:occam_dataset_export|USES|CAP-776
TOOL:occam_dataset_export|USES|CAP-777
TOOL:occam_dataset_export|USES|CAP-778
TOOL:occam_dataset_export|USES|CAP-779
TOOL:occam_dataset_export|USES|CAP-283
TOOL:occam_dataset_export|USES|CAP-090
TOOL:occam_dataset_export|USES|CAP-091
TOOL:occam_dataset_export|USES|CAP-280
TOOL:occam_dataset_export|USES|CAP-068
TOOL:occam_dataset_export|USES|CAP-069
TOOL:occam_dataset_export|USES|CAP-070
TOOL:occam_dataset_export|USES|CAP-078
TOOL:occam_dataset_export|USES|CAP-100
TOOL:occam_dataset_export|USES|CAP-052
PARAM:urls|ENABLES|CAP-770
PARAM:backend_policy|ENABLES|CAP-052
PARAM:backend_policy|ROUTES_TO|http_extract_backend
PARAM:backend_policy|ROUTES_TO|browser_extract_backend
PARAM:backend_policy|FALLS_BACK_TO|managed_extract_backend
PARAM:session_profile|CONSUMES|session
CAP-770|ROUTES_TO|TranscodePipeline
CAP-771|PRODUCES|unbounded_markdown_per_row
CAP-772|ENABLES|CAP-070
CAP-772|ENABLES|CAP-078
CAP-774|PRODUCES|manifest
CAP-774|CONSUMES|CAP-283
CAP-775|PRODUCES|receipt
CAP-775|FALLS_BACK_TO|null_receipt
CAP-776|PRODUCES|receipt
CAP-776|FALLS_BACK_TO|no_capsule
CAP-776|FALLS_BACK_TO|no_time_anchor
CAP-283|PRODUCES|manifestRoot
CAP-283|PRODUCES|rowLeaf
CAP-283|CONSUMES|CAP-252
CAP-283|CONSUMES|CAP-257
CAP-283|CONSUMES|CAP-254
CAP-777|PRODUCES|contentHash
CAP-779|CONSUMES|CAP-008
CAP-779|CONSUMES|CAP-009
```

---

## HIDDEN / NON-OBVIOUS CAPABILITIES

Capabilities a user would **never** discover from the tool's short MCP description
(`"Params: urls (JSON array), backend_policy, session_profile."`):

1. **CAP-771** — every row is a *reduced* transcode: no token budget (not even the ambient
   client-capability default), no `fit_markdown`/`focus_query`/selectors, no `rank_blocks`/
   `tag_trust`, no translation, no capsule, no cache, no llms.txt preference, no diff/delta. A caller
   who assumes "dataset export = N calls to occam_transcode with a receipt on top" is wrong on nearly
   every axis except the markdown extraction itself and the (forced-on, hidden) block collection.
2. **CAP-771** — **no token budget at all** is the single biggest surprise: unlike `occam_transcode`,
   omitting a limit here does not fall back to the ~20%-context ambient default — it falls back to
   **no limit whatsoever**, for every row, every time.
3. **CAP-773** — the envelope's `ok` field is **always `true`** once arguments parse, even if every
   row failed. There is no aggregate success signal; a caller must scan `rows[].ok`.
4. **CAP-772** — `playbook_policy=auto` and `json_blocks`-style internal block collection are
   silently forced on for every URL, with no opt-out parameter, and the collected blocks are
   discarded after hashing (never returned).
5. **CAP-775/776** — receipts are quietly weaker than `occam_transcode`'s: failure receipts exist for
   only ~6 specific codes/status values (everything else is `receipt:null`), and success receipts
   never carry a capsule or time anchor, with no parameter to request either.
6. **CAP-283 (reconfirmed)** — the manifest's own signature (the "whole set is tamper-evident" claim
   in the tool's description) can only be checked via the `occam` CLI's `verify --mode manifest`,
   never via the `occam_verify` MCP tool — an MCP-only agent session cannot self-verify the manifest
   it just received, only the individual row receipts.
7. **CAP-777** — the `contentHash` field format (`sha256:`-prefixed) differs from `occam_transcode`'s
   own `contentHash` field (bare hex) for the same underlying hash.
8. **CAP-778** — rows are processed strictly sequentially; a 20-URL export has no parallelism and no
   partial/streaming results — the call blocks until the last row (success or failure) completes.

---

## Uncertainties

- Whether an unhandled exception thrown mid-loop inside `DatasetExportService.ExportAsync` (e.g. a
  backend/worker throwing instead of returning a failure outcome) is caught anywhere above
  `OccamDatasetExportTool.Export` and converted into a typed MCP error, or propagates as a raw
  transport-level error to the client — no `try/catch` was found in either `OccamDatasetExportTool.cs`
  or `DatasetExportService.cs` around the per-row loop; the top-level MCP host's generic
  exception-to-JSON-RPC-error handling (outside this tool's own files) was not traced as part of this
  audit.
- Whether duplicate URLs in the `urls` array (the same URL twice) are rejected, deduplicated, or
  processed twice as independent rows — code reads as "processed twice as independent rows" (no
  dedup logic found), but this was not separately live-tested.
- Exact behavior of `MaxUrls` interacting with a JSON array containing exactly 20 non-empty entries
  plus extra empty-string entries (e.g. 25 raw entries, 20 non-empty) — traced as accepted (cleaning
  happens before the `MaxUrls` check), but not independently verified against a live call.

## COMPLETENESS

COMPLETE
