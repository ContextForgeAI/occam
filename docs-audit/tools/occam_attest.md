# `occam_attest` — Deep Capability Audit (Wave 2)

**Source of truth:** current executable code only (`src/FFOccamMcp.Core/Tools/OccamAttestTool.cs`,
`src/FFOccamMcp.Core/Attest/*.cs`, `src/FFOccamMcp.Core/Claims/*.cs`, `src/FFOccamMcp.Core/Receipts/*.cs`,
`benchmarks/l0-gate/AttestUnitTests.cs`). `docs/*.md`, `MCP_API_SPEC.md`, `AGENTS.md` were **not** used
as behavioral evidence — they were skimmed only after the code trace, to spot-check for doc-vs-code drift,
never to derive a claim. Every claim below cites a file/line region read directly.

**CAP ID range owned by this audit:** `CAP-720`–`CAP-749` (used: CAP-720…CAP-730; remainder reserved,
not exhausted). Wave-1 CAP IDs are reused wherever `occam_attest` activates already-inventoried behavior
(trust-receipts subsystem CAP-250…291, transcode subsystem CAP-050…112) rather than re-minting.

---

## 0. Entry point and schema

`OccamAttestTool` (`Tools/OccamAttestTool.cs:18-69`) is the sole MCP handler. Its
`[McpServerTool(Name = "occam_attest")]` method takes exactly three parameters:

```
claims (string, required — JSON array of {"claim","sourceUrl"} rows)
backend_policy (string, default "http_then_browser")
session_profile (string?, default null)
```

`claims` is deserialized via a source-generated `JsonSerializerContext`
(`OccamAttestJsonContext`, `AttestModels.cs:81-92`) into `OccamAttestClaimInput[]`. Validation order
(`OccamAttestTool.cs:31-59`): (1) non-empty/non-whitespace string, (2) `backend_policy` must parse via
the shared `OccamBackendPolicyParser` (reuses **CAP-051**), (3) valid JSON, (4) non-null/non-empty array,
(5) `Length <= MaxClaims` where `MaxClaims = 50` (`OccamAttestTool.cs:20,56-59`). Any failure short-circuits
before any network call, returning `{ ok:false, failure:{code:"invalid_arguments", message}, timestamp }`
— never a partial per-claim result.

The tool itself does no extraction — it delegates every claim, one at a time, to
`IAttestService.AttestAsync` (`AttestService.cs:20-50`), which in turn calls the **already-registered**
`IClaimCheckService.CheckAsync` (Wave-2 `occam_claim_check` audit owns CAP-690…719 for that service's own
internals; this report treats it as an upstream dependency and only documents what `occam_attest` does
*with* its output).

**Registration:** always-on core tool (`OccamMcpServerRegistration.OccamToolNames`,
`Transport/OccamMcpServerRegistration.cs:29,113-114`) — but profile-gated: exposed only in `full` and
`auditor` profiles, **hidden in `reader` and `researcher`** (`Transport/OccamToolProfile.cs:34-39`
`AuditorExtra`; reuses **CAP-008/CAP-384** profile machinery). DI: `AddSingleton<IAttestService,
AttestService>()` (`Composition/OccamServiceCollectionExtensions.cs:124`).

---

## CAP-720 — `occam_attest` three-layer orchestration (the tool's core product idea)

**File:** `Attest/AttestService.cs:20-124`, doc-comment `Tools/OccamAttestTool.cs:10-16`.

For each claim row, three **independent** layers are composed, and — this is the load-bearing design
invariant of the whole tool — none of them is allowed to promote another layer's evidence into "the claim
is true":

1. **Retrieval** (`ClaimCheckService.CheckAsync`, top-`K=3` via `RetrievalTopK` constant,
   `AttestService.cs:22,69`) — BM25-ranked candidate blocks from the cited `sourceUrl`, reusing
   `ClaimBlockRanker` (same k1/b formula as `Services.MapLinkRanker`). Retrieval score is read only to pick
   candidates; it is explicitly never treated as support (doc-comment + gate assertion, see CAP-721).
