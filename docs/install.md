# Install

Canonical agent/operator install reference also lives at the repository root:
[`INSTALL.md`](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md).
This page is the documentation-site copy of the same happy path.

**Requirements:** Node.js **20+**. No .NET SDK on the install machine.  
**Published release:** `1.0.0-rc.2`  
**Next candidate in this tree:** `1.0.0-rc.3` (not published yet)

---

## Supported release channel

The supported release channel is a GitHub Release tarball plus the bootstrap
scripts. The bootstrap downloads `ff-occam-<ver>-<rid>.tar.gz`, verifies its
manifest and SHA-256, runs doctor, and connects your MCP host.

| Channel | Supported? | Notes |
|---------|------------|-------|
| `get-ff-occam.sh` / `get-ff-occam.ps1` bootstrap | **Yes** | Recommended release path |
| Manual tarball + manifest from GitHub Releases | **No** | Integrity inspection only; bypasses guarded install and onboarding |
| `git clone` + doctor (contributors) | Build path | Requires .NET 10 SDK |
| `npx @ff-occam/mcp` | **No** | Not a supported release channel |
| Cosign `.bundle` on Releases | **Not enforced** | May exist as release metadata; **no shipped install path verifies Cosign**. Integrity check is **SHA-256 vs the release manifest only**. |

Public binaries are published for exactly `win-x64`, `linux-x64`, and
`osx-arm64`. Intel macOS, Linux ARM64, Windows ARM64, and other architectures are
not release-install targets. The bootstrap rejects them before any network
request. `OCCAM_RID` accepts only those three published values; it is not an
emulation or cross-platform compatibility switch.

---

## One command

=== "Linux x64 / macOS Apple Silicon"

    ```bash
    curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
    ```

=== "Windows x64"

    ```powershell
    irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
    ```

## What the installer does

1. Downloads `ff-occam-<ver>-<rid>.tar.gz` + `ff-occam-<ver>-<rid>-manifest.json` from GitHub Releases
2. Requires the manifest version, RID, and tarball name to match the request, then verifies the archive **SHA-256** (**not** Cosign)
3. Extracts to staging and validates the platform host, `VERSION`, inner manifest, and bundled runtime helpers before replacing `OCCAM_INSTALL_DIR`. An existing target must itself be a consistent Level B Occam release for the current RID; source checkouts, links/reparse points, and unknown directories are refused before processes stop or files move
4. Uses only helpers inside that verified archive; no mutable post-install source overlay is fetched
5. Runs **doctor** (`--skip-build`) — npm workers, Playwright Chromium, host binary check (quiet by default)
6. Verifies the Occam host — expect **15** core `occam_*` tools
7. Writes onboard defaults → `~/.occam/onboard.json` (known install path; no re-prompt)
8. Installs the user launcher transactionally. It replaces only exact Occam-generated current or previous-release launchers and refuses unrelated `occam`, `occam.cmd`, or `occam.ps1` files
9. Runs **`occam connect`** for live-validated AI/MCP hosts (one host auto; multiple confirm first)
10. Reports **Ready** only after host verification (or Installed / Almost ready / Action required)
11. Keeps the previous release tree through all post-install checks; failure stops processes from the new tree and restores the previous tree (or removes a failed fresh tree)

`OCCAM_VERBOSE=1` shows doctor/smoke internals. Human walkthrough: [Quick Start](quick-start.md) · Hosts: [MCP hosts](mcp-hosts.md)

## What install mutates

| Location | What gets written |
|----------|-------------------|
| `OCCAM_INSTALL_DIR` / `OCCAM_HOME` | Release tree: `scripts/`, `workers/`, AOT host binary |
| `~/.occam/onboard.json` | Operator defaults merged into every launcher invocation |
| `~/.occam/keys/signing-key.pem` | Minted on **first host start** (even when `OCCAM_RECEIPTS=off`) |
| Host MCP config files | `occam connect` registrations + `*.occam-bak` backups |
| Playwright browser cache | Chromium downloaded by doctor when needed |

**Uninstall honesty:** removing the install directory alone does **not** remove `~/.occam/`, host MCP configs, skill directories, or Playwright cache. Use `occam uninstall --dry-run` before the scoped removal flow below. See [Trust: installation safety](trust/installation-safety.md).

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

## Disconnect or uninstall

Start with a read-only preview:

```bash
occam disconnect --dry-run
occam uninstall --dry-run
```

`occam disconnect` removes only host registrations recognized as Occam-managed.
`occam uninstall` disconnects those hosts, removes the generated launcher, and
then removes a recognized release install tree. The preview also inventories the
opt-in response cache and shared Playwright cache. Unmanaged entries, source
checkouts, backups, skills, both caches, and `~/.occam/` state are preserved by
default.

```bash
occam disconnect
occam uninstall
```

Use `occam uninstall --remove-cache` to delete the narrow Occam response cache
(`OCCAM_CACHE_DIR`, default `{TEMP}/occam-cache`). The shared Playwright browser
cache is never removed automatically.

Use `occam uninstall --remove-state` only when you also intend to delete signing
keys, session profiles, and operator settings. See
[Installation and connect safety](trust/installation-safety.md#disconnect-and-uninstall).

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
| `OCCAM_HOST` | (none) | Legacy **fallback** snippet preference (`hermes` \| `cursor`) — does not replace connect |
| `OCCAM_INSTALL_DIR` | `~/.local/share/ff-occam` | Install root |
| `OCCAM_VERSION` | `1.0.0-rc.3` (candidate default; published channel remains `1.0.0-rc.2` until cut) | Release version |
| `OCCAM_RID` | detected | Published RID override: `win-x64` \| `linux-x64` \| `osx-arm64` only |

## Do not

| Wrong | Why |
|-------|-----|
| `npx @ff-occam/mcp` | **Not** a supported release channel |
| Trust Cosign bundle alone | Installers do **not** verify Cosign; use SHA-256 manifest |
| Bare clone without .NET 10 | Source only — no AOT binary without doctor build |

Contributor clone + build: see root [INSTALL.md](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md#advanced--contributors).

## Next

- [Connect your AI](connect/index.md)
- [Your first web read](getting-started.md)
- [Troubleshooting](troubleshooting.md)
- [Trust: installation safety](trust/installation-safety.md)
