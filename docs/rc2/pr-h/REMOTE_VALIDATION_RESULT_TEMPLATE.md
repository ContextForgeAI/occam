# Remote validation result template

Copy this file per remote run. Do not mark a RID complete without hashes and exit codes.

## Run identity

| Field | Value |
|---|---|
| Date (UTC) | |
| Operator | |
| Target | macOS ARM64 / Linux x64 |
| Repo commit | |
| Host path | |
| Host SHA-256 | |
| Host size (bytes) | |
| `OCCAM_HOME` | |
| Client (Cursor / Hermes / OpenRouter / other) | |
| Client version | |

## Artifact build

| Field | Value |
|---|---|
| Build command | |
| Build exit code | |
| Build host `uname -a` | |
| Notes | |

## Automated helper

| Field | Value |
|---|---|
| Script | `scripts/rc2-remote-macos-arm64.sh` / `scripts/rc2-remote-linux-x64.sh` |
| Exit code | |
| Output directory | |
| Key log paths | |

## Checklist results

| Check | Pass/Fail | Evidence path or observation |
|---|---|---|
| Artifact SHA-256 | | |
| Executable / launch | | |
| MCP initialize | | |
| tools/list | | |
| Digest native array | | |
| Public terminology access | | |
| Focus / fragment | | |
| Constrained budget | | |
| Semantic envelope | | |
| Lifecycle self | | |
| Lifecycle diagnose | | |
| Shutdown / orphan | | |
| Focused `--pr-g` | | |
| Cumulative `--regression` | | |
| Process-tree before/after | | |

## Process accounting

| Metric | Value |
|---|---|
| Occam-related count before | |
| Occam-related count after | |
| Orphans observed | |
| Max memory (if measured) | |

## Failures

List each failure with owning stage guess (`PR-B`…`PR-G` or environment) and whether it is
reproducible offline.

1.
2.

## Verdict

- [ ] Remote RID artifact built and hashed
- [ ] Offline RC.2 suites green on that host
- [ ] MCP smoke green
- [ ] No orphan hosts
- [ ] Ready to update `artifacts/rc2/manifest.json`
- [ ] Not ready — blockers below

### Blockers

-
