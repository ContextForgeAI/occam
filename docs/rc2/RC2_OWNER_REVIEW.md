# RC.2 owner review — consolidated commit preparation

**Review date:** 2026-07-23
**Repository:** ContextForgeAI/occam-private (local workspace `FFOccamMCP`)
**Branch:** `main` @ `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`
**Commits created by this review:** none
**Tag/publish:** not performed (and not ready)

---

## 1. Reviewed repository state

| Field | Value |
|---|---|
| HEAD | `a535705` — Merge PR #1 (release-version-and-prerelease) |
| Working tree | Dirty: ~58 modified tracked paths + large untracked RC.2 tree |
| `git status --short` lines | 78 (directory-collapsed); expanded non-`validation` untracked ≈ 100+ files |
| `validation/` untracked | **262 files** (RC.1 evidence pack) — see §3 / §11 |
| Local win-x64 AOT | Present under gitignored `artifacts/rc2/win-x64/` |
| Public Windows package | Not built / not published |
| Remote macOS / Linux | Pending |

Hygiene pass (`docs/maintenance/REPOSITORY_HYGIENE_REPORT.md`) is respected: shared `.cursor/rules`, `.codex` example, `.claude/settings.json`, and Graphify skills remain tracked; local/generated state stays ignored.

Owner-review micro-fix applied during this pass (docs only):

- Replaced machine-absolute soak command paths in `docs/rc2/pr-h/RC2_SOAK_REPORT.md` and
  `docs/rc2/pr-h/RC2_FINAL_OWNER_REPORT.md` with repo-relative
  `artifacts/rc2/win-x64/OccamMcp.Core.exe`.

---

## 2. PR-A through PR-H scope summary

| Stage | Scope | Local status |
|---|---|---|
| PR-A | Characterization + expected-red baseline | Complete (characterization 32/32 vs RC.1 host) |
| PR-B | Digest MCP boundary (`JsonElement` urls, normalizer, schema `oneOf`) | Complete |
| PR-C | Unified access classification + worker evidence | Complete |
| PR-D | `SectionIndex` + structure-aware focus / fragments | Complete |
| PR-E | Projection-first budget + answer-unit protection | Complete |
| PR-F | Semantic envelope / recovery / claim_check aliases | Complete |
| PR-G | `HostIdentity`, targeted shutdown, process-group cleanup, CLI diagnose | Complete |
| PR-H | Integration matrix, soak script, remote packs, release artifact notes | Complete (local) |

Cumulative implementation is coherent: materialization/planner boundaries, codec separation, semantic
dimensions, recovery fields, claim-check `retrieved`/`verdict`, lifecycle identity, and validation
docs/scripts are present without contradictory partial hacks in the Core/worker surface reviewed for
commit.

**Gate note:** focused MCP-boundary assertions require
`OCCAM_RC2_HOST=artifacts/rc2/win-x64/OccamMcp.Core.exe`. Without it, resolution falls back to the
stale root RC.1 `OccamMcp.Core.exe` and D12 desired-contract checks appear red. Characterization must
run **without** `OCCAM_RC2_HOST`.

---

## 3. Hygiene decisions

### Include in consolidated commit

- RC.2 Core / workers / launcher / stop-helper changes
- `benchmarks/rc2-regression/**` + related `l0-gate` updates
- `docs/rc2/**`, user-facing contract docs, CHANGELOG Unreleased RC.2 notes
- Remote validation scripts + soak runner
- `.gitignore` tightenings + `.cursor/rules/README.md` link fix
- `docs/maintenance/REPOSITORY_HYGIENE_REPORT.md`

### Exclude from consolidated commit

| Path | Reason |
|---|---|
| `artifacts/**` (incl. win-x64 exe, soak/manifest/gate logs) | Generated; already gitignored |
| Root `OccamMcp.Core.exe` | Stale RC.1 host; ignored |
| `bin/` / `obj/` / `node_modules/` | Generated |
| `.claude/settings.local.json` | Local; ignored |
| **`validation/**` (262 files)** | **Owner decision — exclude by default** (see §11) |

### Intentionally retained contributor infrastructure (do not untrack)

`.cursor/rules/*.mdc`, `.cursor/rules/README.md`, `.cursor/mcp.json.example`,
`.codex/config.toml.example`, `.claude/settings.json`, retained Graphify skills.

### Deferred (untouched)

wasm-extractor, vscode-extension, Graphify consolidation, archive migration, historical report
cleanup, orphan benchmark scripts, broad package restructuring.

---

## 4. Complete gate results (owner-review revalidation)

Environment: `OCCAM_HOME=<repo>`; focused/regression suites with
`OCCAM_RC2_HOST=artifacts/rc2/win-x64/OccamMcp.Core.exe`; characterization without that env.

| Gate | Result |
|---|---|
| `--pr-b` | 13/13, exit 0 |
| `--pr-c` | 22/22, exit 0 |
| `--pr-d` | 34/34, exit 0 |
| `--pr-e` | 43/43, exit 0 |
| `--pr-f` | 54/54, exit 0 |
| `--pr-g` | 61/61, exit 0 |
| `--regression` | 22/22, exit 0 |
| `--characterization` | 32/32, exit 0 (RC.1 host) |
| unit (`--unit-only`) | `L0_GATE_OK`, exit 0 |
| fast L0 | `L0_GATE_FAST_OK`, exit 0 |
| full L0 | `L0_GATE_OK`, exit 0 |
| `node scripts/check-docs.mjs` | OK — 91 documents, exit 0 |
| `git diff --check` | OK (CRLF→LF warnings only) |
| Soak | Not rerun (no runtime-code change in this review beyond absolute-path doc sanitization) |
| linux-x64 / osx-arm64 AOT | Not attempted (cross-OS unsupported from Windows) |

