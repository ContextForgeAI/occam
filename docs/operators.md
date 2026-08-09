# Operators

Use this page to keep an Occam release install connected, healthy, current, and
removable. Occam Core `1.0.0-rc.2` is a release candidate, not GA.

## What you run

| Surface | Role |
|---------|------|
| **Install / bootstrap** | `get-ff-occam.*` → release tarball → SHA-256 verify → doctor → connect |
| **Doctor** | Workers, Playwright, host sanity |
| **`occam` CLI wrapper** | Connect/disconnect, reversible uninstall, session, verify, refresh/control/update helpers |
| **MCP host process** | stdio (default), optional WebSocket / remote / batch server |
| **Connect platform** | detect → classify → backup → configure → verify → restart/action → rollback (limits apply) |

## Supported surfaces

| Surface | Status |
|---------|--------|
| GitHub Release tarball + bootstrap scripts | Recommended release channel |
| Source / `dotnet` contributor builds | Contributor path; requires .NET 10 SDK |
| npm `@ff-occam/mcp` | Experimental; not the supported release install |
| Docker image | No documented public release channel |
| Cosign `.bundle` | Release metadata; shipped installers do not verify it |

## Connect

Connect follows this flow:

1. Detect installed hosts  
2. Classify support tier (live / config / assisted / runtime-only)  
3. Backup existing MCP config  
4. Configure  
5. Verify where the tier allows  
6. Tell you restart / trust / paste actions  
7. Rollback **where implemented** — restart-required file hosts do **not** get a universal atomic rollback guarantee  

Details: [Connect](connect/index.md) · [MCP hosts](mcp-hosts.md)

## Disconnect and uninstall

`occam disconnect --dry-run` previews owned host registrations; `occam
disconnect` removes only those registrations. `occam uninstall --dry-run`
previews the wider removal, and `occam uninstall` removes generated launchers
plus a recognized release tree after disconnect succeeds.

Local `~/.occam/` state is preserved unless `--remove-state` is explicit. The
opt-in response cache is inventoried and requires `--remove-cache`; the shared
Playwright cache is never removed automatically. Unmanaged host entries, source
checkouts, skills, Playwright cache, and backups are never part of the default
removal scope. Details:
[Installation safety](trust/installation-safety.md#disconnect-and-uninstall).

## Runtime modes

See [Transports](transports.md): stdio, WebSocket, remote WSS+JWT, batch HTTP.

## Related

- [Install](install.md) · root [INSTALL.md](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md)
- [Configuration](configuration.md)
- [Troubleshooting](troubleshooting.md)
- [Experimental](experimental.md)
