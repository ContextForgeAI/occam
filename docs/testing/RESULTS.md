# Cross-platform verification — proof-of-read canary

Every row below was produced by running the commands on that machine. Nothing is inferred, and
nothing marked ✅ is a prediction. Raw logs live beside this file, one directory per platform.

**Run date:** 2026-09-16 (UTC)
**Scope:** `src/FFOccamMcp.Core` (`OccamMcp.Core.Canary`, `OccamMcp.Core.Exam`, `OccamMcp.Core.Cascade`) and `tests/OccamMcp.Core.Tests`
**Runner scripts:** `scripts/testing/run-platform.sh` (Unix), `scripts/testing/run-platform.ps1` (Windows)

---

## 1. Summary

| Platform | SDK | Build `-c Release` | Warnings | Tests | Canary selftest | Exam selftest | Cascade selftest | Cascade live (3 URL) | Vectors | Catalogue | HTTP smoke | Native AOT | AOT re-verifies |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| macOS 26.6.2 arm64 | 10.0.301 | ✅ | 0 | ✅ 314/314 | ✅ 17/17 | ✅ 36/36 | ✅ | ✅ p50=1600 ms | ✅ 8/8, byte-identical | ✅ byte-identical | ✅ | ✅ 33.3 MB | ✅ |
| Ubuntu 24.04.4 x64 | 10.0.302 | ✅ | 0 | ✅ 314/314 | ✅ 17/17 | ✅ 36/36 | ✅ | ✅ p50=6468 ms | ✅ 8/8, byte-identical | ✅ byte-identical | ✅ | ✅ 34.4 MB | ✅ |
| Windows 11 x64 | 10.0.301 | ✅ | 0 | ✅ 314/314 | ✅ 17/17 | ✅ 36/36 | ✅ | ✅ p50=3475 ms | ✅ 8/8, byte-identical | ✅ byte-identical | ✅ | ✅ 40.1 MB | ✅ |

No test failed on any platform, and no platform-specific behaviour difference remains after the two
defects in §5 were fixed.

**Warnings** is `0` for both `src/FFOccamMcp.Core` and `tests/OccamMcp.Core.Tests`, with
`TreatWarningsAsErrors=true` on each — including under `dotnet publish -r <rid>`, where the ILC trim
and AOT analysers run. See ADR-0013's follow-up section for how the last six were closed.

## 2. Hosts

| | macOS | Linux | Windows |
|---|---|---|---|
| Label | `macos-arm64` | `linux-x64` | `windows-x64` |
| RID | `osx-arm64` | `linux-x64` | `win-x64` |
| Kernel / OS | Darwin 25.6.0, macOS 26.6.2 (25G83) | Linux 6.8.0-136-generic, Ubuntu 24.04.4 LTS | Windows 11 Pro, build 26200 |
| Machine | Mac mini (Macmini9,1), Apple Silicon | VM, Intel Core i5-14500HX | AMD Ryzen 7 255 |
| Cores | 8 | 16 (vCPU) | 8C/16T |
| RAM | 16 GB | ~21.5 GB | ~22.8 GB |
| .NET SDK | 10.0.301 | 10.0.302 | 10.0.301 |
| SDK install | `dotnet-install.sh` → `~/.dotnet` | `dotnet-install.sh` → `~/.dotnet` | pre-existing, machine-wide |
| Access | SSH `macmini` | SSH `hermesvm` | local |

The Linux host runs SDK **10.0.302** against 10.0.301 elsewhere. `global.json` pins
`rollForward: latestFeature`, so the mismatch is intentional and is itself part of what this run
verified: identical results across two SDK patch levels.

## 3. What each check proves