2. **Semantic classification** (`ClaimSemanticClassifier.Classify`, CAP-721) — a rule-based, network-free
   text-entailment approximator that returns one of five statuses.
3. **Merkle existence proof** (CAP-262, reused) — attached only for the **top-ranked** retrieved block
   (`matches[0]`, `AttestService.cs:89`), proving the cited text was physically present in the
   *signed* extraction of that URL, never that the claim about it is correct.

The three layers are strictly sequential and each is skippable independently: retrieval failure (URL
extraction error) skips 2 and 3 entirely and reports `status=unknown` (CAP-729); retrieval success with
zero matches still runs the classifier (which can still resolve to `unsupported` vs `unknown` — CAP-730)
but layer 3 has nothing to attach (`Proof`/`Leaf`/`BlockMerkleRoot` all `null` on the per-claim JSON, via
`JsonIgnoreCondition.WhenWritingNull` on every optional field, `AttestModels.cs:40-52`).

## CAP-721 — `ClaimSemanticClassifier` (the actual semantic engine — new, not in Wave-1 inventory)

**File:** `Attest/ClaimSemanticClassifier.cs` (464 lines, entirely gate-tested, zero network I/O).

This is the genuinely new piece of product logic Wave 1 did not surface (trust-receipts.md CAP-290 only
noted its *existence* and fail-closed framing in one sentence). It is a **hand-rolled, regex-driven,
two-claim-shape entailment classifier** — not an LLM call, not embeddings, not a generic NLI model:

- Recognizes exactly **two claim shapes** via anchored regex (`IsAClaimRegex`, `UsesClaimRegex`,
  lines 438-442): `"<subject> is [a|an|the] <object>"` and
  `"<subject> {uses|using|utilizes|utilise|utilising} <object>"`. Anything else → `TryParse` returns
  `null` → `Classify` returns `AttestStatus.Unknown` unconditionally (fail-closed on unparseable claim
  shape, line 36-38) — the classifier makes **no attempt** at general-purpose claim parsing; unsupported
  shapes are explicitly out of scope rather than guessed at.
- Per retrieved block, `ClassifyAgainstBlock` requires the claim's **subject phrase** to literally appear
  in the (punctuation-normalized, lower-cased) block text before considering support/contradiction at all
  (line 96-108) — object-only overlap without the subject caps out at `related` (or `unknown` if neither
  subject nor object present), never `supported`.
- IsA claims: affirmed copula-window match → `supported`; negated copula ("is not a"/"isn't a") in the
  same window → `contradicted`; a closed-set **incompatible type-head** check (`SoftTypeHeads` = library/
  module/framework/package/api/toolkit/runtime vs. `DataTypeHeads` = database/db/sql/engine/rdbms/orm/
  datastore/store, lines 13-21) also yields `contradicted` even with **no explicit negation** — e.g. claim
  "X is a database" against a block asserting "X is a library" is caught as a type-family conflict, not
  just missing evidence (`HasIncompatibleTypeAssertion`, lines 241-280). Object-token partial overlap
  without a copula predicate → `related`, never `supported`.
- Uses claims: affirmed/negated "uses" window match, same negation-aware pattern; no incompatible-type
  check (Uses claims have no closed type taxonomy).
- **Aggregation across multiple retrieved blocks** (top-K=3): `contradicted` from any block beats
  `supported` from another (line 68-70) — a single explicit refutation in the corpus wins over a
  co-occurring affirmation, a conservative (trust-preserving) tie-break. `related`-only findings for
  IsA/Uses claims are downgraded to `unsupported` (not `related`) — the doc-comment (lines 80-84) reasons
  that for these two claim kinds, topical co-occurrence without entailment is a **known-false-positive
  shape**, not genuine ambiguity, so it is reported more strongly than "related."

## CAP-722 — Claim shape grammar is a closed, hand-authored set (scope-cut, not a bug)

