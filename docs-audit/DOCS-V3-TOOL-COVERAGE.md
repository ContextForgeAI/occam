# DOCS-V3-TOOL-COVERAGE (Phase 8D)

**Branch:** `docs/v3-canonical`  
**Date:** 2026-07-26  
**Registry SoT:** `OccamMcpServerRegistration.OccamToolNames` (15 core tools)  
**Cross-check:** `docs/tools/*.md` vs `MCP_API_SPEC.md` / code attributes (spot-checked)

Each tool: purpose, params/defaults honesty, automatic behavior, outputs, failure, trust, workflows, profile exposure, examples, limitations, **issues**.

---

## Summary

| Tool | Page | Cross-check | Issues |
|------|------|-------------|--------|
| `occam_client_capabilities` | `docs/tools/occam_client_capabilities.md` | OK | Minor: env fallback path thin vs configuration.md |
| `occam_transcode` | `docs/tools/occam_transcode.md` | OK | Fixed orphan duplicate paragraph; managed ladder only in acquisition.md |
| `occam_probe` | `docs/tools/occam_probe.md` | OK | SSRF masking as `network_error` not on page |
| `occam_digest` | `docs/tools/occam_digest.md` | OK | Strong; `fit_markdown` default differs from transcode (documented) |
| `occam_playbook_resolve` | `docs/tools/occam_playbook_resolve.md` | OK | Community trust limits good |
| `occam_map` | `docs/tools/occam_map.md` | OK | HTTP-only + headers-only session noted implicitly |
| `occam_playbook_heal` | `docs/tools/occam_playbook_heal.md` | OK | Browser trust / evaluate risk in handbook not tool page |
| `occam_playbook_save` | `docs/tools/occam_playbook_save.md` | OK | Always-sign + v2 clear |
| `occam_extract_knowledge` | `docs/tools/occam_extract_knowledge.md` | OK | Telemetry vs Receipt v1 clear; row-mode / CSS safety not on page |
| `occam_search` | `docs/tools/occam_search.md` | OK | Fail-closed provider gate clear |
| `occam_verify` | `docs/tools/occam_verify.md` | OK | MCP/CLI asymmetry + history verdicts clear |
| `occam_claim_check` | `docs/tools/occam_claim_check.md` | OK | Honesty good; index blurb was misleading (fixed) |
| `occam_attest` | `docs/tools/occam_attest.md` | OK | Unsigned aggregate explicit |
| `occam_playbook_lint` | `docs/tools/occam_playbook_lint.md` | OK | “errors break resolve/save” may overstate parser equivalence |
| `occam_dataset_export` | `docs/tools/occam_dataset_export.md` | OK | CLI-only manifest + row-level ok:false clear |

**Tool pages present:** 15/15 core + 4 opt-in pages (batch, watch, crosscheck, failure_atlas)  
**Open issues after fixes:** 5 minor (see per-tool **Issues** sections)

---

## 1. `occam_client_capabilities`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Session-start context window → ambient output budget (~20% clamped 512–16k) |
| **Params / defaults** | `context_tokens` optional; `clear` resets; 1024–2M clamp documented |
| **Automatic behavior** | Host stores session override; `OCCAM_CLIENT_CONTEXT_TOKENS` env fallback |
| **Outputs** | `configured`, `outputBudgetTokens`, `suggestedProfile`, `source` |
| **Failure** | `invalid_arguments` |
| **Trust** | Budget sizing only — not content truth |
| **Workflows** | Once per session before transcode/digest without `max_tokens`; `llms.txt` routes here |
| **Profile exposure** | All profiles (`reader`+) |
| **Examples** | Configure / inspect / clear JSON on page |
| **Limitations** | Does not persist across host restart unless env set |

**Issues:** Link to `configuration.md#client-context-budget` would reduce duplication.

---

