# `occam_claim_check` — Deep Capability Audit (Wave 2)

**Source of truth: current executable code only.** `src/FFOccamMcp.Core/Tools/OccamClaimCheckTool.cs`,
`src/FFOccamMcp.Core/Claims/*.cs`, `src/FFOccamMcp.Core/Routing/TranscodePipeline.cs`,
`src/FFOccamMcp.Core/Tools/OccamTranscodeModels.cs` (shared receipt builders),
`src/FFOccamMcp.Core/Receipts/MerkleTree.cs`, `src/FFOccamMcp.Core/Semantics/SemanticOutcomeMapper.cs`,
`src/FFOccamMcp.Core/Client/ClientCapabilityStore.cs`, `benchmarks/l0-gate/ClaimCheckUnitTests.cs`.
`docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md` were **not** used as evidence.

**CAP ID range owned by this audit:** `CAP-690`–`CAP-719` (used: CAP-690…703; remainder reserved,
not exhausted). Heavily **reuses** Wave-1 IDs from `docs-audit/subsystems/trust-receipts.md` (S19,
CAP-250–291) — S19 already deep-audited the trust/Merkle/receipt layer and specifically named
`occam_claim_check` as the one caller of `LeafSetComplete` (CAP-263). This report does not re-derive
that material; it traces the tool's own schema→service→pipeline wiring and documents what is
genuinely specific to `occam_claim_check` versus what it inherits unmodified from `occam_transcode`'s
subsystems.

---

## 0. Entry point and schema

`OccamClaimCheckTool.Check` (`Tools/OccamClaimCheckTool.cs:19-25`) — MCP name `occam_claim_check`
(SI-16). Parameters:

```
claim (required), url (required), backend_policy = "http_then_browser",
session_profile = null, max_matches = 3
```

Both `claim` and `url` are required (empty/whitespace on either → `invalid_arguments` before any
network call, `OccamClaimCheckTool.cs:29-37`). `backend_policy` is validated with the **exact same**
parser as `occam_transcode` (`OccamBackendPolicyParser.TryParse`, reused, → **CAP-051**) — invalid
value is `invalid_arguments` (note: this tool does not use the `invalid_policy` code that
`occam_transcode` uses for the same failure — a minor code-vocabulary inconsistency between two tools
sharing one parser).

Delegate: `IClaimCheckService.CheckAsync` → `ClaimCheckService` (`Claims/ClaimCheckService.cs`), which
is the entire implementation. There is no separate "Claims" backend — it is a thin orchestration layer
over `TranscodePipeline` + `ClaimBlockRanker` + `MerkleTree` + the shared receipt builders.

---

## CAP-690 — `occam_claim_check` as a fact-grounding primitive (SI-16)

**Evidence:** `ClaimCheckService.CheckAsync` (`Claims/ClaimCheckService.cs:27-91`).

The tool's own doc-comment states its contract precisely and the code honors it: it extracts the page,
BM25-ranks extraction blocks against the claim text, and returns the top-N blocks that clear a
relevance floor, each with a Merkle citation proof — **or** an honest `found:false`. It never infers
support/refute — `Verdict` is hardcoded to `SemanticVerdict.NotEvaluated` on every success path
(`ClaimCheckService.cs:102`, reusing the shared constant from `Semantics/SemanticOutcomeMapper.cs:37`,
the same vocabulary `occam_transcode`/`occam_attest` share — **CAP-107**'s constant, not its mapping
logic). This is a retrieval tool wearing "claim" framing, not a fact-checker; the caller is expected to
read the returned block text itself and judge stance.

## CAP-691 — HIDDEN: `occam_claim_check` extraction is always fully uncapped (no token budget, ever)

