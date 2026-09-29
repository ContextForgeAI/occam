# `occam_dataset_export`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamDatasetExportTool.cs` @ 2026-09-29  
**Profiles:** `auditor`, `full` — not `reader` (default), `researcher`, `minimal`, or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Build a signed, auditable dataset from **1–20** URLs: each URL is live-transcoded into a row; one manifest signature (when receipts enabled) binds the ordered Merkle root of row leaves. Proves **integrity of the export artifact** under your local key — not factual correctness.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Ordered signed corpus with per-row receipts + set-level manifest | One URL markdown → [`occam_transcode`](occam_transcode.md) |
| Row verify via MCP; set verify via CLI `verify --mode manifest` | Bulk async without provenance → [`occam_batch`](occam_batch.md) |
| Same policy/session for every URL is enough | Per-URL token/focus knobs (not exposed here) |

**Implementation:** sequential rows; forced `json_blocks=true`, `playbook_policy=auto`.

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `urls` | yes | — | JSON array of HTTP(S) URLs (1–20) |
| `backend_policy` | no | `http_then_browser` | Applied to every URL |
| `session_profile` | no | null | Applied to every URL |

## Response shape

**Success (`ok: true`):** always after validation — even if some rows fail. `manifest` (`v: 1`, `alg: ecdsa-p256-sha256`, `manifestRoot`, optional `keyId`/`sig`), `rows[]` (`url`, `finalUrl`, `ok`, `rowLeaf`, optional hashes/receipt/`failureCode`), `timestamp`.

When `OCCAM_RECEIPTS` is off: leaves/root remain; signatures omitted. Failed rows may get **negative** receipts only for selected wall codes (e.g. captcha, login, paywall, 401/403/404/410).

**Tool failure:** `invalid_arguments` only.

## Related env vars

`OCCAM_RECEIPTS`, `OCCAM_PROFILE` — [configuration](../configuration.md).

## Verification

- Per row: [`occam_verify`](occam_verify.md) with `rows[i].receipt`
- Whole set: CLI `verify --mode manifest` (not an MCP mode)

## See also

- [Datasets](../datasets.md) · [Receipts](../receipts.md) · [`occam_digest`](occam_digest.md)
