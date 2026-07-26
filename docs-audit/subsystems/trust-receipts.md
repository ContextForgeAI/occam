# Subsystem audit: Trust / Receipts / Signatures / Merkle / Capsules / Offline Verify

Wave 1 subagent **S19** — FFOccamMCP capability audit.
**CAP ID range: CAP-250 – CAP-299** (exclusive to this subsystem/report).
**Source of truth: executable code only** (paths + line numbers cited below). Docs (`docs/receipts.md`,
`MCP_API_SPEC.md`, `AGENTS.md`) were NOT used as evidence and may drift from what is described here —
where a discrepancy was noticed it is flagged explicitly as "doc-vs-code gap", not corrected.

Repo: `c:\PROJECTS\FFOccamMCP`. All paths below are relative to repo root unless stated otherwise.

---

## 0. Executive summary

FF-Occam ships a self-contained, single-key, ECDSA-P256-based "Receipt v1" trust layer. One local key
(generated on first use, no operator PKI) signs:

1. **Extraction receipts** (positive: `contentHash` + optional `blockMerkleRoot`; negative: signed
   `ok:false` for provable unavailability) — attached to `occam_transcode`, `occam_digest`,
   `occam_claim_check`, `occam_dataset_export`, and the opt-in `occam_crosscheck`.
2. **Merkle block-citation proofs** (SI-02/SI-02b) — verifiable membership of one text block in a
   signed extraction, without the page or the other blocks.
3. **Proof-carrying capsules** (`occam://capsule/...`) — an offline, agent-to-agent hand-off object
   bundling receipt + content + leaves + a self-describing verify recipe.
4. **Signed playbooks** (SI-08 local foundation) — a saved playbook JSON gets a `provenance` block
   (keyId/signature/verify-gate score), inspected (not blindly trusted) on resolve.
5. **Signed dataset manifests** (SI-17) — per-row extraction receipts + one detached manifest
   signature over a Merkle root of all rows.
6. **Signed watch-history hash chains** (SI-05) — tamper-evident append-only log for `occam_watch`
   (itself opt-in), each entry both hash-chained to its predecessor and individually signed.
7. **Optional RFC3161 time anchoring** (SI-15) — an independent, fail-open, opt-in third-party
   timestamp over a receipt's signature bytes.

The consumer side is `occam_verify` (5 modes: offline/live/prove/citation/history) — an MCP tool — and
a fully independent, network-free `occam verify` / `occam keys export` **CLI** that reimplements
nothing (same primitives) so a third party can check a receipt without running the MCP host at all.

**Notable gap found:** `occam_extract_knowledge`'s response has a field literally called `Receipt`
(`OccamExtractKnowledgeReceiptInfo`), but it is **not** a signed `ReceiptEnvelope` — it is just
`{confidence, elapsedMs}` telemetry. This is the only core tool whose "receipt" is not cryptographic.
See CAP-287.

**Notable dead code found:** `MaterializedProvenanceResolver` (Merkle-membership tracer for the
Knowledge canonical model) is defined but never called from any tool or service in `src/`. See
CAP-296. Also the `"paywall"` failure-code branch in negative-receipt gating is unreachable — no
post-processor in the codebase ever emits `FailureCode = "paywall"`. See CAP-269.

---

## 1. Data model & canonicalization

### CAP-250 — `ReceiptEnvelope` (Receipt v1 schema)
**File:** `src/FFOccamMcp.Core/Receipts/ReceiptModels.cs:12-53`
**Classification:** Public (data contract; every signed receipt on the wire is this shape).
One record, two variants sharing one schema via `Kind`:
- `"extraction"` (positive) — carries `ContentHash`, optional `BlockMerkleRoot`, `Tokens`, `LeafSetComplete`.
- `"negative"` — carries `FailureCode`, `StatusCode`, no content fields.
Common fields: `V` (=1), `Url`, `FinalUrl`, `Backend`, `Ts`, `Toolchain`, optional `Playbook{Id,Version}`,
signed advisory `Confidence`, and the signature block `KeyId`/`Alg`/`Sig`.
`Sig` is attached post-signing and is **excluded** from the canonical signed bytes (CAP-251).
`LeafSetComplete` (CAP-270) is a later addition; when `null` it serializes to bytes identical to a
pre-field receipt (explicit backward-compat design, verified in golden test — see §7).

### CAP-251 — `ReceiptCanonicalizer` (hand-written canonical bytes)
**File:** `src/FFOccamMcp.Core/Receipts/ReceiptCanonicalizer.cs`
**Classification:** Internal (implementation detail; the wire *effect* — stable signable bytes — is
what the public contract depends on).
Deliberately **not** `System.Text.Json` reflection/sorted-keys serialization — a fixed, hand-coded
field order via `Utf8JsonWriter`, chosen so the scheme is immune to serializer/property-order drift
and stays Native-AOT safe. `sig` is always excluded. Null optional fields are omitted (not written as
`null`), which is what makes `LeafSetComplete: null` byte-identical to older receipts.
`ContentHash(content)` = `"sha256:" + hex(SHA256(utf8(content)))` — the single content-hash codec
reused by `MerkleTree.HashPrefix`, `WatchService`, and the `if_none_match`/`ContentHashToken` ETag path
(cross-checked in the gate — see `ReceiptUnitTests.cs:345-353`).
**Security note:** the ENTIRE signature scheme's integrity rests on this class's byte-stability. A
golden vector test freezes the exact output (§7) — this is the correct mitigation for the risk, and it
exists.

### CAP-252 — `MerkleTree` (ordered SHA-256 Merkle tree over extraction blocks)
**File:** `src/FFOccamMcp.Core/Receipts/MerkleTree.cs`
**Classification:** Internal primitive, exposed indirectly via `blockMerkleRoot` / `blockLeaves` on
several public tool responses.
- Leaf preimage = `utf8(text + '\0' + (source_selector ?? ""))` — binds a block's text AND its CSS
  location, so a citation can prove *where* text was, not just that some text existed (design intent
  stated in the doc-comment, D2).
- Odd levels duplicate the last node (Bitcoin-style) — standard, but be aware this means a Merkle tree
  with a single element at an odd level pairs with itself; this is a well-known (non-fatal) property
  of this specific tree construction, not a bug, but worth flagging for anyone reusing `HashPair`
  externally (self-pairing collision is not a security concern here because the leaf preimages already
  bind position implicitly via tree structure, but it does mean two *different* odd-sized trees whose
  duplicated tail node happens to collide are not distinguishable from the pair alone — extremely low
  practical risk with SHA-256).
- `Root`, `RootFromLeafHashes`, `LeafHashesHex`, `Proof`, `VerifyProof` are all pure, deterministic,
  and exception-safe (`VerifyProof`/proof parsing catch `FormatException` and return `false`, never
  throw) — good hardening for untrusted-input paths (citation mode takes attacker-controlled JSON).

### CAP-253 — `Base64Url`
**File:** `src/FFOccamMcp.Core/Receipts/Base64Url.cs`
**Classification:** Internal. RFC4648 §5 base64url, no padding. Used for `sig` and for the capsule
wire body. `Decode` re-pads defensively; no length/format validation beyond what `Convert.FromBase64String`
throws (callers all wrap in try/catch for `FormatException`).

