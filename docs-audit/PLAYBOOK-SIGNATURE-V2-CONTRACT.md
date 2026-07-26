# PLAYBOOK SIGNATURE V2 — PREIMAGE CONTRACT

**Status:** Phase 6.5 (OD-4) · authoritative for implementation · 2026-07-26  
**Fundamental rule (unchanged):** a v2 signature proves **integrity of the signed fields relative to the signing key**. It does NOT prove identity, origin authenticity, truth, factual accuracy of the recipe, or trusted wall-clock time. `signedAt` is a self-asserted, now tamper-evident, signer-clock claim — nothing more.

## 1. Why v2

v1 (`playbook-sig-v1`) signs only `utf8(contentHash)` where `contentHash` is a SHA-256 over the recipe body with the entire top-level `provenance` excluded. Everything inside `provenance` — `keyId`, `alg`, `signedAt`, `verify.score`, `verify.passesGate`, `verify.noiseLeakage` — is unsigned and freely editable without invalidating the signature (EF-058). v2 brings the trust-relevant provenance fields under the signature with explicit versioning and domain separation.

## 2. Version marker

- Location: `provenance.sigScheme` (string).
- Values: `"playbook-sig-v1"` (legacy; may also be **absent** → treated as v1) · `"playbook-sig-v2"`.
- A verifier dispatches by this marker. A v1 artifact MUST NOT be verified under v2 rules and vice-versa. Feeding a v1 artifact to the v2 verifier yields `unsupported_version` (or, at the dispatcher, is routed to v1). Any unknown value → `unsupported_version`.

## 3. Signed fields (v2 preimage)

The signature covers a canonical serialization of this **assertion object** (field values taken from the emitted `provenance`, except `signature` which is never part of its own preimage):

| Field | Source | Why signed |
|-------|--------|-----------|
| `v` | integer `2` | Binds the scheme version into the preimage (defence against downgrade) |
| `alg` | `"ecdsa-p256-sha256"` | Binds algorithm |
| `keyId` | signer key id (`k1:…`) | Author claim cannot be relabelled without breaking the signature |
| `contentHash` | `sha256:` over recipe body with top-level `provenance` excluded (identical computation to v1 `ContentHash`) | Binds the recipe body |
| `signedAt` | RFC3339 `yyyy-MM-ddTHH:mm:ssZ` UTC | Self-asserted signing instant, now tamper-evident |
| `verify` | object `{ score?, passesGate, noiseLeakage? }` | The gate snapshot the product echoes; must not be forgeable |

`verify` sub-fields:
- `score` (integer, optional — present only when known)
- `passesGate` (boolean, always present)
- `noiseLeakage` (number, optional — present only when known)

Optional fields are **omitted** from both the emitted JSON and the preimage when absent (never emitted as `null`). Presence/absence is therefore part of the signed shape: adding or removing an optional field after signing breaks verification.

## 4. Explicitly unsigned fields

- `provenance.signature` — the detached signature itself.
- `provenance.sigScheme` — the dispatch marker (its meaning is fixed by §2; it selects the verifier, and `v` inside the signed preimage independently binds the version so a downgrade attack cannot both flip `sigScheme` and keep a valid preimage).
- Any purely local/editorial field a future version may add outside the assertion object. None exist today.
- The recipe body is not *directly* in the preimage but is bound transitively via `contentHash`.

## 5. Canonicalization (deterministic)

1. Compute `contentHash = ContentHash(playbookJson)` — existing recursive canonical writer: objects with keys sorted `Ordinal`, arrays in order, top-level `provenance` excluded. Unchanged from v1.
2. Build the assertion object with the fields in §3.
3. Serialize the assertion with the **same** canonical writer (`WriteCanonical`, no top-level exclusion): keys sorted `Ordinal`, recursive, numbers/strings/booleans written via `JsonElement.WriteTo`. This yields deterministic bytes independent of field insertion order.
4. Prepend the domain-separation prefix (see §6) to those bytes.
5. `signature = base64url(ECDSA_P256_SHA256_sign(privateKey, prefix ‖ canonicalAssertionBytes))` using the IEEE P1363 fixed-size r‖s encoding (matches `ReceiptSigner.SignDetached`).

