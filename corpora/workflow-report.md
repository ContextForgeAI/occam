# Prompt 5 — Workflow Regression Deliverable

**Date:** 2026-07-21  
**Gates:** `L10_WORKFLOW_FROZEN_OK` · `L11_WORKFLOW_SECURITY_OK` · `L12_WORKFLOW_LIVE_OK` (`--workflow-live`)

## 1. Workflow test matrix

Canonical checklist: [`corpora/workflow-matrix.jsonl`](../corpora/workflow-matrix.jsonl)

| Chain | Tools | Frozen coverage |
|-------|-------|-----------------|
| A | client_capabilities → transcode | ambient budget, hash, matKey, receipt, economy |
| B | probe → transcode | 6 fixtures (clean/JS/PDF/feed/login/challenge), stop handoff |
| C | map → digest | entity-first rank, focusMatched, focus_not_found |
| D | search → digest → attest | rerank scorer, 5-status counts, retrieval≠support |
| E | claim_check → verify | BM25 floor, Merkle proof, absence/complete leaves |
| F | transcode ×3 | unchanged envelope, delta reconstruct, option drift |
| G | transcode → verify(chunks) | trust/salience annotate, chunk staleness |
| H | resolve/extract/heal/lint/save | terminal≠heal, lint broken, OverlayApplied |
| I | transcode(capsule) → verify | offline, tamper, wrong key, prove |
| J | dataset_export → manifest | success+fail rows, edit/reorder/drop |

Stop rules + observability JSONL report under `artifacts/workflow-reports/` (gitignored).

## 2. Fixtures / corpora

- Matrix: `corpora/workflow-matrix.jsonl`
- Reuses: discovery-focus pools (inline), golden HTML names via probe signal fixtures (synthetic `ProbeAnalysis`), playbook lint JSON strings, capsule/dataset builders
- Report artifact: `artifacts/workflow-reports/L10-*.jsonl`

## 3. Gate names

| Marker | Blocking | Command |
|--------|----------|---------|
| `L10_WORKFLOW_FROZEN_OK` | yes | `dotnet run --project benchmarks/l0-gate -- --unit-only` |
| `L11_WORKFLOW_SECURITY_OK` | yes | same |
| `L12_WORKFLOW_LIVE_OK` | no (pre-release) | `--workflow-live` |
| `L12_WORKFLOW_LIVE_SKIPPED` | — | no published host binary |

## 4. Before / after failures

| Before (Prompt 5 stub) | After |
|------------------------|-------|
| Only A/B/F/I skeleton | Full A–J stage-prefixed asserts |
| Fake `WouldLoop` | Production `TranscodeAgentDecisions` + `PlaybookHealPolicy` |
| L11 placeholder `!(false&&true)` | Real leafSetComplete / capsule / dataset / attest / stop |
| No L12 | `--workflow-live` tunnel path + optional node contract |

## 5. Live tunnel report

Run:

```powershell
$env:OCCAM_HOME = (Get-Location).Path
dotnet run --project benchmarks\l0-gate -- --workflow-live
```

Expected when AOT/publish host exists:

- `tools/list` schema (digest `source_url`, transcode RC1 params)
- best-effort conditional size smoke (`if_none_match`)
- best-effort discovery focus (`occam_map` + asyncio)
- `node scripts/check-public-mcp-contract.mjs` → `PUBLIC_MCP_CONTRACT_OK`
- marker `L12_WORKFLOW_LIVE_OK`

When host missing: `L12_WORKFLOW_LIVE_SKIPPED` (non-blocking).

## 6. Limitations

| Item | Class |
|------|--------|
| L12 network smokes soft-skip on drift | non-blocking |
| Search chain uses scorer stub, not live `ISearchProvider` | non-blocking (deterministic gate) |
| Heal/save live browser path stays in L3 | non-blocking |
| Capsule live emit needs `OCCAM_RECEIPTS` on host | non-blocking (frozen I covers codec) |
| Full multi-URL live rotation corpus | non-blocking / future |
