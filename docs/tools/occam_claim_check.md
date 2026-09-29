# `occam_claim_check`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamClaimCheckTool.cs` @ 2026-09-29  
**Profiles:** `researcher`, `auditor`, `full` — **not** default `reader`, `minimal`, or `basic`  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Live claim grounding for one sentence against one URL: fetch via transcode pipeline, rank blocks by BM25 vs `claim`, return top matches with Merkle leaf + proof. Shows **which source text was retrieved**; does **not** decide support vs refute (`verdict` always `not_evaluated`).

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Provable block(s) from this URL for one assertion | Host-side stance status → [`occam_attest`](occam_attest.md) |
| You will judge support yourself from `matches[].text` | Full page markdown only → [`occam_transcode`](occam_transcode.md) |
| Re-check leaf + proof via [`occam_verify`](occam_verify.md) `citation` | Expecting truth from the claim string alone |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `claim` | yes | — | Assertion to ground (a sentence) |
| `url` | yes | — | HTTP(S) URL |
| `backend_policy` | no | `http_then_browser` | `http`, `browser`, or `http_then_browser` |
| `session_profile` | no | null | Optional session profile id |
| `max_matches` | no | `3` | Max blocks (1–10; silently clamped) |

**Fixed internally:** `json_blocks=true`, `playbook_policy=auto`; no caller `max_tokens` / `fit_markdown`.

## Response shape

**Success:** `found`, `retrieved` (= `found`), `verdict: not_evaluated`, optional `proven` when `found:false` (retrieval-complete negative only — not semantic absence), `blockMerkleRoot`, `matches[]` (`blockIndex`, `text`, `score`, `leaf`, `proof[]`, …), optional `receipt`, `timestamp`.

**Failure:** `failure: { code, message }` — page content unknown. Distinct from success with `found: false`.

## Common failure codes

`invalid_arguments`; plus transcode taxonomy on fetch/extract. Full list: [failure-codes](../failure-codes.md).

## Related env vars

`OCCAM_PROFILE`, `OCCAM_RECEIPTS`, session profiles — [configuration](../configuration.md).

## See also

- [`occam_verify`](occam_verify.md) · [`occam_attest`](occam_attest.md) · [Guide: claims](../guides/claims.md)
