# Contributing to FF-Occam MCP

Welcome! FF-Occam is a local-first MCP server that compiles web pages into LLM-ready Markdown. We value clear code, honest failures, and good documentation.

---

## Quick start

```powershell
# 1. Clone (public)
git clone https://github.com/ContextForgeAI/occam.git occam
cd occam

# 2. Install dependencies + publish AOT (.NET 10 SDK required)
$env:OCCAM_HOME = (Get-Location).Path
.\scripts\occam-doctor.ps1

# 3. Run the fast smoke gate (~30s)
.\scripts\run-l0-fast.ps1

# 4. Run the full gate (L0–L7)
dotnet run --project benchmarks\l0-gate

# 5. Launch MCP host (for manual testing)
node scripts\launch-mcp-host.mjs
```

**Operators (no .NET SDK):** use the canonical one-liner in [INSTALL.md](INSTALL.md) instead of this clone path.

---

## Project architecture

```
src/FFOccamMcp.Core/          ← Native AOT .NET 10 host (the MCP server)
  Program.cs                   ← CLI parse → transport start
  Transport/                   ← JSON-RPC framing (stdio / WebSocket)
  Tools/                       ← 8 MCP tool handlers
  Routing/                     ← Backend policy dispatch (http/browser/http_then_browser)
  PostProcessors/              ← Quality validation (thin, challenge, login)
  Compile/                     ← Token-aware markdown pruning (BM25)
  Workers/                     ← Node.js process management
  Services/                    ← Orchestration (probe, digest, map)
  Playbooks/                   ← Heal/save loop, genome resolution
  Batch/                       ← Experimental batch HTTP server (SQLite)
  Abstractions/                ← Interface contracts

workers/                       ← npm workspace (Node.js)
  http-extract/                ← HTTP-only extraction (domino + readability + turndown)
  browser-extract/             ← Playwright Chromium extraction
  css-extract/                 ← CSS extraction worker
  shared/lib/                  ← Egress proxy, consent, cookies

benchmarks/l0-gate/            ← Gate system (console app, not test framework)
packages/                      ← npm packages (@ff-occam/mcp, @ff-occam/agent-sdk)
scripts/                       ← Build, test, CI scripts
docs/                          ← User documentation
```

**Key design decisions:**
- **Always live extract** — no file cache in Core by design
- **Honest compiler (K1)** — `ok: true` with markdown, or typed `failure.code`; never hallucinate
- **Token contract (K2)** — opt-in `max_tokens`, `fit_markdown`, `focus_query`
- **Native AOT** — single-file binary, no runtime dependency on target
- **Stderr for diagnostics, stdout only for MCP JSON** — never mix

---

## How to contribute

### Reporting issues

- Use GitHub Issues
- Include: OS, .NET version (`dotnet --version`), Node version (`node --v`), Occam version
- For extraction failures: include the `failure.code`, `url`, and `backend_policy` used
- For bugs: include the gate output or stderr logs

### Submitting changes

1. Fork the repository
2. Create a feature branch: `git checkout -b feat/my-change`
3. Make your change — keep it small and focused
4. Run the gate: `.\scripts\run-l0-fast.ps1` (or `dotnet run --project benchmarks\l0-gate`)
5. Update docs if you change tool behavior, failure codes, or env vars
6. Commit with a clear message (see below)
7. Push and open a PR against `main`

### Commit conventions

```
feat(core): add confidence scoring to transcode response
fix(router): handle timeout in http_then_browser fallback
docs(roadmap): add v0.9 agent-first enhancements
chore(ci): update occam-release.yml for .NET 10
```

Prefixes: `feat`, `fix`, `docs`, `chore`, `refactor`, `test`, `perf`.

### Code style

- **C#:** Follow existing patterns. Use `sealed` classes where possible. Prefer `record` for immutable data. Use source-generated JSON (`JsonSerializerContext`) for AOT compatibility.
- **No file cache** — if you're tempted to cache, you're probably solving the wrong problem
- **Honest failures** — new failure codes go in `docs/failure_codes.md` AND `src/FFOccamMcp.Core/Routing/FailureCodeStrings.cs`
- **AOT-safe** — no reflection, no dynamic loading, no `System.Text.Json` without source generators
- **Tests** — gate tests live in `benchmarks/l0-gate/`. Add a gate level for new features.

### What NOT to change without discussion

- MCP tool names or parameter contracts (breaking change)
- The "always live extract" principle
- The 8-tool surface (no new tools without community consensus)
- The honest compiler principle (K1)

---

## Gate system

The gate is a console application organized as "levels" — each level is a static class with a `Run()` method:

| Level | What it tests |
|-------|---------------|
| L0 | Smoke corpus (l0-smoke.jsonl) |
| L1a | Token economy (max_tokens, fit_markdown, focus_query) |
| L1b | Probe classifier, redirects, SPA detection |
| L1 | Failure taxonomy (every failure code) |
| L2 | Digest, map, session profiles, transport, egress, media refs |
| L3 | Heal-learn (playbook heal → save → verify) |
| L4 | Genome resolution (playbook_policy=auto, genome merge) |
| L5 | Batch server |
| L6 | Browser pool lifecycle |
| L7 | Resource safety (concurrent extract, cleanup) |

Run specific levels: `dotnet run --project benchmarks\l0-gate -- --smoke-only`

---

## Documentation discipline

When changing code, update the doc matrix:

| Change type | Files to update |
|-------------|----------------|
| New tool parameter | `MCP_API_SPEC.md`, `docs/tool_reference.md` |
| New failure code | `docs/failure_codes.md`, `FailureCodeStrings.cs` |
| New env var | `docs/configuration.md`, `MCP_API_SPEC.md` |
| New gate level | `docs/roadmap.md` |
| Architecture change | `CLAUDE.md`, `docs/agent_handbook.md` |

---

## Community

- Be kind. See [CODE_OF_CONDUCT.md](CODE_OF_CONDUCT.md).
- Ask questions in GitHub Discussions (or Issues for bugs).
- Help others in PRs — review is a form of contribution.

---

## License

FF-Occam MCP is licensed under [AGPL-3.0](LICENSE). By contributing, you agree to license your contributions under the same license.
