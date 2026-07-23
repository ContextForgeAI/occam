# Compatibility regression harness

This project freezes historical defect surfaces and asserts the current production
contracts for access classification, focus, budget projection, and related
semantics. It is intentionally separate from `benchmarks/l0-gate`.

Architecture notes: [docs/architecture/semantic-contract.md](../../docs/architecture/semantic-contract.md).

```powershell
dotnet run --project benchmarks/rc2-regression -c Release -- --characterization
dotnet run --project benchmarks/rc2-regression -c Release -- --spikes
dotnet run --project benchmarks/rc2-regression -c Release -- --regression
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-b
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-c
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-d
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-e
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-f
dotnet run --project benchmarks/rc2-regression -c Release -- --pr-g
```

- `characterization` must exit 0. Offline cases freeze historical defect surfaces through
  Legacy\* adapters. Live MCP D12 boundary cases expect the current `occam_digest.urls`
  `oneOf` schema and typed `invalid_arguments` for empty/mixed arrays.
- `spikes` must exit 0 and prints bounded measurements for test-only prototypes.
- `regression` must exit 0 after the full PR-B…PR-G contract set (lifecycle identity present).
- `pr-b` … `pr-g` are cumulative stop gates for earlier stages.
- `pr-f` adds the public semantic envelope (transport/usability/access/focus/completeness/verdict)
  while keeping legacy aliases.

The D12 boundary cases launch the published stdio host resolved by the normal Occam binary order.
Publish the host before running the suite. Set `OCCAM_RC2_HOST` to validate an isolated candidate.
All other cases use local fixtures and make no network requests.

Fixtures are synthetic reductions. They contain no credentials, cookies, authorization values,
private URLs, embeddings, or hosted-model dependencies. See
[docs/maintenance/FIXTURE_SOURCES.md](../../docs/maintenance/FIXTURE_SOURCES.md).
