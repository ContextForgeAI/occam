# Subsystem audit: Consensus / Cross-check (`occam_crosscheck`, SI-14)

Wave 3 subagent **S3-03** — FFOccamMCP capability audit.
**CAP ID range: CAP-850 – CAP-869** (this report uses 850–867; 868–869 reserved/unused).
**Source of truth: executable code only.** Docs (`docs/tools/occam_crosscheck.md`, `docs/tools-reference.md`,
`MCP_API_SPEC.md`, `CHANGELOG.md`) were read for cross-check but not trusted as evidence; discrepancies are
flagged explicitly, not silently corrected.

Repo: `c:\PROJECTS\FFOccamMCP`. Paths relative to repo root unless stated.

Reuses heavily from Wave 1 `docs-audit/subsystems/trust-receipts.md` (CAP-250–291, esp. CAP-264/278/279/280
which already document crosscheck's receipt wiring in detail) and `docs-audit/NONCORE-SURFACE-MAP.md` §B/§K.

---

## 0. Executive summary

`occam_crosscheck` is a **single, opt-in MCP tool** (`OCCAM_CONSENSUS_MCP=1`) backed by a tiny, self-contained
subsystem: `Consensus/ConsensusEvaluator.cs` (pure verdict logic), `Consensus/ConsensusService.cs`
(orchestration — reuses `TranscodePipeline` + `ReceiptSigner`), `Consensus/ConsensusModels.cs` (records +
JSON context), `Tools/OccamCrosscheckTool.cs` (MCP surface). Total: 4 files, ~330 lines.

**What it does:** extracts one URL through 2+ **vantage points** — the backend axis (`http` vs `browser`) and,
if a `session_profile` is supplied, an anon-vs-authed axis per backend — and classifies agreement:
`consensus` | `divergent` | `access_divergent` | `inconclusive`. Comparison key is `blockMerkleRoot` (falls
back to `contentHash` if no blocks). Each vantage carries a normal Receipt v1 envelope (same builder as
`occam_transcode`/`occam_digest`/`occam_claim_check`/`occam_dataset_export` — CAP-278/279 in Wave 1). The
**verdict itself is never separately signed** — by design, per the doc-comment: it is "re-derivable by anyone
from the [individual, signed] receipts."

**What it is not:** there is no distributed/multi-node jury (explicitly deferred per `CHANGELOG.md` — "the
distributed multi-node version… is deliberately deferred until external nodes exist"), no code coupling to
`occam_claim_check`/`occam_attest` (siblings, not callers of each other), no agent-facing discoverability hint
anywhere in `OccamServerInstructions`, and — most importantly for correct use — the tool proves **only** that
N vantages *from this one host* saw the same or different bytes. It does not prove which vantage is
"canonical," does not prove absence of geographic/CDN-edge cloaking (single egress IP for every vantage), and
does not distinguish malicious cloaking from ordinary personalization/A-B testing.

---

## 1. Code inventory (executable entrypoints)

| File | Role |
|------|------|
| `src/FFOccamMcp.Core/Tools/OccamCrosscheckTool.cs` | MCP tool `occam_crosscheck`: param parsing/validation, delegates to `IConsensusService` |
| `src/FFOccamMcp.Core/Consensus/ConsensusService.cs` | `IConsensusService`/`ConsensusService`: builds vantage list, drives `TranscodePipeline` per vantage, builds receipts, calls the evaluator |
| `src/FFOccamMcp.Core/Consensus/ConsensusEvaluator.cs` | Pure, static, deterministic verdict classifier over `VantageObservation[]` |
| `src/FFOccamMcp.Core/Consensus/ConsensusModels.cs` | `VantageObservation`, `DivergencePair`, `ConsensusVerdict`, MCP response records + `OccamCrosscheckJsonContext` (source-gen JSON) |
| `src/FFOccamMcp.Core/Transport/OccamMcpServerRegistration.cs:141-146` | Registration gate: `if (OccamEnvironment.GetFlag("OCCAM_CONSENSUS_MCP", false)) { AddSingleton<IConsensusService,...>(); WithTools<OccamCrosscheckTool>(); }` |
| `benchmarks/l0-gate/ConsensusUnitTests.cs` | Gate: pure-evaluator unit tests only (`L_CONSENSUS_OK`) |

No CLI verb, no operator script, no daemon, no worker touches this subsystem directly — it is 100% "call the
existing pipeline N times and diff the results."

---

## 2. Capability entries (CAP-850 – CAP-867)

### CAP-850 — `occam_crosscheck` MCP tool surface
**File:** `Tools/OccamCrosscheckTool.cs:16-44`
**Classification:** Public, opt-in (absent from `tools/list` unless `OCCAM_CONSENSUS_MCP=1`).
Params: `url` (required), `vantages` (default `"http,browser"`), `session_profile` (optional),
`focus_query` (optional). Returns `OccamCrosscheckSuccessResponse{ok,url,verdict,vantages[],divergence[],
timestamp}` or a typed failure `{ok:false,url,failureCode,message,timestamp}`. Only one failure code is
raised locally: `invalid_arguments` (empty url, or a `vantages` token that isn't `http`/`browser`) — all other
failure codes flow up from `ConsensusService.CrosscheckAsync`, but that method's own signature always returns
`FailureCode: null` (see CAP-852) — i.e. in practice **every non-`invalid_arguments` failure is absorbed into
a per-vantage `Ok:false` entry inside a successful `ok:true` envelope**, never a top-level tool failure. An
agent must inspect `vantages[].ok`/`failureCode`, not the outer envelope, to see individual fetch failures.

### CAP-851 — `ConsensusEvaluator.Evaluate` verdict algorithm
**File:** `Consensus/ConsensusEvaluator.cs:17-49`
**Classification:** Public (core semantics), pure/deterministic/order-independent (order-independence is
gate-tested, `ConsensusUnitTests.cs:44-47`).
Fingerprint key = `BlockMerkleRoot ?? ContentHash` (block root preferred; a vantage with no blocks still
compares on raw content hash). Decision order:
1. `usable.Count >= 1 && walls.Count >= 1` → **`access_divergent`** (any provable access wall alongside any
   successful witness — checked FIRST, so it overrides divergent even when the successful witnesses also
   disagree with each other — see CAP-858).
2. else `usable.Count >= 2` → distinct fingerprint count == 1 ? **`consensus`** : **`divergent`**.
3. else → **`inconclusive`** (0 or 1 usable witness — includes the single-vantage call, and the case where a
   second vantage transient-failed with a non-wall code like `timeout`/`network_error`).

### CAP-852 — Vantage generation: backend axis × session axis
**File:** `Consensus/ConsensusService.cs:25-59`
**Classification:** Public. For each requested backend (`http`, `browser`, in caller order after
`TryParseBackends` dedup) it always adds one anonymous vantage; if `session_profile` is non-blank it ALSO adds
a `"<backend>+session"` vantage per backend. So `vantages=http,browser` + a session profile yields **4**
extracts per call (2 backends × 2 session states), not 2 — a real cost multiplier worth calling out (each
extract is a full `TranscodePipeline` run, http ≤35s / browser ≤120s timeout per the core backend contract).
`CrosscheckAsync`'s own return type always sets `FailureCode`/`Message` to `null` (line 58) — confirmed: there
is no code path inside `ConsensusService` that returns a top-level failure; only the tool-level
`invalid_arguments` check (CAP-850) can fail the call before vantages run.

### CAP-853 — `PlaybookPolicy.Off` forced for every vantage
**File:** `Consensus/ConsensusService.cs:78` (`PlaybookPolicy = Playbooks.PlaybookPolicy.Off`)
**Classification:** Public, deliberate design choice (doc-comment: "compare raw content, don't let a genome
mask divergence"). Every vantage skips the `PlaybookSeedResolver`/genome-overlay branch in
`TranscodePipeline.TranscodeAsync` entirely (`PlaybookPolicy.ShouldApply(Off)` is false — confirmed by reading
`Routing/TranscodePipeline.cs:57`), falling straight to `TranscodeCoreAsync`. Correct and consistent with the
tool's job: a per-host CSS-selector recipe could otherwise normalize away a real cloaking divergence before
the comparison ever sees it.

### CAP-854 — Crosscheck bypasses cascade `backend_policy`; calls each vantage backend directly
**File:** `Consensus/ConsensusService.cs:40,45,72-81`
**Classification:** Public/architectural. Unlike `occam_transcode`'s default `http_then_browser` (escalate on
failure), crosscheck never uses `OccamBackendPolicy.HttpThenBrowser` — it calls
`pipeline.TranscodeAsync(url, backend, options, ct)` once per **explicit** backend (`Http` or `Browser`,
never the cascade enum value), so a backend that fails is recorded as a failed **vantage**, not silently
retried by the other backend. This is the correct behavior for the tool's purpose (you want to know that HTTP
failed and browser succeeded, not have that difference erased by escalation) but is a genuine behavioral
difference from every other core tool that touches `TranscodePipeline` with a policy parameter.

### CAP-855 — Per-vantage receipts reuse the shared `BuildReceipt`/`BuildNegativeReceipt` builder
**File:** `Consensus/ConsensusService.cs:84-106`; builder at `Tools/OccamTranscodeModels.cs:347-454`
**Classification:** Public. Confirmed fan-in with Wave 1 CAP-278/279 (trust-receipts.md §7): crosscheck is one
of 5 call sites of the exact same receipt builder used by transcode/digest/claim_check/dataset_export.
Positive vantage → `BuildReceipt(outcome, url, effectiveSigner)?.Signed` (content hash + block Merkle root,
same as a `json_blocks` transcode, since `JsonBlocks=true` is always forced — see CAP-852's options literal at
line 74). Failed vantage → `BuildNegativeReceipt(...)` gated on the same
`captcha_or_challenge`/`requires_login`/`paywall`/401/403/404/410 set as everywhere else (CAP-264). Every
vantage — success or wall-failure — carries a receipt object in the response; only a **transient** failure
(timeout, network_error, workers_unavailable) yields a vantage with `Receipt: null`.

### CAP-856 — The consensus **verdict is not itself signed** — re-derivable-by-design
**File:** `Consensus/ConsensusModels.cs:49-55` (`OccamCrosscheckSuccessResponse.Verdict` is a plain `string`,
no signature field at the response level); doc-comment `Tools/OccamCrosscheckTool.cs:12-13`.
**Classification:** Public, security-relevant design fact, not a bug. Only the **per-vantage** artifacts
(`ContentHash`, `BlockMerkleRoot`, `Receipt`) are cryptographically signed. The `verdict` string and the
`divergence[]` pairwise comparison are **plain JSON computed by the tool at response time** — nothing stops a
compromised/buggy host from reporting `"consensus"` while shipping receipts that actually diverge. The
mitigating design intent (stated in the code, not verified by this audit as automated anywhere reachable via
MCP/CLI) is that a distrustful consumer is expected to **recompute** `ConsensusEvaluator.Evaluate` themselves
from the individual signed receipts rather than trust the reported `verdict` field. **No MCP tool or CLI verb
actually performs that re-derivation for the caller** — `occam_verify` verifies one receipt at a time (offline
mode); it has no "verify a crosscheck response" mode that re-runs `ConsensusEvaluator` over the vantages and
cross-checks the reported verdict. See CAP-863/EF-030.

### CAP-857 — `DivergencePair` block-overlap math (usable-only, union-based)
**File:** `Consensus/ConsensusEvaluator.cs:51-69`
**Classification:** Public. Pairwise comparisons (`divergence[]`) are only computed over vantages that are
`Ok && Fingerprint != null` (the `usable` list) — a walled vantage (`access_divergent` case) never appears in
any `DivergencePair`, even though it does appear in `vantages[]`. `BlocksCommon`/`BlocksTotal` are only
populated when **both** sides of a pair have non-empty `LeafHashes` (both null otherwise, `JsonIgnore`d out of
the wire payload) — i.e. if either vantage produced zero blocks (e.g. a thin/empty page), the pair still
reports `rootsMatch` but omits the overlap magnitude entirely, rather than reporting `0/0`.

### CAP-858 — `access_divergent` priority rule (wall beats content-divergence)
**File:** `Consensus/ConsensusEvaluator.cs:33-41`; gate-proven `ConsensusUnitTests.cs:56-62`
**Classification:** Public, explicit design ("the strongest cloaking signal"). If 2 successful vantages
already disagree with each other (root mismatch) AND a third vantage is walled, the verdict is
`access_divergent`, not `divergent` — the wall signal takes precedence even though the underlying content
divergence is still real and still reported in `divergence[]`. An agent that only reads `verdict` (and ignores
`divergence[]`) could miss that the *successful* vantages also disagreed with each other.

### CAP-859 — Trust-model scope: single-node "local jury", not a distributed jury
**File:** doc-comments `Consensus/ConsensusEvaluator.cs:4-9`, `Consensus/ConsensusService.cs:17-22`;
`CHANGELOG.md` SI-14 entry
**Classification:** Architecture/security-relevant scope-cut (documented, not hidden). All vantages in a
single `occam_crosscheck` call originate from **the same host process, same egress path/IP, same
`OCCAM_HTTP_PROXY`/`OCCAM_HTTPS_PROXY` configuration** (no per-vantage proxy/geo override anywhere in
`ConsensusService`) — they differ ONLY in (a) which extraction engine ran (bare HTTP fetch vs full Chromium)
and (b) whether a caller-supplied `session_profile`'s cookies/headers were attached. This means:
- It **can** detect classic bot-vs-browser cloaking (serve a clean/empty page to a non-browser UA) and
  anon-vs-authenticated personalization/paywalling.
- It **cannot** detect geographic/CDN-edge cloaking, ISP-based blocking, or "serves different content to
  different real users" in general — every vantage is the same network origin.
- The distributed multi-node design ("N of M nodes," remote signers) that would close this gap is explicitly
  named in the code/changelog as **deferred, not implemented** ("reuses this exact comparison logic" once it
  exists — i.e. `ConsensusEvaluator` itself is already written to be reusable for that future, but no remote
  transport/signer-registry exists today).

### CAP-860 — `occam_crosscheck` registration is orthogonal to `OCCAM_PROFILE`
**File:** `Transport/OccamMcpServerRegistration.cs:141-146` (no `OccamToolProfile.IsExposed` call, contrast
lines 79-117 which gate all 15 core tools through it)
**Classification:** Public, confirmed for this specific tool (concrete instance of Wave 1's general
CAP-011 "opt-in MCP tools are not profile-filtered"). Practical effect: an operator running
`OCCAM_PROFILE=reader` (meant to hide advanced/trust tooling from a casual agent) gets **no** suppression of
`occam_crosscheck` if `OCCAM_CONSENSUS_MCP=1` is also set — the two env vars are fully independent gates, and
there is no way to have "consensus enabled, but hidden from a reader-profile agent" short of unsetting
`OCCAM_CONSENSUS_MCP` entirely.

### CAP-861 — No `OccamServerInstructions`/agentHints coverage for crosscheck — discoverability gap
**File:** `Transport/OccamServerInstructions.cs` (grepped for `crosscheck`/`consensus`, case-insensitive — zero
matches)
**Classification:** Finding/OBSERVATION. The static `ServerInstructions` capability/decision guide that ships
to every connected client (added per `CHANGELOG.md` "Occam previously set no MCP `instructions`…") never
mentions `occam_crosscheck`, unlike e.g. `occam_probe`'s live `agentHints.warnings` which proactively nudges an
agent toward `json_feed`/`max_tokens`/`session_profile` based on what probe actually saw (CAP T3.1 in
CHANGELOG). Even when an operator has gone out of their way to set `OCCAM_CONSENSUS_MCP=1`, the connected
agent gets **zero in-band guidance** that the tool exists or when to reach for it — discoverability rests
entirely on the tool's own `[Description]` string (visible only once the agent already lists tools) and on
external docs (`docs/tools/occam_crosscheck.md`).

### CAP-862 — Relation to `occam_claim_check` / `occam_attest`: siblings, not collaborators
**File:** cross-read `Consensus/*.cs`, `Claims/ClaimCheckService.cs`, `Attest/AttestClassifier.cs` — no
`IConsensusService`/`ConsensusService`/`OccamCrosscheckTool` reference found in either, and no reverse
reference from Consensus code to `IClaimCheckService`/`AttestClassifier`.
**Classification:** Architecture. The three "trust-adjacent" tools answer three **orthogonal** questions and
share only the low-level primitives (Receipt v1 builder, `MerkleTree`, `TranscodePipeline`), never each
other's service layer:
- `occam_crosscheck` — "did every vantage point see the **same bytes**?" (agreement, not truth).
- `occam_claim_check` — "does **this one** extracted page contain text that matches **this claim**?"
  (relevance/existence, not truth, per Wave 2 `docs-audit/tools/occam_claim_check.md`).
- `occam_attest` — batches `occam_claim_check` over a report's `{claim, sourceUrl}` rows (grounded/unsupported
  tally) — this one genuinely reuses `IClaimCheckService` internally (Wave 1/2 confirmed), but still never
  touches `IConsensusService`.
There is **no built-in composition** (e.g. "run crosscheck first, and only claim-check the vantage that didn't
hit a wall") — an agent wanting that workflow must orchestrate it itself by calling both tools and reading both
responses; the code offers no combined tool or shared session/cache between them.

### CAP-863 — What a `consensus` verdict does NOT prove (semantics ledger)
**File:** synthesis of CAP-851/856/858/859 above; no single code location, but every point below is directly
traceable to the cited CAPs.
**Classification:** Documentation-grade finding — the honest boundary of the capability, worth stating
explicitly because the tool name ("crosscheck"/"consensus") invites over-trust:
1. **Does not prove which vantage is "true."** A `divergent` verdict names two disagreeing fingerprints; there
   is no authority/ranking signal that says which one is the "real" page. (CAP-851.)
2. **Does not prove the content is accurate.** Same trust model as `claim_check`/`attest`: proof of *what was
   served*, never proof of *truth*. Agreement across vantages means "not obviously cloaked," not "correct."
3. **Does not detect geo/CDN/ISP-level cloaking.** All vantages share one egress path. (CAP-859.)
4. **Cannot distinguish malicious cloaking from legitimate personalization/A-B testing.** An
   `access_divergent` or session-axis `divergent` verdict is equally consistent with a hostile bot-wall and
   with ordinary "logged-in users see a different page" design — the tool reports the *fact* of divergence,
   the caller must supply the *interpretation*.
5. **A single requested vantage always collapses to `inconclusive`**, never `consensus` — you need ≥2 usable
   witnesses to claim agreement; `vantages="http"` alone (no comma) is a well-formed call that can never return
   `consensus` or `divergent`. (CAP-851, gate-proven `ConsensusUnitTests.cs:38-39`.)
6. **The reported `verdict` field itself is unsigned** and not independently re-verified by any shipped tool —
   trusting it means trusting the host that computed it, same as trusting any other unsigned JSON field.
   (CAP-856.)
7. **Receipts are all self-signed by the same single local key** (Wave 1 CAP-288/289) — "signed" here means
   "this host attests," not "an independent third party attests." Divergence across vantages is real evidence
   of *something*, but the overall claim never rests on multi-party trust.

### CAP-864 — Dead `"paywall"` failure-code branch, duplicated a second time
**File:** `Consensus/ConsensusService.cs:100` (`code is "captcha_or_challenge" or "requires_login" or
"paywall" || ...`)
**Classification:** Finding, low severity — same unreachable condition Wave 1 already flagged once for the
transcode builder (`docs-audit/subsystems/trust-receipts.md` CAP-264/EF-008: no post-processor in the codebase
ever emits `FailureCode = "paywall"`). This audit confirms the **identical** dead disjunct is hand-copied into
`ConsensusService`'s own local `isWall` check rather than calling a shared helper — i.e. the duplication named
in Wave 1 ("this exact string also appears, identically unreachable, in `ConsensusService.cs:100`") is
independently reconfirmed here, not a new bug. Harmless (fails safe), but two independent hand-copies of the
same wall-code list is a maintenance smell if a future failure code is ever added to one and not the other.

### CAP-865 — `OCCAM_RECEIPTS` flag re-implemented locally instead of via `ReceiptsPolicy.Enabled()`
**File:** `Consensus/ConsensusService.cs:114-122` (`EffectiveSigner()`)
**Classification:** Finding, low severity, already named once in Wave 1 (trust-receipts.md CAP-280: "
`ConsensusService.cs` re-implements the SAME flag-parsing logic locally… instead of calling
`ReceiptsPolicy.Enabled()`"). Reconfirmed here: the accepted-values set (`off`/`0`/`false`, case-insensitive,
default enabled) is currently byte-for-byte identical to `Receipts/ReceiptsPolicy.cs`, so there is no
behavioral divergence today — but it is genuinely a second, independent parser of the same env var that could
silently drift from the canonical one on a future edit to either file.

### CAP-866 — `vantages` parsing: dedup, order-preservation, default-pair fallback
**File:** `Tools/OccamCrosscheckTool.cs:46-80` (`TryParseBackends`)
**Classification:** Public. Case-insensitive token match against `http`/`browser` only; unknown token →
`invalid_arguments` naming the bad token verbatim. Duplicate tokens are silently deduped (`HashSet<...>.Add`
guards the list append) while preserving first-seen order. An empty or all-duplicate result (e.g.
`vantages=","` or `vantages="http,http"` → after dedup only `http` remains, which is NOT empty, so the
default-pair fallback at lines 72-76 only fires for a **genuinely empty** result, e.g. `vantages=""` or
`vantages=",,"`) resets to the full default pair `[Http, Browser]` — i.e. `vantages="http,http"` legitimately
runs a **single-backend** (`http`-only) crosscheck, which per CAP-851/863(5) can only ever report
`inconclusive` (no second usable witness), a subtly surprising outcome for a caller who typed a "duplicate" by
mistake expecting the tool to still compare something.

### CAP-867 — Gate coverage: pure-evaluator only, no pipeline/signing/MCP-surface integration test
**File:** `benchmarks/l0-gate/ConsensusUnitTests.cs` (65 lines, all against `ConsensusEvaluator.Evaluate`
directly with hand-built `VantageObservation` literals); `Program.cs:126,246` (`ConsensusUnitTests.Run(...)`
wired into both the fast-unit and full-gate paths, always run since it needs no network).
**Classification:** Finding/OBSERVATION — this is Wave 1's trust-receipts.md §13 observation
("No dedicated gate file found for `ConsensusService`/`occam_crosscheck` receipt wiring specifically… not
verified whether crosscheck's per-vantage receipts are gate-tested at all") **reconfirmed and narrowed** by
this deeper pass:
- `ConsensusUnitTests.cs` tests exactly one function (`ConsensusEvaluator.Evaluate`) against synthetic inputs.
  It never constructs a `ConsensusService`, never calls `CrosscheckAsync`, never touches `OccamCrosscheckTool`.
- No corpus file (`corpora/*.jsonl`) references crosscheck; no `--url`/ad-hoc gate flag targets it either.
- Consequence: nothing in the gate currently proves that (a) `AddVantageAsync` actually attaches a real signed
  `Receipt` to each vantage, (b) `PlaybookPolicy.Off` is actually honored end-to-end (vs. just requested), or
  (c) the MCP tool's JSON serialization (`OccamCrosscheckJsonContext`) round-trips correctly for a real
  success/failure response. All three are exercised only informally (by this audit's static code reading), not
  by an executable assertion. See EF-030 below.

CAP-868–869 reserved, not allocated.

---

## 3. Capability graph edges

```
TOOL occam_crosscheck        |REGISTERED_BY_FLAG|      OCCAM_CONSENSUS_MCP           (CAP-850, CAP-860)
TOOL occam_crosscheck        |USES|                    OccamMcp.Core.Consensus.ConsensusService   (CAP-850)
ConsensusService              |USES|                    TranscodePipeline.TranscodeAsync           (CAP-852, CAP-854; cross-ref CAP-052)
ConsensusService              |USES|                    ReceiptSigner (single local key)           (CAP-855; cross-ref CAP-254, CAP-289 in trust-receipts.md)
ConsensusService              |USES|                    OccamTranscodeResponseBuilder.BuildReceipt / BuildNegativeReceipt (CAP-855; cross-ref CAP-278, CAP-279)
ConsensusService              |FORCES|                  PlaybookPolicy.Off for every vantage        (CAP-853)
ConsensusService              |RE-IMPLEMENTS|           ReceiptsPolicy env-parsing logic            (CAP-865; cross-ref CAP-280)
ConsensusService              |DUPLICATES_DEAD_BRANCH|  "paywall" wall-code check                   (CAP-864; cross-ref CAP-264/EF-008)
ConsensusEvaluator            |PURE_FUNCTION_OF|        VantageObservation[]                        (CAP-851, CAP-857, CAP-858)
TOOL occam_crosscheck        |NOT_GATED_BY|            OccamToolProfile.IsExposed                  (CAP-860; cross-ref CAP-011)
TOOL occam_crosscheck        |ABSENT_FROM|             OccamServerInstructions                     (CAP-861)
TOOL occam_crosscheck        |SIBLING_OF_NOT_CALLER_OF| occam_claim_check, occam_attest             (CAP-862)
TOOL occam_crosscheck        |VERIFIED_PER_VANTAGE_BY|  occam_verify (offline mode, one receipt at a time) (CAP-856; no crosscheck-aware verify mode exists)
TOOL occam_crosscheck        |COVERED_BY_GATE(PARTIAL)| ConsensusUnitTests.cs (pure evaluator only)  (CAP-867)
```

---

## 4. Artifacts created/consumed

Reuses Wave 2's `ARTIFACT-MAP.md` **ART-007** (Receipt v1 positive) and **ART-008** (negative receipt) — every
vantage's `Receipt` field is exactly one of those two artifact shapes, no new receipt shape introduced.

New artifact surfaced by this tool only, not yet named in `ARTIFACT-MAP.md`:

| Artifact | Created by | Consumed by | Persisted? | Verifiable? | Signed? |
|----------|-----------|--------------|------------|-------------|---------|
| Crosscheck verdict envelope (`{verdict, vantages[], divergence[]}`) | `occam_crosscheck` | Calling agent only | No | Only by manual re-derivation (CAP-856) — no tool re-verifies it | **No** (verdict/divergence are plain JSON; only the nested per-vantage `Receipt` objects are signed) |

---

## 5. "INVISIBLE PRODUCT" — what an MCP-only user never sees

- **The tool does not exist for them at all**, by default — it is absent from `tools/list` unless the
  *operator* (not the calling agent) has set `OCCAM_CONSENSUS_MCP=1` in the host's environment. An MCP client
  has no in-protocol way to detect "consensus checking is available but off" vs. "this build doesn't have it."
- **Even when enabled, nothing advertises it proactively.** `OccamServerInstructions` (the one place Occam
  actively teaches a connecting agent what its tools are for) is silent on crosscheck (CAP-861) — contrast
  `occam_probe`'s `agentHints`, which actively points an agent at other opt-ins it detects a need for.
- **The distributed/"N of M nodes" jury the SI-14 name implies is vaporware today.** Nothing in the shipped
  product lets a second real node participate; "consensus" here means one process comparing its own two (or
  four) fetches of the same page, not a multi-party attestation. A user reading only the tool's short
  description ("Cross-check a URL across vantage points… Detects cloaking/personalization") could reasonably
  assume more independence than exists.
- **The verdict's own trustworthiness is invisible.** A caller sees `"verdict": "consensus"` and nothing marks
  that string as unsigned/unverified vs. the signed `Receipt` objects sitting right next to it in the same
  JSON — the schema gives both classes of field equal visual weight.
- **Cost is invisible until you read the code.** `vantages=http,browser` + `session_profile` silently becomes
  4 full extractions per call (CAP-852); nothing in the tool description states the multiplier.

---

## 6. Engineering findings (candidates)

Two of the CAPs above (CAP-864, CAP-865) are **reconfirmations** of Wave 1 findings already in the ledger
(EF-008 → CAP-264 "paywall" dead branch; the CAP-280 entry in trust-receipts.md already named the
`ConsensusService.EffectiveSigner()` duplication) — not re-appended as new EF rows to avoid duplicate IDs for
the same underlying fact. Two genuinely new observations from this pass:

| ID | Class | Related CAPs | Summary | Confidence | Needs repro? | Security review? |
|----|-------|--------------|---------|------------|--------------|------------------|
| EF-029 | OBSERVATION | CAP-860, CAP-861 | `occam_crosscheck` is exempt from `OCCAM_PROFILE` filtering (no `IsExposed` gate, same as all opt-ins) AND has zero coverage in `OccamServerInstructions`/agentHints — an operator narrowing the surface for a "reader" agent while separately enabling consensus gets no suppression, and an agent that IS allowed to use it gets no in-band nudge that it exists. | PROVEN in code (grep-confirmed absence in both files) | No (static) | No |
| EF-030 | OBSERVATION | CAP-867, CAP-856 | `ConsensusUnitTests.cs` only exercises the pure `ConsensusEvaluator.Evaluate` function against synthetic data; no gate test constructs a real `ConsensusService`/`OccamCrosscheckTool` call, so the "each vantage carries a signed receipt" claim and the "verdict is re-derivable from receipts" design intent are **never asserted end-to-end** by the gate — only inferable from static reading of `ConsensusService.cs`. Also: no MCP tool/CLI verb re-derives/cross-checks a reported `verdict` against its own `vantages[]` receipts, so the re-derivation the design relies on for trust is currently a manual exercise for any consumer, not a shipped feature. | PROVEN in code (absence confirmed by file enumeration + Program.cs wiring read) | Optional (write an integration test with an ephemeral signer + fake pipeline) | No |

---

## 7. Completeness verdict

**Complete** for the assigned scope (occam_crosscheck tool + Consensus/* + its registration/env-gate + gate
coverage + relation to claim_check/attest + explicit non-proof semantics). All 4 subsystem files were read in
full; the registration site, the shared receipt builder, the pipeline call pattern, and the one gate file were
all inspected directly rather than inferred from docs. Cross-referenced and reconciled against Wave 1
(`trust-receipts.md`, `NONCORE-SURFACE-MAP.md`) and Wave 2 (`ARTIFACT-MAP.md`, `capabilities.json`) without
re-litigating settled findings. Not independently re-verified in this pass: whether a **live** network call to
a real cloaked site actually produces `access_divergent`/`divergent` in practice (no live corpus exists for
this tool — see CAP-867/EF-030) — this report's claims about behavior are code-derived and consistent with the
gate's synthetic assertions, but not confirmed against a real HTTP/browser round-trip.
