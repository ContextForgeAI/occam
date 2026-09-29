# `occam_crosscheck`

**Status:** verified against `src/FFOccamMcp.Core/Tools/OccamCrosscheckTool.cs` @ 2026-09-29  
**Profiles:** opt-in — `OCCAM_CONSENSUS_MCP=1` (not part of `OCCAM_PROFILE` lists)  
**Contract depth:** [MCP API](../reference/mcp-api.md) · runtime `tools/list`

## Purpose

Cross-check one URL from **2–4 local vantages** on the same host (HTTP vs browser, optional session). Compares fingerprints and returns an **unsigned** agreement verdict. Multi-source comparison — **not** consensus proof or multi-node quorum.

## When to use / when not

| Use when | Do not use when |
|----------|-----------------|
| Suspect cloaking / bot-vs-browser / anon-vs-session | Single read is enough → [`occam_transcode`](occam_transcode.md) |
| Pairwise fingerprint signals | Independent geo witnesses or signed “jury” verdict |
| Interpreting `divergent` / `access_divergent` as signals | Claim stance → [`occam_claim_check`](occam_claim_check.md) / [`occam_attest`](occam_attest.md) |

## Parameters

| Name | Required | Default | Meaning |
|------|----------|---------|---------|
| `url` | yes | — | HTTP(S) URL |
| `vantages` | no | `http,browser` | Comma list: `http` and/or `browser` only |
| `session_profile` | no | null | Adds authenticated vantage per backend |
| `focus_query` | no | null | Identical prune on every vantage |

Each vantage: `json_blocks=true`, `playbook_policy=off`.

## Response shape

**Success (`ok: true`)** after valid args — even if all vantages fail:

- `verdict`: `consensus` \| `divergent` \| `access_divergent` \| `inconclusive`
- `vantages[]`, `divergence[]`, `timestamp`

**Tool failure (`ok: false`):** only `invalid_arguments`. Per-vantage failures stay in `vantages[]`.

## Related env vars

`OCCAM_CONSENSUS_MCP=1`, `OCCAM_RECEIPTS`.

## See also

- [`occam_transcode`](occam_transcode.md) · [`occam_verify`](occam_verify.md) · [experimental](../experimental.md) · [trust-and-safety](../trust-and-safety.md)
