# RC.2 soak report

## Command

```powershell
$env:OCCAM_HOME = (Get-Location).Path
$env:OCCAM_RC2_HOST = (Resolve-Path artifacts/rc2/win-x64/OccamMcp.Core.exe).Path
node scripts/rc2-soak.mjs --iterations=3 --host=$env:OCCAM_RC2_HOST
```

Exact recorded command:

```text
node scripts/rc2-soak.mjs --iterations=3 --host=artifacts/rc2/win-x64/OccamMcp.Core.exe
```

## Design

Bounded local soak only:

1. Offline focused `--pr-g` against the candidate host (deterministic fixtures, no network).
2. `lifecycle self` and `lifecycle diagnose` (read-only CLI).
3. One MCP stdio session per iteration: `tools/list`, `occam_probe`, focused/budgeted
   `occam_transcode`, native-array `occam_digest` against `https://example.com/` with
   `backend_policy=http`.
4. Exact-child `SIGTERM` shutdown; no kill-by-name; no paid services.

## Measured results

| Metric | Value |
|---|---|
| Iterations | 3 |
| Failures | 0 |
| Exit code | 0 |
| Elapsed time | 27,978 ms |
| Process count before | 1 |
| Process count after | 1 |
| Orphaned host count | 0 |
| Maximum observed memory | 42.3 MB (`OccamMcp.Core` working set) |
| Artifact path | `artifacts/rc2/soak-report.json` |
| Artifact SHA-256 | `902748184297fde030d5a8b6be2b7034ab29ca0fc90c3bceb6a6767c6335ddce` |

## Per-iteration observations (summary)

Every iteration recorded:

- offline PR-G `61/61`
- lifecycle self with a populated `runtimeId`
- lifecycle diagnose `ok=true`
- `tools/list` with 15 core tools
- probe against example.com without a hard login false positive
- focused/budgeted transcode with semantic envelope fields present (`focus`, `completeness`)
- digest native array accepted (`ok=true`, no `invalid_arguments`)

## Limitations

- Live network is limited to `example.com` and is informational relative to frozen fixtures.
- Process counting on Windows matches Occam/node worker command lines heuristically; it is not a
  full OS process-tree auditor.
- Memory sampling observes `OccamMcp.Core` working set only while processes are alive; peak may be
  understated if a child exits before sampling.
- Soak does not replace full L0 live corpora evidence already recorded in the integration matrix.