**Evidence:** `ClaimCheckService` constructor injects `TranscodePipeline` and `ReceiptSigner` only —
**no `ClientCapabilityStore`** (contrast `OccamTranscodeTool`/`OccamDigestTool`, both of which inject
it — grep-confirmed: `ClientCapabilityStore` is referenced only in `OccamTranscodeTool.cs`,
`OccamDigestTool.cs`, `OccamClientCapabilitiesTool.cs`, `OccamServiceCollectionExtensions.cs`). The
`OccamTranscodeOptions` built in `CheckAsync` (lines 35-40) never sets `MaxTokens`, so it stays at its
record default (`null`). Tracing what a `null` `MaxTokens` means inside `TranscodePipeline`:
`Compile.BudgetOwnership.PrepareSurfaceBudget(null, …)` returns `new Prepared(null, null, null)`
(`BudgetOwnership.cs:46-49`) — a genuine **no-cap** path, not "cap defaults to ambient budget." The
ambient-20%-of-context-window default (**CAP-060**) is resolved **only** inside
`OccamTranscodeTool.cs:78` (`clientCapabilities.ResolveMaxTokens(max_tokens)`) *before* the options
object is built — that resolution step does not exist anywhere in `ClaimCheckService` or
`TranscodePipeline` itself. **Net effect: every `occam_claim_check` call compiles and ranks the FULL
page markdown with zero token budget**, regardless of whether the caller (or the operator via
`OCCAM_CLIENT_CONTEXT_TOKENS`) ever configured a client budget for `occam_transcode`/`occam_digest`.
This is invisible from the tool's schema (no `max_tokens` parameter exists on `occam_claim_check` at
all) and is the load-bearing precondition for **CAP-263**'s `leafSetComplete` claim (the code comment
at `ClaimCheckService.cs:57-60` states this explicitly: "claim_check never sets max_tokens/fit, so no
compile pruning"). The only ceiling still in effect is the operator-wide response-body-byte cap
(**CAP-101**, unrelated to token budgeting) and the backend's own wall-clock timeout.

## CAP-692 — `json_blocks` forced on, unconditionally, not caller-controlled

**Evidence:** `ClaimCheckService.cs:37` — `JsonBlocks = true` is a hardcoded options field, not derived
from any tool parameter (`occam_claim_check` has no `json_blocks` parameter at all). This differs from
**CAP-078**'s "always computed internally, optionally projected" story for `occam_transcode`: here the
*projection* itself (not just the internal DOM walk) is unconditionally on, because the ranker and
Merkle-leaf builder both require `outcome.Blocks` to be non-null. `json_tables`/`json_feed` are never
requested (options left at their `false` defaults) — table/feed data is computed internally per
**CAP-078** but never reaches this service or its response.

## CAP-693 — HIDDEN: `playbook_policy` is forced to `Auto` on every call, no opt-out

**Evidence:** `ClaimCheckService.cs:39` — `PlaybookPolicy = Playbooks.PlaybookPolicy.Auto` is
hardcoded. `occam_claim_check` exposes **no `playbook_policy` parameter** at all, so a caller cannot
even discover, let alone disable, this. This is the **opposite default** from `occam_transcode`, where
`playbook_policy` defaults to `off` and the caller must opt in (**CAP-070**). Consequence: every single
`occam_claim_check` call runs `PlaybookSeedResolver.ResolveExtended` (tiered local → `WT_PLAYBOOKS_PATH`
→ community → seeds lookup) and, if a playbook resolves and its overlay actually matches
(`OverlayApplied`), that playbook's selectors/backend-preference shape the extraction before ranking —
silently, with no `playbookId`/`playbookVersion` surfaced anywhere in
`OccamClaimCheckSuccessResponse` (unlike `occam_transcode`'s response, which does carry `PlaybookId`/
`PlaybookVersion` when an overlay applied). A caller comparing a claim-check result against a
plain-`occam_transcode` result for the same URL could see different extracted text purely because one
path silently used a playbook overlay and the other (with `playbook_policy` left at its own default
`off`) did not.

## CAP-694 — `max_matches` — silent clamp, not a validated range

**Evidence:** `ClaimCheckService.cs:77` — `Math.Clamp(maxMatches, 1, 10)`. Values `<1` are silently
raised to `1`; values `>10` are silently capped at `10`; there is no `invalid_arguments` failure for an
out-of-range or even negative `max_matches` — the tool's schema description ("1-10. Default 3") is
enforced by clamping, not by rejecting the call.

