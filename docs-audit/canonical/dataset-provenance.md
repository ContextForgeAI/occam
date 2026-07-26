# Dataset provenance

**Slug:** `dataset-provenance` · **Product system:** PS-6 Trust and provenance · **CAPs:** 10 · **Public relevance:** HIGH.

## What it is

`occam_dataset_export` sequentially extracts up to 20 URLs, returns full markdown rows and per-row receipts where available, and builds one optional detached-signed manifest over the ordered row identities (CAP-770–779; CAP-283; ART-022).

The manifest binds attempted row identity/order/outcome, not origin truth and not necessarily successful content (CAP-774; TRUST-MODEL §7/§10).

## Why it exists

- Export a machine-readable set of extraction attempts with explicit success/failure rows (CAP-770/774).
- Bind row insertion/deletion/reordering and identity-field edits under one manifest root/signature (CAP-283/774).
- Retain per-row Receipt v1 for independent content checks where producer issued one (CAP-775/776).

## User-visible entrypoints

| Surface | Role | Evidence |
|---|---|---|
| MCP `occam_dataset_export` | Produces rows and manifest; full/auditor only | CAP-770/779 |
| CLI `occam verify --mode manifest` | Verifies whole-set manifest | CAP-283/276 |
| MCP `occam_verify` | Can verify individual row receipts only; no manifest mode | CAP-283; EF-018 |

## Core behavior

1. Validate `urls` JSON array string, policy, nonempty cleaned rows, and maximum 20 (CAP-770).
2. Process URLs sequentially in input order through `TranscodePipeline` (CAP-778).
3. Force blocks and playbook auto, but expose almost no transcode options (CAP-771/772).
4. Success row carries full markdown, prefixed content hash, block root, and optional receipt (CAP-771/776/777).
5. Failure row carries code and selected negative receipt; all rows become manifest leaves (CAP-774/775).
6. Build ordered Merkle root and, if receipts enabled, detached-sign manifest canonical bytes (CAP-283).
7. Return top-level `ok:true` after argument validation even if every row failed (CAP-773).

## Advanced behavior

| Mechanism | Detail | Evidence |
|---|---|---|
| Row leaf | URL, final URL, ok, contentHash, block root, failure code in fixed order | `DatasetManifest.cs`; CAP-283/774 |
| Manifest | version, createdAt, rowCount, root, keyId, alg, sig | CAP-283 |
| Row order | Significant; reorder changes root | CAP-283 |
| Success hash | `sha256:`-prefixed, unlike transcode top-level bare hex | CAP-777 |
| Blocks | Forced for root, then block contents discarded | CAP-772 |
| Playbooks | Auto forced per row; no opt-out | CAP-772 |

## Automatic / silent behavior

- Empty array entries are trimmed/dropped before limit validation (CAP-770).
- Invalid/private URL is a failed row, not whole-call failure (CAP-770/771).
- `MaxTokens=null`: ambient client budget is bypassed and every row is full/unbounded markdown (CAP-771; EF-016).
- Blocks are collected but not returned (CAP-772).
- Top-level `ok` is literal true after input validation, regardless of row outcomes (CAP-773; EF-018).
- Rows run strictly serially; no streaming/partial response (CAP-778).
- Success receipts never include capsule/time anchor; many failures have no receipt (CAP-775/776).

## Parameters

| Name | Default | Effect | Evidence |
|---|---|---|---|
| `urls` | required JSON array string | 1–20 effective URL strings | CAP-770 |
| `backend_policy` | `http_then_browser` | One route applied to every row | CAP-770/772 |
| `session_profile` | null | One session applied to every row | CAP-771/772 |

There is no max_tokens, fit/focus, selectors, playbook toggle, conditional/diff, cache, capsule, translation, screenshot, structured-table/feed/chunk, trust-tag, or per-row policy/session parameter (CAP-771).

## Configuration

`OCCAM_RECEIPTS` gates row signatures and manifest signature together; root/rows remain when off (CAP-280/283). Pipeline/browser/proxy/robots/managed/session/playbook variables apply per row. No dataset-specific env exists (CAP-770–779).

## Backends

