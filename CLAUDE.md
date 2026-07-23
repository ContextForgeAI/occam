# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

### Build, publish, and run

```powershell
# Doctor — npm install + playwright chromium + dotnet publish
.\scripts\occam-doctor.ps1

# Fast smoke gate (~30s, mdn + nginx + 404 HTTP-only)
.\scripts\run-l0-fast.ps1

# Full integration gate (L0–L7: smoke + live tests)
dotnet run --project benchmarks\l0-gate

# Fast gate with unit tests
.\scripts\run-l0-fast.ps1 -WithUnit

# Ad-hoc transcode (single URL)
dotnet run --project benchmarks\l0-gate -- --url=https://example.com

# Ad-hoc with visual artifact output
dotnet run --project benchmarks\l0-gate -- --url=https://example.com --visual

# AOT publish for current RID
dotnet publish src\FFOccamMcp.Core -c Release -r win-x64

# Launch MCP host (prefers AOT binary, falls back to dotnet run)
node scripts\launch-mcp-host.mjs

# Visual matrix (HTML report comparing golden cases)
.\scripts\run-visual-matrix.ps1 -Open

# Batch smoke (Level B install verification)
.\scripts\batch-smoke.ps1

# Build release tarball (replace X.Y.Z with current version)
.\scripts\build-release.ps1 -Version X.Y.Z
```

### Single test or gate tier

The gate system has no test framework — it's a console app. Tests are organized into independent runners invoked from `benchmarks/l0-gate/Program.cs`. Key gate flags:

```
--smoke-only     # Skip unit tests, run only live smoke
--fast           # 3-URL HTTP-only subset
--visual         # Write HTML artifacts to ./artifacts/l0-runs/<timestamp>/
--visual-matrix  # Full visual matrix report
--open           # Open HTML report in browser
--url=X          # Ad-hoc single URL (bypasses all tests)
--backend=X      # http | browser | http_then_browser (default)
```

### macOS / Linux variants

```bash
# Same semantics, .sh extension
export OCCAM_HOME="$(pwd)"
./scripts/occam-doctor.sh
./scripts/run-l0-fast.ps1   # works if pwsh installed
```

## Task discipline

Follow this sequence on every task — do not skip steps:

1. **UNDERSTAND** — Read AGENTS.md + relevant `docs/*.md` for the area you touch.
2. **SCOPE** — Confirm change is in L0; no drive-by refactors.
3. **IMPLEMENT** — Minimal correct diff in code/workers/scripts.
4. **VERIFY** — Run the appropriate gate tier when behavior changed.
5. **SYNC DOCS** — Update every affected doc per the matrix below (same session).
6. **CLEAN UP** — Remove orphan files, stale comments, broken doc links.
7. **REPORT** — Tell the user: code changed, docs updated, gate run + result.

**Definition of done:** code works, docs match code, tree is clean, no stale files, no broken links in `docs/`.

### Scope-first sizing (do not over- or under-process)

Match the ceremony to the task before touching anything:

- **Trivial** (1 action, obvious fix — rename, typo, one-line tweak): skip the pipeline, do it directly. Running UNDERSTAND→VERIFY on a typo is wasted budget.
- **Medium** (one feature, 2–4 non-obvious decisions): the 7-step sequence, but keep clarifying questions to ≤ 3.
- **Large** (several parts/subsystems): full sequence + an explicit written plan first.
- **When unsure between two sizes, pick the larger.** Only drop a level on an explicit "this is small, skip the process."

### Spec-gate before fan-out (token-budget guard)

**Never spawn parallel sub-agents (Task / Agent Teams) before an approved plan or spec exists.** Parallel agents without a shared spec each guess independently, diverge, and the guesses still have to reconcile — that is how a session burns the daily limit without producing a landed change.

- Sub-agents spend the **same shared token budget** as the main chat. They buy speed on genuinely independent work, not on exploration.
- Approval = an **explicit "yes"** to a direct "plan ok, shall I start?" — silence, "looks fine", or "do what's best" is not approval; re-ask.
- If the user asks to parallelize but no spec exists yet, decline and offer to write the spec first.
- Split work for sub-agents **only along truly independent seams in the spec** — do not fan out "just in case."
- This complements the standing rule to not spawn agents unless asked; here the bar is even higher when the work is still undefined.

### Orchestrator + workers (delegation flow — owner-approved 2026-06-27)

