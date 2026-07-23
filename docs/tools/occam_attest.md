# occam_attest

Before shipping a report, check its citations with a **fail-closed trust model**.
Given a JSON array of `{claim, sourceUrl}` rows, Occam runs three independent
layers per claim:

1. **Retrieval** — BM25 / claim-check surfaces the best candidate blocks (scores only).
2. **Semantic support** — a local classifier returns `status`
   (`supported` | `contradicted` | `related` | `unsupported` | `unknown`).
3. **Merkle proof** — when a block is attached, `leaf` + `proof` prove only that
   the block existed in the signed extract — **never** that the claim is true.

**Gate on `status`.** `grounded` is a compat alias: `true` only when
`status=supported`. Lexical / BM25 hits alone never set `grounded=true`.

## When to use

- Final honesty gate for a research report: refuse to ship claims the source
  does not semantically support.
- One claim, one page (retrieval only) → [`occam_claim_check`](occam_claim_check.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `claims` | string | — | **yes** | JSON array of `{"claim":"…","sourceUrl":"https://…"}` rows (1–50) |
| `backend_policy` | string | `http_then_browser` | no | Applied to every cited page |
| `session_profile` | string? | null | no | Applied to every cited page |

## Returns

Success envelope:

- `ok: true`, `claimsTotal`, `timestamp`
- Named status counts (canonical): `supported`, `contradicted`, `related`,
  `unsupported`, `unknown`
- Compat: `grounded` (= `supported`); `unsupportedTotal` (= sum of all
  non-supported statuses; `grounded + unsupportedTotal == claimsTotal`)
- `perClaim[]` — `{claim, sourceUrl, status, grounded, blockIndex?, text?, score?,
  leaf?, proof?, blockMerkleRoot?, receipt?, reason?}`
  - `status` is the semantic verdict (fail-closed)
  - `grounded` ≡ `status == "supported"`
  - citation fields prove **block existence** via [`occam_verify`](occam_verify.md)
    `mode=citation`; they do **not** mean the claim is true
  - `reason` explains non-supported / unknown rows (`no_matching_block`,
    `no_semantic_support`, `related_not_supported`, `contradicted_by_source`,
    `insufficient_confidence`, or an extraction failure code)

Failure envelope: `ok: false`, `failure: {code, message}`, `timestamp` — only for
bad input; per-page fetch failures surface as `status=unknown` rows with a `reason`.

## Failure codes

`invalid_arguments` (empty/malformed `claims`, more than 50 rows, bad `backend_policy`).
Per-row fetch problems appear in `perClaim[].reason` with `status=unknown`, not as a
call failure.

## Example

Call:

```json
{
  "claims": "[{\"claim\":\"nginx supports weighted round-robin\",\"sourceUrl\":\"https://nginx.org/en/docs/http/load_balancing.html\"},{\"claim\":\"nginx was written in Rust\",\"sourceUrl\":\"https://nginx.org/en/\"}]"
}
```

Trimmed response:

```json
{
  "ok": true,
  "claimsTotal": 2,
  "supported": 1,
  "contradicted": 0,
  "related": 0,
  "unsupported": 1,
  "unknown": 0,
  "grounded": 1,
  "unsupportedTotal": 1,
  "perClaim": [
    { "claim": "nginx supports weighted round-robin", "status": "supported", "grounded": true, "score": 0.7, "leaf": "…", "proof": [ … ], "blockMerkleRoot": "…" },
    { "claim": "nginx was written in Rust", "status": "unsupported", "grounded": false, "reason": "no_matching_block" }
  ]
}
```

## Related

- [occam_claim_check](occam_claim_check.md) — single-claim retrieval (not stance)
- [occam_verify](occam_verify.md) — verify existence proofs
- [occam_dataset_export](occam_dataset_export.md) — signed corpora for the sources themselves