## CAP-695 — `ClaimBlockRanker` — dedicated BM25 relevance-floor ranker (distinct from `MapLinkRanker`/`FitMarkdown`)

**Evidence:** `Claims/ClaimBlockRanker.cs`. Same BM25 family (`k1=1.2`, `b=0.75`) as
`FitMarkdown`/`FocusMatcher`/`BlockSalience` (**CAP-063/064/087**) and structurally similar to
`Services/MapLinkRanker` (per this file's own doc-comment, "the same k1/b formula as
`Services.MapLinkRanker`, generalised over block text") — but it is its **own independent
implementation**, not a shared call into either of those, with claim-specific behavior:
- Tokens ≥3 chars only (`WordRegex`, `[a-z0-9]{3,}`), case-folded.
- Term matching allows **prefix-family** overlap (`token.Length>=4 && term.StartsWith(token)`, or vice
  versa) in addition to exact match, plus a substring fallback (`docText.Contains(term, OrdinalIgnoreCase)`
  scored as `tf=1` even if the regex tokenizer missed it) — a deliberately loose recall net so a claim
  phrased with a slightly different word form than the source text can still be judged as covering that
  term.
- **`ClearsFloor`** (`ClaimBlockRank.ClearsFloor`, line 11): a block only counts as a match if it covers
  **≥ ceil(40% of the claim's distinct content terms)**, with a hard floor of at least 1 matched term.
  This is the honesty gate behind `found` — a single shared common word between claim and block is
  explicitly insufficient (verified in the gate: a claim sharing one term with "office/contact us"
  clears the floor for none of the three test blocks, `ClaimCheckUnitTests.cs:37-38`).
- Empty claim or zero blocks → empty ranking, not an error (`Rank` returns `[]`, `ClaimCheckService`
  then reports `found:false`).

## CAP-696 — Retrieval/stance decoupling contract (`Found` / `Retrieved` / `Verdict`)

**Evidence:** `ClaimModels.cs:21-42`, `ClaimCheckService.BuildResponse` (lines 93-108).
`Retrieved` is a doc-commented **alias of `Found`** — in the current implementation the two fields are
always set to the identical value (`Retrieved: found` at every call site) — i.e. `Retrieved` carries no
independent information today; it exists purely so a caller cannot misread `Found` as "the claim is
supported" (the doc-comment: *"PR-F alias clarifying that `Found` is retrieval relevance, not
support"*). `Verdict` is always the literal string `"not_evaluated"` — the `SemanticVerdict` enum
defines `Supported`/`Refuted`/`Contradicted` values (**CAP-107**'s vocabulary) but **nothing in this
tool ever produces them** — there is no stance-classification code path here at all; this is a
deliberate, honestly-labeled non-goal, not an unfinished feature silently defaulting.

## CAP-697 — `LeafSetComplete` → `Proven` wiring (consumption of CAP-263)

**Evidence:** `ClaimCheckService.cs:56-67, 87-90`. This is the one live call site for **CAP-263**
(Wave-1 already identified this); documented here from the claim_check side as the actual decision
table:

| `blocks.Count` | `found` | `complete` (`!outcome.Truncated`) | `Proven` |
|---|---|---|---|
| `0` | `false` (nothing to rank) | n/a | `false` (absence NOT proven — no leaves exist to attest over) |
| `>0` | `true` (≥1 block clears the floor) | any | `null` (omitted — "found" makes absence-proof moot) |
| `>0` | `false` (none clear the floor) | `true` | `true` — **grounded "no": the complete, untruncated leaf set provably does not contain matching text** |
| `>0` | `false` | `false` (would only happen if some future change added truncation) | `false` — cannot claim provable absence over a partial leaf set |

Given **CAP-691** (claim_check never truncates), the `complete=false` row is currently **dead in
practice** for this tool — `outcome.Truncated` should never be `true` on any live claim_check call,
making the `Proven:true` branch effectively the only reachable non-null-non-omitted outcome whenever
`found=false` and at least one block exists.

## CAP-698 — HIDDEN: `occam_claim_check` is never cached, with no way to request caching

**Evidence:** `Caching/TranscodeResponseCache.cs`/`TranscodeCacheEligibility.cs`/`TranscodeCacheKey.cs`
are referenced **only** from `Tools/OccamTranscodeTool.cs` (grep-confirmed) — `ClaimCheckService` calls
`TranscodePipeline.TranscodeAsync` directly and never touches the cache classes at all. Since
`occam_transcode`'s own caching (**CAP-085**) is implemented as tool-layer plumbing wrapped *around*
the pipeline call (not inside the pipeline), every other caller of `TranscodePipeline` — including this
tool — is automatically cache-blind. There is no `cache_ttl_s` parameter on `occam_claim_check` and no
way to make repeated claim-checks against the same URL reuse a prior extraction; each call is a fresh,
live, uncapped (**CAP-691**) extraction.

## CAP-699 — Merkle root/proof survive `OCCAM_RECEIPTS=off` (signature-independent citation math)

**Evidence:** `ClaimCheckService.cs:70-72, 87`. The ordered leaf hashes
(`MerkleTree.LeafHashesHex(merkleBlocks)`) and the root
(`var root = receipt?.BlockMerkleRoot ?? MerkleTree.RootFromLeafHashes(leaves);`) are computed
**directly from the extracted blocks**, independent of whether `effectiveSigner` is non-null. When
`ReceiptsPolicy.Enabled()` is `false` (**CAP-280**, `OCCAM_RECEIPTS=off`), `effectiveSigner` is `null`,
`BuildReceipt` short-circuits to a receipt-less telemetry object whose `.Signed` is `null`, so
`receipt` is `null` — but the `??` fallback still computes and returns a real `BlockMerkleRoot` and
real per-match `Leaf`/`Proof` values in every `Matches[]` entry. **Practical effect:** an operator who
disables receipt *signing* globally still gets fully-formed, internally-consistent Merkle proofs out of
`occam_claim_check` on every call — only the `Receipt`/`KeyId` fields (the cryptographic attestation
layer) become `null`; the proof-of-membership math itself is receipt-signing-independent. A caller
cannot cryptographically prove the root came from *this specific signer* without receipts on, but can
still locally verify "this leaf is consistent with this root" via `MerkleTree.VerifyProof` using only
data in the response — a graceful degradation, not an all-or-nothing feature.

## CAP-700 — Failure-path negative-receipt reuse (SI-03, unmodified from CAP-264)

**Evidence:** `ClaimCheckService.cs:44-53`, calling the exact same
`OccamTranscodeResponseBuilder.BuildNegativeReceipt` used by `occam_transcode`/`occam_dataset_export`/
`occam_crosscheck` (**CAP-264/279**). Same provable-unavailability gate
(`captcha_or_challenge`/`requires_login`/`paywall`(dead branch)/`401`/`403`/`404`/`410`); transient
failures (`timeout`/`network_error`/`workers_unavailable`) correctly get **no** negative receipt
(`negative` stays `null`). The failure response mirrors the request (`Url`, `Claim` both echoed back)
so a caller iterating many claims against many URLs can correlate failures without re-sending the
inputs.

## CAP-701 — Sidecar surface is deliberately narrow (no capsule, no time anchor, no media/tables/feed/chunks)

**Evidence:** `ClaimCheckService` never references `CapsuleCodec`, `TimeAnchorService`,
`MediaRefMapper`, table/feed/chunk types — confirmed via `OccamClaimCheckJsonContext`
(`ClaimModels.cs:55-64`), whose `[JsonSerializable]` allow-list contains only claim-check's own records
plus `MerkleProofStep`/`ReceiptEnvelope`/`ReceiptPlaybook` — no capsule, time-anchor, media-ref, table,
or feed types are even AOT-registered for this tool's response type. Even though the underlying
`TranscodePipeline` call always internally collects blocks+tables (**CAP-078**) and media refs
(**CAP-109**), none of that reaches the `occam_claim_check` JSON at all — this tool's response surface
is intentionally minimal (claim, found, matches, receipt) rather than a claim-flavored superset of
`occam_transcode`'s response.

## CAP-702 — Full inherited transport/security/routing stack (edges only, no new behavior)

**Evidence:** because `ClaimCheckService` calls the shared `TranscodePipeline.TranscodeAsync` overload
(the same entry point `occam_transcode` uses), `occam_claim_check` automatically inherits, unmodified:
SSRF/private-URL blocking (**CAP-100**), the `http_then_browser` cascade + managed-backend third rung
(**CAP-052/054**), robots/politeness throttling (**CAP-103**), domain-tier registry
(**CAP-104**), challenge/login/thin-extract post-processors in the documented order (**CAP-094/095/096/097**),
session-profile header/cookie/storageState resolution + path hardening (**CAP-068/069**), and
meta-refresh/PDF/feed/plain-text format dispatch (**CAP-059/080/110/111**) at the worker layer. None of
this is reimplemented or special-cased for claim-check — it is a genuine pass-through, which is a
correctness strength (one router to audit) but also means every finding from `occam_transcode`'s audit
about that shared machinery (e.g. **CAP-083**'s `diff_against`/blocks coupling — not reachable here
since claim_check never sets `diff_against`) applies identically here whenever relevant.

## CAP-703 — Per-match citation self-sufficiency (compact proof, no full `blocks[]` leak)

**Evidence:** `ClaimModels.cs:12-19`, `ClaimCheckService.cs:78-85`. Each `OccamClaimMatchInfo` bundles
its own `Leaf` + `Proof[]` (the `O(log N)` sibling path, **CAP-262**) alongside the matched block's
`Text`/`SourceSelector`/`Score` — sufficient on its own for a peer to run `occam_verify mode=citation`
against the receipt's `BlockMerkleRoot` **without ever receiving the full block array**. This is the
opposite of `occam_transcode`'s **CAP-083** finding (`diff_against` alone forces the complete `blocks[]`
into the response) — `occam_claim_check` was designed to leak only the minimum needed for verification
(one leaf + one proof per returned match), not the whole document's block set.

---

## Reused Wave-1 capabilities (no new behavior, cited for traceability)

| CAP | What | How claim_check uses it |
|---|---|---|
| CAP-051 | `backend_policy` parser (http / browser / http_then_browser) | Identical parse call, `OccamClaimCheckTool.cs:39` |
| CAP-052 | `http_then_browser` cascade | Full cascade runs inside `TranscodePipeline.TranscodeAsync` |
| CAP-054 | Managed backend 3rd-rung escalation | Reachable transparently if operator-configured; no claim_check-specific gating |
| CAP-059/080/110/111 | PDF / feed / meta-refresh / plain-text dispatch | Worker-layer, unmodified |
| CAP-068/069 | `session_profile` resolution + path hardening | `session_profile` passed straight through, `ClaimCheckService.cs:38` |
| CAP-070/071/072 | `playbook_policy=auto` resolution + soft overlay + preferred-backend override | Forced on, see **CAP-693** |
| CAP-078 | Always-on internal block/table DOM walk | Same features list construction (`TranscodePipeline.cs:44-47`) |
| CAP-090/091 | Receipt v1 positive/negative signing | Positive via `BuildReceipt` (CAP-278), negative via `BuildNegativeReceipt` (CAP-279/**CAP-700**) |
| CAP-094/095/096/097 | Post-processor pipeline (challenge/login/thin-extract) | Unmodified; drives claim_check's failure-path `outcome.FailureCode` |
| CAP-100 | SSRF / private-network guard | Same `FetchPreflight`/`PrivacyClassifier` path |
| CAP-103/104 | Robots throttle / domain-tier registry | Inherited, no override |
| CAP-107 | `SemanticVerdict` vocabulary (constants only) | `NotEvaluated` reused; mapping logic itself (Access/Focus/Completeness) NOT used — claim_check has no `semantic.*` fields |
| CAP-250 | `ReceiptEnvelope` schema | `Receipt` field is a bare `ReceiptEnvelope` |
| CAP-252/262 | `MerkleTree` core + membership proof | Core primitive for `Matches[].Leaf/Proof` and `BlockMerkleRoot` |
| CAP-263 | `LeafSetComplete` / provable absence | claim_check is the **only** call site (Wave-1 finding); wiring detailed in **CAP-697** |
| CAP-264/279 | Negative receipts | **CAP-700** |
| CAP-278 | `BuildReceipt` shared builder | `ClaimCheckService.cs:61-62`, only call site passing `leafSetComplete: complete` |
| CAP-280 | `ReceiptsPolicy` kill-switch | `ReceiptsPolicy.Enabled() ? signer : null` at `ClaimCheckService.cs:43`; interacts with **CAP-699** |

---

## Capability graph edges

```
TOOL:occam_claim_check|USES|CAP-690
TOOL:occam_claim_check|USES|CAP-691
TOOL:occam_claim_check|USES|CAP-692
TOOL:occam_claim_check|USES|CAP-693
TOOL:occam_claim_check|USES|CAP-694
TOOL:occam_claim_check|USES|CAP-695
TOOL:occam_claim_check|USES|CAP-696
TOOL:occam_claim_check|USES|CAP-697
TOOL:occam_claim_check|USES|CAP-698
TOOL:occam_claim_check|USES|CAP-699
TOOL:occam_claim_check|USES|CAP-700
TOOL:occam_claim_check|USES|CAP-701
TOOL:occam_claim_check|USES|CAP-702
TOOL:occam_claim_check|USES|CAP-703
TOOL:occam_claim_check|USES|CAP-051
TOOL:occam_claim_check|USES|CAP-052
TOOL:occam_claim_check|USES|CAP-068
TOOL:occam_claim_check|USES|CAP-069
TOOL:occam_claim_check|USES|CAP-070
TOOL:occam_claim_check|USES|CAP-078
TOOL:occam_claim_check|USES|CAP-090
TOOL:occam_claim_check|USES|CAP-091
TOOL:occam_claim_check|USES|CAP-094
TOOL:occam_claim_check|USES|CAP-095
TOOL:occam_claim_check|USES|CAP-096
TOOL:occam_claim_check|USES|CAP-097
TOOL:occam_claim_check|USES|CAP-100
TOOL:occam_claim_check|USES|CAP-103
TOOL:occam_claim_check|USES|CAP-104
TOOL:occam_claim_check|USES|CAP-107
TOOL:occam_claim_check|USES|CAP-250
TOOL:occam_claim_check|USES|CAP-252
TOOL:occam_claim_check|USES|CAP-262
TOOL:occam_claim_check|USES|CAP-263
TOOL:occam_claim_check|USES|CAP-264
TOOL:occam_claim_check|USES|CAP-278
TOOL:occam_claim_check|USES|CAP-279
TOOL:occam_claim_check|USES|CAP-280
PARAM:claim|ENABLES|CAP-690
PARAM:url|ENABLES|CAP-690
PARAM:backend_policy|ENABLES|CAP-051
PARAM:session_profile|ENABLES|CAP-068
PARAM:max_matches|ENABLES|CAP-694
CAP-690|ROUTES_TO|TranscodePipeline
CAP-691|ROUTES_TO|BudgetOwnership.PrepareSurfaceBudget(null)
CAP-692|PRODUCES|blocks
CAP-693|ROUTES_TO|PlaybookSeedResolver
CAP-695|CONSUMES|blocks
CAP-695|PRODUCES|ClaimBlockRank[]
CAP-697|CONSUMES|CAP-263
CAP-697|PRODUCES|proven
CAP-698|FALLS_BACK_TO|no-cache (live extraction every call)
CAP-699|PRODUCES|BlockMerkleRoot
CAP-699|PRODUCES|MerkleProofStep[]
CAP-699|CONSUMES|CAP-280
CAP-700|PRODUCES|negative-receipt
CAP-700|CONSUMES|CAP-264
CAP-701|PRODUCES|(nothing: capsule/timeAnchor/media/tables/feed/chunks absent by design)
CAP-702|ROUTES_TO|HttpExtractBackend
CAP-702|ROUTES_TO|BrowserExtractBackend
CAP-702|FALLS_BACK_TO|ManagedExtractBackend
CAP-703|PRODUCES|citation-proof (per-match)
CAP-703|CONSUMES|CAP-262
```

---

## Cross-cutting category checklist

| Category | Status |
|---|---|
| proxy | Not used directly by this tool; inherited transparently via `TranscodePipeline`/worker egress (**CAP-102**) — not tool-specific. |
| session | **Used** — `session_profile` param → `SessionProfile` option, full CAP-068/069 stack. |
| cookies | Inherited via session_profile → Playwright storageState / HTTP header injection (CAP-068), not tool-specific. |
| headers | Inherited (session-profile header merge), not tool-specific. |
| http | **Used** — `backend_policy=http` reaches `HttpExtractBackend` exactly as in transcode. |
| browser | **Used** — `backend_policy=browser`/cascade escalation reaches `BrowserExtractBackend`. |
| managed | **Used (opt-in, transparent)** — reachable if operator has `OCCAM_MANAGED_PROVIDER` configured; no claim_check-specific gating (CAP-054). |
| retry | Not used — no retry logic in `ClaimCheckService`; same "cascade, not retry" story as CAP-098's parent tool. |
| cache | **Confirmed NOT used** — see **CAP-698**; no `cache_ttl_s` parameter, no cache-layer call. |
| diff | Not used — no `diff_against`/`if_none_match`/`delta_only` parameters exist on this tool. |
| blocks | **Used, forced on** — see **CAP-692**; internal, not response-projected as a raw `blocks[]` array (only per-match text/leaf/proof is projected). |
| tables | Computed internally (CAP-078) but **never surfaced** — see **CAP-701**. |
| chunks | Not used — no `semantic_chunking` parameter or wiring. |
| budget | **Confirmed NOT used — always uncapped**, see **CAP-691** (the most significant hidden finding in this audit). |
| receipts | **Used** — positive (CAP-278/CAP-699) and negative (CAP-279/CAP-700), gated by `ReceiptsPolicy` (CAP-280). |
| merkle | **Used, central to the tool** — `MerkleTree` leaf/root/proof primitives (CAP-252/262), survives receipts-off (CAP-699). |
| capsules | **Not used** — `emit_capsule` has no equivalent here; see CAP-701. |
| playbooks | **Used, forced auto** — see **CAP-693** (opposite default polarity from `occam_transcode`). |
| datasets | Not used — no relationship to `occam_dataset_export`; separate call site of the same shared receipt builders. |
| claims | **This tool's entire purpose** — see CAP-690/695/696. |
| trust tags | Not used — `tag_trust`/`rank_blocks` have no equivalent parameter or wiring in `ClaimCheckService`. |
| screenshots | Not used — `capture_screenshot` has no equivalent parameter. |
| translate | Not used — `translate_to` has no equivalent parameter. |
| llms.txt | Not used — `prefer_llms_txt` has no equivalent parameter; claim_check always extracts the actual target URL. |
| feeds | Computed internally only if the page is a feed and the worker's own content-type dispatch fires (CAP-080); `json_feed` sidecar itself is never requested, so feed items are never surfaced even then — the raw feed-parsed markdown would still be ranked as blocks like any other page. |
| profile | Not applicable — `OCCAM_PROFILE` gating is at the MCP tool-registration layer (CAP-008/009), outside this tool's own logic. |
| env | No claim_check-specific environment variables were found; all env sensitivity is inherited (OCCAM_RECEIPTS, OCCAM_ALLOW_PRIVATE_URLS, session/proxy/robots vars) via the shared pipeline. |

---

## Failure codes reachable on this tool's path

Same taxonomy as `occam_transcode`'s extraction path (**CAP-105**), since failures come straight from
`TranscodePipeline.TranscodeAsync`'s `outcome.FailureCode`: `invalid_arguments` (claim_check's own
pre-flight, plus `invalid_arguments` for bad `backend_policy` — note, NOT `invalid_policy` as transcode
uses), `extraction_failed` (generic fallback when `outcome.FailureCode` is null but `Ok=false`),
`timeout`, `thin_extract`, `captcha_or_challenge`, `requires_login`, `http_4xx`/`http_5xx`,
`response_too_large`, `private_url_blocked`, `dns_error`, `tls_error`, `network_error`,
`workers_unavailable`. Every failure response carries `Url`/`Claim` echoed back plus an optional
negative receipt (**CAP-700**) when the failure is provable.

---

## Security summary

- Inherits the full SSRF/private-URL/redirect-revalidation stack unmodified (**CAP-100**) — no
  claim_check-specific bypass exists.
- `session_profile` path-traversal hardening (**CAP-069**) applies identically.
- The BM25 ranker (**CAP-695**) operates only on already-extracted text; it introduces no new
  attacker-input surface beyond what `occam_transcode` already accepts (the `claim` string itself is
  never interpreted as a selector, URL, or code — it is pure tokenized text).
- Merkle proof/citation verification (`MerkleTree.VerifyProof`, reused) never throws on malformed input
  (CAP-252's hardening) — relevant here because `Matches[].Proof` values, once handed to a third party
  and round-tripped back into `occam_verify mode=citation`, are attacker-observable-but-not-attacker-
  forgeable without the private key.
- **CAP-691** (no token budget) is a resource-consumption note, not a security vulnerability per se,
  but it does mean a very large page fed to `occam_claim_check` incurs the full worker parse + BM25
  ranking cost over the entire document on every single call, with no caller-side lever to bound it
  (only the operator-wide `OCCAM_MAX_RESPONSE_BYTES` cap applies).

---

## Hidden / advanced findings (summary)

1. **CAP-691** — `occam_claim_check` never applies a token budget, not even the ambient
   `occam_client_capabilities` default — the tool has no `max_tokens` parameter and no
   `ClientCapabilityStore` wiring at all. Full-document extraction, every call.
2. **CAP-693** — `playbook_policy` is silently forced to `auto` with no parameter to disable it —
   opposite default polarity from `occam_transcode`, invisible from the schema.
3. **CAP-698** — no caching layer is reachable from this tool under any input; every call is a live,
   uncached extraction (consistent with "no file cache by design," but stronger here since there isn't
   even the opt-in `cache_ttl_s` escape hatch that `occam_transcode` has).
4. **CAP-699** — Merkle root and per-match proofs are computed and returned **even when
   `OCCAM_RECEIPTS=off`** — only the signature/`KeyId` disappear; the citation math itself has no
   receipts dependency.
5. **CAP-694** — `max_matches` outside `[1,10]` is silently clamped, never rejected, despite the
   parameter description reading like a validated range.
6. **CAP-696** — `Retrieved` is a fully redundant alias of `Found` in the current implementation (both
   always set from the same boolean) — present purely for API self-documentation, not distinct signal.

## Uncertainties

- Whether a future change could cause `outcome.Truncated` to become `true` for claim_check (e.g. if a
  response-size-cap partial-mode truncation, **CAP-101**, is ever wired to set that flag rather than
  only the token-budget compiler) — if so, the `complete=false` branch of **CAP-697**'s table would
  become reachable; not confirmed either way from the code read in this pass (response-body-cap logic
  lives in the Node worker, and whether/how it flows into `TranscodeOutcome.Truncated` on the C# side
  was not traced to completion here).
- Whether the `invalid_arguments` vs `invalid_policy` code discrepancy for a bad `backend_policy`
  (**CAP-051**, noted in §0) is an intentional per-tool choice or an oversight — not clarified in any
  code comment found.

## Completeness

COMPLETE for `occam_claim_check`'s own schema, service, and response-shape surface. Deep re-derivation
of the shared trust/receipts subsystem was deliberately avoided in favor of citing S19's existing
Wave-1 audit (`docs-audit/subsystems/trust-receipts.md`), per this audit's mandate to trace what is
tool-specific.
