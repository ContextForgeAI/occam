# Operators

The operator surface is a **first-class** product system — not a developer footnote.

**Status:** USABLE_WITH_LIMITATIONS

## What you run

| Surface | Role |
|---------|------|
| **Install / bootstrap** | `get-ff-occam.*` → release tarball → SHA-256 verify → doctor → connect |
| **Doctor** | Workers, Playwright, host sanity |
| **`occam` CLI wrapper** | Subcommands: connect, session, keys, verify verbs, refresh/control/update helpers |
| **MCP host process** | stdio (default), optional WebSocket / remote / batch server |
| **Connect platform** | detect → classify → backup → configure → verify → restart/action → rollback (limits apply) |

## Install channels (honest)

| Channel | Docs v3 status |
|---------|----------------|
| GitHub Release tarball + bootstrap scripts | **GA path for 1.0** |
| Source / `dotnet` contributor builds | Developer |
| **npm `@ff-occam/mcp`** | **NOT GA** — INTERNAL / EXPERIMENTAL until end-to-end install contract passes |
| Docker image | USABLE_WITH_LIMITATIONS if published; healthcheck uses a non-blocking CLI verb |
| Cosign `.bundle` | Release metadata; **installers do not enforce Cosign** |

## Connect

Preserve the Docs v2 flow, with honesty:

1. Detect installed hosts  
2. Classify support tier (live / config / assisted / runtime-only)  
3. Backup existing MCP config  
4. Configure  
5. Verify where the tier allows  
6. Tell you restart / trust / paste actions  
7. Rollback **where implemented** — restart-required file hosts do **not** get a universal atomic rollback guarantee  

Details: [Connect](connect/index.md) · [MCP hosts](mcp-hosts.md)

## Runtime modes

See [Transports](transports.md): stdio, WebSocket, remote WSS+JWT, batch HTTP.

## Related

- [Install](install.md) · root [INSTALL.md](https://github.com/ContextForgeAI/occam/blob/main/INSTALL.md)
- [Configuration](configuration.md)
- [Troubleshooting](troubleshooting.md)
- [Experimental](experimental.md)
- Handbook: Operating an install
