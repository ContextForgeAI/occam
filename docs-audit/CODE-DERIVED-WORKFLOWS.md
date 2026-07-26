# CODE-DERIVED-WORKFLOWS (Wave 2)

Inferred **only** from compatible code contracts (schemas, return fields, shared services). Not from public recipes.

---

## FLOW-001 — Read one page with proof

| Field | Value |
|-------|--------|
| TOOLS | `occam_client_capabilities`? → `occam_transcode` → `occam_verify` |
| ARTIFACTS | ART-001, ART-007, optional ART-006 |
| CAPABILITIES | CAP-050…, CAP-090, CAP-250…, CAP-650… |
| EVIDENCE | Transcode builds receipt/capsule; VerifyTool parses receipt/capsule offline |
| LIMITATIONS | Capsule needs `emit_capsule` + receipts on; live verify drops session/playbook |
| CONFIDENCE | PROVEN |

## FLOW-002 — Cheap triage then read

| Field | Value |
|-------|--------|
| TOOLS | `occam_probe` → `occam_transcode` (params from agentHints) |
| ARTIFACTS | ART-012 → ART-001 |
| CAPABILITIES | CAP-420…434, CAP-051… |
| EVIDENCE | ProbeAgentHints recommend backend/json_tables/llms.txt/max_tokens; probe never runs browser/managed |
| LIMITATIONS | Hints advisory; unsupported_content_type on probe ≠ transcode failure |
| CONFIDENCE | PROVEN |

## FLOW-003 — Discover then multi-read

| Field | Value |
|-------|--------|
| TOOLS | `occam_map` and/or `occam_search` → `occam_digest` (or N× transcode) |
| ARTIFACTS | ART-011 / ART-013 → ART-010 |
| CAPABILITIES | CAP-510…, CAP-620…, CAP-450… |
| EVIDENCE | map `suggestedNext=occam_digest`; DigestService reuses MapService for `source_url`; search returns URLs |
| LIMITATIONS | Digest: no playbooks, no transcode sidecars; search needs `OCCAM_SEARCH_PROVIDER` |
| CONFIDENCE | PROVEN |

## FLOW-004 — Search with extractability rerank

| Field | Value |
|-------|--------|
| TOOLS | `occam_search` (`rerank=true`) → uses ProbeService internally → agent → transcode/digest |
| ARTIFACTS | ART-013 + probe scores |
| CAPABILITIES | CAP-627, CAP-425, CAP-435 |
| EVIDENCE | SearchTool calls ProbeService.AnalyzeAsync per hit |
| LIMITATIONS | Up to 20 live HTTP probes; not a cheap re-sort |
| CONFIDENCE | PROVEN |

## FLOW-005 — Playbook authoring loop

| Field | Value |
|-------|--------|
| TOOLS | `occam_playbook_heal` → (agent drafts JSON) → `occam_playbook_lint`? → `occam_playbook_save` → `occam_playbook_resolve` / transcode auto |
| ARTIFACTS | ART-016 → ART-015 → ART-017 |
| CAPABILITIES | CAP-530…, CAP-750…, CAP-560…, CAP-490… |
| EVIDENCE | Heal returns skeleton/candidates; save writes local signed playbook; resolve/transcode read tiers; save clears resolver cache |
| LIMITATIONS | full profile for heal/resolve/save; lint may disagree with save; save signs always |
| CONFIDENCE | PROVEN (human/agent draft step is outside code) |

## FLOW-006 — Schema-driven facts

| Field | Value |
|-------|--------|
| TOOLS | `occam_playbook_resolve` → `occam_extract_knowledge` |
| ARTIFACTS | ART-017 schema → ART-014 facts |
| CAPABILITIES | CAP-590…, CAP-070 |
| EVIDENCE | KnowledgeExtractService resolves playbook schema then css-extract |
| LIMITATIONS | Not TranscodePipeline; fake Receipt; no claim_check wiring to facts[] |
| CONFIDENCE | PROVEN |

## FLOW-007 — Claim grounding + optional attest

