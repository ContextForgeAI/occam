# Install

Canonical agent/operator install reference also lives at the repository root:
[`INSTALL.md`](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md).
This page is the documentation-site copy of the same happy path.

**Requirements:** Node.js **20+**. No .NET SDK on the install machine.  
**Current release:** `1.0.0-rc.2`

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

1. Downloads `ff-occam-<ver>-<rid>.tar.gz` + manifest  
2. Verifies **SHA-256**  
3. Runs **doctor** (workers + Playwright)  
4. Verifies the Occam host — expect **15** core `occam_*` tools  
5. Runs onboard defaults  
6. Runs **`occam connect`** for live-validated AI/MCP hosts  
7. Reports Ready / Almost ready / Action required / Not ready (manual snippet only as fallback)

Human walkthrough: [Quick Start](quick-start.md) · Hosts: [MCP hosts](mcp-hosts.md)

## Verify

```bash
export OCCAM_HOME="${OCCAM_INSTALL_DIR:-$HOME/.local/share/ff-occam}"
export PATH="$OCCAM_HOME/scripts:$PATH"
occam smoke
occam connect
```

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
| `npx @ff-occam/mcp` | Not part of this RC |
| Bare clone without .NET 10 | Source only — no AOT binary |

Contributor clone + build: see root [INSTALL.md](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md#advanced--contributors).

## Next

- [Connect your AI](connect/index.md)
- [Your first web read](getting-started.md)
- [Troubleshooting](troubleshooting.md)
