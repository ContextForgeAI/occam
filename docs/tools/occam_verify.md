# occam_verify

Verify or cite an extraction receipt **without trusting FF-Occam**. Five modes: offline signature
check, live drift re-check, building a block citation proof, verifying such a proof, and verifying
a watch history chain.

## When to use

- `offline` (default) — check a receipt's signature; pass `markdown` to also check `contentHash`.
- `live` — re-fetch the page and report how much drifted, and which of your RAG chunks went stale.
- `prove` — emit a compact proof that one block was in the page (needs a `json_blocks` receipt).
- `citation` — verify someone else's block + proof against the signed root — no page needed.
- `history` — verify a signed [`occam_watch`](occam_watch.md) change chain.

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `receipt` | string | — | **yes** | A transcode response's `receipt` object (`{signed, blockLeaves, timeAnchor}`) or a bare signed envelope. In `history` mode: the watch `history` array or `{history:[…]}` |
| `mode` | string | `offline` | no | `offline` \| `live` \| `prove` \| `citation` \| `history` |
| `markdown` | string? | null | no | Extracted markdown to check against `contentHash` (offline) |
| `public_key` | string? | null | no | PEM public key to verify against; omit for this host's local key |
| `block_index` | int? | null | prove | Index of the block to build a citation proof for |
| `block_text` | string? | null | citation | The block's text |
| `block_selector` | string? | null | citation | The block's `source_selector` |
| `proof` | string? | null | citation | The proof JSON (array of `{hash, siblingIsRight}`) from a `prove` call |
| `chunks` | string? | null | live | JSON array of chunk leaf-hashes your RAG store holds for this URL; the response reports which of **these** went stale. Omit to check the receipt's own block leaves |

## Returns

Verify envelope (`offline` / `live` / `citation` / `history`):

- `ok: true`, `signatureValid`, `contentHashMatch?`, `keyId`, `mode`
- `verdict` — e.g. `verified`, `drifted`, `refetch_failed`, `signature_invalid`,
  `citation_verified` / `citation_invalid`, `history_verified` / `history_invalid`
- `live?` (live mode) — `{refetched, contentHashMatch?, blockRootMatch?, blocksTotal?,
  blocksStillPresent?, drift?, chunkStaleness?}`; `chunkStaleness` is
  `{total, present, stale, staleChunks[]}`
- `history?` (history mode) — `{entriesTotal, signedCount, headSeq, chainValid}`
- `timeAnchor?` — `{present, valid, genTime?, tsa?, tsaSubject?}` when the receipt carried an
  independent RFC3161 timestamp

`prove` mode returns `{ok, keyId, root, leafIndex, leaf, proof[]}` — hand `leaf`-owner the block
text + `proof` and they verify with `citation` mode.

Failure envelope: `ok: false`, `failureCode`, `message`.

## Failure codes

`invalid_receipt` (not valid receipt JSON, unsupported version, or `blockLeaves` don't reconstruct
the signed root), `invalid_arguments` (missing mode-specific inputs).

## Example

Offline check of a transcode receipt:

```json
{ "receipt": "{\"signed\":{…},\"blockLeaves\":[…]}", "markdown": "# nginx documentation\n…" }
```

Trimmed response:

```json
{ "ok": true, "signatureValid": true, "contentHashMatch": true, "keyId": "k1:…", "mode": "offline", "verdict": "verified" }
```

## Related

- [Receipts](../receipts.md) and the normative [receipt verification spec](../receipt_verification.md)
- [occam_transcode](occam_transcode.md) — where receipts come from (`json_blocks` for block proofs)
- [occam_claim_check](occam_claim_check.md) / [occam_attest](occam_attest.md) — emit citation proofs this tool verifies