**File:** `ClaimSemanticClassifier.cs:338-375,438-442`.
Only `IsA` and `Uses` are recognized `ClaimKind`s. A claim like "React was created by Facebook" or "the
API returns JSON" has no matching regex and is unconditionally `unknown` — this is architecturally a v1
scope-cut (two claim shapes), not a partial-coverage bug; extending the taxonomy means adding a new
`ClaimKind`, a new regex, and a new `ClassifyAgainstBlock` branch, not tuning existing regexes.

## CAP-723 — Negation-aware entailment (contradiction detection, not just absence)

**File:** `ClaimSemanticClassifier.cs:174-239,282-336`.
Both claim kinds independently detect **explicit negation** in a bounded text window around the matched
copula/verb (`HasNegatedCopula`, `HasNegatedUses`) — contractions are normalized first (`isn't`→`is not`,
`aren't`→`are not`, `doesn't`→`does not`, `Normalize`, lines 377-388) so "X isn't a Y" and "X is not a Y"
classify identically. This is what lets the tool return `contradicted` (a claim the source explicitly
refutes) as distinct from `unsupported` (a claim the source is simply silent on) — a real product
distinction: an LLM report claiming something the cited page *actively denies* is a stronger authorship
failure than a claim the page merely doesn't mention, and the response schema tracks them as separate
named counters (see CAP-726).

## CAP-724 — Incompatible type-head assertion (implicit contradiction without negation)

**File:** `ClaimSemanticClassifier.cs:18-21,241-280`.
Distinct from CAP-723: this fires when the source text asserts a *different* type-family for the subject
than the claim asserts, with **no negation word anywhere** — e.g. claim "asyncio is a database engine"
against source "asyncio is a library" is `contradicted` purely from the `SoftTypeHeads`/`DataTypeHeads`
closed-set mismatch (verified in gate, `AttestUnitTests.cs:36-49`). This closed vocabulary (8 "soft"
heads, 8 "data" heads) is a deliberately narrow, extend-carefully set per the doc-comment (line 12) — it
will not catch arbitrary type mismatches outside these two families (e.g. "X is a car" vs "X is a fruit"
would not be caught by this path, only by the generic lack of copula/object match falling through to
`related`/`unsupported`).

## CAP-725 — Reason-code taxonomy attached to every non-`supported` result

**File:** `AttestService.cs:90-98`.
A `reason` string (nullable, omitted when `supported`) is derived from the final status **plus** whether
any blocks were retrieved at all:
`no_matching_block` (unsupported + zero matches) · `no_semantic_support` (unsupported + matches existed)
· `related_not_supported` · `contradicted_by_source` · `insufficient_confidence` (unknown) · plus the
early `invalid_arguments` reason for empty claim/URL rows (`AttestService.cs:63-64`) and the propagated
upstream `failure.Failure.Code` from `claim_check` when extraction itself failed (line 74-75, feeds
CAP-729). This is a genuinely new, attest-specific vocabulary — none of these six strings appear in the
Wave-1 failure-code taxonomy inventory (`docs-audit` failure-codes coverage is transcode-failure-code
oriented, e.g. `timeout`/`http_403`; these are semantic-outcome reasons, a different namespace entirely).

## CAP-726 — Aggregation / summary counts model (named partition + compat aliases)

