# Docs v2 → v3 delta plan

**Agent:** P5-T  
**Scope:** public documentation information architecture only; public docs remain frozen.  
**Canonical model:** 9 product systems, 39 registered families, 38 live product capabilities, 674 CAP records (`canonical-capabilities.json:5-21`).  
**Method:** preserve accurate v2 material, correct false claims, expose hidden capability families, and move operator/developer depth to a handbook route. Public prose is evidence only after reconciliation against the Phase-5 canonical model.

## Executive verdict

Docs v2 is a strong task-oriented shell around the fifteen always-on MCP tools, but it is not yet a faithful product map. It explains the default read path, typed failures, focused materialization, tool selection, and host connection well. Its structural weakness is **tool-count myopia**: PS-7, PS-8, PS-9, silent automation, advanced materialization, and non-MCP operator surfaces are either buried or absent. Its semantic weakness is concentrated in trust, routing, cache/live claims, session isolation, connect rollback, and distribution assurances.

This is a delta plan, not a blank-slate rewrite. Keep the useful task guides and examples; add a nine-system capability layer; consolidate duplicated install/connect/trust prose; and rewrite only pages that carry contradictory semantics.

Size legend: **S** ≤40 non-empty lines, **M** 41–100, **L** >100 (rough, based on the current file).

## 1. P0 honesty fixes

Ranked by risk of causing an agent or operator to take a wrong action.

| Rank | Current locations | Required correction | Evidence | Dependency |
|---:|---|---|---|---|
| 1 | `README.md`, `docs/index.md`, `docs/what-is-occam.md`, `docs/trust-and-safety.md`, `docs/how-occam-works.md`, `docs/concepts.md`, `docs/receipts.md`, `docs/guides/verify-sources.md` | Replace “verifiable/cryptographic provenance,” “prove what was extracted,” and receipt-as-origin-proof language with the exact local self-signed integrity guarantee. A receipt proves that the holder of a supplied key asserted exact bytes; it does not prove identity, origin truth, fetch occurrence, or time. | `TRUST-MODEL.md:13-32,502-528`; EF-027 | None; canonical trust wording already exists. |
| 2 | `MCP_API_SPEC.md`, `docs/tools/occam_transcode.md`, `docs/concepts.md`, `docs/how-occam-works.md` | Replace the universal HTTP→browser→managed story with the gated ladder: usable HTTP stops; 404/410 stop; public-reference failures stop; managed runs only after both local failures and only managed success surfaces; dual-failure ranking is HTTP vs browser by `FailureRanking`. | EF-056; GAP-001/014; `ACQUISITION-ROUTING-MODEL.md:44-55,96-115` | None for documentation: corrected canonical model has landed. Any future router behavior change must version separately. |
| 3 | `MCP_API_SPEC.md`, `docs/concepts.md`, `docs/what-is-occam.md`, `docs/trust-and-safety.md`, `docs/faq.md`, `docs/roadmap.md` | Remove “every call live / no file cache” absolutes. `cache_ttl_s` uses an opt-in disk response cache and replays the prior signed envelope. Correct “in-memory” wording and explain fragment/cache hazards. | FLOW-019; ART-035; EF-001/045; `TRUST-MODEL.md:100-106` | None; cache remains EXPERIMENTAL and must carry warnings. |
| 4 | `README.md`, `INSTALL.md`, `docs/trust-and-safety.md`, `docs/trust/installation-safety.md`, `docs/mcp-hosts.md`, connect pages | Narrow “safe connect,” rollback, and atomic-safety assurances. Restart-required file hosts do not get the advertised verification/rollback guarantee; installer replacement has no rollback; onboarding can leave state after failure. | EF-021/028/029/035; `PRODUCT-VS-ENGINEERING.md:59-68,102-111` | EF-035 blocks Level-B connect/contract claims until fixed; other surfaces may be documented with warnings. |
| 5 | `docs/configuration.md`, `docs/concepts.md`, `docs/receipts.md`, `MCP_API_SPEC.md`, troubleshooting | State that `OCCAM_RECEIPTS=off` controls most receipt emission, not all signing: playbook save still signs and the host always mints/loads a key. | EF-005/044; GAP-005; TRUST forbidden claim #13 | None; warn now. |
| 6 | `docs/tools/occam_verify.md`, `docs/receipts.md`, `docs/receipt_verification.md`, `MCP_API_SPEC.md` | Document MCP/CLI asymmetry: MCP defaults to the local key; unknown MCP modes fall back to offline; live verify drops original context and collapses failures; `history_verified` may be wholly unsigned; CLI requires `--pubkey`; manifest verify is CLI-only; wrong key and tamper share `signature_invalid`. | EF-011/012/018/025/026/027; EFC-P5-05-2/5; `TRUST-MODEL.md:259-289` | Withhold “signed/verified history” until EFC-P5-05-2 is fixed/canonicalized. |
| 7 | `docs/tools/occam_crosscheck.md`, `docs/tools-reference.md`, `docs/choosing-a-tool.md`, `MCP_API_SPEC.md` | Remove “independently re-derivable verdict,” genuineness, and consensus implications. It is one process/egress comparing extraction vantages; the aggregate verdict is unsigned and no shipped verifier re-derives it. | EF-032; TRUST forbidden claims #9–10 | No blocker to an explicitly experimental observation page; do not feature it as trust proof. |
| 8 | `docs/tools/occam_claim_check.md`, `docs/tools/occam_attest.md`, guides/examples/recipes, `MCP_API_SPEC.md` | Define claim check as BM25 lexical retrieval plus membership proofs, never semantic absence; define attest as an unsigned narrow regex classifier, not a cryptographic or general semantic gate. | `TRUST-MODEL.md:138-161,351-361`; EF-016 | None; rewrite claims now. |
| 9 | `docs/tools/occam_playbook_resolve.md`, `docs/tools/occam_playbook_save.md`, lint page, `MCP_API_SPEC.md` | Do not call playbooks self-authenticating or treat `verify.score`, `passesGate`, `signedAt`, or claimed `keyId` as signed. `unknown_key` is not authenticated foreign authorship; lint does not guarantee save/resolve acceptance; the Core sanitizer is dead. | EF-015/047/052; EFC-P5-05-1; TRUST X1–X2 | Withhold trusted marketplace/foreign-author claims until EF-052 and EFC-P5-05-1 are fixed. |
| 10 | session guide/example/configuration/concepts/API | Remove cross-tool full-session and isolation implications. Probe/map/heal/extract often carry headers only; anonymous pooled browser contexts are not a security boundary; imported plaintext cookies remain under `_imports/`. | EF-002/017/039/040/054 | EF-002 blocks isolation guarantees; EF-054 blocks “no retained secrets.” |
| 11 | structured extraction guide/example/tool/API | State that extract telemetry named `receipt` is not Receipt v1; confidence is non-informative; row `base_selector` is unreachable; malformed kinds can escape typed errors; Nuxt/CSS safety parity is blocked. | EF-006/013/014/043/055; GAP-004/024/025/026 | Do not document Nuxt eval or CSS extraction as safe for untrusted pages until EF-013/043 are fixed. |
| 12 | configuration, local-first, concepts, API | Scope SSRF/proxy statements per path. Managed/search/translation/CSS and Core clients do not share one guard/proxy policy; proxy rotation misses daemons/CSS/skeleton; an empty proxy file suppresses the inline list. Managed providers receive URLs and may supply bytes that are signed. | EF-003/007/042/043/057; CAP-165/166; GAP-019/030 | EF-043 blocks CSS safety parity claims. |
| 13 | README/install/roadmap/index/transports | Keep npm explicitly unavailable; remove signed-supply-chain and Docker-health implications; distinguish tarball, npm, Docker, and source capabilities. | EF-034/035/051/052/053; `SHIPPED-CODE-MAP.md` | npm, Docker-health, trusted marketplace, cosign-install claims remain withheld until their listed fixes. |
| 14 | README/install/getting-started/roadmap/llms/API | Stop using fixed “15 tools” as a health/product model. Runtime `tools/list` varies by profile and independent opt-ins; crosscheck bypasses profile filtering; server instructions can advertise gated watch. | EF-022/031/033/036; GAP-012; `DISCOVERABILITY-GATE.md:R1,R2,R7` | None; use registry/runtime language now. |
| 15 | batch/watch/atlas/dataset pages | Add retention, concurrency, top-level-success, and trust limits: batch has no Receipt v1 and no retention; watch has no eviction and multi-writer races; unsigned history can verify; atlas is in-memory telemetry, not proof that a host is a dead end; dataset `ok` is export completion, not all rows. | EF-018/019/020/037/038; EF-024 WITHDRAWN | Withhold signed-history wording per EFC-P5-05-2; never revive EF-024. |
| 16 | `MCP_API_SPEC.md` failure table and any copied timeout prose | Correct browser default from 120s to 60s and state the configured clamp/queue distinction. | `ACQUISITION-ROUTING-MODEL.md:203-213,412-420` | None. |

