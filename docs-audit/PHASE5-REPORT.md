# PHASE 5 — CANONICAL PRODUCT MODEL

**Date:** 2026-07-26 · **Input:** Waves 1–4 corpus (78 files) · **Output:** 27 new canonical model files
**Source of truth:** executable code. Public docs remained FROZEN and untouched throughout.

---

## Envelope

**STATUS:** COMPLETE

**AUDIT CORPUS**
- canonical files: 22 pre-existing (10 ledgers + 8 Wave-4 correction layer + 4 boundary maps), extended by 22 Phase-5 canonical model files + 39 family cards
- superseded files: 3 (`_wave1-cap-extract.json`, `_wave2-cap-extract.json` → `capabilities.json`; root `SESSION-LIFECYCLE.md` → `subsystems/session-lifecycle.md` as evidence)
- conflicts resolved: 9 corpus-level (C1–C9 in `CANONICAL-AUDIT-INDEX.md`) + 1 intra-Phase-5 taxonomy conflict (P5-01's 39 families vs P5-02's 38 — reconciled by the orchestrator, see below)

**CAPABILITY NORMALIZATION**
- raw CAP entries: **674** (all IDs preserved, none renumbered, none deleted)
- canonical product capabilities: **38**
- capability families: **39 registered** = 38 live + 1 retained dead cluster
- product systems: **9**
- implementation/detail classifications: SUBCAPABILITY 192 · CONFIG_BEHAVIOR 110 · MECHANISM 61 · TRUST_PROPERTY 61 · EXPOSURE_BEHAVIOR 60 · ARTIFACT_PROPERTY 57 · FAILURE_BEHAVIOR 47 · IMPLEMENTATION_DETAIL 34
- duplicate/merge candidates: **14**

The headline number: **674 discovered items compress to 38 product capabilities.** 94% of the raw inventory is mechanism, parameter, config, exposure, trust property, failure semantic, or duplicate — evidence, not documentation structure.

**CANONICAL PRODUCT DEFINITION (one sentence)**

> Occam is a locally run host process that turns a URL into content an LLM agent can actually use: it acquires the page through a gated HTTP→browser→(optional third-party) ladder, compiles the result into a token-bounded, focusable, optionally structured representation, returns a typed `ok:false` meaning *the content is unknown* rather than a guess when acquisition fails, and can sign what it did produce so the exact bytes can later be checked for tampering against a key the recipient obtains out of band.

**TOP-LEVEL PRODUCT SYSTEMS** (7 value + 2 enabling)

| ID | System | Families | CAPs |
|----|--------|----------|------|
| PS-1 | Acquisition | 9 | 122 |
| PS-2 | Materialization | 5 | 56 |
| PS-3 | Discovery | 3 | 50 |
| PS-4 | Knowledge extraction | 2 (1 live + dead cluster) | 17 |
| PS-5 | Playbooks | 4 | 68 |
| PS-6 | Trust and provenance | 4 | 86 |
| PS-7 | Monitoring and multi-source | 5 | 79 |
| PS-8 | Runtime and exposure *(enabling)* | 3 | 37 |
| PS-9 | Operator surface *(enabling)* | 4 | 159 |

**ENTRYPOINTS** — 51 named entrypoints across 11 classes. The 15 core MCP tools reach roughly 6 of 9 product systems and are ~29% of entrypoints. Product capability and MCP tool count are demonstrably different things; PS-9 alone (159 CAPs, the largest system) is almost entirely outside MCP.

**ARTIFACT FAMILIES** — 39 artifacts (ART-001…039) in 11 families: CONTENT · STRUCTURE · KNOWLEDGE · DISCOVERY · SESSION · TRUST (absorbed PROVENANCE) · VERIFICATION · DATASET · MONITORING · RUNTIME · OPERATOR. Trust hub is ART-034 (the signing key) feeding ART-007/008/015/022/025. Three dead outputs identified (ART-038 cosign bundle, ART-037 retained cookies, ART-018 lint advisory).

