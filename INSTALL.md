# Install — start here (agents & operators)

> **If you are an automated agent:** run **only** the canonical command for your OS below.
> Do **not** edit `FFOccamMcp.Core.csproj`, do **not** install .NET 8, do **not** run
> `npx @ff-occam/mcp`, and do **not** invent alternate install paths.

**Product:** Occam (Occam MCP)  
**Repo:** [https://github.com/ContextForgeAI/occam](https://github.com/ContextForgeAI/occam)  
**Docs site:** [https://contextforgeai.github.io/occam/](https://contextforgeai.github.io/occam/)

**Requirements:** Node.js **20+** (installer also checks Homebrew locations `/opt/homebrew/bin` and `/usr/local/bin` when `node` is not already on `PATH`). No .NET SDK on the install machine.

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

### Supported GA install path

**GA install = GitHub Release tarball + bootstrap scripts below.** Manual tarball + manifest is also GA.

| Channel | GA? | Notes |
|---------|-----|-------|
| Bootstrap scripts (`get-ff-occam.*`) | **Yes** | Recommended |
| Manual tarball + `*-manifest.json` | **Yes** | Air-gap / mirror |
| `git clone` + doctor | Build path | Requires .NET 10 SDK |
| `npx @ff-occam/mcp` | **No** | Not a supported 1.0 install channel |
| Cosign `.bundle` on Releases | **Not enforced** | May exist as metadata; **no shipped path verifies Cosign** — integrity is SHA-256 vs manifest only |

### What the bootstrap does

1. Downloads `ff-occam-<ver>-<rid>.tar.gz` + `ff-occam-<ver>-<rid>-manifest.json` from GitHub Releases  
2. Verifies **SHA-256** of the archive against the manifest (**not** Cosign)  
3. Extracts into `OCCAM_INSTALL_DIR` (default `~/.local/share/ff-occam`)  
4. Runs **doctor** (`--skip-build`) — npm workers + Playwright (quiet by default)  
5. Verifies the Occam host (`verify-install` + smoke) — expect **15** `occam_*` tools  
6. Writes operator defaults to `~/.occam/onboard.json` (no second `OCCAM_HOME` prompt)  
7. Runs **`occam connect`** — detects AI/MCP hosts; one host auto-connects; multiple hosts confirm first (or `OCCAM_CONNECT_ALL=1` for automation)  
8. Reports **Ready** only after host verification — or **Installed** / **Almost ready** / **Action required** as appropriate  

Default output is quiet (~15–25 lines). Internals: `OCCAM_VERBOSE=1`.

**What install mutates:** install tree under `OCCAM_HOME`, `~/.occam/onboard.json`, signing key on first host start (`~/.occam/keys/`), host MCP configs (+ backups), Playwright cache. Removing the install directory alone does **not** remove all `~/.occam/` state or host configs.

Human walkthrough: [docs/quick-start.md](docs/quick-start.md) · Host tiers: [docs/mcp-hosts.md](docs/mcp-hosts.md) · Safety: [docs/trust/installation-safety.md](docs/trust/installation-safety.md)

Optional env (compatibility — same on all platforms):

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_SETUP` | `auto` (when unset) | `auto` \| `manual` \| `ask` — default install never prompts; `ask` shows menu only on a true interactive TTY |
| `OCCAM_CONNECT_ALL` | unset | `1` — non-interactive installs may configure every detected Tier-A host; without it, multiple hosts are left for an explicit confirm / `occam connect` |
| `OCCAM_VERBOSE` | unset | `1` — show doctor/smoke/connect internals during install |
| `OCCAM_HOST` | (none) | Legacy preference for the **fallback** connection snippet only (`hermes` or `cursor`) — not a phantom pre-selected host |
| `OCCAM_INSTALL_DIR` | `~/.local/share/ff-occam` | Install root |
| `OCCAM_VERSION` | `1.0.0-rc.2` | Release version |

`OCCAM_HOST` does **not** replace `occam connect`. Prefer letting connect detect and configure validated hosts.

Interactive setup menu (Enter = Auto): set `OCCAM_SETUP=ask`.

---

## Verify

```bash
export OCCAM_HOME="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
export PATH="$OCCAM_HOME/scripts:$PATH"
occam smoke
# or: node "$OCCAM_HOME/scripts/hermes-smoke.mjs"
```

```powershell
$env:OCCAM_HOME = if ($env:OCCAM_INSTALL_DIR) { $env:OCCAM_INSTALL_DIR } else { Join-Path $env:USERPROFILE ".local\share\ff-occam" }
$env:PATH = "$env:OCCAM_HOME\scripts;$env:PATH"
occam smoke
```

Expect **exit 0** and **15** `occam_*` tools.

Re-run host connection any time:

```bash
occam connect
```

---

## Connect your AI (normal path)

```bash
occam connect
```

- **Live validated** hosts are configured automatically when detected.  
- **Config validated** hosts need an explicit name, e.g. `occam connect --only vscode`.  
- **Assisted** hosts get paste guidance.  
- **Model runtimes** (Ollama, etc.) are reported but never registered — they are not MCP hosts.  

Statuses: **Ready**, **Almost ready** (restart the named app), **Action required** (trust/paste — not a broken install), **Not ready** (Occam itself failed to start).

Safety: unmanaged `ff-occam` entries are left alone; configs are backed up; writes are atomic; CI does not mutate desktop configs by default. Details: [docs/mcp-hosts.md](docs/mcp-hosts.md).

---

## Manual / generic MCP (advanced)

Only when connect cannot cover your client. Use the snippet printed as fallback, or:

| Field | Value |
|-------|-------|
| Command | `node` |
| Args | `["$OCCAM_HOME/scripts/launch-mcp-host.mjs"]` |
| Env | `OCCAM_HOME=<install root>` |

MCP server registration name: `ff-occam` (compatibility identifier — do not rename casually).

Do **not** put LLM API keys in Occam's env.

---

## Do not

| Wrong | Why |
|-------|-----|
| `npx @ff-occam/mcp` | **Not** a GA 1.0 install channel |
| Trust Cosign bundle alone | Installers do **not** verify Cosign; use SHA-256 manifest |
| `npm ci` / `npm run bootstrap` at repo root | Does not exist — doctor installs workers |
| Bare `git clone` without .NET 10 SDK | Source only — no AOT binary |
| `git clone` + `doctor --skip-build` without a release binary | Fails — no host binary |
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
occam connect
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
occam connect
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

- [Quick Start](docs/quick-start.md)
- [Getting started](docs/getting-started.md)
- [MCP hosts / connect](docs/mcp-hosts.md)
- [Troubleshooting](docs/troubleshooting.md)
- [MCP_API_SPEC.md](MCP_API_SPEC.md)

---

## Preview the documentation site (contributors)

```bash
python -m venv .venv-docs
# Windows: .\.venv-docs\Scripts\Activate.ps1
source .venv-docs/bin/activate
pip install -r docs/requirements.txt
mkdocs serve
```

Open http://127.0.0.1:8000/ — docs toolchain only; not required for product install.
