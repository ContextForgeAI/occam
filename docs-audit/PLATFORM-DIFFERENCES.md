# PLATFORM-DIFFERENCES (Wave 4 Phase 4I)

Central sweep of `OperatingSystem.Is*` / `RuntimeInformation` / RID / exe-suffix / shell branches in **shipped C#**. Worker (`.mjs`) and script (`.ps1`/`.sh`) platform splits are folded in from W4-F / W4-G reports.

## C# runtime platform branches (code-proven)

| Site | Windows | Unix (Linux/macOS/FreeBSD) | Capability difference? |
|------|---------|----------------------------|------------------------|
| `Receipts/ReceiptSigner.cs:86` (`RuntimeInformation.IsOSPlatform(Windows)`) | branch for key file handling | else-branch | **Verify in W4-E:** key-at-rest protection may differ per OS (DPAPI/file perms) — potential MISSING_SECURITY_SEMANTIC if only Windows path hardens |
| `Cli/OccamCliVerbs.cs:134` | `cmd.exe` to invoke `npx` (browser install) | direct `npx` | Behavioral: Windows shells out through `cmd.exe` (quoting surface); same capability, different exec path |
| `Workers/PlaywrightEnvironment.cs:54,75` | `%LOCALAPPDATA%\ms-playwright` | macOS `~/Library/Caches/ms-playwright`, Linux `~/.cache/ms-playwright` | Cache path only — packaging, not capability |
| `Session/SessionProfileHeaders.cs:230` | case-**insensitive** storageState path-containment check | case-**sensitive** | **Security-relevant:** containment check strictness differs by OS; confirm no bypass on case-insensitive FS (W4-D/E) |
| `Workers/WorkerProcessGroup.cs:119,153,199,224,312` | Win32 **Job Object** for process-tree kill | POSIX process-group kill (`IsLinux/IsMacOS/IsFreeBSD`) | Behavioral parity intended; cleanup mechanism differs. FreeBSD explicitly handled |

## Notable

- **FreeBSD** is explicitly a supported cleanup target (`WorkerProcessGroup.cs:312`) — broader than the win/mac/linux RID triple that ships as release tarballs (S3-12). Capability present in code, no released binary → PRODUCT_MISTAKEN_AS_INTERNAL candidate / RID gap.
- RID release matrix (S3-12): `linux-x64`, `osx-arm64`, `win-x64` ship; `osx-x64`, `linux-arm64`, FreeBSD do not — a **packaging** gap, but the *code* is platform-portable.
- `InvariantGlobalization=true` in csproj → culture-invariant string ops everywhere; no locale-dependent behavior by design.

## Reconciled from blind agents

| Diff | Capability impact | Evidence |
|------|-------------------|----------|
| stop-occam Win Name-eq (no root) vs POSIX `mentionsHost` bypass | Both name-wide; match logic differs; collateral kill (EF-049) | stop-occam-processes.mjs |
| get-ff-occam `.ps1` lacks node welcome/setup of `.sh` | Bootstrap UX/capability divergence | get-ff-occam.* |
| `ReceiptSigner.TryHardenPermissions` no-op on Windows | Key-at-rest hardening Unix-only | ReceiptSigner.cs:86-88 |
| Docker = linux-x64 only; release RIDs {linux-x64, osx-arm64, win-x64} | No osx-x64 / win-arm64 / linux-arm64 ships; npm advertises arm64 then rejects | W4-H |
| VectorizedHtmlScanner AVX2/AdvSimd/SSE2/scalar | Perf path only, same semantics | W4-B |
| doctor Linux-root `install-deps chromium` (CAP-946) | Linux-only doctor step | Wave3 + W4-G |