Verification recomputes steps 1–4 from the emitted `provenance` and checks the signature over `prefix ‖ canonicalAssertionBytes`, then independently confirms `provenance.contentHash == recomputed contentHash` (body integrity) before returning `verified`.

## 6. Domain separation

Preimage prefix bytes: the ASCII string `occam-playbook-sig-v2` followed by a single `0x0A` (newline) separator:

```
occam-playbook-sig-v2\n<canonical-assertion-json-bytes>
```

This prevents a v2 assertion preimage from ever colliding with:
- a v1 preimage (`utf8(contentHash)`, no prefix),
- a receipt/watch/dataset canonical preimage (different structure, no prefix),
- a raw recipe body.

## 7. Backward compatibility

- v1 artifacts (missing `sigScheme` or `sigScheme=="playbook-sig-v1"`): verified by the unchanged v1 path (signature over `utf8(contentHash)`, provenance excluded from the body hash). Still `verified`/`invalid`/`wrong_key` as before.
- New saves emit v2 (`sigScheme=="playbook-sig-v2"`, signature over the §5 preimage). The emitted `provenance` retains the same field names as v1 (`keyId`, `alg`, `contentHash`, `signature`, `signedAt`, `verify{…}`) plus `sigScheme`, so existing readers that only display fields keep working; only the signature semantics strengthened.
- No silent reinterpretation: the verifier requires the marker to match the rules it applies.

## 8. Verification verdicts

`Inspect` returns `{ present, status, sigVersion, keyId, score, passesGate }`. `status` ∈:

| Verdict | Meaning |
|---------|---------|
| `unsigned` | No `provenance.signature` (or unparseable JSON). |
| `verified` | Signature valid under the supplied key AND (v2) all signed fields intact AND body hash matches AND claimed keyId == local key id. |
| `key_mismatch` | Signature valid under the supplied key but claimed keyId differs from local (v2: unreachable in practice because keyId is signed; retained for defence). |
| `wrong_key` | Signature does NOT verify under the supplied key and the claimed keyId differs from local key id → likely a foreign key, not tamper. |
| `invalid` | Signature does NOT verify under the supplied key and the claimed keyId equals local (tamper of a signed field or body). |
| `unsupported_version` | `sigScheme` present but not a recognized scheme. |

`sigVersion` ∈ `{ 1, 2, null }` (null when unsigned). Mutating any signed field (keyId, signedAt, verify.score, verify.passesGate, verify.noiseLeakage) or the recipe body on a v2 artifact MUST yield `invalid` (same key) — never `verified`, never a softened class.

## 9. Test matrix (Phase 6.5C)

| # | Input | Expected |
|---|-------|----------|
| T1 | v1 artifact, correct key | `verified`, `sigVersion=1` |
| T2 | v2 artifact, correct key | `verified`, `sigVersion=2` |
| T3 | v2 artifact, `verify.score` mutated | `invalid` |
| T4 | v2 artifact, `signedAt` mutated | `invalid` |
| T5 | v2 artifact, `keyId` mutated | `invalid` (signed) — not `wrong_key`/`key_mismatch` |
| T6 | v2 artifact, recipe body mutated | `invalid` |
| T7 | v2 artifact verified against a different (wrong) public key | `wrong_key` |
| T8 | malformed base64 signature | `invalid` |
| T9 | `sigScheme` set to `playbook-sig-v3` | `unsupported_version` |
| T10 | v1 artifact still verifies via v1 path (`Verify` back-compat) | true |
| T11 | v2 `Verify(json, pubkey)` boolean back-compat helper returns true for intact v2 | true |
| T12 | Unsigned optional field (none defined today) — n/a | documented n/a |

Golden fixtures: store one canonical v1 and one canonical v2 signed playbook so future canonicalization changes are caught.

## 10. Non-goals

- No identity, PKI, registry, or origin binding.
- No trusted timestamp (TSA remains a separate, unchained, opt-in receipt concern).
- v2 does not elevate `verify.score` from a heuristic to a proof; it only makes the recorded snapshot tamper-evident.
