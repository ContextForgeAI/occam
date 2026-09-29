# `occam_verify`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamVerifyTool.cs` @ 2026-09-29  
**Profiles:** `reader` (default), `researcher`, `auditor`, `full` — not `minimal` or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Consumer half of Receipt v1: check signatures and optional content hashes, re-fetch for drift, build or verify Merkle citation proofs, or verify a signed `occam_watch` history chain. A passing `verdict` means cryptographic checks against the **public key you supply** (or this host’s local key) — not that the page is true.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| You have a transcode `receipt` (`signed` + optional `blockLeaves` / `timeAnchor`) or bare signed envelope | Input is extract-knowledge telemetry (not Receipt v1) |
| Offline integrity (`offline`) or live staleness (`live`) | Dataset **set** binding → CLI `verify --mode manifest` (not on MCP) |
| Portable agent bundle via `occam://capsule/…` as `receipt` | — |
| Citation without the full page (`prove` → `citation`) | — |
| Watch change-chain audit (`history`) | — |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `receipt` | yes | — | Transcode receipt, bare envelope, `occam://capsule/…`, or (history) watch entries / `{ history: [...] }` |
| `markdown` | no | null | Markdown vs `contentHash` (`offline` / pre-live; capsule may supply content) |
| `public_key` | no | host local PEM | PEM public key; omit → this host’s local key |
| `mode` | no | `offline` | `offline` \| `live` \| `prove` \| `citation` \| `history` |
| `block_index` | prove only | null | Block index (needs `blockLeaves`) |
| `block_text` | citation only | null | Block text |
| `block_selector` | no | null | Optional `source_selector` for citation leaf hash |
| `proof` | citation only | null | Proof JSON from `prove` |
| `chunks` | no | null | `live` only: JSON array of chunk leaf-hashes; omit → use receipt `blockLeaves` |

## Modes

- **`offline`:** verify signature (+ optional markdown vs hash); report `timeAnchor` without changing MCP `verdict`.
- **`live`:** offline first; if `signatureValid`, re-fetch (`http_then_browser`); `verified` / `drifted` / `refetch_failed` + `live` object.
- **`prove`:** Merkle proof for `block_index` → `{ ok, keyId, root, leafIndex, leaf, proof[] }`.
- **`citation`:** membership of `block_text` (+ optional selector) via `proof`.
- **`history`:** watch chain integrity + per-entry signatures.

## Response shape

**Standard success (`ok: true`):** `signatureValid`, `contentHashMatch?`, `keyId`, `mode`, `verdict`, optional `live` / `history` / `timeAnchor`.

**MCP verdicts:** offline `verified` \| `wrong_key` \| `signature_invalid` \| `content_mismatch`; live adds `drifted` \| `refetch_failed`; citation `citation_verified` \| `citation_invalid`; history `history_verified` \| `history_chain_ok` \| `history_wrong_key` \| `history_invalid`.

**Failure (`ok: false`):** `failureCode`, `message` (not nested `failure`). Codes: `invalid_receipt`, `invalid_arguments`.

## MCP vs CLI

MCP has `live` / `prove`; CLI has `manifest` and may gate “verified” on time-anchor validity. MCP reports `content_mismatch`; CLI receipt mode may say `content_hash_mismatch`.

## Related env vars

None read directly by this tool. Host key / `OCCAM_PROFILE` → [configuration](../configuration.md).

## See also

- [Receipts](../receipts.md) · [Receipt verification](../receipt_verification.md) · [Guide: verify sources](../guides/verify-sources.md)
- [`occam_transcode`](occam_transcode.md) · [`occam_dataset_export`](occam_dataset_export.md)
