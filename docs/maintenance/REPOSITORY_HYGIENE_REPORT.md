# Repository Hygiene Report — Final pass before RC.2

**Date:** 2026-07-23
**Branch at audit:** `main` @ `a535705` (plus local uncommitted RC.2 WIP — not part of this pass)
**Scope:** noise reduction and classification only. No runtime, API, CI, or release-logic changes.
**Commits created by this pass:** none.

---

## Executive summary

Tracked generated Python bytecode is already gone (prior hygiene). Eval-harness `dist/` and machine-specific Codex config are already ignored. Release tarballs stage an explicit allow-list and do **not** include IDE dirs, corpora, or caches.

This final pass:

1. Classified all tracked files under `.cursor/`, `.codex/`, `.claude/` (and related `.agents/`).
2. Tightened `.gitignore` for common generated/patch leftovers.
3. Fixed broken relative links in `.cursor/rules/README.md`.
4. **Did not** untrack project Cursor rules or Graphify skill trees (would harm contributor DX / needs an owner decision).
5. **Did not** delete historical docs, orphan scripts, wasm/vscode packages, or Graphify duplicates.

**Recommendation for RC.2 tag:** proceed with release hygiene as-is; schedule a dedicated post-RC cleanup PR for IDE/Graphify consolidation after the RC.2 feature WIP lands.

---

## 1. Files removed

| Path | Action | Notes |
|------|--------|-------|
| *(none in this pass)* | — | Prior pass already removed `scripts/bench/__pycache__/*.pyc` and tracked `.codex/config.toml` |

Local files were **not** deleted from disk. No `git rm --cached` was applied to IDE directories in this pass (see §3 recommendations).

---

## 2. Files ignored

### Already present (kept)

| Pattern | Purpose |
|---------|---------|
| `.cursor/mcp.json`, `.mcp.json` | Local MCP wiring |
| `.codex/config.toml` | Machine-specific Codex MCP paths |
| `artifacts/`, `docs-internal/`, `bin/`, `obj/`, `node_modules/` | Build / engineering |
| `__pycache__/`, `*.pyc` | Python bytecode |
| `corpora/eval-harness/dist/` | Eval harness tsc output |
| `validation/results-private/`, `validation/work/` | Private validation evidence (already in working-tree `.gitignore`) |
| Graphify `graphify-out/` | Generated knowledge graph |
| Secrets / certs / OS junk | Standard |

### Added in this pass

| Pattern | Purpose |
|---------|---------|
| `*.orig`, `*.rej` | Patch leftovers |
| `*.pdb` | Debug symbols (belt-and-suspenders beyond `bin/`/`obj/`) |
| `coverage/` | Test coverage output |
| `TestResults/` | .NET test result folders |
| `.claude/settings.local.json` | Claude Code local overrides (also often covered by global gitignore) |

### Intentionally versioned under `bin/` exceptions

| Path | Why kept |
|------|----------|
| `packages/occam-mcp/bin/occam-mcp.js` | Published npm entry (hand-written) |
| `packages/occam-skill/bin/install.mjs` | Published npm installer |

---

## 3. Files intentionally kept (IDE / agent)

### `.cursor/`