---

## 2. Signing & key management

### CAP-254 — `ReceiptSigner` (local ECDSA P-256 signer)
**File:** `src/FFOccamMcp.Core/Receipts/ReceiptSigner.cs`
**Classification:** Advanced/internal (drives every public signed-receipt capability; the class itself
is not directly MCP-exposed, but `KeyId` and the exported public key ARE surfaced — see CAP-259, CAP-286).
- Algorithm: `ECDsa` NIST P-256, `alg` string in the envelope is `"ecdsa-p256-sha256"`
  (`ReceiptEnvelope.AlgEcdsaP256`). Doc-comment explicitly reserves the `alg` field so **Ed25519 could
  be added later without a wire-format break** — currently only P-256 is implemented.
- Signature encoding: **IEEE P1363 fixed-size r‖s** via `ECDsa.SignData(...)`'s default output — chosen
  explicitly over DER/ASN.1 for cross-platform byte-stability (comment at line 60-61). This is a
  correct and deliberate choice for a format that must be byte-identical everywhere.
- `KeyId` = `"k1:" + hex(SHA256(SPKI))[..16]` (CAP-255) — a short, deterministic fingerprint of the
  **public** key, not a random/opaque id. Anyone with the public key can recompute the same `KeyId`
  their peer would report — no registry needed for basic identity, but see CAP-291 for the trust gap
  this does NOT close.
- `Toolchain` = `"ff-occam/" + <assembly informational version, build metadata stripped>` — static per
  process, embedded (unsigned-but-covered) in every receipt for provenance/debugging.

### CAP-255 — Key storage / lifecycle (`LoadOrCreate`)
**File:** `ReceiptSigner.cs:26-45`
**Classification:** Advanced (operator-relevant; security-sensitive).
- Default root: `~/.occam/keys/` (`Environment.SpecialFolder.UserProfile` + `.occam/keys`), overridable
  via **`OCCAM_KEYS_ROOT`**.
- File: `signing-key.pem`, PKCS8 **unencrypted** private key (`ExportPkcs8PrivateKey()` → PEM, no
  passphrase, no OS keychain/DPAPI/keyring integration).
- Generated on first use if absent; loaded verbatim if present (so an operator CAN drop in a
  pre-provisioned key by placing it at that path — not documented as a supported workflow in the code
  itself, but nothing prevents it).
- **Security implication (Windows):** `TryHardenPermissions` (lines 84-99) is a no-op on Windows
  (`return;` with comment "NTFS ACLs inherit; POSIX 0600 not applicable") — i.e., on Windows the key
  file's permissions are whatever the parent directory / user profile ACLs already grant, with **no
  additional hardening applied by the code**. On this repo's stated dev platform (win32), the signing
  key sits with default user-profile ACLs.
- **Security implication (POSIX):** best-effort `chmod 600` (`UnixFileMode.UserRead | UserWrite`)
  wrapped in a bare `catch { }` — if hardening silently fails (e.g., unsupported filesystem), the key
  is still written and used with no warning surfaced to the operator.
- No key rotation mechanism, no expiry, no revocation list anywhere in this subsystem. A compromised
  key has no built-in remediation path other than deleting the PEM (which then invalidates ALL
  previously-issued receipts' verifiability against the *new* key, and old receipts remain verifiable
  forever against the *old*, now-untrusted, public key if an attacker retains it).

### CAP-256 — `ReceiptSigner.CreateEphemeral`
**File:** `ReceiptSigner.cs:48`
**Classification:** Internal (test/gate-only). In-memory key, no disk I/O. Used pervasively by
`benchmarks/l0-gate/*UnitTests.cs`. Not reachable from any MCP tool or CLI verb.

### CAP-257 — `ReceiptSigner.SignDetached` / generic detached-signature primitive
**File:** `ReceiptSigner.cs:71-72`, verified via `ReceiptVerifier.VerifyDetached` (`ReceiptVerifier.cs:26-38`)
**Classification:** Internal primitive, reused by THREE other subsystems (fan-out, not receipts
themselves):
1. **Playbook signing** — `PlaybookSignature.BuildSignedJson` (CAP-285).
2. **Dataset manifest signature** — `DatasetManifestBuilder` (CAP-283) via `DatasetExportService.cs:53`.
3. **Watch-history entry signatures** — `WatchHistoryChain.Append` (CAP-277).
**Design note:** all four use-cases (receipt envelope, playbook, manifest, history entry) share the
**same physical ECDSA key** (CAP-291) but each defines its OWN canonical-bytes function
(`ReceiptCanonicalizer`, `PlaybookSignature.ContentHash`+manual JSON, `DatasetManifestBuilder.CanonicalBytes`,
`WatchHistoryCanonicalizer.CanonicalBytes`) — i.e., there is no single canonicalization abstraction
shared across them; each was hand-rolled independently but following the same discipline (fixed field
order, `sig`/signature excluded, hand-coded `Utf8JsonWriter`). This is consistent in *style* but is
4 independent hand-written serializers to audit for canonical-form bugs, not 1.

---

## 3. Verification (offline half)

### CAP-258 — `ReceiptVerification` result type + verdict vocabulary
**File:** `src/FFOccamMcp.Core/Receipts/ReceiptVerifier.cs:6-15`
**Classification:** Public (verdict strings appear verbatim in `occam_verify` / CLI JSON output).
Verdicts: `verified`, `signature_invalid`, `content_mismatch`, `invalid_receipt` (plus tool-level
extensions added by `OccamVerifyTool`: `refetch_failed`, `drifted`, `citation_verified`,
`citation_invalid`, `history_verified`, `history_invalid`).

### CAP-259 — `ReceiptVerifier.VerifyOffline`
**File:** `ReceiptVerifier.cs:40-80`
**Classification:** Public (core primitive behind `occam_verify` mode=offline and CLI `verify --mode receipt`).
Two independent checks: (1) ECDSA signature over `ReceiptCanonicalizer.CanonicalBytes(receipt)` against
a **caller-supplied** public key PEM; (2) if `markdown` is supplied AND the receipt has a
`contentHash`, recompute and compare. Exceptions from bad PEM / bad base64 (`FormatException`,
`CryptographicException`) are caught and mapped to `invalid_receipt`/`false` — no unhandled-exception
path for malformed attacker input.
**Trust boundary called out explicitly in the doc-comment (verbatim, accurate):** *"Trust of the KEY
itself (who owns k1:…) is out of scope for v1 — that is the registry PKI (SI-08)."* I.e., this
function proves "whoever holds the private key for THIS pem signed THIS envelope, unmodified" — it
does **not** prove that key belongs to the entity the caller thinks it does. The caller must have
obtained the correct public key out-of-band (TOFU model). See CAP-290/291.

### CAP-260 — `ReceiptVerifier.VerifyDetached`
**File:** `ReceiptVerifier.cs:26-38`
**Classification:** Internal primitive (generic ECDSA-over-bytes verify), reused by playbook/manifest/
history verification (CAP-257 fan-out).

