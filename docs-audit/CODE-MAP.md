# CODE-MAP — executable components (Phase 1)

**Generated:** 2026-07-26  
**Branch:** `docs/site-overhaul` @ `23d645329fb10b4509d4cab711f9dd3ef53fc938`  
**Rule:** map territory only — no behavior claims yet. Source of truth = code paths below.  
**Docs status:** existing `docs/`, `README.md`, `llms.txt`, `MCP_API_SPEC.md` are **UNTRUSTED** until gap matrix (Phase 8).

---

## 1. Runtime host (shipped MCP)

| Field | Value |
|-------|-------|
| Path | `src/FFOccamMcp.Core/` |
| Purpose | Native AOT .NET 10 MCP host + offline CLI verbs |
| Entrypoint | `Program.cs` → `OccamCliVerbs.TryRun` **or** transport start |
| Shipped | Yes (primary product binary) |
| User-visible | Yes (MCP + CLI) |
| Main callers | Cursor/hosts via stdio/WS/remote; `scripts/launch-mcp-host.mjs`; npm `packages/occam-mcp` |
| Main deps | ModelContextProtocol, DI (`AddOccamCore`), Node workers via `WorkerPaths` |

### 1.1 Layer directories (host)

| Path | Purpose (map only) | User-visible? |
|------|--------------------|---------------|
| `Tools/` | MCP tool handlers (`[McpServerTool]`) | Direct |
| `Transport/` | Registration, profiles, stdio/WS/remote, binding guard | Direct (transport) / indirect (profile) |
| `Routing/` | Transcode pipeline / router / options / compiler | Indirect |
| `Backends/` | HTTP, browser, managed providers (Firecrawl/Jina/Spider/Scrapfly) | Indirect (params/env) |
| `Workers/` | Node process spawn, HTTP daemon, browser daemon host | Indirect |
| `PostProcessors/` | Challenge / login / thin / quality | Indirect |
| `Probe/` | Cheap URL diagnosis | Via `occam_probe` |
| `Digest/` | Multi-URL digest contracts | Via `occam_digest` |
| `Services/` | DigestService, MapService, rankers | Indirect |
| `Compile/` | Token budget, fit/focus, projection | Via tool params |
| `Knowledge/` | Materialization planner / surfaces / tables | Indirect |
| `Codecs/` | Knowledge codecs (md/json/compact) | Indirect |
| `Playbooks/` | Resolve/heal/save/lint/genome/community | Via playbook tools |
| `Receipts/` | Sign / verify / Merkle / capsule / time anchor | Via verify/receipt fields |
| `Claims/` / `Attest/` | Claim check + attest classifier | Via tools |
| `Dataset/` | Dataset export + manifest | Via `occam_dataset_export` |
| `Session/` | Session profiles, fetch headers, preflight | Via params + CLI session |
| `Caching/` | Transcode response cache eligibility/key/store | Indirect (env/params) |
| `Watch/` | Stateful page watch | Opt-in tool |
| `Consensus/` | Cross-check vantage points | Opt-in tool |
| `Batch/` | Async batch jobs (+ optional BatchServerHost) | Opt-in tools / CLI mode |
| `Search/` | SearXNG / Brave / Tavily providers | Via `occam_search` + env |
| `Access/` | Access evidence / classifier | Indirect |
| `Agent/` | Agent hints | Indirect (responses) |
| `Client/` | Client capabilities / budget ambient | Via `occam_client_capabilities` |
| `Configuration/` | `OccamEnvironment` helpers | Indirect |
| `Cli/` | Offline `keys` / `verify` / `install-browser` / `lifecycle` / `version-surface` | Direct CLI |
| `Lifecycle/` | Host identity / lifecycle verb | Direct CLI |
| `Telemetry/` | Failure atlas sink/store | Opt-in tool |
| `Semantics/` | Outcome mapping | Indirect |
| `Extract/` / `Text/` / `Json/` / `Portable/` / `Abstractions/` | Shared primitives | Internal |
| `Composition/` | `AddOccamCore` DI wiring | Internal |

---

## 2. MCP tool registration (code, not docs)

**Canonical catalog:** `Transport/OccamMcpServerRegistration.OccamToolNames` (15 core names).

**Registration:** `AddOccamMcpServer` → `WithTools<…>` gated by `OccamToolProfile` + opt-in env flags.

### 2.1 Always-on core (subject to `OCCAM_PROFILE`)