| Path | Classification | Recommendation |
|------|----------------|----------------|
| `rules/occam-l0-core.mdc` | **Required by every contributor** | KEEP tracked |
| `rules/documentation-sync.mdc` | Required (doc edits) | KEEP |
| `rules/csharp-host.mdc` | Required (C# edits) | KEEP |
| `rules/node-workers.mdc` | Required (worker edits) | KEEP |
| `rules/l0-gate.mdc` | Required (gate edits) | KEEP |
| `rules/quality-audit.mdc` | Required (quality paths) | KEEP |
| `rules/README.md` | Supporting index | KEEP (links fixed this pass) |
| `mcp.json.example` | **Optional example** | KEEP (uses `${workspaceFolder}`, no absolute paths) |
| `mcp.json` (local) | Machine-specific | Already ignored |

**Do not** remove `.cursor/rules/` from Git: they encode L0 scope and doc discipline for Cursor contributors.

### `.codex/`

| Path | Classification | Recommendation |
|------|----------------|----------------|
| `config.toml.example` | Optional example | KEEP |
| `config.toml` (local) | Machine-specific | Already ignored |
| `hooks.json` | Optional Graphify PreToolUse hooks | KEEP for now; candidate to demote/example-ize in a future PR |

### `.claude/`

| Path | Classification | Recommendation |
|------|----------------|----------------|
| `CLAUDE.md` | Optional Graphify tip (4 lines) | KEEP or fold into root `CLAUDE.md` later |
| `settings.json` | Optional shared Graphify hooks | KEEP for now; owner-review whether project-wide |
| `settings.local.json` | Machine-specific | Untracked; now explicitly ignored |
| `skills/graphify/**` | Optional third-party skill (near-dup of `.agents`) | KEEP pending consolidation PR |

### `.agents/` (related)

| Path | Classification | Recommendation |
|------|----------------|----------------|
| `skills/graphify/**` | Near-duplicate of `.claude/skills/graphify` | Future: one canonical copy + client adapters |

---

## 4. Phase classification summary

### Generated artifacts (Phase 2)

| Check | Result |
|-------|--------|
| Tracked `__pycache__` / `*.pyc` | None |
| Tracked `dist/` / `coverage/` / `TestResults/` / `node_modules/` | None |
| Tracked `bin/` / `obj/` outside intentional package bins | None |
| Accidental binaries at repo root | Ignored (`/OccamMcp.Core(.exe)`) |

### Structure noise (Phase 3) — report only, not deleted

| Item | Status | Future action |
|------|--------|---------------|
| Dual Graphify trees (`.agents` + `.claude`) | Near-duplicate (byte sizes differ slightly) | Consolidate in cleanup PR |
| Root `CLAUDE.md` vs `.claude/CLAUDE.md` | Overlap / tip split | Optional consolidate |
| `packages/wasm-extractor` | Planned / not in RC runtime | Owner-review post-RC |
| `packages/vscode-extension` | Stale surface (old tool counts) | Update or archive post-RC |
| Zero-ref `scripts/bench/*` experiments | Maintainer-only | Triage / archive post-RC |
| Historical `corpora/quality-audit-reports/` | Evidence | Archive path later |
| Stale numbered doc refs in some `.mdc` matrices (`docs/04`, …) | Repairable | Separate docs PR |
| Uncommitted `docs/rc2/**`, `validation/**`, Core WIP | Active RC.2 work | Out of scope for this hygiene pass |

### Release hygiene (Phase 5)

`scripts/lib/build-release.mjs` → `stageReleaseTree` copies only:

- published host binary
- `workers/` (no `node_modules`)
- selected `scripts/*` + `scripts/lib/`
- `profiles/`
- `skills/occam/`
- `VERSION` + inner `release-manifest.json`

**Not packaged:** `.cursor/`, `.codex/`, `.claude/`, `.agents/`, `corpora/`, `benchmarks/`, `docs/`, `packages/`, `artifacts/`, caches, secrets, `dist/`, `__pycache__`.

No packaging change required for RC.2.

---

## 5. Remaining technical debt

1. **Graphify duplication** across `.agents/skills/graphify` and `.claude/skills/graphify`.
2. **Contributor rule matrix drift** in `.cursor/rules/documentation-sync.mdc` (still mentions retired numbered docs in some rows).
3. **Satellite packages** (`wasm-extractor`, `vscode-extension`) not part of tarball RC but still in tree.
4. **Orphan / experimental bench scripts** with weak references.
5. **Working tree noise:** large uncommitted RC.2 feature set — keep hygiene commits separate from feature PRs.
6. **`docs/development/CURRENT_STATE.md`** still documents a LAN Forgejo URL — scrub before any public mirror.

---

## 6. Recommendations for RC.2

| Priority | Action |
|----------|--------|
| **Do now (before tag)** | Keep `.gitignore` tightenings from this pass; ensure private `validation/` paths stay ignored; do not ship IDE dirs (already true). |
| **Do now** | Land RC.2 feature work on clean, reviewable PRs — do not mix with IDE untracking. |
| **Do not** | `git rm --cached` `.cursor/rules` before RC.2. |
| **Do not** | Delete Graphify / historical audits / wasm in the RC.2 tag rush. |
| **Verify on tag pipeline** | `occam-release.yml` linux + macos-on-tag; version scripts from PR #1. |

---

## 7. Future cleanup PR (post-RC.2)

Suggested dedicated PR (reviewable, no runtime diffs):

1. Consolidate Graphify to one tree; thin adapters for the other client.
2. Decide fate of `.claude/settings.json` / `.codex/hooks.json` (project vs example).
3. Finish stale path repairs in Cursor rule matrices.
4. Triage `scripts/bench` orphans → `scripts/bench/archive/` or delete with owner list.
5. Archive dated quality-audit reports under `docs/archive/` or `corpora/archive/`.
6. Scrub `CURRENT_STATE.md` LAN URLs before public exposure.
7. Update or quarantine `vscode-extension` / `wasm-extractor` docs claims.

---

## Validation (this pass)

| Check | Result |
|-------|--------|
| `git diff --check` | Run at end of pass |
| `node scripts/check-docs.mjs` | OK prior to edits; re-run after |
| Tracked generated artifacts | None found matching pyc/dist/coverage/node_modules |
| Machine-specific tracked paths | No `C:\Users\…` / `C:\PROJECTS\…` in tracked config (Codex example uses placeholders) |
| Secrets grep (`PRIVATE KEY` / `ghp_` / `sk-`) | No hits in tracked sources |
| Accidental binaries | Only intentional package `bin/` wrappers tracked |

---

## Changes made in this working tree (uncommitted)

| Path | Change |
|------|--------|
| `.gitignore` | Added `*.orig`, `*.rej`, `*.pdb`, `coverage/`, `TestResults/`, `.claude/settings.local.json` (plus pre-existing uncommitted `validation/*` ignore lines) |
| `.cursor/rules/README.md` | Fixed broken relative links to `AGENTS.md` / launch script; removed missing `docs/12-…` public link |
| `docs/maintenance/REPOSITORY_HYGIENE_REPORT.md` | This report |

**Not modified:** Core, workers, CI workflows, release scripts, Eval Harness logic, public MCP APIs, tests.