## 2. `occam_transcode`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Default single-URL reader → Markdown + optional sidecars |
| **Params / defaults** | Only `url` required; ~19 opt-in params grouped; `backend_policy=http_then_browser`; `playbook_policy=auto`; `cache_ttl_s` off by default |
| **Automatic behavior** | HTTP→browser ladder; playbook overlay when `auto`; consent dismiss silent in browser; key mint/sign per receipts policy; optional cache replay |
| **Outputs** | `markdown`, `quality`, `receipt`, `recovery[]`, structured sidecars, `agentHints` |
| **Failure** | Full taxonomy; terminal 404/410; thin vs short_quality distinguished |
| **Trust** | `ok:false` = unknown; Receipt v1 = integrity vs key; negative receipts on walls |
| **Workflows** | Probe optional → transcode; verify guide; materialization guide |
| **Profile exposure** | All profiles |
| **Examples** | Minimal nginx call on page |
| **Limitations** | Managed provider not a param — acquisition.md; CAPTCHA not solved |

**Issues (fixed):** Removed stray duplicate “retryable/thin” paragraph (lines 98–99). **Remaining:** EF-056 managed ladder not repeated on tool page (by design — link acquisition.md).

---

## 3. `occam_probe`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Pre-fetch extractability 0–1 + recommended backend |
| **Params / defaults** | `timeout_ms=10000`; `include_social_meta=false`; optional `session_profile` |
| **Automatic behavior** | Shares login decision model with transcode; search rerank reuses scorer |
| **Outputs** | `classification`, `recommendation`, `policy.privacyMode`, `agentHints` |
| **Failure** | Probe-specific + HTTP taxonomy; `ok:false` ≠ “page is bad” |
| **Trust** | Prediction before fetch — not post-extract quality |
| **Workflows** | choosing-a-tool read flow; search-and-discover guide |
| **Profile exposure** | All profiles |
| **Examples** | example.com article |
| **Limitations** | Policy blocks may surface as generic `network_error` (not on page) |

**Issues:** Add one line on SSRF/private URL masking for parity with failure-codes.md.

---

## 4. `occam_digest`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Multi-URL synthesis (≤8) + optional combined markdown |
| **Params / defaults** | `fit_markdown=true` (unlike transcode); `urls` array or `source_url` discovery; `per_url_max_tokens` |
| **Automatic behavior** | Per-URL transcode pipeline; discovery ranker; focus honesty via `focusMatched` |
| **Outputs** | `items[]`, `combined`, `stats`, per-item receipts |
| **Failure** | Digest-level + per-item failures; whole digest can be `ok:true` with failed items |
| **Trust** | Per-item `ok`; `focus_not_found` agent hint |
| **Workflows** | research-multiple guide; map/search → digest |
| **Profile exposure** | All profiles |
| **Examples** | `source_url` and `urls` variants |
| **Limitations** | Global session profile; no cross-item token rebalancing documented |

**Issues:** None blocking.

---

## 5. `occam_playbook_resolve`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Read-only tier resolve + signature inspect |
| **Params / defaults** | `schema_version=1.0`; `include_lessons`, `fetch_site_genome` opt-in |
| **Automatic behavior** | Tier order local → WT_PLAYBOOKS_PATH → community → seeds |
| **Outputs** | Selectors, schema, `signature{status,sigVersion,score,passesGate}` |
| **Failure** | `playbook_not_found` |
| **Trust** | Integrity vs local key; v1 gate fields unsigned; v2 tamper-evident heuristic |
| **Workflows** | structured extraction; playbooks.md |
| **Profile exposure** | **`full` only** (hidden from reader/researcher/auditor) |
| **Examples** | nginx resolve with signature block |
| **Limitations** | Community ≠ trusted publisher |

**Issues:** Profile hiding not stated on tool page — add pointer to choosing-a-tool profiles table.

---

## 6. `occam_map`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Same-domain link discovery (HTTP-only, ≤64) |
| **Params / defaults** | `source=homepage`; `max_links=32`; `focus_query` re-ranks |
| **Automatic behavior** | Sitemap/robots/homepage paths; hub expand on weak focus |
| **Outputs** | `links[]`, `partial`, `expanded`, digest hints |
| **Failure** | `sitemap_not_found`, `thin_extract`, timeout |
| **Trust** | Discovery only — no content extraction |
| **Workflows** | search-and-discover; discover-then-research example |
| **Profile exposure** | All profiles |
| **Examples** | On page |
| **Limitations** | Headers-only session; no browser storageState |

