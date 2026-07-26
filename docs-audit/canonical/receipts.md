# Receipts and proofs

**Slug:** `receipts` · **Product system:** PS-6 Trust and provenance · **CAPs:** 35 · **Public relevance:** HIGH.

## What it is

Receipt v1 is a local, single-key integrity envelope for host assertions about extraction success or selected failures. It combines fixed canonical bytes, ECDSA P-256 signatures, content hashes, optional block-Merkle commitments, optional capsules, and an optional RFC3161 sidecar (CAP-090–093, CAP-250–291; TRUST-MODEL §1–3).

It is not verified origin provenance. One auto-minted, self-signed local key attests to the host's own assertions; no PKI, identity binding, registry, revocation, rotation, or origin transcript exists (CAP-255/288/289; TRUST-MODEL §1/§13).

## Why it exists

- Detect post-delivery edits to signed envelope fields and paired markdown (CAP-250/251/259).
- Commit to extracted blocks and support compact membership citations (CAP-252/262).
- Sign selected access-wall failures rather than inventing content (CAP-264/269).
- Package receipt plus checkable cargo for offline handoff (CAP-265).
- Reuse one signer for playbooks, manifests, and watch history (CAP-257/289).

## User-visible entrypoints

| Producer/surface | Receipt behavior | Evidence |
|---|---|---|
| `occam_transcode` | Positive/selected negative; optional capsule/time anchor | CAP-278/279 |
| `occam_digest` | Reduced positive per item; no block leaves/time anchor | CAP-457 |
| `occam_claim_check` | Positive/negative; leaf completeness; proofs | CAP-697/700 |
| `occam_dataset_export` | Per-row receipt plus manifest | CAP-775/776 |
| `occam_crosscheck` | Per-vantage receipts; aggregate unsigned | CAP-278; TRUST-MODEL C10 |
| `occam_watch` | Detached-signed history entries when receipts enabled | CAP-284 |
| `occam_playbook_save` | Always detached-signs recipe body; outside receipt policy | CAP-281; EF-005 |
| CLI `keys export` | Exports public key, but mints if store empty | CAP-275; TRUST-MODEL A4 |

`occam_extract_knowledge.receipt` is not part of this family; it is telemetry (CAP-287/EF-006).

## Core behavior

1. Host DI always loads or creates `signing-key.pem` (EF-044).
2. Success computes SHA-256 over compiled markdown unconditionally (CAP-251; TRUST-MODEL C2).
3. If blocks exist, ordered leaves hash `text + NUL + source_selector` and form a duplicate-last Merkle tree (CAP-252).
4. Build envelope fields: URL/final URL/backend/time/toolchain/content/failure/playbook/tokens/confidence/completeness/key/alg (CAP-250/278).
5. Canonicalize in fixed field order with `sig` excluded and sign P1363 ECDSA P-256/SHA-256 (CAP-251/254).
6. Optionally request a TSA token over SHA-256(signature bytes) and/or encode a capsule (CAP-265/267).
7. Return signed envelope plus unsigned leaves/anchor/capsule wrapper as applicable.

## Advanced behavior

| Primitive | What it proves | What it does not prove | Evidence |
|---|---|---|---|
| Content hash | Byte equality of compiled markdown | URL/origin/time/truth | TRUST-MODEL C2 |
| Envelope signature | Holder of supplied key signed canonical fields | Key owner identity or assertion truth | C3/C4 |
| Merkle proof | `(text,selector)` leaf belongs under committed root | Text was on origin page or true | C5/C7 |
| Capsule | Nested envelope plus cargo can be checked offline | Wrapper/recipe are signed | C6 |
| Negative receipt | Host asserted one fetch hit a selected wall/status | Universal unavailability | §4 C3/C4 |
| Time anchor | Token binds signature imprint | Trusted TSA identity unless chain checked externally | C3/C9 |

## Automatic / silent behavior