**TRUST MODEL**
- One auto-minted, self-signed, local key. Minted on host start regardless of `OCCAM_RECEIPTS`. Bound to no identity, no PKI, no CA.
- A signature proves exactly: *the holder of this key asserted these bytes, and nobody edited them since.* Nothing about origin, accuracy, authorship, or time.
- Exactly one signature boundary. Above it — fetch, origin, network, compile, clock — nothing is proven.
- Only roots and envelopes are signed. Block leaves, capsule wrappers, verify recipes, time anchors, consensus verdicts and attest aggregates are unsigned cargo.
- The content hash covers **post-budget, post-fit, post-translate markdown** — not the page the server sent.
- A playbook signature excludes the entire top-level `provenance` block, so `verify.score`, `passesGate`, `keyId` and `signedAt` are unsigned and editable (EF-058).
- A watch chain in which *no entry is signed* still returns `history_verified` (EF-059) — link integrity presented as verification.
- `claim_check` is BM25 + regex retrieval; `attest` is an unsigned heuristic aggregate; `crosscheck` is source agreement. None is cryptographic evidence.
- 9 verification-mode surfaces across 2 programs; only the CLI forces an explicit key, and it is unreachable through the operator wrapper.
- Output includes **20 explicitly forbidden public-docs claims**. That list is binding on all future documentation.

**ACQUISITION MODEL**
- 8 rungs, hand-executable: tool gates → preflight/robots → policy → HTTP → *(404/410 or public-reference: STOP)* → browser → optional managed → `ChooseRawFallback`.
- Success gate = ok + body + not-thin + not short-challenge (2000-char threshold in the router).
- **The previously documented cascade was wrong** (EF-056, 10 discrete corrections with `path:line`). Real behavior: only 404/410 plus `IsPublicReferencePage` skip the browser; dual-failure selection uses `FailureRanking` informativeness, *not* markdown density; managed-provider failures never win the surfaced result; managed providers run only under `http_then_browser` after both local backends fail.
- Sessions are three tiers, not one contract: full (headers + `storageState`), HTTP-only, and headers-only with `storageState` silently dropped.
- Proxy reach is partial: workers honor static proxy, rotation is one-shot only (CAP-165), Core HttpClients honor none (CAP-166).
- Browser default timeout is **60s**, not the ~120s in legacy prose.
- No CAPTCHA solving. Private-IP/SSRF is blocked on http/browser but **not** in css-extract (EF-043), and probe masks the block as `network_error` (EF-042).

**STATE MODEL** — 29 state items (ST-01…29) across EPHEMERAL / PROCESS / SESSION / PERSISTENT / PORTABLE ARTIFACT / HOST CONFIGURATION. Verdict on "no file cache by design": **true only for the default live-extract path.** False for the durable `~/.occam` tree (keys, sessions, playbooks, watch chains, batch store), for the opt-in response cache, for the Playwright browser cache, and for host config files Occam writes into. Uninstall does not wipe secrets or state.

**AUTOMATIC BEHAVIOR (top 10 by surprise)**
1. Signing key minted on every host start, regardless of the receipts switch (EF-044)
2. `playbook_save` always signs, even with `OCCAM_RECEIPTS=off` (EF-005)
3. Operator refresh kills host processes by binary name machine-wide, ignoring `OCCAM_HOME` (EF-049)
4. `launch-mcp-host` injects `~/.occam/onboard.json` env into every launch (EF-050)
5. `BrowserPoolManager.InstallShared` calls `StopAll()` on each new WS/Remote session DI, killing the process-wide pool (EF-041)
6. Browser contexts always run with `bypassCSP:true`, and playbooks may execute `page.evaluate` (EF-046)
7. Silent browser escalation and silent no-browser short-circuits on public-reference pages
8. Automatic consent/cookie-banner dismissal, with aggressive retry
9. Automatic cookie-wall / thin-extract downgrade of an apparent success
10. Implicit URL-fragment focus that is omitted from the cache key (EF-045)

29 behaviors total in 7 classes; 11 are effectively undisableable.

**CANONICAL COMPOSITIONS** — 15 supported (CMP-001…015), **8 explicitly rejected**. Learn-order spine: `client_capabilities` → `transcode` (+`verify`) → `probe` → `map`/`search` → `digest` → sessions → `extract_knowledge` → `claim_check`/`attest` → `dataset_export` → `heal`/`save` → `watch`/`crosscheck`/`batch`. Rejected chains matter more than the supported ones for docs honesty: `transcode`→`extract_knowledge` (no markdown handoff), `batch`→`dataset`, `claim_check` JSON→`attest` (attest re-fetches), `heal`→`save` JSON, crosscheck verdict→`verify`, extract's "Receipt"→`verify`.