---

## 5. Windows packaging readiness classification

**Classification: `READY_WITH_EXISTING_PACKAGING`**

Existing machinery (`scripts/build-release.ps1` → `scripts/lib/build-release.mjs`) already stages:

- Native AOT host binary
- `workers/` (without `node_modules`)
- operator scripts + `scripts/lib/`
- `profiles/`, optional `skills/occam/`
- `VERSION` + inner `release-manifest.json`
- tarball + external SHA-256 manifest

The local `artifacts/rc2/win-x64/OccamMcp.Core.exe` alone is **not** a public release package.
Producing a Level-B Windows RC.2 tarball later is an **execution** of existing packaging, not new
architecture. Product version string remains `1.0.0-rc.1` until the owner chooses RC.2 naming/tagging.

---

## 6. Pending external validations

| Target | Status |
|---|---|
| macOS ARM64 | **Pending** — package prepared (`docs/rc2/pr-h/MACOS_ARM64_VALIDATION.md`, `scripts/rc2-remote-macos-arm64.sh`) |
| Linux x64 / Hermes-neutral MCP | **Pending** — package prepared (`docs/rc2/pr-h/LINUX_X64_HERMES_VALIDATION.md`, `scripts/rc2-remote-linux-x64.sh`); no Occam-invented Hermes API |
| Public Windows release package | Not prepared / not published |
| Tag / publish RC.2 | **Not ready** |

Docs consistently mark remote work as pending; no false “validated on macOS/Linux” claim found in
`docs/rc2/pr-h/`.

---

## 7. Known limitations

1. linux-x64 / osx-arm64 Native AOT require native OS hosts.
2. Remote validation hardware results are not filed.
3. Hermes remains an external MCP client boundary.
4. Product identity remains `1.0.0-rc.1` until owner chooses RC.2 tag/version.
5. Characterization suite intentionally freezes RC.1 host behavior against root `OccamMcp.Core.exe`.
6. `docs/rc2/` remains the working engineering set; durable ADR/user-doc fold-in is post-commit follow-up.

---

## 8. Deferred work

As listed in the hygiene report and owner brief: wasm-extractor, vscode-extension, Graphify
consolidation, archive migration, historical report cleanup, orphan bench scripts, broad package
restructuring, CURRENT_STATE historical scrub (unless introduced by this RC.2 diff — it was not).

---

## 9. Proposed consolidated commit message

**Title:**

```text
feat(rc2): complete materialization, semantics, lifecycle and validation
```

**Body:**

```text
Land the cumulative RC.2 local implementation (PR-A…PR-H): digest MCP boundary,
unified access classification, structure-aware focus, projection-first budget,
semantic envelope + claim-check honesty, identity-scoped lifecycle/shutdown,
integration soak tooling, and RC.2 validation documentation.

Remote macOS ARM64 and Linux x64/Hermes packs are prepared but not executed.
Windows public release packaging uses existing Level-B machinery and is not
published in this commit. Product version remains 1.0.0-rc.1 until tagging.

Excludes generated artifacts and the unscrubbed validation/ evidence tree
(personal paths / LAN markers) pending owner decision.
```

---

## 10. Exact path scope (include)

### Modified tracked

`.cursor/rules/README.md`, `.gitignore`, `CHANGELOG.md`, `MCP_API_SPEC.md`,
`benchmarks/l0-gate/*` (listed in git status), user docs under `docs/` touched by RC.2,
`packages/occam-mcp/lib/index.ts`, `packages/occam-skill/skill/**`,
`scripts/launch-mcp-host.mjs`, `scripts/lib/stop-occam-processes.mjs`,
all modified `src/FFOccamMcp.Core/**` and worker access-evidence plumbing files.

### Untracked to add

- `benchmarks/l0-gate/SectionIndexUnitTests.cs`
- `benchmarks/rc2-regression/**`
- `docs/maintenance/REPOSITORY_HYGIENE_REPORT.md`
- `docs/rc2/**` (including this file)
- `scripts/rc2-soak.mjs`, `scripts/rc2-remote-macos-arm64.sh`, `scripts/rc2-remote-linux-x64.sh`
- `src/FFOccamMcp.Core/Access/**`, new Compile/Digest/Knowledge/Lifecycle/Semantics files
- `workers/shared/lib/access-evidence.mjs`, `workers/http-extract/lib/access-evidence.selftest.mjs`

### Intentionally excluded

- `artifacts/**`
- `validation/**` (default recommendation)
- release binaries / tarballs
- local IDE configs already ignored

---

## 11. Exact blockers / owner decisions

### Owner decision (resolved for consolidated commit)

**`validation/`** (local RC.1 evidence pack on disk) contained personal absolute paths and a private
LAN endpoint marker. No API keys / private keys / token material were found in the RC.2 candidate
source/docs scope. Provider labels such as OpenRouter appear only as environment names.

**Decision:** exclude the entire `validation/` tree from the consolidated commit; keep it on disk;
ignore via `.gitignore` (`validation/`). Do not scrub or commit evidence in this change set.

### Non-blockers for consolidated commit

- Pending remote macOS/Linux validation (explicitly out of commit scope)
- Public Windows package not yet produced (packaging machinery exists)
- Product version still `1.0.0-rc.1`
- Stale root `OccamMcp.Core.exe` (ignored; keep for characterization)

---

## 12. Final readiness verdict

**`READY_FOR_CONSOLIDATED_COMMIT`** (after owner confirmation to exclude `validation/`)

Local RC.2 implementation + gates are green. Consolidated commit stages PR-A…PR-H code, tests, docs,
tooling, and approved hygiene only.

Not ready to tag or publish RC.2.
