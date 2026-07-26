# P — Adversarial product-model redteam (Wave 4 Phase 4P)

**Owner:** W4-P  
**Mode:** ATTACK (not defend). Discovery only. SoT = current shipped executable code + Wave 4 negative-space artifacts.  
**Date:** 2026-07-26  
**Written file only:** this document.

---

## 1. Blind inventory (code-first, before trusting Waves 1–3 prose)

Externally meaningful behaviors that break a tidy “pipeline + 15 tools + receipts” story:

| # | Behavior | Evidence |
|---|----------|----------|
| P1 | Escalation spine is `OccamRouter`, not `TranscodePipeline`. Pipeline wraps router + playbook overlay + post-processors + materialize. | `OccamRouter.cs:134-182`, `TranscodePipeline.cs:13-20,25-80` |
| P2 | Cascade ≠ “http→browser→managed”. Terminal 404/410 short-circuit; public-ref skip browser; dual-fail ranks by `FailureRanking`, not density; managed fail never surfaces as winner. | `OccamRouter.cs:145-182`, `:206-213` |
| P3 | Managed is off-by-default **but** with provider set and `OCCAM_MANAGED_DOMAINS` unset → **any host** eligible. | `ManagedExtractBackend.cs:66-76` |
| P4 | Parallel extract spine: `KnowledgeExtractService` → `CssExtractWorker` (no `TranscodePipeline`, no Receipt v1). | `KnowledgeExtractService.cs:10-114`, `OccamExtractKnowledgeTool.cs:74-88` |
| P5 | Parallel spines: probe/map/search/heal/session/batch/watch/CLI/install/connect — not acquisition+materialization only. | `SHIPPED-CODE-MAP.md` entrypoints; `CLI-SURFACE.md` A/B/C |
| P6 | Playbooks are soft overlays **inside** transcode (`PlaybookVerifyScope`) + genome resolve + heal/save/lint — not a side recipe system. | `TranscodePipeline.cs:57-78` |
| P7 | `playbook_save` always signs; ignores `OCCAM_RECEIPTS`; DI always `LoadOrCreate` key. | `PlaybookSaveService.cs:86-91`, `OccamServiceCollectionExtensions.cs:23` |
| P8 | `PlaybookCommunitySanitizer` ships compiled, **zero Core callers**. | `PlaybookCommunitySanitizer.cs` + repo grep |
| P9 | Fragment drives focus but is stripped from cache/materialization keys. | `FocusIntent.cs:8-30`, `TranscodeCacheKey.cs:54-71`, `MaterializationKey.cs:32` |
| P10 | WS/Remote per-session DI → `InstallShared` → `StopAll` process-wide pool. | `BrowserPoolManager.cs:45-48`, `OccamServiceCollectionExtensions.cs:39-46` |
| P11 | Operator kill is name-wide; onboard env auto-injected; Docker `--version` hangs; marketplace auto-merge without gate; cosign unused by install. | GAP-033…037; `stop-occam-processes.mjs`; `Dockerfile:76` |
| P12 | Whole C# glob ships (~320 `.cs`); dead Canonical/bench/sanitizer types still in AOT binary. | `SHIPPED-CODE-MAP.md`; Docker omits `profiles/` |
| P13 | Attest `grounded` = count of `supported` statuses; Merkle proves block existence only; claim_check does not judge stance. | `AttestService.cs:15-18,47,84-100`, `ClaimCheckService.cs:19-24` |
| P14 | Server instructions advertise `occam_watch` without requiring `OCCAM_WATCH_MCP`. | `OccamServerInstructions.cs:112`, GAP-012 |
| P15 | Canonical IR + Merkle leaves built then discarded on live markdown-passthrough path. | W4-C §1 items 8–9; `AUTOMATIC-BEHAVIORS.md` #18 |

---

## 2. Attack on the twelve Waves 1–3 statements

Verdict vocabulary: `CONFIRMED | PARTIALLY_TRUE | MISLEADING | FALSE | UNKNOWN`.  
Preference: prefer `MISLEADING`/`FALSE` when evidence supports it.

### STATEMENT_1 — "TranscodePipeline is the central spine."