| Check | Command | Evidence file |
|---|---|---|
| Cascade facade selftest | `dotnet run … -- cascade selftest` | `*/cascade-selftest.log` |
| Cascade live smoke (3 URLs) | `scripts/testing/cascade-live-smoke.{ps1,sh}` | `*/cascade-live-smoke.log`, `*/cascade-live-smoke.jsonl` |
| Test suite + coverage | `dotnet test --collect:"XPlat Code Coverage"` | `*/test.log`, `*/coverage-summary.txt` |
| Canary verdict state machine | `occam canary selftest` | `*/canary-smoke.log` |
| Derivation matches the published vectors | `occam canary vectors --verify` | `*/canary-vectors.log` |
| Emitted vector file is byte-identical | `occam canary vectors --emit --out` + SHA-256 | `*/canary-vectors.log` |
| End-to-end HTTP round trip | `occam canary smoke` | `*/canary-smoke.log` |
| Exam grading, tiering, cache, hysteresis | `occam exam selftest` | `*/exam-selftest.log` |
| Task catalogue is byte-identical and LF-only | `occam exam tasks --out` + SHA-256 | `*/exam-selftest.log`, `*/exam-tasks.json` |
| Native AOT publish | `dotnet publish -c Release -r <rid>` | `*/aot-publish.log`, `*/binary-size.txt` |
| AOT binary behaves like the JIT build | AOT binary runs all three canary verbs + the exam selftest | `*/canary-aot.log` |

## 4. Numbers

### Tests

| Platform | Passed | Failed | Skipped | Duration |
|---|---|---|---|---|
| macOS arm64 | 314 | 0 | 0 | 621 ms |
| Linux x64 | 314 | 0 | 0 | 398 ms |
| Windows x64 | 314 | 0 | 0 | 395 ms |

`macos-arm64/test.log` is not in English. That host has a non-English system locale, so the .NET CLI
localises its summary line; the counts are the same 149 passed / 0 failed / 0 skipped. The log is
kept verbatim rather than re-run under `DOTNET_CLI_UI_LANGUAGE=en`, because an evidence artefact
should be what the machine actually printed.

### Coverage

Identical on all three platforms, which is itself a determinism signal:

| Scope | Line | Branch |
|---|---|---|
| `OccamMcp.Core.Canary.*` | **91.33 %** (748/819) | **75.48 %** (237/314) |
| `OccamMcp.Core.Exam.*` | **95.49 %** (529/554) | **83.19 %** (198/238) |
| Whole `OccamMcp.Core` assembly | 2.54 % (2138/84328) | 2.81 % (585/20826) |

The assembly-wide figure is low and is reported as-is rather than hidden behind a blended average.
The ~300 pre-existing files in this host are exercised by the integration gate
(`benchmarks/l0-gate`), which is not a coverage-instrumented unit-test runner. The ≥70 % target is
met for the code this work added; it is **not** met for the repository as a whole. See
[`../../docs/testing/README.md`](README.md) for what that means operationally.

### Native AOT

| Platform | Publish wall time | Binary | Size | SHA-256 |
|---|---|---|---|---|
| macOS arm64 | 49 s | Mach-O 64-bit arm64 | 33 280 048 B (31.7 MiB) | `b0a8801b0218d90523ae44a10006861d3c4150850f962a4be4aff93a4d107b15` |
| Linux x64 | 44 s | ELF 64-bit LSB PIE, stripped | 34 379 792 B (32.8 MiB) | `52def527330b16273c5af13521791963d5a58c530b4848e61978bad5205e0105` |
| Windows x64 | incremental | PE32+ | 40 055 296 B (38.2 MiB) | `d3b786f2d0450982a9ebcc36ad9c9a80778364dc7c458802bf7de73ef570f37d` |

Native AOT needed no extra system toolchain on any of the three hosts. Binaries are not reproducible
across machines and are not expected to be; the hashes identify these exact artefacts.

The Windows publish time is not comparable: that leg re-published into a warm output directory, so
`publish_wall_seconds=3` measures an incremental link, not a cold build. A cold Windows AOT publish
earlier in the same session took 64 s. Recorded rather than quietly dropped, since the other two
numbers are cold.

### Byte-identical emitted artefacts

Both files the CLI publishes as portable are byte-identical on all three platforms, from the JIT
build and from the Native AOT binary alike:

