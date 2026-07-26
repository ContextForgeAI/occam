# DOCUMENTATION-EXPOSURE-MATRIX (Phase 5O)

**Agent:** P5-09  
**SoT:** 39 family slugs from `canonical-capabilities.json`; entry/trust/dead ledgers.  
**Docs frozen/untrusted** — this matrix is the **target** exposure model for a future handbook, not a claim about current `docs/`.  
**Date:** 2026-07-26  
**Phase 6.5 reconcile:** consistent with `OWNER-DECISIONS.md` + `HONESTY-SCHEMA-MAP.md`. npm stays non-public (OD-3); cosign not an install guarantee (OD-2); marketplace-trust excluded pending EA-052 (OD-1); extract `receipt`=telemetry (OD-5), `claim_check.proven`/`attest`/crosscheck framed per OD-6/7/8.

---

## 0. Legend

| Class | Meaning |
|-------|---------|
| `PUBLIC_CORE` | Default path: every agent/operator must discover without hunting env flags. |
| `PUBLIC_ADVANCED` | Shipped & reachable; documented with depth, not buried. Advanced ≠ hidden. |
| `REFERENCE_ONLY` | Spec/reference depth; no task-guide lead. |
| `OPERATOR` | Humans installing/wiring/operating hosts — not agent task guides. |
| `DEVELOPER` | Alternate transports, packaging internals, host binary flags. |
| `EXPERIMENTAL` | Env-gated / opt-in; document as opt-in with limits. |
| `INTERNAL` | Mechanism useful to maintainers; not a user feature page. |
| `DO_NOT_DOCUMENT_AS_FEATURE` | Dead, buggy-as-promise, or name-overclaims — may appear only as limits/warnings. |

`DISCOVERABILITY_PRIORITY`: **HIGH** / **MEDIUM** / **LOW** — how hard future docs must work to surface the family (orthogonal to class: an EXPERIMENTAL family can still be HIGH if operators must find the gate).

`public_relevance` in `canonical-capabilities.json` is nearly all **HIGH** — this matrix **discriminates**. Column **ΔPR** = disagreement with peer `public_relevance`.

---

## 1. Matrix (all 39 families)

