# Playbook authoring

**Slug:** `playbook-authoring` · **Product system:** PS-5 Playbooks · **CAPs:** 18 · **Public relevance:** HIGH.

## What it is

`occam_playbook_save` validates a draft's minimal schema/hygiene, optionally runs a live extraction quality gate, appends bounded lessons, signs the recipe body, and writes it into the local playbook tier (CAP-560–577).

It is local authoring, not community publishing or third-party certification.

## Why it exists

- Persist agent/human-authored site recipes safely under a confined local root (CAP-567).
- Test an unsaved draft against a URL before writing (CAP-562/568/569).
- Keep a small lesson journal across iterations (CAP-565/566).
- Detect later body tampering against the same host's local key (CAP-571/572; conservative trust scope).

## User-visible entrypoints

MCP `occam_playbook_save` is the write surface and is full-profile-only by profile policy (`OccamToolProfile.cs`; CAP-547 family). A common workflow is heal → draft → lint → save → resolve/transcode.

No MCP authoring call writes community or seed tiers (CAP-567).

## Core behavior

1. Validate absolute `url`, lesson bounds, forbidden-key hygiene, and minimal 1.x/id/hosts schema (CAP-560/561/565).
2. If `verify=true`, require `verify_url` host match, push draft in strict `PlaybookVerifyScope`, run one real pipeline extraction, and apply hard quality thresholds (CAP-562/564/568/569/570).
3. Reject before any write on verification failure (CAP-562).
4. Optionally append and rotate lessons (CAP-565/566).
5. Confine target to local root and prevent seed overwrite/path traversal (CAP-567).
6. Build a signed JSON document and pretty-print/write it (CAP-571/572/577).
7. Bust resolver/genome caches so the new file is immediately visible (CAP-575).
8. Return path, verify metrics, and signing key id (CAP-573).

## Advanced behavior

| Feature | Detail | Evidence |
|---|---|---|
| Draft verify scope | Temp JSON + inline raw JSON; strict selectors, warm-daemon compatible | `PlaybookVerifyScope.cs`; CAP-568 |
| Quality score | 30% retention, 25% inverse noise, 20% structure, 15% completeness, 10% fixed backend confidence | `QualityGate.cs`; CAP-569 |
| Hard thresholds | score ≥70 and noise ≤0.12; markdown ≥100 chars | CAP-562/569 |
| Host match | Exact or subdomain suffix after stripping `www.` | CAP-570 |
| Lesson rotation | Append then trim oldest to `MaxLessonsPerFile` | CAP-565 |
| Cache hot reload | Production call to `ClearCacheForTests()` | CAP-575 |

## Automatic / silent behavior

- Malformed JSON is classified as hygiene rejection because hygiene runs before schema parsing (CAP-561).
- `verify_url` defaults to `url` (CAP-564).
- Dry-run backend comes from draft preferred backend or `http_then_browser`; no caller backend parameter exists (CAP-562).
- Dry-run has no session profile, token budget, cache, or structured sidecars (CAP-562).
- Every successful save signs, including `verify=false`, regardless of `OCCAM_RECEIPTS` (CAP-571; EF-005).
- Host startup already auto-mints the same local key, even receipts off (EF-044).
- Pretty-print failure silently falls back to raw signed JSON (CAP-577).

## Parameters

| Name | Default | Effect | Evidence |
|---|---|---|---|
| `url` | required | Host/file context and default verification target | CAP-560 |
| `playbook_json` | required | Draft recipe; hygiene/schema input | CAP-561 |
| `verify` | `true` | Live strict quality gate; `false` writes without gate | CAP-562/563 |
| `verify_url` | `url` | Independent same-host/subdomain test URL | CAP-564/570 |
| `lesson_note` | null; 1–500 chars | Append lesson | CAP-565 |
| `failure_reason` | null | Lesson metadata, not structurally secret-filtered | CAP-566 |
| `host_id` | null | Lesson metadata, convention only | CAP-566 |

No session, backend, max_tokens, cache, publication, key choice, or signing opt-out parameter exists.

## Configuration

`OCCAM_PLAYBOOKS_LOCAL_ROOT` selects the write root (CAP-567). `OCCAM_KEYS_ROOT` selects the auto-minted signing key (CAP-571; ART-034). Pipeline/acquisition env may affect `verify=true`.

`OCCAM_RECEIPTS` does not disable playbook signing (EF-005); do not document it as a master switch.

## Backends

`verify=true` uses `TranscodePipeline` with draft preferred backend or cascade; corrected router behavior applies, including terminal 404/410/public-reference stops and managed-success-only fallback (EF-056).

`verify=false` performs no network/backend call. Save itself is filesystem/signing logic.