## 2. Docs v2 inventory and action ledger

### 2.1 Root routes and nav manifest

Audience codes: **A** agent, **H** human user, **O** operator, **D** developer/auditor.

| Path | Purpose · audience · size | Canonical families covered | Action | Why / evidence | Effort · priority · blocker |
|---|---|---|---|---|---|
| `README.md` | First contact, install, host summary · H/O · L | `install-onboarding`, `host-connectors`, `mcp-exposure`, `quality-failure-semantics`, `receipts` | `REWRITE` | Trust and safe-connect claims overreach; fixed-count framing. TRUST #1–6; EF-021/028/029/031 | M · P0 · EF-035 limits Level B |
| `INSTALL.md` | Canonical automated installer route · O/A · L | `install-onboarding`, `host-connectors`, `operator-cli`, `packaging-distribution` | `REWRITE` | Preserve one-command route, but add destructive replacement/onboard residue and shipped-surface boundaries. EF-028/029/034/035 | M · P0 · EF-034/035 |
| `llms.txt` | Compact agent documentation map · A · M | Most tool-exposed families; weak PS-7/8/9 | `EXPAND` | Preserve selective loading rules; add verbatim PUBLIC_CORE/ADVANCED family index and trust limits per discoverability R4–R6. | M · P1 · none |
| `MCP_API_SPEC.md` | Normative response semantics · A/D · L | Nearly all MCP-exposed families | `REWRITE` | High-value contract, but contains cache/live, routing, trust, timeout, playbook-signature, verify and out-of-scope contradictions. EF-005/011/012/027/056 | L · P0 · several limited claims withheld |
| `mkdocs.yml` | Nav/exclusion manifest, not a content page · D · M | IA exposure only | `REWRITE` | Retarget nav to four routes + nine systems + handbook; current developer/maintenance exclusions hide useful operator context while task/tool lists dominate. | S · P1 · after target files exist |

### 2.2 Site pages: foundations, trust, install, connect, reference