**PUBLIC CORE (13)** — `acquisition-routing` · `http-acquisition` · `browser-acquisition` · `token-budget` · `focus-selection` · `quality-failure-semantics` · `probe-diagnostics` · `site-mapping` · `digest-synthesis` · `runtime-transports` (stdio) · `mcp-exposure` · `client-context` · `install-onboarding`

**PUBLIC ADVANCED (15)** — `network-safety` · `session-fetch` · `access-consent` · `structured-materialization` · `differential-materialization` · `web-search` · `schema-knowledge-extraction` · `playbook-resolution` · `playbook-authoring` · `playbook-healing` · `playbook-validation` · `receipts` · `verification` · `claims-attestation` · `dataset-provenance`

**OPERATOR / DEVELOPER (4)** — `operator-cli` · `host-connectors` · `packaging-distribution` · `proxy-egress`
**EXPERIMENTAL (5)** — `managed-acquisition` · `response-cache` · `batch-jobs` · `change-monitoring` · `failure-atlas`

**DO NOT DOCUMENT AS FEATURE (2 families + item-level entries)**
- `canonical-knowledge-ir` — built on every transcode then discarded; codecs unreachable (CAP-330/332/333, CAP-328). Ships in the binary; not a product surface.
- `consensus-crosscheck` — the *claim* "consensus proof" is forbidden (EF-031/032); the env gate may appear under experimental opt-ins.
- Item-level: extract's "Receipt" field (telemetry, not Receipt v1 — CAP-287) · `history_verified` on unsigned chains (EF-059) · playbook `verify.score` as a signed quality guarantee (EF-058) · cosign release signing (unused by any install path — EF-053) · every dead entry in `DEAD-OR-UNREACHABLE.md`.

**ENGINEERING FINDINGS AFFECTING THE PRODUCT MODEL**
- Classified: AFFECTS_PUBLIC_SEMANTICS 42 · DOCS_MUST_WARN 43 · DO_NOT_DOCUMENT_BUG_AS_FEATURE 47 · SECURITY_RELEVANT 24 · PERFORMANCE_RELEVANT 12 · NEEDS_FIX_BEFORE_DOC 9 · INTERNAL_ONLY 5
- Ranked `NEEDS_FIX_BEFORE_DOC`: EF-052 (marketplace auto-merge) → EF-043 + EF-013 (css-extract SSRF/cap parity, Nuxt eval) → EF-002 (anonymous context bleed) → EF-053 (cosign theater) → EF-034 (npm package DOA) → EF-035 (tarball omits advertised scripts) → EF-054 (plaintext cookie retention) → EF-051 (Docker HEALTHCHECK)
- **New this phase: EF-058…EF-062**, canonicalized from the conservative trust re-read and orchestrator-verified in code (playbook provenance unsigned; unsigned watch chain verifies; Merkle duplicate-last ambiguity; `reader` profile emits receipts but hides the verifier; no `wrong_key` verdict).
- EF-024 remains **WITHDRAWN**.

**PRODUCT MODEL GRAPH** — `canonical-product-graph.json`: **155 nodes / 280 edges / 12 closed relations**, beside the preserved raw discovery graph (658/588). Hubs: `EP:occam_transcode` (spine), `FAM:verification` (trust), `FAM:acquisition-routing` (shared acquisition). Only family with no `EXPOSES` edge is the dead cluster. `ESCALATES_TO` is a DAG.

**HANDBOOK** — 27 chapters in 7 parts + 4 appendices, 6 spine chapters, 6 reading paths, one recurring worked example threaded through all chapters, 22 anti-chapters. Structure verdict: the proposed 25-chapter progression was **substantially restructured (26 justified deviations)** — trust moves from payoff to prerequisite (`ok:false` at chapter 2), the acquisition ladder is taught as one object rather than three rungs, digest gains a chapter it never had, and every chapter carries a runnable `CHECK` so the book is falsifiable.

