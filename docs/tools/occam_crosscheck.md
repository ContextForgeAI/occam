# Crosscheck sources (opt-in)

**Canonical tool:** `occam_crosscheck`

**Multi-source comparison / source agreement** — one host fetches a URL through 2–4 local vantages
(HTTP vs browser, anonymous vs session) and compares content fingerprints (`blockMerkleRoot` or
`contentHash`).

**Experimental, opt-in.** Not consensus proof, not multi-node quorum, not proof the content is
correct. The **agreement verdict is unsigned** computed JSON; individual vantage extracts may carry
signed Receipt v1 envelopes you can verify separately.

> **Opt-in tool.** Absent from `tools/list` unless the host starts with `OCCAM_CONSENSUS_MCP=1`.
> It runs 2+ full extracts per call.

## When to use

- You suspect cloaking, personalization, or access-walling (bot vs browser, anon vs logged-in).
- Ordinary reads → [`occam_transcode`](occam_transcode.md).

## Parameters

| Parameter | Type | Default | Required | Description |
|---|---|---|---|---|
| `url` | string | — | **yes** | HTTP or HTTPS URL to cross-check |
| `vantages` | string | `"http,browser"` | no | Comma list: `http`, `browser` |
| `session_profile` | string? | null | no | Adds authenticated vantages per backend |
| `focus_query` | string? | null | no | Focus prune applied identically to every vantage |

## Returns

Success envelope:

- `verdict` — `consensus` | `divergent` | `access_divergent` | `inconclusive`
  - **`consensus`** — vantages agreed on fingerprint. Agreement ≠ correctness.
  - **`divergent`** — materially different content across vantages.
  - **`access_divergent`** — some vantages walled, others succeeded.
- Per-vantage results with optional signed `receipt` each
- **No signed verdict** — re-derive agreement manually from vantage receipts if needed; no shipped
  tool re-derives the verdict cryptographically

Failure envelope: `ok: false`, `url`, `failureCode`, `message`, `timestamp`.

## Failure codes

`invalid_arguments`, plus fetch-level codes when no vantage could extract. See
[failure codes](../failure-codes.md).

## Example

Call:

```json
{ "url": "https://news.example/story", "session_profile": "news-example" }
```

A `divergent` verdict signals different fingerprints — not which vantage was "right."

## Related

- [occam_transcode](occam_transcode.md) — single-vantage read
- [occam_verify](occam_verify.md) — verify each vantage's receipt (not the agreement verdict)
- [Trust & Safety](../trust-and-safety.md)