| Path | Purpose · audience · size | Canonical families covered | Action | Why / evidence | Effort · priority · blocker |
|---|---|---|---|---|---|
| `docs/index.md` | Human hub/task router · H/A · M | Broad tool families, weak PS-7/8/9 | `EXPAND` | Keep task table and SoT order; add nine-system map and four audience routes. DISC R1–R5. | M · P1 · none |
| `docs/quick-start.md` | Install→connect→first read · H/O · M | `install-onboarding`, `host-connectors`, `quality-failure-semantics`, `receipts` | `REWRITE` | Preserve short path; remove fixed count and overbroad connect/receipt assurances. EF-021/028/029. | M · P0 · EF-035 limited |
| `docs/getting-started.md` | First read + operator CLI + wiring · H/O/A · M | `mcp-exposure`, `operator-cli`, `session-fetch`, `host-connectors` | `SPLIT` | Keep first-read tutorial; move CLI/session/programmatic details to handbook/reference. EF-025/054. | M · P1 · EF-054 claim limit |
| `docs/install.md` | Short duplicate installer page · H/O · M | `install-onboarding`, `packaging-distribution` | `MERGE` | Merge unique human explanation into root `INSTALL.md`; one canonical installer route. | S · P1 · after INSTALL rewrite |
| `docs/what-is-occam.md` | Human product explanation · H · M | acquisition/materialization, `receipts`, `quality-failure-semantics` | `REWRITE` | Preserve problem framing; replace “prove/cryptographic provenance” and no-cache absolute. TRUST #1–6; FLOW-019. | S · P0 · none |
| `docs/how-occam-works.md` | Plain-language mental model · H/A · M | PS-1/2/5/6/8 | `REWRITE` | Preserve simple flow; add gated acquisition and exact receipt boundary. EF-056; TRUST §1. | M · P0 · none |
| `docs/concepts.md` | Cross-cutting concepts · A/H · M | PS-1/2/5/6 | `REWRITE` | Cache is disk not in-memory; managed and routing incomplete; receipt/master-switch claims wrong. EF-001/005/044/056. | M · P0 · none |
| `docs/choosing-a-tool.md` | Goal→tool router and profiles · A · L | Tool families across PS-1–7, `mcp-exposure` | `REWRITE` | Keep excellent decision-table form; qualify claim/attest/crosscheck and profile producer-without-verifier trap. TRUST #7–10; EFC-P5-05-4. | M · P0 · none |
| `docs/faq.md` | Short operational answers · H/O · S | install, cache, tools, trust | `REWRITE` | “No cache” and “verify extraction happened” are false expansions. FLOW-019; TRUST #19. | S · P0 · none |
| `docs/troubleshooting.md` | Symptom→fix runbook · O · M | acquisition, install, exposure, receipts | `EXPAND` | Keep operational form; add wrapper-vs-host verbs, session retention, proxy/path scope, profile/opt-in diagnostics. EF-025/031/050/054/057. | M · P1 · none |
| `docs/configuration.md` | Env catalog and defaults · O/D · L | broad PS-1/2/5/6/7/8/9 | `REWRITE` | Valuable catalog; correct receipts switch, proxy coverage, managed privacy, cache type, profiles/gates, onboard env precedence. EF-003/005/007/044/050/057. | L · P0 · EF-043 limits CSS parity |
| `docs/failure-codes.md` | Failure meanings and actions · A/O · M | `quality-failure-semantics`, PS-1/3/4/5 | `REWRITE` | Preserve action-oriented taxonomy; remove unreachable codes, add probe SSRF masking and fallback semantics. EF-008/042/055/056. | M · P0 · none |
| `docs/transports.md` | stdio/WS/Remote/batch + direct verbs · O/D · M | `runtime-transports`, `operator-cli`, `batch-jobs` | `EXPAND` | Preserve accurate modes; add multi-session pool disruption, WS cap asymmetry, and wrapper-unreachable direct verbs. EF-025/041; GAP-013. | M · P1 · none |
| `docs/mcp-hosts.md` | Connector support tiers and config safety · O · M | `host-connectors`, `install-onboarding` | `REWRITE` | Host-tier honesty is good; rollback guarantee is false for restart-required hosts. EF-021. | M · P0 · none |
| `docs/reference/mcp-api.md` | Thin pointer to root API spec · A/D · S | `mcp-exposure` | `REFERENCE_ONLY` | Keep as site bridge; no duplicated contract prose. | S · P2 · after API rewrite |
| `docs/tools-reference.md` | Compact all-tool reference · A/D · L | all MCP tool families | `REWRITE` | Preserve compact lookup; correct trust/routing/experimental limits and link family pages. EF-011/015/018/032/056. | L · P0 · blocked claims omitted |
| `docs/receipt_verification.md` | Normative byte-level receipt format · D/auditor · L | `receipts`, `verification` | `REWRITE` | Regenerate formula and executable names from code; include proof boundary and key distribution limits. EF-026/027; TRUST C2–C7. | L · P0 · none |
| `docs/receipts.md` | Human receipt and CLI guide · H/O/A · M | `receipts`, `verification`, dataset/watch links | `REWRITE` | Preserve walkthrough; correct master switch, key mint, CLI reachability, history, origin/time guarantees. EF-005/025/044; TRUST §7. | M · P0 · EFC-P5-05-2 |
| `docs/trust-and-safety.md` | Trust overview · H/O · S | `network-safety`, `quality-failure-semantics`, `receipts`, install | `REWRITE` | Current short answers falsely say no disk cache, prove extraction, and universal rollback. FLOW-019; EF-021; TRUST. | M · P0 · none |
| `docs/trust/local-first.md` | Local-first boundary · H/O · S | `http-acquisition`, `browser-acquisition`, `managed-acquisition`, `proxy-egress` | `REWRITE` | Keep default-local explanation; explicitly disclose managed/search/translation/TSA egress and path-specific proxy/guard behavior. EF-003/007. | S · P0 · none |
| `docs/trust/honest-failures.md` | `ok:false` handling · A/H · S | `quality-failure-semantics` | `KEEP` | Strong and aligned: unknown content; thin≠short; honesty≠verification. TRUST §9 Q5. | S · P2 · none |
| `docs/trust/installation-safety.md` | Artifact/connect safety · O · S | `install-onboarding`, `host-connectors`, `packaging-distribution` | `REWRITE` | Correct rollback/destructive install/onboard residue/session imports/supply-chain limits. EF-021/028/029/053/054. | M · P0 · EF-053/054 limits |
| `docs/trust/security-policy.md` | Vulnerability reporting pointer · H/D · S | cross-cutting security | `KEEP` | Appropriate narrow public policy; not a capability claim. | S · P2 · none |
| `docs/roadmap.md` | Shipped log and direction · H/D · S | broad release surface | `MOVE_TO_HANDBOOK` | Release/status material is operator/developer context; current shipped table repeats live-only and tool-count framing and stale “12-page” claim. FLOW-019; C8. | S · P1 · packaging blockers |
| `docs/quality-baseline.md` | Maintainer quality/gate baseline · D · M | quality mechanisms, gates | `MOVE_TO_HANDBOOK` | Useful contributor evidence, not a user task page; keep under developer/maintainer handbook. | S · P2 · none |
| `docs/architecture/semantic-contract.md` | RC semantic invariants · D · M | PS-1/2 trust dimensions | `MOVE_TO_HANDBOOK` | Preserve architecture depth under contributor handbook; reconcile with canonical system pages first. | M · P2 · none |
| `docs/ask-ai.md` | Docs-loading instructions · A/H · S | `mcp-exposure`/documentation route | `EXPAND` | Keep selective loading; route agents through `llms.txt`, family index and runtime descriptions, not all pages. DISC R1/R4. | S · P2 · none |

### 2.3 Connect pages

| Path | Purpose · audience · size | Families | Action | Why / evidence | Effort · priority · blocker |
|---|---|---|---|---|---|
| `docs/connect/index.md` | Connect flow and status meanings · O · S | `host-connectors` | `KEEP` | Good support-tier entry; link to corrected safety/rollback limits. | S · P2 · none |
| `docs/connect/automatic.md` | Auto-connect behavior · O · S | `host-connectors`, `install-onboarding` | `REWRITE` | Add restart-required rollback limit and onboard env source. EF-021/050. | S · P0 · none |
| `docs/connect/explicit-only.md` | Config-validated `--only` hosts · O · S | `host-connectors` | `KEEP` | Correctly distinguishes implemented config shape from live validation. | S · P2 · none |
| `docs/connect/manual.md` | Generic stdio registration · O · S | `host-connectors`, `runtime-transports` | `KEEP` | Durable fallback; keep one canonical launcher snippet. | S · P2 · none |
| `docs/connect/after-install.md` | Explain connect report and next action · O · S | `install-onboarding`, `host-connectors` | `EXPAND` | Add persisted `onboard.json` environment precedence and cleanup after failed onboarding. EF-029/050. | S · P1 · none |
| `docs/connect/troubleshooting.md` | Connection symptom map · O · S | `host-connectors` | `KEEP` | Focused and non-duplicative; link to full operator troubleshooting. | S · P2 · none |

### 2.4 Task guides and examples

