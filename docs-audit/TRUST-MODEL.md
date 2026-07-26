# TRUST-MODEL — canonical, code-verified (Phase 5I, agent P5-05)

**Status:** CANONICAL for the trust/provenance product system (PS-6) and for every trust claim made
about any other system. **Source of truth: executable code**, re-read for this document. Where the
Wave 1–4 audit corpus disagrees with the code, the code wins and the correction is recorded in §12.

**Phase 6 delta (2026-07-26, branch `fix/phase6-product-hardening`):**
- Watch history: `chainIntegrity` + `signatureStatus`; `history_verified` only when every entry is signed and verifies (EF-059 **FIXED**).
- Playbook Inspect: verify-before-classify; `wrong_key` / `key_mismatch` (EF-058 **MITIGATED** at P6).
- Receipt/verify surfaces may emit `wrong_key` (EF-062 **FIXED**).
- Reader profile exposes `occam_verify` (EF-061 **FIXED**).
- All 20 forbidden claims in §13 remain **in force** (none became newly true).

**Phase 6.5 delta (2026-07-26, OD-4):** Playbook signature **v2** (`PLAYBOOK-SIGNATURE-V2-CONTRACT.md`) now covers `keyId`, `alg`, `contentHash`, `signedAt` and the `verify{score,passesGate,noiseLeakage}` snapshot under a domain-separated preimage (`occam-playbook-sig-v2\n…`); v1 verification preserved; `sigVersion` reported; `unsupported_version` distinct. This **retires findings X1/X2 for v2 artifacts** (the gate snapshot is now tamper-evident) — but a v2 signature still proves **integrity relative to a key, not truth, identity, origin, or trusted time**, and `verify.score` remains a heuristic. v1 artifacts keep the X1/X2 caveat (gate fields unsigned). All 20 forbidden claims remain in force.

**This document is deliberately pessimistic.** Where a mechanism proves less than its name implies,
the shortfall is the finding, not a footnote. Nothing here is marketing copy and nothing here should
be paraphrased upward.

---

## 1. Honest summary — what Occam's trust layer is and is not

Occam's trust layer is a **single-key, self-signed, local integrity-and-provenance log**. On first
host start it silently mints an unencrypted ECDSA P-256 private key at `~/.occam/keys/signing-key.pem`
(`OccamServiceCollectionExtensions.cs:23` → `ReceiptSigner.cs:26-45`) and thereafter signs, with that
one key, five different kinds of object: extraction receipts, negative (provable-unavailability)
receipts, playbook provenance blocks, dataset manifests, and watch-history entries. What a signature
proves is exactly one thing: **"the holder of this private key asserted this exact byte-string, and
nobody altered it afterwards."** It does not prove who that holder is, that the assertion is true,
that the origin server actually served that content, that the content is accurate, or when it
happened (the timestamp is the signer's own clock unless an opt-in RFC3161 anchor is configured, and
even then the TSA certificate is never chained to a trust root — `ReceiptTimeAnchor.cs:34-35`). The
Merkle layer is genuinely useful and genuinely narrow: it proves a text block was among the blocks
the signer committed to, never that the block is true or that the page contained it. `occam_verify`
and the `occam verify` CLI are correct implementations of that narrow math. **The binding of the key
to any real-world identity is entirely absent from this codebase** — there is no PKI, no registry, no
key distribution, no revocation, no rotation, no expiry; the model is trust-on-first-use over a
public-key PEM the consumer must obtain out of band. In practice, for the overwhelmingly common
single-machine deployment, an Occam signature is **the machine attesting to itself**, and its honest
value is tamper-evidence and self-consistency over time — not third-party provenance.

---

## 2. Vocabulary — twelve concepts that must never be blurred

The single most common failure mode in writing about this subsystem is collapsing these into
"verified". They are distinct, and several of them involve no cryptography at all.

| # | Concept | One-line definition (code-derived) |
|---|---------|-------------------------------------|
| C1 | **ACQUISITION FACT** | The host ran an extraction and got bytes. No artifact of its own; everything else is downstream of an unverifiable claim that this happened. |
| C2 | **CONTENT HASH** | `sha256:` + hex of SHA-256 over the UTF-8 *compiled markdown* (`ReceiptCanonicalizer.cs:17-18`). Computed **unconditionally**, signer or not (`OccamTranscodeTool.cs:273`). |
| C3 | **RECEIPT** | A `ReceiptEnvelope` (`ReceiptModels.cs:12-53`): a fixed set of fields (url, finalUrl, backend, ts, toolchain, contentHash / failureCode, keyId, alg) plus a signature. Two kinds: `extraction` (positive) and `negative`. |
| C4 | **SIGNATURE** | ECDSA P-256 / SHA-256, IEEE P1363 fixed-size `r‖s`, base64url (`ReceiptSigner.cs:32,62,72`), over hand-written canonical bytes with `sig` excluded (`ReceiptCanonicalizer.cs:21-56`). |
| C5 | **MERKLE INCLUSION** | An ordered SHA-256 tree over extraction blocks, leaf preimage `utf8(text + '\0' + source_selector)` (`MerkleTree.cs:18-22`); `Proof`/`VerifyProof` give ≈log₂N membership proofs (`MerkleTree.cs:109-169`). |
| C6 | **CAPSULE** | `occam://capsule/<base64url(json)>` bundling `{cap, kind, signed, content, blockLeaves, timeAnchor, verifyRecipe}` (`CapsuleCodec.cs:16-58`). **The capsule wrapper itself is not signed** — only the nested envelope is. |
| C7 | **CITATION** | A `{leaf, proof[]}` package for one block, emitted by `occam_verify mode=prove` / carried per-match by `occam_claim_check` (`OccamVerifyTool.cs:236-257`, `ClaimCheckService.cs:78-85`). |
| C8 | **CLAIM CHECK** | BM25 retrieval of blocks relevant to a claim string, plus per-match Merkle proofs. **No stance evaluation** — `Verdict` is hardcoded `not_evaluated` (`ClaimCheckService.cs:102`). |
| C9 | **ATTESTATION** (`occam_attest`) | A rule-based, regex-driven entailment classifier over claim-check output, producing `supported / contradicted / related / unsupported / unknown`. **The aggregate response is not signed at all.** |
| C10 | **CONSENSUS / CROSSCHECK** | Same host fetches one URL through 2–4 vantages and compares `blockMerkleRoot ?? contentHash`. **The verdict string is plain unsigned JSON** (`ConsensusModels.cs:49-55`, CAP-856). |
| C11 | **LIVE VERIFICATION** | `occam_verify mode=live` re-fetches `finalUrl` through the full pipeline and reports drift (`OccamVerifyTool.cs:169-219`). Requires network; produces no second receipt. |
| C12 | **OFFLINE VERIFICATION** | Pure math over supplied bytes + a supplied public key: `ReceiptVerifier.VerifyOffline`, `MerkleTree.VerifyProof`, `DatasetManifestBuilder.Verify`, `WatchHistoryChain.Verify`. |

---

## 3. Per-primitive matrix

Columns: **NET** = network access required · **ORIG** = original content required · **3P** = a third
party can verify, and what they must possess.

