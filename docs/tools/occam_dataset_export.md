# occam_dataset_export

Build a signed, auditable dataset from a set of URLs (1–20): each row is transcoded with its own
signed receipt, and one manifest signature covers the Merkle root of all rows — so the set is
tamper-evident and verifiable per-row and per-set.

## When to use

- Handing off an auditable corpus for RAG, evaluation, or provenance.
- Each row verifies via [`occam_verify`](occam_verify.md); the set verifies by reconstructing the
  manifest root from the rows plus checking one signature.
- Just want async bulk transcodes without provenance → the opt-in
  [`occam_batch_*`](occam_batch.md) tools.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `urls` | string | — | **yes** | JSON array of HTTP/HTTPS URL strings (1–20) |
| `backend_policy` | string | `http_then_browser` | no | Applied to every URL |
| `session_profile` | string? | null | no | Applied to every URL |

## Returns

- `ok`, `timestamp`
- `manifest` — `{v, createdAt, rowCount, manifestRoot, keyId, alg, sig}` — the one signature over
  the Merkle root of the per-row leaves
- `rows[]` — `{url, finalUrl, ok, contentHash?, blockMerkleRoot?, failureCode?, rowLeaf,
  receipt?}`; a row can be `ok:false` (with `failureCode`) while the export as a whole succeeds —
  the failure is then part of the signed record

Failure envelope: `ok: false`, `failure: {code, message}`, `timestamp` — only for bad input.

## Failure codes

`invalid_arguments` (empty/malformed `urls`, more than 20, bad `backend_policy`). Per-row fetch
failures land in `rows[].failureCode` using the transcode taxonomy — see
[failure codes](../failure-codes.md).

## Example

Call:

```json
{ "urls": "[\"https://nginx.org/en/docs/\", \"https://nginx.org/en/docs/http/load_balancing.html\"]" }
```

Trimmed response:

```json
{
  "ok": true,
  "manifest": { "v": 1, "createdAt": "2026-07-18T…", "rowCount": 2, "manifestRoot": "…", "keyId": "k1:…", "alg": "…", "sig": "…" },
  "rows": [
    { "url": "https://nginx.org/en/docs/", "ok": true, "contentHash": "sha256:…", "blockMerkleRoot": "…", "rowLeaf": "…", "receipt": { … } }
  ]
}
```

## Related

- [occam_verify](occam_verify.md) — verify a row's receipt
- [Receipts](../receipts.md) · [Receipt verification spec](../receipt_verification.md)