**DOCS V2 → V3** — 80 pages inventoried. Actions: KEEP 14 · EXPAND 15 · **REWRITE 40** · ADD 13 · MOVE_TO_HANDBOOK 6 · SPLIT 2 · MERGE 1 · REMOVE 2 · REFERENCE_ONLY 1. 16 ranked P0 honesty fixes, led by trust/provenance claims, the acquisition cascade, cache/live semantics, and install/connect safety.

**UNRESOLVED PRODUCT QUESTIONS:** NONE of the 28 is unanswered. Five bounded uncertainties remain (below).

**CANONICAL MODEL CONFIDENCE:** HIGH for structure, semantics and trust; MEDIUM for a small set of runtime-unverified findings.

**READY FOR DOCUMENTATION SYNTHESIS:** YES

**WHY:** Every one of the 674 CAPs has a canonical owner with verified structural integrity; all 39 families have writer-ready cards; the four models that most constrain honest documentation (trust, acquisition, state, automation) were rebuilt directly from code and each produced corrections to the prior model; the bug/feature boundary is explicit with a ranked pre-documentation blocker list; and the discoverability gate exists to prevent the original failure — capabilities becoming invisible — from recurring.

---

## The five bounded uncertainties

1. **No runtime reproduction** for source-proven findings: EF-041 (dual-WS pool kill), EF-045 (fragment cache collision), EF-051 (Docker health), EF-058/059/060. Proven by construction and code reading only.
2. **Cross-canonicalizer preimage analysis** — four hand-written canonicalizers with no domain-separation tag. No collision was constructed, so it stays an uncertainty rather than a finding.
3. **Marketplace branch-protection state** (EF-052) is outside the repository.
4. **Unmeasured quantities**: tokenizer `heuristic-unicode-v1` error bounds, and managed-provider extraction fidelity versus local markdown. No token-reduction percentage may be published until the first is measured.
5. **Editorial, not factual**: whether `client-context` belongs to PS-8 or PS-2, and whether `access-consent` and `quality-failure-semantics` should be one family (they are two halves of one registered post-processor pipeline).

---

## Orchestrator reconciliation decisions

Agent conclusions do not become canonical automatically. Three reconciliations were required.

**R1 — Taxonomy: 39 vs 38 families.** [P5-01](a00c5d71-aa74-4fdd-8033-48125a03df97) produced 9 systems / 39 families; [P5-02](eea72216-b444-495d-b420-1441849ccdda) stress-tested it and proposed 38. Resolution against code: **accepted** T-1 (`quality-failure-semantics` → PS-1; the post-processors run on the router result at `TranscodePipeline.cs:152-157`, before `FinishMaterialize`) and T-2 (`digest-synthesis` → PS-7; digest fans out over N URLs rather than discovering them, so PS-7 is redefined as composition across multiple fetches). **Modified** T-3: `canonical-knowledge-ir` is retained as a registered family flagged `DEAD_CLUSTER` with zero product capabilities rather than deleted, so its four CAPs keep an owner and its card stays reachable. Net: 39 registered, 38 live. All six CAP-level reassignments (R-1…R-6) applied.

**R2 — Trust findings.** [P5-05](8e1838f8-dbec-4d4a-9b38-3a6a2a3e1a1c) raised five candidates; [P5-10](a78d8cd3-2124-4613-9769-676e2af0af5e) independently confirmed all five; the orchestrator personally verified the two most consequential in source (`PlaybookSignature.cs:29-36,63-84` excludes the whole `provenance` block; `WatchHistory.cs:155` skips signature checks for unsigned entries). Canonicalized as EF-058…062. `EFC-P5-G2-1` was found to duplicate `EFC-P5-05-1` and was merged rather than allocated a number.

**R3 — Exposure calibration.** [P5-01](a00c5d71-aa74-4fdd-8033-48125a03df97) marked nearly every family `public_relevance: HIGH`, which does not discriminate. [P5-09](57f45c23-7e25-4055-97cb-c8c8c471b311) overrode 20 of 39. The exposure matrix wins for documentation decisions; the JSON field is retained as the raw signal.