| Concept | WHAT IS PROVEN | WHO/WHAT MUST BE TRUSTED | NET | ORIG | CRYPTO (algorithm · key · `path:line`) | 3P VERIFIABLE? |
|---|---|---|---|---|---|---|
| **C1 ACQUISITION FACT** | Nothing. There is no artifact and no independent witness of the fetch. | The host binary, the operator, the network path, the origin server, every worker process, every managed provider in the cascade | yes (at the time) | n/a | **none** | **No.** Nothing outside the host observes the fetch. |
| **C2 CONTENT HASH** | Two byte-strings are or are not identical. | Whoever asserts the hash corresponds to a page — the hash itself asserts nothing about origin | no | yes (to compare) | SHA-256 · no key · `ReceiptCanonicalizer.cs:17-18` | Yes — anyone with the markdown can recompute. Proves **nothing** about where it came from. |
| **C3 RECEIPT** | The listed field values were asserted together as one unit. | The signer (host binary + key holder), and the host's clock for `ts` | no (to verify) | no | see C4 | Yes, with the correct public-key PEM. |
| **C4 SIGNATURE** | The holder of the private key for the supplied PEM signed these exact canonical bytes; no field inside the canonical set was altered afterwards. | **The key holder — whose identity is not established anywhere in this codebase.** Plus the byte-stability of the hand-written canonicalizer. | no | no | ECDSA P-256 / SHA-256, P1363 `r‖s`, base64url · `~/.occam/keys/signing-key.pem` · `ReceiptSigner.cs:32,51-64,71-72`; verify `ReceiptVerifier.cs:40-80` | Yes — they must possess the receipt JSON **and** the correct public-key PEM, obtained out of band. |
| **C5 MERKLE INCLUSION** | A leaf hash — i.e. an exact `(text, source_selector)` pair — was in the multiset committed to by a given root. | Everything C4 requires, plus that the root was in the *signed* envelope and not supplied loose | no | no (that is the point) | SHA-256 tree, odd levels duplicate last · no key of its own · `MerkleTree.cs:74-101,109-169` | Yes — needs `{envelope, block_text, block_selector, proof[], pubkey}`. |
| **C6 CAPSULE** | Only what the nested signed envelope proves. `content`, `blockLeaves`, `timeAnchor`, `verifyRecipe` are **unsigned cargo**; their only assurance is that they re-hash to the signed `contentHash` / `blockMerkleRoot` when a verifier checks (`CapsuleCodec.cs:13-14`). | Same as C4. `verifyRecipe` is advisory text nothing validates. | no | no (it carries its own content) | inherited from C4/C5 · `CapsuleCodec.cs:33-88` | Yes — the capsule string plus the PEM. This is the strongest hand-off form Occam ships. |
| **C7 CITATION** | The quoted text sat at that selector inside the signed extraction. | Same as C5. Also: the extraction is what the host chose to extract, not the page. | no | no | SHA-256 Merkle · `OccamVerifyTool.cs:260-293`, `OccamCliVerbs.cs:296-337` | Yes — `{envelope, block_text, block_selector, proof, pubkey}`. |
| **C8 CLAIM CHECK** | (a) which extracted blocks cleared a BM25 relevance floor for the claim string; (b) Merkle membership of each returned block. `proven:true` additionally asserts a complete (untruncated) leaf set. | Same as C5, plus the ranker's heuristic (`ClaimBlockRanker.ClearsFloor` ≥ ceil(40 % of distinct claim terms)) | yes (fetch) | no (afterwards) | SHA-256 Merkle + optional C4 · `ClaimCheckService.cs:56-90` | Partially — the per-match proofs are third-party verifiable; the *retrieval decision* is not reproducible without re-running Occam's ranker. |
| **C9 ATTESTATION** | Nothing cryptographic. It is a regex/keyword entailment classifier's opinion, plus a Merkle proof of the **top** retrieved block only (`AttestService.cs:87-89`). | The classifier's two hard-coded claim shapes and closed type vocabularies (CAP-721/722/724) | yes | no | **none at the aggregate level** — `OccamAttestResponse` carries no signature (`AttestService.cs:38-49`, G-E-08) | **No** for the verdict. Yes for each nested receipt/proof, individually. |
| **C10 CONSENSUS** | That N fetches **from the same process, same egress IP, same proxy config** produced the same or different fingerprints. | The host that ran the comparison and reported the verdict | yes (N fetches) | no | per-vantage receipts only; **verdict unsigned** · `ConsensusService.cs:84-107`, CAP-856 | Only by manual re-derivation from the per-vantage receipts. **No shipped tool or CLI verb re-derives it** (CAP-863(6), EF-032). |
| **C11 LIVE VERIFICATION** | That an anonymous, playbook-less, default-budget `http_then_browser` fetch of `finalUrl` **right now** does or does not reproduce the signed hashes. | Everything in C1 all over again, plus the network and origin at verification time | **yes** | no | reuses C2/C5 · `OccamVerifyTool.cs:169-219` | No — it is a same-process operation and produces no new signed artifact. |
| **C12 OFFLINE VERIFICATION** | Exactly and only what C4/C5 prove, computed without any network. | The verifier's own binary and the provenance of the PEM they chose | **no** | no (markdown optional) | ECDSA P-256 + SHA-256 · `ReceiptVerifier.cs`, `OccamCliVerbs.cs:217-409` | Yes — this is the surface designed for it. |

---

## 4. WHAT IS NOT PROVEN — the column that matters

### C2 Content hash
Does not prove the content came from the URL, from the origin server, from the network at all, or at
any particular time. `contentHash` is present on transcode successes **even with `OCCAM_RECEIPTS=off`**
(`OccamTranscodeTool.cs:273`) — an unsigned hash is a self-consistency token, nothing more. It also
hashes the **compiled markdown after token budgeting, fitting, focus pruning and translation**, not
the page: two receipts for the same page under different budgets legitimately disagree.

### C3/C4 Receipt and signature
- **Does not prove the origin served that content.** No TLS certificate, no transcript, no origin
  signature is captured anywhere. A host that fabricated the markdown produces a signature
  indistinguishable from an honest one.
- **Does not prove who signed.** `keyId` is `"k1:" + first 16 hex chars of SHA-256(SPKI)`
  (`ReceiptSigner.cs:74-78`) — a 64-bit truncated self-descriptive fingerprint with no identity claim
  attached. `ReceiptVerifier.VerifyOffline`'s own doc-comment states the limit verbatim
  (`ReceiptVerifier.cs:19-21`).
- **Does not prove when.** `ts` is `DateTimeOffset.UtcNow` on the signing host
  (`OccamTranscodeModels.cs:380`), signed but self-asserted. Clock skew, deliberate clock
  manipulation, and offline back-dating are all unconstrained.
- **Does not prove freshness of delivery.** A response served from the opt-in disk cache replays the
  *stored* signed envelope verbatim with its original `ts`; `cached:true` / `cacheAgeS` are added
  outside the signature (`OccamTranscodeTool.cs:377-392`). A receipt cannot distinguish a fresh
  extraction from a cache replay.
- **Does not prove which URL the caller asked for, under cache.** `TranscodeCacheKey.NormalizeUrl`
  drops the fragment (`TranscodeCacheKey.cs:70`) while the signed `url` field is the caller's verbatim
  string, so a hit on `page#a` can be served to a request for `page#b` (EF-045).
- **Negative receipts do not prove the page is inaccessible to anyone else** — only that *this* fetch,
  from *this* egress, with *these* headers, hit a wall code in the set
  `captcha_or_challenge | requires_login | paywall | 401 | 403 | 404 | 410`
  (`OccamTranscodeModels.cs:427-428`). The `"paywall"` disjunct is unreachable (EF-008).