**Issues:** Session tier matrix lives in sessions.md — tool page could link once.

---

## 7. `occam_playbook_heal`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Browser skeleton evidence for recipe drafting (host/agent edits JSON) |
| **Params / defaults** | URL + optional session; browser-dependent |
| **Automatic behavior** | DOM skeleton capture; consent/CSP behaviors silent |
| **Outputs** | Selector candidates, skeleton metadata — host drafts |
| **Failure** | Browser/worker taxonomy |
| **Trust** | Heal output is draft — not signed until save |
| **Workflows** | playbooks authoring loop |
| **Profile exposure** | **`full` only** |
| **Examples** | Linked from playbooks.md |
| **Limitations** | `--consent-aggressive` CLI-only; untrusted page evaluate risk |

**Issues:** Tool page shorter than handbook ch.12 on browser code trust.

---

## 8. `occam_playbook_save`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Write local playbook + unconditional sign (v2) |
| **Params / defaults** | `verify=true` dry-run gate |
| **Automatic behavior** | Signs regardless of `OCCAM_RECEIPTS=off`; v2 gate snapshot in signature |
| **Outputs** | `writtenPath`, `verify{passesGate,score}`, `signedKeyId` |
| **Failure** | `playbook_verify_failed`, schema invalid |
| **Trust** | Local-key integrity; not marketplace authorship |
| **Workflows** | heal → lint → save |
| **Profile exposure** | **`full` only** |
| **Examples** | SPA example JSON |
| **Limitations** | Local save ≠ publish/sanitize community |

**Issues:** None blocking.

---

## 9. `occam_extract_knowledge`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Recipe D typed `facts[]` from `knowledge_schema` |
| **Params / defaults** | `url` + optional `backend_policy`, `session_profile` |
| **Automatic behavior** | Resolve → extract; `playbook_policy=auto` internal |
| **Outputs** | `facts[]`, `meta.koId`, telemetry `receipt` |
| **Failure** | Schema/playbook failure codes + partial facts |
| **Trust** | **`receipt` = telemetry only** — explicitly not Receipt v1 |
| **Workflows** | structured-extraction guide |
| **Profile exposure** | All profiles (including reader) |
| **Examples** | Shop product with telemetry block |
| **Limitations** | Row `base_selector` unreachable; Nuxt/CSS untrusted parity not on page |

**Issues:** Safety boundary for eval/CSS should mirror structured-extraction guide (one sentence).

---

## 10. `occam_search`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Provider web search → URLs |
| **Params / defaults** | `max_results=8`; `rerank=false` |
| **Automatic behavior** | Fail closed without `OCCAM_SEARCH_PROVIDER` |
| **Outputs** | `results[]`, optional extractability when rerank |
| **Failure** | `search_unconfigured`, timeout, HTTP errors |
| **Trust** | Provider egress — not Occam extract quality |
| **Workflows** | search-and-discover; search-then-research example |
| **Profile exposure** | All profiles |
| **Examples** | nginx query + rerank |
| **Limitations** | No shared SSRF guard with extract workers (networking.md) |

**Issues:** None blocking.

---

## 11. `occam_verify`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Receipt v1 / history / citation verification |
| **Params / defaults** | `mode=offline`; MCP defaults to local pubkey |
| **Automatic behavior** | Live mode drops session/playbook replay |
| **Outputs** | `verdict`, mode-specific payloads |
| **Failure** | `invalid_receipt`, `invalid_arguments` |
| **Trust** | Integrity vs supplied key — not truth; history_verified vs chain_ok |
| **Workflows** | verify-sources guide; verify-receipt example |
| **Profile exposure** | `reader`+ (8 tools in reader includes verify) |
| **Examples** | Offline verify JSON |
| **Limitations** | No manifest mode on MCP; extract telemetry rejected |

**Issues:** None blocking.

---

