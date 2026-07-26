# occam_claim_check

**Evidence / support lookup** over extracted page content: retrieve blocks that clear a lexical BM25
floor for a claim string, attach Merkle membership proofs, and return a signed Receipt v1 for the
extract — or an honest `found:false`.

**Not a fact check.** `verdict` is always `not_evaluated`. The tool does not classify support vs
refute. Merkle proofs prove **block membership** in the signed extract, not that the claim is true.

## When to use

- Grounding a single assertion against one source URL before citing it.
- Auditing many claims with stance heuristics → [`occam_attest`](occam_attest.md) (unsigned aggregate).
- Third-party re-check of each returned proof → [`occam_verify`](occam_verify.md) `mode=citation`.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `claim` | string | — | **yes** | The assertion to ground (a sentence) |
| `url` | string | — | **yes** | HTTP or HTTPS URL to check against |
| `backend_policy` | string | `http_then_browser` | no | `http`, `browser`, or `http_then_browser` |
| `session_profile` | string? | null | no | For gated pages |
| `max_matches` | int | `3` | no | Max relevant blocks to return (1–10) |

## Returns

Success envelope:

- `ok: true`, `url`, `claim`, `found` — retrieval result, not stance
- `verdict` — always `not_evaluated` (use [`occam_attest`](occam_attest.md) for `status`)
- `proven` — **legacy field:** when `found:false`, `true` means **retrieval-complete negative**
  (untruncated leaf set, no BM25 hit). **Not** semantic proof the page omits the claim. `null` when
  `found:true`.
- `blockMerkleRoot?`, `keyId?` — the signed root proofs anchor to
- `matches[]` — retrieved blocks with text, score, leaf, Merkle proof
- `receipt?` — signed Receipt v1 for the page extract (verifiable)
- `timestamp`

Failure envelope: `ok: false` — page could not be read (content unknown). Distinct from `found:false`.

## Failure codes

`invalid_arguments`, plus the transcode fetch taxonomy. See [failure codes](../failure-codes.md).

## Example

Call:

```json
{
  "claim": "nginx supports weighted round-robin load balancing",
  "url": "https://nginx.org/en/docs/http/load_balancing.html"
}
```

Trimmed response:

```json
{
  "ok": true,
  "found": true,
  "verdict": "not_evaluated",
  "blockMerkleRoot": "…",
  "keyId": "k1:…",
  "matches": [ { "text": "…weight parameter…round-robin…", "score": 0.71, "leaf": "…", "proof": [ … ] } ],
  "receipt": { "signed": { "contentHash": "sha256:…", "sig": "…" } }
}
```

## Related

- [occam_attest](occam_attest.md) — heuristic citation assessment (batch)
- [occam_verify](occam_verify.md) — verify membership proofs
- [Guide: claims](../guides/claims.md) · [Receipts](../receipts.md)
