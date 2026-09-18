# Testing evidence

This directory holds **artefacts**, not prose. Every claim about a platform in the repository should
resolve to a file here, or it should not be made.

## Contents

| Path | What it is |
|---|---|
| [`RESULTS.md`](RESULTS.md) | Cross-platform summary table with per-platform numbers and an explicit "not verified" section |
| [`install-failures.md`](install-failures.md) | Clean-VM / agent install failure catalog → CI regressions |
| [`status.md`](status.md) | Which machines were reachable during the run, and which were not |
| [`canary-vectors.json`](canary-vectors.json) | Canonical proof-of-read test vectors (PROBE_PROTOCOL.md §9) |
| `macos-arm64/`, `linux-x64/`, `windows-x64/` | Raw per-platform logs |

Per-platform directory layout:

| File | Content |
|---|---|
| `runtime-info.txt` | OS, kernel, CPU, RAM, SDK version |
| `build.log` | `dotnet restore` + `dotnet build -c Release`, with exit codes and wall time |
| `test.log` | `dotnet test --collect:"XPlat Code Coverage"` output |
| `coverage-summary.txt` | Line/branch coverage for the namespace under test and for the whole assembly |
| `canary-smoke.log` | `canary selftest` and `canary smoke` transcripts |
| `canary-vectors.log` | Semantic and byte-identity checks of the published vectors |
| `aot-publish.log` | `dotnet publish -c Release -r <rid>` |
| `binary-size.txt`, `hash.txt` | AOT binary size, file type and SHA-256 |
| `cascade-selftest.log` | `cascade selftest` marker |
| `cascade-live-smoke.log` / `.jsonl` | Live 3-URL cascade with per-URL latency |
| `canary-aot.log` | The AOT binary re-running all three canary verbs |

## Two test layers, different purposes

This repository has two verification systems, and conflating them produces misleading coverage
claims.

**`tests/OccamMcp.Core.Tests` (xunit).** Unit, integration and property-based tests over
deterministic code. Fast (~350 ms), coverage-instrumented, runs everywhere with no network, no
browser and no Node. This is what the coverage numbers in `RESULTS.md` measure.

**`benchmarks/l0-gate` (console runner).** The pre-existing integration gate: live extraction
against real URLs, worker spawning, browser pool, failure taxonomy. It asserts by printing markers
such as `L0_GATE_OK`. It needs `npm ci` and Playwright Chromium, and it is not
coverage-instrumented — so the ~300 files it exercises show as uncovered in a Cobertura report.

A single blended coverage percentage across both layers would be dishonest in either direction:
it understates what is verified and overstates what is unit-tested. `RESULTS.md` therefore reports
the two scopes separately.

## Re-running

See [`RESULTS.md` §7](RESULTS.md#7-reproducing-this). The runner scripts used for the recorded run
live in `scripts/testing/`:

| Script | Purpose |
|---|---|
| `run-platform.sh <label> <rid>` | Full Unix leg: runtime info, build, test + coverage, canary verbs, AOT publish, binary hash |
| `run-platform.ps1 [-PlatformLabel] [-Rid]` | Same leg on Windows |
| `verify-vectors.sh <label>` | Sentinel derivation only: semantic re-derive plus a SHA-256 comparison of the emitted file |
| `coverage-summary.ps1 -Path <cobertura>` | Reduces a Cobertura report to the two-scope summary committed here |

These are maintainer tooling for evidence collection, not part of the shipped `occam` CLI. The
checks they wrap *are* shipped — `occam canary selftest`, `vectors` and `smoke` all run from the
published binary, which is what makes the evidence reproducible by someone else.
