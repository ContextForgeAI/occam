# Install — start here (agents & operators)

> **If you are an automated agent:** run **only** the canonical command for your OS below.
> Do **not** edit `FFOccamMcp.Core.csproj`, do **not** install .NET 8, do **not** run
> `npx @ff-occam/mcp`, and do **not** invent alternate install paths.

**Repo:** [https://github.com/ContextForgeAI/occam](https://github.com/ContextForgeAI/occam)

**Requirements:** Node.js **20+**. No .NET SDK on the install machine.

**Current release:** `1.0.0-rc.2` (GitHub Release tag `v1.0.0-rc.2`).

---

## Canonical install (one path)

### Linux / macOS

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

### Windows (PowerShell)

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

Optional env (same on all platforms):

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_SETUP` | `auto` when non-interactive | `auto` or `manual` onboard |
| `OCCAM_HOST` | `hermes` | `hermes` or `cursor` connection snippet |
| `OCCAM_INSTALL_DIR` | `~/.local/share/ff-occam` | Install root |
| `OCCAM_VERSION` | `1.0.0-rc.2` | Release version |

Example (Cursor, non-interactive):

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh \
  | OCCAM_SETUP=auto OCCAM_HOST=cursor bash
```

```powershell
$env:OCCAM_SETUP = "auto"
$env:OCCAM_HOST = "cursor"
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

What the bootstrap does:

1. Downloads `ff-occam-<ver>-<rid>.tar.gz` + manifest from GitHub Releases  
2. Verifies SHA-256  
3. Runs doctor (`--skip-build`) — npm workers + Playwright  
4. Runs `hermes-smoke.mjs`  
5. Prints an MCP connection snippet  

---

## Verify

```bash
export OCCAM_HOME="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
node "$OCCAM_HOME/scripts/hermes-smoke.mjs"
```

```powershell
$env:OCCAM_HOME = if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else { Join-Path $env:USERPROFILE ".local\share\ff-occam" }
node "$env:OCCAM_HOME\scripts\hermes-smoke.mjs"
```

Expect **exit 0** and **15** `occam_*` tools.

---

## Wire MCP (after install)

Use the snippet printed by bootstrap, or:

| Field | Value |
|-------|-------|
| Command | `node` |
| Args | `["$OCCAM_HOME/scripts/launch-mcp-host.mjs"]` |
| Env | `OCCAM_HOME=<install root>` |

Do **not** put LLM API keys in Occam's env.

---

## Do not

| Wrong | Why |
|-------|-----|
| `npx @ff-occam/mcp` | **Not** part of this RC |
| `npm ci` / `npm run bootstrap` at repo root | Does not exist — doctor installs workers |
| Bare `git clone` without .NET 10 SDK | Source only — no AOT binary |
| `git clone` + `doctor --skip-build` without a release binary | Fails — no `OccamMcp.Core` |
| Edit `TargetFramework` to net8.0 | Must stay `net10.0` |

---

## Advanced / contributors

### Git clone + build (.NET 10 SDK required)

```bash
git clone https://github.com/ContextForgeAI/occam.git
cd occam
export OCCAM_HOME="$(pwd)"
./scripts/occam-doctor.sh
node scripts/hermes-smoke.mjs
```

Windows: `.\scripts\occam-doctor.ps1`.

### Manual tarball (air-gap / mirror)

Download both assets for your RID from the GitHub Release, then:

```bash
INSTALL_DIR="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
mkdir -p "$INSTALL_DIR"
tar -xzf ff-occam-1.0.0-rc.2-<rid>.tar.gz -C "$INSTALL_DIR" --strip-components=1
export OCCAM_HOME="$INSTALL_DIR"
bash scripts/occam-doctor.sh --skip-build
node scripts/hermes-smoke.mjs
```

Expected asset names:

```text
ff-occam-1.0.0-rc.2-linux-x64.tar.gz
ff-occam-1.0.0-rc.2-linux-x64-manifest.json
ff-occam-1.0.0-rc.2-osx-arm64.tar.gz
ff-occam-1.0.0-rc.2-osx-arm64-manifest.json
ff-occam-1.0.0-rc.2-win-x64.tar.gz
ff-occam-1.0.0-rc.2-win-x64-manifest.json
```

---

## More

- [docs/getting-started.md](docs/getting-started.md)
- [docs/troubleshooting.md](docs/troubleshooting.md)
- [MCP_API_SPEC.md](MCP_API_SPEC.md)

---

## Maintainer: publish a GitHub Release

Operators need assets at  
`https://github.com/ContextForgeAI/occam/releases/download/v<ver>/`.

On tag `v*` matching SemVer, `.github/workflows/occam-release.yml` builds and uploads
linux-x64, osx-arm64, and win-x64 tarballs + manifests.

```bash
git tag v1.0.0-rc.2 <main-sha>
git push origin v1.0.0-rc.2
```