| Path | Purpose · audience · size | Families | Action | Why / evidence | Effort · priority · blocker |
|---|---|---|---|---|---|
| `docs/guides/read-a-page.md` | Minimal transcode workflow · A/H · S | acquisition, token/focus, failures | `KEEP` | Strong default task route; add links to acquisition/materialization family pages during nav pass. | S · P2 · none |
| `docs/guides/research-multiple.md` | Multi-source digest workflow · A · S | `digest-synthesis`, token/focus | `KEEP` | Correctly prefers digest to N calls. | S · P2 · none |
| `docs/guides/search-and-discover.md` | Search/map workflow · A · S | `web-search`, `site-mapping`, `probe-diagnostics` | `EXPAND` | Keep task split; make provider prerequisite/fail-closed search visible. DISC matrix. | S · P1 · none |
| `docs/guides/structured-extraction.md` | Schema extraction workflow · A · S | `schema-knowledge-extraction`, playbook resolution | `REWRITE` | Add telemetry-not-receipt, confidence/row-mode limits and safety boundary. EF-006/013/014/043/055. | M · P0 · EF-013/043 |
| `docs/guides/verify-sources.md` | Receipt verification workflow · A/H · S | `verification`, `receipts` | `REWRITE` | Remove “provenance” expansion; distinguish local-key default, live vs offline, and CLI-only manifest. TRUST §7. | M · P0 · none |
| `docs/guides/claims.md` | Single-claim workflow · A/H · S | `claims-attestation` | `REWRITE` | State lexical retrieval limits and narrow attest classifier. TRUST C8/C9. | S · P0 · none |
| `docs/guides/sessions.md` | Authenticated-page workflow · O/A · S | `session-fetch` | `REWRITE` | Add per-tool session matrix, anonymous pool non-isolation, cold path, retained imports. EF-002/017/039/040/054. | M · P0 · EF-002/054 |
| `docs/examples/index.md` | Example index · A/H · S | task map | `KEEP` | Useful lightweight route; update destinations after consolidation. | S · P2 · none |
| `docs/examples/read-one-page.md` | Minimal/focused read example · A · S | acquisition, token/focus | `KEEP` | Accurate core path; replace receipt wording only through shared trust links. | S · P2 · none |
| `docs/examples/research-several.md` | Digest example · A · S | `digest-synthesis` | `KEEP` | Concise, correct task example. | S · P2 · none |
| `docs/examples/search-then-research.md` | Search→digest example · A · S | `web-search`, `digest-synthesis` | `KEEP` | Preserve; co-locate provider setup link. | S · P2 · none |
| `docs/examples/discover-then-research.md` | Map→digest example · A · S | `site-mapping`, `digest-synthesis` | `KEEP` | Good composition example. | S · P2 · none |
| `docs/examples/verify-receipt.md` | Offline verify example · A/H · S | `verification`, `receipts` | `REWRITE` | Add required trusted PEM and exact “what it proves/does not prove.” TRUST C4/C12. | S · P0 · none |
| `docs/examples/check-a-claim.md` | Claim/attest example · A · S | `claims-attestation` | `REWRITE` | “Proven matching blocks” must not become claim truth; expose regex classifier limit. TRUST #7–8. | S · P0 · none |
| `docs/examples/structured-extraction.md` | Knowledge-schema example · A · S | `schema-knowledge-extraction` | `REWRITE` | Add false-confidence/telemetry/safety limits and no row-mode example. EF-006/013/014. | S · P0 · EF-013 |
| `docs/examples/session-profile.md` | Session example · A/O · S | `session-fetch` | `EXPAND` | Add browser storage-state vs headers-only matrix and secret-retention cleanup. EF-017/054. | S · P1 · EF-054 claim |
| `docs/examples/dataset-export.md` | Signed export example · A/auditor · S | `dataset-provenance`, `verification` | `REWRITE` | Inspect per-row success; manifest verification is direct CLI only; signature is local-key integrity. EF-018/025. | S · P0 · none |
| `docs/recipes.md` | Multi-tool workflow collection · A · L | broad PS-1/3/4/5/6/7 | `SPLIT` | Preserve proven compositions; split into task recipes and advanced/experimental recipes, correcting trust/watch language. FLOW-001…022; TRUST. | L · P1 · EFC-P5-05-2 |

### 2.5 Tool pages

| Path | Purpose · audience · size | Primary families | Action | Why / evidence | Effort · priority · blocker |
|---|---|---|---|---|---|
| `docs/tools/index.md` | Tool-by-job index · A · M | `mcp-exposure`, all tool families | `EXPAND` | Keep job table; add system/family links and explicit experimental/trust limits. DISC R1/R2. | M · P1 · none |
| `docs/tools/occam_client_capabilities.md` | Ambient budget handshake · A · S | `client-context`, `token-budget` | `KEEP` | Useful invisible-default explanation; promote from tool-only burial. | S · P2 · none |
| `docs/tools/occam_transcode.md` | One-page extraction contract · A · L | PS-1/2, receipts | `REWRITE` | Correct cascade/public-reference/managed failure, cache replay/key omissions, session and receipt boundaries. EF-001/045/056. | L · P0 · none |
| `docs/tools/occam_probe.md` | Pre-fetch diagnostics · A · M | `probe-diagnostics`, `network-safety` | `EXPAND` | Preserve useful classifier; warn policy blocks may surface as `network_error`. EF-042. | S · P1 · none |
| `docs/tools/occam_digest.md` | Multi-URL synthesis · A · M | `digest-synthesis`, token/focus, receipts | `EXPAND` | Add per-item trust qualification and session/global-budget boundaries. EF-016/017. | M · P1 · none |
| `docs/tools/occam_map.md` | Site link discovery · A · M | `site-mapping`, `session-fetch`, network safety | `EXPAND` | Add HTTP-only and headers-only session/proxy/guard scope. EF-007/017. | S · P1 · none |
| `docs/tools/occam_search.md` | Provider-backed web search · A/O · M | `web-search` | `EXPAND` | Keep fail-closed setup; disclose provider egress and lack of shared outbound guard. Exposure matrix; network-safety scope. | S · P1 · none |
| `docs/tools/occam_extract_knowledge.md` | Schema-driven facts · A · M | `schema-knowledge-extraction` | `REWRITE` | Existing telemetry warning is good; add confidence, row-mode, malformed-schema, CSS/Nuxt safety and session limits. EF-006/013/014/017/043/055. | M · P0 · EF-013/043 |
| `docs/tools/occam_playbook_resolve.md` | Recipe tier resolution · A/author · M | `playbook-resolution` | `REWRITE` | `unknown_key`, score/pass gate, well-known bounds and marketplace trust are overstated. EF-048/052; EFC-P5-05-1; TRUST X1. | M · P0 · EF-052/EFC |
| `docs/tools/occam_playbook_heal.md` | Skeleton evidence for repair · A/author · M | `playbook-healing` | `EXPAND` | Preserve clear “host drafts” boundary; disclose browser code-like trust and headers-only session. EF-017/046. | S · P1 · none |
| `docs/tools/occam_playbook_save.md` | Verify/sign/save local recipe · A/author · M | `playbook-authoring`, receipts | `REWRITE` | Not self-authenticating; quality claims are unsigned; save always signs; local save is not publish sanitation. EF-005/047; TRUST X1. | M · P0 · EFC-P5-05-1 claim |
| `docs/tools/occam_playbook_lint.md` | Static recipe validation · A/author · M | `playbook-validation` | `REWRITE` | “errors break resolve/save” implies parser equivalence; lint is advisory; community sanitizer is not live. EF-015/047. | S · P0 · none |
| `docs/tools/occam_verify.md` | Receipt/proof/history verification · A/auditor · M | `verification`, receipts/change | `REWRITE` | “without trusting host” is too broad; add local-key default, live context loss, mode fallback, unsigned-history verdict and wrong-key ambiguity. EF-011/012; EFC-P5-05-2/5. | M · P0 · history claim withheld |
| `docs/tools/occam_claim_check.md` | Claim-relevant block retrieval · A · M | `claims-attestation`, receipts | `REWRITE` | Existing retrieval caveat is good; `found:false` text and “third-party” wording still need exact completeness/key limits. TRUST C8; EF-016. | M · P0 · none |
| `docs/tools/occam_attest.md` | Multi-claim rule tally · A · M | `claims-attestation` | `REWRITE` | “semantic support/final honesty gate” overstates two regex shapes; aggregate is unsigned. TRUST C9/#7. | M · P0 · none |
| `docs/tools/occam_dataset_export.md` | URL set + manifest · A/auditor · M | `dataset-provenance`, verification | `REWRITE` | Top-level `ok`, row failures, CLI-only manifest verification and local-key identity need correction. EF-018. | M · P0 · none |
| `docs/tools/occam_batch.md` | Async job trio · A/O · M | `batch-jobs` | `EXPAND` | Add no Receipt v1, indefinite retention, single-writer persistence and privacy ownership. EF-037/038. | S · P1 · none |
| `docs/tools/occam_watch.md` | Stateful change checks · A/O · M | `change-monitoring`, verification | `REWRITE` | No eviction, multi-writer loss, and unsigned histories can be `history_verified`; “signed chain” false under receipts-off. EF-019/020; EFC-P5-05-2. | M · P0 · history claim withheld |
| `docs/tools/occam_crosscheck.md` | Multi-vantage comparison · A/O · S | `consensus-crosscheck` | `REWRITE` | Tool may remain as experimental observation, not consensus/trust feature; verdict unsigned/not re-derived. EF-032; TRUST #9–10. | M · P0 · none |
| `docs/tools/occam_failure_atlas.md` | In-session host failure aggregation · A/O · S | `failure-atlas` | `REWRITE` | “Provably walled/dead ends” overstates one-session telemetry; keep EF-024 withdrawn. | S · P0 · none |