### CAP-261 — `TimeAnchorVerifier` (RFC3161 consumer)
**File:** `src/FFOccamMcp.Core/Receipts/ReceiptTimeAnchor.cs:29-84`
**Classification:** Advanced. Verifies a base64 RFC3161 token binds to an expected imprint
(`SHA-256(signature bytes)`) and that the token's own internal signature validates
(`Rfc3161TimestampToken.VerifySignatureForHash`).
**Explicit, documented scope-cut (verbatim):** *"TSA trust (chain-to-root) is out of scope for v1 — we
return the signer subject and let the consumer decide."* This means **the TSA certificate chain is
never validated against any trust root** — `VerifyToken` returns whatever `signer.Subject` string the
token claims, unauthenticated against a CA bundle. A consumer that doesn't independently validate the
TSA cert chain is trusting an unauthenticated "I am freetsa.org" self-report. This is a real,
documented (not hidden) limitation — flagged here because it is easy for a downstream consumer to
assume "time anchor valid" implies "trusted timestamp authority," which it does not fully guarantee.

---

## 4. Trust primitives above the base receipt

### CAP-262 — Merkle membership proof (SI-02b): `MerkleTree.Proof` / `VerifyProof`
**File:** `MerkleTree.cs:109-169`
**Classification:** Advanced — surfaced via `occam_verify` mode=`prove`/`citation`, `occam_claim_check`
matches, `occam_attest` per-claim proof, CLI `verify --mode citation`.
Compact (≈log₂N) sibling-hash path lets a third party prove "this exact block existed under this
signed Merkle root" **without the page, the other blocks, or re-fetching** — a genuinely strong
provenance primitive when used correctly. Verified end-to-end in gate: `ReceiptUnitTests.cs:191-217`
(round-trip for every leaf, rejects wrong leaf, rejects tampered root).

