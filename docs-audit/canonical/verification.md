# Receipt verification

**Slug:** `verification` · **Product system:** PS-6 Trust and provenance · **CAPs:** 11 · **Public relevance:** HIGH.

## What it is

Verification checks receipt signatures/content hashes, Merkle citations, watch-history links, optional live drift, and CLI-only dataset manifests. MCP and CLI surfaces differ materially (CAP-258–260/262/268/274/276, CAP-650–653; TRUST-MODEL §7).

Every verdict is about bytes, keys, hash membership, or a re-fetch comparison—not truth, origin authenticity, or signer identity (TRUST-MODEL §13).

## Why it exists

- Check that a receipt's canonical fields were signed by the supplied key holder (CAP-259).
- Compare optional markdown to signed contentHash (CAP-259).
- Produce or consume compact block membership citations (CAP-262/271/272).
- Re-fetch and report content/block drift (CAP-270/652).
- Check watch chain structure/signatures and dataset set-binding (CAP-273/276/283).

## User-visible entrypoints

| Surface | Modes | Evidence |
|---|---|---|
| MCP `occam_verify` | `offline` (default), `live`, `prove`, `citation`, `history` | CAP-268–274 |
| Core CLI `occam verify` | `receipt`, `citation`, `manifest`, `history` | CAP-276 |
| Core CLI `occam keys export` | Public PEM export; can mint a key in empty store | CAP-275 |

The friendly Node `occam` wrapper does not route `verify` or `keys` (EF-025). `reader` profile can produce receipts via transcode but hides MCP verify (TRUST-MODEL D9).

## Core behavior

1. MCP history mode parses history before receipt/capsule handling (CAP-273).
2. Other modes parse capsule, wrapped receipt, or bare envelope (CAP-274/650).
3. Choose supplied `public_key` or silently default MCP to local host key (CAP-650; TRUST-MODEL X6).
4. Offline verify canonical signature; optionally compare markdown hash and report time-anchor sidecar (CAP-259/261/269).
5. Live mode proceeds only if signature valid, then runs a fresh bare `http_then_browser` pipeline comparison (CAP-270/652).
6. Prove mode validates supplied leaves reconstruct the signed root before generating a path (CAP-271).
7. Citation mode requires both valid envelope signature and leaf membership (CAP-272).
8. History checks sequence/links and signatures only where present (CAP-273/284).

## Advanced behavior

| Mode | Network | Proves | Key limitation | Evidence |
|---|---:|---|---|---|
| MCP offline | no | Signature + optional content bytes | Anchor is non-gating | TRUST-MODEL §7 |
| MCP live | yes | Bare current re-fetch matches hashes | Drops original context; no second receipt | CAP-652/653 |
| MCP prove | no | Supplied leaves bind to root, then emits proof | Leaf-count ambiguity | CAP-271 |
| MCP citation | no | Exact text+selector leaf membership under signed root | Not truth/context | CAP-272 |
| MCP history | no | Chain links and signatures that exist | Fully unsigned chain can pass | EFC-P5-05-2 |
| CLI receipt | no | Offline checks plus anchor gating | Requires explicit PEM | CAP-276 |
| CLI manifest | no | Ordered dataset row identity set | Row identity, not row content | CAP-283 |

## Automatic / silent behavior

- Unknown MCP `mode` silently falls through to offline and reports `"mode":"offline"` (CAP-651; EF-011).
- MCP `public_key` omitted uses this running host's own key, which can turn wrong-key mistakes into `signature_invalid` (CAP-650; TRUST-MODEL D6/X6).
- Capsules work for offline/live/prove/citation, though history branches before capsule parsing (CAP-650).
- Live mode hardcodes `http_then_browser`, no session, no playbook, no budget, no selectors (CAP-652).
- All live fetch failures collapse to `refetch_failed` (CAP-653; EF-012).
- MCP reports invalid time anchor but can still return `verified`; CLI fails the verdict (TRUST-MODEL §7).
- Unsigned history entries skip signature verification (EFC-P5-05-2).

## Parameters

| Name | Default | Effect | Evidence |
|---|---|---|---|
| `receipt` | required | Capsule/wrapper/bare envelope; history JSON in history mode | CAP-274/650 |
| `markdown` | null | Optional contentHash comparison; capsule content can fill | CAP-259/269 |
| `public_key` | local host PEM | Trust anchor for MCP checks | CAP-259; X6 |
| `mode` | `offline` | Dispatch; unknown silently offline | CAP-268/651 |
| `block_index` | null | Required prove index | CAP-271 |
| `block_text` | null | Citation leaf text | CAP-272 |
| `block_selector` | empty when omitted | Citation selector component | CAP-252/272 |
| `proof` | null | JSON Merkle path for citation | CAP-272 |
| `chunks` | null | Live chunk-staleness inputs | CAP-266/270 |

