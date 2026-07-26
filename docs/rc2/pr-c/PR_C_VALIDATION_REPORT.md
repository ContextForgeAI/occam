# PR-C unified access classification validation report

## Stop-gate result

**Pass.** PR-C is independently green and may hand off to PR-D. INV-1 is demonstrated across both
adapters, production postprocessing, worker evidence, the real AOT MCP boundary, and preserved RC.1
characterization.

## Results

| Check | Command | Result |
|---|---|---|
| Focused PR-C | isolated PR-C host with `dotnet run --project benchmarks/rc2-regression -- --pr-c` | Pass; 22/22; D9/D19 plus cumulative PR-B D12 |
| PR-A characterization | same project with `--characterization` | Pass; 32/32 against preserved RC.1 behavior |
| Cumulative expected-red | same project with `--regression` and isolated PR-B-compatible host | Expected exit 1; D9, D19, and five D12 checks green; eight later-stage assertions remain red |
| Production unit gate | `dotnet run --project benchmarks/l0-gate -- --unit-only` | Pass; exit 0; `L0_GATE_OK` |
| Worker checks | four `node --check` calls plus access evidence selftest | Pass; `ACCESS_EVIDENCE_SELFTEST_OK` |
| Fast gate | `scripts/run-l0-fast.ps1` with repository `OCCAM_HOME` | Pass; exit 0; `L0_GATE_FAST_OK` |
| Native AOT | isolated win-x64 publish to `artifacts/rc2-prc-host` | Pass; focused suite exercised the published host |
| Docs and hygiene | `check-docs`, `git diff --check`, frozen-evidence diff | See final stage audit below |

Focused access coverage includes public authentication prose, OpenID identity documentation, a real
blocking login UI, a public login widget with usable content, a login-like requested path, a redirect to
login, HTTP 401 plus authentication challenge, deterministic classification, and redacted stable codes.

## AOT artifact

- Path: `artifacts/rc2-prc-host/OccamMcp.Core.exe`
- Size: 38,251,008 bytes
- SHA-256: `383b581177ac137939dfe1e218d55771b946bab010512fa4cb872ca588df7147`
- Purpose: local isolated PR-C validation only; not a release and not an RC.1 replacement.

## Performance and warnings

Classification is pure local work. It adds no request and no hosted-model dependency. Evidence collection
reuses the existing probe sample or loaded DOM; terminology input is capped to 65,536 characters. The fast
live smoke passed without a material behavioral or latency regression.

Build/publish emitted four pre-existing nullable warnings in
`Knowledge/MaterializedProvenanceResolver.cs`. That unrelated file is unchanged; no new AOT or trimming
warning was emitted by PR-C.

## Stop-policy audit

- Frozen characterization did not regress.
- Real login controls remain true positives.
- D9/D19 assertions were not weakened; they now exercise the shared production rule.
- No breaking field removal, new tool, frozen evidence edit, or RC.1 artifact overwrite occurred.
- PR-D through PR-F failures remain visible and correctly owned.

Result: proceed only to the separately scoped PR-D implementation after the final docs/hygiene checks pass.