| Artefact | SHA-256 | Bytes |
|---|---|---|
| `canary-vectors.json` (`canary vectors --emit --out`) | `d0cc1b15096451f9c63d9c53d428f7b3f468800f3ac8e5cddbdbb838da0e50b5` | 1658 |
| `exam-tasks.json` (`exam tasks --out`) | `157b4ba4542d4444c3a9be58bc0a41e6b677c8aad1d373a5ebad3a40786b2e53` | 2024 |

## 5. Defects found by these runs

Both are the same class of bug, and both are exactly what the exercise was meant to catch: a check
that passes on the developer's machine while producing a non-portable artefact.

**1. Emitted JSON differed between Windows and Unix.** `Utf8JsonWriter` with `Indented = true`
defaults its newline to `Environment.NewLine`, so the file written on Windows used CRLF and the one
written on macOS/Linux used LF. Every sentinel matched — the semantic check passed everywhere — but
the file the protocol publishes as a cross-platform artefact was not byte-portable. Fixed by pinning
`JsonWriterOptions.NewLine = "\n"`, then re-emitting and comparing SHA-256 on all three platforms.

**2. `--emit` to stdout and `--emit --out` produced different bytes.** The payload already ends with
LF, and `Console.Out.WriteLine` appended `Environment.NewLine` on top — so a shell redirect gained a
byte the `--out` path did not have. For a tool whose entire purpose is byte-identical artefacts,
that asymmetry is a defect rather than a cosmetic difference. Fixed by writing the payload verbatim;
`exam tasks` gained `--out` for the same reason.

A related finding that is **not** a code defect, recorded because it will mislead the next person:
on Windows a PowerShell `>` redirect re-encodes the stream (the 1658-byte vector file came out as
3432 bytes, UTF-16). `--out` exists so an artefact is written by the process that produced it.

## 5b. No regression in the existing gate

The pre-existing integration gate still passes on the local Windows host with this work applied:

```
$env:OCCAM_HOME = (Get-Location).Path; $env:OCCAM_PROFILE = "full"
dotnet run --project benchmarks/l0-gate -c Release -- --fast
→ L0_GATE_UTF8_OK  L1A_TOKEN_OK  L1_FAILURE_TAXONOMY_OK  L2_MEDIA_REFS_OK
  L8_AGENT_FIRST_OK  L0_GATE_FAST_OK
```

The MCP tool surface includes the cascade facade. `scripts/check-public-mcp-contract.mjs` computes
`schemaFingerprint = 520bd00bcc42…` (`PUBLIC_MCP_CONTRACT_OK`), matching
`corpora/public-mcp-schema-fingerprint.txt` after `occam` landed (16-tool catalog). Regenerate with
`OCCAM_PROFILE=full` and `node scripts/check-public-mcp-contract.mjs --write-fingerprint` against a
freshly published binary.

Two false alarms are worth recording, because both would mislead the next person who runs this
locally:

1. **The fingerprint corpus is keyed to `OCCAM_PROFILE=full`.** Running the check under the default
   `reader` profile (9 of 16 tools) produces a different, legitimately different fingerprint and
   reports a mismatch. CI sets the profile explicitly; a local run must too.
2. **The check reads a *published* binary, not the build output.** An eleven-day-old binary at
   `src/FFOccamMcp.Core/bin/Release/net10.0/win-x64/publish/` was what the first run measured. Run
   `dotnet publish -c Release -r <rid>` before trusting a fingerprint result.

### Cascade live smoke (2026-09-16, n=3 URLs)

Corpus: `https://example.com`, `https://nginx.org/`,
`https://developer.mozilla.org/en-US/docs/Web/HTTP/Status/404`. Marker: `CASCADE_LIVE_SMOKE_OK`.
Wall-clock includes `dotnet run` process overhead. Artefacts: `*/cascade-live-smoke.log` + `.jsonl`.