## 12. `occam_claim_check`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | BM25 retrieval + Merkle membership + signed extract receipt |
| **Params / defaults** | `max_matches=3`; `backend_policy` default |
| **Automatic behavior** | Forces playbook auto internally (playbooks.md) |
| **Outputs** | `found`, `proven` (retrieval-complete negative), `matches[]`, `receipt` |
| **Failure** | Fetch failures vs `found:false` |
| **Trust** | `verdict=not_evaluated`; proofs = membership not truth |
| **Workflows** | claims guide; check-a-claim example |
| **Profile exposure** | `researcher`+ |
| **Examples** | nginx load balancing |
| **Limitations** | Lexical floor only |

**Issues (fixed):** `docs/tools/index.md` said “provable source blocks” — corrected to evidence lookup wording.

---

## 13. `occam_attest`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Batch heuristic stance classifier over claims |
| **Params / defaults** | 1–50 claims JSON; shared backend/session |
| **Automatic behavior** | Per-claim retrieve + regex entailment |
| **Outputs** | Counts + `perClaim[]`; aggregate **unsigned** |
| **Failure** | Bad input; per-page → `unknown` rows |
| **Trust** | Not cryptographic attestation; gate on `status` |
| **Workflows** | claims guide; choosing-a-tool verifiable report |
| **Profile exposure** | `auditor`+ |
| **Examples** | Rust/nginx mixed claims |
| **Limitations** | Narrow regex shapes only |

**Issues:** None blocking.

---

## 14. `occam_playbook_lint`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | Static schema validation — no network |
| **Params / defaults** | `playbook_json` string |
| **Automatic behavior** | Deterministic grade `ready/usable/broken` |
| **Outputs** | `issues[]` severities |
| **Failure** | Malformed input reported as lint errors (no `ok:false`) |
| **Trust** | Advisory — save may still reject on live verify |
| **Workflows** | Before save in authoring loop |
| **Profile exposure** | `auditor`+ |
| **Examples** | Broken missing schema_version |
| **Limitations** | Core community sanitizer dead — not documented on page |

**Issues:** “errors break resolve/save” could note lint ≠ save parser in edge cases.

---

## 15. `occam_dataset_export`

| Aspect | Coverage |
|--------|----------|
| **Purpose** | 1–20 URL signed export + manifest |
| **Params / defaults** | JSON `urls` array; shared backend/session |
| **Automatic behavior** | Per-row transcode + manifest Merkle sign |
| **Outputs** | `manifest`, `rows[]` with optional receipts |
| **Failure** | Bad input; row failures inside successful export |
| **Trust** | Export integrity — not row truth; manifest verify CLI-only |
| **Workflows** | dataset-export example; datasets.md |
| **Profile exposure** | `auditor`+ |
| **Examples** | Two nginx URLs |
| **Limitations** | MCP has no manifest verify mode |

**Issues:** None blocking.

---

## Opt-in tools (not in 15 core — cross-reference only)

| Tool(s) | Page | In 8D scope |
|---------|------|-------------|
| `occam_batch_*` | `docs/tools/occam_batch.md` | Referenced |
| `occam_watch` | `docs/tools/occam_watch.md` | Referenced |
| `occam_crosscheck` | `docs/tools/occam_crosscheck.md` | Referenced |
| `occam_failure_atlas` | `docs/tools/occam_failure_atlas.md` | Referenced |

See `DOCS-V3-NONMCP-COVERAGE.md` for CLI/transports/batch-server.

---

## Aggregate tool issues list (actionable)

1. **probe:** Document SSRF/private URL masking behavior (1 line).
2. **playbook_resolve / heal / save:** Add profile exposure note (`full` only for authoring trio).
3. **extract_knowledge:** One-line CSS/Nuxt untrusted safety pointer to structured-extraction guide.
4. **playbook_lint:** Clarify lint vs save parser equivalence limit.
5. **tools/index.md claim_check blurb** — **fixed** this session.

---

## Index / router cross-check

| Asset | Status |
|-------|--------|
| `docs/tools/index.md` | Job table complete; opt-in section; claim_check wording fixed |
| `docs/choosing-a-tool.md` | Decision table matches 15+opt-in; profile table fixed |
| `docs/tools-reference.md` | Compact reference exists (not re-audited line-by-line in 8D) |
| `mkdocs.yml` | All 15 core + 4 opt-in tool pages in Reference nav |
