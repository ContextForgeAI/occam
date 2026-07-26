# Current development state — synchronized baseline

**Date:** 2026-07-22  
**Baseline tag (after this commit):** `baseline-2026-07-22`  
**Validated tip (parent of this docs commit):** `85d2e13fad0bae1250ab0399247b7d99d6b7670a`  

All agents **must begin from the commit identified by** `baseline-2026-07-22` (this file’s commit once tagged). Do not start new feature work from an older tip or an unmerged side branch unless the task explicitly names that branch.

---

## Exact baseline

| Item | Value |
|------|--------|
| Branch | `main` |
| Validated parent SHA | `85d2e13fad0bae1250ab0399247b7d99d6b7670a` |
| Subject (parent) | `fix(env): drop dead OTEL/NEEDLES docs; scan packages for release URL` |
| This document | Committed on `main` immediately after validation; annotated tag points here |

Resolve the live baseline SHA with:

```bash
git rev-parse baseline-2026-07-22
```

---

## Git remote layout

| Remote | Role |
|--------|------|
| `origin` | **Primary** public/private GitHub remote for day-to-day work |
| Optional backup remote | Maintainer-local mirror only — not part of the public release identity |

Public release identity (tarball downloads, docs links, package metadata):

```text
https://github.com/ContextForgeAI/occam
```

Everyday workflow:

```bash
git pull
git push
```

---

## Branch synchronization state (at baseline authoring)

| Ref | Relationship |
|-----|----------------|
| `main` ↔ `origin/main` | Identical at validated tip `85d2e13` (then this docs commit advances both) |
| Backup remote `main` before sync | Behind by 6 commits (`1f3df42` …) |
| Backup remote `main` after Phase 4 | Fast-forwarded to match `main` / `origin/main` |

**Preserved unfinished branch (not merged):**

| Branch | Tip | Remotes |
|--------|-----|---------|
| `feature/playbook-chain-and-manifesto` | `e24487b8b75a99acfc1e49b78d722f1c656b4a8e` | primary + optional backup |

Historical already-merged feature/fix/ci branch refs were **not** mirrored as extra remote tips (already contained in `main`).

---

## Validation results (2026-07-22, tip `85d2e13`)

Commands (Windows, `OCCAM_HOME` = repo root):

```powershell
npm ci --no-fund --no-audit --prefix workers
dotnet build src/FFOccamMcp.Core -c Release
dotnet publish src/FFOccamMcp.Core -c Release -r win-x64 --self-contained `
  -o src/FFOccamMcp.Core/bin/Release/net10.0/win-x64/publish
.\scripts\run-l0-fast.ps1 -WithUnit
```

| Check | Result |
|-------|--------|
| `npm ci` (workers) | OK |
| `dotnet build` Core Release | OK |
| Native AOT `dotnet publish` win-x64 | OK (normal publish path for live `tools/list`) |
| `run-l0-fast.ps1 -WithUnit` | **OK** — marker `L0_GATE_FAST_OK`, exit 0 |

No application-code fixes were applied during this reconciliation.

---

## PR-A status

- No active git branch named `PR-A` / `pr-a`.
- Local engineering note (`docs-internal/CANONICAL-KNOWLEDGE-PR-A.md`, gitignored): **landed (additive types only)**.
- Canonical / planner / codec / EQM work is already on `main` (see history through `1f3df42` and later).
- **Do not reopen PR-A as a branch** unless a new scoped task explicitly requires it.

---

## Unmerged branches

Only:

- `feature/playbook-chain-and-manifesto` — 2 commits ahead of `main` (manifesto / proof_of_extraction draft). **Do not merge** unless a separate task approves it.

Other local branch refs (`feat/*`, `fix/*`, `pr-30`, `worktree-agent-*`, …) have tips that are ancestors of `main` (already integrated).

---

## Known unfinished work

- `feature/playbook-chain-and-manifesto` preserved on remotes, unmerged.
- Gitignored `docs-internal/` engineering notes (not part of the published tree).
- Author identity scrub / public OSS prep deferred where still private.

---

## Known failures

- None blocking this baseline: local L0 fast gate green at `85d2e13`.
- Prior CI issues (non-GitHub action URLs, `check-docs` syntax, env-catalog dead vars, missing AOT artifact for gate-fast) were fixed on `main` in the six commits between `1f3df42` and `85d2e13`.

---

## Next intended task

Multi-agent / feature work may resume **only after** checking out `baseline-2026-07-22` (or `main` at that tag). Do not continue “PR-A” as an open branch. Pick a new scoped task from the product roadmap / owner brief.

---

## Agent start rule

```text
git fetch origin --prune
git checkout main
git pull --ff-only
git rev-parse HEAD   # must equal: git rev-parse baseline-2026-07-22
```

If SHAs differ, **stop** and reconcile before coding.