The expensive model (Opus) runs as an **orchestrator**: it holds the plan, judgment, decisions, and the live context, and delegates the grunt work to cheaper worker sub-agents (native `Agent` tool with a `model` override) so the orchestrator's context stays lean and cost drops. Goal: **both** a clean orchestrator context AND lower limit spend.

- **Roles:** orchestrator = Opus (holds plan/judgment/context, never does bulk reading or log-parsing itself). Workers = **Sonnet** for scouting/research that needs reasoning, **Haiku** for pure mechanics (run a command, grep, report a marker).
- **When to delegate:** a task that is (a) **mechanical** (run the gate/sweep, apply a precisely-specified fix) or (b) **scoped scouting** ("read X → answer question Y"). Trivial one-liners the orchestrator does itself — spawning has overhead. Final judgment/design stays with the orchestrator.
- **Worker input = a self-contained mini-spec:** exact task + files/commands + the **required output format**. Never open-ended ("look around X").
- **Worker output = conclusion + verifiable evidence only:** the gate marker + exit code, an artifact path, numbers each **with their source file**. NOT raw logs. "Not measured" when it wasn't (anti-overclaim §5c in `docs-internal/HANDOFF-2026-06-25.md`).
- **Verification is on the artifact, not the prose:** the orchestrator re-checks the returned marker/number/artifact itself (confirm `L0_GATE_OK` is present, re-read the cited value) before trusting a worker's summary.
- **Context stays isolated:** the worker's file reads / log parsing live in its own context; only the compressed result returns to the orchestrator.
- Spec-gate above still applies: no parallel fan-out before an approved plan; workers get only truly-independent seams.

## Language preference

- Always communicate with the user in Russian. All explanations, reasoning, thoughts, and final reports MUST be written in Russian.
- Keep technical terms, file paths, command outputs, and code blocks in English as per the source material, but wrap them in Russian prose.

## Architecture

### High-level picture

FF-Occam MCP is a **Native AOT .NET 10 host** that presents 14 MCP tools. Each tool delegates to **Node.js workers** for actual HTML extraction. The host handles routing, post-processing, token budgeting, playbook merge, and session management.

```
                   ┌──────────────────────────────┐
                   │    MCP Client (Cursor, etc.)   │
                   └──────────┬───────────────────┘
                              │ JSON-RPC (stdio / WS)
                   ┌──────────▼───────────────────┐
                   │    StdioMcpTransport          │
                   │    or WebSocketMcpTransport   │
                   └──────────┬───────────────────┘
                              │
              ┌───────────────┼───────────────────┐
              │   OccamRouter │ TranscodePipeline  │
              │   ProbeService│ DigestService      │
              │   MapService  │ Playbook services  │
              └───────┬───────┴────────┬───────────┘
                      │                │
            ┌─────────▼──┐    ┌────────▼────────┐
            │ http-extract│    │ browser-extract  │
            │ (Node.js +  │    │ (Playwright)     │
            │  domino +   │    │                  │
            │  turndown)  │    │ dom-skeleton     │
            └─────────────┘    └─────────────────┘
```

### Core assembly (`src/FFOccamMcp.Core/`)

C# namespace: `OccamMcp.Core.*` (the directory name differs from the namespace — do not use `FFOccamMcp` in new code).

| Layer | Key files | Responsibility |
|-------|-----------|---------------|
| **Entry** | `Program.cs` | CLI parse → transport start |
| **Transport** | `Transport/IMcpTransport.cs`, `StdioMcpTransport.cs`, `WebSocketMcpTransport.cs` | JSON-RPC framing (stdio or WebSocket) |
| **Tools** | `Tools/OccamTranscodeTool.cs`, `OccamProbeTool.cs`, `OccamDigestTool.cs`, `OccamMapTool.cs`, `OccamPlaybookResolveTool.cs`, `OccamPlaybookHealTool.cs`, `OccamPlaybookSaveTool.cs`, `OccamExtractKnowledgeTool.cs` | MCP tool handlers — validate params, call services, serialize response |
| **Routing** | `Routing/TranscodePipeline.cs`, `OccamRouter.cs`, `Backends/` `HttpExtractBackend.cs`, `BrowserExtractBackend.cs` | Backend policy dispatch (`http` / `browser` / `http_then_browser`), escalation |
| **Post-process** | `PostProcessors/` `ChallengePagePostProcessor.cs`, `RequiresLoginPostProcessor.cs`, `ThinExtractPostProcessor.cs` | Validate extract quality, detect failures |
| **Workers** | `Workers/HttpExtractRunner.cs`, `BrowserExtractRunner.cs`, `BrowserDaemonHost.cs`, `NodeWorkerProcessSpawner.cs` | Spawn/drive Node.js processes, manage browser pool |
| **Services** | `Services/ProbeService.cs`, `DigestService.cs`, `MapService.cs` | Orchestration for probe, digest, map tools |
| **Playbooks** | `Playbooks/PlaybookHealPolicy.cs`, `PlaybookSaveService.cs`, `PlaybookGenomeMerger.cs`, `WellKnownGenomeFetcher.cs` | Heal/save loop, genome resolution, community seed fetch |
| **Compile** | `Compile/FitMarkdown.cs`, `FocusMatcher.cs`, `TokenBudget.cs` | Token-aware markdown pruning |
| **DI** | `Composition/OccamServiceCollectionExtensions.cs` | Singleton wiring — `AddOccamCore()` |
| **Batch** | `Batch/BatchServerHost.cs`, `SqliteBatchJobStore.cs` | Experimental batch HTTP server |