Structural integrity after reconciliation was verified programmatically: 674 capabilities, ID set exactly equal to `capabilities.json`, every family and system reference resolves, family↔system assignments consistent, and every capability present in its family's member list. All PASS.

---

## The 28 final product questions

**1. What is Occam?** A locally run host process that turns a URL into content an LLM agent can actually use — see the canonical definition above and `PRODUCT-DEFINITION.md` for the one-paragraph and five-bullet forms.

**2. What problem does it solve?** Two failures at once. Without it, an agent asked to read a page either hallucinates the content from memory or receives an empty shell and cannot tell the difference — Occam's `ok:false` contract makes "I do not know" a typed, machine-readable outcome. Second, raw page HTML does not fit a context window usefully; Occam compiles it to budgeted, focusable markdown. `PRODUCT-DEFINITION.md` §4.

**3. What are its major product systems?** Nine: seven value systems (Acquisition, Materialization, Discovery, Knowledge extraction, Playbooks, Trust and provenance, Monitoring and multi-source) and two enabling systems (Runtime and exposure, Operator surface). `PRODUCT-TAXONOMY.md`.

**4. What happens when an agent asks Occam to read something?** Safety and session preflight → robots/throttle → policy selection → HTTP extraction worker → success gate (ok + body + not thin + not a short challenge) → browser escalation unless a 404/410 or public-reference short-circuit applies → optional managed provider only if both local backends failed → post-processor classification that can downgrade an apparent success → materialization (token budget, focus, optional sidecars) → response, optionally with a signed receipt. `PRODUCT-ARCHITECTURE.md` §main flow; `ACQUISITION-ROUTING-MODEL.md`.

**5. How does it handle difficult acquisition?** Through the gated ladder plus operator-supplied capability: sessions for login walls, browser for SPA/JS, managed providers for hard blocks, proxy for network-level blocks, playbooks for site-specific extraction. It does **not** solve CAPTCHAs. `ACQUISITION-ROUTING-MODEL.md` has a 12-row obstacle table stating, per obstacle, what is automatic, what the operator can add, and what Occam refuses to do.

**6. What does it materialize?** Budgeted markdown as the primary product, plus opt-in sidecars: JSON blocks, tables, feed items, chunks, section index, omitted-content manifest, and deltas against a hash the caller already holds. `ARTIFACT-ONTOLOGY.md` families CONTENT and STRUCTURE; the `structured-materialization` and `differential-materialization` cards.

**7. What state does it keep?** 29 items. Durable: signing key, session profiles, playbooks, watch chains, batch job store, `~/.occam` config, installer backups, Playwright browser cache, opt-in response cache. Ephemeral: per-call header temp files, browser contexts, in-memory failure atlas. `STATE-MODEL.md`, including the complete outside-install footprint.

**8. What does a session actually mean?** Not one contract — three tiers. Tier 1 (full: headers + Playwright `storageState`) on transcode/digest/claim_check/attest/dataset_export/batch/watch/crosscheck; Tier 2 (HTTP-only: headers apply, browser never in path) on probe/map; Tier 3 (headers-only, `storageState` silently dropped) on playbook_heal and extract_knowledge. Tool descriptions claiming "same as occam_transcode" are misleading for Tier 3. `session-fetch` card; `subsystems/session-lifecycle.md`.

**9. What are playbooks?** Reusable, per-site extraction recipes with a resolution ladder (local → `WT_PLAYBOOKS_PATH` → community → seeds), an authoring/save path that always signs, a heal loop that analyses a DOM skeleton to draft a recipe, and an advisory lint. They are in-band overlays on the acquisition spine, not a parallel system. Caveat: their signature does not cover the quality score (EF-058). PS-5 cards.

**10. What can be cryptographically verified?** That a set of bytes Occam produced has not changed since Occam signed them, checked against a key the verifier obtains out of band. That is the whole guarantee. `TRUST-MODEL.md`.

**11. What can NOT be cryptographically verified?** That the content matches what the origin served; that the origin is authentic; that the content is accurate; when it was fetched (the time anchor is host-clock self-assertion); who signed it in any identity sense; that a playbook's quality score is genuine (EF-058); that a watch chain was ever signed (EF-059); that claim_check, attest or crosscheck results mean anything cryptographic. 20 forbidden claims are enumerated.

