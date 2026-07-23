# Install — start here (agents & operators)

> **If you are an automated agent:** stop. Read this entire file before running any command.
> Do **not** edit `FFOccamMcp.Core.csproj`, do **not** install .NET 8, do **not** run
> `packages/occam-mcp/bin/occam-mcp.js` on a git clone. There is **no** root `npm run bootstrap`.

**Repo:** [https://github.com/ContextForgeAI/occam](https://github.com/ContextForgeAI/occam)

This host is **.NET 10 Native AOT** + **Node 20+** workers.

**RC distribution (`1.0.0-rc.2`):** Occam `1.0.0-rc.2` is distributed through GitHub release archives for:

- Linux x64
- macOS arm64
- Windows x64

npm packages, NuGet packages, and the VS Code extension are **not** part of this release candidate.
Contributor source builds remain documented separately.

---

## Pick your path

| Your machine | Do this |
|--------------|---------|
| **No repo checkout, no .NET 10 SDK** | Download Level B GitHub Release assets, extract, then doctor (below) |
| **Existing checkout, no .NET 10 SDK** | Install from published assets via `scripts/install.sh --from-url` or `get-ff-occam.sh` |
| **Git clone + .NET 10 SDK** | `./scripts/occam-doctor.sh` (publishes `OccamMcp.Core`) |
| **Git clone only, no SDK, no tarball** | **Will not work** — clone has source, not the AOT binary |

**Git clone does not include `OccamMcp.Core`.**  
`./scripts/occam-doctor.sh --skip-build` only works when a **release tarball** (or copied binary) already placed `OccamMcp.Core` under `OCCAM_HOME`.

---

## Installation from downloaded release assets (no checkout)

Use this on Hermes / production when you only have GitHub Release assets and **Node 20+**.

1. Download both assets for your RID (Hermes = `linux-x64`):

```text
ff-occam-<ver>-linux-x64.tar.gz
ff-occam-<ver>-linux-x64-manifest.json
```

Example for `v1.0.0-rc.2`:

```text
ff-occam-1.0.0-rc.2-linux-x64.tar.gz
ff-occam-1.0.0-rc.2-linux-x64-manifest.json
```

2. Extract the tarball, then run doctor from the extracted tree (scripts live **inside** the bundle):

```bash
INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
mkdir -p "$INSTALL_DIR"
tar -xzf ff-occam-1.0.0-rc.2-linux-x64.tar.gz -C "$INSTALL_DIR" --strip-components=1
export OCCAM_HOME="$INSTALL_DIR"
cd "$OCCAM_HOME"
bash scripts/occam-doctor.sh --skip-build
node scripts/hermes-smoke.mjs
```

Verify the SHA-256 in `ff-occam-*-manifest.json` against the tarball before extract when installing outside CI.

Do **not** run `node scripts/lib/release-install.mjs` unless that script is already on disk (checkout or extracted release). Without a checkout, use the downloaded GitHub Release assets above, or:

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

---

## Installation from an existing checkout

### Level B — published assets, no .NET SDK

From a clone that already contains `scripts/`:

```bash
cd /path/to/FFOccamMCP
export OCCAM_VERSION=1.0.0-rc.2
export OCCAM_RELEASE_BASE="https://github.com/ContextForgeAI/occam/releases/download/v${OCCAM_VERSION}"
export OCCAM_INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
bash scripts/get-ff-occam.sh
export OCCAM_HOME="$OCCAM_INSTALL_DIR"
cd "$OCCAM_HOME"
node scripts/hermes-smoke.mjs
```

Equivalent explicit install:

```bash
./scripts/install.sh --from-url "$OCCAM_RELEASE_BASE/ff-occam-${OCCAM_VERSION}-linux-x64.tar.gz"
```

`install.sh --from-url` calls `scripts/lib/release-install.mjs` from the **checkout** and verifies the adjacent `-manifest.json`.

### Level A — build from source (.NET 10 SDK)

```bash
git clone https://github.com/ContextForgeAI/occam.git /path/to/FFOccamMCP
cd /path/to/FFOccamMCP
export OCCAM_HOME="$(pwd)"
./scripts/occam-doctor.sh
node scripts/hermes-smoke.mjs
```

**.NET 8 cannot build this project** — you will see `NETSDK1045`. Install .NET 10 or use the tarball path.

---

## Do not

| Wrong | Why |
|-------|-----|
| `npm ci` / `npm run bootstrap` at repo root | **Does not exist** — workers install is inside `occam-doctor` |
| `packages/occam-mcp/bin/occam-mcp.js` on git clone | Exits immediately — use `scripts/occam-wrapper.sh` |
| `git clone` + `doctor --skip-build` without tarball | Doctor **fails** — no `OccamMcp.Core` |
| `bash scripts/get-ff-occam.sh` with no checkout and no downloaded script | Script is not on PATH — download assets or clone first |
| Edit `TargetFramework` to net8.0 | Doctor fails — must stay `net10.0` |
| `OPENROUTER_API_KEY` in Occam MCP `env` | Hermes LLM key — not the MCP host |

---

## Hermes MCP config

```yaml
mcp_servers:
  ff-occam:
    command: /path/to/install/root/scripts/occam-wrapper.sh
    env:
      OCCAM_HOME: /path/to/install/root
```

Reload MCP in Hermes after saving.

---

## Verify

```bash
export OCCAM_HOME=/path/to/install/root
node scripts/hermes-smoke.mjs
```

Expect **exit 0** and **15** `occam_*` tools.

---

## More

- [docs/getting-started.md](docs/getting-started.md)
- [corpora/occam-host-wizard-manifest.json](corpora/occam-host-wizard-manifest.json)
- `occam onboard --non-interactive --host-target hermes --json`

---

## Maintainer: publish a GitHub Release

Operators need release assets at
`https://github.com/ContextForgeAI/occam/releases/download/v<ver>/`. CI builds and publishes them on
**tag push**.

### RC operator sequence (`v1.0.0-rc.2`)

Do **not** publish npm, NuGet, or VSIX as part of this RC.

1. Push the release branch and wait for all PR checks.
2. Merge into `main`.
3. Record the exact merged `main` SHA.
4. Create tag `v1.0.0-rc.2` at that SHA (do not retag an unmerged tip).
5. Push the tag.
6. Verify Linux x64 and macOS arm64 GitHub Release assets (tarball + manifest).
7. Announce only after both platforms pass.

### One-time setup

1. Enable GitHub Actions for the repository.
2. Allow `.github/workflows/occam-release.yml` its declared `contents: write` permission. It uses the
   job's `GITHUB_TOKEN`; no custom release secret is required.
3. On each SemVer tag `v*`, CI builds both `linux-x64` and `osx-arm64` release assets.

### Publish v1.0.0-rc.2 (CI)

```bash
# Only after merge to main — use the recorded main SHA
git tag v1.0.0-rc.2 <merged-main-sha>
git push origin v1.0.0-rc.2
```

Workflow `.github/workflows/occam-release.yml`:

1. Accepts SemVer tags matching `v*` (no build metadata) and derives the product version by removing `v`.
2. Builds the `linux-x64` and `osx-arm64` Native AOT hosts with that version embedded in the binary.
3. Verifies the tag-derived version against the bundle, manifests, `VERSION`, and executable host.
4. Publishes the tarball and SHA-256 manifest to GitHub Releases.
5. Marks **any** SemVer with a non-empty prerelease component as a GitHub prerelease. Only tags with
   no prerelease component (for example `v1.1.0`) are stable releases. Tags with SemVer build
   metadata (`+…`) are rejected before publish.

Expected assets:

```text
ff-occam-1.0.0-rc.2-linux-x64.tar.gz
ff-occam-1.0.0-rc.2-linux-x64-manifest.json
ff-occam-1.0.0-rc.2-osx-arm64.tar.gz
ff-occam-1.0.0-rc.2-osx-arm64-manifest.json
ff-occam-1.0.0-rc.2-win-x64.tar.gz
ff-occam-1.0.0-rc.2-win-x64-manifest.json
```

npm packages, NuGet packages, and the VS Code extension remain **out of scope** for this RC checklist.

### Manual fallback (no CI)

```powershell
.\scripts\build-release.ps1 -Rid linux-x64 -Version 1.0.0-rc.2
```

Upload `artifacts/releases/ff-occam-1.0.0-rc.2-linux-x64.tar.gz` + `-manifest.json` to the matching GitHub Release.

Windows x64 packages use the same Level B machinery (also produced by `occam-release` on tag):

```powershell
.\scripts\ci-release-build.ps1 -Rid win-x64 -Version 1.0.0-rc.2
# → artifacts/releases/ff-occam-1.0.0-rc.2-win-x64.tar.gz
# → artifacts/releases/ff-occam-1.0.0-rc.2-win-x64-manifest.json
```
