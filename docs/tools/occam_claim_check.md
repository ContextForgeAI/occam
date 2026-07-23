# occam_claim_check

Does **this** page contain lexically relevant blocks for a claim? Extracts the page and returns the
top BM25-ranked source block(s), each with a Merkle citation proof and a signed receipt — or an
honest `found:false`.

**`found` is retrieval only** — not semantic support. The tool proves **which** block is relevant
enough to retrieve. **You** (or [`occam_attest`](occam_attest.md)) judge support vs refute — BM25 will
not guess stance.

## When to use

- Grounding a single assertion against one source URL before citing it.
- Auditing many claims across many sources at once → [`occam_attest`](occam_attest.md).
- A third party can re-verify each returned proof with [`occam_verify`](occam_verify.md)
  `mode=citation` — no page refetch needed.

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

- `ok: true`, `url`, `claim`, `found` — `false` means the page has no sufficiently matching block
  (that is a result, not an error)
- `blockMerkleRoot?`, `keyId?` — the signed root the proofs anchor to
- `matches[]` — each relevant block with its text, score, leaf and Merkle proof (verify via
  `occam_verify` citation mode)
- `receipt?` — the signed extraction receipt for the page
- `timestamp`

Failure envelope: `ok: false`, `url`, `claim`, `failure: {code, message}`, `receipt?`, `timestamp`.
`ok:false` = the page could not be read at all (content unknown) — distinct from `found:false`.

## Failure codes

`invalid_arguments`, plus the transcode fetch taxonomy (`timeout`, `http_*`, `thin_extract`,
`captcha_or_challenge`, `requires_login`, …). See [failure codes](../failure-codes.md).

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
  "blockMerkleRoot": "…",
  "keyId": "k1:…",
  "matches": [ { "text": "…weight parameter…round-robin…", "score": 0.71, "leaf": "…", "proof": [ { "hash": "…", "siblingIsRight": true } ] } ],
  "receipt": { "v": 1, "kind": "extraction", "sig": "…" }
}
```

## Related

- [occam_attest](occam_attest.md) — the batch form
- [occam_verify](occam_verify.md) — third-party verification of the proof
- [Receipts](../receipts.md)
