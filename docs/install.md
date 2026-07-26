# Install

Canonical agent/operator install reference also lives at the repository root:
[`INSTALL.md`](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md).
This page is the documentation-site copy of the same happy path.

**Requirements:** Node.js **20+**. No .NET SDK on the install machine.  
**Current release:** `1.0.0-rc.2`

---

## Supported GA install path

**GA install = GitHub Release tarball + bootstrap scripts.** The bootstrap downloads `ff-occam-<ver>-<rid>.tar.gz`, verifies integrity, runs doctor, and connects your MCP host.

| Channel | GA? | Notes |
|---------|-----|-------|
| `get-ff-occam.sh` / `get-ff-occam.ps1` bootstrap | **Yes** | Recommended one-command path |
| Manual tarball + manifest from GitHub Releases | **Yes** | Air-gap / mirror installs |
| `git clone` + doctor (contributors) | Build path | Requires .NET 10 SDK |
| `npx @ff-occam/mcp` | **No** | Not a supported 1.0 install channel |
| Cosign `.bundle` on Releases | **Not enforced** | May exist as release metadata; **no shipped install path verifies Cosign**. Integrity check is **SHA-256 vs the release manifest only**. |

---

## One command

=== "Linux / macOS"

    ```bash
    curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
    ```

=== "Windows"

    ```powershell
    irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
    ```

## What the installer does

1. Downloads `ff-occam-<ver>-<rid>.tar.gz` + `ff-occam-<ver>-<rid>-manifest.json` from GitHub Releases  
2. Verifies **SHA-256** of the archive against the manifest (**not** Cosign)  
3. Extracts into `OCCAM_INSTALL_DIR` (default `~/.local/share/ff-occam`)  
4. Runs **doctor** (`--skip-build`) — npm workers, Playwright Chromium, host binary check (quiet by default)  
5. Verifies the Occam host — expect **15** core `occam_*` tools  
6. Writes onboard defaults → `~/.occam/onboard.json` (known install path; no re-prompt)  
7. Runs **`occam connect`** for live-validated AI/MCP hosts (one host auto; multiple confirm first)  
8. Reports **Ready** only after host verification (or Installed / Almost ready / Action required)

`OCCAM_VERBOSE=1` shows doctor/smoke internals. Human walkthrough: [Quick Start](quick-start.md) · Hosts: [MCP hosts](mcp-hosts.md)

## What install mutates

| Location | What gets written |
|----------|-------------------|
| `OCCAM_INSTALL_DIR` / `OCCAM_HOME` | Release tree: `scripts/`, `workers/`, AOT host binary |
| `~/.occam/onboard.json` | Operator defaults merged into every launcher invocation |
| `~/.occam/keys/signing-key.pem` | Minted on **first host start** (even when `OCCAM_RECEIPTS=off`) |
| Host MCP config files | `occam connect` registrations + `*.occam-bak` backups |
| Playwright browser cache | Chromium downloaded by doctor when needed |

**Uninstall honesty:** removing the install directory alone does **not** remove `~/.occam/`, host MCP configs, skill directories, or Playwright cache. See [Trust: installation safety](trust/installation-safety.md).

## Doctor

Doctor installs worker dependencies and verifies the host can start:

```bash
export OCCAM_HOME="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
export PATH="$OCCAM_HOME/scripts:$PATH"
occam doctor
# or: bash "$OCCAM_HOME/scripts/occam-doctor.sh" --skip-build
```

Re-run doctor after upgrades or when you see `workers_unavailable`.

## Connect

```bash
occam connect
```

- **Live validated** hosts are configured automatically when detected.  
- **Config validated** hosts need an explicit name, e.g. `occam connect --only vscode`.  
- Connect **backs up** before write, writes **atomically**, and does **not** overwrite unmanaged `ff-occam` entries unless you pass `--force`.  
- Connect makes **no network calls** for registration — it only touches local files and may start the local Occam server to verify it responds.

Details: [Connect](connect/index.md) · [MCP hosts](mcp-hosts.md)

## Verify

```bash
export OCCAM_HOME="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
export PATH="$OCCAM_HOME/scripts:$PATH"
occam smoke
occam connect
```

Expect **exit 0** and **15** core `occam_*` tools.

## Optional environment

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_SETUP` | `auto` | `auto` \| `manual` \| `ask` |
| `OCCAM_HOST` | `hermes` | Legacy **fallback** snippet preference (`hermes` \| `cursor`) — does not replace connect |
| `OCCAM_INSTALL_DIR` | `~/.local/share/ff-occam` | Install root |
| `OCCAM_VERSION` | `1.0.0-rc.2` | Release version |

## Do not

| Wrong | Why |
|-------|-----|
| `npx @ff-occam/mcp` | **Not** a GA 1.0 install channel |
| Trust Cosign bundle alone | Installers do **not** verify Cosign; use SHA-256 manifest |
| Bare clone without .NET 10 | Source only — no AOT binary without doctor build |

Contributor clone + build: see root [INSTALL.md](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md#advanced--contributors).

## Next

- [Connect your AI](connect/index.md)
- [Your first web read](getting-started.md)
- [Troubleshooting](troubleshooting.md)
- [Trust: installation safety](trust/installation-safety.md)
