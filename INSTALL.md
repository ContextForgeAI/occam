# Install — start here (agents & operators)

> **If you are an automated agent:** run **only** the canonical command for your OS below.
> Do **not** edit `FFOccamMcp.Core.csproj`, do **not** install .NET 8, do **not** run
> `npx @ff-occam/mcp`, and do **not** invent alternate install paths.

**Product:** Occam (Occam MCP)  
**Repo:** [https://github.com/ContextForgeAI/occam](https://github.com/ContextForgeAI/occam)  
**Docs site:** [https://contextforgeai.github.io/occam/](https://contextforgeai.github.io/occam/)

**Requirements:** Node.js **20+** (installer also checks Homebrew locations `/opt/homebrew/bin` and `/usr/local/bin` when `node` is not already on `PATH`). No .NET SDK on the install machine.  
**Cosign:** required when the release manifest declares `signaturePolicy=required-cosign-v1` (published `v1.0.0-rc.3`+). Install from [Sigstore Cosign docs](https://docs.sigstore.dev/cosign/system_config/installation/) before bootstrap if Cosign is not already on `PATH`. Authenticity ≠ page-content truth.

**Published release:** `1.0.0` (GitHub Release tag `v1.0.0`).
**Public install default** (unset `OCCAM_VERSION`): **`1.0.0`**.

---

## Canonical install (one path)

### Linux x64 / macOS Apple Silicon

```bash
curl -fsSL https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.sh | bash
```

### Windows x64 (PowerShell)

```powershell
irm https://raw.githubusercontent.com/ContextForgeAI/occam/main/scripts/get-ff-occam.ps1 | iex
```

### Supported release channel

The supported release channel is a GitHub Release tarball plus the bootstrap
scripts below. Manual extraction is not a supported install path.

| Channel | Supported? | Notes |
|---------|------------|-------|
| Bootstrap scripts (`get-ff-occam.*`) | **Yes** | Recommended release path |
| Manual tarball + `*-manifest.json` | **No** | Integrity inspection only; bypasses guarded install and onboarding |
| `git clone` + doctor | Build path | Requires .NET 10 SDK |
| `npx @ff-occam/mcp` | **No** | Not a supported release channel |
| Cosign `.bundle` on Releases | **Policy-gated** | Always SHA-256 vs manifest. When the manifest declares `signaturePolicy=required-cosign-v1` (published `v1.0.0-rc.3`+), the installer verifies the Cosign bundle fail-closed and **requires the `cosign` CLI on PATH**. Undeclared / `sha256-only` (published `v1.0.0-rc.2`) stays SHA-256-only. Authenticity ≠ page-content truth. |

Public binaries are published for exactly `win-x64`, `linux-x64`, and
`osx-arm64`. Intel macOS, Linux ARM64, Windows ARM64, and other architectures are
not release-install targets. The bootstrap rejects them before any network
request. `OCCAM_RID` accepts only those three published values; it is not an
emulation or cross-platform compatibility switch.

### What the bootstrap does

Install behavior follows the **release manifest contract** (not the version string alone):

| Manifest | Contract |
|----------|----------|
| no `runtimeLayout` (published `v1.0.0-rc.2`) | Legacy Level B — SHA-256; operator CLI may refresh from the repository overlay |
| `runtimeLayout=self-contained-v1` (published `v1.0.0-rc.3`+) | Self-contained — SHA-256 + archive preflight + runtime closure; **no** executable helper overlay; Cosign when `signaturePolicy=required-cosign-v1` |
| unknown `runtimeLayout` / unknown `signaturePolicy` | Fail closed |

**Public default** (unset `OCCAM_VERSION`): **`1.0.0`**. Set `OCCAM_VERSION=1.0.0-rc.5`, `1.0.0-rc.4`, `1.0.0-rc.3`, or `1.0.0-rc.2` only when you intentionally need an older channel.

1. Downloads `ff-occam-<ver>-<rid>.tar.gz` + `ff-occam-<ver>-<rid>-manifest.json` from GitHub Releases (or `OCCAM_RELEASE_BASE`)
2. Requires the manifest version, RID, and tarball name to match the requested release, then verifies the archive **SHA-256**. When `signaturePolicy=required-cosign-v1` is declared, also verifies the Cosign bundle fail-closed (legacy undeclared/`sha256-only` stays SHA-256-only). For self-contained manifests, archive-member preflight runs **before** extract
3. Extracts to staging. Self-contained installs check the platform host, `VERSION`, inner manifest, and bundled runtime helpers before replacing `OCCAM_INSTALL_DIR` (default `~/.local/share/ff-occam`). An existing target must itself be a consistent Occam release for the current RID (inner `layout: level-b` markers); source checkouts, links/reparse points, and unknown directories are refused before processes stop or files move
4. **Self-contained:** uses only helpers inside that verified archive (no mutable post-install executable helper overlay). **Legacy Level B:** may refresh operator CLI helpers from the repository overlay. Bootstrap **script** delivery from the mutable `main` raw URL remains a separate T4 concern
5. Runs **doctor** (`--skip-build`) — npm workers + Playwright (quiet by default)
6. Verifies the Occam host (`verify-install` + smoke) — expect the profile's required tool identities (default `reader` = **8**; `full` = **15**)
7. Writes operator defaults to `~/.occam/onboard.json` (no second `OCCAM_HOME` prompt)
8. Installs a user-scoped **`occam`** launcher (`~/.local/bin`; Windows: `occam.cmd` + `occam.ps1`) and prepends that directory to the **User** PATH (and the current shell PATH) so `occam` resolves immediately after install. Existing launchers are replaced only when they exactly match an Occam-generated current or previous-release launcher; unrelated same-named files stop the install, and multi-file launcher updates roll back as one transaction
9. Runs **`occam connect`** — detects AI/MCP hosts; one host auto-connects; multiple hosts confirm first (or `OCCAM_CONNECT_ALL=1` for automation)
10. Reports **Ready** only after host verification — or **Installed** / **Almost ready** / **Action required** as appropriate
11. Keeps the previous release tree until doctor, self-check, launcher setup, and Connect finish; a failure stops processes from the new tree and restores the previous tree (or removes a failed fresh tree)

Default output is quiet (~15–25 lines). Internals: `OCCAM_VERBOSE=1`.  
After install, `occam connect` / `occam doctor` resolve without a manual PATH export.

**What install mutates:** install tree under `OCCAM_HOME`, `~/.occam/onboard.json`, signing key on first host start (`~/.occam/keys/`), host MCP configs (+ backups), Playwright cache. Removing the install directory alone does **not** remove all `~/.occam/` state or host configs; use the scoped `occam uninstall --dry-run` flow below.

Human walkthrough: [docs/quick-start.md](docs/quick-start.md) · Host tiers: [docs/mcp-hosts.md](docs/mcp-hosts.md) · Safety: [docs/trust/installation-safety.md](docs/trust/installation-safety.md)

Optional env (compatibility — same on all platforms):

| Variable | Default | Purpose |
|----------|---------|---------|
| `OCCAM_SETUP` | `auto` (when unset) | `auto` \| `manual` \| `ask` — setup-mode selection does not prompt by default; connecting multiple detected hosts may still require confirmation |
| `OCCAM_CONNECT_ALL` | unset | `1` — non-interactive installs may configure every detected Tier-A host; without it, multiple hosts are left for an explicit confirm / `occam connect` |
| `OCCAM_VERBOSE` | unset | `1` — show doctor/smoke/connect internals during install |
| `OCCAM_HOST` | (none) | Legacy preference for the **fallback** connection snippet only (`hermes` or `cursor`) — not a phantom pre-selected host |
| `OCCAM_INSTALL_DIR` | `~/.local/share/ff-occam` | Install root |
| `OCCAM_VERSION` | `1.0.0` (public default; published GitHub Release) | Release version; set an older tag only for a legacy channel |
| `OCCAM_RID` | detected | Published RID override: `win-x64` \| `linux-x64` \| `osx-arm64` only |

`OCCAM_HOST` does **not** replace `occam connect`. Prefer letting connect detect and configure validated hosts.

Interactive setup menu (Enter = Auto): set `OCCAM_SETUP=ask`.

---

## Verify

```bash
# After one-line install, `occam` is already on PATH via ~/.local/bin.
occam smoke
# or: node "${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}/scripts/hermes-smoke.mjs"
```

```powershell
# After one-line install, `occam` is already on PATH via %USERPROFILE%\.local\bin.
occam smoke
```

Expect **exit 0**. Tool count follows `OCCAM_PROFILE` (default `reader` = **8**; `full` = **15**).

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

## Disconnect or uninstall

Preview host-registration removal without changing anything:

```bash
occam disconnect --dry-run
```

Then remove only registrations that carry Occam's ownership marker or point at
the Occam launcher:

```bash
occam disconnect
# One host only:
occam disconnect --only cursor
```

Preview the complete uninstall before running it:

```bash
occam uninstall --dry-run
occam uninstall
```

Default uninstall disconnects managed hosts, removes the generated `occam`
launcher, and removes a recognized release install tree. Its preview inventories
the opt-in response cache and shared Playwright cache, but preserves both by
default. It does **not** delete source checkouts, unmanaged host entries,
`*.occam-bak` files, installed skills, the Playwright cache, or `~/.occam/` state.

Delete only Occam's flat response cache (`OCCAM_CACHE_DIR`, default
`{TEMP}/occam-cache`) with an explicit scope:

```bash
occam uninstall --dry-run --remove-cache
occam uninstall --remove-cache
```

The shared Playwright browser cache is never removed automatically because other
Playwright applications may use it.

To also delete local state — including signing keys, session profiles, and
operator settings — make that destructive scope explicit:

```bash
occam uninstall --dry-run --remove-cache --remove-state
occam uninstall --remove-cache --remove-state
```

The command fails closed on relative, broad, symlinked, malformed, or ambiguous
targets. Details: [installation safety](docs/trust/installation-safety.md#disconnect-and-uninstall).

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
| Trust Cosign without reading `signaturePolicy` | Always verify SHA-256. Cosign is required only when the manifest declares `required-cosign-v1`; it proves release authenticity/signer identity, not page-content truth |
| `npm ci` / `npm run bootstrap` at repo root | Does not exist — doctor installs workers |
| Bare `git clone` without .NET 10 SDK | Source only — no AOT binary |
| `git clone` + `doctor --skip-build` without a release binary | Fails — no host binary |
| Edit `TargetFramework` to net8.0 | Must stay `net10.0` |

---

## Maintainer: publish a GitHub Release

Do not create the next `v<semver>` tag until all of these are true:

- CI is green on the exact commit and that commit is contained in `main`.
- The GitHub `github-release` environment exists and requires an independent
  reviewer for deployments from version tags.
- GitHub immutable Releases are enabled for the repository.
- The versioned bootstrap defaults and release notes point at the same tag.

The `occam-release` workflow always builds exactly `linux-x64`, `osx-arm64`, and
`win-x64`. Pull requests and `main` exercise those builds with read-only
repository permissions. A tag run gives write and OIDC permissions only to the
protected publish job after all three builds pass.

The publish job downloads the exact six archive/manifest workflow artifacts,
revalidates every manifest SHA-256 binding, signs exactly three archives with
keyless Cosign, verifies the expected workflow identity and tamper rejection,
and creates one **draft** containing exactly nine assets. It verifies that draft
before changing `draft` to `false` once. It never uploads assets after
publication, so the flow is compatible with immutable Releases.

If the job fails before publication, there is no visible partial Release. A
failed run can leave a private draft; the next run intentionally refuses to
overwrite any existing draft or Release for the tag. Inspect and remove that
draft only after the failure is understood. `sign-release.yml` is verification
only; it cannot sign or modify a Release.

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

### Mirrors and offline environments

Do not extract a release archive over an existing install by hand. That skips
manifest binding, pre-swap runtime checks, process-safe replacement, launcher
setup, doctor, and Connect.

For a connected internal mirror, host the versioned archive and manifest over
HTTPS, audit a bootstrap copied from the same immutable release tag, and set
`OCCAM_RELEASE_BASE` plus `OCCAM_VERSION` before running that local bootstrap.
The current installer still downloads npm and Playwright dependencies when they
are absent, so Occam does **not** ship a complete air-gap installer.

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