### 2.6 Developer/maintenance pages currently under `docs/`

| Path | Purpose · audience · size | Families | Action | Why / evidence | Effort · priority · blocker |
|---|---|---|---|---|---|
| `docs/developers/contributing.md` | Contributor setup · D · S | operator/build surface | `MOVE_TO_HANDBOOK` | Contributor route belongs under AGENTS-linked handbook, not user task nav. | S · P2 · none |
| `docs/developers/vision.md` | Minimal vision pointer · D/H · S | none stable | `REMOVE` | Seven-line stub adds no durable product contract; point nav to canonical vision/release material. | S · P2 · none |
| `docs/development/README.md` | Private baseline pointer inside public tree · D · S | none | `REMOVE` | Explicitly private/maintenance content; keep only in `docs-internal/`. | S · P1 · none |
| `docs/maintenance/FIXTURE_SOURCES.md` | Fixture attribution/immutability · D · S | testing artifacts | `MOVE_TO_HANDBOOK` | Preserve attribution and contributor policy outside user product nav. | S · P2 · none |
| `docs/maintenance/REPOSITORY_MAP.md` | Public source-tree map · D · S | packaging/runtime implementation | `MOVE_TO_HANDBOOK` | Useful contributor navigation; AGENTS/handbook route, not product capability route. | S · P2 · none |

### 2.7 New v3 pages

| New path | Action | Purpose and family coverage | Effort · priority · dependency |
|---|---|---|---|
| `docs/product/acquisition.md` | `ADD` | PS-1 overview; all nine acquisition families; exact gated ladder and path-specific safety. | L · P0 · canonical acquisition model landed; omit blocked safety guarantees |
| `docs/product/materialization.md` | `ADD` | PS-2 overview; budget, focus, structured, differential, cache. | M · P1 · cache warnings EF-001/045 |
| `docs/product/discovery.md` | `ADD` | PS-3 overview; probe, map, search and provider prerequisites. | M · P1 · none |
| `docs/product/knowledge-extraction.md` | `ADD` | PS-4 live schema extraction; canonical IR only under non-goals/dead implementation. | M · P1 · omit EF-013/043 blocked safety claims |
| `docs/product/playbooks.md` | `ADD` | PS-5 resolve/apply/author/heal/validate, tier and trust boundaries. | L · P0 · omit EF-052/EFC provenance claims |
| `docs/product/trust-provenance.md` | `ADD` | PS-6 exact proof vocabulary, key model, receipts/verify/claims/datasets, forbidden claims. | L · P0 · TRUST-MODEL canonical |
| `docs/product/monitoring-multi-source.md` | `ADD` | PS-7 digest plus experimental batch/watch/crosscheck/atlas with gates and limits. | M · P1 · signed-history wording withheld |
| `docs/product/runtime-exposure.md` | `ADD` | PS-8 transports, profiles, independent gates, instructions, ambient client context. | M · P1 · EF-031/041 warnings |
| `docs/product/operator-surface.md` | `ADD` | PS-9 boundary and route into installer/operator handbook. | S · P1 · packaging blockers explicitly withheld |
| `docs/reference/capability-index.md` | `ADD` | 39-family machine/human bridge: system, exposure class, task/tool/handbook links, live/dead status. | M · P1 · generated from canonical model |
| `docs/reference/profiles-and-gates.md` | `ADD` | `OCCAM_PROFILE`, core subset, independent opt-ins, search/provider gate, producer-without-verifier warning. | M · P1 · EF-031/EFC-P5-05-4 |
| `docs/reference/operator-cli.md` | `ADD` | Wrapper commands vs direct host verbs, exact reachability and exit contracts. | M · P1 · EF-025/035 |
| `docs/handbook/index.md` | `ADD` | Operator/developer/auditor handbook route, clearly separate from task docs. | S · P1 · no `HANDBOOK-OUTLINE.md` existed at audit time |

## 3. Coverage analysis

### 3.1 Product systems → Docs v2

| System | Current status | Evidence in v2 | Required v3 delta |
|---|---|---|---|
| PS-1 Acquisition | **WRONGLY DESCRIBED** | transcode/concepts/how-it-works/config/session pages | Add system page with exact ladder, managed privacy, per-path guards/proxies/sessions, consent automation. EF-003/042/043/056. |
| PS-2 Materialization | **PARTIALLY COVERED** | transcode, tools-reference, semantic contract | Promote budget/focus/structured/diff to one system page; label cache experimental and correct disk/replay semantics. |
| PS-3 Discovery | **COVERED** | probe/map/search tool pages and guide | Add coherent system page and provider/guard limits; retain task guides. |
| PS-4 Knowledge extraction | **WRONGLY DESCRIBED** | extract tool/guide/example and playbooks | Correct receipt/confidence/row/safety semantics; never expose dead canonical IR as a feature. |
| PS-5 Playbooks | **PARTIALLY/WRONGLY COVERED** | resolve/heal/save/lint pages and recipes | Preserve authoring loop; correct signature, lint, sanitizer, marketplace and executable-input trust boundaries. |
| PS-6 Trust and provenance | **WRONGLY DESCRIBED** | extensive trust/receipt/claim pages | Rebase all wording on canonical trust vocabulary and forbidden-claims list. |
| PS-7 Monitoring and multi-source | **PARTIALLY COVERED, LOW DISCOVERABILITY** | digest plus four opt-in tool pages | One system page linking all gates and limits; no consensus/signed-history inflation. |
| PS-8 Runtime and exposure | **PARTIALLY COVERED** | transports/config/tool profiles/client context | Explain exposure as registry + profile + independent gates + instructions + transport/session side effects, not a fixed count. |
| PS-9 Operator surface | **PARTIALLY COVERED, FRAGMENTED** | install/connect/troubleshooting/roadmap | Handbook route for CLI, packaging, connectors, sessions, state, safety, platform differences. |

### 3.2 Model → Docs v2 by family

Absence is a defect only for `PUBLIC_CORE` / `PUBLIC_ADVANCED`; experimental/operator rows are evaluated for honest gated discoverability. `canonical-knowledge-ir` is retained as a dead cluster and excluded from the 38-live count.

