## Summary
<!-- One-line description of what this PR does -->

## Type
- [ ] `feat` — new capability
- [ ] `fix` — bug fix
- [ ] `docs` — documentation only
- [ ] `refactor` — no behavior change
- [ ] `chore` — CI, build, tooling

## Checklist
- [ ] Gate passes: `.\scripts\run-l0-fast.ps1` (or `dotnet run --project benchmarks\l0-gate`)
- [ ] Docs updated if tool params / failure codes / env vars changed
- [ ] No new `if (host === ...)` branches in workers
- [ ] No file cache added
- [ ] Commit message follows convention: `feat(core): ...`, `fix(router): ...`

## Gate Output
<!-- Paste L0_GATE_FAST_OK or L0_GATE_OK output -->

## Breaking Changes
<!-- List any breaking changes, or write "None" -->