**Verdict: MISLEADING**

**Attack:** Pipeline is the *transcode orchestration shell*. The escalation / backend-selection spine is `OccamRouter`. Several flagship tools never enter the pipeline at all (`occam_probe`, `occam_map`, `occam_search`, `occam_extract_knowledge`, playbook heal/save/lint/resolve, CLI verbs, batch store, watch store). Digest/claim_check/attest *do* call the pipeline — that proves it is a shared *extract* path, not *the* product spine.

**Evidence:**
- Pipeline depends on router: `TranscodePipeline.cs:13-20`.
- Escalation + short-circuits live in router: `OccamRouter.cs:134-182`.
- Extract-knowledge bypass: `KnowledgeExtractService.cs:10-114` (CssExtractWorker only).
- Citations: `docs-audit/negative-space/B-routing-backends-blind.md` §1.1–1.2; `docs-audit/SHIPPED-CODE-MAP.md` §Executable entrypoints.

---

### STATEMENT_2 — "Occam is primarily acquisition + materialization."

**Verdict: MISLEADING**

**Attack:** Acquisition+materialization is the *marketing* center of gravity for `occam_transcode`. The shipped product is also: trust/signing, playbook authoring, session cookie ops, operator install/connect mutation of ≤15 host configs, process kill/refresh, batch/watch state, consensus, marketplace CI, Docker packaging, skill install overwrites, search ranking, attest classification. Calling the product “primarily A+M” erases the operator/trust/security surface that Wave 4 proved adversarial.

**Evidence:**
- Live materialization is mostly **passthrough** after discard of Canonical IR (W4-C §1.2–1.9) — so even “materialization” is oversold.
- Operator/connect/session/install: `docs-audit/negative-space/G-scripts-operator-blind.md` B-27…B-48.
- Trust/state: `docs-audit/negative-space/E-trust-state-blind.md` §1.1–1.6.
- Packaging/CI: `docs-audit/negative-space/H-packaging-ci-blind.md` B-19…B-28.

---

### STATEMENT_3 — "Receipts/Merkle/capsules are the proof layer."

**Verdict: MISLEADING**

**Attack:** They are *a* opt-in / partial proof layer with holes large enough to falsify the singular “the proof layer” claim:

1. `OCCAM_RECEIPTS` gates transcode/digest/claim/dataset/watch — **not** `playbook_save` (always signs).
2. Key is minted on every host start even when receipts are off (`LoadOrCreate`).
3. Capsule packaging is **not** signed (`CapsuleCodec` comment; E-trust §1.5).
4. QualityGate / heuristic quality can be sealed into signed provenance (GAP-029).
5. Attest aggregate counts / consensus verdict are unsigned (GAP-028).
6. Batch results never construct receipts (E-trust §1.2).
7. `extract_knowledge.receipt` is a **confidence+latency alias**, not Receipt v1 (`OccamExtractKnowledgeReceiptInfo`).
8. Merkle OOB proof returns empty list silently (E-trust §1.4).
9. Canonical Merkle work on the live path is often **computed then discarded** (AUTOMATIC #18).

**Evidence:** `PlaybookSaveService.cs:86-91`; `OccamServiceCollectionExtensions.cs:23`; `ReceiptsPolicy.cs`; `OccamExtractKnowledgeTool.cs:111-114,88`; GAP-005/027/028/029; `docs-audit/negative-space/E-trust-state-blind.md` §1.2–1.5.

---

### STATEMENT_4 — "15 core tools + profile × opt-in adequately describes MCP exposure."

**Verdict: FALSE**

**Attack:** The count is necessary but not sufficient. Exposure is also shaped by:

- **Server instructions** that advertise `occam_watch` without the env gate (GAP-012 / `OccamServerInstructions.cs:112`).
- **Silent DI side effects** registering batch processor / atlas sink when flags on — not just tool list.
- **Transport mode** (stdio vs WS vs Remote) with different session/pool semantics (GAP-002, GAP-013).
- **Banner lie** claiming stdio even on WS/Remote (GAP-032).
- **Profile** hides tools but does not hide playbook overlay / managed / cache / key mint / bypassCSP.
- **npm help** still says “14 tools” / omits client_capabilities (H B7) — product messaging ≠ registry.
- Opt-in tools are **not** the only “extra” surface: JSON printable escapes, ambient client budget, features-scope always injecting `json_blocks/json_tables` (AUTOMATIC #6, #15).

**Evidence:** `OccamMcpServerRegistration.cs:15-157`; `OccamToolProfile.cs`; GAP-010/011/012/013/032; H-packaging B7.

---

### STATEMENT_5 — "CLI/operator surface has now been fully captured."

**Verdict: FALSE**

**Attack:** Wave 3 CLI-SURFACE captured three surfaces (A/B/C) as a catalog. Wave 4 proved **dangerous unmodeled semantics** inside those surfaces and adjacent scripts:

- Name-wide process kill on `occam refresh` ignoring `OCCAM_HOME` (GAP-033) — contradicts INV-10 narrative that only guarded `stopOccamHostByPid`.
- Every launch injects `~/.occam/onboard.json` env (GAP-034).
- Connect desktop default mutates up to 15 host configs without confirm (G B-28/29).
- Session import retains plaintext cookies.txt in `_imports/` while `list` claims no secrets (GAP-038).
- `launch-mcp-host` hardcodes `[]` args → WS/Remote/Batch unreachable via canonical launcher (G B-23).
- Docker HEALTHCHECK `--version` starts stdio and hangs (GAP-035 / `Dockerfile:76`).
- Marketplace auto-merge + cosign theater (GAP-036/037).
- Help registry rows not reachable via `occam <sub>` (CLI-SURFACE §1 C; G B-08/09).

“Captured as names” ≠ “understood as behavior.”

**Evidence:** `docs-audit/NEGATIVE-SPACE-GAPS.md` GAP-033…040; `docs-audit/negative-space/G-scripts-operator-blind.md`; `docs-audit/CLI-SURFACE.md` §1.

---

### STATEMENT_6 — "Managed providers are only a last-rung escalation mechanism."

**Verdict: MISLEADING**

**Attack:** Position in the cascade is last-rung **only on the `http_then_browser` path after both locals fail** — true as far as it goes. The statement collapses under:

1. **Not reached** on 404/410 or public-reference short-circuit — so “last rung” is often “never rung” (GAP-001).
2. **Managed failure never becomes the surface winner** — recovery-only (GAP-014 / `OccamRouter.cs:182`). Success can win; failure is invisible as the chosen outcome.
3. **Domain gate default is open:** provider env alone opts in **all hosts** (`ManagedExtractBackend.cs:66-76`). That is a **policy-wide third-party egress switch**, not a narrow last-rung safety valve.
4. Managed HttpClient has **no** `OutboundHttpGuard` (B-routing §1.4) — SSRF model does not apply the same way as probe.
5. Sync `HttpClient.Send` wrapped in `ValueTask.FromResult` — thread-block side effect on the “last rung.”
6. Single-backend policies never touch managed (B2) — so “escalation mechanism” is policy-conditional, not universal.

**Evidence:** `OccamRouter.cs:163-182`; `ManagedExtractBackend.cs:28-76`; `docs-audit/negative-space/B-routing-backends-blind.md` §1.3; GAP-001/014.

---

### STATEMENT_7 — "Playbooks are a parallel recipe system."

**Verdict: FALSE**

**Attack:** Playbooks are **in-band soft overlays and genome policy** on the main extract path, plus a separate CSS knowledge path that **requires** a resolved playbook schema. They are not “parallel recipes” like the old worker `recipes/*.mjs` host branches.

- Transcode auto-resolve + `PlaybookVerifyScope` soft overlay: `TranscodePipeline.cs:57-78`.
- Preferred-backend override when policy is `HttpThenBrowser`.
- Browser interaction plans execute `page.evaluate` from playbook content (GAP-007).
- `extract_knowledge` hard-depends on playbook + knowledgeSchema: `KnowledgeExtractService.cs:37-65`.
- Local save always signs into `~/.occam/playbooks/local/` and feeds resolve tiers; community marketplace can auto-merge into the same resolve graph (GAP-036).
- Worker host recipes still exist **in addition** (`workers/browser-extract/lib/recipes/`) — if anything is “parallel recipes,” it is that registry, not playbooks.

**Evidence:** `TranscodePipeline.cs:57-78`; `KnowledgeExtractService.cs:37-114`; GAP-007/008/036; SHIPPED-CODE-MAP recipes row.

---

### STATEMENT_8 — "extract_knowledge is a separate weaker-trust path."

**Verdict: PARTIALLY_TRUE**

**Attack:** “Separate” is **CONFIRMED** (CssExtractWorker, no TranscodePipeline, no Receipt v1). “Weaker-trust” as a clean product label is **underspecified / soft**:

- It is **stronger-coupling** to playbooks (hard fail without schema) — not a casual weaker path.
- Security posture can be **worse**, not merely weaker: css-extract lacks SSRF/private-IP parity and body cap; EF-013 `(0,eval)(__NUXT__)` still live (GAP-004).
- Response field named `receipt` is a **trust-word collision** with Receipt v1 (`confidence`+`latencyMs` only) — agents can over-trust.
- Browser fallback exists inside css path — it is not “HTTP-only weak.”

So: separate = true; weaker-trust = incomplete framing that hides **different and sometimes riskier** trust.

**Evidence:** `KnowledgeExtractService.cs:10-114`; `OccamExtractKnowledgeTool.cs:88,111-114`; GAP-004; `docs-audit/FAILURE-BEHAVIOR-MAP.md` css rows; F-workers EF-013 reconfirm.

---

### STATEMENT_9 — "claim_check/attest semantics are accurately represented."

**Verdict: MISLEADING**

**Attack:** Tool descriptions are directionally honest (“YOU judge support”; Merkle ≠ truth). Wave 4 still breaks “accurately represented” at the model layer:

- Attest `Grounded` aggregate **equals Supported count** (`AttestService.cs:47`) — easy to misread as “report is grounded” rather than “N claims classified supported.”
- Per-claim `Grounded` is an alias of status (`IsGroundedAlias`) — dual meaning of the same word.
- claim_check forces `PlaybookPolicy.Auto` silently (`ClaimCheckService.cs:39`) — unadvertised overlay.
- claim_check/attest inherit cascade/short-circuit/public-ref/managed-fail invisibility from router (GAP-001) — failure codes can be cascade-shaped, not claim-shaped.
- Leaf-set “provable absence” assumes no truncation; honest only under that invariant (`ClaimCheckService.cs:56-60`).
- Attest response envelope unsigned; nested receipts may be null when `OCCAM_RECEIPTS=off` while status still returned (E-trust).
- Quality/heuristic sealed into signed provenance elsewhere pollutes the same trust vocabulary (GAP-029).

**Evidence:** `AttestService.cs:15-100`; `ClaimCheckService.cs:19-60`; `OccamAttestTool.cs:22`; `OccamClaimCheckTool.cs:18`; GAP-028/029; E-trust §1.2.

---

### STATEMENT_10 — "We understand all persistent/stateful behavior."

**Verdict: FALSE**

**Attack:** Wave 4 inventory of `~/.occam` and in-process state still shows holes and false confidence:

| Store | Gap |
|-------|-----|
| `signing-key.pem` | Always created; missing from ARTIFACT-MAP (GAP-027) |
| playbooks local | Always signed; sanitizer dead (GAP-005/008) |
| watch.json | URLs uncapped; Remove unused; non-atomic write (E-trust §1.1) |
| batch jobs.json | No delete API; Persist IO can swallow (FAILURE-MAP) |
| cache / MaterializationKey | Fragment omitted → cross-# poison (GAP-006) |
| browser pool Shared | Cross-session StopAll (GAP-002) |
| Failure atlas | In-memory only — tool description says so; easy to confuse with durable |
| session `_imports/` | Plaintext cookies retained (GAP-038) |
| `onboard.json` / `connect-last.json` | Auto-written; env injection (GAP-034; G B-34) |
| genome cache | 1h in-process TTL |
| ClientCapabilityStore | Ambient max_tokens shifts cache identity (AUTOMATIC #15) |

“All” is falsified by GAP-002/006/022/027/034/038 alone.

**Evidence:** `docs-audit/negative-space/E-trust-state-blind.md` §1.1; `AUTOMATIC-BEHAVIORS.md`; GAP-002/006/022/027/034/038.

---

### STATEMENT_11 — "We understand all automatic/silent behavior."

**Verdict: FALSE**

**Attack:** `AUTOMATIC-BEHAVIORS.md` itself is labeled DRAFT and already lists 24 entries that Waves 1–3 understated. Hard breaks:

- InstallShared pool kill on new WS/Remote session (CRITICAL GAP-002).
- Always-on `bypassCSP: true` (GAP-007 / AUTOMATIC #7) — not disableable.
- Consent dismiss / CSS-hide / virtual-scroll / Chromium auto-provision (AUTOMATIC #8–10).
- Features-scope always injects structured IR (AUTOMATIC #6).
- Public-ref skip browser (AUTOMATIC #5) — silent vs naive cascade docs.
- playbook_save always-sign (AUTOMATIC #14).
- Canonical build-then-discard (AUTOMATIC #18).
- Robots fail-open; Playwright proxy fail-open (GAP-018/030).
- Probe SSRF→`network_error` mask (GAP-003) — silent *misclassification*.
- Marketplace auto-merge / skill `rmSync` overwrite / connect host mutation.

If Wave 4 needed a dedicated AUTOMATIC map after Waves 1–3 “completeness,” the prior claim of understanding is false.

**Evidence:** `docs-audit/AUTOMATIC-BEHAVIORS.md`; GAP-002/003/007/018/030/033/034/036/039.

---

### STATEMENT_12 — "We understand what actually ships."

**Verdict: FALSE**

**Attack:** `SHIPPED-CODE-MAP` was required precisely because prior waves conflated “modeled capability” with “ships.” Remaining falsifiers:

- Whole Core glob compiles (~320 `.cs`) including dead Canonical/Legacy/bench/Sanitizer types — dead ≠ unshipped.
- Docker **omits** `profiles/` → silent built-in fallbacks (GAP-043 / H B19/B22).
- Level B tarball omits connect/contract scripts (S3-12 / SHIPPED map).
- `@ff-occam/*` unpublished / DOA cross-import if published (H B2/B24).
- agent-sdk is real product code but npm-unreachable (GAP-044 / PRODUCT_AS_INTERNAL).
- Cosign `.bundle` produced but **unused** by install/npx verify (GAP-037) — ships a trust theater artifact.
- HEALTHCHECK command does not match any CLI verb (GAP-035) — ships a broken operator contract.
- Help/refresh strings still claim “9” or “14” tools in places (G B-17; H B7).

**Evidence:** `docs-audit/SHIPPED-CODE-MAP.md`; GAP-035/037/041/043/044; H-packaging §1D–1E.

---

## 3. Gap classification (model-break summary)

| Statement | Class vs Waves 1–3 model |
|-----------|---------------------------|
| 1 Pipeline spine | COVERED_WRONG |
| 2 A+M primary | COVERED_PARTIALLY / PRODUCT_MISTAKEN framing |
| 3 Proof layer | COVERED_WRONG (policy holes) |
| 4 MCP exposure | MISSING_RUNTIME_SURFACE + MISSING_EDGE |
| 5 CLI fully captured | MISSING_SECURITY_SEMANTIC + MISSING_WORKFLOW |
| 6 Managed last-rung | COVERED_WRONG |
| 7 Playbooks parallel | COVERED_WRONG |
| 8 extract_knowledge weaker | COVERED_PARTIALLY + MISSING_SECURITY_SEMANTIC |
| 9 claim/attest accurate | COVERED_PARTIALLY / MISSING_EDGE |
| 10 All persistent | MISSING_ARTIFACT + MISSING_CAPABILITY |
| 11 All automatic | MISSING_RUNTIME_SURFACE (volume) |
| 12 What ships | DEAD_CODE_MISTAKEN_AS_PRODUCT + PRODUCT_MISTAKEN_AS_INTERNAL + MISSING_FAILURE_SEMANTIC |

No new CAP numbers assigned (orchestrator owns CAP-1050+). Prefer correcting CAP-052/104 and edges over minting.

**EFC proposals (not canonical EF-NNN):**
- EFC-P-1: Product narrative “pipeline spine” → replace with router+multi-spine map (OBS, HIGH).
- EFC-P-2: “Proof layer” singular → split Receipt policy matrix incl. always-sign save (DESIGN, HIGH).
- EFC-P-3: extract_knowledge `receipt` field rename/disambiguate in future product work (DESIGN, MED) — discovery only here.

---

## 4. Compact envelope

```
STATEMENT_1: MISLEADING — Escalation spine is OccamRouter; pipeline is one shell; probe/map/search/css-knowledge bypass it.
STATEMENT_2: MISLEADING — A+M is the agent-facing center; shipped product is also trust/operator/connect/CI/session/kill surfaces.
STATEMENT_3: MISLEADING — Receipts/Merkle/capsules are partial, holed, and name-collided; not a unified proof layer.
STATEMENT_4: FALSE — Profile×opt-in misses instructions/banner/transport/DI/side-effects and wrong tool counts in packaging.
STATEMENT_5: FALSE — Names were catalogued; Wave 4 proved kill/onboard/connect/Docker/marketplace semantics uncaptured.
STATEMENT_6: MISLEADING — Last-rung only on one policy path; open domain default; managed fail never surfaces; SSRF-unguarded client.
STATEMENT_7: FALSE — Playbooks are in-band overlays + schema gates, not a parallel recipe system (worker recipes are).
STATEMENT_8: PARTIALLY_TRUE — Separate yes; “weaker-trust” hides SSRF/eval risk and fake receipt field.
STATEMENT_9: MISLEADING — Descriptions honest-ish; grounded dual-meaning + silent playbook_auto + unsigned aggregates break accuracy.
STATEMENT_10: FALSE — Fragment cache poison, InstallShared, key mint, imports cookies, uncapped watch — not “all understood.”
STATEMENT_11: FALSE — AUTOMATIC map + CRITICAL silent pool kill / bypassCSP / SSRF mask falsify completeness.
STATEMENT_12: FALSE — Whole-glob dead types, Docker missing profiles, cosign theater, broken HEALTHCHECK — ship boundary still wrong.

MODEL_BREAKS:
- CAP-052/104 cascade narrative (http→browser→managed density ranking)
- “TranscodePipeline = product spine”
- “Managed = narrow last-rung safety valve”
- “Playbooks = parallel recipes”
- “OCCAM_RECEIPTS centralizes all signing”
- “15+profile×opt-in = MCP exposure”
- “CLI surface fully captured after Wave 3”
- “We know persistent/automatic/shipped boundary”

STRONGEST_ATTACKS:
1. OccamRouter short-circuit + FailureRanking + managed-fail-excluded (GAP-001) — cascade model wrong.
2. InstallShared StopAll on every WS/Remote DI (GAP-002) — silent cross-session availability kill.
3. playbook_save ignores OCCAM_RECEIPTS + always LoadOrCreate key (GAP-005) — proof-layer lie.
4. stop-occam name-wide kill + onboard env injection + connect auto-mutate (GAP-033/034 + G) — operator surface hostile.
5. Whole-glob ships dead types + Docker HEALTHCHECK hang + marketplace auto-merge + cosign unused (SHIPPED + GAP-035/036/037) — “what ships” unknown.
```

---

## 5. Uncertainties (bounded)

- Branch-protection may block marketplace auto-merge in practice; **code path is open** (GAP-036).
- Exact live WS/Remote call frequency of `AddOccamCore` per session not re-measured this phase; DI factory + `InstallShared` coupling is proven in source.
- Whether any external script invokes `PlaybookCommunitySanitizer` via reflection: none found; treated Core-dead.
- C# file count drift 309→~320 does not change whole-glob conclusion.

**CONVERGENCE_IN_SCOPE:** YES for the twelve statements — each has a stable verdict backed by code + Wave 4 GAP IDs; further work is product correction, not more discovery of the same breaks.