- Key is minted at host startup even if `OCCAM_RECEIPTS=off` (EF-044; AUTO #1).
- Eligible receipts are signed by default; `off|0|false` disables selected producers (CAP-280).
- Playbook saves still sign and key remains on disk when receipts are off (EF-005/044).
- Managed provider content can be signed without the receipt identifying provider trust beyond backend string (EF-003).
- Cache can replay an old signed envelope; `cached`/age are outside signature (ART-035; TRUST-MODEL D8).
- TSA failure is swallowed and indistinguishable from anchoring disabled (CAP-267; FAILURE-BEHAVIOR-MAP:19).
- Merkle leaves/proofs may still be emitted unsigned when receipts are off (CAP-699).

## Parameters

This family is mostly embedded in producer parameters:

| Parameter | Surface/default | Effect | Evidence |
|---|---|---|---|
| `emit_capsule` | transcode, false | Adds `occam://capsule` wrapper | CAP-265 |
| `json_blocks` | transcode, false | Enables public leaves/root and later proof workflows | CAP-252/278 |
| receipt policy | no per-call switch | Global env controls most signatures | CAP-280 |
| `cache_ttl_s` | transcode, off | May persist/replay full signed envelope | ART-035 |

Digest, claim, dataset, watch, and playbook producers choose reduced/hardcoded receipt forms internally (CAP-457/697/775/281).

## Configuration

| Variable | Default/effect | Evidence |
|---|---|---|
| `OCCAM_KEYS_ROOT` | `~/.occam/keys`; key location | CAP-255 |
| `OCCAM_RECEIPTS` | enabled unless `off|0|false`; incomplete switch | CAP-280; EF-005/044 |
| `OCCAM_TIME_ANCHOR` | off; must pair with TSA URL | CAP-267 |
| `OCCAM_TSA_URL` | unset; operator-selected RFC3161 endpoint | CAP-267 |
| `OCCAM_TSA_TIMEOUT_MS` | 3000, clamp 500–15000 | CAP-267 |

Consensus duplicates receipt-policy parsing rather than calling `ReceiptsPolicy` (CAP-280).

## Backends

Receipts are built after producer acquisition/materialization. They do not independently fetch except the optional TSA call. Any HTTP/browser/managed content can be signed because the signer trusts the host outcome (CAP-278; EF-003).

## Sessions / state

ART-034 private key persists unencrypted at `signing-key.pem` (CAP-255; ST-15). Receipts are response artifacts unless callers save them or cache persists the full envelope (ART-007/008/035).

No rotation, revocation, expiry, key registry, or automatic deletion exists (CAP-255/288).

## Network behavior

Signing and verification are offline. Time anchoring makes one guarded TSA POST and fails open (CAP-267). Acquisition network behavior belongs to producer tools and is not proven by the receipt (TRUST-MODEL C1).

## Artifacts produced

- ART-007 positive Receipt v1.
- ART-008 negative receipt.
- ART-006 capsule; wrapper unsigned, inner envelope signed.
- ART-009 time-anchor sidecar.
- ART-024 content hash/materialization identity.
- ART-034 host key.
- Related detached signatures on ART-015, ART-022, ART-025 (ARTIFACT-ONTOLOGY.md:96-105).

## Trust / provenance properties

Exact licensed claim: “the holder of the private key corresponding to this supplied public key asserted these canonical field values, and the bytes have not changed” (TRUST-MODEL §6).

Not proven: signer identity, origin delivery, fetch occurrence, accuracy, truth, context, freshness, honest clock, or uncompromised host (TRUST-MODEL §4/§10/§13).

The capsule wrapper, blockLeaves, timeAnchor sidecar, and verifyRecipe are unsigned cargo; they gain assurance only through re-hashing against signed fields (TRUST-MODEL C6).

Duplicate-last Merkle construction means `[…,X]` and `[…,X,X]` can reconstruct the same root; leaf-count-derived values are not signed quantities (TRUST-MODEL EFC-P5-05-3).

## Failure / fallback behavior

- Selected negative receipt allow-list: challenge, requires_login, status 401/403/404/410; `"paywall"` branch is unreachable (CAP-264/279; EF-008).
- Timeout/network/workers failures do not receive negative receipts (CAP-264).
- No signer / receipts off yields unsigned hash/Merkle telemetry where producer supports it (CAP-280/699).
- TSA failures return no anchor without warning (CAP-267).
- Cache replay returns original receipt timestamp (TRUST-MODEL §4).

`ok:false` means content is unknown even if a negative receipt is valid (TRUST-MODEL §9.5).

## Platform differences

Private key is unencrypted PKCS8. Windows permission hardening is a no-op; POSIX attempts `0600` but swallows failure (CAP-255; `ReceiptSigner.cs:84-99`; TRUST-MODEL §10.2).

P1363 signatures and canonical bytes are designed for cross-platform stability (CAP-251/254).

## Composition with other capabilities

- Verification consumes receipts, capsules, leaves, citations, histories, and manifests.
- Claim/attest consume Merkle commitments but semantic verdicts remain separate.
- Dataset manifests and watch histories use detached signatures under the same key.
- Playbook signatures share key but exclude provenance metadata.
- Response cache may persist/replay receipts.

## Known limitations

- One self-signed local key for multiple purposes (CAP-289).
- No PKI/key distribution/rotation/revocation/expiry (CAP-255/288).
- No origin-authenticated transcript.
- Timestamps are local-clock assertions unless optional partial TSA anchor.
- Time-anchor chain is not validated to a root (CAP-261).
- Capsule wrapper is unsigned.
- Duplicate-tail Merkle ambiguity.
- Incomplete global receipt switch (EF-005/044).
- `occam_extract_knowledge` telemetry naming collision (EF-006).

## Engineering findings

- EF-001/045: cache key gaps and fragment collision can replay mismatched annotations/URL fragments.
- EF-003: managed content can be signed through unguarded provider client.
- EF-005/044: signing policy/key-mint incoherence.
- EF-006: fake receipt field.
- EF-008: unreachable paywall negative branch.
- EF-046: browser bypassCSP/playbook evaluation can mutate signed DOM.
- EFC-P5-05-3: duplicate-tail Merkle structural ambiguity.

## Code evidence

- `src/FFOccamMcp.Core/Receipts/ReceiptModels.cs:12-53`
- `src/FFOccamMcp.Core/Receipts/ReceiptCanonicalizer.cs`
- `src/FFOccamMcp.Core/Receipts/MerkleTree.cs:18-169`
- `src/FFOccamMcp.Core/Receipts/ReceiptSigner.cs:26-99`
- `src/FFOccamMcp.Core/Receipts/CapsuleCodec.cs`
- `src/FFOccamMcp.Core/Receipts/TimeAnchorService.cs`
- `src/FFOccamMcp.Core/Tools/OccamTranscodeModels.cs:347-454`
- CAP-090–093, CAP-250–291; ART-006–009/024/034; TRUST-MODEL.

## Public-doc relevance

Critical. Use binding TRUST-MODEL vocabulary. Forbidden: “verified provenance,” “proves the page said this,” “tamper-proof,” unqualified “third-party verifiable,” “signed by Occam,” or “timestamp proves fetch time” (TRUST-MODEL §13).

## Handbook relevance

Provide a primitive-by-primitive matrix of what is and is not proven, key handling, producer differences, negative receipt meaning, capsule anatomy, receipts-off degradation, and safe language for audit reports.
