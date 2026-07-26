# PR-H — validation report

## Stop-gate status

**Pass (local).** All available integration gates on this Windows host are green. Remote macOS/Linux
execution remains pending and is not represented as completed.

## Gate results

| Suite | Command | Result |
|---|---|---|
| PR-B focused | `OCCAM_RC2_HOST=artifacts/rc2/win-x64/OccamMcp.Core.exe dotnet run --project benchmarks/rc2-regression -c Release -- --pr-b` | 13/13, exit 0 |
| PR-C focused | `… --pr-c` | 22/22, exit 0 |
| PR-D focused | `… --pr-d` | 34/34, exit 0 |
| PR-E focused | `… --pr-e` | 43/43, exit 0 |
| PR-F focused | `… --pr-f` | 54/54, exit 0 |
| PR-G focused | `… --pr-g` | 61/61, exit 0 |
| Cumulative RC.2 regression | `… --regression` | 22/22, exit 0 |
| Characterization (RC.1 host, no `OCCAM_RC2_HOST`) | `dotnet run --project benchmarks/rc2-regression -c Release -- --characterization` | 32/32, exit 0 |
| Unit | `dotnet run --project benchmarks/l0-gate -c Release -- --unit-only` | `L0_GATE_OK`, exit 0 |
| Fast L0 | `.\scripts\run-l0-fast.ps1` | `L0_GATE_FAST_OK`, exit 0 |
| Full L0 | `dotnet run --project benchmarks/l0-gate -c Release` | `L0_GATE_OK`, exit 0 (~273 s) |
| Docs | `node scripts/check-docs.mjs` | OK — 81 documents, exit 0 |
| Diff hygiene | `git diff --check` | exit 0 |
| Soak | `node scripts/rc2-soak.mjs --iterations=3 --host=artifacts/rc2/win-x64/OccamMcp.Core.exe` | 0 failures, exit 0 |
| Native AOT win-x64 | `dotnet publish … -r win-x64 -o artifacts/rc2/win-x64` | exit 0 |
| Native AOT linux-x64 | `dotnet publish … -r linux-x64` | exit 1 — cross-OS unsupported |
| Native AOT osx-arm64 | `dotnet publish … -r osx-arm64` | exit 1 — cross-OS unsupported |

## Expected-red residual

None. Cumulative `--regression` is fully green (22/22). Characterization remains the frozen RC.1
baseline against the RC.1 host and stays green (32/32).

## Frozen RC.1 evidence

- Path: `validation/evidence/rc1/_archives/SHA256SUMS.txt`
- SHA-256 of manifest: `e5e1637458cf495b76c8a637c125bcd32e63ebfe5b34708f1b810143432b01d2`
- PR-H made no edits under `validation/evidence/rc1/`.

## Hygiene

- No temporary patch / `*.orig` / graphify scratch files left by PR-H.
- Soak and gate logs live under gitignored `artifacts/rc2/`.
- No unrelated process termination; soak uses exact-child `SIGTERM` only.
- No commit created.

## Candidate host

- Path: `artifacts/rc2/win-x64/OccamMcp.Core.exe`
- Size: 38,630,400 bytes
- SHA-256: `184d6e7ce8024339eb560f7af91bb3860174c75725712b19b59c1d73202fdaff`
