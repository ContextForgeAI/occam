# `occam_verify` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only — `src/FFOccamMcp.Core/Tools/OccamVerifyTool.cs`,
`OccamVerifyModels.cs`, plus their direct call graph (`Receipts/*`, `Routing/TranscodePipeline.cs`,
`Watch/WatchHistory.cs`). Documentation was **not** used as evidence.

**CAP ID range owned by this report:** `CAP-650`–`CAP-689` (used: CAP-650…653; remainder reserved).
This tool's core mode/trust-primitive behavior was **already deeply audited in Wave 1 / S19**
(`docs-audit/subsystems/trust-receipts.md`, `CAP-250`–`CAP-291`) — that report is the primary reference
for the trust model, canonicalization, Merkle math, and CLI verb details; this report **does not
duplicate it**, only cites it, and adds what a fresh read of `OccamVerifyTool.cs`/`OccamVerifyModels.cs`
surfaces beyond it.

---

## 0. Entry point and schema

`OccamVerifyTool` (`src/FFOccamMcp.Core/Tools/OccamVerifyTool.cs`), DI ctor
`(TranscodePipeline pipeline, ReceiptSigner localSigner)`. Single `[McpServerTool]` method `Verify`:

```
receipt (required), markdown, public_key, mode = "offline",
block_index, block_text, block_selector, proof, chunks
```

Only `receipt` is required. `mode` accepts `offline|live|prove|citation|history` (case-insensitive via
`ToLowerInvariant`) — full mode semantics: **CAP-268…273** (trust-receipts.md).

---

## 1. Full parameter trace (schema → branch → response field)

| Param | Consumed by | CAP (existing) | Notes |
|---|---|---|---|
| `receipt` | capsule/envelope parser (lines 49-72) | CAP-269, CAP-274 | Universally parsed before mode dispatch except `history` — see **CAP-650**. |
| `markdown` | `ReceiptVerifier.VerifyOffline` | CAP-259 | Optional; enables `contentHashMatch`. Also filled from a capsule's own `content` if omitted (CAP-269). |
| `public_key` | `ReceiptVerifier.VerifyOffline`/`Citation` | CAP-259 | Defaults to `localSigner.ExportPublicKeyPem()` when omitted — MCP-tool-only convenience; the CLI has **no** such fallback (CAP-276 already flags this asymmetry). |
| `mode` | top-level `switch` (line 74) | CAP-268 | See **CAP-651** for the unmatched-value behavior. |
| `block_index` | `Prove` | CAP-271 | Required in `prove`; range-checked against `leaves.Length`. |
| `block_text` / `block_selector` | `Citation` → `MerkleTree.LeafHash` | CAP-272 | `block_selector` optional (empty string if omitted, per `MerkleTree` leaf preimage rule, CAP-252). |
| `proof` | `Citation` → `JsonSerializer.Deserialize<MerkleProofStep[]>` | CAP-272 | Malformed JSON → `invalid_arguments`, never throws. |
| `chunks` | `OfflineOrLiveAsync` (live only) → `ChunkStalenessEvaluator.Compute` | CAP-266, CAP-270 | JSON string array; malformed/absent → falls back to the receipt's own `leaves`. |

---

## 2. Pipeline edges — `live` mode re-fetch (new findings beyond CAP-270)

Wave 1's CAP-270 already established that `live` mode re-fetches `envelope.FinalUrl` through the
injected `TranscodePipeline` and is the one non-offline mode. Tracing `TranscodePipeline.TranscodeAsync`
itself (`Routing/TranscodePipeline.cs:25-85`) for this specific call site adds:

- The re-fetch is **not** a raw HTTP call — it runs the **full** pipeline: `PlaybookPolicy.ShouldApply`
  gate (options default `PlaybookPolicy=off` since `OccamVerifyTool` never sets it → **no playbook
  overlay** is applied on the re-fetch even if the original extraction used one), then
  `TranscodeCoreAsync` → `OccamRouter` cascade → the same `_postProcessors` chain (challenge/
  requires-login/thin-extract) that `occam_transcode` uses (`TranscodePipeline.cs:153-156`).