| Platform | ok | p50 ms | p95 ms | mean ms | min–max ms | Backend used |
|---|---|---|---|---|---|---|
| macOS arm64 | 3/3 | 1600 | 1600 | 1579 | 1312–1826 | `node_readability_turndown` (HTTP) |
| Linux x64 | 3/3 | 6468 | 6468 | 6706 | 5692–7959 | `browser_playwright` (HTTP miss → browser fallback) |
| Windows x64 | 3/3 | 3475 | 3475 | 3690 | 3158–4438 | `node_readability_turndown` (HTTP) |

n=3 is too small for a stable p95 — the script's percentile formula is reported honestly; with three
samples p50 and p95 coincide on the middle value. Linux resolved via browser on every URL in this
run; the cascade still returned `ok:true` — evidence that browser fallback works, not that Linux HTTP
matched macOS/Windows latency.

An earlier Windows attempt against `https://www.w3.org/TR/PNG/` returned typed `http_403` — a site
refusal, not a cascade-engine failure. That URL was dropped from the corpus.

## 6. Explicitly not verified

Listed so nothing here reads as a broader claim than it is.

| Item | Status | Why |
|---|---|---|
| Cascade `selftest` (no workers) macOS/Linux/Windows | ✅ `CASCADE_SELFTEST_OK` | `*/cascade-selftest.log` |
| Cascade live smoke 3 URLs × 3 OS | ✅ `CASCADE_LIVE_SMOKE_OK` | See § cascade live table; Linux via browser fallback. |
| Capability exam administered end-to-end against a model | ❌ not measured | Offline harness + mock pipelines verified (`EXAM_HARNESS_SELFTEST_OK`, `H2_PIPELINE_OK`, `H3_PIPELINE_OK`); no LLM cells. |
| H2 / H3 experiments | ❌ not run against models | Mock scorecards in `docs/research/results/`; see `docs/research/exam-harness.md`. |
| Full `benchmarks/l0-gate` integration gate on remote hosts | ❌ not run | Needs `npm ci` + Playwright Chromium on each host; that is a system-level install and was out of scope for this run. |
| Full `benchmarks/l0-gate` (all tiers) locally | ❌ not run | Only the `--fast` tier was run; the full gate does live extraction across the whole smoke corpus. |
| `dotnet format --verify-no-changes` repo-wide | ⚠️ not clean | `.editorconfig` was added in this pass, so the pre-existing tree now has a style baseline it was never written against: **138 files** would be reformatted (`dotnet format src/FFOccamMcp.Core --verify-no-changes` → exit 2). New code verifies clean (exit 0). Mass-reformatting 138 working files without tests covering them was out of scope. |
| `TreatWarningsAsErrors` repo-wide | ⚠️ partial | Enabled with **0 warnings** on `src/FFOccamMcp.Core` and `tests/OccamMcp.Core.Tests`, including under AOT publish. Not yet enabled on `benchmarks/l0-gate` (1 warning) or the three benchmark projects. |
| Dynamic tool-surface changes mid-session | ❌ not implemented | The surface is fixed when the MCP server is built. A tier selects a profile at process start; `notifications/tools/list_changed` is not wired. |
| Linux arm64, FreeBSD, macOS x64 | ❌ not tested | No access to such a host. |
| BenchmarkDotNet baselines | ❌ not produced | No benchmark project exists yet. |

## 7. Reproducing this

On a Unix host:

```bash
curl -fsSL https://dot.net/v1/dotnet-install.sh | sh -s -- --channel 10.0 --install-dir ~/.dotnet
export PATH="$HOME/.dotnet:$PATH"
git clone <repo> && cd occam
dotnet test tests/OccamMcp.Core.Tests/OccamMcp.Core.Tests.csproj -c Release
dotnet run --project src/FFOccamMcp.Core -c Release -- canary selftest
dotnet run --project src/FFOccamMcp.Core -c Release -- canary vectors --verify docs/testing/canary-vectors.json
dotnet run --project src/FFOccamMcp.Core -c Release -- canary smoke
dotnet run --project src/FFOccamMcp.Core -c Release -- exam selftest
```

Expected markers on stdout: `CANARY_SELFTEST_OK`, `CANARY_VECTORS_OK checked=8`, `CANARY_SMOKE_OK`,
`EXAM_SELFTEST_OK`.