| Tool | Handler type | Path |
|------|--------------|------|
| `occam_client_capabilities` | `OccamClientCapabilitiesTool` | `Tools/OccamClientCapabilitiesTool.cs` |
| `occam_transcode` | `OccamTranscodeTool` | `Tools/OccamTranscodeTool.cs` |
| `occam_probe` | `OccamProbeTool` | `Tools/OccamProbeTool.cs` |
| `occam_digest` | `OccamDigestTool` (custom schema union for `urls`) | `Tools/OccamDigestTool.cs` |
| `occam_playbook_resolve` | `OccamPlaybookResolveTool` | `Tools/OccamPlaybookResolveTool.cs` |
| `occam_map` | `OccamMapTool` | `Tools/OccamMapTool.cs` |
| `occam_playbook_heal` | `OccamPlaybookHealTool` | `Tools/OccamPlaybookHealTool.cs` |
| `occam_playbook_save` | `OccamPlaybookSaveTool` | `Tools/OccamPlaybookSaveTool.cs` |
| `occam_extract_knowledge` | `OccamExtractKnowledgeTool` | `Tools/OccamExtractKnowledgeTool.cs` |
| `occam_search` | `OccamSearchTool` | `Tools/OccamSearchTool.cs` |
| `occam_verify` | `OccamVerifyTool` | `Tools/OccamVerifyTool.cs` |
| `occam_claim_check` | `OccamClaimCheckTool` | `Tools/OccamClaimCheckTool.cs` |
| `occam_attest` | `OccamAttestTool` | `Tools/OccamAttestTool.cs` |
| `occam_playbook_lint` | `OccamPlaybookLintTool` | `Tools/OccamPlaybookLintTool.cs` |
| `occam_dataset_export` | `OccamDatasetExportTool` | `Tools/OccamDatasetExportTool.cs` |

### 2.2 Opt-in MCP tools (env-gated in registration)

| Tool(s) | Gate | Handler |
|---------|------|---------|
| `occam_batch_submit` / `status` / `results` | `OCCAM_BATCH_MCP=1` | `OccamBatchTools.cs` |
| `occam_watch` | `OCCAM_WATCH_MCP=1` | `OccamWatchTool.cs` |
| `occam_crosscheck` | `OCCAM_CONSENSUS_MCP=1` | `OccamCrosscheckTool.cs` |
| `occam_failure_atlas` | `OCCAM_ATLAS_MCP=1` | `OccamFailureAtlasTool.cs` |

### 2.3 Profile narrowing (`OCCAM_PROFILE`)

| Profile | Exposed subset (code) |
|---------|----------------------|
| `full` (default) | All 15 `OccamToolNames` (+ opt-ins if env on) |
| `reader` | capabilities, transcode, probe, digest, map, search, extract_knowledge |
| `researcher` | reader + claim_check, verify |
| `auditor` | researcher + attest, dataset_export, playbook_lint |

**Note for Phase 2:** playbook heal/save/resolve are **not** in reader/researcher/auditor — only in `full` (unless registration logic differs; verify in Phase 3).

---

## 3. Host CLI verbs (binary, no MCP)

| Verb | Path | Purpose (map) | Shipped |
|------|------|---------------|---------|
| `keys export` | `Cli/OccamCliVerbs.cs` | Public key export | Yes |
| `verify` | same | Offline receipt/citation/manifest/watch-history verify | Yes |
| `install-browser` | same | Playwright chromium provision | Yes |
| `version-surface` | same | Version surface dump | Yes |
| `lifecycle` | same + `Lifecycle/` | Host lifecycle | Yes |

Transport modes (CLI parse → `Program.cs`): `stdio` (default), `WebSocket`, `Remote`, `BatchServer`.

---

## 4. Node workers

| Path | Entrypoint | Purpose | Shipped | User-visible |
|------|------------|---------|---------|--------------|
| `workers/http-extract/` | `extract.mjs` | HTTP HTML→markdown extract | Yes | Indirect |
| `workers/browser-extract/` | `browser-extract.mjs`, `browser-daemon.mjs` | Playwright extract + daemon pool | Yes | Indirect |
| `workers/css-extract/` | `css-extract.mjs` | CSS/schema extract | Yes | Indirect (knowledge/playbook) |
| `workers/shared/lib/` | (library) | Egress proxy, cookies, feeds, PDF, chunks, private-IP, headers, plugins | Yes | Indirect |

---

## 5. Operator / install / connect (scripts)

