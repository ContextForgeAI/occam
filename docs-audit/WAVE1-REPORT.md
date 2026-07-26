# WAVE 1 REPORT

**WAVE:** 1  
**STATUS:** COMPLETE (with orchestrator takeover of failed S0)

## SUBAGENTS COMPLETED

| Agent | Status | Report |
|-------|--------|--------|
| S0 Runtime/MCP | FAILED then orchestrator rewrite | `docs-audit/subsystems/runtime-mcp.md` |
| S1 occam_transcode | OK — [S1 Transcode](b6f8bbf9-6e4d-4085-8f7b-a667b0ee609f) | `docs-audit/tools/occam_transcode.md` |
| S17 Network | OK — [S17 Network](daf71e5d-17c2-457b-9283-cb6f8359175e) | `docs-audit/subsystems/network-fetch-proxy.md` |
| S18 Browser | OK — [S18 Browser](a155b317-d4a3-49e9-bf86-fafd7da8f27c) | `docs-audit/subsystems/browser-workers.md` |
| S19 Trust | OK — [S19 Trust](90b679c3-2958-422b-977f-d48b405e0626) | `docs-audit/subsystems/trust-receipts.md` |
| S20 Materialization | OK — [S20 Materialization](24ddd439-df63-4567-a77d-4c9f35e8c375) | `docs-audit/subsystems/materialization.md` |
| S24 Config/env | OK — [S24 Config](6d35b192-e805-4c08-bb40-b02d6092a0fa) | `ENVIRONMENT-VARIABLES.md` + `config-env.md` |

Failed original S0: [S0 Runtime](f066cb9e-c780-4be6-a4e7-e5e6a4433b1c) — `resource_exhausted` mid-Write; no partial file.

## FILES CREATED/UPDATED

- `docs-audit/WAVE1-ASSIGNMENT.md`
- `docs-audit/subsystems/runtime-mcp.md` (orchestrator)
- `docs-audit/tools/occam_transcode.md`
- `docs-audit/subsystems/network-fetch-proxy.md`
- `docs-audit/subsystems/browser-workers.md`
- `docs-audit/subsystems/trust-receipts.md`
- `docs-audit/subsystems/materialization.md`
- `docs-audit/subsystems/config-env.md`
- `docs-audit/ENVIRONMENT-VARIABLES.md`
- `docs-audit/CAPABILITY-INVENTORY.md`
- `docs-audit/capabilities.json`
- `docs-audit/DEAD-OR-UNREACHABLE.md`
- `docs-audit/WAVE1-REPORT.md` (this file)

## CAPABILITIES BEFORE / AFTER

- **Before:** 0
- **After (unique CAP IDs indexed):** see `CAPABILITY-INVENTORY.md` / `capabilities.json` (~300+ extracted headers; ranges partitioned so no cross-agent ID collisions)

## NEW CAPABILITIES (highlights — not exhaustive)

See inventory for full list. Highest-signal Wave 1 IDs:

- CAP-008/009/011/025 — profile + opt-in surface taxonomy
- CAP-054–058 / CAP-238–244 — managed providers (Firecrawl/Jina/Spider/Scrapfly)
- CAP-162–164 / CAP-234 / CAP-381 — **proxy rotation**
- CAP-167–176 / CAP-191 — sessions/cookies across tools
- CAP-074–089 — transcode advanced params (diff, cache, capsule, trust tags, llms.txt, translate, screenshot)
- CAP-250–291 — receipts/Merkle/capsules/verify CLI
- CAP-315 — cache-key bug
- CAP-330 — Canonical layer computed-then-discarded
- CAP-350–395 — env/config capabilities; 74 env vars catalogued

## MOST IMPORTANT HIDDEN CAPABILITIES

1. **Proxy rotation pool** (round-robin; forces one-shot daemons) — CAP-162/164/234
2. **Managed third-party scrape fallback** (env-gated, last resort in `http_then_browser` only) — CAP-054/238–244
3. **`OCCAM_PROFILE` shrinks tools/list**; opt-ins still add tools — CAP-008/011/025
4. **Always-on internal blocks/tables collection**; flags only gate serialization — CAP-078
5. **`diff_against` forces `blocks[]` even when `json_blocks=false`** — CAP-083
6. **Transparent PDF / feed / plain-text dispatch** inside HTTP worker — CAP-059/080/111
7. **Browser pool cookie bleed across hosts** until recycle — CAP-249
8. **Managed HTTP client bypasses local SSRF guard** — S18 finding
9. **Canonical Knowledge pipeline runs then discarded** — CAP-330
10. **`OCCAM_RECEIPTS` inconsistent** (Consensus reimplements; playbook_save signs unconditionally) — CAP-280

## CONTRADICTIONS FOUND

| Topic | Tension | Resolution for inventory |
|-------|---------|--------------------------|
| Tool count “15” | Profile + opt-ins change `tools/list` | CAP-025 taxonomy; do not claim fixed 15 alone |
| Managed “ships?” | Code+DI yes; default off; not in default agent instructions as a tool | Status: **advanced / env-gated escalation**, not a separate MCP tool |
| Proxy env | Node workers honor proxy; Core C# HttpClients do not (CAP-166) | Document asymmetry |
| Receipts kill-switch | Policy vs call sites vs playbook_save | CAP-280 — needs Wave 2/3 verify on playbook_save |
| S0 vs S18 managed SSRF | S18 claims managed client skips OutboundHttpGuard | Keep as UNCERTAIN until orchestrator spot-check in Wave 3 packaging |
| CAP count S18 | Envelope said 50; header extract ~45 (+ lettered IDs 206b/248a/b) | Treat report body as SoT |

## UNCERTAIN / NEEDS ORCHESTRATOR REVIEW

1. BatchServer HTTP API vs `OCCAM_BATCH_MCP` tools (dual surfaces)
2. Profile gate test coverage (L2 tests assert full catalog only)
3. Managed provider SSRF bypass — confirm `occam.managed` HttpClient construction
4. Whether `OCCAM_RECEIPTS=off` truly ignored by `occam_playbook_save`
5. Browser PDF after HTTP PDF failure (S1 unresolved)
6. Search/managed request construction (S17 scoped out)

## NEXT WAVE RECOMMENDATION

**Do not start until you approve.** Proposed Wave 2: one fresh subagent per remaining core tool:

`occam_probe`, `occam_digest`, `occam_map`, `occam_search`, `occam_client_capabilities`, `occam_playbook_resolve`, `occam_playbook_heal`, `occam_playbook_save`, `occam_playbook_lint`, `occam_extract_knowledge`, `occam_verify`, `occam_claim_check`, `occam_attest`, `occam_dataset_export`

(Note: `occam_verify` partially covered by S19 — still needs dedicated tool-file depth per Wave 2 rules.)

Allocate new CAP ranges starting **CAP-400+**.

No public doc rewrite yet.
