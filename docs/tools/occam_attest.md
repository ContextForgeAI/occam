# occam_attest

**Heuristic citation assessment** for a batch of `{claim, sourceUrl}` rows — **not** cryptographic
attestation, not proof of truth, and not vendor/root identity certification.

Per claim, Occam:

1. **Retrieves** candidate blocks (BM25 / claim-check path).
2. **Classifies stance** with a narrow regex/rule entailment engine → `status`
   (`supported` | `contradicted` | `related` | `unsupported` | `unknown`).
3. **Attaches Merkle proof** for the top retrieved block when present — proof = **block membership**
   in the signed extract only.

**Gate on `status`.** `grounded` is a compat alias: `true` only when `status=supported`. BM25 score
alone never sets `grounded=true`.

The **aggregate response** (`supported`, `contradicted`, counts, partition totals) is **unsigned**
plain JSON. Individual nested `receipt` / `proof` fields remain separately verifiable.

## When to use

- Batch honesty gate before shipping a research report: refuse rows with `status` other than
  `supported` when you require explicit support.
- Single-claim retrieval without stance → [`occam_claim_check`](occam_claim_check.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `claims` | string | — | **yes** | JSON array of `{"claim":"…","sourceUrl":"https://…"}` rows (1–50) |
| `backend_policy` | string | `http_then_browser` | no | Applied to every cited page |
| `session_profile` | string? | null | no | Applied to every cited page |

## Returns

Success envelope:

- `ok: true`, `claimsTotal`, `timestamp`
- Status counts: `supported`, `contradicted`, `related`, `unsupported`, `unknown`
- Compat: `grounded` (= supported count); `unsupportedTotal`
- `perClaim[]` — `{claim, sourceUrl, status, grounded, blockIndex?, text?, score?,
  leaf?, proof?, blockMerkleRoot?, receipt?, reason?}`
  - `status` — heuristic classifier verdict (fail-closed)
  - `grounded` ≡ `status == "supported"` — not proof of truth
  - `proof` / `receipt` — verify via [`occam_verify`](occam_verify.md); membership only

Failure envelope: `ok: false` for bad input only; per-page fetch failures → `status=unknown` rows.

## Failure codes

`invalid_arguments` (empty/malformed `claims`, more than 50 rows, bad `backend_policy`).

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
  "unsupported": 1,
  "grounded": 1,
  "perClaim": [
    { "claim": "nginx supports weighted round-robin", "status": "supported", "grounded": true, "leaf": "…", "proof": [ … ] },
    { "claim": "nginx was written in Rust", "status": "unsupported", "grounded": false, "reason": "no_matching_block" }
  ]
}
```

## Related

- [occam_claim_check](occam_claim_check.md) — retrieval-only evidence lookup
- [occam_verify](occam_verify.md) — verify nested proofs
- [Guide: claims](../guides/claims.md)
