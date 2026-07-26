# PR-B digest MCP boundary validation report

## Stop-gate result

**Pass.** PR-B is independently green and may hand off to PR-C. INV-3 is demonstrated at the pure
normalizer, real stdio MCP schema, handler, and service-boundary levels.

## Results

| Check | Command | Result |
|---|---|---|
| Focused PR-B | `OCCAM_RC2_HOST=artifacts/rc2-prb-host/OccamMcp.Core.exe dotnet run --project benchmarks/rc2-regression -c Release -- --pr-b` | Pass; 13/13; exit 0; final measured wall time 5,547 ms including build/startup |
| PR-A characterization | `dotnet run --project benchmarks/rc2-regression -c Release -- --characterization` | Pass; 32/32; exit 0 against the preserved RC.1 host |
| Cumulative expected-red | same project with `--regression` and isolated PR-B host | Expected exit 1; 5 D12 checks green; 10 unrelated checks remain `EXPECTED_RED` |
| Existing unit gate | `dotnet run --project benchmarks/l0-gate -c Release -- --unit-only` | Pass; exit 0; `L0_GATE_OK` and component markers emitted |
| Existing fast gate | `scripts/run-l0-fast.ps1` with `OCCAM_HOME` set to the repository | Pass; exit 0; `L0_GATE_FAST_OK` |
| Docs | `node scripts/check-docs.mjs` | Pass; 58 documents, 316 local links, 42 anchors, 15 core tools |
| Native AOT | `dotnet publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r win-x64 -o artifacts/rc2-prb-host` | Pass; exit 0 |
| Diff hygiene | `git diff --check` | Pass |
| Frozen evidence | `git diff -- validation/evidence/rc1` | Empty; no tracked RC.1 evidence change |

The focused suite covers native order, legacy JSON-array and delimiter compatibility, mixed and nested
rejection, empty and wrong top-level shapes, oversized input, truthful `oneOf`, and typed MCP results
for empty/mixed/nested/malformed calls. The boundary cases make no network requests.

## Red-test accounting

The two original PR-A D12 red assertions are green:

1. `tools/list` accepts native arrays and legacy strings.
2. Empty native array returns typed `invalid_arguments`.

Three additional negative MCP-boundary assertions added by PR-B are also green: mixed array, nested
array, and malformed legacy string. The still-red tests remain exactly in their later-stage scopes:
D9, D19, D15 (two), D11, D17, D10 (two), C10b, and semantic-result separation.

## AOT artifact

- Path: `artifacts/rc2-prb-host/OccamMcp.Core.exe`
- Size: 38,201,856 bytes
- SHA-256: `a0c97a71c8bea805b06d041c5a1b6d9fb69ba5962a2762c85c5ac344b0ab947a`
- Purpose: local isolated PR-B validation only; not a release and not an RC.1 replacement.

The repository support matrix is unchanged; no RID was removed. PR-B directly published and exercised
`win-x64`. Remote macOS/Linux validation remains a PR-H concern.

## Performance and warnings

The final focused run took 5,547 ms wall time, dominated by `dotnet run` and four stdio calls. The
normalizer itself is local, linear, capped at 256 entries / 65,536 characters, and adds no acquisition
or hosted-model work. No material performance regression was observed in the fast gate.

Four nullable warnings in `Knowledge/MaterializedProvenanceResolver.cs` appeared during the existing
gate build. That unrelated file is not changed by PR-B; no new AOT/trimming warning was emitted by the
digest schema adapter.

## Stop-policy audit

- Earlier characterization did not regress.
- Existing unit and fast gates passed.
- No breaking public removal was introduced.
- No frozen evidence or RC.1 artifact was modified.
- No focus, login, planner, semantic, or lifecycle assertion was weakened.
- No owner decision beyond the supplied array/string compatibility decision was required.

Result: proceed only to the separately scoped PR-C implementation.
