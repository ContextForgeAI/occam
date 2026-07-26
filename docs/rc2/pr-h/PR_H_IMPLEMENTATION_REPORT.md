# PR-H — implementation report

## Scope

PR-H is integration, soak, release-candidate documentation, and remote validation packaging only.
It does not introduce new architecture, semantic fields, access heuristics, ranking, budget policy,
lifecycle redesign, or digest transport changes.

## Starting state

| Item | Value |
|---|---|
| Base commit | `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f` |
| Prior stages | PR-A…PR-G complete; independent stop gates green |
| Commits created by PR-H | none |
| Production Core behavior changes in PR-H | none |

## Deliverables added by PR-H

| Path | Role |
|---|---|
| `scripts/rc2-soak.mjs` | Bounded local soak (offline PR-G + MCP + lifecycle) |
| `scripts/rc2-remote-macos-arm64.sh` | macOS ARM64 remote validation helper |
| `scripts/rc2-remote-linux-x64.sh` | Linux x64 Hermes-neutral remote validation helper |
| `artifacts/rc2/manifest.json` | Machine-readable release-candidate artifact manifest (gitignored) |
| `artifacts/rc2/soak-report.json` | Soak measurement artifact (gitignored) |
| `artifacts/rc2/win-x64/OccamMcp.Core.exe` | Locally built win-x64 Native AOT host (gitignored) |
| `docs/rc2/pr-h/*` | Integration matrix, soak, artifacts, remote packs, owner report |

## Validation performed locally

All focused PR suites, cumulative RC.2 regression, frozen characterization, unit gate, fast L0, full L0,
docs check, `git diff --check`, frozen evidence integrity check, win-x64 Native AOT publish, and the
bounded soak completed successfully. Exact counts and commands are in
[PR_H_VALIDATION_REPORT.md](PR_H_VALIDATION_REPORT.md) and
[RC2_INTEGRATION_MATRIX.md](RC2_INTEGRATION_MATRIX.md).

## Release artifacts

| RID | Status | Size | SHA-256 |
|---|---|---|---|
| win-x64 | Built on this host | 38,630,400 | `184d6e7ce8024339eb560f7af91bb3860174c75725712b19b59c1d73202fdaff` |
| linux-x64 | Pending native OS build | — | — |
| osx-arm64 | Pending native OS build | — | — |

Cross-OS Native AOT from Windows failed with ILCompiler:
`Cross-OS native compilation is not supported.` Remote packages document on-host publish commands.

## Compatibility

No public MCP fields were removed. Legacy aliases from PR-F remain. Digest array/string union from PR-B
remains. Lifecycle remains identity-scoped diagnostics only (PR-G). Version metadata was not renamed;
product packaging remains `1.0.0-rc.1` until the owner chooses an RC.2 tag/name.

## Limitations recorded honestly

- linux-x64 and osx-arm64 AOT binaries were not produced on this Windows builder.
- External macOS and Linux/Hermes validations were prepared but not executed on remote hardware.
- Hermes external APIs were not invented; Linux package stays MCP/lifecycle-neutral.
- Large binaries under `artifacts/` remain gitignored and are not committed.

## Stop disposition

PR-H local integration gate: **pass**.
Recommended next action: owner review → optional remote validation → first consolidated commit →
tag/publish only after remote RID artifacts exist and remote packs report green.