| Family slug | Exposure class | Disc. priority | ΔPR | One-line justification |
|-------------|---------------|----------------|-----|------------------------|
| `acquisition-routing` | PUBLIC_CORE | HIGH | — | Default `http_then_browser` behavior every agent hits; must document **real** ladder (EF-056), not mythic cascade. |
| `http-acquisition` | PUBLIC_CORE | HIGH | — | Primary cheap path; worker/doctor dependency is day-one. |
| `browser-acquisition` | PUBLIC_CORE | HIGH | — | Escalation + SPA path; Playwright provision is core operator/agent knowledge. |
| `managed-acquisition` | EXPERIMENTAL | MEDIUM | **YES** (was HIGH) | Env-gated third-party providers; never a public `backend_policy` value; EF-003 egress. |
| `network-safety` | PUBLIC_ADVANCED | HIGH | **YES** | SSRF/private-URL blocks are user-visible failures; depth is advanced, not “core tutorial.” |
| `proxy-egress` | OPERATOR | MEDIUM | **YES** | Operator env/proxy lists; rotation incomplete for daemons (CAP-165). |
| `session-fetch` | PUBLIC_ADVANCED | HIGH | — | Login walls; needs operator session CLI + agent param; EF-039/040/054 caveats. |
| `access-consent` | PUBLIC_ADVANCED | MEDIUM | **YES** | Automatic consent dismiss is silent (AUTO #8); document as behavior+limit, not a feature brand. |
| `token-budget` | PUBLIC_CORE | HIGH | — | K2 contract; ambient client budget is the default agent path. |
| `focus-selection` | PUBLIC_CORE | HIGH | — | Fragment + `focus_query` / fit_markdown are common agent needs. |
| `structured-materialization` | PUBLIC_ADVANCED | HIGH | **YES** | Opt-in sidecars (`json_*`); core agent path works without them. |
| `differential-materialization` | PUBLIC_ADVANCED | MEDIUM | **YES** | `diff_against` / `if_none_match` — valuable but secondary. |
| `response-cache` | EXPERIMENTAL | LOW | **YES** | Opt-in `cache_ttl_s`; EF-001/045 make “cache as feature” unsafe to promote. |
| `quality-failure-semantics` | PUBLIC_CORE | HIGH | — | Trust rule `ok:false` / thin vs short_quality is the product’s honesty spine. |
| `probe-diagnostics` | PUBLIC_CORE | HIGH | — | Pre-fetch diagnosis tool is always-on core. |
| `site-mapping` | PUBLIC_CORE | HIGH | — | Always-on discovery tool. |
| `web-search` | PUBLIC_ADVANCED | HIGH | **YES** | Tool is core-registered but **fails closed** without provider env — advanced setup. |
| `digest-synthesis` | PUBLIC_CORE | HIGH | — | Multi-URL default recommendation vs N× transcode. |
| `schema-knowledge-extraction` | PUBLIC_ADVANCED | HIGH | — | Recipe D; requires playbook schema; not day-one read path. |
| `canonical-knowledge-ir` | DO_NOT_DOCUMENT_AS_FEATURE | LOW | **YES** (was MEDIUM) | Built then discarded / dead codecs (CAP-330/333/328/332); ships in binary (C8) but not a product surface. |
| `playbook-resolution` | PUBLIC_ADVANCED | HIGH | — | Soft auto overlay on transcode; resolve tool for authors. |
| `playbook-authoring` | PUBLIC_ADVANCED | HIGH | — | Save always signs (EF-005) — document with trust limits. |
| `playbook-healing` | PUBLIC_ADVANCED | HIGH | — | Heal-learn loop; browser-dependent. |
| `playbook-validation` | PUBLIC_ADVANCED | MEDIUM | **YES** | Lint advisory; sanitizer Core-dead (EF-047) — do not document sanitizer as live. |
| `receipts` | PUBLIC_ADVANCED | HIGH | — | Real integrity layer; must include **forbidden claims** (`TRUST-MODEL` §13). |
| `verification` | PUBLIC_ADVANCED | HIGH | — | Pair with receipts; MCP key default vs CLI `--pubkey` honesty. |
| `claims-attestation` | PUBLIC_ADVANCED | MEDIUM | **YES** | Tools ship; **name overstates** — document as BM25+regex, not crypto attestation (forbidden #7–8). |
| `dataset-provenance` | PUBLIC_ADVANCED | MEDIUM | **YES** | Useful auditor export; manifest verify CLI-only (EF-018) — advanced/ops adjacent. |
| `batch-jobs` | EXPERIMENTAL | MEDIUM | **YES** | `OCCAM_BATCH_MCP` / BatchServer; no Receipt v1 (EF-037). |
| `change-monitoring` | EXPERIMENTAL | MEDIUM | **YES** | `OCCAM_WATCH_MCP`; store races / no Remove (EF-019/020). |
| `consensus-crosscheck` | DO_NOT_DOCUMENT_AS_FEATURE | LOW | **YES** | Opt-in tool may be mentioned as **experimental observation**, never as “consensus proof” (forbidden #9–10; EF-031/032). Class = do-not-as-feature for the *claim*; env gate may appear under Experimental opt-ins. |
| `failure-atlas` | EXPERIMENTAL | LOW | **YES** | Session telemetry sink; not durable analytics product. |
| `runtime-transports` | PUBLIC_CORE | HIGH | **YES** (split) | **stdio** = PUBLIC_CORE; WS/Remote/BatchServer = DEVELOPER/OPERATOR — peer HIGH flattens this. Primary class for docs lead: PUBLIC_CORE (stdio). |
| `mcp-exposure` | PUBLIC_CORE | HIGH | — | Tool list, profiles, server instructions — the exposure contract. |
| `client-context` | PUBLIC_CORE | HIGH | — | Session-start budget handshake. |
| `operator-cli` | OPERATOR | HIGH | **YES** | Not agent PUBLIC_CORE; operators need HIGH discoverability. |
| `install-onboarding` | PUBLIC_CORE | HIGH | — | Without install, no product; also OPERATOR handbook depth. |
| `host-connectors` | OPERATOR | HIGH | **YES** | Connect adapters are operator integration, not agent tools. |
| `packaging-distribution` | OPERATOR | MEDIUM | **YES** | Release/tarball/Docker/npm — operator/maintainer; EF-051…053 limits. |

### Counts by exposure class

| Class | Count |
|-------|------:|
| PUBLIC_CORE | 13 |
| PUBLIC_ADVANCED | 15 |
| EXPERIMENTAL | 5 |
| OPERATOR | 4 |
| DO_NOT_DOCUMENT_AS_FEATURE | 2 |
| REFERENCE_ONLY | 0 |
| DEVELOPER | 0* |
| INTERNAL | 0* |
| **Total families** | **39** |

\*WS/Remote depth is folded into `runtime-transports` (split note) and `packaging-distribution` / `operator-cli` rather than separate DEVELOPER rows — avoids double-counting the 39 slugs. Internal dead mechanisms are listed in §2 without inventing extra family slugs.

### ΔPR disagreement count

**20 / 39** families marked **YES** in ΔPR (disagree with peer `public_relevance`, almost always HIGH→not-public-HIGH or HIGH→lower priority/class):  
`managed-acquisition`, `network-safety`, `proxy-egress`, `access-consent`, `structured-materialization`, `differential-materialization`, `response-cache`, `web-search`, `canonical-knowledge-ir`, `playbook-validation`, `claims-attestation`, `dataset-provenance`, `batch-jobs`, `change-monitoring`, `consensus-crosscheck`, `failure-atlas`, `runtime-transports`, `operator-cli`, `host-connectors`, `packaging-distribution`.

---

## 2. DO_NOT_DOCUMENT_AS_FEATURE — explicit list

| Item | Kind | Reason | Evidence |
|------|------|--------|----------|
| `canonical-knowledge-ir` family (as product IR / alternate codecs) | Dead shipped | IR built then discarded; codecs never selectable | CAP-330/333/328/332; DEAD-OR-UNREACHABLE; C8 |
| `MaterializedProvenanceResolver` / `ProvenanceTrace` | Dead shipped | Zero callers | CAP-286/331 |
| `ResponseBudgetMode.Unchanged` / `DeltaOnly` | Dead shipped | Unit-only | CAP-324 |
| `TableSemanticMaterializer` as live tables path | Dead shipped | Bench/test-only | CAP-334 |
| `"paywall"` negative-receipt code as supported wall | Unreachable | No post-processor emits it | CAP-264/279; EF-008 |
| Proxy rotation as covering daemons/CSS/skeleton | Overclaim / incomplete | Does not reach those spawns | CAP-165 |
| Core C# `HttpClient` honors `OCCAM_HTTP_PROXY` | False | Never honors | CAP-166 |
| Automatic network retry/backoff | Absent | None | CAP-188 |
| `occam_extract_knowledge` field `Receipt` as Receipt v1 | Name overclaim | Telemetry `{confidence, elapsedMs}` | CAP-287; EF-006 |
| `OCCAM_RECEIPTS` as master signing switch | Overclaim | Save always signs; key always minted | C6; EF-005/044; TRUST §13 #13 |
| Capsules as “signed bundles” | Overclaim | Wrapper unsigned | TRUST §13 #11 |
| `occam_attest` as cryptographic attestation | Overclaim | Unsigned regex tally | TRUST §13 #7 |
| `occam_claim_check` proves claim absent from page | Overclaim | Lexical floor only | TRUST §13 #8 |
| Crosscheck / “consensus” as genuineness or multi-node proof | Overclaim | Same process/egress; verdict unsigned | TRUST §13 #9–10; EF-032 |
| Cosign-verified install / signed supply chain | Overclaim | Bundle unused by install | EF-053; TRUST §13 #17 |
| Signed playbook quality score | Overclaim | `verify{}` outside signature | TRUST X1; §13 #12 |
| `history_verified` ⇒ signed history | Overclaim | Unsigned chain can pass | EFC-P5-05-2; TRUST §13 #14 |
| `PlaybookCommunitySanitizer` as live Core path | Dead | Core-dead | EF-047; C3 |
| Heal `--consent-aggressive` via MCP | Unreachable | CLI/worker only | CAP-553 |
| `base_selector` row-mode in host extract_knowledge | Dead earlier | Host never sets | CAP-600; C4 |
| Marketplace / community playbooks as authenticated authors | Overclaim | sha256 integrity ≠ identity; auto-merge risk | EF-052; G-E-03 |
| Docker image as health-checked production unit | Bug-derived | HEALTHCHECK broken | EF-051 |

**Note on `consensus-crosscheck`:** the **tool existence** may appear under Experimental opt-ins with honest limits; documenting it as a **trust/consensus feature** is forbidden.

---

## 3. What the previous documentation hid

Capabilities that **exist, matter, and were effectively invisible** (env gates, advanced params, opt-in tools, silent automation, CLI-only, operator-only). Phase 5 exists to stop this from recurring.

| Hidden surface | Why it mattered | How it was hidden | Evidence |
|----------------|-----------------|-------------------|----------|
| Real acquisition ladder (404/410 stop; public-ref skip; managed never wins on fail) | Agents escalate wrongly / docs teach false cascade | Prose claimed density/managed last-rung | EF-056; C1 |
| `OCCAM_RECEIPTS` incompleteness | Operators think signing is off | Described as master switch | C6; EF-005/044 |
| Auto key mint on every host start | Disk secret appears without request | Silent DI | AUTO #1; EF-044 |
| Playbook save always signs | Trust policy surprise | Env ignored | EF-005 |
| Opt-in MCP: batch / watch / crosscheck / atlas | Entire PS-7 invisible in default tools/list | Four env flags; not profile-filtered | `ENTRYPOINT` §5; CAP-011 |
| Server instructions mentioning watch without gate | Agents hunt missing tools | GAP-012 | GAP-012 |
| `OCCAM_PROFILE` subsets | Produce-without-verify (`reader`) | Env only | EFC-P5-05-4 |
| `OCCAM_SEARCH_PROVIDER` | Search tool appears then fails closed | Env | `ENTRYPOINT` §3 |
| Managed providers (`OCCAM_MANAGED_PROVIDER`) | Third-party sees URL; host may sign their bytes | Env; success looks like normal backend | EF-003 |
| Session profiles + `_imports/` cookie retain | Login path; plaintext secret risk | Operator CLI only | ART-026/037; EF-054 |
| Host offline `keys` / `verify` / `install-browser` | Only forced-pubkey verify path | Unreachable via `occam` wrapper | EF-025 |
| WS / Remote / BatchServer | Multi-session + HTTP batch | Flags; launcher hardcodes `[]` | CAP-1001 |
| `cache_ttl_s` response cache | Full envelope replay; fragment collisions | Advanced param default off | EF-001/045; FLOW-019 |
| Ambient `occam_client_capabilities` | Changes default max_tokens + cache identity | One-shot tool easy to skip | ART-023 |
| Capsule / Merkle / time-anchor | Real integrity options | Advanced params + dual env for TSA | TRUST C5–C6, C9 |
| Dataset manifest verify | Auditor export integrity | **CLI-only** | EF-018 |
| Onboard env merge into every launch | Silent config | Operator file | EF-050 |
| `occam refresh` name-wide process kill | Collateral damage | Operator verb | EF-049 |
| Consent dismiss / bypassCSP / virtual scroll | Page mutation before hash | Silent browser automation | AUTO #7–9; EF-046 |
| Failure atlas / crosscheck | Ops diagnostics | Opt-in + no server-instructions for crosscheck | CAP-861; EF-031 |
| Level B tarball vs npm/cosign story | What actually installs | Packaging docs vs EF-053 | ART-032/038 |

---

## 4. Calibration rules (for future writers)

1. **HIGH public_relevance ≠ PUBLIC_CORE.** Prefer: does a default agent need it on first successful read?
2. **Advanced must still be linked** from task guides (“see Structured outputs”) — never only buried in env catalogs.
3. **Experimental** always states the env gate and the honesty limits in the same paragraph as the tool name.
4. **DO_NOT_DOCUMENT_AS_FEATURE** items may appear only under “Limits / Non-goals / Known gaps,” never as capability headlines.
5. Peer `canonical-capabilities.json` `public_relevance` remains evidence of *inventory importance*, not documentation class — **this matrix overrides it for handbook planning.**

---

## 5. Uncertainty

| Item | Status |
|------|--------|
| Whether `claims-attestation` should split into two exposure rows (claim_check PUBLIC_ADVANCED vs attest REFERENCE_ONLY) | UNCERTAIN — kept one family slug; document both with distinct claim strength |
| Whether `network-safety` belongs in PUBLIC_CORE because private_url_blocked appears early | Rejected — failure code is core; mechanism depth is advanced |
