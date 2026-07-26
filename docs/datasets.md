# Datasets

Signed, auditable URL sets for hand-off — [`occam_dataset_export`](tools/occam_dataset_export.md) transcribes
1–20 URLs, emits a Receipt v1 per row when receipts are enabled, and one manifest signature over the
ordered row Merkle root.

This proves **integrity of the export artifact** under your local key — not that each page's content is
factually correct.

## When to use

- Hand off a corpus for RAG, evaluation, or provenance tracking
- Bind an exact ordered set of row identities (URL, hashes, failure codes) tamper-evidently
- Async bulk transcode **without** provenance → opt-in [`occam_batch_*`](tools/occam_batch.md)

## Export shape

| Part | Role |
|------|------|
| `rows[]` | Per-URL result: `url`, `finalUrl`, `ok`, `contentHash?`, `blockMerkleRoot?`, `failureCode?`, `rowLeaf`, `receipt?` |
| `manifest` | `{v, createdAt, rowCount, manifestRoot, keyId, alg, sig}` — one signature over the ordered row leaf multiset |

A row can be `ok: false` with a typed `failureCode` while the export call succeeds — the failure is
part of the signed record (honest inclusion of unreadable URLs).

The tool response `ok: true` means the export completed — **not** that every URL extracted successfully.

## What the manifest proves

!!! success "What this proves"
    Exactly this ordered set of row leaves (derived from each row's identity fields) was signed together
    under the manifest key. Per-row Receipt v1 envelopes (when present) bind each row's signed extraction
    individually (integrity vs key — not factual correctness).

!!! failure "What this does not prove"
    - Factual correctness of any row's markdown
    - That extracts were live/fresh or origin-served
    - Who signed (local self-signed key only)
    - Semantic quality of the corpus

Manifest verification binds **row identity and order**, not row content semantics. Content checks
require per-row `occam_verify` against each `receipt` and optional markdown.

## Verify limits

### Per-row receipt

Any row with `receipt` verifies like a transcode receipt:

- MCP [`occam_verify`](tools/occam_verify.md) — `mode=offline` (+ optional `markdown`)
- Pass the **producer's public key** when verifying on another host

### Manifest (set binding)

**CLI only** — MCP `occam_verify` has no `manifest` mode:

```bash
OccamMcp.Core verify --mode manifest --input export.json --pubkey pubkey.pem
```

Requires the full export JSON and the signing public key PEM. Exit `0` = manifest signature valid under
that key.

!!! note "Operator wrapper gap"
    The friendly `occam` Node wrapper does not expose `verify` — invoke the host binary directly
    (`OccamMcp.Core verify …`).

### What MCP agents cannot do structurally

- Verify dataset manifest signatures through MCP alone
- Infer manifest validity from `occam_dataset_export` response fields without CLI verify

Plan hand-off accordingly: export JSON file + exported public key PEM + documented verify command.

## Merkle and receipts in exports

- Each successful row may include `contentHash`, `blockMerkleRoot`, and Receipt v1.
- Failed rows contribute signed failure identity in the row leaf — content unknown, failure typed.
- Citation proofs for blocks inside a row use that row's receipt like any transcode.

Merkle proofs prove block membership in **that row's signed extract**, not claim truth.

## Configuration

- [`OCCAM_RECEIPTS=off`](configuration.md#receipts) removes per-row signed envelopes but does not
  change export structure semantics — see [Receipts](receipts.md) (not a master switch).
- Playbook overlay is forced `auto` for dataset export paths.

## Example workflow

1. Export:

```json
{
  "name": "occam_dataset_export",
  "arguments": {
    "urls": "[\"https://nginx.org/en/docs/\", \"https://nginx.org/en/docs/http/load_balancing.html\"]"
  }
}
```

2. Save response JSON to disk.
3. Export producer public key: `OccamMcp.Core keys export`.
4. Consumer verifies manifest (CLI) and spot-checks rows with `occam_verify`.

See [Example: signed dataset export](examples/dataset-export.md).

## Related

- [`occam_dataset_export`](tools/occam_dataset_export.md)
- [Receipts](receipts.md)
- [Guide: verify a source](guides/verify-sources.md)
- [Trust & Safety](trust-and-safety.md)
