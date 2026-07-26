# RC.2 release artifacts

## Policy

- Large Native AOT binaries remain under gitignored `artifacts/` unless packaging policy requires
  otherwise.
- `artifacts/rc2/manifest.json` lists **only actually generated files** in `artifacts[]`.
- Pending RID builds are recorded under `pendingNativeOsBuilds` and must not be treated as present.

## Built on this host (Windows x64)

| Field | Value |
|---|---|
| RID | win-x64 |
| File name | `OccamMcp.Core.exe` |
| Relative path | `artifacts/rc2/win-x64/OccamMcp.Core.exe` |
| Size | 38,630,400 bytes |
| SHA-256 | `184d6e7ce8024339eb560f7af91bb3860174c75725712b19b59c1d73202fdaff` |
| Build command | `dotnet publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r win-x64 -o artifacts/rc2/win-x64` |
| Timestamp (UTC) | 2026-07-22T21:57:37.6270610Z |
| Runtime requirements | Windows x64; `OCCAM_HOME` with Node workers; Playwright Chromium when browser backend is used |
| Known limitations | Gitignored; not a committed release asset by default |

## Pending native-OS builds

| RID | File name | Build command | Blocker |
|---|---|---|---|
| linux-x64 | `OccamMcp.Core` | `dotnet publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r linux-x64 -o artifacts/rc2/linux-x64` | Cross-OS Native AOT unsupported from Windows |
| osx-arm64 | `OccamMcp.Core` | `dotnet publish src/FFOccamMcp.Core/FFOccamMcp.Core.csproj -c Release -r osx-arm64 -o artifacts/rc2/osx-arm64` | Cross-OS Native AOT unsupported from Windows |

Observed local failure text:

```text
error : Cross-OS native compilation is not supported.
```

## Manifest

Machine-readable source of truth after a local publish:

```text
artifacts/rc2/manifest.json
```

Update the manifest on each remote RID publish by appending a real `artifacts[]` entry with size and
SHA-256, and removing the matching `pendingNativeOsBuilds` row.

## Related measurement artifacts (gitignored)

| Path | Purpose |
|---|---|
| `artifacts/rc2/soak-report.json` | Local soak measurements |
| `artifacts/rc2/gate-logs/` | Captured unit/docs/fast/full gate logs |
