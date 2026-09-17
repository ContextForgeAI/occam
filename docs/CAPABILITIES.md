# Capabilities

Hub: inventory of shipped capabilities and their status.
Individual MCP schemas live in [tools-reference](tools-reference.md).
Domain overview (older map): [capabilities/index.md](capabilities/index.md).

Related hubs: [TRUST](TRUST.md) · [CASCADE](CASCADE.md) · [SEARCH](SEARCH.md) ·
[configuration](configuration.md) · [experimental](experimental.md)

Status vocabulary (from recon / code):

| Status | Meaning |
|--------|---------|
| stable | Default product path |
| opt-in | Shipped; env-gated |
| beta / research | Implemented; not auto-wired into MCP runtime |
| experimental | Advanced / external / not bundled |

Source of truth for this table: codebase paths cited below
(recon snapshot aligned with `OccamMcpServerRegistration`, `Canary/*`, `Exam/*`,
`Cascade/*`, `Search/*`, `Receipts/*`).

## Feature inventory (22)

| # | Feature | Evidence | Status | Env / config |
|---|---------|----------|--------|--------------|
| 1 | Core MCP tools (16) | `Transport/OccamMcpServerRegistration.cs:16–34` | stable | `OCCAM_PROFILE` |
| 2 | Cascade tool `occam` | `Cascade/CascadeService.cs`, `Tools/OccamCascadeTool.cs:15` | stable | — · [ADR-0017](adr/0017-cascade-facade.md) |
| 3 | Transcode / probe / digest / map | `Tools/OccamTranscodeTool.cs:46`, `OccamProbeTool.cs:13`, `OccamDigestTool.cs:18`, `OccamMapTool.cs:12` | stable | `backend_policy`, sessions |
| 4 | Playbook resolve / heal / lint / save | `Playbooks/PlaybookSeedResolver.cs:42`, `PlaybookHealService.cs:9`, `PlaybookLinter.cs:16`, `PlaybookSaveService.cs:11` | stable | `OCCAM_PLAYBOOKS_*`, genome fetch |
| 5 | Extract knowledge | `Tools/OccamExtractKnowledgeTool.cs:15` | stable | playbook `knowledge_schema` |
| 6 | Search (DDG default + fan-out) | `Search/*`, `Services/SearchService.cs` | stable | `OCCAM_SEARCH_*` |
| 7 | Receipts + Merkle + verify | `Receipts/*`, `Tools/OccamVerifyTool.cs:25` | stable | `OCCAM_RECEIPTS`, `OCCAM_KEYS_ROOT` |
| 8 | Claim check / attest / dataset | `Claims/`, `Attest/`, `Dataset/` | stable | receipts on |
| 9 | Batch MCP | `Tools/OccamBatchTools.cs:62–112` | opt-in / experimental | `OCCAM_BATCH_MCP=1` |
| 10 | Watch | `Tools/OccamWatchTool.cs:18` | opt-in / experimental | `OCCAM_WATCH_MCP=1` |
| 11 | Crosscheck consensus | `Tools/OccamCrosscheckTool.cs:20` | opt-in / experimental | `OCCAM_CONSENSUS_MCP=1` |
| 12 | Failure atlas | `Tools/OccamFailureAtlasTool.cs:18` | opt-in / experimental | `OCCAM_ATLAS_MCP=1` |
| 13 | Browser interact | `Tools/OccamBrowserInteractTool.cs:31` | opt-in / experimental | `OCCAM_BROWSER_ACTIONS_MCP=1` |
| 14 | Proxy rotation | `Services/RoundRobinProxyRotationService.cs:3–33` | stable | `OCCAM_PROXY_LIST*` |
| 15 | Browser pool / Playwright | `Workers/Browser*` | stable | `OCCAM_BROWSER_*` |
| 16 | Proof-of-read canary | `Canary/*`, `Tools/OccamCanary*Tool.cs` | stable **MCP + CLI** | `OCCAM_CANARY_*` |
| 17 | Capability exam | `Exam/*` | beta / research (CLI) | recommends → `OCCAM_PROFILE` (manual) |
| 18 | Client capabilities | `Tools/OccamClientCapabilitiesTool.cs:16` | stable | `OCCAM_CLIENT_CONTEXT_TOKENS` |
| 19 | Time anchor TSA | `Receipts/TimeAnchorService.cs` | opt-in | `OCCAM_TIME_ANCHOR` + `OCCAM_TSA_URL` |
| 20 | Translate / PDF OCR | `Services/TranslationService.cs`, `External/ExternalCli.cs` | opt-in / advanced | `OCCAM_TRANSLATE_*`, `OCCAM_PDF_OCR*` |
| 21 | External CLI search | `Search/ExternalCliSearchProvider.cs:10` | experimental | `OCCAM_SEARCH_PROVIDER=external_cli`, `OCCAM_EXTERNAL_SEARCH_PATH` |
| 22 | Streamable HTTP / remote TLS | `Transport/*` | stable | `OCCAM_TLS_*`, JWT vars |

## Explicit gaps

| Gap | Status |
|-----|--------|
| Browser fingerprint rotation | **Not found** — do not document as a feature |
| Canary as MCP tool | **Shipped** — `occam_canary_issue` + `occam_canary_verify` in `OccamToolNames` |
| Exam auto-applies `OCCAM_PROFILE` | **No** — operator sets env manually |
| Proxy / external as cascade stages | **No** — separate from `CascadeStepKind` |

## Profiles (document only these)

| Profile | Tool count | Notes |
|---------|------------|-------|
| `minimal` | 1 | Exam Weak mapping |
| `basic` | 3 | Exam Medium mapping |
| `reader` | 11 | Default |
| `full` | 18 core | Exam Strong mapping |

Source: `Transport/OccamToolProfile.cs`, `Exam/ExamScoring.cs:47–51`,
`Exam/ExamCliVerbs.cs:153–156`.