**File:** `AttestModels.cs:63-74`, `AttestService.cs:129-175`.
`OccamAttestResponse` carries **both** a fully-named 5-way partition (`Supported`, `Contradicted`,
`Related`, `Unsupported`, `Unknown` — mutually exclusive, sums to `ClaimsTotal`) **and** two backward-compat
aggregates for callers still gating on a binary trust signal: `Grounded` (== `Supported` count) and
`UnsupportedTotal` (== `Contradicted + Related + Unsupported + Unknown`, i.e. "everything that is not a
clean pass"). The invariant `Supported + UnsupportedTotal == ClaimsTotal` is gate-verified
(`AttestUnitTests.cs:102-103`). `AttestClassifier.Summarize` (pure, static, network-free,
`AttestService.cs:143-174`) is the single aggregation function — reused nowhere else in the codebase
(grep-confirmed no other call site), so this counting logic is `occam_attest`-exclusive, not a shared
utility.

## CAP-727 — `claims` batch input contract (1–50 rows, fail-closed on the whole batch)

**File:** `OccamAttestTool.cs:20,51-59`.
Hard cap `MaxClaims = 50` rejects the **entire call** (not a partial/truncated batch) when exceeded — an
agent submitting 51 claims gets `invalid_arguments` for all of them, not the first 50 processed. This is
an all-or-nothing gate, distinct from e.g. `occam_digest`'s per-URL partial-success model (different tool,
different failure philosophy) — worth flagging because an agent porting digest-style expectations
("some URLs succeed, some fail, I get both") to attest will be surprised that a single malformed row
count overflow fails the whole batch before any network activity.

## CAP-728 — Per-claim serial fan-out of shared `backend_policy`/`session_profile`

**File:** `AttestService.cs:30-35`, `ClaimCheckService.cs:35-42`.
One `backend_policy` and one optional `session_profile` are parsed **once** per `occam_attest` call and
applied identically to **every** cited `sourceUrl` in the batch (there is no per-row override in the input
schema — `OccamAttestClaimInput` is only `{Claim, SourceUrl}`, `AttestModels.cs:26`). Claims are processed
in a plain sequential `for` loop with `await` per iteration (`AttestService.cs:30-35`) — **not**
parallelized (unlike `occam_digest`'s bounded-concurrency fan-out, a different subsystem) — so attesting
50 claims against 50 distinct slow pages is 50 sequential extractions, each up to the backend's own
timeout (35s HTTP / 120s browser, reused **CAP-187**). This is a real latency characteristic an agent
should know before batching many claims with `backend_policy=browser`.

## CAP-729 — Extraction failure → `status=unknown` (fail-closed, never "refuted")

**File:** `AttestService.cs:68-76`.
When the underlying `ClaimCheckService.CheckAsync` returns a failure (extraction failed, timeout, blocked,
etc. — the same failure-code taxonomy as `occam_transcode`, reused via `outcome.FailureCode`), attest maps
this to `status=unknown` with `reason=<upstream failure code>` — explicitly **not** `unsupported` or
`contradicted`. The doc-comment is precise about why: "cannot verify → unknown (not unsupported-as-
refuted)" (line 73). This is the single most important fail-closed decision point in the tool: a page that
is temporarily unreachable must never be reported as evidence *against* a claim.

## CAP-730 — `retrievalComplete` gate: distinguishes "provably absent" from "couldn't check"