## Sessions / state

Writes persistent ART-015 under the local tier (ST-07). Uses ART-034 private key. Draft verify creates a best-effort-deleted temp file (CAP-568).

No session_profile is accepted, so authenticated pages cannot be verified using stored login state (CAP-562 report cross-cutting finding).

## Network behavior

Only `verify=true` fetches `verify_url`, once through pipeline. No save-level retry. Managed provider may be reached transitively if configured and router gates allow; cache is bypassed because the service calls pipeline directly (CAP-562).

## Artifacts produced

- ART-015 persistent local playbook JSON with top-level `provenance` block (`ARTIFACT-ONTOLOGY.md:83`).
- Optional embedded lesson entries (CAP-565).
- Ephemeral verification metrics in response.
- Temp draft overlay file during verify (CAP-568).

## Trust / provenance properties

The recipe body is hashed with the entire top-level `provenance` excluded, and the hash string is signed with the local ECDSA P-256 key (`PlaybookSignature.cs:36,45-46`; TRUST-MODEL X1).

Therefore `provenance.verify.score`, `passesGate`, `noiseLeakage`, `keyId`, `alg`, and `signedAt` are **not signed**, contrary to CAP-574 and older save/trust prose. They can be edited without invalidating body verification (TRUST-MODEL §12 X1/X2).

The signature proves only that the holder of the local self-signed key asserted the body. It does not identify an author, establish recipe quality, prove the verify run occurred, or create third-party provenance (TRUST-MODEL §1/§13).

## Failure / fallback behavior

| Code | Trigger | Evidence |
|---|---|---|
| `playbook_schema_invalid` | Missing/invalid required structure, lesson bounds, host mismatch | CAP-561/564/565 |
| `playbook_save_rejected` | Forbidden key, path escape, seed overwrite | CAP-561/567 |
| `invalid_url` | Bad URL/verify URL | CAP-560/564 |
| `playbook_verify_failed` | Fetch failure, short body, generic gate failure | CAP-562 |
| `playbook_verify_low_score` | Score <70 | CAP-569 |
| `playbook_verify_high_noise` | Noise >0.12 | CAP-569 |

Verification failure writes nothing. `verify=false` is the explicit fallback and still signs/persists (CAP-563/571).

## Platform differences

Path prefix confinement uses ordinal-ignore-case in save code (CAP-567). Signing-key hardening is no-op on Windows and best-effort `0600` on POSIX (`ReceiptSigner.cs:84-99`; TRUST-MODEL §10.2).

## Composition with other capabilities

- Usually consumes evidence from `playbook-healing`.
- May consume advisory `playbook-validation`, but lint is neither required nor authoritative (CAP-759–762).
- Produces files consumed by `playbook-resolution` and auto overlays.
- Cache invalidation makes save immediately visible to resolve/transcode (CAP-575).
- Shares one key with receipts/dataset/watch, without purpose-separated keys (CAP-289).

## Known limitations

- Local tier only; no publishing.
- Verify cannot use sessions and uses heuristic thresholds (CAP-562/569).
- `verify=false` bypasses quality entirely (CAP-563).
- Quality/provenance metadata is unsigned (TRUST-MODEL X1).
- Secret-key hygiene checks property names, not arbitrary secret-looking string values (CAP-561/566).
- No key selection, rotation, revocation, expiry, or signing opt-out (CAP-255/571; EF-005/044).

## Engineering findings

- EF-005: save always signs despite `OCCAM_RECEIPTS=off`.
- EF-044: key is minted on host startup regardless of receipts.
- Binding correction: CAP-574 is false; verify metrics are excluded with provenance.
- EFC-P5-G2-1: editable unsigned keyId can alter inspection status.
- CAP-575: production hot reload uses test-named API.

## Code evidence

- `src/FFOccamMcp.Core/Tools/OccamPlaybookSaveTool.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookSaveService.cs:23-105`
- `src/FFOccamMcp.Core/Playbooks/PlaybookSaveVerifier.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookVerifyScope.cs`
- `src/FFOccamMcp.Core/Playbooks/QualityGate.cs`
- `src/FFOccamMcp.Core/Playbooks/PlaybookSignature.cs:36-198`
- CAP-560–577; ART-015/034; EF-005/044.

## Public-doc relevance

High. Document exact defaults/thresholds, verify=false behavior, local-only persistence, session absence, cache invalidation, unconditional signing, and the precise signature boundary. Remove any claim that quality scores or `passesGate` are signed.

## Handbook relevance

Use as the “persist a tested recipe” workflow. Include pre-save lint caveats, authenticated-page limitation, interpretation of verify metrics as unsigned telemetry, and safe handoff to resolve/transcode after save.