| Family | Exposure | v2 status | Principal v2 locations | Delta |
|---|---|---|---|---|
| `acquisition-routing` | PUBLIC_CORE | **WRONG** | concepts/transcode/how/API | Rewrite ladder (EF-056). |
| `http-acquisition` | PUBLIC_CORE | **COVERED** | concepts/transcode/config | Link from PS-1 page; preserve worker/doctor path. |
| `browser-acquisition` | PUBLIC_CORE | **PARTIAL** | concepts/transcode/config | Add pool/context/timeout and safety limits (EF-002/039/040/041/046). |
| `managed-acquisition` | EXPERIMENTAL | **PARTIAL/WRONG** | config/concepts/API | State open-domain default, third-party URL/content path, no public policy value, managed-fail behavior (EF-003/056). |
| `network-safety` | PUBLIC_ADVANCED | **PARTIAL/WRONG** | API/config/trust | Per-path matrix; no global parity (EF-003/042/043). |
| `proxy-egress` | OPERATOR | **PARTIAL/WRONG** | config | Add coverage exceptions and empty-file precedence (CAP-165/166; EF-057). |
| `session-fetch` | PUBLIC_ADVANCED | **PARTIAL/WRONG** | sessions/config/API | Per-tool matrix, isolation/retention/cold-path limits. |
| `access-consent` | PUBLIC_ADVANCED | **ABSENT** | only implicit browser prose | Add silent page mutation and limits; not a feature headline (AUTO #7–9; EF-046). |
| `quality-failure-semantics` | PUBLIC_CORE | **COVERED** | honest-failures/failure-codes/tools | Preserve `ok:false` and thin≠short; correct edge cases. |
| `token-budget` | PUBLIC_CORE | **COVERED** | transcode/client-context/API | Promote ambient handshake and serialized-bound caveat (EF-055). |
| `focus-selection` | PUBLIC_CORE | **COVERED** | transcode/digest/map | Add fragment/cache collision warning (EF-045). |
| `structured-materialization` | PUBLIC_ADVANCED | **COVERED/PARTIAL** | transcode/API | System page; explain always-collected blocks and translation blocking (EF-010/057). |
| `differential-materialization` | PUBLIC_ADVANCED | **COVERED** | transcode/recipes | Preserve conditional/delta workflow; clarify forced blocks. |
| `response-cache` | EXPERIMENTAL | **WRONG** | concepts/config/API/FAQ | Correct disk cache and replay hazards (EF-001/045). |
| `probe-diagnostics` | PUBLIC_CORE | **PARTIAL** | probe/guide | Add SSRF-code masking warning (EF-042). |
| `site-mapping` | PUBLIC_CORE | **COVERED** | map/search guide | Add HTTP/session/proxy scope. |
| `web-search` | PUBLIC_ADVANCED | **COVERED** | search/config/guide | Improve provider requirement and egress disclosure. |
| `digest-synthesis` | PUBLIC_CORE | **COVERED** | digest/research guides | Preserve preferred multi-URL path; qualify receipts/sessions. |
| `schema-knowledge-extraction` | PUBLIC_ADVANCED | **WRONG** | extract/structured docs/API | Correct receipt/confidence/row/error/safety semantics. |
| `canonical-knowledge-ir` | DO_NOT_DOCUMENT | Correctly not a headline, but API implies reusable structured internals in places | API/semantic prose | Mention only as a non-feature/dead internal cluster (EF-004; C8). |
| `playbook-resolution` | PUBLIC_ADVANCED | **PARTIAL/WRONG** | resolve/concepts/API | Correct tier signature and marketplace/well-known limits. |
| `playbook-authoring` | PUBLIC_ADVANCED | **WRONG** | save/recipes/API | Correct unconditional signing, unsigned score, code-like input. |
| `playbook-healing` | PUBLIC_ADVANCED | **COVERED/PARTIAL** | heal/recipes | Preserve host-drafts boundary; add browser/session limits. |
| `playbook-validation` | PUBLIC_ADVANCED | **WRONG** | lint/save | Lint advisory; sanitizer dead; no trusted marketplace claim. |
| `receipts` | PUBLIC_ADVANCED | **WRONG** | README/trust/receipts/API | Rebase on local self-signed integrity vocabulary. |
| `verification` | PUBLIC_ADVANCED | **WRONG** | verify/receipts/API | Enumerate MCP/CLI asymmetries and downgrade paths. |
| `claims-attestation` | PUBLIC_ADVANCED | **WRONG** | claim/attest/guides | Lexical retrieval + narrow unsigned regex tally. |
| `dataset-provenance` | PUBLIC_ADVANCED | **PARTIAL/WRONG** | dataset tool/example/API | Per-row status, CLI-only manifest, identity limits. |
| `batch-jobs` | EXPERIMENTAL | **PARTIAL** | batch/config/transports | Add no-receipt, retention and single-writer warnings. |
| `change-monitoring` | EXPERIMENTAL | **WRONG** | watch/recipes/verify | Add lifecycle/race/unsigned-history limits. |
| `consensus-crosscheck` | DO_NOT_AS_FEATURE | **WRONG** | crosscheck/tool index/choosing/API | Limits-only experimental observation; no proof/consensus headline. |
| `failure-atlas` | EXPERIMENTAL | **WRONG** | atlas/config | In-session telemetry, not proof or durable analytics. |
| `runtime-transports` | PUBLIC_CORE (stdio) | **PARTIAL** | transports/API/config | Preserve stdio; separate WS/Remote/Batch operator depth and EF-041. |
| `mcp-exposure` | PUBLIC_CORE | **PARTIAL/WRONG** | llms/tool index/config/API | Replace fixed count with profile+opt-ins+instructions/runtime `tools/list`. |
| `client-context` | PUBLIC_CORE | **COVERED but buried** | client tool/config | Promote session-start handshake in quick agent route. |
| `operator-cli` | OPERATOR | **PARTIAL/WRONG** | getting-started/transports | Wrapper/direct-host matrix and name-wide refresh warning (EF-025/049). |
| `install-onboarding` | PUBLIC_CORE | **WRONG safety semantics** | README/INSTALL/quick-start | Preserve canonical command; disclose destructive replacement, residue, onboard env. |
| `host-connectors` | OPERATOR | **PARTIAL/WRONG** | connect/mcp-hosts | Preserve support tiers; narrow rollback guarantees. |
| `packaging-distribution` | OPERATOR | **PARTIAL/WRONG** | install/roadmap/index | Honest tarball/npm/Docker/cosign/marketplace matrix; withhold blocked claims. |

Coverage result for the 28 families that require public task/capability depth (`PUBLIC_CORE` + `PUBLIC_ADVANCED`): **13 covered sufficiently in topic existence, 12 partial/wrong, 1 absent (`access-consent`), 2 covered but buried/with critical caveats (`client-context`, `runtime-transports`)**. Topic existence is not gate compliance: no family currently has the explicit multi-path evidence required by `DISCOVERABILITY-GATE.md`.

### 3.3 Docs → model drift

| v2 claim/pattern | Model verdict | Evidence |
|---|---|---|
| “Verifiable/cryptographic provenance,” receipt proves URL/time/page | Materially overstated | TRUST forbidden #1–6, #15, #19 |
| Every call live; no file cache; cache in-memory | False | FLOW-019; ART-035; EF-001/045 |
| Universal HTTP→browser→managed last resort | False/incomplete | EF-056; GAP-001/014 |
| `OCCAM_RECEIPTS=off` disables signing | False | EF-005/044 |
| `occam_attest` is a semantic/final trust gate | Materially overstated | TRUST C9/#7 |
| claim-check `found:false` proves absence | Materially overstated without exact lexical/completeness scope | TRUST C8/#8 |
| crosscheck verdict independently re-derivable / consensus | False as shipped surface | EF-032; TRUST C10 |
| signed watch history / `history_verified` | False for unsigned chains | EFC-P5-05-2 |
| playbook is self-authenticating; score/pass gate signed | False | TRUST X1; EFC-P5-05-1 |
| lint errors exactly match resolve/save rejection | False | EF-015 |
| all fetch tools share SSRF/session/proxy behavior | False | EF-003/007/017/042/043 |
| safe rollback/atomic install guarantees | Overbroad | EF-021/028/029/035 |
| fixed fifteen tools proves a healthy surface | False product model | EF-022/031/033/036 |
| browser default 120 seconds | Stale | canonical routing model default 60s |
| Docker/signed supply chain/community validation implied by artifacts | Withhold | EF-051/052/053 |
| canonical IR/codecs/table semantic model as output capability | Dead/non-documentable | EF-004; CAP-328/330/332/334; C8 |

## 4. Proposed v3 information architecture

### 4.1 Four routes

1. **People:** `docs/index.md` → choose task or product system.
2. **Tool-using agents:** `llms.txt` → task router → one tool/family page → contract/limits.
3. **Installers/operators:** `INSTALL.md` → connect/verify → handbook operations.
4. **Contributors:** `AGENTS.md` → handbook development/architecture/quality; public user docs are not engineering notes.

### 4.2 Target nav tree

```text
Home
├─ What Occam does
├─ Quick start
├─ Choose a task
│  ├─ Read one page
│  ├─ Research several sources
│  ├─ Search and discover
│  ├─ Extract structured data
│  ├─ Verify bytes and citations
│  ├─ Check claims (with limits)
│  └─ Use authenticated sessions
├─ Product systems
│  ├─ PS-1 Acquisition
│  ├─ PS-2 Materialization
│  ├─ PS-3 Discovery
│  ├─ PS-4 Knowledge extraction
│  ├─ PS-5 Playbooks
│  ├─ PS-6 Trust and provenance
│  ├─ PS-7 Monitoring and multi-source
│  ├─ PS-8 Runtime and exposure
│  └─ PS-9 Operator surface
├─ Examples and recipes
├─ Tools
│  ├─ Tool-by-job index
│  ├─ Fifteen always-on tool pages
│  └─ Experimental opt-ins (batch/watch/crosscheck/atlas; gate+limits in title block)
├─ Reference
│  ├─ Capability-family index (39, one dead/non-feature)
│  ├─ MCP API contract
│  ├─ Tools reference
│  ├─ Failure codes
│  ├─ Configuration
│  ├─ Profiles and gates
│  ├─ Transports
│  ├─ Operator CLI reachability
│  └─ Receipt byte specification
├─ Connect
└─ Handbook
   ├─ Install, upgrade, rollback and recovery
   ├─ Host connectors and config safety
   ├─ Sessions and secret handling
   ├─ Runtime operations and state
   ├─ Trust model and audit procedures
   ├─ Packaging/distribution/platform matrix
   ├─ Architecture and semantic contract
   ├─ Quality baseline and fixtures
   └─ Contributing / repository map
```

The handbook is part of the same docs site but a separate audience route. It is not a dumping ground for `docs-audit/`: it contains durable operator/developer procedures only. No `HANDBOOK-OUTLINE.md` existed when this plan was written, so its future outline should be reconciled against the section above before implementation.

### 4.3 v2 page → v3 destination

This mapping is intentionally exhaustive. Rows grouped with `+` retain each source's unique material; no source is silently discarded.

| v2 source(s) | v3 destination |
|---|---|
| `README.md` | root first-contact page linking four routes |
| `INSTALL.md` + `docs/install.md` | canonical `INSTALL.md`; handbook install/upgrade limits |
| `llms.txt` | agent map + generated family links |
| `MCP_API_SPEC.md` + `docs/reference/mcp-api.md` | root normative API + thin site bridge |
| `docs/index.md` | human hub + nine-system map |
| `docs/quick-start.md` | minimum install/connect/read path |
| `docs/getting-started.md` | `guides/read-a-page.md` + handbook CLI/session sections |
| `docs/what-is-occam.md` | product introduction |
| `docs/how-occam-works.md` + `docs/concepts.md` | product introduction + nine system pages; concepts retained only for cross-cutting glossary |
| `docs/choosing-a-tool.md` | task router |
| `docs/faq.md` | FAQ |
| `docs/troubleshooting.md` | operator troubleshooting |
| `docs/configuration.md` | reference/configuration |
| `docs/failure-codes.md` | reference/failure semantics |
| `docs/transports.md` | PS-8 page + reference/transports |
| `docs/mcp-hosts.md` | Connect overview + handbook connector matrix |
| `docs/tools-reference.md` | compact generated tool reference |
| `docs/receipt_verification.md` | normative receipt byte spec |
| `docs/receipts.md` + `docs/trust-and-safety.md` | PS-6 page + receipt task guide + handbook trust chapter |
| `docs/trust/local-first.md` | PS-1/PS-6 boundary + handbook egress matrix |
| `docs/trust/honest-failures.md` | retained task/trust page |
| `docs/trust/installation-safety.md` | handbook install/connect safety |
| `docs/trust/security-policy.md` | retained security policy |
| `docs/roadmap.md` | handbook release/status (or root changelog link) |
| `docs/quality-baseline.md` | handbook quality |
| `docs/architecture/semantic-contract.md` | handbook architecture |
| `docs/ask-ai.md` | agent docs-loading guide |
| all `docs/connect/*.md` | Connect section; safety depth in handbook |
| all `docs/guides/*.md` | retained task guides, corrected in place |
| all `docs/examples/*.md` | retained examples index and examples, corrected in place |
| `docs/recipes.md` | task recipes + advanced/experimental recipes |
| `docs/tools/index.md` | tool-by-job index + system/family links |
| each always-on `docs/tools/occam_*.md` | retained per-tool page linked to one or more family pages |
| `docs/tools/occam_batch.md`, `occam_watch.md`, `occam_crosscheck.md`, `occam_failure_atlas.md` | PS-7 experimental section + retained gate/limits reference pages |
| `docs/developers/contributing.md` | handbook contributing (AGENTS route) |
| `docs/developers/vision.md` | remove stub; canonical vision/release link only |
| `docs/development/README.md` | remove from public tree; private notes remain internal |
| `docs/maintenance/FIXTURE_SOURCES.md` | handbook quality/fixtures |
| `docs/maintenance/REPOSITORY_MAP.md` | handbook repository map |

## 5. The invisibility problem

| Useful capability hidden by v2 | Why it was invisible | v3 mechanism | Gate |
|---|---|---|---|
| Exact acquisition short-circuits and fallback ranking | Simplified cascade prose | PS-1 decision ladder + transcode cross-link + R10 phrase check | DISC R10 |
| Ambient client context budget | One tool page and config tail | Agent-start box in `llms.txt`, quick guide and PS-8 | R1/R4 |
| Access-consent mutation, bypassCSP, virtual scroll | Silent browser behavior | PS-1 “page mutations” limits + handbook trust boundary | PUBLIC_ADVANCED ≥2 paths |
| Session behavior differs by tool | Flat “session-aware” lists | Per-tool matrix in PS-1/reference/handbook | R3 + human review |
| Managed-provider third-party egress | Env-only, success resembles local backend | PS-1 rung card + configuration privacy warning | EXPERIMENTAL co-location |
| Response cache and fragment-sensitive risk | Advanced param amid live-only slogans | PS-2 experimental cache section + tool warning | EXPERIMENTAL ≥2 paths |
| Structured/differential sidecars | Large transcode parameter table | PS-2 capability page linked from read/RAG tasks | PUBLIC_ADVANCED ≥2 |
| Search provider prerequisite | Core tool listed as if ready | PS-3 task flow names provider gate inline | R2-style check |
| Playbook tier/signature/sanitizer distinctions | Four tool pages without system boundary | PS-5 lifecycle and trust-state table | PUBLIC_ADVANCED ≥2 |
| CLI-only manifest verification and direct host verbs | Buried in transports/receipts; wrapper mismatch | Operator CLI reachability matrix + handbook | R8 |
| Profiles can produce receipts without verifier | Profile table lacks workflow consequence | Profiles/gates reference + agent warning | R7 |
| Batch/watch/crosscheck/atlas | Independent env gates, absent from default `tools/list` | PS-7 experimental page with gate+limits beside each name | R2 |
| Auto key mint and save-always-sign | Silent DI/service behavior | PS-6 “automatic side effects” + config warning | R5/R6 |
| Onboard env injection and name-wide refresh kill | Operator scripts outside product docs | Handbook runtime/state chapter | OPERATOR ≥2 paths |
| Packaging asymmetries | “Ships” collapsed into one release story | Handbook platform/distribution matrix | OPERATOR rule |

## 6. What Docs v2 got right

1. **The task-first route is good.** `choosing-a-tool.md`, focused guides, examples and `recipes.md` give agents actionable call order instead of an API dump.
2. **Failure honesty is unusually clear.** The repeated `ok:false = unknown`, “never substitute model memory,” and thin-versus-short distinction should remain prominent.
3. **The default read path is simple.** One required `url`, a minimal transcode example, and digest-over-N-calls guidance are appropriate.
4. **Runtime schema precedence is explicit.** `llms.txt` correctly says `tools/list` is authoritative for availability and input schemas.
5. **Search setup is mostly honest.** The search tool page says it fails closed without a configured provider.
6. **Opt-in tool gates are usually co-located.** Batch/watch/crosscheck/atlas pages name their env gates at the top; v3 must retain that pattern while adding limits.
7. **Host support tiers are candid.** Live-validated, config-validated, assisted and non-MCP runtime categories are a strong operator pattern.
8. **Structured extraction already admits its fake receipt field.** `occam_extract_knowledge.md` correctly says the field is telemetry, not Receipt v1; v3 should preserve and amplify this.
9. **Claim check already separates retrieval from stance.** That distinction is correct; the remaining delta is exact negative/key/completeness language and attest limits.
10. **Contributor/user routes are partly separated.** `AGENTS.md`, exclusions in `mkdocs.yml`, and thin public contributor pages show the right intent; v3 formalizes it as a handbook.
11. **Reference layering exists.** Per-tool pages, compact tools reference, root API contract, and byte-level receipt spec are useful distinct layers once contradictions are removed.
12. **The docs are navigable.** The current MkDocs nav and small focused pages are worth preserving; adding product-system pages should not turn the site into a 674-item catalog.

## 7. Sequencing

1. **Freeze a claim denylist and correction checklist.** Derive it from `TRUST-MODEL.md` §13, EF-056 and P0 table above. No prose migration before this review aid exists.
2. **Land canonical system/family skeletons.** Generate the 39-family index and nine empty system pages with exposure classes and canonical links. Do not copy v2 claims yet.
3. **Correct normative sources first.** Rewrite `MCP_API_SPEC.md`, receipt byte spec, configuration, failure codes, profiles/gates and operator CLI reachability. Task pages must link to these rather than duplicate semantics.
4. **Correct P0 entry routes.** README, INSTALL, quick start, index, llms, what/how/concepts, trust overview. Remove fixed-count, provenance, cache/live and connect-safety contradictions.
5. **Write PS-1 and PS-6 before dependent pages.** Acquisition prose must follow EF-056; trust prose must follow `TRUST-MODEL.md`. These two systems govern most downstream wording.
6. **Write PS-4/PS-5 with blocked claims omitted.** Do not publish CSS/Nuxt safety, authenticated marketplace/foreign-author, or signed quality-score claims before their fixes.
7. **Write PS-2/3/7/8/9.** Make advanced/experimental/operator surfaces discoverable without promoting them as default-core features.
8. **Revise per-tool pages.** Keep parameter tables thin and generated where possible; put workflow semantics in family/system pages and response semantics in the contract.
9. **Revise guides/examples/recipes.** Preserve successful v2 task material, correcting trust/session/cache language and splitting advanced recipes.
10. **Build the handbook route.** Move durable operator/developer pages; delete only the two true stubs/private pointers after links are updated.
11. **Rebuild `mkdocs.yml`, `docs/index.md`, and `llms.txt` links.** Verify every v2 page has a mapped destination.
12. **Enable discoverability checks in stages.** WARN for coverage while migrating, then FAIL for PUBLIC_CORE/ADVANCED, trust denylist and DO_NOT feature headlines as specified in `DISCOVERABILITY-GATE.md`.
13. **Final code-derived audit.** Run doc link/anchor checks, code↔env/schema generation, forbidden-claim scan, and a human review of every P0 row. Do not use v2 prose as the verifier.

## 8. Out of scope for v3

- Raw CAP ledgers, Wave reports, negative-space reports, engineering findings, assignment prompts and this delta plan remain in `docs-audit/`.
- Gate runbooks, private quality reports, architecture work logs and maintainer session prompts remain in `docs-internal/`.
- Dead `canonical-knowledge-ir`, codecs, semantic table materializer, unreachable paywall branch, unused spawner/gate abstractions and Core-dead sanitizer are not public feature pages.
- Engineering bugs are not normalized as features. Public docs may state a bounded limitation or withhold a claim according to `PRODUCT-VS-ENGINEERING.md`.
- No rebrand, API redesign, code fix, new MCP tool, package publication, marketplace promise, Docker-readiness claim, or supply-chain claim is part of this docs plan.
- A 674-item public feature list is explicitly rejected. The public hierarchy stops at systems/families/capabilities and links to reference depth.

## 9. Action counts

The ledger uses one primary action per current page/manifest and one action per proposed new page.

| Action | Count |
|---|---:|
| `KEEP` | 14 |
| `EXPAND` | 15 |
| `SPLIT` | 2 |
| `MERGE` | 1 |
| `REWRITE` | 40 |
| `ADD` | 13 |
| `REMOVE` | 2 |
| `MOVE_TO_HANDBOOK` | 6 |
| `REFERENCE_ONLY` | 1 |
| **Total action rows** | **94** |

Current content inventory: **80 pages** (`docs/` 76 + four root pages) plus `mkdocs.yml` as the nav manifest. Proposed additions: 13 pages.

## 10. Bounded uncertainty

1. `docs/receipt_verification.md` could not be read by the file reader because it contains the literal NUL documented by EF-027; its purpose and required rewrite are corroborated by the nav, links, `MCP_API_SPEC.md`, EF-026/027 and `TRUST-MODEL.md`.
2. No `docs-audit/HANDBOOK-OUTLINE.md` existed at read time. Reconcile rather than overwrite if one appears before execution.
3. The action count treats `mkdocs.yml` as an action row but not as a page, and counts each proposed new page once.
4. Runtime behavior was not re-executed; this plan uses the canonical code-first audit and its explicitly bounded source-proven findings.