| Field | Value |
|-------|--------|
| TOOLS | `occam_claim_check` → `occam_verify`(citation/prove) and/or `occam_attest` |
| ARTIFACTS | ART-019 → ART-021 / ART-020 |
| CAPABILITIES | CAP-690…, CAP-262, CAP-720… |
| EVIDENCE | AttestService delegates to ClaimCheckService; matches carry leaf+proof; Verify citation mode |
| LIMITATIONS | claim_check: no token budget; playbook auto forced; attest classifier understands limited English shapes |
| CONFIDENCE | PROVEN |

## FLOW-008 — Auditable corpus handoff

| Field | Value |
|-------|--------|
| TOOLS | `occam_dataset_export` → CLI `occam verify --mode manifest` (+ optional per-row `occam_verify`) |
| ARTIFACTS | ART-022 |
| CAPABILITIES | CAP-770…, CAP-283 |
| EVIDENCE | DatasetManifestBuilder; CLI verify manifest mode; MCP verify has **no** manifest mode |
| LIMITATIONS | Top-level ok always true; no row token budget; sequential |
| CONFIDENCE | PROVEN |

## FLOW-009 — Authenticated / session read

| Field | Value |
|-------|--------|
| TOOLS | Operator `occam-session` → `session_profile` on transcode/digest/claim/… |
| ARTIFACTS | ART-026 → fetch |
| CAPABILITIES | CAP-068, CAP-167…191 |
| EVIDENCE | FetchPreflight + SessionProfileHeaders; browser storageState on some paths |
| LIMITATIONS | probe/map/heal/extract-fallback often headers-only (export-state inert) |
| CONFIDENCE | PROVEN |

## FLOW-010 — Conditional / delta re-read

| Field | Value |
|-------|--------|
| TOOLS | `occam_transcode` (`if_none_match` / `diff_against` / `delta_only`) ; digest combined ETag-like |
| ARTIFACTS | ART-024, ART-002 |
| CAPABILITIES | CAP-074, CAP-082, CAP-083, CAP-089, CAP-458 |
| EVIDENCE | MaterializationKey / BlockDiff paths in transcode; digest if_none_match on combined |
| LIMITATIONS | diff_against forces blocks[]; digest does not shrink items[] |
| CONFIDENCE | PROVEN |

## FLOW-011 — Watch history verify without watch tool registered

| Field | Value |
|-------|--------|
| TOOLS | Prior watch artifact → `occam_verify` mode=`history` |
| ARTIFACTS | ART-025 → ART-021 |
| CAPABILITIES | CAP-284, verify history mode |
| EVIDENCE | VerifyTool history path; watch tool opt-in separately |
| LIMITATIONS | Creating history requires OCCAM_WATCH_MCP (Wave 3); verifying does not |
| CONFIDENCE | PROVEN |

## Explicitly NOT proven as automatic pipelines

- facts[] → claim_check (no code bridge)
- dataset rows from prior in-memory materialization without re-fetch (always live transcode per URL)
- lint grade guaranteeing save success (parser drift)
- probe session_profile via export-state alone

---

## Wave 3 flows

## FLOW-012 — Batch job lifecycle

| Field | Value |
|-------|--------|
| TOOLS / APIs | `occam_batch_submit` → `status` → `results` **or** `--batch-server` HTTP |
| ARTIFACTS | ART-027 |
| CAPABILITIES | CAP-800…818 |
| LIMITATIONS | No Receipt v1; cross-process store clobber (EF-038) |
| CONFIDENCE | PROVEN |

## FLOW-013 — Watch then history verify

| Field | Value |
|-------|--------|
| TOOLS | `occam_watch` (opt-in) → `occam_verify` mode=`history` |
| ARTIFACTS | ART-028, ART-025 |
| CAPABILITIES | CAP-830…848, CAP-273 |
| LIMITATIONS | No un-watch; multi-process race (EF-019/020) |
| CONFIDENCE | PROVEN |

## FLOW-014 — Crosscheck consensus