**12. Relationship between receipts, Merkle data and capsules?** A receipt is the signed envelope over an acquisition fact and a content hash. Merkle structures let a single signed root cover many leaves (blocks, dataset rows) so an individual leaf can be proven included without the whole set — but the leaves themselves and the leaf counts are unsigned. A capsule is a portable wrapper bundling a receipt with the material needed to check it later; the wrapper itself is not separately signed. `TRUST-MODEL.md` §primitives; `ARTIFACT-ONTOLOGY.md` TRUST family.

**13. What do `claim_check` and `attest` actually guarantee?** `claim_check` guarantees only that a BM25-plus-regex retrieval found (or did not find) supporting spans in content Occam fetched. `attest` guarantees only that it re-fetched the cited sources and produced an unsigned heuristic status per citation. Neither is a cryptographic attestation; neither judges truth. Their names overstate them, which is recorded as a naming-honesty finding.

**14. What is crosscheck/consensus?** Fetching the same question across multiple sources and reporting agreement. Agreement is not evidence of correctness and the verdict is unsigned and not verifiable (EF-031/032). Classified `DO_NOT_DOCUMENT_AS_FEATURE` for the consensus *claim*, while the opt-in gate may be listed as experimental.

**15. What can watch do?** Monitor a URL over time, producing a hash-linked history chain with diffs between observations, opt-in via `OCCAM_WATCH_MCP`. Limits: store races, no removal path (EF-019/020), and an unsigned chain still reports `history_verified` (EF-059). `change-monitoring` card.

**16. What does `dataset_export` produce?** An auditable set of URLs with a manifest and per-row Merkle leaves, exportable for later checking. Manifest verification is CLI-only (EF-018), so the MCP-only user cannot complete the loop. `dataset-provenance` card.

**17. Why are there profiles?** Because different hosts and use cases need different tool surfaces: profiles narrow which of the registered tools a client sees, keeping the exposed set appropriate. The mechanism has a known defect — `reader` emits receipts but hides `occam_verify` (EF-061). `mcp-exposure` card; `PROFILE-TOOL-MATRIX.md`.

**18. Why are some tools opt-in?** Because they carry cost, risk, or immaturity that should not be on by default: `occam_batch_*` (`OCCAM_BATCH_MCP`), `occam_watch` (`OCCAM_WATCH_MCP`), `occam_crosscheck` (`OCCAM_CONSENSUS_MCP`), `occam_failure_atlas` (`OCCAM_ATLAS_MCP`). Opt-in must not mean undiscoverable — that is exactly what `DISCOVERABILITY-GATE.md` guards.

**19. What exists outside MCP?** Most of PS-9 (159 CAPs — the largest system): the host CLI verbs, the `occam` operator wrapper and its subcommands, doctor, install/onboard, connect host adapters, session import/export, process control, skill install, and the packaging/release surface. Also the alternate runtime modes: WebSocket, remote WSS+JWT, and the batch HTTP server. 51 entrypoints total. `ENTRYPOINT-MODEL.md`.

**20. What does the CLI expose?** Two surfaces: host binary verbs (including offline receipt verification, which is the only surface that forces an explicit public key) and the `occam` wrapper (doctor, install, connect, session, refresh, update-check, control loop). Some advertised commands are missing from the Level B tarball (EF-035). `operator-cli` and `install-onboarding` cards; `CLI-SURFACE.md`.

**21. How does install → doctor → connect work?** Install provisions the host binary, Node workers and a browser; doctor verifies the toolchain and repairs what it can; connect writes MCP configuration into detected host applications, with backups. This chain mutates the machine outside Occam's own directory — that footprint, its backups and its rollback are documented honestly in the `install-onboarding` and `host-connectors` cards, along with EF-049 (name-wide process kill) and EF-050 (env injection).

**22. What is automatic/silent?** 29 behaviors in 7 classes, 11 undisableable — see the top-10 list above and `AUTOMATION-MODEL.md`, which also states, per behavior, whether the response discloses it and whether public docs must disclose it.