### Workers (`workers/`)

npm workspace with four packages:

- **`http-extract/`** — HTTP-only extraction via `@mixmark-io/domino` + `@mozilla/readability` + `turndown`. No browser, ~35s timeout.
- **`browser-extract/`** — Playwright Chromium full browser extract, ~120s timeout. Separate `browser-daemon.mjs` for persistent pool. `dom-skeleton-capture.mjs` for playbook heal analysis.
- **`css-extract/`** — CSS extraction worker.
- **`shared/lib/`** — Egress proxy, consent management, cookie injection, plugin runner.

Worker env contract: `docs/configuration.md`.

### Packages (`packages/`)

- **`@ff-occam/mcp`** — npm wrapper (zero-config `npx @ff-occam/mcp` entry point).
- **`@ff-occam/agent-sdk`** — Agent SDK package.

### Gate system (`benchmarks/l0-gate/`)

The gate is a console application organized as "levels" — each level is a static class with a `Run(…)` method:

| Level | Tests | Fast flag |
|-------|-------|-----------|
| L0 | Smoke corpus (l0-smoke.jsonl) | `--fast` = 3 URLs |
| L1a | Token economy (max_tokens, fit_markdown, focus_query) | Unit-only |
| L1b | Probe classifier, redirects, SPA detection | Unit + live |
| L1 | Failure taxonomy (every failure code) | Unit-only |
| L2 | Digest, map, session profiles, transport, egress, media refs | Unit + live |
| L3 | Heal-learn (playbook heal → save → verify) | Full |
| L4 | Genome resolution (playbook_policy=auto, genome merge) | Full |
| L5 | Batch server | Full |
| L6 | Browser pool lifecycle | Full |
| L7 | Resource safety (concurrent extract, cleanup) | Full |
| L8 | Agent-First enhancements (AF-1..AF-6: confidence, receipt, auto-recovery, differential) | Unit + live |
| L9 | Golden set (frozen-HTML extraction-fidelity regression, deterministic) | Full |

L0 gate architecture fact: the L0 gate runner bench `benchmarks/l0-gate` provides the only assertion/reporting mechanism and programmatically references the Core project with `OccamGateBuild=true` (which activates a special `OCCAM_GATE` conditional compilation symbol defined in the csproj).

### Key patterns

- **No file cache** — every call does live extraction.
- **Source-generated JSON** — all JSON serialization uses `System.Text.Json` source generators (`JsonSerializerContext`) for AOT compatibility.
- **Stderr for diagnostics, stdout only for MCP JSON** — banners, logs, CLI help all go to stderr. stdout is pure JSON-RPC.
- **`ok: false` means unknown content** — the trust model: never infer page content from model memory on failure.
- **Singleton DI** — almost all services are singletons (transient scope not used).
- **WorkerPath discovery** — resolves from `OCCAM_HOME` env var, then walks up from `AppContext.BaseDirectory`, then checks CWD.

### Important environment variables

| Variable | Purpose |
|----------|---------|
| `OCCAM_HOME` | Root directory for worker scripts |
| `OCCAM_BROWSER_CHANNEL` | Playwright channel (chrome, msedge, chromium) |
| `OCCAM_SESSIONS_ROOT` | Session profile JSON directory |
| `OCCAM_HTTP_PROXY` / `OCCAM_HTTPS_PROXY` | Egress proxy |
| `OCCAM_FORCE_DOTNET_RUN` | Bypass AOT binary, use `dotnet run` |
| `OCCAM_LOG` | Log level for MCP stderr logger |
| `OCCAM_BANNER` | Banner suppression |

