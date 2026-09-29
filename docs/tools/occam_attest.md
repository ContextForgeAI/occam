# `occam_attest`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamAttestTool.cs` and `Attest/AttestService.cs` @ 2026-09-29  
**Profiles:** `auditor`, `full` — not on `reader`, `researcher`, `minimal`, or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Batch citation check for report rows `{claim, sourceUrl}`. For each row: live extract, retrieve up to **three** BM25 blocks, fail-closed semantic classifier → `status`, attach Merkle proof for the top block when present. Proof = block membership only — never claim truth. Aggregate JSON is **unsigned**.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Stance per cited row in a report | One claim retrieval only → [`occam_claim_check`](occam_claim_check.md) |
| Gate on `status` (not BM25 alone) | Claims outside **is-a** / **uses** classifier shapes → often `unknown` |
| Batch counts (`supported`, `contradicted`, …) | Profile is `reader` / `researcher` |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `claims` | yes | — | JSON array of `{claim, sourceUrl}` (1–50) |
| `backend_policy` | no | `http_then_browser` | Applied to every claim |
| `session_profile` | no | null | Applied to every cited page |

## Response shape

**Success:** counts (`supported`, `contradicted`, `related`, `unsupported`, `unknown`), `grounded` (= supported count), `unsupportedTotal`, `perClaim[]` with `status`, evidence fields, optional `reason`.

**Gate on `status`.** Per-row fetch failures become `status=unknown` without failing the whole call.

**Failure (`ok: false`):** `invalid_arguments` only (bad/empty/oversized `claims`, bad policy).

## Semantic classifier

Parses only **is-a** (`Subject is [a|an|the] object`) and **uses** (`uses|using|utilizes`). Lexical co-occurrence alone never yields `supported`.

## Related env vars

`OCCAM_PROFILE` must be `auditor` or `full`. Full catalog: [configuration](../configuration.md).

## See also

- [`occam_claim_check`](occam_claim_check.md) · [`occam_verify`](occam_verify.md) · [Guide: claims](../guides/claims.md)
- [Failure codes](../failure-codes.md)
