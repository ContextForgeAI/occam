# RC.2 Windows build fix

**Branch:** `fix/rc2-windows-build`
**Base commit:** `2cf5a53ebb898dcc2cac3c161ec404c1088b07a8`
**Date:** 2026-07-23
**Commits created by this work:** none yet
**Tag / publish:** not performed

## 1. Original failing command

Canonical Windows CI packaging entry point:

```powershell
.\scripts\ci-release-build.ps1 -Rid win-x64 -Version 1.0.0-rc.1
```

(Equivalent core packaging step: `.\scripts\build-release.ps1 -Rid win-x64 -Version 1.0.0-rc.1`.)

## 2. Observed failure

`dotnet publish` + tarball staging often succeeded when no host was running. The **end-to-end Windows
release path was still broken** in the pre-publish cleanup step that `ci-release-build` always invokes:

```text
node scripts/lib/stop-occam-processes.mjs
```

Evidence before the fix (live `OccamMcp.Core.exe` pid 31248):

| Check | Result |
|---|---|
| `publishExePath()` | `…\publish\FFOccamMcp.Core.exe` (**missing**; actual AssemblyName is `OccamMcp.Core`) |
| `listOccamHostProcesses()` | `[]` while `OccamMcp.Core` was running |
| `stopOccamHostProcesses()` | `stopped=[]`, process left alive |
| CLI main | **absent** — `node stop-occam-processes.mjs` loaded exports and exited 0 without stopping anything |

Secondary packaging gaps (not a failed `dotnet publish`, but incomplete Windows release confirmation):

- `.github/workflows/occam-release.yml` had **no Windows job** (linux + macOS only).
- `INSTALL.md` documented only a linux-x64 manual `build-release.ps1` example.

## 3. Root cause

Categories:

1. **Incorrect / stale host binary name** in `scripts/lib/stop-occam-processes.mjs` (legacy
   `FFOccamMcp.Core.exe` only; current publish output is `OccamMcp.Core.exe`).
2. **Missing CLI entrypoint** — CI/scripts called the module as a program, but there was no `main()`.
3. Combined effect: pre-publish unlock is a no-op → **file-lock / stale-host race** on Windows rebuilds
   when an Occam host holds the publish binary; orphan cleanup for packaged smoke also fails.

Not caused by AOT config, RID, VERSION mismatch, or missing worker copy logic in `build-release.mjs`.

## 4. Files changed

| Path | Change |
|---|---|
| `scripts/lib/stop-occam-processes.mjs` | Prefer `OccamMcp.Core`, keep legacy `FFOccamMcp.Core`; add CLI `main()` |
| `scripts/lib/stop-occam-processes.selftest.mjs` | New selftest for publish path preference / fallback |
| `.github/workflows/occam-release.yml` | Add `release-windows` job (build+verify only; no publish) |
| `INSTALL.md` | Document local Windows `ci-release-build.ps1` without claiming public upload |
| `docs/rc2/RC2_WINDOWS_BUILD_FIX.md` | This report |

## 5. Exact fix

- Resolve publish binary candidates in the same order as Level B layout helpers:
  `OccamMcp.Core` then legacy `FFOccamMcp.Core`.
- Match Windows process names for both executables.
- When executed directly, call `stopOccamHostProcesses(OCCAM_HOME || repoRoot)` and exit non-zero if
  the publish binary remains locked.
- CI: on tag / `workflow_dispatch`, run Windows `ci-release-build.ps1` + selftest; **do not** upload
  Windows assets (public Windows release remains unpublished / out of RC checklist).

## 6. Canonical Windows build command

```powershell
.\scripts\ci-release-build.ps1 -Rid win-x64 -Version 1.0.0-rc.1
```

Outputs:

```text
artifacts/releases/ff-occam-1.0.0-rc.1-win-x64.tar.gz
artifacts/releases/ff-occam-1.0.0-rc.1-win-x64-manifest.json
```

## 7. Required prerequisites

- Windows x64 host (Native AOT cannot be cross-compiled from another OS)
- .NET SDK 10.x
- Node.js ≥ 20
- PowerShell 5.1+
- `tar` available on PATH (Windows 10+ / Git)

## 8. Clean-build validation

1. Removed prior `artifacts/releases/ff-occam-1.0.0-rc.1-win-x64.*`, `.release-stage`, and
   `src/FFOccamMcp.Core/{bin,obj}`.
2. Re-ran `.\scripts\ci-release-build.ps1 -Rid win-x64 -Version 1.0.0-rc.1`.
3. Result: **exit 0**; `build-release: OK`; `verify-release-artifact: OK`; ~74 s wall time.
4. Fresh timestamps on tarball / exe confirmed newly generated outputs.

## 9. Package inventory summary

Level B stage / tarball contains:

- `OccamMcp.Core.exe`
- `VERSION` (= `1.0.0-rc.1`)
- `release-manifest.json` (`version`, `rid`, `nodeMajorMin`, `layout: level-b`)
- `workers/` (no `node_modules`; install post-extract)
- `scripts/` + `scripts/lib/`
- `profiles/`
- `skills/occam/` when present

Excluded: source trees, corpora, `validation/`, IDE dirs, PDBs, secrets.

## 10. Executable / package SHA-256

Measured after the clean rebuild on this host:

| Artifact | Size (bytes) | SHA-256 |
|---|---:|---|
| `ff-occam-1.0.0-rc.1-win-x64.tar.gz` | 13,689,521 | `22d3ba89f939205e2e5cbf4ece98b0a32ad254944c90bd243664e7a50045d9a3` |
| External manifest `sha256` field | — | matches tarball |
| `OccamMcp.Core.exe` (publish + packaged) | 38,630,400 | `9718570fedd32d0492d9b75a888851a1f83e75f8072534ccfa52caf1beb866ba` |

`VERSION` / host `version-surface` remain **`1.0.0-rc.1`**.

## 11. Smoke-test result

Extracted tarball → `npm ci --omit=dev` in `workers/` →
`node scripts/launch-mcp-host.mjs` with `OCCAM_HOME=<package root>`:

- `initialize` OK
- `tools/list` → **15** core tools
- exit path via launcher kill + stop helper
- Result: **SMOKE_EXIT=0**

## 12. Orphan-process check

After smoke + `node scripts/lib/stop-occam-processes.mjs`:

- `Get-Process -Name OccamMcp.Core` → **0**
- Helper reports `stillLocked=false`

Before the fix, a live `OccamMcp.Core` was invisible to the helper and survived stop attempts.

## 13. Remaining limitations

- Public Windows GitHub Release **upload is still out of scope** for the RC checklist (`INSTALL.md`).
- Windows CI job verifies packaging; it does not publish assets.
- Packaged workers still require post-extract `npm ci` (unchanged Level B policy).
- No tag/publish of RC.2 was performed.

## 14. Confirmation

- No tag created.
- No release published.
- No push performed.
- Product version remains `1.0.0-rc.1`.
- Wording: Windows package **builds and verifies locally**; Windows release artifact **validated
  locally**; **public release not yet published**.
