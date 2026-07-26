# PR-A work log

## Scope

PR-A freezes the RC.1 regression baseline and evaluates test-only architecture spikes. It does not change production behavior, public contracts, package versions, release artifacts, or frozen RC.1 evidence.

## Starting state

- Branch: `main`
- Commit: `a535705c03a9bf483cbbb23aefe8c4e60cf7b48f`
- Existing user changes at start: modified `.gitignore`; untracked `docs/rc2/` and `validation/`
- Evidence manifest: `validation/evidence/rc1/_archives/SHA256SUMS.txt`
- Evidence integrity: 21/21 archives matched before editing

The existing user changes were preserved. New documentation is confined to `docs/rc2/pr-a/`; new executable test assets are confined to `benchmarks/rc2-regression/`.

## Files inspected

- `AGENTS.md`, `docs/index.md`, `.cursor/rules/README.md`
- All seven `docs/rc2/RC2_*` design inputs and `CODEX_EVIDENCE_REVIEW.md`
- `validation/RC1_PROBLEM_EVIDENCE_PACK.md`, `validation/RC1_EVIDENCE_PACK_SUMMARY.md`, and defect indexes
- `OccamDigestTool`, `DigestUrlParser`, `DigestService`, and live MCP contract tests
- `HtmlProbeClassifier`, `LoginWallDetector`, and `RequiresLoginPostProcessor`
- `FocusMatcher`, `TokenBudget`, `ResponseBudgetPlanner`, `BudgetOwnership`, and `TranscodePipeline`
- `L0Gate` entry points, unit conventions, and stdio MCP test helpers

`graphify-out/graph.json` was used only as an attempted navigation aid. Its stored interpreter pointed to an inaccessible environment, so the graph query failed; every implementation decision was verified directly in source.

## Decisions

1. Keep intentionally red tests out of the normal L0 gate in a separate console project.
2. Provide three explicit modes: green characterization, green spikes, and expected-red desired contracts.
3. Exercise D12 against the real published stdio boundary; keep every other blocking fixture offline.
4. Use synthetic minimized fixtures derived from frozen evidence rather than copying large or host-specific pages.
5. Treat D17 and D3 as characterization plus test-design/spike coverage because dedicated frozen D17 evidence and deterministic production lifecycle control are incomplete.
6. Do not add production seams: all diagnostics and prototypes remain under the regression project.

## Blockers and limitations

- The mission referenced `validation/evidence/rc1/SHA256SUMS.txt`; the actual manifest is `_archives/SHA256SUMS.txt`.
- `d10-rfc-*` remains classified as D15, not D10.
- D17 lacks a dedicated frozen archive. The test proves that fragment intent is not passed into the current planner; PR-D must validate final fragment semantics.
- Production lifecycle identity is not exposed. PR-A models exact-owner shutdown in a test harness and leaves process-tree automation to PR-G.
- The PowerShell `run-l0-fast.ps1` wrapper surfaced Node stderr as `NativeCommandError`. The compiled `L0Gate.exe --fast` was run directly to obtain the real exit code.
- The filesystem patch helper intermittently failed with `helper_unknown_error`. Only new PR-A files were patched; production and pre-existing user files were not modified.

## Commands executed

```powershell
git status --short
git branch --show-current
git rev-parse HEAD
Get-FileHash validation/evidence/rc1/_archives/* -Algorithm SHA256
dotnet build benchmarks/rc2-regression/Rc2Regression.csproj -c Release
dotnet run --project benchmarks/rc2-regression -c Release -- --characterization
dotnet run --project benchmarks/rc2-regression -c Release -- --spikes
dotnet run --project benchmarks/rc2-regression -c Release -- --regression
dotnet run --project benchmarks/l0-gate -c Release -- --unit-only
benchmarks/l0-gate/bin/Release/net10.0/L0Gate.exe --fast
benchmarks/l0-gate/bin/Release/net10.0/L0Gate.exe
node scripts/check-docs.mjs
dotnet publish src/FFOccamMcp.Core -c Release -r win-x64
```

## Results

- PR-A build: passed, 0 warnings in the final project build.
- Green characterization: 32/32 passed after adding parser coverage.
- Technical spikes: 4/4 passed.
- Expected-red suite: 12/12 desired assertions failed deterministically on RC.1.
- Existing unit gate: exit 0.
- Existing fast gate: direct executable exit 0.
- Existing full gate: direct executable exit 0.
- Docs check: exit 0, prior to the final doc-only additions; rerun is recorded in the validation report.
- Release win-x64 publish: exit 0.
- Evidence archives: 21/21 SHA-256 matches before and after PR-A.

See [the test matrix](PR_A_TEST_MATRIX.md), [semantic contract](CURRENT_SEMANTIC_CONTRACT.md), [spike results](PR_A_SPIKE_RESULTS.md), and [validation report](PR_A_VALIDATION_REPORT.md).
