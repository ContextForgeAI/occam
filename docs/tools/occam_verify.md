# Verify a receipt

**Canonical tool:** `occam_verify`

Verify Receipt v1 integrity **against a public key you supply** — offline signature check, live drift
re-check, Merkle citation proofs, and watch history chain verification.

**`verified` means bytes + key**, not truth, origin identity, or that the page said what was extracted.

## When to use

- `offline` (default) — check envelope signature; pass `markdown` to also check `contentHash`.
- `live` — re-fetch and report hash drift (context-light; session/playbook not replayed).
- `prove` — emit a compact Merkle proof for one block (needs `json_blocks` receipt).
- `citation` — verify block + proof against signed root — no page needed; membership only.
- `history` — verify watch change chain (`history_verified` vs `history_chain_ok`).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `receipt` | string | — | **yes** | Transcode `receipt` object, bare signed envelope, or watch `history` array |
| `mode` | string | `offline` | no | `offline` \| `live` \| `prove` \| `citation` \| `history` |
| `markdown` | string? | null | no | Markdown to check against `contentHash` (offline) |
| `public_key` | string? | null | no | PEM public key. **Defaults to this host's local key** — supply producer PEM for third-party checks |
| `block_index` | int? | null | prove | Block index for citation proof |
| `block_text` | string? | null | citation | Block text |
| `block_selector` | string? | null | citation | Block `source_selector` |
| `proof` | string? | null | citation | Proof JSON from `prove` |
| `chunks` | string? | null | live | JSON array of chunk leaf-hashes for staleness report |

## Returns

Verify envelope:

- `ok: true`, `signatureValid`, `contentHashMatch?`, `keyId`, `mode`
- `verdict` — e.g. `verified`, `wrong_key`, `signature_invalid`, `drifted`, `refetch_failed`,
  `citation_verified` / `citation_invalid`, `history_verified` / `history_chain_ok` /
  `history_wrong_key` / `history_invalid`
- `live?`, `history?` — mode-specific detail
- `timeAnchor?` — reported on MCP; **does not gate** MCP `verified` (CLI gates on anchor validity)

`prove` mode returns `{ok, keyId, root, leafIndex, leaf, proof[]}`.

## MCP vs CLI

| | MCP | CLI `OccamMcp.Core verify` |
|---|-----|---------------------------|
| `public_key` / `--pubkey` | Optional (local default) | **Mandatory** |
| `manifest` mode | **Not available** | `--mode manifest` for dataset exports |
| `live`, `prove` | Yes | No |

Extract-knowledge telemetry objects are **not** valid input.

## Failure codes

`invalid_receipt`, `invalid_arguments`. See [failure codes](../failure-codes.md).

## Example

```json
{
  "receipt": "{\"signed\":{…},\"blockLeaves\":[…]}",
  "markdown": "# nginx documentation\n…",
  "public_key": "-----BEGIN PUBLIC KEY-----\n…"
}
```

```json
{ "ok": true, "signatureValid": true, "contentHashMatch": true, "verdict": "verified", "keyId": "k1:…" }
```

## Related

- [Receipts](../receipts.md) · [Receipt verification spec](../receipt_verification.md)
- [Guide: verify a source](../guides/verify-sources.md)
- [occam_transcode](occam_transcode.md) — Receipt v1 source