Registry: `scripts/lib/operator/occam-command-registry.mjs`.

| ID / path | Purpose | User-visible |
|-----------|---------|--------------|
| `scripts/occam.mjs` (+ `.ps1`, `occam`) | Operator CLI dispatcher | Yes |
| `scripts/install.ps1` / `install.sh` | Install | Yes |
| `scripts/occam-doctor.ps1` / `.sh` | Doctor | Yes |
| `scripts/occam-onboard.mjs` | Onboarding | Yes |
| `scripts/occam-connect.mjs` + `scripts/lib/operator/connect/` | Host connect plan/apply/verify/adapters | Yes |
| `scripts/launch-mcp-host.mjs` | MCP host launcher | Yes |
| `scripts/occam-session.mjs` + `lib/occam-sessions-lib.mjs` | Session profile export/management | Yes |
| `scripts/occam-playbook-publish.*` | Playbook publish CLI | Advanced |
| `scripts/get-ff-occam.*` | Bootstrap install | Yes |
| `scripts/build-release.*` | Release tarball | Maintainer |
| `scripts/check-docs.mjs` / `check-docs-brand.mjs` | Doc lint | Maintainer |
| Gates: `run-l0-fast.ps1`, `benchmarks/l0-gate`, agent-mvp, etc. | Verification | Maintainer / CI |

Connect adapters (map): `scripts/lib/operator/connect/adapters/` — cursor, claude-code, codex, gemini, windsurf, junie, roo, goose, opencode, zed, cline, …

---

## 6. Packages

| Path | Purpose | Shipped |
|------|---------|---------|
| `packages/occam-mcp` | npm MCP wrapper / npx entry | Yes (publishable) |
| `packages/occam-agent-sdk` | Agent SDK | Yes |
| `packages/occam-skill` | Skill install package | Yes |

---

## 7. Profiles / seeds / corpora

| Path | Purpose | User-visible |
|------|---------|--------------|
| `profiles/playbooks/seeds/` | Built-in playbook seeds | Indirect |
| `profiles/playbooks/community/` | Community playbooks + manifest | Indirect |
| `corpora/` | Gate / smoke / quality fixtures | Maintainer (some recipes public) |

---

## 8. Gates / tests / CI

| Path | Purpose |
|------|---------|
| `benchmarks/l0-gate/` | Primary integration gate (L0–L9 levels) |
| `benchmarks/l0-ram-stress/` | Maintainer RAM stress |
| `benchmarks/rc2-regression/` | RC2 regression |
| `workers/**/*.selftest.mjs` | Worker unit selftests |
| `scripts/lib/**/*.selftest.mjs` | Script selftests (connect, env-catalog, …) |
| `packages/occam-mcp/test/` | Package tests |
| `validation/` | Validation harness assets |
| `.github/workflows/` | ci, docs, nightly-gate, occam-release, sign-release, agent-mvp-gate, eval-harness, playbook-marketplace |

---

## 9. Docs / site (UNTRUSTED for product truth)

| Path | Role in this audit |
|------|--------------------|
| `docs/` | Phase 8 gap target only |
| `site/` | MkDocs/site build artifacts — not SoT |
| `MCP_API_SPEC.md`, `llms.txt`, `README.md` | Compare after inventory |
| `docs-internal/` | gitignored engineering — may hint, never trust alone |
| `docs-audit/` | **This audit workspace** (code-derived) |

---

## 10. Ancillary / investigate later

| Path | Notes |
|------|-------|
| `cursor-proxy/` | Separate proxy tree — determine if shipped with Occam product |
| `tools/` | Large tree — classify shipped vs local tooling |
| `skills/` | Skill assets |
| `docker-compose.yml`, `Dockerfile` | Packaging surface |
| `graphify-out/` | Local knowledge graph — not product runtime |

---

## 11. Phase 1 completeness

| Question | Status |
|----------|--------|
| Top-level executable surfaces listed? | Yes (host, workers, scripts, packages, CI) |
| MCP registration source identified? | Yes — `OccamMcpServerRegistration.cs` |
| Opt-in tools identified? | Yes — batch/watch/consensus/atlas |
| Behavior documented? | **No** (deferred to Phases 2–5) |
| Dead/unreachable classified? | **No** (Phase → `DEAD-OR-UNREACHABLE.md`) |

**Phase 1 verdict:** territory mapped at directory/entrypoint level. Ready for Phase 2 capability inventory via subsystem subagents.
)