### CAP-263 — `LeafSetComplete` / provable absence
**File:** `ReceiptModels.cs:42-47`, wired at `ClaimCheckService.cs:56-62`
**Classification:** Advanced, narrow reach. Only `ClaimCheckService` ever passes `leafSetComplete: true`
into `OccamTranscodeResponseBuilder.BuildReceipt` (grep-confirmed — no other call site sets it true).
Gate: `blocks.Count > 0 && !outcome.Truncated` (i.e., `occam_claim_check` never sets `max_tokens`/`fit`,
so its own block extraction is never pruned — a legitimate, narrow "completeness" claim). Consumed by
`OccamClaimCheckSuccessResponse.Proven`: `found:false` + `leafSetComplete:true` → `Proven:true` ("the
extracted content provably does NOT contain matching text", not just "we didn't find it"). This is a
carefully-scoped, honestly-gated feature — the completeness claim is tied 1:1 to the actual absence of
truncation in the one caller that emits it.
**`occam_transcode`/`occam_digest`/`occam_dataset_export`/`occam_crosscheck` themselves never set
`leafSetComplete`** (default `false` at `OccamTranscodeModels.cs:349`), even though several of those
also request `json_blocks` and could in principle be complete — this looks like an intentionally
conservative default (only claim completeness where it's been explicitly reasoned about) rather than
an oversight, but it does mean e.g. a `dataset_export` row with an untruncated block set still won't
carry a `leafSetComplete` flag a consumer could use for the same "provable absence" trick.

### CAP-264 — Negative receipts (SI-03): `OccamTranscodeResponseBuilder.BuildNegativeReceipt`
**File:** `src/FFOccamMcp.Core/Tools/OccamTranscodeModels.cs:419-454`
**Classification:** Public (core, attaches to `occam_transcode` failure responses and to
`occam_claim_check`/`occam_dataset_export`/`occam_crosscheck` failure paths).
Signs a claim of **provable unavailability** — i.e., "this page returned a challenge/login-wall/4xx",
which only an honest extractor can sign, as opposed to inventing content. Gate:
```
code is "captcha_or_challenge" or "requires_login" or "paywall" || statusCode is 401 or 403 or 404 or 410
```
**Finding — dead branch:** `"paywall"` is **never** emitted as a `FailureCode` by any post-processor in
the codebase (grep of `src/FFOccamMcp.Core/PostProcessors/*.cs` finds only `captcha_or_challenge`
(`ChallengePagePostProcessor.cs:36`) and `requires_login` (`RequiresLoginPostProcessor.cs:43`);
`"paywall"` only appears as a probe **risk tag** string in `HtmlProbeClassifier.cs:108,162`, which is a
different code path (`occam_probe`'s `risks[]`, not a transcode `failureCode`)). This `or "paywall"`
condition is therefore currently unreachable in this codebase — harmless (fails safe: no negative
receipt is signed for a case that never occurs), but worth a maintainer note since it implies an
intended-but-unshipped paywall failure code, or a rename that was missed in one of the two mirrored
gate conditions (this exact string also appears, identically unreachable, in
`ConsensusService.cs:100`).
Correctly returns `null` (no receipt) for transient errors (timeout/network/workers-unavailable) since
those are not a provable claim about the page — verified in gate (`ReceiptUnitTests.cs:175-176`).

### CAP-265 — `CapsuleCodec` / proof-carrying capsules (HARNESS-P0)
**File:** `src/FFOccamMcp.Core/Receipts/CapsuleCodec.cs`
**Classification:** Advanced/opt-in (`emit_capsule` param on `occam_transcode`; consumed by
`occam_verify` transparently for any input starting with the scheme).
Wire form: `occam://capsule/<base64url(json)>`. Bundles `{cap, kind, signed, content, blockLeaves,
timeAnchor, verifyRecipe}` — i.e., a **self-contained** artifact a receiving agent can verify with
**zero prior knowledge of Occam** (no docs, no separate page fetch): the `verifyRecipe` field embeds
the algorithm name, the Merkle leaf construction description, the key anchor (`keyId`), and a literal
runnable CLI one-liner (`"occam verify --receipt <capsule> --pubkey <pem>"`). This self-describing
design is a genuinely good agent-ergonomics feature — it directly targets the "stranger agent" use
case named in the doc-comment.
`TryParse` never throws (catches `FormatException`/`JsonException`) — safe against arbitrary/malformed
`occam://capsule/...` strings fed into `occam_verify`. Verified end-to-end including a real
transcode→emit→verify round trip with no page re-fetch (`CapsuleUnitTests.cs:78-93`).
**Note:** the packaging (base64url + JSON wrapper) is explicitly NOT part of the signed bytes — only
`Signed` (the `ReceiptEnvelope`) is covered by the ECDSA signature. This is correct (wrapping/unwrapping
can't retroactively change what was signed) but does mean the capsule's OTHER fields
(`content`/`blockLeaves`/`timeAnchor`) are only as trustworthy as they are *consistent with* the signed
envelope — which is exactly what `occam_verify offline` re-checks (`contentHash` match,
`blockMerkleRoot` reconstruction from `blockLeaves`), so the design is sound, just worth naming
explicitly: the capsule container itself is unsigned; only its cargo's summary (hashes/roots) is.

### CAP-266 — `ChunkStalenessEvaluator` (SI-12 per-chunk RAG expiry)
**File:** `src/FFOccamMcp.Core/Receipts/ChunkStaleness.cs`
**Classification:** Advanced, only reachable via `occam_verify mode=live` + `chunks` param.
Pure set-difference (`sourceChunkHashes` vs. live re-fetched leaf set) — reports exactly which chunk
hashes a caller's RAG store should invalidate rather than "the whole document might have changed."
Deterministic, no I/O of its own (depends on the caller already having done the live re-fetch via
`occam_verify`).

### CAP-267 — `ReceiptTimeAnchor` producer: `TimeAnchorService`
**File:** `src/FFOccamMcp.Core/Receipts/TimeAnchorService.cs`
**Classification:** Advanced/opt-in, off by default.
Gates: **`OCCAM_TIME_ANCHOR=1|true|on`** AND **`OCCAM_TSA_URL`** set (both required —
`IsEnabled()`/`TryAnchor` check both). Requests an RFC3161 token over `SHA-256(receipt signature
bytes)` from the operator-configured TSA.
**Security-positive details:**
- SSRF-guarded: `PrivacyClassifier.Classify(tsaUrl).IsPrivateHost` check refuses to ever POST to a
  private/internal host (`TimeAnchorService.cs:28-31`) — correct, since a malicious/misconfigured
  `OCCAM_TSA_URL` could otherwise be used to probe internal network hosts on every extraction.
- Also uses the shared `OutboundHttpGuard.ConnectAsync` SSRF-guarding `SocketsHttpHandler` via its
  named `HttpClient` ("receipts.timeAnchor", wired in `OccamServiceCollectionExtensions.cs:73-79`) —
  belt-and-suspenders against DNS-rebinding-style SSRF (guard applies at actual connect time, not just
  URL string inspection).
- Timeout bounded 500ms–15000ms via `OCCAM_TSA_TIMEOUT_MS` (default 3000ms) — short enough that an
  unreachable/slow TSA can't meaningfully stall the extraction path.
- Fail-open by design: **any** exception (network, malformed response, timeout, self-check failure) is
  swallowed by a bare `catch { return null; }` (`TimeAnchorService.cs:70-73`) — a broken TSA never
  blocks or degrades the underlying extraction; the receipt just ships without an anchor. This is the
  correct trade-off for an optional integrity **bonus**, explicitly documented as such.
- Self-checks its own request: only attaches a token if `VerifySignatureForHash` on the *response*
  passes locally first (`TimeAnchorService.cs:59-63`) — never blindly trusts what the TSA sends back.
`OCCAM_TSA_URL` itself is operator-controlled (env var, not a per-call MCP parameter) — so this cannot
be turned into a per-request arbitrary-URL SSRF vector by an MCP caller; only an operator with env
access chooses the TSA.

---

## 5. `occam_verify` MCP tool (consumer surface)

### CAP-268 — `OccamVerifyTool` — mode dispatch
**File:** `src/FFOccamMcp.Core/Tools/OccamVerifyTool.cs`
**Classification:** Public, core tool (`occam_verify` is in `OccamToolNames`, always-on unless
`OCCAM_PROFILE` narrows it).
5 modes, dispatched by a single string switch (`Verify(...)` method, lines 74-80):

#### CAP-269 — mode=`offline` (default)
Signature + optional `contentHash` check via `ReceiptVerifier.VerifyOffline`. Also transparently
accepts a full capsule (`occam://capsule/...`) as the `receipt` argument (auto-detected via
`CapsuleCodec.IsCapsule`) — the capsule's own bundled `content` supplies the markdown for the
content-hash check if the caller doesn't pass `markdown` separately (lines 56-68). Also surfaces
`TimeAnchor` verification if the parsed receipt carried one (lines 158-164).

#### CAP-270 — mode=`live`
Re-fetches `envelope.FinalUrl` via the **injected `TranscodePipeline`** (real extraction, real network
call — this is the one mode that is NOT purely offline despite the tool's overall framing) with
`backend_policy=http_then_browser` and `json_blocks` enabled only if the original receipt had a block
root (`OccamVerifyTool.cs:171-174`). Computes: whole-content drift (`contentMatch`), block-root
reconstruction match, granular `blocksTotal`/`blocksStillPresent`/`drift` (SI-02), and per-chunk
staleness (SI-12, CAP-266) against either the caller-supplied `chunks` param or the receipt's own
leaves. **Only runs the re-fetch if the offline signature check already passed** (`if (live &&
offline.SignatureValid)` at line 169) — correctly refuses to spend a live fetch verifying content
against an already-untrustworthy signature.

#### CAP-271 — mode=`prove` (SI-02b producer)
Builds a compact `{keyId, root, leafIndex, leaf, proof[]}` citation package for one block index,
requiring the caller to already hold `blockLeaves` (e.g. from a prior `json_blocks` transcode) and
first **verifying those leaves reconstruct the signed `blockMerkleRoot`** before emitting a proof
(`OccamVerifyTool.cs:248-251`) — prevents proving a leaf set the signer never actually attested to.

#### CAP-272 — mode=`citation` (SI-02b consumer)
Verifies someone else's `{block_text, block_selector, proof}` against the signed root — **no page, no
leaves array needed**, just the envelope + the proof. Correctly gates the verdict on BOTH the envelope
signature AND the Merkle membership (`!offline.SignatureValid ? signature_invalid : membershipOk ?
citation_verified : citation_invalid`, lines 286-288) — a forged/unsigned envelope can't be used to
"prove" a citation even if the Merkle math happens to check out.

#### CAP-273 — mode=`history` (SI-05 consumer)
Verifies a signed watch-history hash-chain (see CAP-277). Branches BEFORE the receipt parser (a
history array is not a valid `ReceiptEnvelope`), and accepts either a bare JSON array or `{history:
[...]}` (matching the shape `occam_watch`'s response actually returns).

### CAP-274 — `OccamVerifyReceiptInput` flexible parsing
**File:** `src/FFOccamMcp.Core/Tools/OccamVerifyModels.cs:84-87`, parse logic
`OccamVerifyTool.cs:296-319`
**Classification:** Internal (input ergonomics). Accepts either a full `{signed, blockLeaves,
timeAnchor}` wrapper (the shape every tool response actually emits) OR a bare `ReceiptEnvelope` —
falls back to bare-envelope parse only if the wrapper parse doesn't yield a `Signed` value. Malformed
JSON is caught (`JsonException`) and reported as `invalid_receipt`, never an unhandled exception.

---

## 6. CLI verify surface (fully independent of the MCP host)

### CAP-275 — `OccamCliVerbs.TryRun` dispatch + `occam keys export`
**File:** `src/FFOccamMcp.Core/Cli/OccamCliVerbs.cs:28-56, 208-215`
**Classification:** Public. Dispatched **before** any MCP transport/argument parsing in `Program.cs:12`
(`if (OccamCliVerbs.TryRun(args, out var verbExit)) { return verbExit; }`) — i.e., `occam verify` /
`occam keys export` never spin up a worker process, browser pool, or MCP JSON-RPC loop; they are pure,
fast, standalone verbs. `keys export --keys-root <path>` prints the PEM public key to **stdout** (with
a `# occam public key (keyId ...)` banner on stderr) — the intended pinning workflow for a consumer who
doesn't run the MCP host.

### CAP-276 — `occam verify` CLI verb, 4 modes
**File:** `OccamCliVerbs.cs:217-409`
**Classification:** Public. `--mode receipt|citation|manifest|history`, always requires `--pubkey
<path>` (no implicit "use local key" fallback the way the MCP tool has — a deliberate difference
since the CLI's whole point is a THIRD PARTY checking a receipt without the signing host, so there is
no "local key" to default to). Reads receipt/markdown/proof from `--<flag> <path|->` (`-` = stdin).
**Exit codes are the load-bearing machine-readable contract** (doc-comment at lines 22-23, honored
exactly in code): `0` = verified, `1` = parsed-but-not-verified (tamper/wrong key/drift/unsigned),
`2` = usage/IO error. Verdict JSON → stdout; diagnostics → stderr — clean separation for scripting.
- `--mode receipt`: same `ReceiptVerifier.VerifyOffline` core, plus (unlike the MCP tool's offline mode)
  also folds a present time anchor into the pass/fail verdict (`anchorValid != false` required for
  `verified`) — the CLI's receipt-mode is actually a strictly stronger check than the MCP tool's
  offline mode in this one respect.
- `--mode citation`: reads a proof JSON file, verifies via `MerkleTree.VerifyProof`.
- `--mode manifest`: verifies a full `occam_dataset_export` response — reconstructs the dataset's
  `DatasetRow[]` from the JSON and calls `DatasetManifestBuilder.Verify` (CAP-283).
- `--mode history`: verifies a watch-history chain via `WatchHistoryChain.Verify` (CAP-277), same logic
  as the MCP tool's history mode.

### CAP-277 — `occam version-surface` / `occam install-browser` / `occam lifecycle`
**File:** `OccamCliVerbs.cs:44-49, 65-166, 168-206, 482-548`
**Classification:** Public but **adjacent, not trust-subsystem** — noted here only because they share
the file and the `TryRun` dispatcher. Not receipts/signature-related; out of this report's deep-dive
scope (mentioned for completeness of "what lives in `OccamCliVerbs.cs`").

---

## 7. Call sites — how receipts attach to tool responses

### CAP-278 — `OccamTranscodeResponseBuilder.BuildReceipt` (the one shared builder)
**File:** `src/FFOccamMcp.Core/Tools/OccamTranscodeModels.cs:347-412`
**Classification:** Internal, but it is THE single call site every signed-positive-receipt tool goes
through (fan-in, mirroring the fan-out of CAP-257's detached-sign primitive):

| Caller | File:line | Notes |
|---|---|---|
| `occam_transcode` (success) | `OccamTranscodeTool.cs:302,313` | Also gated on `ReceiptsPolicy.Enabled()`; `unchanged:true` responses skip signing entirely and emit a compact telemetry-only receipt (lines 287-296); delta-primary responses sign but strip `BlockLeaves`/`Capsule` (lines 306-309) |
| `occam_digest` (per-URL) | `Services/DigestService.cs:342` | `timeAnchor: null` explicitly — digest never time-anchors sub-results |
| `occam_claim_check` | `Claims/ClaimCheckService.cs:61-62` | Only call site passing `leafSetComplete: complete` (CAP-263) |
| `occam_dataset_export` (per-row) | `Dataset/DatasetExportService.cs:102` | Default `leafSetComplete: false`; each row ALSO gets its own row-leaf hashed into the manifest (CAP-283) |
| `occam_crosscheck` (opt-in) | `Consensus/ConsensusService.cs:92` | Per-vantage; consensus verdict itself carries no separate signature — doc-comment states "the verdict is re-derivable by anyone from the [individual] receipts" |

Threading a shared builder through 5 call sites means a canonicalization or signing bug fixed once
fixes it everywhere — a real strength — but also means all 5 tools inherit the SAME limitations (e.g.
none of them time-anchor except transcode's non-`unchanged`/non-delta path; only claim_check ever
attests `leafSetComplete`).

### CAP-279 — `OccamTranscodeResponseBuilder.BuildNegativeReceipt` — fan-out to failure paths
**File:** `OccamTranscodeModels.cs:419-454`; call sites: `OccamTranscodeTool.cs:604`,
`ClaimCheckService.cs:48-49`, `DatasetExportService.cs:89-90`, `ConsensusService.cs:102-103`.
Same "provable unavailability" gate everywhere (CAP-264) — consistent behavior across all 4 producers.

### CAP-280 — `ReceiptsPolicy` — the single global kill-switch
**File:** `src/FFOccamMcp.Core/Receipts/ReceiptsPolicy.cs`
**Classification:** Advanced (operator env var). `OCCAM_RECEIPTS=off|0|false` (case-insensitive)
disables signing; **on by default** (`null` env var → enabled). Checked independently at each call
site (`ReceiptsPolicy.Enabled() ? signer : null` pattern) rather than centrally suppressing the signer
singleton — meaning a caller of `BuildReceipt`/`BuildNegativeReceipt` with a non-null signer bypasses
the flag entirely if it forgets the check. Verified: `OccamTranscodeTool.cs`, `DigestService.cs`,
`ClaimCheckService.cs`, `DatasetExportService.cs` all correctly gate; `ConsensusService.cs` re-implements
the SAME flag-parsing logic locally (`EffectiveSigner()`, lines 114-122) instead of calling
`ReceiptsPolicy.Enabled()` — functionally identical today (same env var, same accepted values) but is
duplicated logic that could silently diverge from `ReceiptsPolicy` if one is edited and not the other.
**Important scope note:** `OCCAM_RECEIPTS=off` disables the SIGNATURE only. The top-level
`contentHash` field on a transcode success response is computed **unconditionally**
(`OccamTranscodeTool.cs:273`, `Compile.ContentHashToken.BareHex(...)`, independent of `receiptSigner`)
— so turning off receipts does not remove content-hash exposure from the response, only the
cryptographic attestation of it.
**`occam_playbook_save`'s signing is NOT gated by `ReceiptsPolicy` at all** — `PlaybookSaveService.cs:86-91`
calls `PlaybookSignature.BuildSignedJson(...)` unconditionally with the injected `ReceiptSigner`; there
is no `OCCAM_RECEIPTS` check before playbook signing. This is an inconsistency worth flagging: an
operator who sets `OCCAM_RECEIPTS=off` expecting "no signing anywhere" will still get signed playbooks
on disk.

---

## 8. Playbook signing (SI-08 local foundation)

### CAP-281 — `PlaybookSignature.BuildSignedJson` (producer, at `occam_playbook_save`)
**File:** `src/FFOccamMcp.Core/Playbooks/PlaybookSignature.cs:43-88`; call site
`Playbooks/PlaybookSaveService.cs:86-91`.
**Classification:** Public (indirectly — every playbook saved via the core `occam_playbook_save` tool
gets this). Injects a `provenance` block into the playbook JSON: `keyId`, `alg`, `contentHash` (SHA-256
over the playbook **with its own prior `provenance` excluded** — a canonical-JSON writer with
alphabetically-sorted keys, `PlaybookSignature.cs:169-198`, distinct from `ReceiptCanonicalizer`'s
fixed-order approach but achieving the same "idempotent under re-signing" property), `signature`
(detached sign over the content hash string bytes), `signedAt`, and the recipe's own verify-gate claim
(`score`/`passesGate`/`noiseLeakage`) — i.e. the signature also vouches for what the SAVE-TIME gate
computed, not just the recipe body.

### CAP-282 — `PlaybookSignature.Inspect` (consumer, at `occam_playbook_resolve`)
**File:** `PlaybookSignature.cs:97-140`; call site `Tools/OccamPlaybookResolveTool.cs:42-59`.
**Classification:** Public, surfaced on every `occam_playbook_resolve` success as a
`signature_trust`-style field (`OccamPlaybookSignatureInfo`). 4-state classification, each
semantically distinct (this is the most nuanced trust-status logic in the whole subsystem):
- `unsigned` — no `provenance.signature` present at all.
- `verified` — claimed `keyId` == the **inspecting host's own** `localSigner.KeyId`, AND signature +
  content-hash both check out.
- `invalid` — claimed `keyId` == our own key, but signature/hash do NOT check out → **tamper
  detected** on a playbook that claims to be self-authored.
- `unknown_key` — claimed `keyId` != our own key → a genuinely foreign author's recipe; explicitly
  documented as "a real signature we cannot verify with the only key we hold" — NOT reported as
  `invalid` (which would incorrectly imply tampering). This distinction (own-key-tampered vs.
  foreign-key-unverifiable) is the correct way to avoid false "tamper" alarms on legitimately
  multi-author playbook trees, and it is unit-tested for both branches
  (`ReceiptUnitTests.cs:231-245`).
**Trust model note:** since there is still only ONE local key in this v1 (no registry/PKI, CAP-290),
"unknown_key" is currently the terminal state for anything not self-signed by this exact host — there
is no way today to pin a *second*, explicitly-trusted foreign key and have it classify as `verified`
rather than `unknown_key`. This matches the documented SI-08 scope-cut ("registry PKI" is future work)
rather than being an oversight.

---

## 9. Dataset export manifests (SI-17)

### CAP-283 — `DatasetManifestBuilder`
**File:** `src/FFOccamMcp.Core/Dataset/DatasetManifest.cs`
**Classification:** Public (backs `occam_dataset_export`'s `manifest` field and CLI
`verify --mode manifest`).
`DatasetRow{Url, FinalUrl, Ok, ContentHash?, BlockMerkleRoot?, FailureCode?}` is the leaf identity —
deliberately does NOT carry the extracted content itself (keeps the manifest small; content
verification happens per-row via each row's own `receipt`, not via the manifest). Row leaf = SHA-256
over a newline-joined, fixed-field-order, always-6-fields-present (empty string for null) preimage —
own hand-written canonicalization, independent of `ReceiptCanonicalizer` (3rd of the 4 independent
canonicalizers noted in CAP-257). Manifest root = `MerkleTree.RootFromLeafHashes` over row leaves (row
**order is significant** — reordering rows changes the root, which is correct: it proves "this exact
sequence of N extractions was produced together," not just "this set"). One **detached** signature
(via `SignDetached`/`VerifyDetached`, CAP-257/260) over `{v, createdAt, rowCount, manifestRoot, keyId,
alg}` covers the whole set with a single signature — so tampering with row order, adding/dropping a
row, or editing any row's identity fields all invalidate the manifest root and thus the signature
check, without needing N signatures.
**`Verify` is a static, pure, offline function** — reachable both from `occam_verify`... actually, note:
`occam_verify` (the MCP tool) does **NOT** expose a manifest mode — only the CLI does
(`--mode manifest`, CAP-276). This is an asymmetry worth flagging: an agent using only MCP tools has no
way to verify a dataset manifest's signature without shelling out to the CLI binary; only the CLI verb
covers dataset manifests.

---

## 10. Watch-history signed chain (SI-05) — opt-in dependency

### CAP-284 — `WatchHistoryEntry` / `WatchHistoryChain`
**File:** `src/FFOccamMcp.Core/Watch/WatchHistory.cs`
**Classification:** Advanced, but gated behind an **opt-in tool** (`occam_watch`, requires
`OCCAM_WATCH_MCP=1` per `Transport/OccamMcpServerRegistration.cs:134-139`) — i.e. this whole capability
is unreachable via MCP unless the operator has explicitly enabled `occam_watch`. `occam_verify
mode=history` and CLI `verify --mode history`, however, are **always reachable** (they're on the
always-on `occam_verify` tool / always-available CLI) even when `occam_watch` itself is disabled — so a
consumer could still verify a history array **if they somehow obtained one from a prior run** when
`OCCAM_WATCH_MCP` was on, even after it's turned off. Minor but real asymmetry.
- Each entry: `Seq`, `ObservedAt`, `Event` (`first_seen`|`changed`), `ContentHash`, optional
  `BlockMerkleRoot`, optional `ContentDeltaTokens`, `PrevEntryHash` (hash of the prior **fully-signed**
  entry, so signatures themselves are pinned into the chain, not just content), `KeyId`/`Alg`/`Sig`.
- `EntryHash` = SHA-256 over the canonical bytes **including** `sig` (`includeSig: true`) — deliberately
  different from the signing bytes (which exclude `sig`, `includeSig: false`) — this two-mode
  canonicalizer (4th independent one, CAP-257) is a subtle but correct design: the *signature* covers
  the entry-without-its-own-signature (standard), while the *chain link* covers the entry-with-its-
  signature (so an attacker can't swap in a different, still-validly-signed version of "the same"
  entry without breaking the next link).
- `Verify` checks, per entry: consecutive `Seq`, correct `PrevEntryHash` link, genesis (`seq==0`) has
  null `PrevEntryHash`, and (if signed) the detached signature validates. **Handles a windowed/pruned
  chain correctly** — verification does not assume entry 0 of the array is the true genesis; it only
  enforces the genesis-null rule when the first retained entry's `Seq == 0` (comment + behavior at
  `WatchHistory.cs:126-131`, tested at `ReceiptUnitTests.cs:261-262`).
- Unsigned entries (when receipts are off) still chain via hash-links and `Verify` skips only the
  per-entry signature check for those — the tamper-evidence of the *chain structure* survives even
  with receipts fully disabled, only the per-entry authorship claim is lost.

---

## 11. Knowledge-provenance bridge (extract_knowledge / Knowledge canonical model)

### CAP-285 — `KnowledgeProvenance.ReceiptContentHash` / `BlockLeafHash`
**File:** `src/FFOccamMcp.Core/Knowledge/Canonical/KnowledgeProvenance.cs:22-25`
**Classification:** Internal. Explicitly documented (doc-comment) as an "opaque bridge" — these two
`string?` fields are NOT a second receipt model; they just carry over a content-hash / leaf-hash string
computed elsewhere, with the doc-comment explicitly warning readers not to confuse this type with
`ReceiptEnvelope` or `Playbooks.PlaybookProvenance`. Good defensive naming/documentation given there are
now 3 different things called "provenance" in the codebase.

### CAP-286 — `MaterializedProvenanceResolver` — **UNREACHABLE / dead code**
**File:** `src/FFOccamMcp.Core/Knowledge/MaterializedProvenanceResolver.cs`
**Classification:** Internal — but flagged as a **finding**: grep across `src/` finds this class
referenced ONLY in its own defining file. `Resolve` / `ResolveAndVerify` implement a real, working
Claim→Evidence→Source chain-walk with Merkle-membership verification (reusing `MerkleTree.Proof`/
`VerifyProof`, same primitives as `occam_verify`), but **no MCP tool, CLI verb, or service currently
calls it**. It is not wired into `OccamExtractKnowledgeTool` (whose own "receipt" is non-cryptographic
telemetry, see CAP-287) or anywhere else. This looks like a provenance-verification capability that was
built (with its own `ProvenanceTrace`/`ProvenanceTraceStatus` result types) but never connected to a
caller — either scaffolding for a not-yet-shipped feature, or a capability that regressed out of its
call site during a refactor. Worth a maintainer decision: wire it up (e.g. to
`occam_extract_knowledge` facts, giving them the same Merkle-provable-membership guarantee as
`occam_claim_check`) or remove it.

### CAP-287 — `occam_extract_knowledge`'s "Receipt" is NOT a signed Receipt v1 — **finding**
**File:** `src/FFOccamMcp.Core/Tools/OccamExtractKnowledgeTool.cs:88,111-115`
**Classification:** Public field name, but the CONTENT is not part of the trust subsystem at all.
`OccamExtractKnowledgeReceiptInfo(Confidence, ElapsedMs)` — doc-comment literally says "AF-3: receipt
for knowledge extract," and the field on the success response is named `Receipt`, but it carries only
two floats/ints, no `ReceiptEnvelope`, no signature, no `keyId`, no `contentHash`. A consumer who sees
`"receipt": {...}` in an `occam_extract_knowledge` response and assumes it can be handed to
`occam_verify` (as every OTHER tool's `receipt`/`Signed` field can) will find it rejected as
`invalid_receipt` — there is nothing to verify. Given every other core "provenance" tool in this
subsystem (`transcode`, `digest`, `claim_check`, `dataset_export`, even the opt-in `crosscheck`) DOES
attach a real signed envelope, this is the one inconsistency in an otherwise systematic design. Not
a security hole (nothing is falsely claimed to be signed — the field simply isn't a receipt in the
crypto sense), but a naming/API-consistency gap a doc-vs-code or product-consistency pass should catch.

---

## 12. Cross-cutting security observations

### CAP-288 — No PKI / key-ownership registry (v1 explicit scope-cut)
Called out by name in THREE separate doc-comments (`ReceiptSigner.cs:9-11`, `ReceiptVerifier.cs:19-21`,
`OccamVerifyTool.cs:19-20`) — "SI-08" is the internal shorthand for "future registry." Today, `KeyId`
(`k1:<16-hex>`) is a fingerprint with no attached identity/authority claim; a consumer's only trust
anchor is whichever public key PEM they were handed out-of-band (classic TOFU). This is consistently
documented, not hidden, and the verification code correctly never pretends otherwise (e.g. `unknown_key`
vs `invalid` in playbook inspection, CAP-282) — but any product messaging claiming "cryptographically
verified provenance" should be read alongside this: verified against WHOM is still an open question in
v1.

### CAP-289 — Single key, many purposes
One `ReceiptSigner` instance (one ECDSA P-256 keypair per host, DI singleton,
`OccamServiceCollectionExtensions.cs:23`) signs: extraction receipts, negative receipts, playbook
provenance blocks, dataset manifests, and watch-history entries. There is no per-purpose key
separation or domain-separation prefix baked into any of the 4 independent canonical-byte functions
(CAP-257) beyond their differing field sets — i.e. cross-protocol signature confusion is prevented only
by the fact that the canonical byte layouts of a `ReceiptEnvelope`, a playbook's content-hash string, a
dataset-manifest preimage, and a watch-history entry are all structurally different JSON shapes, not by
an explicit domain tag. In practice this is a common and generally low-risk pattern for ECDSA when the
signed structures are non-ambiguous JSON objects with different key sets (a signature over one can't be
naively replayed as a valid signature over a different, differently-shaped preimage) — but it is
architecturally 1 key doing 4 jobs with 4 bespoke serializers, which is worth knowing if a future
change ever makes two of those preimages accidentally byte-coincide.

### CAP-290 — Fail-closed vs fail-open posture (consistent, by design)
- **Fail-closed correctly:** `occam_attest`'s status classification never infers "supported" from
  retrieval score or Merkle proof alone (`AttestService.cs`, doc-comments at
  `Tools/OccamAttestTool.cs:11-16` and `Attest/AttestModels.cs:28-34`) — proof only proves existence,
  never truth. `occam_verify live` refuses to re-fetch if the offline signature already failed
  (CAP-270). Malformed input everywhere in this subsystem (bad JSON, bad base64, bad PEM) maps to a
  typed failure/verdict rather than throwing or silently succeeding.
- **Fail-open correctly (opt-in bonuses only):** the RFC3161 time-anchor producer (CAP-267) is the only
  deliberately fail-open path in the subsystem, and it's fail-open only for an **additive, optional**
  guarantee (a receipt is still fully valid without a time anchor) — not for anything load-bearing.

### CAP-291 — Attacker-controlled input handling
Every parser reachable from untrusted MCP/CLI input in this subsystem (`OccamVerifyTool.TryParseReceipt`,
`CapsuleCodec.TryParse`, `MerkleTree.VerifyProof`, `ReceiptVerifier.VerifyOffline`, CLI's
`TryParseReceipt`) catches its expected exception types (`JsonException`, `FormatException`,
`CryptographicException`) and returns a typed failure/`false` rather than propagating. No stack traces
or internal exception messages are echoed back into verdict JSON. This is consistently good hygiene
across the whole file set reviewed.

---

## 13. Gate / test coverage observed (evidence the above is exercised, not just written)

- `benchmarks/l0-gate/ReceiptUnitTests.cs` (357 lines) — sign/verify roundtrip, 4 distinct tamper
  vectors (content, signature bytes, a signed field, wrong key), a **byte-for-byte canonical golden
  vector** (guards silent serializer/field-order drift), Merkle root determinism + empty/single-leaf
  edge cases, negative receipts (captcha + 4xx, and confirms NO receipt on transient timeout / no
  signer), full JSON round-trip survival, `occam_verify` offline/prove/citation modes through the real
  tool class, signed-playbook sign/verify/tamper/wrong-key, playbook `Inspect` all 4 states including
  the `unknown_key` vs `invalid` distinction, signed watch-history chain (genesis rules, tamper,
  reorder, broken link, **windowed/pruned chain**, unsigned chain), a **real captured RFC3161 token**
  verified against a fixed SHA-256 imprint (genuine third-party TSA interop test, not just a mock), and
  block-survival reconciliation (pruned blocks correctly excluded from the signed Merkle root).
- `benchmarks/l0-gate/CapsuleUnitTests.cs` — encode/decode round trip, tamper→content_mismatch, wrong
  key→signature_invalid, malformed capsule never throws, prove-mode through a capsule, and a genuine
  **producer→consumer end-to-end** test (`OccamTranscodeResponseBuilder.BuildReceipt(...,
  emitCapsule:true)` → `OccamVerifyTool.Verify` with no page) — this is the strongest evidence in the
  whole subsystem that the "agent hands another agent a capsule, no re-fetch" claim is real and tested,
  not aspirational.
- `benchmarks/l0-gate/CliVerbsUnitTests.cs` — exercises the CLI's `TryRun` end-to-end with real temp
  files (genuine receipt exit 0, wrong key exit 1, tampered markdown exit 1, `keys export` exit 0,
  missing `--pubkey` exit 2, non-verb falls through, `version-surface` shape) — confirms the exit-code
  contract documented in the CLI's own doc-comment is actually honored.
- `benchmarks/l0-gate/DatasetExportUnitTests.cs`, `AttestUnitTests.cs`, `ClaimCheckUnitTests.cs` exist
  (file list confirmed via grep) but were not opened line-by-line in this pass — flagged for a follow-up
  reviewer if deeper manifest/attest/claim-check test-quality auditing is wanted.
- No dedicated gate file found for `ConsensusService`/`occam_crosscheck` receipt wiring specifically
  (opt-in tool; only reachable in the gate via whatever generic coverage exists, not confirmed here) —
  **not verified** whether crosscheck's per-vantage receipts are gate-tested at all.

---

## 14. Summary capability table (CAP-250 – CAP-291 used; CAP-292–299 reserved/unused)

| CAP | Capability | Classification | File(s) |
|---|---|---|---|
| 250 | `ReceiptEnvelope` schema | Public | Receipts/ReceiptModels.cs |
| 251 | `ReceiptCanonicalizer` | Internal | Receipts/ReceiptCanonicalizer.cs |
| 252 | `MerkleTree` core | Internal/Advanced | Receipts/MerkleTree.cs |
| 253 | `Base64Url` | Internal | Receipts/Base64Url.cs |
| 254 | `ReceiptSigner` (ECDSA P-256) | Advanced | Receipts/ReceiptSigner.cs |
| 255 | Key storage (`OCCAM_KEYS_ROOT`, unencrypted PEM) | Advanced/security | Receipts/ReceiptSigner.cs:26-45,84-99 |
| 256 | `ReceiptSigner.CreateEphemeral` | Internal (test-only) | Receipts/ReceiptSigner.cs:48 |
| 257 | `SignDetached`/`VerifyDetached` fan-out (4 independent canonicalizers) | Internal primitive | ReceiptSigner.cs, ReceiptVerifier.cs |
| 258 | Verdict vocabulary | Public | Receipts/ReceiptVerifier.cs:6-15 |
| 259 | `VerifyOffline` | Public | Receipts/ReceiptVerifier.cs:40-80 |
| 260 | `VerifyDetached` | Internal | Receipts/ReceiptVerifier.cs:26-38 |
| 261 | `TimeAnchorVerifier` (no TSA chain validation) | Advanced/security note | Receipts/ReceiptTimeAnchor.cs:29-84 |
| 262 | Merkle proof / VerifyProof (SI-02b) | Advanced | Receipts/MerkleTree.cs:109-169 |
| 263 | `LeafSetComplete` provable absence | Advanced (narrow: claim_check only) | ReceiptModels.cs, ClaimCheckService.cs |
| 264 | Negative receipts (SI-03) | Public | OccamTranscodeModels.cs:419-454 |
| 265 | `CapsuleCodec` | Advanced/opt-in | Receipts/CapsuleCodec.cs |
| 266 | `ChunkStalenessEvaluator` (SI-12) | Advanced | Receipts/ChunkStaleness.cs |
| 267 | `TimeAnchorService` producer (SSRF-guarded, fail-open) | Advanced/opt-in | Receipts/TimeAnchorService.cs |
| 268 | `OccamVerifyTool` dispatch | Public core | Tools/OccamVerifyTool.cs |
| 269 | `occam_verify` offline mode (+ capsule auto-detect) | Public | OccamVerifyTool.cs:44-72 |
| 270 | `occam_verify` live mode | Public | OccamVerifyTool.cs:148-233 |
| 271 | `occam_verify` prove mode | Public/advanced | OccamVerifyTool.cs:236-257 |
| 272 | `occam_verify` citation mode | Public/advanced | OccamVerifyTool.cs:260-293 |
| 273 | `occam_verify` history mode | Public/advanced | OccamVerifyTool.cs:84-108 |
| 274 | Flexible receipt-input parsing | Internal | OccamVerifyModels.cs, OccamVerifyTool.cs:296-319 |
| 275 | CLI dispatch + `keys export` | Public | Cli/OccamCliVerbs.cs:28-56,208-215 |
| 276 | CLI `verify` (4 modes, exit codes) | Public | Cli/OccamCliVerbs.cs:217-409 |
| 277 | CLI adjacent verbs (version-surface/install-browser/lifecycle) | Public, out of scope | Cli/OccamCliVerbs.cs |
| 278 | `BuildReceipt` shared builder (5 call sites) | Internal fan-in | OccamTranscodeModels.cs:347-412 |
| 279 | `BuildNegativeReceipt` shared builder (4 call sites, 1 dead branch) | Internal fan-in | OccamTranscodeModels.cs:419-454 |
| 280 | `ReceiptsPolicy` kill-switch (inconsistently applied) | Advanced/finding | Receipts/ReceiptsPolicy.cs |
| 281 | `PlaybookSignature.BuildSignedJson` (unconditional, not receipts-gated) | Public | Playbooks/PlaybookSignature.cs:43-88 |
| 282 | `PlaybookSignature.Inspect` (4-state trust) | Public | Playbooks/PlaybookSignature.cs:97-140 |
| 283 | `DatasetManifestBuilder` (no MCP verify mode, CLI-only) | Public/finding | Dataset/DatasetManifest.cs |
| 284 | `WatchHistoryChain` (opt-in tool, always-verifiable) | Advanced | Watch/WatchHistory.cs |
| 285 | `KnowledgeProvenance` opaque bridge fields | Internal | Knowledge/Canonical/KnowledgeProvenance.cs |
| 286 | `MaterializedProvenanceResolver` — **dead code, unreachable** | Internal/finding | Knowledge/MaterializedProvenanceResolver.cs |
| 287 | `occam_extract_knowledge` "Receipt" is non-cryptographic — **finding** | Public/finding | Tools/OccamExtractKnowledgeTool.cs:88,111-115 |
| 288 | No PKI / key-ownership registry (documented scope-cut) | Architecture/security | 3 files, see §12 |
| 289 | Single key, 4 purposes, 4 bespoke canonicalizers | Architecture/security | see §12 |
| 290 | Fail-closed/fail-open posture audit | Architecture/security | see §12 |
| 291 | Untrusted-input exception handling audit | Security | see §12 |

CAP-292 through CAP-299 are **reserved, not allocated** — no further distinct capabilities were found
in this subsystem beyond the 42 (250–291) enumerated above.

---

## 15. Explicit non-findings (checked, found consistent/correct)

To avoid these being re-litigated by a later pass: the following were specifically checked and found
to be correct, not gaps —
- Signature encoding (P1363 vs DER) — deliberate, documented, correct for cross-platform stability.
- `sig` exclusion from canonical bytes — correct in all 4 canonicalizers (receipt, playbook, manifest,
  history-signing-mode).
- Null-field omission for backward compatibility (`LeafSetComplete`) — verified byte-identical via gate.
- Merkle proof/citation never conflates "block existed" with "claim is true" — consistently enforced in
  `occam_claim_check`, `occam_attest`, `occam_verify citation`.
- SSRF guarding on the one outbound network call this subsystem makes (`TimeAnchorService`) — double
  guarded (explicit private-host check + shared `OutboundHttpGuard`).
- Windowed/pruned watch-history chains verify correctly without assuming array index 0 is genesis.