Each row uses corrected shared router semantics. HTTP/browser and configured managed success are reachable. The tool bypasses the transcode wrapper, so cache, ambient budget, translation, capsule, and related wrapper features never run (CAP-771; EF-056).

## Sessions / state

One session profile applies uniformly. Export is not automatically persisted; caller owns returned ART-022 (ST-16). Local key ART-034 is persistent.

No dataset store, cache, resume, job state, or cleanup path exists in this family.

## Network behavior

Up to 20 sequential full acquisition flows. Worst-case latency is linear with cascades (CAP-778). No export-level retry, concurrency, streaming, or cache.

Managed providers can receive URL/content when configured; manifest signature still only attests host assertion (EF-003; TRUST-MODEL).

## Artifacts produced

ART-022 contains ordered rows and manifest (`ARTIFACT-ONTOLOGY.md:119`). Successful rows may contain ART-001 markdown and ART-007 receipt; failed rows may contain ART-008 only for selected wall/status cases.

Manifest root is over row identity fields, not markdown bytes directly; successful contentHash/receipt provide the content link (CAP-283).

## Trust / provenance properties

Manifest verification proves the supplied ordered row identities reconstruct the signed root and that the supplied key holder signed manifest fields. It does not prove rows succeeded, content came from origin, timestamps are honest, or signer identity (CAP-283/774; TRUST-MODEL C4/C12).

Failed rows are valid manifest members. A signed all-failure export is still a valid manifest of failed attempts (CAP-773/774).

Per-row receipt limits follow receipt model. Many transient failures have no row receipt; manifest signs only the asserted failure-code leaf (CAP-775).

## Failure / fallback behavior

- Tool-level invalid JSON/policy/empty/>20 → `invalid_arguments`, no export (CAP-770).
- Per-row pipeline failures populate `rows[i].ok=false` and code; processing continues (CAP-771).
- Negative receipt only for challenge/login/401/403/404/410 (`paywall` dead); otherwise null (CAP-775).
- Top-level remains `ok:true` even all rows fail (CAP-773).
- Unexpected exception handling above service is uncertain; no local catch surrounds loop (tool report uncertainty).

## Platform differences

No dataset-semantic differences. Manifest canonicalization/signature is cross-platform. Key protection differs Windows vs POSIX (CAP-255). Worker acquisition inherits platform process/browser differences.

## Composition with other capabilities

- Reuses acquisition/materialization per row with reduced options (CAP-771).
- Forces playbook resolution and block extraction (CAP-772).
- Uses receipt signer and Merkle tree for row and set commitments (CAP-283).
- Whole manifest requires CLI verification; individual receipts use MCP verification (EF-018).
- Does not use claim checking, attest, cache, or canonical knowledge IR.

## Known limitations

- Top-level success is not aggregate success (CAP-773).
- No token budget; potentially very large response (CAP-771/EF-016).
- Sequential only (CAP-778).
- Manifest verification unavailable over MCP (CAP-283/EF-018).
- No capsules/time anchors (CAP-776).
- Transient failure rows often lack receipts (CAP-775).
- Blocks are discarded after hash (CAP-772).
- One shared policy/session, no per-row overrides.

## Engineering findings

- EF-016: no token budget.
- EF-018: `ok` always true and manifest verification CLI-only.
- EF-025: friendly wrapper cannot reach verify CLI.
- CAP-777: hash-format inconsistency.
- EF-003: managed provider trust boundary applies before host signs.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamDatasetExportTool.cs`
- `src/FFOccamMcp.Core/Dataset/DatasetExportService.cs`
- `src/FFOccamMcp.Core/Dataset/DatasetManifest.cs`
- `src/FFOccamMcp.Core/Cli/OccamCliVerbs.cs:356-375`
- CAP-770–779; CAP-283; ART-022; EF-016/018.

## Public-doc relevance

Critical. Explain top-level `ok`, all-failure valid manifests, row identity vs content proof, no budget, serial execution, reduced receipts, and CLI-only set verification. Never call the export a verified successful corpus without inspecting rows.

## Handbook relevance

Provide a production/export checklist: scan every row status, store rows+manifest+public PEM together, run CLI manifest verification, and separately verify important row content. Include safe audit wording for what the manifest binds.