- **Does not prove the content was not tampered with in the browser.** The browser context runs with
  `bypassCSP:true` unconditionally and playbook interaction plans may execute `page.evaluate`
  (EF-046, AUTOMATIC-BEHAVIORS #7/#12) — the DOM that was hashed may have been mutated by
  Occam itself, by a consent-dismisser, or by a playbook.

### C5/C7 Merkle inclusion and citation
- **Does not prove the block was on the page** — only that it was among the blocks the extractor
  produced and the signer committed to. Extraction is lossy and policy-dependent.
- **Does not prove the block is true, current, or in context.** A quote proven present may be a
  disclaimer, a rebuttal, an advertisement, or attacker-planted text.
- **Does not prove leaf-set size or completeness by itself.** Only the *root* is signed. `blockLeaves`
  travels unsigned; a verifier checks it reconstructs the root (`OccamVerifyTool.cs:248-251`) — but
  because odd levels duplicate the last node (`MerkleTree.cs:60,93,139`), a leaf array with a
  duplicated tail reconstructs the **identical** root as the original. `blocksTotal` / `drift`
  denominators derived from a supplied leaf array are therefore not signed quantities (see
  EFC-P5-05-3).
- **`Proof` for an out-of-range index returns an empty list, silently** (`MerkleTree.cs:112-114`);
  an empty proof verifies only for a degenerate single-leaf tree.

### C6 Capsule
The base64url+JSON packaging is outside the signature by design. A capsule's `verifyRecipe` — including
the `keyAnchor` keyId string and the runnable command — is unsigned advisory text that no code
cross-checks against the envelope. A consumer who reads `verifyRecipe.keyAnchor` instead of
`signed.keyId` is reading attacker-writable data. The design is still sound *because* offline verify
re-checks content against the signed hashes, but the capsule is not "a signed bundle" — it is a signed
core in an unsigned wrapper.

### C8 Claim check
`proven:true` is the strongest-sounding field in the product and the most easily over-read. It means:
*no extracted block cleared a lexical BM25 floor for this claim string, over a leaf set that was not
token-truncated.* It does **not** mean the page does not state the claim. Paraphrase, synonymy,
non-English phrasing, text inside images, content behind interaction, and anything the extractor
dropped are all outside its reach. It is a *retrieval*-complete negative, not a *semantic*-complete
negative. (`ClaimCheckService.cs:56-90`, `ClaimBlockRanker.ClearsFloor`, CAP-263/697.)

### C9 Attestation
`occam_attest` is named like a cryptographic attestation and is not one. `status=supported` is the
output of anchored regexes recognising exactly two English claim shapes — `X is [a] Y` and `X uses Y`
(CAP-721/722) — over the top-3 retrieved blocks. Every other phrasing returns `unknown`. The aggregate
counts (`grounded`, `unsupportedTotal`, the five-way partition) are **plain unsigned JSON**. A reader
of the name would assume a signed, verifiable statement about a report's citations; what ships is an
unsigned heuristic tally with per-claim receipts attached underneath.

### C10 Consensus
Agreement between vantages is **not cryptographic evidence of anything**. All vantages leave from one
process, one egress IP, one proxy configuration (CAP-859); they differ only in extraction engine
(HTTP vs Chromium) and whether a session profile's cookies were attached. Agreement therefore rules
out exactly one class of cloaking (bot-vs-browser and anon-vs-authed) and says nothing about
geographic/CDN/ISP-level differentiation, nor about accuracy. `divergent` names no authority: there is
no signal saying which vantage is "the real page". The verdict itself is unsigned and no shipped tool
re-derives it from the signed vantage receipts (EF-032).

### C11 Live verification
`drifted` frequently means "my re-fetch lacked the context the original had", not "the page changed".
The re-fetch drops session profile, playbook overlay, content selectors, token budget and backend pin
— it is a bare `new OccamTranscodeOptions { JsonBlocks = … }` with a hardcoded `http_then_browser`
policy (`OccamVerifyTool.cs:171-175`, EF-012/CAP-652). Any receipt from a login-walled or
playbook-assisted extraction will very plausibly report `refetch_failed` or large drift while nothing
changed. And every re-fetch failure — login wall, challenge, timeout, 404 — collapses to the single
verdict `refetch_failed` with no failure code anywhere in the response (CAP-653).

### C12 Offline verification
Proves nothing about key ownership. The `occam verify` CLI is honest here — it **refuses to run
without `--pubkey`** (`OccamCliVerbs.cs:221-224`). The MCP tool is not: `public_key` is optional and
defaults to *this running host's own key* (`OccamVerifyTool.cs:40`). An agent verifying a foreign
receipt while omitting the parameter gets `signature_invalid`, and the verdict vocabulary cannot
distinguish "wrong key" from "tampered" (`ReceiptVerifier.cs:6-15`).

---

## 5. Trust boundary diagram — who can lie at which point

```
 ┌─────────────┐   can lie: content, headers, status, cloaking by UA/IP/cookie,
 │ ORIGIN      │   prompt-injection text planted for the reading model
 │ SERVER      │   → Occam has NO mechanism to detect any of this
 └──────┬──────┘
        │  (2) network:  TLS terminated inside the worker; no transcript,
        │      no cert pinning, no origin signature is ever retained
 ┌──────▼───────────────────────────────────────────────────────────┐
 │ ACQUISITION EDGE                                                  │
 │  http-extract (Node) · browser-extract (Chromium, bypassCSP:true) │  ← EF-046
 │  css-extract (no DNS-pin, no body cap)                            │  ← EF-043
 │  MANAGED PROVIDER (Jina/Firecrawl/Scrapfly/Spider) — a third      │  ← EF-003
 │  party that sees the URL and returns content Occam will SIGN      │
 └──────┬────────────────────────────────────────────────────────────┘
        │  (3) compile: budget, fit, focus, translate, playbook overlay
        │      → the hash covers the RESULT of these, not the page
 ┌──────▼───────────────────────────────────────────────────────────┐
 │ HOST BINARY  (fully trusted — the entire model rests here)        │
 │  chooses ts (own clock) · chooses url/finalUrl/backend strings    │
 │  computes contentHash + Merkle root · may serve a CACHE REPLAY    │
 │  ┌──────────────────────────────────────────────┐                 │
 │  │ signing-key.pem — unencrypted PKCS8, 0600 on  │  ← ART-034      │
 │  │ POSIX, NO hardening on Windows                │  EF-044         │
 │  │ (ReceiptSigner.cs:84-99)                      │                 │
 │  └──────────────────────────────────────────────┘                 │
 └──────┬────────────────────────────────────────────────────────────┘
        │  (4) SIGNATURE BOUNDARY ── everything above is asserted, not proven
        │      everything below is tamper-evident against this key
 ┌──────▼──────┐
 │ OPERATOR    │  owns the key file, OCCAM_KEYS_ROOT, OCCAM_RECEIPTS,
 │             │  OCCAM_TSA_URL, OCCAM_PROFILE, proxies, managed keys.
 │             │  Can mint keys, delete keys, sign anything, at will.
 └──────┬──────┘
        │  (5) out-of-band PEM hand-off — NOT SOLVED BY THIS CODEBASE
 ┌──────▼──────┐
 │ CONSUMER /  │  can check: signature, contentHash, Merkle membership,
 │ THIRD PARTY │  manifest set-binding, history chain links.
 │             │  cannot check: who k1:… is, whether the origin said it,
 │             │  whether the clock was honest, whether the fetch happened.
 └─────────────┘
        │  (6) optional TSA — proves the SIGNATURE existed by genTime,
        │      but the TSA certificate is NEVER chained to a root
        └──► ReceiptTimeAnchor.cs:34-35
```

**Trusted-by-necessity list, in order of blast radius:** host binary → operator → signing key file →
worker processes and Chromium → managed provider (if configured) → network path → origin server →
host clock → TSA (if configured, and only partially).

---

## 6. Chain of custody — one successful transcode, end to end

| Step | What happens | Code | Trust status of the step |
|---|---|---|---|
| 1 | Host starts; DI resolves `ReceiptSigner.LoadOrCreate()` — mints `signing-key.pem` if absent, **regardless of `OCCAM_RECEIPTS`** | `OccamServiceCollectionExtensions.cs:23`; `ReceiptSigner.cs:26-45` | Automatic, unrequested, silent (AUTOMATIC-BEHAVIORS #1, EF-044) |
| 2 | `occam_transcode(url)` → preflight, router, backend, post-processors | `TranscodePipeline`, `OccamRouter` | **Unwitnessed.** Nothing outside the host observes it. |
| 3 | Markdown compiled (budget / fit / focus / translate); blocks reconciled to it | `Compile/*` | The thing that gets hashed is this, not the page |
| 4 | `contentHash = sha256:hex(utf8(markdown))` | `ReceiptCanonicalizer.cs:17-18` | Unsigned at this point; also emitted bare at `OccamTranscodeTool.cs:273` |
| 5 | Ordered leaves `LeafHashesHex(blocks)`; `blockMerkleRoot = RootFromLeafHashes(leaves)` | `OccamTranscodeModels.cs:372,384` | Root commits to the ordered leaf multiset only |
| 6 | Envelope built: `ts = UtcNow`, `toolchain`, `url`, `finalUrl`, `backend`, optional `playbook{id,version}`, `tokens`, `confidence`, `leafSetComplete` | `OccamTranscodeModels.cs:374-393` | Every field is a host self-assertion |
| 7 | `signer.Sign(envelope)` — stamps `keyId`+`alg`, canonicalizes with `sig` excluded, ECDSA-P256/SHA-256 | `ReceiptSigner.cs:51-64` | **Signature boundary.** `keyId` and `alg` *are* inside the signed bytes (`ReceiptCanonicalizer.cs:49-50`) — key substitution is detectable |
| 8 | Optional: `TimeAnchorService.TryAnchor(sig)` POSTs `SHA-256(sig bytes)` to the operator's TSA; self-checks the reply; **fail-open to null** | `TimeAnchorService.cs:20-74` | Sidecar, unsigned by Occam, not committed to by the envelope — freely strippable |
| 9 | Optional: capsule encoded `{signed, content, blockLeaves, timeAnchor, verifyRecipe}` | `CapsuleCodec.cs:46-65` | Wrapper unsigned; cargo checkable against signed hashes |
| 10 | Response serialized; if cache-eligible, the **whole post-sign envelope** is written to disk | `OccamTranscodeTool.cs:363-368`; ART-035 | Later hits replay this signed receipt verbatim |
| 11 | Consumer receives `receipt: {signed, blockLeaves, timeAnchor, capsule}` | — | Nothing yet verified |
| 12 | **Later:** consumer obtains the PEM out of band — `occam keys export` against the producer's key store | `OccamCliVerbs.cs:208-215` | **The unsolved link.** Running it against an empty `--keys-root` silently mints an unrelated key and exports that. |
| 13 | `occam verify --mode receipt --pubkey pub.pem --receipt r.json [--markdown page.md]` | `OccamCliVerbs.cs:246-294` | Recomputes canonical bytes, checks ECDSA, checks content hash, **gates on time anchor validity too** |
| 14 | Exit `0` verified · `1` parsed-but-not-verified · `2` usage/IO | `OccamCliVerbs.cs:290-293` | Machine-readable contract, gate-tested |

**What step 14 licenses you to say:** "the holder of the key in `pub.pem` asserted, at a self-reported
`ts`, that a fetch of this URL produced markdown with this hash, and the bytes have not been altered
since." **Nothing more.**

---

## 7. Verification modes — enumerated from code

Nine mode surfaces across two programs, resolving to six distinct proof kinds. **The MCP tool and the
CLI are not a 1:1 surface.**

| Surface | Mode | Proof kind | Network | Needs original content | Proves | Notably does not prove |
|---|---|---|---|---|---|---|
| MCP `occam_verify` | `offline` (default) | envelope signature + optional content hash | no | optional (`markdown`, or capsule's own `content`) | C4 (+C2) | Nothing about key ownership. Time anchor reported but **non-gating** |
| MCP | `live` | C2/C5 re-derivation vs. a fresh fetch | **yes** | no | that a bare anonymous re-fetch now does/doesn't match | that the page changed (EF-012); why a re-fetch failed (CAP-653) |
| MCP | `prove` | emits a citation package for block *i* | no | needs `blockLeaves` | that supplied leaves reconstruct the signed root before proving | leaf-count integrity (tail-duplicate ambiguity) |
| MCP | `citation` | Merkle membership + envelope signature | no | no | C7 | truth, context, currency of the quote |
| MCP | `history` | watch-chain link + per-entry signature | no | no | consecutive seq + prev-hash links | **that the chain is signed at all** — unsigned entries skip the signature check (`WatchHistory.cs:155`) |
| MCP | *anything else* | silently falls through to `offline` | no | — | — | the response reports `"mode":"offline"` with no trace of the rejected input (EF-011) |
| CLI `occam verify` | `receipt` | as MCP `offline` **plus** time-anchor gating | no | optional | C4 (+C2 +C6-anchor) | key ownership |
| CLI | `citation` | as MCP `citation` | no | no | C7 | — |
| CLI | `manifest` | dataset row-set binding | no | needs the full export JSON | that exactly these rows, in this order, were signed together (`DatasetManifest.cs:96-114`) | row *content* — the manifest binds row identity only |
| CLI | `history` | as MCP `history` | no | no | chain links | same unsigned-chain gap; CLI reports no `signedCount` at all |
| CLI | *anything else* | **rejected**, exit 2 | — | — | — | — |

Asymmetries that matter:
- **`manifest` is CLI-only.** A pure-MCP agent structurally cannot verify a dataset manifest signature
  (CAP-283, EF-018).
- **`live` and `prove` are MCP-only.**
- **Time anchor gates the CLI verdict but not the MCP verdict** — the same receipt with a broken anchor
  is `"verdict":"verified"` over MCP and exit `1` on the CLI (`OccamCliVerbs.cs:282-288` vs
  `OccamVerifyTool.cs:158-164`).
- **`--pubkey` is mandatory on the CLI, optional on MCP** (defaults to the running host's own key).
- **Neither surface can verify a crosscheck verdict or an attest aggregate** — there is no mode for
  either, because neither is signed.
- **Neither surface is reachable through the friendly `occam` operator wrapper** (EF-025): the Node
  wrapper's closed subcommand table has no `verify` / `keys` entry and exits 1 with "unknown command".

---

## 8. Trust downgrades and silent behaviors

Cross-referenced with `FAILURE-BEHAVIOR-MAP.md` and `AUTOMATIC-BEHAVIORS.md`.

### 8.1 Automatic and unrequested (nobody asked for these)

| # | Behavior | Trigger | Visible? | Disableable? | Evidence |
|---|---|---|---|---|---|
| A1 | **Signing key minted on disk** | every host start, any transport | no | **no** — not gated by `OCCAM_RECEIPTS` | `OccamServiceCollectionExtensions.cs:23`; EF-044, ART-034, AUTO #1 |
| A2 | **Every eligible success is signed** | default-on (`OCCAM_RECEIPTS` unset → enabled) | `receipt` field | `OCCAM_RECEIPTS=off\|0\|false` | `ReceiptsPolicy.cs:10-17`; E-trust-state-blind §1.10 |
| A3 | **Every playbook save is signed, always** | `occam_playbook_save` success | `signedKeyId` | **no** — `ReceiptsPolicy` is never consulted | `PlaybookSaveService.cs:86-91`; EF-005, AUTO #14 |
| A4 | **`keys export` mints a key** if the target store is empty | `occam keys export --keys-root <empty dir>` | banner only | no | `OccamCliVerbs.cs:208-215`; CAP-902 §"key-store side effects" |
| A5 | **`playbook_policy=auto` forced** inside `claim_check` / `attest` / `dataset_export` | every call | no — no `playbookId` in the claim-check response | no parameter exists | `ClaimCheckService.cs:39`; CAP-693 |
| A6 | **Managed third-party backend may serve the content that gets signed** | operator has provider keys | not in the receipt | no per-call control | EF-003; CAP-054 |
| A7 | **`bypassCSP:true` + playbook `page.evaluate`** on every browser extract | browser backend | no | **no** | EF-046; AUTO #7/#12 |
| A8 | **Signed receipt written to disk cache and later replayed** | `cache_ttl_s>0` | `cached:true` outside the signature | eligibility rules | ART-035; `OccamTranscodeTool.cs:363-392` |

### 8.2 Silent downgrades (a weaker guarantee, reported as the same thing)

| # | Downgrade | Mechanism | Signal to the consumer |
|---|---|---|---|
| D1 | **Time anchor absent** — from unreachable/slow/malformed TSA | bare `catch { return null; }` | **none.** The receipt is byte-identical to one from a host with anchoring off. FAILURE-BEHAVIOR-MAP:19 |
| D2 | **Time anchor invalid** over MCP | reported as a non-gating field | verdict still `verified` |
| D3 | **Unsigned history chain verifies** | `Verify` skips the signature check when `Sig is null` | verdict `history_verified`; MCP reports `signedCount` separately, **CLI reports nothing** — `WatchHistory.cs:155` |
| D4 | **Merkle proofs survive `OCCAM_RECEIPTS=off`** | root/leaves computed from blocks, not from the receipt | `receipt`/`keyId` become null but `blockMerkleRoot` + per-match proofs still ship — internally consistent, cryptographically unanchored (CAP-699) |
| D5 | **Playbook tamper reported as "foreign author"** | `Inspect` compares the **unsigned** `provenance.keyId` and short-circuits to `unknown_key` before ever calling `Verify` | `unknown_key` instead of `invalid` — `PlaybookSignature.cs:128-134` (EFC-P5-05-1) |
| D6 | **Wrong key looks like tampering** | verdict vocabulary has no `wrong_key` | `signature_invalid` for both — `ReceiptVerifier.cs:11-14`; worst over MCP where the key silently defaults to the local host's |
| D7 | **Unknown verify mode silently downgrades to offline** | switch default arm | response claims `"mode":"offline"` — EF-011 |
| D8 | **Cache replay across URL fragments** | fragment dropped from the cache key | signed `url` may not match the requested URL — EF-045 |
| D9 | **`OCCAM_PROFILE=reader` produces receipts but hides the verifier** | `occam_verify` is `researcher+` while `occam_transcode` is in `reader` | `OccamToolProfile.cs:17-32` — a reader-profile agent can obtain signed receipts and has no MCP way to check any of them |
| D10 | **Community playbooks are integrity-checked, not authenticated** | `CommunityManifest` compares sha256 only; no signature at community load | a writer controlling both `manifest.json` and the playbook passes the gate (G-E-03) |
| D11 | **Release cosign bundle verifies nothing** | no shipped install path consumes it | EF-053 — "trust theater", ART-038 |

---

## 9. The ten framing questions, answered directly

**1. What can be cryptographically verified, and what cannot?**
*Can:* that a byte-string was signed by the holder of a given public key and is unaltered (C4); that a
`(text, selector)` pair is in the multiset committed by a signed Merkle root (C5/C7); that an exact
ordered set of dataset row identities was signed together (CLI `manifest`); that a watch chain's links
are consistent and that *those entries which carry a signature* are validly signed (C12).
*Cannot:* who the key belongs to; that a fetch occurred; that the origin served the content; the
truth, currency or context of any content; when anything happened (absent an anchor, and only
partially with one); that a consensus verdict or an attest tally is correct; that a page does not
contain a claim (only that Occam's ranker did not retrieve one).

**2. Receipts vs Merkle data vs capsules.**
The **receipt** is the only signed object. It commits to at most two hashes: `contentHash` over the
whole compiled markdown, and `blockMerkleRoot` over the ordered block leaves. **Merkle data** —
`blockLeaves`, individual leaves, proof arrays — is *never signed*; it is authenticated only
transitively, by reconstructing the signed root. A **capsule** is a transport container: an unsigned
base64url/JSON wrapper whose only signed element is the nested envelope, carrying alongside it the
markdown and leaves that the envelope's hashes let you check. So: receipt = the commitment; Merkle
data = checkable payload under that commitment; capsule = an unsigned envelope-plus-payload bundle
for hand-off. Compromising the wrapper cannot change what was signed; it can strip the anchor, alter
the advisory `verifyRecipe`, and remove or duplicate leaves in ways the root does not always catch.

**3. What do `occam_claim_check` and `occam_attest` actually guarantee?**
`occam_claim_check` guarantees: these specific extracted blocks cleared a BM25 lexical floor for your
claim string, and here is a Merkle proof that each was in the signed extraction. When `found:false`
and `proven:true`, it additionally guarantees the leaf set was untruncated — a *retrieval*-complete
negative. It performs **no stance evaluation whatsoever** (`Verdict` is hardcoded `not_evaluated`).
`occam_attest` guarantees: a hand-written regex classifier recognising two English claim shapes
returned this status, over the top-3 retrieved blocks, with one Merkle proof attached for the top
block only. Its aggregate response is **not signed**.
A reader of the names would assume "the claim was checked against the source and attested". The
honest reading is "text lexically similar to your claim was, or was not, retrieved from what we
extracted; and a narrow rule engine guessed at entailment."

**4. Trust status of a self-signed, auto-minted, locally-generated key.**
Occam mints `signing-key.pem` on first host start with no prompt, no passphrase, no OS keychain, no
hardware backing, unencrypted PKCS8, `chmod 600` on POSIX and **no hardening at all on Windows**
(`ReceiptSigner.cs:84-99`). There is no rotation, no expiry, no revocation, no registry.
- **(a) To the same machine:** meaningful, and this is its real use. It provides tamper-evidence and
  self-consistency: "this install said this, and the bytes have not been edited since." It is
  equivalent in strength to a local integrity log. Any process running as that user can read the key
  and forge arbitrary receipts; on Windows, any process with the user-profile ACL can.
- **(b) To a different user:** worth **only as much as the channel that delivered the PEM**. This is
  TOFU, structurally identical to pinning an SSH host key — with the extra hazard that the documented
  way to obtain a PEM (`occam keys export`) *creates* a key when pointed at an empty directory, so a
  confused consumer can silently pin a key that never signed anything. If they got the PEM from the
  same channel as the receipt, they have verified nothing an attacker who controls that channel could
  not have forged.
- **(c) To a third-party auditor:** **it proves nothing about the producer's identity.** An auditor can
  establish self-consistency of a corpus — "all of these receipts came from one key, and none were
  edited after signing" — which has genuine forensic value for detecting post-hoc tampering within a
  dataset. It establishes nothing about who ran Occam, whether the fetches happened, or whether the
  origin agreed. Any party who can write to `~/.occam/keys/` can mint an identity and back-date `ts`
  to any value. **A self-signed Occam signature must never be presented to an auditor as evidence of
  provenance.**

**5. What does `ok:false` mean, and what happens if an agent ignores it?**
`ok:false` means **the page content is UNKNOWN to the system.** It is not "the page is empty", not
"the page is short", and not licence to fill in from model memory. Occam's failure taxonomy exists to
make the unknown typed rather than silent (see `docs-audit/FAILURE-BEHAVIOR-MAP.md`). Two specific
traps: `thin_extract` means *bad extraction*, not a short page — a genuinely short good page is
`ok:true` with `quality.verdict=short_quality`; and a *negative receipt* is a signed statement of a
wall, so a signature on an `ok:false` response proves the failure was claimed, never that content
was obtained. **Failure mode when ignored:** the agent substitutes recalled or invented content and
proceeds. Because that content never entered Occam, it carries no hash, no receipt and no Merkle
leaf, so every downstream trust surface — `claim_check`, `attest`, `dataset_export`, `verify` — is
either bypassed or, worse, decorated with receipts belonging to *other* URLs that did succeed. This
is the single failure that converts the whole trust layer into decoration, and no mechanism in the
codebase detects it.

**6. Where does trust silently downgrade?** See §8.2 (D1–D11). The three most consequential: a TSA
failure vanishes without a trace (D1); an entirely unsigned watch chain returns `history_verified`
(D3); a tampered self-signed playbook can be relabelled a foreign author's by editing one unsigned
string (D5).

**7. Which trust behavior is automatic and unrequested?** See §8.1 (A1–A8). The headline three:
the key is minted on every host start whether or not receipts are enabled (EF-044); `occam_playbook_save`
signs unconditionally and `OCCAM_RECEIPTS=off` has zero effect on it (EF-005); every eligible success
is signed by default with no per-call opt-out.

**8. What is the time anchor and what does it actually anchor to?**
An opt-in RFC3161 timestamp token over `SHA-256(receipt signature bytes)` — not over the content, not
over the envelope (`TimeAnchorService.cs:35-37`). It anchors **the existence of the signature**, so it
proves the signed receipt existed no later than the TSA's `genTime`. It is off unless the operator
sets **both** `OCCAM_TIME_ANCHOR` and `OCCAM_TSA_URL`. It is fail-open: any error yields no anchor and
no warning. It rides as an unsigned sidecar not committed to by the envelope, so it can be stripped
without detection. Critically, **`TimeAnchorVerifier` never validates the TSA certificate chain to any
trust root** — it returns `signer.Subject` verbatim and defers the decision (`ReceiptTimeAnchor.cs:34-35`).
"Time anchor valid" therefore means "some certificate, whose issuer we did not check, signed this
imprint at this claimed time". Without an anchor, the only time evidence is the host's own clock in
the signed `ts` field.

**9. Is crosscheck agreement cryptographic evidence?**
**No.** Agreement is an observation, not a proof, and it is produced by one process comparing its own
fetches. It is signed only at the level of individual vantage receipts; the verdict and the divergence
pairs are ordinary unsigned JSON, and the response schema gives them the same visual weight as the
signed objects beside them. Multi-vantage agreement narrows one cloaking hypothesis (bot-vs-browser,
anon-vs-authed) from a single network origin. It is not multi-party attestation, there is no jury, no
remote node, no independent signer, and the "N of M nodes" design the SI-14 name evokes is explicitly
deferred and does not exist in the shipped product.

**10. Threat model.** See §10.

---

## 10. Threat model

### 10.1 In scope — Occam's trust layer meaningfully addresses these

| Threat | Mechanism | Strength |
|---|---|---|
| Post-hoc edit of a delivered receipt or its content | ECDSA over canonical bytes; `contentHash` recheck | Strong, gate-tested with four tamper vectors |
| Post-hoc edit of a quoted block | Merkle membership under a signed root | Strong for text+selector identity |
| Silent reordering / insertion / deletion in a dataset export | manifest root over ordered row leaves + one detached signature | Strong for row identity and order |
| Reordering / insertion / deletion in a watch history | hash chain where the link covers each entry *including* its signature | Strong **only if the entries are signed** — see D3 |
| Serializer/property-order drift silently changing signed bytes | hand-written fixed-order canonicalizer + a byte-for-byte golden vector in the gate | Strong; the correct mitigation and it exists |
| Malformed/hostile input to any verifier | every parser catches `JsonException`/`FormatException`/`CryptographicException` and returns a typed verdict | Strong, consistently applied (CAP-291) |
| A model inventing content on failure | typed `ok:false` + signed negative receipts for provable walls | Partial — it makes honesty *possible*; it cannot enforce it |
| SSRF via the one outbound trust call | `PrivacyClassifier` private-host refusal + `OutboundHttpGuard` connect callback on `receipts.timeAnchor` | Strong for this path (contrast EF-003, EF-043 elsewhere) |

### 10.2 Out of scope — explicitly not addressed

| Threat | Why Occam cannot help | Evidence |
|---|---|---|
| **A compromised host** | The host chooses every field and holds the key. A compromised host produces perfectly valid signatures over fabricated content. There is no remote attestation, no reproducible build binding, no independent witness. | Whole model |
| **An operator-controlled key** | The operator owns the key file and can back-date `ts`, sign anything, delete and re-mint. Nothing distinguishes an honest operator from a dishonest one. | `ReceiptSigner.cs:26-45` |
| **A lying origin** | Cloaking by IP/UA/cookie, A-B tests, personalization and deliberate deception are all faithfully hashed and signed as if genuine. `occam_crosscheck` narrows one axis from one egress and cannot distinguish malice from personalization. | CAP-859/863 |
| **Prompt injection inside fetched content** | `tag_trust` is an explicitly heuristic annotation, not a guarantee (tool description), it requires `json_blocks`, it is off by default, and the tag itself is **outside** the signed envelope. Injected text is hashed, signed and Merkle-provable exactly like real content. | `OccamTranscodeTool.cs:66`; EF-001 (tag replay via cache key) |
| **Playbook / browser code execution** | Browser contexts run `bypassCSP:true` unconditionally; playbook interaction plans reach `page.evaluate` and `waitForFunction`; css-extract runs `(0,eval)(__NUXT__)` on page-controlled data. A recipe is data that drives code execution against untrusted pages. | EF-046, EF-013, AUTO #7/#12/#22 |
| **Managed-provider intermediaries** | If the operator configures Jina/Firecrawl/Scrapfly/Spider, a third party sees the URL and returns the content Occam will sign. The `occam.managed` HttpClient has **no** `OutboundHttpGuard`. The receipt's `backend` field is the only trace, and no tool description discloses the possibility. | EF-003; CAP-054 |
| **Key distribution and identity** | No PKI, no registry, no well-known key endpoint, no certificate chain anywhere. TOFU only, and the export path can mint the very key it is meant to reveal. | CAP-288/903; `OccamCliVerbs.cs:208-215` |
| **Key compromise recovery** | No rotation, no expiry, no revocation list. The only remediation is deleting the PEM, which invalidates every prior receipt against the new key while old receipts remain forever verifiable against the retained old key. | CAP-255 |
| **Key at rest** | Unencrypted PKCS8. `TryHardenPermissions` is a **no-op on Windows**; on POSIX a failed `chmod` is swallowed silently. | `ReceiptSigner.cs:84-99`; PLATFORM-DIFFERENCES |
| **Multi-party / distributed attestation** | Does not exist. One key, one host, one process. | CAP-859 |
| **Supply chain of the host itself** | Docker HEALTHCHECK broken (EF-051); marketplace can auto-merge unvalidated community playbooks (EF-052); the cosign release bundle is verified by no shipped install path (EF-053). Community playbooks are sha256-integrity-checked but unauthenticated (G-E-03). | EF-051/052/053 |
| **Session/cookie bleed across extractions** | The browser pool reuses a `BrowserContext`; content extracted under a leaked session would be signed as normal. | EF-002/EF-040 |
| **Denial of the trust layer** | `OCCAM_RECEIPTS=off` removes signatures but not `contentHash`, not Merkle math, not the key file, and not playbook signing. There is no attested way for a consumer to learn that receipts were off. | `ReceiptsPolicy.cs`; EF-005/044; CAP-699 |

---

## 11. Engineering findings affecting trust (ledger references — no fixes proposed)

Canonical IDs from `ENGINEERING-FINDINGS.md`. Grouped by which trust property they erode.

| Property eroded | EF IDs |
|---|---|
| **Signing policy coherence** | EF-005 (playbook_save ignores `OCCAM_RECEIPTS`), EF-044 (key minted regardless of receipts) |
| **Verifier honesty / usability** | EF-011 (unknown mode → silent offline), EF-012 (live re-fetch drops context; failures collapse), EF-018 (manifest verify CLI-only; `dataset_export.ok` always true), EF-025 (trust verbs unreachable via the operator wrapper), EF-026/EF-027 (verification spec doc drift: stale binary name, raw `0x00` byte in the normative Merkle formula) |
| **Receipt fidelity** | EF-001 (cache key omits `emit_capsule`/`rank_blocks`/`tag_trust`), EF-045 (fragment omitted from cache + materialization key → cross-fragment replay), EF-006 (`occam_extract_knowledge` "Receipt" is telemetry, not a receipt), EF-008 (`"paywall"` negative-receipt branch unreachable) |
| **Content integrity at acquisition** | EF-003 (managed HttpClient unguarded), EF-013 (`eval` of page-controlled `__NUXT__`), EF-043 (css-extract lacks DNS-pin and body cap), EF-046 (`bypassCSP:true` + playbook `page.evaluate`), EF-002/EF-040 (browser-context session bleed) |
| **Playbook trust** | EF-047 (`PlaybookCommunitySanitizer` Core-dead; local save skips publish-sanitize), EF-048 (`WellKnownGenomeFetcher` empty-Content-Type bypass + read-before-truncate) |
| **Multi-source / aggregate trust** | EF-031 (crosscheck exempt from `OCCAM_PROFILE`, absent from server instructions), EF-032 (consensus gate is unit-only; no shipped re-derivation of the verdict) |
| **Retention / durability of trust state** | EF-037 (batch produces no Receipt v1, retains results indefinitely), EF-019/EF-020 (watch store races, no eviction), EF-054 (plaintext cookie retention) |
| **Distribution supply chain** | EF-051 (Docker health), EF-052 (marketplace auto-merge), EF-053 (cosign bundle nothing verifies) |
| **Budget-driven completeness** | EF-016 (claim_check and dataset_export apply no token budget) — load-bearing precondition of `leafSetComplete` |

Withdrawn and must not be revived: **EF-024** (claimed process-wide `FailureAtlasStore` leak; per-session DI re-verified in Wave 4).

---

## 12. Corrections to the prior audit model (code wins)

| # | Prior claim | Code | Correction |
|---|---|---|---|
| **X1** | `trust-receipts.md` CAP-281: the playbook signature "also vouches for what the SAVE-TIME gate computed, not just the recipe body." | `PlaybookSignature.cs` v1 excludes the whole top-level `provenance` key from the content hash and signs only `utf8("sha256:<hex>")`. **Phase 6.5:** v2 (`sigScheme=playbook-sig-v2`) signs `keyId/alg/contentHash/signedAt/verify{...}` under a domain-separated preimage. | **FALSE for v1**, **TRUE for v2 (integrity only).** Under v1 `score/passesGate/noiseLeakage/signedAt/keyId/alg` are unsigned and editable. Under v2 they are tamper-evident — but still a heuristic bound to a key, never a proof of quality/truth. |
| **X2** | `negative-space/E-trust-state-blind.md` G-E-09: "QualityGate heuristic scores are embedded in **signed** playbook provenance." | Same evidence as X1. | **v1:** scores are embedded but NOT signed (risk = "treating an unsigned, editable number as signed"). **v2 (Phase 6.5):** scores ARE signed (tamper-evident), so the residual risk shifts to "treating a signed heuristic as a quality proof" — still forbidden. |
| **X3** | `trust-receipts.md` CAP-284 and §15: windowed watch chains "verify correctly"; unsigned entries "still chain … only the per-entry authorship claim is lost." | `WatchHistory.cs:155` — `if (e.Sig is not null && !VerifyDetached(...)) return false;`. An entry with no `Sig` is never signature-checked. | Accurate as far as it goes, but the **verdict surface** was not modelled: a wholly unsigned, internally consistent chain returns `history_verified` over MCP and exit **0** on the CLI. "Verified" here can mean "correctly linked and signed by nobody." |
| **X4** | `trust-receipts.md` CAP-252 characterises the odd-level duplicate-last property as "non-fatal … extremely low practical risk with SHA-256". | `MerkleTree.cs:60,93,139` — duplicate-last in `Root`, `RootFromLeafHashes` **and** `Proof`. | The risk is not a hash collision; it is a **structural ambiguity** (the classic Bitcoin CVE-2012-2459 shape): for signed leaves `[A,B,C]`, the array `[A,B,C,C]` reconstructs the identical root. Since only the root is signed, `blockLeaves`-derived quantities — `blocksTotal`, `drift`, and the `prove` index range — are not signed quantities. It does **not** let an attacker prove a block that was never extracted. |
| **X5** | `ReceiptsPolicy.cs:4-6` doc-comment: centralized for "transcode / digest / claim-check / dataset". | Also gates watch and consensus (consensus via a **duplicated local parser**, `ConsensusService.cs:114-122`); does **not** gate `playbook_save`; does not gate key minting. | Confirms C6 in `CANONICAL-AUDIT-INDEX.md`: `OCCAM_RECEIPTS` is **not** a master switch. Never describe it as one. |
| **X6** | Corpus repeatedly frames `occam_verify` as the third-party verification surface. | `OccamVerifyTool.cs:40` defaults `public_key` to the running host's own key; `OccamToolProfile.cs:17-32` hides `occam_verify` from the `reader` profile while exposing `occam_transcode`. | Over MCP, `occam_verify` is by default a **same-instance self-check**. The CLI is the only surface that forces the caller to name the key they trust — and it is unreachable via the documented operator wrapper (EF-025). |

---

## 13. Claims we must never make

Each row is a forbidden public-docs statement plus the reason it is false or unprovable. These are
binding for all future user-facing writing about Occam.

| # | Forbidden claim | Why it is false or unprovable |
|---|---|---|
| 1 | "Cryptographically verified provenance" / "verified provenance" | The signature proves possession of a key that is bound to no identity. Provenance is precisely what is **not** established. `ReceiptVerifier.cs:19-21` says so in the source. |
| 2 | "Proves the page said this" | It proves the **host asserted** an extraction result. No origin signature, TLS transcript or independent witness is ever captured. |
| 3 | "Tamper-proof" | Tamper-**evident**, and only against the same key. A holder of the private key retroactively rewrites anything. |
| 4 | "Third-party verifiable" (unqualified) | Only with a correct public-key PEM obtained out of band, which this codebase does not distribute; and `occam keys export` mints a fresh key against an empty store. |
| 5 | "Signed by Occam" | Signed by *an* Occam install's auto-minted local key. There is no vendor key, no code-signing identity, no attestation of the binary. |
| 6 | "Timestamped" / "proves when the page was fetched" | Default `ts` is the signer's own clock. Even the opt-in RFC3161 anchor covers only the signature's existence, and its TSA certificate is never chained to a trust root. |
| 7 | "`occam_attest` attests to your citations" | It is an unsigned tally from a regex classifier that understands two English sentence shapes. Nothing about the aggregate is cryptographic. |
| 8 | "`occam_claim_check` proves the claim is absent from the page" | It proves no extracted block cleared a lexical BM25 floor over an untruncated leaf set. Paraphrase, images, unextracted regions and non-English phrasing are all out of reach. |
| 9 | "Consensus / crosscheck proves the content is genuine" | Same process, same egress IP, same proxy. Agreement excludes one cloaking axis and is an unsigned observation. |
| 10 | "Multi-node consensus" / "N-of-M attestation" | No remote node, no remote signer, no jury exists. Explicitly deferred. |
| 11 | "Capsules are signed bundles" | The capsule wrapper is unsigned; only the nested envelope is. `verifyRecipe` is unvalidated advisory text. |
| 12 | "Signed playbooks guarantee recipe quality" (or "signed quality score") | `verify.score` / `passesGate` sit inside the excluded `provenance` block and are **not covered by the signature** (§12 X1). They are a local substring/length heuristic besides. |
| 13 | "`OCCAM_RECEIPTS=off` turns signing off" | Playbook save still signs; the private key is still minted on disk; `contentHash` and Merkle proofs still ship. |
| 14 | "`history_verified` means the change history is signed" | An entirely unsigned chain returns `history_verified` / exit 0 (§12 X3). |
| 15 | "Verified means the content is accurate / trustworthy / true" | Every verify verdict in this codebase is about **bytes and keys**. None is about truth. |
| 16 | "Occam prevents prompt injection" | `tag_trust` is an off-by-default heuristic annotation carried **outside** the signature; injected text is hashed, signed and Merkle-provable like any other content. |
| 17 | "Occam's supply chain is signed / cosign-verified" | The release cosign bundle is consumed by no shipped install path (EF-053); community playbooks are sha256-integrity-checked but unauthenticated (G-E-03). |
| 18 | "The signing key is protected by the OS" | Unencrypted PKCS8; hardening is a **no-op on Windows**; a POSIX `chmod` failure is swallowed. |
| 19 | "A receipt proves the extraction was live/fresh" | Cached hits replay the stored signed envelope; `cached:true` is outside the signature. |
| 20 | "You can verify a dataset manifest from your agent" | Manifest verification exists **only** on the CLI, which is unreachable through the documented `occam` wrapper. |

---

## 14. New engineering-finding candidates (orchestrator allocates EF-058+)

| ID | Class | Confidence | Finding |
|---|---|---|---|
| **EFC-P5-05-1** | SECURITY-CANDIDATE | PROVEN in code | `PlaybookSignature.Inspect` (`PlaybookSignature.cs:128-134`) branches on the **unsigned** `provenance.keyId` and returns `unknown_key` **before** calling `Verify`. Editing that one unsigned string downgrades a detectable tamper (`invalid`) on a self-signed playbook into an innocuous "foreign author" verdict. Compounded by X1: `keyId`, `alg`, `signedAt` and the whole `verify{}` block are outside the signed bytes. |
| **EFC-P5-05-2** | BUG-CANDIDATE | PROVEN in code | `WatchHistoryChain.Verify` (`WatchHistory.cs:155`) skips the signature check for any entry with `Sig == null`. A fully unsigned, internally hash-consistent chain therefore yields `history_verified` via `occam_verify mode=history` and exit `0` via `occam verify --mode history`. MCP surfaces `signedCount` as a separate non-gating field; the CLI surfaces nothing. Extends the receipts-off degradation modelled at CAP-284/G-E-09 to the **verdict** surface. |
| **EFC-P5-05-3** | OBSERVATION | PROVEN by construction | Duplicate-last Merkle construction (`MerkleTree.cs:60,93,139`) makes leaf arrays `[…,X]` and `[…,X,X]` reconstruct the same root. Because only the root is signed and `blockLeaves` travels unsigned, leaf-count-derived values (`blocksTotal`, `drift`, `prove` index bounds) are not signed quantities. No new-block forgery is possible. |
| **EFC-P5-05-4** | DESIGN-QUESTION | PROVEN in code | `OCCAM_PROFILE=reader` exposes `occam_transcode` (a receipt producer) while hiding `occam_verify` (`OccamToolProfile.cs:17-32`) — a produce-but-cannot-verify surface with no in-band signal that verification exists. |
| **EFC-P5-05-5** | OBSERVATION | PROVEN in code | Verdict-vocabulary gap: `ReceiptVerification` (`ReceiptVerifier.cs:11-14`) has no `wrong_key` verdict, so "verified against the wrong public key" and "tampered" are indistinguishable — most dangerous over MCP, where the key silently defaults to the local host's (`OccamVerifyTool.cs:40`). Narrower and more actionable than the general CAP-288 PKI scope-cut. |

---

## 15. Uncertainties (bounded)

1. **Not runtime-reproduced.** Every claim here is source-derived. EFC-P5-05-1/2/3 are proven by
   construction but were not executed against a live host in this phase. Resolution: a gate test that
   (a) rewrites `provenance.keyId` on a self-signed playbook and asserts the `Inspect` verdict,
   (b) strips all `Sig` fields from a history chain, rebuilds `prevEntryHash`, and asserts
   `WatchHistoryChain.Verify`, (c) appends a duplicate tail leaf and asserts root equality.
2. **Canonicalizer independence.** Four hand-written canonical-byte functions (receipt, playbook,
   dataset manifest, watch entry) exist with **no domain-separation tag** (CAP-289). Cross-protocol
   confusion is prevented today only by the preimages being structurally different. Whether any two
   preimages can be made to coincide was not exhaustively analysed.
3. **`keyId` truncation.** 64 bits of SHA-256(SPKI). Not load-bearing for signature verification (the
   full PEM is), but it *is* the branch condition in `PlaybookSignature.Inspect`. A collision there
   would force a `Verify` call that then fails — i.e. it fails in the safe direction — but this was
   reasoned about, not tested.
4. **Crosscheck end-to-end.** Reconfirms EF-032: no gate test constructs a real `ConsensusService`, so
   "every vantage carries a signed receipt" remains static-read-only.
5. **Managed-provider receipts in the wild.** EF-003's path was not exercised; whether a managed-backend
   success is distinguishable in a receipt beyond the `backend` string was not confirmed empirically.
