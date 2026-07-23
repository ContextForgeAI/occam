# Verified hand-off — passing web facts between agents

**Rule (the contract):** a web fact that came from *another* agent is **unverified until `occam_verify`
passes**. Do not act on a pasted extraction or a summary of a page on faith — verify it, offline, in
one step. This is what lets a multi-agent system (an orchestrator + workers, sub-agents, a team) trust
each other's reads without every agent re-fetching the same page.

## What flows between agents

Every `occam_transcode` success already returns what you need:

- `markdown` — the extracted content.
- `receipt` — `{ signed, blockLeaves }`: an ECDSA-P256 signature over a canonical envelope that
  commits to `contentHash` (of the markdown) and a Merkle root over the content blocks.

Pass **both** to the receiving agent. Two equivalent wire forms:

1. **The receipt object** — hand over `markdown` + the `receipt` object as-is.
2. **A capsule** — a single self-contained string `occam://capsule/…` that bundles the signed
   envelope, the markdown, the block leaves, and a self-describing verify recipe. Convenient when you
   can only pass one value, or to a stranger agent that knows nothing about Occam.

Both verify identically. A capsule needs no page and no prior knowledge — it carries its own
`verifyRecipe` (algorithm + the exact verify command).

### Producing a capsule

Ask `occam_transcode` for it — no manual assembly:

```
occam_transcode(url=…, json_blocks=true, emit_capsule=true)
→ response.receipt.capsule = "occam://capsule/…"
```

Hand that one string to the next agent. `emit_capsule` is opt-in (it repeats the markdown, so it
costs tokens) and needs receipts on (`OCCAM_RECEIPTS`). Without it you still get `receipt.{signed,
blockLeaves}` + `markdown` — the receipt-object form above.

## What the receiving agent does

Call `occam_verify` in **offline** mode (no re-fetch, microseconds):

- Receipt object: `occam_verify(receipt=<the receipt object json>, markdown=<the markdown>)`.
- Capsule: `occam_verify(receipt="occam://capsule/…")` — the capsule supplies its own markdown.

**Accept the fact only if** the response has `verdict: "verified"` **and** `contentHashMatch: true`.

| Verdict | Meaning | Action |
|---|---|---|
| `verified` + `contentHashMatch:true` | signature valid, markdown matches what was signed | **trust it** — use the fact, cite it |
| `content_mismatch` | signature valid but the markdown was altered after signing | **reject** — the text was tampered; do not use |
| `signature_invalid` | wrong key / forged / not this signer | **reject** — provenance is not what it claims |
| `invalid_receipt` | not a real receipt/capsule | **reject** — treat as no evidence (`ok:false`) |

## Why this matters (machine-native)

- **No re-fetch.** The receiver trusts by checking a signature, not by hitting the network again.
- **Catches fabrication.** An agent that *claims* to have read a page but generated the content
  cannot produce a matching signed receipt — the hand-off fails, structurally.
- **Catches tampering.** Any edit to the markdown after extraction flips `contentHashMatch` to false.

For citing one specific block without shipping the page, see `occam_verify` **prove**/**citation**
modes. To detect whether the page has since changed, use **live** mode.