### Documentation discipline

#### Rules (NON-NEGOTIABLE)

1. **CODE FIRST** — Every API change MUST update `MCP_API_SPEC.md` BEFORE commit.
2. **NO DOC DUPLICATION** — Single source of truth per topic: env vars → `docs/configuration.md`, receipts CLI → `docs/receipts.md`, tools → `docs/tools-reference.md`, transport → `docs/transports.md`.
3. **SINGLE SOURCE** — Tool reference is auto-generated from `[Description]` attributes in `src/FFOccamMcp.Core/Tools/*.cs`. Edit code, not docs, when parameters change.
4. **GENERATE DON'T GUESS** — Never write docs from memory. Read the source code. If a parameter exists in code but not in docs, that's a bug.
5. **LEGACY CHECK** — Before adding new doc, grep existing docs. Check for outdated references (old namespace `FFOccamMCP`, old tool names `web_*`).
6. **REVIEW GATE** — If you changed a tool signature, you MUST update `docs/tools-reference.md` and `MCP_API_SPEC.md`.
7. **TESTING DOCS** — Testing methodology lives in `docs/testing_methodology.md`. The key principle: **self-use testing** (Cursor uses Occam, compares with raw data) is the primary quality gate. Synthetic unit tests are necessary but not sufficient.

#### Doc change matrix

| You changed… | Update these |
|--------------|-------------|
| `OccamTranscodeTool` / `OccamDigestTool` params | `MCP_API_SPEC.md`, `docs/tools-reference.md` |
| Failure codes / post-processors | `MCP_API_SPEC.md`, `docs/failure-codes.md`, `docs/troubleshooting.md` |
| `backend_policy` / router logic | `docs/concepts.md`, `docs/tools-reference.md` |
| Timeouts in backends | `docs/concepts.md`, `MCP_API_SPEC.md` |
| Env vars | `docs/configuration.md`, `MCP_API_SPEC.md` |
| Install / doctor / scripts | `docs/getting-started.md`, `docs/troubleshooting.md`, `README.md` |
| New user-visible capability | `docs/choosing-a-tool.md`, `MCP_API_SPEC.md`, `CHANGELOG.md` |
| Release-worthy milestone | `CHANGELOG.md` |

#### What NOT to document

- Closed plans, analysis notes, session prompts, release artifacts.
- Engineering-only content stays in local `docs-internal/` when present (gitignored; never commit; never link from user docs).
- **`docs/` is user-facing.** Prefer durable public pages under `docs/`; do not dump private diaries into the hub.

### Publishable hygiene (pre-public, pseudonym-safe)

The tree is published **pseudonymously** (see memory `identity-pseudonym-first`). Everything committed is a potential public + de-anon surface:

- **No private infrastructure or personal identity as a shipped default.** Never hardcode a LAN/RFC1918 IP, an internal hostname, a private-forge URL, or a personal name/email anywhere a user or the wrapper reads it. Distribution URLs must be **public + reachable**, or an env override (`OCCAM_RELEASE_BASE_URL`, `OCCAM_REPO_URL`) with a **neutral public default**. Any such leak is a **pre-public scrub blocker**.
- **A publishable npm package must not import outside its `files` set.** `packages/occam-mcp` ships only `bin/` + `lib/`; a top-level `import` from `../../scripts/lib/…` will not be in the tarball → the published package is DOA. Vendor shared helpers into `lib/`, or drop the npm `publishConfig`/`bin` if the package is not actually published.

### Related repositories

- `c:\PROJECTS\FFWebMCP` — predecessor reference (read-only, do not grow L0 there).

## graphify

This project has a knowledge graph at graphify-out/ with god nodes, community structure, and cross-file relationships.

Rules:
- For codebase questions, first run `graphify query "<question>"` when graphify-out/graph.json exists. Use `graphify path "<A>" "<B>"` for relationships and `graphify explain "<concept>"` for focused concepts. These return a scoped subgraph, usually much smaller than GRAPH_REPORT.md or raw grep output.
- If graphify-out/wiki/index.md exists, use it for broad navigation instead of raw source browsing.
- Read graphify-out/GRAPH_REPORT.md only for broad architecture review or when query/path/explain do not surface enough context.
- After modifying code, run `graphify update .` to keep the graph current (AST-only, no API cost).

