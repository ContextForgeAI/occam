# Connect your AI

Occam’s **connect platform** is a first-class product capability: after install, Occam detects supported AI/MCP hosts and registers itself where it is safe to do so.

```bash
occam connect
```

## Flow

```text
installer
  → occam connect
  → detect hosts / runtimes
  → safe auto-connect (live-validated)
  → verify
  → report Ready / Almost ready / Action required / Not ready
```

## Support tiers (honest)

| Public name | Connect behavior |
|-------------|------------------|
| **Live validated** | Configured automatically when detected |
| **Config validated** | Implemented + tested against published config shape; needs `occam connect --only <id>` until live end-to-end proof exists |
| **Assisted** | Detected; paste / wizard guidance only |
| **Model runtimes** | Detected for awareness; **never** registered (not MCP hosts) |

Occam does **not** claim to support every MCP client.

Full host table: [Supported hosts](../mcp-hosts.md)

## Status meanings

| Status | Meaning |
|--------|---------|
| Ready | Configured and confirmed |
| Almost ready | Configured — restart the named app |
| Action required | Host needs trust/paste/choice — **not** a broken Occam install |
| Not ready | Occam itself could not start |

## In this section

- [Automatic connection](automatic.md)
- [Explicit `--only` hosts](explicit-only.md)
- [Manual / generic MCP](manual.md)
- [What happened after install?](after-install.md)
- [Connection troubleshooting](troubleshooting.md)