| Field | Value |
|-------|--------|
| TOOLS | `occam_crosscheck` (`OCCAM_CONSENSUS_MCP=1`) |
| ARTIFACTS | per-vantage receipts (when receipts on) |
| CAPABILITIES | CAP-850… |
| LIMITATIONS | Profile-exempt; no e2e gate (EF-031/032) |
| CONFIDENCE | PROVEN |

## FLOW-015 — Doctor → verify → onboard → connect

| Field | Value |
|-------|--------|
| TOOLS | `occam doctor` → `verify-install` → `occam-onboard` → `occam connect` |
| ARTIFACTS | ART-029, ART-030, ART-031 |
| CAPABILITIES | CAP-940…959, CAP-960…979, CAP-980… |
| LIMITATIONS | Onboard writes before verify; connect rollback gaps |
| CONFIDENCE | PROVEN |

## FLOW-016 — Level B bootstrap install

| Field | Value |
|-------|--------|
| TOOLS | `get-ff-occam.*` / `install -FromUrl` → doctor `--skip-build` → connect |
| ARTIFACTS | ART-032 |
| CAPABILITIES | CAP-961…965, CAP-1026… |
| LIMITATIONS | Destructive extract; tarball connect/contract entry gap |
| CONFIDENCE | PROVEN |

## FLOW-017 — Session for Tier-1 fetch

| Field | Value |
|-------|--------|
| TOOLS | `occam-session` init/export-state → Tier-1 tool with `session_profile` |
| ARTIFACTS | ART-026 |
| CAPABILITIES | CAP-880…885, CAP-167…176 |
| LIMITATIONS | Tier-2/3 tools silently weaker; pool recycle on every headered call |
| CONFIDENCE | PROVEN |

## FLOW-018 — Public MCP contract check

| Field | Value |
|-------|--------|
| TOOLS | `occam contract` / `version-surface` (Surface B) |
| ARTIFACTS | fingerprint corpus |
| CAPABILITIES | CAP-920, CAP-929 |
| LIMITATIONS | Name collision with host-binary `version-surface` (EF-023); missing from Level B tarball (EF-035) |
| CONFIDENCE | PROVEN |

---

## FLOW-019 — Opt-in transcode cache replay

| Field | Value |
|-------|--------|
| TOOLS | `occam_transcode` with cache eligibility + `cache_ttl_s` / cache dir |
| ARTIFACTS | ART-035 (and replayed ART-001/007/006) |
| CAPABILITIES | CAP-315 family (key completeness still incomplete — EF-045 fragment) |
| LIMITATIONS | Fragment omitted from key; reader-selected TTL; full envelope incl. receipt replayed |
| CONFIDENCE | PROVEN |

## FLOW-020 — Implicit URL-fragment focus

| Field | Value |
|-------|--------|
| TOOLS | `occam_transcode(url#section)` without `focus_query` |
| ARTIFACTS | FocusIntent → section rank → budget |
| CAPABILITIES | Compile/FocusIntent |
| LIMITATIONS | Collides with FLOW-019 identity (EF-045) |
| CONFIDENCE | PROVEN |

## FLOW-021 — Operator refresh / name-wide host kill

| Field | Value |
|-------|--------|
| TOOLS | `occam refresh` → `stop-occam-processes` → relaunch |
| ARTIFACTS | — |
| CAPABILITIES | CAP-928 family |
| LIMITATIONS | Kills by binary name machine-wide (EF-049); not INV-10 pid-scoped |
| CONFIDENCE | PROVEN |

## FLOW-022 — Community playbook marketplace (CI)

| Field | Value |
|-------|--------|
| TOOLS | GH workflow `playbook-marketplace.yml` → community tier resolve |
| ARTIFACTS | community JSON + `.sig` (signing often broken) |
| CAPABILITIES | CAP-1031 |
| LIMITATIONS | Auto-merge on skipped gate (EF-052); cosign misconfigured (EF-053) |
| CONFIDENCE | PROVEN (code path; branch-protection unknown) |

---

**Workflow count:** 22 proven flows (FLOW-001…022). Wave 4 added FLOW-019…022.