No live backend/session/playbook/budget/cache parameter exists.

## Configuration

`OCCAM_KEYS_ROOT` determines default local MCP key. There is no verify-specific env variable. Live mode inherits acquisition env; history behavior depends on how producer used `OCCAM_RECEIPTS`.

CLI requires `--pubkey`; MCP does not (CAP-276; TRUST-MODEL §7).

## Backends

Offline/prove/citation/history use no backend. Live uses `TranscodePipeline` with hardcoded corrected cascade and no cache because it bypasses `OccamTranscodeTool` (CAP-652; EF-056).

## Sessions / state

Verification is stateless unless reading caller-saved artifacts. Local key is persistent ART-034. Live mode does not restore the session used by the original extraction (CAP-652).

## Network behavior

Only live mode fetches, through full pipeline. It may reach managed provider if configured and router gates allow. It emits no new receipt (CAP-652).

All other modes are offline; CLI manifest/history are also offline.

## Artifacts produced

ART-021 verify verdict; prove emits citation package; live emits drift/chunk staleness; history emits chain verdict (`ARTIFACT-ONTOLOGY.md:113`).

The verdict itself is not signed. It is a computation over supplied artifacts/current fetch.

## Trust / provenance properties

Offline verifies “holder of private key for supplied PEM signed these bytes.” It does not determine who owns the key or whether assertions are true (CAP-259/288; TRUST-MODEL C4/C12).

Citation verifies extracted-leaf membership, not page origin/truth (C7). Live verification is same-process observation and no new signed artifact (C11). Manifest binds ordered row identities only (TRUST-MODEL §7).

`history_verified` can mean an internally linked chain signed by nobody; callers must inspect `signedCount` over MCP, while CLI gives no count (TRUST-MODEL D3).

## Failure / fallback behavior

- Malformed JSON/base64/PEM/proof maps to typed invalid verdict, not exception (CAP-291).
- Unknown mode silently offline (EF-011).
- Wrong key and tamper both become `signature_invalid` (TRUST-MODEL D6).
- Live failures become `refetch_failed` without underlying code (CAP-653/EF-012).
- Citation gates membership on signature validity (CAP-272).
- CLI unknown mode exits 2; verified=0, parsed-not-verified=1, usage/IO=2 (CAP-276).

## Platform differences

Canonical crypto is cross-platform. Public-key trust handoff is out of band on all platforms. Key-file protection is weaker by implementation on Windows (no hardening) than POSIX best-effort 0600 (CAP-255).

## Composition with other capabilities

- Consumes `receipts`, capsules, claim proofs, watch histories, and dataset manifests.
- Live mode reuses acquisition/materialization but cannot reproduce contextual options.
- Dataset manifest verification is CLI-only; per-row receipts remain MCP-verifiable (CAP-283).
- Claim/attest aggregate verdicts are not verify modes because those aggregates are unsigned (TRUST-MODEL §7).

## Known limitations

- No key identity/PKI.
- MCP local-key default is unsafe for foreign receipt ergonomics.
- Unknown mode downgrade.
- Live context loss and generic failure.
- No manifest MCP mode.
- No crosscheck/attest aggregate verification.
- Unsigned histories pass.
- Time-anchor parity differs MCP vs CLI.
- Friendly wrapper cannot reach trust CLI verbs (EF-025).

## Engineering findings

- EF-011: unknown mode → offline.
- EF-012: live drops context and collapses failure.
- EF-018: manifest verification CLI-only.
- EF-025: wrapper lacks verify/keys.
- EFC-P5-05-2: unsigned history verifies.
- EFC-P5-05-3: leaf-count-derived drift/prove range is not signed.
- EFC-P5-05-5: no wrong-key verdict.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamVerifyTool.cs:23-319`
- `src/FFOccamMcp.Core/Receipts/ReceiptVerifier.cs:6-80`
- `src/FFOccamMcp.Core/Receipts/MerkleTree.cs:109-169`
- `src/FFOccamMcp.Core/Cli/OccamCliVerbs.cs:208-409`
- `src/FFOccamMcp.Core/Watch/WatchHistory.cs`
- CAP-258–260/262/268/274/276, CAP-650–653; ART-021; TRUST-MODEL §7.

## Public-doc relevance

Critical. Explain each mode separately, exact proof boundaries, required PEM provenance, MCP/CLI asymmetry, live context loss, unsigned-history gap, and verdict vocabulary. Never equate `verified` with true, accurate, origin-authenticated, or trusted.

## Handbook relevance

Provide mode decision tables and copy-ready offline/citation/manifest workflows, with explicit key handoff and interpretation language. Treat live mode as a contextual re-fetch comparison, not proof that a page changed.