**23. Which capabilities depend on env/config?** Managed providers, search (fails closed without a provider), proxy/egress, response cache, all four opt-in tools, receipts behavior, sessions root, browser channel, and the alternate transports. Complete catalog in `ENVIRONMENT-VARIABLES.md`, cross-checked by `CONFIG-NEGATIVE-SPACE.md`; per-family effects in each card's Configuration section.

**24. What actually ships?** The AOT host binary compiled from the *entire* Core glob (so unreachable types still ship), the Node workers, the operator scripts, the Level B tarball, the Docker image, and the npm packages. `SHIPPED-CODE-MAP.md`. The governing rule for documentation: **shipped ≠ reachable ≠ documentable.**

**25. Which important behaviors are currently bugs rather than intended features?** 47 items are classified `DO_NOT_DOCUMENT_BUG_AS_FEATURE` and 9 are `NEEDS_FIX_BEFORE_DOC` (ranked above). The most consequential for the product model: the cascade behavior everyone believed (EF-056 — a model error, not a code bug), the unsigned playbook provenance (EF-058), `history_verified` on unsigned chains (EF-059), the browser pool kill (EF-041), and the cosign signing that no install path checks (EF-053). `PRODUCT-VS-ENGINEERING.md`.

**26. How should a new user discover the advanced capabilities?** Through the exposure matrix plus the discoverability gate: `PUBLIC_CORE` requires at least three paths including a task-oriented one; `PUBLIC_ADVANCED` requires at least two including a reference page. Rules R1–R10 in `DISCOVERABILITY-GATE.md`, with a design for mechanical checking inside `scripts/check-docs.mjs` (design only — nothing implemented).

**27. How should an AI model discover them?** Via `llms.txt` as the agent route, MCP tool descriptions and server instructions as the in-band route, and `client_capabilities` as the session-start handshake that sizes later reads. The gate requires every live family slug to be reachable from the agent route, so no capability is agent-invisible merely because it is advanced.

**28. How would a developer learn the entire system from first principles?** The 27-chapter handbook in `HANDBOOK-OUTLINE.md`, which teaches in dependency order rather than feature order: the honesty contract before receipts, the acquisition ladder before playbooks, materialization before the token budget. Six spine chapters yield roughly 80% of understanding; every chapter carries a runnable `CHECK` so the reader can falsify the book against the running system.

---

## Files created / updated in Phase 5

**Created (46):** `CANONICAL-AUDIT-INDEX.md` · `PHASE5-SHARED-INSTRUCTIONS.md` · `CANONICAL-CAPABILITIES.md` · `canonical-capabilities.json` · `PRODUCT-TAXONOMY.md` · `PRODUCT-DEFINITION.md` · `PRODUCT-ARCHITECTURE.md` · `ENTRYPOINT-MODEL.md` · `ARTIFACT-ONTOLOGY.md` · `TRUST-MODEL.md` · `ACQUISITION-ROUTING-MODEL.md` · `STATE-MODEL.md` · `AUTOMATION-MODEL.md` · `COMPOSITION-MODEL.md` · `USE-CASE-MODEL.md` · `DOCUMENTATION-EXPOSURE-MATRIX.md` · `DISCOVERABILITY-GATE.md` · `PRODUCT-VS-ENGINEERING.md` · `CANONICAL-PRODUCT-GRAPH.md` · `canonical-product-graph.json` · `HANDBOOK-OUTLINE.md` · `DOCS-V3-PLAN.md` · `PHASE5-REPORT.md` · `canonical/` (39 family cards)

**Updated (2):** `ENGINEERING-FINDINGS.md` (EF-058…062) · `CANONICAL-CAPABILITIES.md` (reconciliation header)

**Preserved untouched:** `capabilities.json`, `capability-graph.json`, all Wave 1–4 artifacts, and every public doc (`README.md`, `INSTALL.md`, `docs/`, `llms.txt`, `mkdocs.yml`, `MCP_API_SPEC.md`). No product code, script or test was modified — verified with `git status`.

---

## Next recommended phase

Resolve the ranked `NEEDS_FIX_BEFORE_DOC` shortlist (starting with EF-052, EF-043/EF-013 and EF-002) and decide the naming-honesty questions (EF-058/059, `claim_check`/`attest`/`crosscheck`) **before** writing Docs v3, because those decisions determine what the trust and acquisition chapters are allowed to say.