- The call passes a **bare** `new OccamTranscodeOptions { JsonBlocks = envelope.BlockMerkleRoot is not
  null }` (`OccamVerifyTool.cs:174`) — every other option (`SessionProfile`, `PlaybookPolicy`,
  `MaxTokens`, `ContentSelectors`, `FitMarkdown`, `FocusQuery`) is left at its type default. See
  **CAP-652**.
- Backend policy is hardcoded to `OccamBackendPolicy.HttpThenBrowser` — no parameter on `occam_verify`
  lets a caller pin `http` or `browser` for the re-fetch, even if the original receipt's `Backend` field
  says `browser` (already noted by Wave 1 CAP-270; restated here as a pipeline-edge fact).
- On re-fetch failure, `outcome.FailureCode`/`outcome.Message` (the same detailed taxonomy `occam_transcode`
  surfaces — `requires_login`, `captcha_or_challenge`, `timeout`, `http_4xx`, …) are computed by the
  pipeline but **discarded**: `OfflineOrLiveAsync` only checks `!outcome.Ok || markdown empty` and emits
  the single generic verdict `"refetch_failed"` with no failure-code field anywhere in
  `OccamVerifySuccessResponse`/`OccamVerifyLiveInfo`. See **CAP-653**.
- The re-fetch itself is never receipt-signed (it calls `pipeline.TranscodeAsync` directly, bypassing
  `OccamTranscodeTool`'s `BuildReceipt`/`BuildNegativeReceipt` call sites, CAP-278/279) — `live` mode
  produces a same-request comparison, not a second provable receipt a third party could chain to.

## Capability graph edges

```
TOOL:occam_verify|USES|CAP-250
TOOL:occam_verify|USES|CAP-251
TOOL:occam_verify|USES|CAP-252
TOOL:occam_verify|USES|CAP-259
TOOL:occam_verify|USES|CAP-261
TOOL:occam_verify|USES|CAP-262
TOOL:occam_verify|USES|CAP-263
TOOL:occam_verify|USES|CAP-265
TOOL:occam_verify|USES|CAP-266
TOOL:occam_verify|USES|CAP-268
TOOL:occam_verify|USES|CAP-269
TOOL:occam_verify|USES|CAP-270
TOOL:occam_verify|USES|CAP-271
TOOL:occam_verify|USES|CAP-272
TOOL:occam_verify|USES|CAP-273
TOOL:occam_verify|USES|CAP-274
TOOL:occam_verify|USES|CAP-650
TOOL:occam_verify|USES|CAP-651
TOOL:occam_verify|USES|CAP-652
TOOL:occam_verify|USES|CAP-653
PARAM:receipt|ENABLES|CAP-650
PARAM:mode|ENABLES|CAP-651
PARAM:mode=live|ENABLES|CAP-652
PARAM:chunks|ENABLES|CAP-266
PARAM:public_key|ENABLES|CAP-259
CAP-652|ROUTES_TO|TranscodePipeline
CAP-652|ROUTES_TO|OccamRouter/http_then_browser_cascade
CAP-652|FALLS_BACK_TO|thin_extract/requires_login/captcha_or_challenge (post-processors, silently collapsed — CAP-653)
CAP-269|CONSUMES|occam://capsule/... (produced by CAP-265, occam_transcode emit_capsule)
CAP-271|PRODUCES|citation_proof (leaf+MerkleProofStep[])
CAP-272|CONSUMES|citation_proof
CAP-273|CONSUMES|watch_history_chain (opt-in occam_watch, CAP-284)
CAP-268|ROUTES_TO|ReceiptVerifier.VerifyOffline (CAP-259)
CAP-268|ROUTES_TO|WatchHistoryChain.Verify (CAP-284)
```

---

## 3. New / non-obvious findings (CAP-650…653)

### CAP-650 — Capsule auto-detection is universal across modes, not offline-only

**Evidence:** `OccamVerifyTool.cs:49-72` — the `CapsuleCodec.IsCapsule(receipt)` check and the
envelope/leaves/anchor extraction happen **once, before** the `mode` switch, for every mode except
`history` (which branches even earlier). Wave 1's CAP-269 describes this under the "mode=offline"
heading, but the code shows `prove`, `citation`, and `live` all receive the *same* parsed
envelope/leaves/anchor — i.e. a caller can pass an `occam://capsule/...` string as `receipt` to `prove`
or `citation` mode too, not only to `offline`. This is a genuinely hidden capability: nothing in the
tool description or Wave 1's per-mode framing tells a caller that capsules work everywhere receipts do.

### CAP-651 — Unrecognized `mode` value silently falls back to offline verification

**Evidence:** `OccamVerifyTool.cs:74-80` — the mode switch's default arm (`_ =>`) runs the **offline**
path. There is no `invalid_arguments`/`invalid_mode` failure for a typo'd or unsupported mode string
(e.g. `mode="prov"`, `mode="Live "` with trailing space, `mode=""`). Compounding this, the response's
`Mode` field is set from the internal `live` boolean (`live ? "live" : "offline"`,
`OccamVerifyTool.cs:228`), **not** from the caller's original string — so the response looks like a
normal, deliberate `mode="offline"` call with no signal that the caller's actual input was rejected/
ignored. An agent debugging a bad mode string gets no error to act on.

### CAP-652 — `live` mode re-fetch drops session/playbook/budget context from the original request

**Evidence:** `OccamVerifyTool.cs:171-175`, `TranscodePipeline.cs:25-26,57` (default `OccamTranscodeOptions`
has `PlaybookPolicy` off). The live re-fetch is a **from-scratch, unauthenticated, playbook-less,
budget-default** transcode of `envelope.FinalUrl` — it does not know (and the `ReceiptEnvelope` schema,
CAP-250, does not carry) whether the original extraction used a `session_profile`, a resolved playbook
overlay, `content_selectors`, or a specific `backend_policy` other than the cascade default. Practically:
verifying a receipt from a login-walled page, or a page that only extracted cleanly under a playbook
overlay, will very plausibly report `"refetch_failed"` or a large `drift` even when the page content is
completely unchanged — the tool cannot distinguish "the page changed" from "my re-fetch lacks the
context the original had."

### CAP-653 — `live` mode collapses every re-fetch failure into one generic verdict, discarding the detailed failure taxonomy

**Evidence:** `OccamVerifyTool.cs:177-181`, cross-referenced against `TranscodeOutcome`
(`Routing/TranscodeOutcome.cs:5-12`, fields `FailureCode`/`Message` populated by the same
`FailureCodeStrings` normalizer `occam_transcode` uses, per Wave-1 CAP-105). `OccamVerifyLiveInfo`
(`OccamVerifyModels.cs:45-60`) has no field for the underlying failure code — a `requires_login` vs.
`captcha_or_challenge` vs. `timeout` vs. `http_404` re-fetch outcome are all indistinguishable to the
caller as `"refetch_failed"`. This is the same detailed information `occam_transcode` would have
returned had the caller called it directly instead of `occam_verify(mode=live)` — it is computed
internally and then thrown away at this call site specifically.

---

## 4. Cross-cutting capability checklist (per Wave 2 shared instructions)

| Category | Used by `occam_verify`? | Evidence |
|---|---|---|
| proxy | No (direct) | Only indirectly if `live` mode's re-fetch reaches `HttpExtractBackend`/`BrowserExtractBackend`, which do honor `OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY` (Wave-1 CAP-102) — not a verify-specific capability. |
| session/cookies/headers | **Not used** — `live` mode's re-fetch never carries a `session_profile` (CAP-652). |
| http / browser backends | Yes, transitively in `live` mode only (full `http_then_browser` cascade, CAP-652). |
| managed backend | Reachable transitively via the same cascade if operator-configured (Wave-1 CAP-054) — not verify-specific. |
| retry | No explicit retry parameter; internal cascade escalation only (same as transcode, Wave-1 CAP-098). |
| cache | Not used — `live` re-fetch is never eligible for `TranscodeResponseCache` because it doesn't go through `OccamTranscodeTool`'s cache-eligibility check at all (calls the pipeline directly). |
| diff | Yes — the whole point of `live`/`prove`/`citation` modes is a diff/proof primitive (CAP-262, CAP-266, CAP-270). |
| blocks / tables | Blocks: yes (`JsonBlocks` conditionally requested in `live`, CAP-270/652). Tables: not requested or compared at all in this tool. |
| chunks | Yes — `chunks` param, SI-12 (CAP-266). |
| budget (max_tokens) | **Not used** — `live` re-fetch never sets `MaxTokens`; the comparison is against a token-budget-default extraction regardless of what budget produced the original receipt (part of CAP-652). |
| receipts / merkle / capsules | Core purpose of the tool — CAP-250-253, 258-274. |
| playbooks | **Not applied** on the `live` re-fetch (CAP-652); playbook *signature* inspection is a separate tool (`occam_playbook_resolve`, CAP-282) not touched here. |
| datasets | Not used directly; dataset manifests are CLI-only (`verify --mode manifest`, CAP-283) — the MCP tool has no dataset mode. |
| claims | Not used directly (separate tool `occam_claim_check`); shares the same `ReceiptVerifier`/`MerkleTree` primitives. |
| trust tags (`tag_trust`) | Not applicable — `occam_verify` operates on already-extracted receipts/leaves, not live block trust tagging. |
| screenshots | Not used. |
| translate | Not used. |
| llms.txt | Not used. |
| feeds | Not used. |
| profile (env) | Indirect only, via the re-fetch's underlying backends in `live` mode (proxy/robots/domain-tier env vars) — no `occam_verify`-specific env var found. |

---

## 5. HIDDEN / NON-OBVIOUS CAPABILITIES

Capabilities a user would **never** guess from the tool's short MCP description ("Verify or cite an
extraction receipt... offline / live / prove / citation / history"):

1. **CAP-650** — capsules work as `receipt` input for `prove` and `citation` modes too, not just
   `offline`/implicitly `live`.
2. **CAP-651** — a mistyped `mode` value is not an error; it silently runs `offline` verification and
   the response claims `"mode":"offline"` with no trace of the rejected input.
3. **CAP-652** — `mode=live`'s "did the page change" verdict is really "did the page change **assuming
   an anonymous, playbook-less, default-budget HTTP-then-browser fetch of it right now**" — a receipt
   from an authenticated or playbook-assisted extraction cannot be faithfully re-verified live.
4. **CAP-653** — `live` mode cannot tell a caller *why* a re-fetch failed (login wall vs. challenge vs.
   network) — only that it failed.
5. (Restated from Wave 1, still worth surfacing here) **CAP-283** — there is no MCP `manifest` mode;
   dataset-export manifest verification is CLI-only.
6. (Restated from Wave 1) **public_key** defaults silently to this host's own local signer's public key
   when omitted — a caller who forgets to pass the *actual* signer's public key for a receipt produced
   elsewhere gets a `signature_invalid` verdict that looks like tampering rather than "wrong key,"
   because `ReceiptVerifier.VerifyOffline` does not distinguish the two causes in its verdict vocabulary
   (CAP-258).

---

## 6. CLI parity note

`occam verify` (CLI, `Cli/OccamCliVerbs.cs:217-409`, fully detailed in CAP-275/276) and the MCP
`occam_verify` tool are **not** a 1:1 surface:

- MCP has `live` and `prove` modes; the CLI has **neither** (CLI modes: `receipt|citation|manifest|history`).
- CLI has a `manifest` mode (dataset-export manifest verification, CAP-283); MCP has **no** equivalent.
- CLI's `receipt` mode (≈ MCP `offline`) additionally folds time-anchor validity into the pass/fail
  verdict; MCP's `offline`/`live` modes report `TimeAnchor` as a separate, non-gating field
  (`OccamVerifyTimeAnchorInfo`) — a receipt with an invalid time anchor can still report
  `verdict:"verified"` via the MCP tool while the CLI would call the same receipt not-verified.
- CLI always requires `--pubkey` explicitly; MCP's `public_key` is optional (defaults to the local
  signer's own key, see finding 6 above).

No deeper CLI trace performed here per assignment scope ("CLI parity note only") — see CAP-275/276 in
`docs-audit/subsystems/trust-receipts.md` for the full CLI audit.

---

## 7. Unresolved items

- Whether `ConsensusService`/`occam_crosscheck` (opt-in) receipts are verifiable via `occam_verify`
  without modification was not re-checked here — Wave 1 already flagged the same gap generally
  (trust-receipts.md §13, no dedicated gate file found).
- Exact behavior when `receipt` is a capsule string AND `mode=history` was not tested/traced — the code
  branches to `History(receipt, ...)` before any capsule detection, so a capsule passed under
  `mode=history` would fail `ParseHistory`'s array/`{history:[...]}` shape check and return
  `invalid_arguments`; this was read from the code but not exercised at runtime.