**File:** `AttestService.cs:78-85`.
When retrieval succeeds but returns **zero** matching blocks, the classifier still needs to decide
`unsupported` vs `unknown` for that claim (see CAP-721's `Classify` zero-block branch). Attest computes
this via `retrievalComplete = matches.Length > 0 || success.Proven != false` — i.e. it reuses
`occam_claim_check`'s own **provable-absence** signal (CAP-263, `LeafSetComplete`/`Proven`) rather than
re-deriving completeness independently. Concretely: if the claim-check extraction was untruncated (a
complete, signed leaf set) and genuinely found nothing relevant, attest can confidently call the claim
`unsupported` (a real "no"); if the extraction was truncated/incomplete, the same zero-match result
degrades to `unknown` (fail-closed — "we don't actually know, we might have missed it"). This is a subtle,
correct piece of cross-layer reuse: a Wave-1 receipts primitive (CAP-263) built for `occam_claim_check`
directly determines a semantic-layer verdict in `occam_attest`, two tools and two files apart.

---

## Cross-cutting categories (explicit checks)

| Category | Status | Evidence |
|---|---|---|
| **proxy** | Inherited, not exposed | `ClaimCheckService` runs full `TranscodePipeline.TranscodeAsync`, same router as `occam_transcode` → egress proxy (CAP-102/157-166) applies transitively to every cited URL; no attest-level proxy param. |
| **session** | Used directly | `session_profile` is an explicit, top-level `occam_attest` parameter, applied uniformly to every claim (CAP-728); reuses CAP-068/191. |
| **cookies / headers** | Inherited via session_profile only | No independent cookie/header surface on `occam_attest` itself. |
| **http / browser** | Used directly | `backend_policy` (http / browser / http_then_browser), reuses CAP-051/052 verbatim — same parser, same router. |
| **managed** | Inherited, hidden | `ManagedExtractBackend` escalation (CAP-054) is reachable transitively through the shared `TranscodePipeline`/`OccamRouter` if the operator has managed-provider keys configured — **not** documented or mentioned anywhere in the tool's own description; an agent has no way to know a claim's page fetch might silently escalate to a third-party scraping API. |
| **retry** | Absent | No attest-level retry; inherits the transcode subsystem's documented no-auto-retry posture (CAP-188). |
| **cache** | Not used | `ClaimCheckService`'s `OccamTranscodeOptions` never sets `cache_ttl_s` — every attest call is a fresh, uncached extraction of the cited page. |
| **diff** | Not used | No `if_none_match`/`diff_against` wiring anywhere in the Attest/Claims path. |
| **blocks** | Forced on, always | `OccamTranscodeOptions.JsonBlocks = true` is hard-coded in `ClaimCheckService.cs:37` — attest **always** requests structured blocks regardless of caller intent; this is not a pass-through param, it's a mandatory internal dependency. |
| **tables** | Not used | `JsonTables` left default `false`. |
| **chunks** | Not used | No `semantic_chunking`/`chunks` wiring. |
| **budget** | Ambient, not attest-controlled | `OccamTranscodeOptions` never sets `MaxTokens` → the ambient client-capability default budget (CAP-060/304) applies to the underlying transcode call; this can in principle truncate the extract (`outcome.Truncated`), which is exactly what CAP-730's `retrievalComplete` gate is defending against. |
| **receipts** | Used, but attest doesn't build them | Attest's per-claim `Receipt` field is whatever `ClaimCheckService` produced (CAP-278/279 reused) — attest is a pure consumer, never signs anything itself. |
| **merkle** | Used directly (CAP-262 reused) | Top-match `Leaf`/`Proof`/`BlockMerkleRoot` surfaced per claim — see CAP-720. |
| **capsules** | Not used | `emit_capsule` never set; attest responses never contain `occam://capsule/...`. |
| **playbooks** | Forced on, hidden | `PlaybookPolicy = Playbooks.PlaybookPolicy.Auto` is hard-coded in `ClaimCheckService.cs:39` — genome-aware playbook resolution (CAP-070) is **always active** for every URL an attest claim cites, with no way to disable it per call. Not mentioned in the tool description. |
| **datasets** | Not used | No `occam_dataset_export` interaction. |
| **claims** | Core dependency | Entire tool is a semantic layer bolted onto `occam_claim_check`'s retrieval+proof output — see CAP-720. |
| **trust tags** (`rank_blocks`/`tag_trust`) | Not used | Neither flag is set by `ClaimCheckService`; blocks used for ranking here are plain `WorkerExtractBlockInfo`, not BM25-annotated/trust-tagged blocks. |
| **screenshots** | Not used | No `capture_screenshot` wiring. |
| **translate** | Not used | No `translate_to` wiring — claims and cited pages are matched in their original language only; no cross-language claim checking. |
| **llms.txt** | Not used | `prefer_llms_txt` left default `false` — attest never short-circuits through a site's `/llms.txt`. |
| **feeds** | Not used | `JsonFeed` left default `false`. |
| **profile** (`OCCAM_PROFILE`) | Gated | Exposed only in `full`/`auditor` profiles; hidden from `reader`/`researcher` (CAP-008/384 reused) — see §0. |
| **env** | None attest-specific | No environment variable read anywhere in `Tools/OccamAttestTool.cs` or `Attest/*.cs`; all env sensitivity (proxy, receipts kill-switch, keys root, etc.) is inherited transitively through the shared transcode pipeline and `ReceiptsPolicy`/`ReceiptSigner` singletons. |

---

## HIDDEN / NON-OBVIOUS CAPABILITIES

Capabilities a user would **never** discover from the tool's short MCP description
(`"Before shipping a report, check citations with a fail-closed trust model: ... Params: claims,
backend_policy, session_profile."`):

1. **`json_blocks` and `playbook_policy=auto` are silently forced on** for every cited URL — the
   description mentions none of this; a caller cannot turn either off, and genome-based playbook overlays
   (site-specific extraction recipes) can change what text is even available to attest against, invisibly.
2. **Managed third-party backend escalation (Jina/Firecrawl/Scrapfly/Spider, CAP-054) can fire** for any
   cited URL if the operator has configured managed-provider keys — the tool description gives zero
   indication that "checking a citation" might route the page fetch through an external paid API.
3. **No per-call token budget control** — the underlying extraction uses whatever ambient client-context
   budget is active; a large cited page can be silently truncated, and the *only* visible trace of this is
   the internal `retrievalComplete` gate downgrading a would-be `unsupported` verdict to `unknown` (a
   caller sees a status change, not a "truncated" flag).
4. **The classifier only understands two English claim shapes** (`X is [a] Y`, `X uses Y`) via fixed
   regex — any other phrasing (comparatives, negated premises phrased without "is not", multi-clause
   claims, non-English text) is unconditionally `unknown`, silently, with no "claim shape not recognized"
   signal distinct from "evidence ambiguous" in the response (both surface as bare `status=unknown`;
   `reason` for the pure-parse-failure case is simply omitted, whereas insufficient-evidence unknowns get
   `reason=insufficient_confidence` — but a caller must know to check for a *missing* reason to infer
   "unparseable claim shape," which is not documented anywhere).
5. **50-claim hard cap fails the entire batch**, not a partial/truncated set (CAP-727) — easy to assume
   digest-style graceful degradation.
6. **Claims are processed strictly serially** (CAP-728) — no concurrency, so latency scales linearly with
   claim count and each claim's own backend timeout (up to 120s for `browser` policy).
7. **`occam_attest` is invisible in the `reader` and `researcher` `OCCAM_PROFILE`s** — only `full` and
   `auditor` expose it; an agent running under a narrower profile will get a routing "unknown tool" error
   rather than any attest-specific message.
8. **Incompatible-type contradiction detection (CAP-724) fires without any explicit negation** — a report
   author might reasonably expect "contradicted" to require the source literally saying "is not," but the
   classifier will also flag a same-copula, different-type-family assertion as contradiction.
9. **The Merkle proof is attached only for the single top-ranked block** — even though retrieval fetches up
   to 3 candidates internally (`RetrievalTopK`), only `matches[0]`'s existence is provable in the response;
   the other 1-2 retrieved-but-unattached blocks that may have influenced the semantic verdict (aggregated
   across all retrieved blocks, CAP-721) leave no provenance trail of their own in the JSON.

---

## Capability graph edges

```
TOOL|USES|CAP-720
TOOL|USES|CAP-721
TOOL|USES|CAP-722
TOOL|USES|CAP-723
TOOL|USES|CAP-724
TOOL|USES|CAP-725
TOOL|USES|CAP-726
TOOL|USES|CAP-727
TOOL|USES|CAP-728
TOOL|USES|CAP-729
TOOL|USES|CAP-730
TOOL|USES|CAP-262
TOOL|USES|CAP-263
TOOL|USES|CAP-278
TOOL|USES|CAP-279
TOOL|USES|CAP-051
TOOL|USES|CAP-068
TOOL|USES|CAP-191
TOOL|USES|CAP-008
TOOL|USES|CAP-384
PARAM:claims|ENABLES|CAP-727
PARAM:backend_policy|ENABLES|CAP-051
PARAM:session_profile|ENABLES|CAP-068
CAP-720|ROUTES_TO|occam_claim_check
CAP-720|CONSUMES|ClaimCheckService
CAP-729|CONSUMES|ClaimCheckService
CAP-730|CONSUMES|CAP-263
CAP-720|PRODUCES|OccamAttestResponse
CAP-726|PRODUCES|OccamAttestResponse
CAP-720|PRODUCES|receipt
CAP-720|PRODUCES|merkle_proof
CAP-262|PRODUCES|merkle_proof
CAP-720|ROUTES_TO|http_extract_backend
CAP-720|ROUTES_TO|browser_extract_backend
CAP-720|FALLS_BACK_TO|managed_extract_backend
CAP-720|CONSUMES|session
CAP-720|CONSUMES|playbook_genome
occam_claim_check|ROUTES_TO|TranscodePipeline
TranscodePipeline|ROUTES_TO|OccamRouter
OccamRouter|FALLS_BACK_TO|managed_extract_backend
```

---

## Summary capability table (CAP-720 – CAP-730 minted; CAP-731–749 reserved, unused)

| CAP | Capability | Classification | File(s) |
|---|---|---|---|
| 720 | `occam_attest` three-layer orchestration | Public core | Attest/AttestService.cs |
| 721 | `ClaimSemanticClassifier` rule-based entailment engine | Public/advanced (new) | Attest/ClaimSemanticClassifier.cs |
| 722 | Closed 2-shape claim grammar (IsA/Uses only) | Architecture/scope-cut | ClaimSemanticClassifier.cs:338-375 |
| 723 | Negation-aware contradiction detection | Advanced | ClaimSemanticClassifier.cs:174-239,282-336 |
| 724 | Incompatible type-head contradiction (no negation needed) | Advanced/hidden | ClaimSemanticClassifier.cs:18-21,241-280 |
| 725 | Reason-code taxonomy (attest-specific, new namespace) | Public | Attest/AttestService.cs:90-98 |
| 726 | Named-partition + compat aggregate counts | Public | Attest/AttestModels.cs:63-74, AttestService.cs |
| 727 | 1-50 claim batch, all-or-nothing validation | Public | Tools/OccamAttestTool.cs:20,51-59 |
| 728 | Serial per-claim fan-out, shared policy/session | Public/hidden (latency) | Attest/AttestService.cs:30-35 |
| 729 | Extraction failure → unknown (fail-closed) | Public/security | Attest/AttestService.cs:68-76 |
| 730 | `retrievalComplete` gate (reuses CAP-263) | Advanced | Attest/AttestService.cs:78-85 |

CAP-731 through CAP-749 are **reserved, not allocated** — no further distinct product capabilities were
found beyond the 11 (720–730) enumerated above.

---

## Non-findings (checked, found consistent/correct)

- Fail-closed posture is consistent everywhere in this tool: no code path infers `supported` from
  retrieval score, block count, or Merkle proof success alone — confirmed by reading every branch of
  `ClassifyAgainstBlock`/`Classify` and cross-checked against `AttestUnitTests.cs`'s full assertion set.
- `grounded` is computed by a single alias function (`AttestStatus.IsGroundedAlias`) called from both the
  per-claim result constructor and `AttestClassifier.IsGrounded` — no duplicated/divergent "is this
  grounded" logic between the two.
- Malformed `claims` JSON, non-array JSON, and oversized batches are all caught before any network call
  (`OccamAttestTool.cs`), consistent with the rest of the trust subsystem's attacker-input hygiene
  (CAP-291 reused pattern).
- Empty `claim`/`sourceUrl` strings inside an otherwise-valid array are handled per-row (`status=unknown`,
  `reason=invalid_arguments`), not by failing the whole batch — a different, more lenient contract than
  the whole-batch size cap (CAP-727), and this asymmetry is intentional per the code structure (size cap
  checked before parsing loop; row validity checked inside the loop).
